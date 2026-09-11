#nullable enable
// Создано: 2026-05-09 00:00:00 UTC
// Точка входа модуля инвентаря.
// IStartable — инициализация сервисов.
// Migrated from Ai-game3 (Unity+VContainer+MessagePipe) to Ai-game4 (Godot+DI+EventBus) 2026-08-15:
//   - using MessagePipe → using CultivationGame.Core.Events
//   - using VContainer/VContainer.Unity → using CultivationGame.Core.DI / CultivationGame.Core.Interfaces
//   - Handler signature: void OnXxx(XxxEvent e) → void OnXxx(in XxxEvent e)
//   - UnityEngine.Debug.Log → Console.WriteLine
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Helpers;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Inventory;

/// <summary>
/// Точка входа модуля инвентаря.
/// Инициализирует сервисы конфигурацией и подписывается на события.
/// </summary>
public class InventoryModule : IModule
{
    [Inject] private readonly InventoryService _inventoryServiceImpl = null!;
    [Inject] private readonly CraftingService _craftingServiceImpl = null!;
    [Inject] private readonly EquipmentService _equipmentServiceImpl = null!;
    [Inject] private readonly MaterialService _materialService = null!;
    [Inject] private readonly BackpackService _backpackService = null!;
    [Inject] private readonly StorageRingService _storageRingService = null!;
    [Inject] private readonly IItemDatabaseService _itemDatabase = null!;
    [Inject] private readonly IGroundItemService _groundItemService = null!;
    [Inject] private readonly IPlayerService _playerService = null!;

    [Inject] private readonly ISubscriber<ResourceHarvestedEvent> _resourceHarvestedSub = null!;
    [Inject] private readonly ISubscriber<ItemAddRequestEvent> _itemAddRequestSub = null!;
    [Inject] private readonly ISubscriber<EquipmentChangedEvent> _equipChangedSub = null!;
    [Inject] private readonly ISubscriber<CraftCompletedEvent> _craftCompletedSub = null!;

    // IMPL-3: Config injected via DI (replaces obsolete SetConfig()).
    [Inject] private readonly InventoryConfig _config = null!;

    [Inject] private readonly BeltService _beltService = null!;

    private IDisposable? _resourceHarvestedSubscription;
    private IDisposable? _itemAddRequestSubscription;
    private IDisposable? _equipChangedSubscription;
    private IDisposable? _craftCompletedSubscription;

    public string ModuleName => "Inventory";

    public void Start()
    {
        _inventoryServiceImpl.Configure(_config);
        _craftingServiceImpl.RegisterRecipes(_config.Recipes);

        _equipmentServiceImpl.Initialize("player");
        _materialService.InitializeDefaults();
        _backpackService.Initialize();
        _storageRingService.Initialize();
        _beltService.Initialize();

        // === Подписка на кросс-модульные события ===
        _resourceHarvestedSubscription = _resourceHarvestedSub.Subscribe(OnResourceHarvested);
        _itemAddRequestSubscription = _itemAddRequestSub.Subscribe(OnItemAddRequest);
        _equipChangedSubscription = _equipChangedSub.Subscribe(OnEquipmentChanged);
        _craftCompletedSubscription = _craftCompletedSub.Subscribe(OnCraftCompleted);
    }

    public void Tick(int tickCount)
    {
        // Inventory has no per-tick work
    }

    private void OnResourceHarvested(in ResourceHarvestedEvent e)
    {
        if (string.IsNullOrEmpty(e.ItemId) || e.Amount <= 0) return;

        if (_itemDatabase.TryGetItem(e.ItemId, out var itemData))
        {
            // AUDIT-0911 INV-17 FIX: раньше 2-arg TryAddItem — при volume-full
            // ресурс ИСЧЕЗАЛ бесследно (тайл уже истощён до публикации —
            // TileService исчерпывает ДО события, откатить нельзя). Тот же
            // компенсирующий overflow-паттерн, что в OnItemAddRequest/
            // OnCraftCompleted (Review этап 4 P1-4): остаток — на землю.
            bool added = _inventoryServiceImpl.TryAddItem(itemData, e.Amount, out int addedCount);
            int overflow = e.Amount - addedCount;
            if (overflow > 0)
            {
                DropItemsNearPlayer(e.ItemId, overflow);
                Console.WriteLine($"[InventoryModule] Harvest '{e.ItemId}': {addedCount} в инвентарь, {overflow} на землю (инвентарь полон)");
            }
        }
        else
        {
            Console.WriteLine($"[InventoryModule] Предмет '{e.ItemId}' не найден в ItemDatabase (harvest)");
        }
    }

    private void OnItemAddRequest(in ItemAddRequestEvent e)
    {
        if (string.IsNullOrEmpty(e.ItemId) || e.Count <= 0) return;

        if (_itemDatabase.TryGetItem(e.ItemId, out var itemData))
        {
            // Try to add to inventory. If volume full, drop overflow on ground.
            bool added = _inventoryServiceImpl.TryAddItem(itemData, e.Count, out int addedCount);
            int overflow = e.Count - addedCount;
            if (overflow > 0)
            {
                // Drop overflow items on ground near player.
                DropItemsNearPlayer(e.ItemId, overflow);
            }
        }
        else
        {
            Console.WriteLine($"[InventoryModule] Предмет '{e.ItemId}' не найден в ItemDatabase (source={e.Source})");
        }
    }

    /// <summary>
    /// Drop items on the ground near the player position.
    /// Used when inventory volume is full and items can't be added.
    /// </summary>
    private void DropItemsNearPlayer(string itemId, int count)
    {
        if (_playerService == null || _groundItemService == null) return;

        var playerPos = _playerService.Position;
        // Convert tile position to pixel position (center of tile).
        float pixelX = playerPos.X * GameConstants.TILE_PIXELS + GameConstants.TILE_PIXELS / 2f;
        float pixelY = playerPos.Y * GameConstants.TILE_PIXELS + GameConstants.TILE_PIXELS / 2f;

        // Small random offset so items don't stack on same pixel.
        var rng = new System.Random((int)System.DateTime.UtcNow.Ticks);
        float offsetX = (float)(rng.NextDouble() - 0.5) * 30f;
        float offsetY = (float)(rng.NextDouble() - 0.5) * 30f;

        long dropId = _groundItemService.DropItem(itemId, count, pixelX + offsetX, pixelY + offsetY);
        Console.WriteLine($"[InventoryModule] Dropped {itemId}×{count} on ground at ({pixelX + offsetX:F0}, {pixelY + offsetY:F0}), dropId={dropId}");
    }

    private void OnEquipmentChanged(in EquipmentChangedEvent e)
    {
        if (string.IsNullOrEmpty(e.OldItemId)) return;
        // AUDIT-0911: PlayerIdResolver вместо строгого == "player" (хрупко
        // при смешении алиасов; Adapter-аudit кросс-слойное замечание).
        if (!PlayerIdResolver.IsPlayer(e.EntityId)) return;

        if (_itemDatabase.TryGetItem(e.OldItemId, out var oldItem))
        {
            // AUDIT-0911 INV-2 FIX: unequip/замена экипировки при volume-full:
            // 2-arg TryAddItem молча возвращал false → снятый предмет ИСЧЕЗАЛ
            // (комментарий CharacterDollPanel «дропнет излишек на землю» — ложь).
            // Теперь: сколько влезло — в инвентарь, остаток — на землю рядом
            // с игроком (снятое нельзя потерять).
            bool added = _inventoryServiceImpl.TryAddItem(oldItem, 1, out int addedCount);
            if (addedCount < 1)
            {
                DropItemsNearPlayer(e.OldItemId, 1);
                Console.WriteLine($"[InventoryModule] Возврат предмета '{e.OldItemId}' из слота {e.Slot}: инвентарь полон — выброшен на землю");
            }
            else
            {
                Console.WriteLine($"[InventoryModule] Возврат предмета '{e.OldItemId}' в инвентарь из слота {e.Slot}");
            }
        }
    }

    private void OnCraftCompleted(in CraftCompletedEvent e)
    {
        if (string.IsNullOrEmpty(e.ResultItemId)) return;

        if (_itemDatabase.TryGetItem(e.ResultItemId, out var resultItem))
        {
            // Review этап 4 (P1-4): результат крафта НЕ теряется при полном
            // инвентаре — overflow выбрасывается на землю у игрока (тот же
            // компенсирующий путь, что и у pickup-overflow; раньше TryAddItem
            // игнорировался и предмет «исчезал» после списания ингредиентов).
            bool added = _inventoryServiceImpl.TryAddItem(resultItem, e.Count, out int addedCount);
            int overflow = e.Count - addedCount;
            if (overflow > 0)
            {
                DropItemsNearPlayer(e.ResultItemId, overflow);
                Console.WriteLine($"[InventoryModule] Результат крафта '{e.ResultItemId}': {addedCount} в инвентарь, {overflow} на землю (инвентарь полон)");
            }
            else
            {
                Console.WriteLine($"[InventoryModule] Результат крафта '{e.ResultItemId}' добавлен в инвентарь (added={added})");
            }
        }
        else
        {
            Console.WriteLine($"[InventoryModule] Результат крафта '{e.ResultItemId}' не найден в ItemDatabase");
        }
    }

    public void Dispose()
    {
        _resourceHarvestedSubscription?.Dispose();
        _resourceHarvestedSubscription = null;
        _itemAddRequestSubscription?.Dispose();
        _itemAddRequestSubscription = null;
        _equipChangedSubscription?.Dispose();
        _equipChangedSubscription = null;
        _craftCompletedSubscription?.Dispose();
        _craftCompletedSubscription = null;
    }
}

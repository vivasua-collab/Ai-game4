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

    // BUG-022/Этап-6: подписка на ResourceHarvestedEvent УДАЛЕНА — при
    // подключённом ResourceService предметы добычи добавлялись дважды
    // (command-событие от ResourceService.Harvest + факт-событие от
    // TileService). См. комментарий в Start().
    [Inject] private readonly ISubscriber<ItemAddRequestEvent> _itemAddRequestSub = null!;
    [Inject] private readonly ISubscriber<EquipmentChangedEvent> _equipChangedSub = null!;
    [Inject] private readonly ISubscriber<CraftCompletedEvent> _craftCompletedSub = null!;

    // IMPL-3: Config injected via DI (replaces obsolete SetConfig()).
    [Inject] private readonly InventoryConfig _config = null!;

    [Inject] private readonly BeltService _beltService = null!;

    private IDisposable? _itemAddRequestSubscription;
    private IDisposable? _equipChangedSubscription;
    private IDisposable? _craftCompletedSubscription;

    public string ModuleName => "Inventory";

        // P1-10 (аудит 09.22, Фазы 7/10): идемпотентный Start — повторный вызов
    // (прямой вызов вне GameEntryPoint / повторная инстанциация сцены) не
    // дублирует подписки/инициализацию. Флаг — только при УСПЕШНОМ завершении
    // (провал остаётся ретраимым). Основной слой защиты — GameEntryPoint
    // (_startedModules, resume from failure); это страховка от прямых вызовов.
    private bool _startCompleted;

    public void Start()
    {
        if (_startCompleted) return;

        _inventoryServiceImpl.Configure(_config);
        _craftingServiceImpl.RegisterRecipes(_config.Recipes);

        _equipmentServiceImpl.Initialize("player");
        _materialService.InitializeDefaults();
        _backpackService.Initialize();
        _storageRingService.Initialize();
        _beltService.Initialize();

        // === Подписка на кросс-модульные события ===
        // BUG-022/Этап-6 (дабл-адд добычи): подписка на ResourceHarvestedEvent
        // УДАЛЕНА. Прежне при подключённом ResourceService (прод-конфигурация)
        // предметы добавлялись ДВАЖДЫ: ResourceService.Harvest публикует
        // ItemAddRequestEvent("harvest") → OnItemAddRequest добавляет, а затем
        // TileService публикует ResourceHarvestedEvent → OnResourceHarvested
        // добавлял ЕЩЁ РАЗ тот же объём. Канонический путь поступления —
        // command-событие ItemAddRequestEvent (EVT-01, Review этап-6 P2-5);
        // ResourceHarvestedEvent — ФАКТ для UI/лога (EventLogWindow), а не
        // команда. INV-17-защита (предмет не исчезает при volume-full) —
        // идентичный overflow-паттерн живёт в OnItemAddRequest.
        _itemAddRequestSubscription = _itemAddRequestSub.Subscribe(OnItemAddRequest);
        _equipChangedSubscription = _equipChangedSub.Subscribe(OnEquipmentChanged);
        _craftCompletedSubscription = _craftCompletedSub.Subscribe(OnCraftCompleted);
    
        _startCompleted = true;
    }

    public void Tick(int tickCount)
    {
        // Inventory has no per-tick work
    }

    private void OnItemAddRequest(in ItemAddRequestEvent e)
    {
        if (string.IsNullOrEmpty(e.ItemId) || e.Count <= 0) return;

        if (_itemDatabase.TryGetItem(e.ItemId, out var itemData))
        {
            // Try to add to inventory. If volume full, drop overflow on ground.
            // BUG-022 (Этап-7): источник запроса (harvest/loot/pickup/quest_reward...)
            // пробрасывается в ItemAddedEvent — цель GatherItem отличает добычу
            // от покупки/награды. Пустой источник = ItemSources.World (легаси).
            string source = string.IsNullOrEmpty(e.Source) ? ItemSources.World : e.Source;
            bool added = _inventoryServiceImpl.TryAddItem(itemData, e.Count, out int addedCount, source);
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
            bool added = _inventoryServiceImpl.TryAddItem(oldItem, 1, out int addedCount, ItemSources.EquipReturn);
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
            bool added = _inventoryServiceImpl.TryAddItem(resultItem, e.Count, out int addedCount, ItemSources.Craft);
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
        _itemAddRequestSubscription?.Dispose();
        _itemAddRequestSubscription = null;
        _equipChangedSubscription?.Dispose();
        _equipChangedSubscription = null;
        _craftCompletedSubscription?.Dispose();
        _craftCompletedSubscription = null;
    }
}

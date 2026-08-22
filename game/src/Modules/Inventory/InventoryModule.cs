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

    [Inject] private readonly IPublisher<ItemAddedEvent> _itemAddedPub = null!;
    [Inject] private readonly IPublisher<ItemRemovedEvent> _itemRemovedPub = null!;

    // IMPL-3: Config injected via DI (replaces obsolete SetConfig()).
    [Inject] private readonly InventoryConfig _config = null!;
    private StorageService? _ringStorage;

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

        // Создание Ring Storage
        _ringStorage = new StorageService(
            StorageType.Ring,
            _config.RingStorageCapacity,
            _itemAddedPub,
            _itemRemovedPub);

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

    /// <summary>Получить кольцо хранения.</summary>
    public StorageService? GetRingStorage() => _ringStorage;

    private void OnResourceHarvested(in ResourceHarvestedEvent e)
    {
        if (string.IsNullOrEmpty(e.ItemId) || e.Amount <= 0) return;

        if (_itemDatabase.TryGetItem(e.ItemId, out var itemData))
        {
            _inventoryServiceImpl.TryAddItem(itemData, e.Amount);
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
        if (e.EntityId != "player") return;

        if (_itemDatabase.TryGetItem(e.OldItemId, out var oldItem))
        {
            _inventoryServiceImpl.TryAddItem(oldItem, 1);
            Console.WriteLine($"[InventoryModule] Возврат предмета '{e.OldItemId}' в инвентарь из слота {e.Slot}");
        }
    }

    private void OnCraftCompleted(in CraftCompletedEvent e)
    {
        if (string.IsNullOrEmpty(e.ResultItemId)) return;

        if (_itemDatabase.TryGetItem(e.ResultItemId, out var resultItem))
        {
            _inventoryServiceImpl.TryAddItem(resultItem, e.Count);
            Console.WriteLine($"[InventoryModule] Результат крафта '{e.ResultItemId}' добавлен в инвентарь");
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

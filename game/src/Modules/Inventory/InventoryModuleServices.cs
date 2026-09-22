#nullable enable
// Создано: 2026-05-10 — Phase 17B: делегат регистрации модуля
// Migrated from Ai-game3 (Unity+VContainer+MessagePipe) to Ai-game4 (Godot+DI+EventBus) 2026-08-15.
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Inventory;

/// <summary>
/// Делегат регистрации публичных сервисов модуля Inventory.
/// </summary>
public static class InventoryModuleServices
{
    public static void Register(IContainerBuilder builder)
    {
        // === Публичные сервисы ===
        builder.Register<IInventoryService, InventoryService>(Lifetime.Singleton);
        builder.Register<ISaveable, InventoryService>(Lifetime.Singleton);
        builder.Register<IEquipmentService, EquipmentService>(Lifetime.Singleton);
        builder.Register<IEquipmentDataProvider, EquipmentDataProvider>(Lifetime.Singleton);
        builder.Register<ICraftingService, CraftingService>(Lifetime.Singleton);
        builder.Register<IStorageRingService, StorageRingService>(Lifetime.Singleton);
        // R35 (Фаза 12 / P1-19): содержимое колец — в сейве (блок "storage_rings",
        // RestoreOrder после "equipment"). IWorldResettable подхватывается
        // ResolveAll по фактическому типу инстанса (как у TradeService P1-1).
        builder.Register<ISaveable, StorageRingService>(Lifetime.Singleton);

        // === Внутренние сервисы ===
        builder.Register<MaterialService>(Lifetime.Singleton);
        builder.Register<BackpackService>(Lifetime.Singleton);

        // === Ground items (dropped on world) ===
        builder.Register<IGroundItemService, GroundItemService>(Lifetime.Singleton);

        // === Spirit Storage (Q9: separated from unified StorageService) ===
        builder.Register<ISpiritStorageService, SpiritStorageService>(Lifetime.Singleton);

        // === Belt quick slots (2026-08-22: хотбар 3-9, гейт по поясу) ===
        builder.Register<BeltService>(Lifetime.Singleton);

        // === R19 «Потребляемые ресурсы»: единая маршрутизация использования
        // предметов по типу (камни Ци / heal / qi_restore) — потребляется
        // ПКМ-контекстным меню инвентаря и BeltService. ===
        builder.Register<IItemUseService, ItemUseService>(Lifetime.Singleton);

        // === Точка входа модуля ===
        builder.Register<InventoryModule>(Lifetime.Singleton);

        // === Конфигурация по умолчанию ===
        // 50/100 per user request 2026-08-22 — владельческий канон
        // (P2-36 отклонён by-user: прямые указания владельца выше аудитора
        // и документации; конс пект — INVENTORY_SYSTEM §3.3-примечание).
        var defaultConfig = new InventoryConfig
        {
            MaxCarryWeight = GameConstants.BASE_CARRY_WEIGHT,
            MaxCarryVolume = 100f,
            SpiritStorageCapacity = 20,
            RingStorageCapacity = 10,
        };
        builder.RegisterInstance(defaultConfig);
    }
}

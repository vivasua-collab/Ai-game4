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

        // === Внутренние сервисы ===
        builder.Register<MaterialService>(Lifetime.Singleton);
        builder.Register<BackpackService>(Lifetime.Singleton);

        // === Точка входа модуля ===
        builder.Register<InventoryModule>(Lifetime.Singleton);

        // === Конфигурация по умолчанию ===
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

#nullable enable
using System;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Inventory;

/// <summary>
/// Inventory module — event-driven (no tick). Subscribes to ItemAddedEvent
/// is NOT done here (ItemAddedEvent is published BY this module, not consumed).
/// V1: no event subscriptions; modules can call IInventoryService.AddItem directly.
/// </summary>
public sealed class InventoryModule : IModule
{
    public string ModuleName => "Inventory";

    [Inject] private readonly IInventoryService _inventoryService = null!;
    [Inject] private readonly IEquipmentService _equipmentService = null!;
    [Inject] private readonly CraftingService _craftingService = null!;
    [Inject] private readonly IPublisher<ItemAddedEvent> _itemAddedPublisher = null!;

    public void Start()
    {
        // V1 stub: injected services are confirmed wired but not yet called per-tick.
        _ = _inventoryService.GetSlots();
        _ = _equipmentService.GetAllEquipped(0);
        _ = _craftingService.GetAvailableRecipes();
        // _itemAddedPublisher will be used when AddItem is called from event handlers (V2).
        Console.WriteLine($"[InventoryModule] Started — pub wired={_itemAddedPublisher != null}");
    }

    public void Tick(int tickCount)
    {
        // Event-driven — no per-tick work in V1.
    }

    public void Dispose()
    {
        Console.WriteLine("[InventoryModule] Disposed");
    }
}

public static class InventoryModuleServices
{
    public static void Register(IContainerBuilder builder)
    {
        builder.Register<InventoryConfig>(Lifetime.Singleton);
        builder.Register<InventoryService>(Lifetime.Singleton);
        builder.Register<EquipmentService>(Lifetime.Singleton);
        builder.Register<CraftingService>(Lifetime.Singleton);
        builder.Register<IInventoryService, InventoryService>(Lifetime.Singleton);
        builder.Register<IEquipmentService, EquipmentService>(Lifetime.Singleton);
        builder.Register<InventoryModule>(Lifetime.Singleton);
    }
}

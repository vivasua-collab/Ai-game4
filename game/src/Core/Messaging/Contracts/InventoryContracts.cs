#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Messaging.Contracts;

// Created: 2026-05-08 (Ai-game3) — migrated 2026-08-15.
// Inventory contracts: item added/removed, equipment changes/blocks, item-add command-event.
// EVT-02: extended for event-driven model — ItemAddRequestEvent command instead of direct IInventoryService call.
// P1-02 FIX: OldItemId in EquipmentChangedEvent; TotalArmor for consumer cache.

public readonly struct ItemAddedEvent
{
    public readonly string ItemId;
    public readonly int Count;
    public ItemAddedEvent(string itemId, int count) { ItemId = itemId; Count = count; }
}

public readonly struct ItemRemovedEvent
{
    public readonly string ItemId;
    public readonly int Count;
    public ItemRemovedEvent(string itemId, int count) { ItemId = itemId; Count = count; }
}

/// <summary>
/// Событие: изменение экипировки.
/// EVT-02: добавлен TotalArmor для кэша потребителей (DamageService).
/// P1-02 FIX: добавлен OldItemId — ID снятого предмета (для возврата в инвентарь).
/// </summary>
public readonly struct EquipmentChangedEvent
{
    public readonly string EntityId;
    public readonly EquipmentSlot Slot;
    public readonly string ItemId;
    public readonly string OldItemId; // P1-02 FIX: старый предмет (null если слот был пуст)
    public readonly float TotalArmor; // EVT-02: итоговая броня после изменения

    public EquipmentChangedEvent(string entityId, EquipmentSlot slot, string itemId)
        { EntityId = entityId; Slot = slot; ItemId = itemId; OldItemId = null; TotalArmor = 0f; }

    public EquipmentChangedEvent(string entityId, EquipmentSlot slot, string itemId, float totalArmor)
        { EntityId = entityId; Slot = slot; ItemId = itemId; OldItemId = null; TotalArmor = totalArmor; }

    public EquipmentChangedEvent(string entityId, EquipmentSlot slot, string itemId, string oldItemId, float totalArmor)
        { EntityId = entityId; Slot = slot; ItemId = itemId; OldItemId = oldItemId; TotalArmor = totalArmor; }
}

public readonly struct EquipmentBlockedEvent
{
    public readonly string EntityId;
    public readonly EquipmentSlot Slot;
    public readonly string Reason;
    public EquipmentBlockedEvent(string entityId, EquipmentSlot slot, string reason)
        { EntityId = entityId; Slot = slot; Reason = reason; }
}

// === EVT-02: НОВОЕ COMMAND-СОБЫТИЕ ===

/// <summary>
/// Команда: запрос на добавление предмета в инвентарь.
/// Публикуется модулями-потребителями (Tile/ResourceService, Combat/CombatLootService).
/// InventoryService подписывается и вызывает TryAddItem() внутренне.
/// </summary>
public readonly struct ItemAddRequestEvent
{
    public readonly string ItemId;
    public readonly int Count;
    public readonly string Source; // Источник запроса (для логирования: "harvest", "loot", "craft")

    public ItemAddRequestEvent(string itemId, int count, string source = "")
        { ItemId = itemId; Count = count; Source = source; }
}

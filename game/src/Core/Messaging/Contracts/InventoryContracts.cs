#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Messaging.Contracts;

// Created: 2026-05-08 (Ai-game3) — migrated 2026-08-15.
// Inventory contracts: item added/removed, equipment changes/blocks, item-add command-event.
// EVT-02: extended for event-driven model — ItemAddRequestEvent command instead of direct IInventoryService call.
// P1-02 FIX: OldItemId in EquipmentChangedEvent; TotalArmor for consumer cache.

/// <summary>
/// Канонические источники поступления предметов (ItemAddedEvent.Source,
/// параметр TryAddItem). BUG-022 (Этап-7 аудита): цель GatherItem считает
/// только получение из мира; покупка/награды/переносы между собственными
/// хранилищами — нет. Новые пути поступления ОБЯЗАНЫ проставлять источник.
/// </summary>
public static class ItemSources
{
    /// <summary>Легаси/неспецифицированный путь (совместимость). Считается сбором.</summary>
    public const string World = "world";

    /// <summary>Добыча ресурса с тайла (F-ключ, ResourceHarvested цепочка).</summary>
    public const string Harvest = "harvest";

    /// <summary>Подбор предмета с земли (GroundItemService/RequestPickup).</summary>
    public const string Pickup = "pickup";

    /// <summary>Обыск трупа (CorpseService).</summary>
    public const string Loot = "loot";

    /// <summary>Результат крафта (CraftCompletedEvent → инвентарь).</summary>
    public const string Craft = "craft";

    /// <summary>Покупка в лавке торговца (TradeService.TryBuy).</summary>
    public const string MerchantBuy = "merchant_buy";

    /// <summary>Награда за квест (QuestRewardService → ItemAddRequestEvent).</summary>
    public const string QuestReward = "quest_reward";

    /// <summary>Перенос между собственными хранилищами (кольцо/духовное хранилище).</summary>
    public const string StorageTransfer = "storage_transfer";

    /// <summary>Возврат из пояса (BeltService.TryTakeBack).</summary>
    public const string BeltTransfer = "belt_transfer";

    /// <summary>Возврат снятой экипировки / откат неудачной экипировки.</summary>
    public const string EquipReturn = "equip_return";

    /// <summary>Извлечение из зарядника (ChargerItemBridge).</summary>
    public const string Charger = "charger";

    /// <summary>Стартовый набор новой игры (StartingGearPhase).</summary>
    public const string StartingGear = "starting_gear";

    /// <summary>Чит-панель / дев-сеятели предметов.</summary>
    public const string Cheat = "cheat";
}

public readonly struct ItemAddedEvent
{
    public readonly string ItemId;
    public readonly int Count;

    /// <summary>
    /// BUG-022 (Этап-7 аудита): источник поступления (ItemSources).
    /// Канонические ненулевые значения — ItemSources.*. Пустая строка
    /// трактуется потребителями как ItemSources.World (легаси-совместимость).
    /// </summary>
    public readonly string Source;

    public ItemAddedEvent(string itemId, int count, string source = ItemSources.World)
        { ItemId = itemId; Count = count; Source = source; }
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
/// Публикуется модулями-потребителями (Tile/ResourceService, NPC/CorpseService).
/// R13-audit (P1-3): Combat/CombatLootService удалён из списка publishers.
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

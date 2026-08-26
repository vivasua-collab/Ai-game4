#nullable enable
// Создано: 2026-08-22 — слоты быстрого доступа пояса (HOTKEYS.md §8 + запрос
// пользователя: хотбар 3-9 активен только при надетом поясе).
// BeltService публикует эти события; UI (HotbarPanel, InventoryWindow)
// и модули (звук/статистика) подписываются на них.

namespace CultivationGame.Core.Messaging.Contracts;

/// <summary>
/// Содержимое слота пояса изменилось (назначение/использование/возврат).
/// SlotIndex: 0-6 (= хотбар 3-9). ItemId пуст, если слот опустел.
/// </summary>
public readonly struct BeltSlotsChangedEvent
{
    public readonly int SlotIndex;
    public readonly string ItemId;
    public readonly int Count;

    public BeltSlotsChangedEvent(int slotIndex, string itemId, int count)
    {
        SlotIndex = slotIndex;
        ItemId = itemId;
        Count = count;
    }
}

/// <summary>
/// Расходник использован (эффект применён). Публикуется BeltService.Use
/// после успешного применения эффектов и списания предмета.
/// </summary>
public readonly struct ConsumableUsedEvent
{
    public readonly string ItemId;
    public readonly string EffectType;
    public readonly float Value;

    public ConsumableUsedEvent(string itemId, string effectType, float value)
    {
        ItemId = itemId;
        EffectType = effectType;
        Value = value;
    }
}

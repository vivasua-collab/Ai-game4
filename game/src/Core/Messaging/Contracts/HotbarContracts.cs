#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Messaging.Contracts;

// Created: 2026-05-24 (Ai-game3) — migrated 2026-08-15.
// Hotbar contracts: slot selection (1-9), slot content changes.
// Фаза 9A: slots 1-9 for technique shortcuts.

/// <summary>
/// Игрок выбрал слот хотбара (нажал 1-9).
/// Публикуется GameInputAdapter при нажатии цифр.
/// Slot: 1-9 = выбранный слот, 0 = сброс.
/// </summary>
public readonly struct TechniqueSlotSelectedEvent
{
    /// <summary>Номер слота (1-9, 0 = сброс)</summary>
    public readonly int Slot;

    public TechniqueSlotSelectedEvent(int slot)
    {
        Slot = slot;
    }
}

/// <summary>
/// Содержимое слота хотбара изменено.
/// Публикуется HotbarService при назначении/очистке слота.
/// UI подписывается для обновления иконок.
/// </summary>
public readonly struct HotbarSlotChangedEvent
{
    /// <summary>Индекс слота (0-8, внутренний)</summary>
    public readonly int SlotIndex;

    /// <summary>ID техники (null если слот очищен)</summary>
    public readonly string TechniqueId;

    /// <summary>Отображаемое имя (первые 2 символа для UI)</summary>
    public readonly string DisplayName;

    /// <summary>Стихия техники (для цвета рамки)</summary>
    public readonly Element Element;

    /// <summary>Слот выделен (активный)?</summary>
    public readonly bool IsSelected;

    public HotbarSlotChangedEvent(int slotIndex, string techniqueId,
        string displayName, Element element, bool isSelected)
    {
        SlotIndex = slotIndex;
        TechniqueId = techniqueId;
        DisplayName = displayName;
        Element = element;
        IsSelected = isSelected;
    }
}

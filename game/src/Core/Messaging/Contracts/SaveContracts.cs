#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Messaging.Contracts;

// Created: 2026-05-08 (Ai-game3) — migrated 2026-08-15.
// Save / load contracts: requests, completion, autosave, deletion.
// Adapted for Ai-game4: events carry slot name + type (string + enum)
// rather than the legacy SaveSlot enum, since SaveSlot is now a struct
// (name + type) and many call sites construct it from those primitives.

/// <summary>
/// Lightweight info about a save file (used in LoadCompletedEvent payload).
/// </summary>
public readonly struct SaveInfo
{
    public readonly SaveSlot Slot;
    public readonly string DisplayName;
    public readonly long CreatedUnixSeconds;
    public readonly long PlayedSeconds;
    public readonly int CultivationLevel;
    public readonly string LocationId;

    public SaveInfo(SaveSlot slot, string displayName, long createdUnixSeconds,
        long playedSeconds, int cultivationLevel, string locationId)
    {
        Slot = slot;
        DisplayName = displayName;
        CreatedUnixSeconds = createdUnixSeconds;
        PlayedSeconds = playedSeconds;
        CultivationLevel = cultivationLevel;
        LocationId = locationId;
    }
}

public readonly struct SaveRequestedEvent
{
    public readonly string SlotName;
    public readonly SaveSlotType SlotType;
    public SaveRequestedEvent(string slotName, SaveSlotType slotType = SaveSlotType.Manual)
        { SlotName = slotName; SlotType = slotType; }
}

public readonly struct LoadRequestedEvent
{
    public readonly string SlotName;
    public readonly SaveSlotType SlotType;
    public LoadRequestedEvent(string slotName, SaveSlotType slotType = SaveSlotType.Manual)
        { SlotName = slotName; SlotType = slotType; }
}

public readonly struct SaveCompletedEvent
{
    public readonly bool Success;
    public readonly string SlotName;
    public readonly string? Error;
    public SaveCompletedEvent(bool success, string slotName, string? error = null)
        { Success = success; SlotName = slotName; Error = error; }
}

/// <summary>
/// Событие: загрузка завершена.
/// Phase 18A: расширена — добавлен payload с информацией о сохранении.
/// </summary>
public readonly struct LoadCompletedEvent
{
    public readonly bool Success;
    public readonly string SlotName;
    public readonly SaveInfo? SaveInfo;
    public LoadCompletedEvent(bool success, string slotName, SaveInfo? saveInfo = null)
        { Success = success; SlotName = slotName; SaveInfo = saveInfo; }
}

/// <summary>
/// Событие: автосохранение сработало.
/// Публикуется SaveModule при достижении интервала автосохранения.
/// </summary>
public readonly struct AutoSaveTriggeredEvent
{
    public readonly string SlotName;
    public AutoSaveTriggeredEvent(string slotName) { SlotName = slotName; }
}

/// <summary>
/// Событие: сохранение удалено.
/// </summary>
public readonly struct SaveDeletedEvent
{
    public readonly string SlotName;
    public SaveDeletedEvent(string slotName) { SlotName = slotName; }
}

#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Messaging.Contracts;

public readonly struct SaveRequestedEvent
{
    public readonly string SlotName;
    public readonly SaveSlotType SlotType;
    public SaveRequestedEvent(string slotName, SaveSlotType slotType)
    {
        SlotName = slotName; SlotType = slotType;
    }
}

public readonly struct LoadRequestedEvent
{
    public readonly string SlotName;
    public LoadRequestedEvent(string slotName) { SlotName = slotName; }
}

public readonly struct SaveCompletedEvent
{
    public readonly bool Success;
    public readonly string SlotName;
    public readonly string? Error;
    public SaveCompletedEvent(bool success, string slotName, string? error)
    {
        Success = success; SlotName = slotName; Error = error;
    }
}

public readonly struct LoadCompletedEvent
{
    public readonly bool Success;
    public readonly string SlotName;
    public readonly string? Error;
    public LoadCompletedEvent(bool success, string slotName, string? error)
    {
        Success = success; SlotName = slotName; Error = error;
    }
}

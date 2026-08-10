#nullable enable
namespace CultivationGame.Core.Messaging.Contracts;

/// <summary>Raised when a new game session starts.</summary>
public readonly struct GameStartedEvent
{
    public readonly int StartVariant;
    public GameStartedEvent(int startVariant) { StartVariant = startVariant; }
}

public readonly struct GamePausedEvent
{
    public readonly long Frame;
    public GamePausedEvent(long frame) { Frame = frame; }
}

public readonly struct GameResumedEvent
{
    public readonly long Frame;
    public GameResumedEvent(long frame) { Frame = frame; }
}

public readonly struct GameSavingEvent
{
    public readonly string SlotName;
    public GameSavingEvent(string slotName) { SlotName = slotName; }
}

public readonly struct GameSavedEvent
{
    public readonly string SlotName;
    public readonly long DurationMs;
    public GameSavedEvent(string slotName, long durationMs) { SlotName = slotName; DurationMs = durationMs; }
}

public readonly struct GameLoadingEvent
{
    public readonly string SlotName;
    public GameLoadingEvent(string slotName) { SlotName = slotName; }
}

public readonly struct GameLoadedEvent
{
    public readonly string SlotName;
    public readonly long DurationMs;
    public GameLoadedEvent(string slotName, long durationMs) { SlotName = slotName; DurationMs = durationMs; }
}

public readonly struct GameQuitEvent
{
    public readonly bool Saved;
    public GameQuitEvent(bool saved) { Saved = saved; }
}

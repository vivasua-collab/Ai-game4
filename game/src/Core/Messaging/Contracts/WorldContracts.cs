#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Messaging.Contracts;

// Created: 2026-05-08 (Ai-game3) — migrated 2026-08-15.
// World/time events: time changes, day/month/year transitions, location/travel, world events.

// === Время ===

public readonly struct TimeChangedEvent
{
    public readonly float Delta;
    public readonly int Day;
    public readonly int Hour;
    public readonly TimeOfDay TimeOfDay;
    public TimeChangedEvent(float delta, int day, int hour, TimeOfDay timeOfDay)
        { Delta = delta; Day = day; Hour = hour; TimeOfDay = timeOfDay; }
}

public readonly struct DayChangedEvent
{
    public readonly int Day;
    public DayChangedEvent(int day) { Day = day; }
}

/// <summary>Смена месяца</summary>
public readonly struct MonthChangedEvent
{
    public readonly int Month;
    public readonly int Year;
    public MonthChangedEvent(int month, int year) { Month = month; Year = year; }
}

/// <summary>Смена года</summary>
public readonly struct YearChangedEvent
{
    public readonly int Year;
    public YearChangedEvent(int year) { Year = year; }
}

public readonly struct TimeSpeedChangedEvent
{
    public readonly TimeSpeed Speed;
    public TimeSpeedChangedEvent(TimeSpeed speed) { Speed = speed; }
}

/// <summary>
/// Публикуется WorldModule каждый тик симуляции (1 тик = 1 минута).
/// Несёт порядковый номер тика и snapshot текущего WorldTime.
/// Сервисы, не зависящие от ITimeService напрямую, могут подписаться
/// на этот контракт для per-tick обновлений.
/// </summary>
public readonly struct TimeTickEvent
{
    public readonly int TickCount;
    public readonly int Day;
    public readonly int Hour;
    public readonly int Minute;

    public TimeTickEvent(int tickCount, int day, int hour, int minute)
    {
        TickCount = tickCount;
        Day = day;
        Hour = hour;
        Minute = minute;
    }
}

// === Локации / Путешествия ===

/// <summary>Смена текущей локации</summary>
public readonly struct LocationChangedEvent
{
    public readonly string PreviousLocationId;
    public readonly string NewLocationId;
    public LocationChangedEvent(string previousLocationId, string newLocationId)
        { PreviousLocationId = previousLocationId; NewLocationId = newLocationId; }
}

/// <summary>Начало путешествия между локациями</summary>
public readonly struct TravelStartedEvent
{
    public readonly string FromLocationId;
    public readonly string ToLocationId;
    public readonly float EstimatedTicks;
    public TravelStartedEvent(string fromLocationId, string toLocationId, float estimatedTicks)
        { FromLocationId = fromLocationId; ToLocationId = toLocationId; EstimatedTicks = estimatedTicks; }
}

// === Сцены ===

public readonly struct SceneTransitionRequest
{
    public readonly string TargetScene;
    public SceneTransitionRequest(string targetScene) { TargetScene = targetScene; }
}

public readonly struct SceneLoadedEvent
{
    public readonly string SceneName;
    public SceneLoadedEvent(string sceneName) { SceneName = sceneName; }
}

// === Мировые события ===

/// <summary>Мировое событие началось</summary>
public readonly struct WorldEventTriggeredEvent
{
    public readonly string EventId;
    public readonly string EventType;
    public readonly int DurationTicks;
    public WorldEventTriggeredEvent(string eventId, string eventType, int durationTicks)
        { EventId = eventId; EventType = eventType; DurationTicks = durationTicks; }
}

/// <summary>Мировое событие завершилось</summary>
public readonly struct WorldEventEndedEvent
{
    public readonly string EventId;
    public WorldEventEndedEvent(string eventId) { EventId = eventId; }
}

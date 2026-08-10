#nullable enable
using System;
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Interfaces;

/// <summary>
/// Engine-agnostic time service. 1 tick = 1 in-game minute. Speeds map
/// to ticks-per-second: Pause=0, Normal=1, Fast=5, Quick=15.
/// </summary>
public interface ITimeService
{
    WorldTime CurrentTime { get; }
    int TickCount { get; }
    TimeSpeed Speed { get; set; }
    bool IsPaused { get; }

    void Pause();
    void Resume();
    void SetSpeed(TimeSpeed speed);

    event Action<int>? OnTick;
    event Action<WorldTime>? OnTimeChanged;
}

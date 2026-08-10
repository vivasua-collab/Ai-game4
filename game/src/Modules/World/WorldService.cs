#nullable enable
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.World;

/// <summary>
/// TimeService — engine-agnostic source of game time. Tracks WorldTime
/// (1 tick = 1 game minute), tick counter, and current speed (Pause/Normal/Fast/Quick).
/// Fires OnTick / OnTimeChanged events.
///
/// Note: ITimeService (Core) does not expose AdvanceTick on the interface;
/// WorldModule calls it via a concrete cast (DI-cast allowed inside module).
/// </summary>
public sealed class TimeService : ITimeService
{
    public WorldTime CurrentTime { get; private set; } =
        new WorldTime(GameConstants.START_YEAR, 1, 1, 6, 0); // 06:00 on day 1

    public int TickCount { get; private set; }
    public TimeSpeed Speed { get; set; } = TimeSpeed.Normal;
    public bool IsPaused => Speed == TimeSpeed.Pause;

    public event Action<int>? OnTick;
    public event Action<WorldTime>? OnTimeChanged;

    public void Pause()
    {
        if (Speed == TimeSpeed.Pause) return;
        Speed = TimeSpeed.Pause;
        Console.WriteLine($"[TimeService] Paused at tick {TickCount}");
    }

    public void Resume()
    {
        if (Speed != TimeSpeed.Pause) return;
        Speed = TimeSpeed.Normal;
        Console.WriteLine($"[TimeService] Resumed at tick {TickCount}");
    }

    public void SetSpeed(TimeSpeed speed)
    {
        if (Speed == speed) return;
        Speed = speed;
        Console.WriteLine($"[TimeService] Speed set to {speed}");
    }

    /// <summary>
    /// Advance time by one tick (= 1 game minute). Called by WorldModule.
    /// NOT on the ITimeService interface — caller must cast to TimeService.
    /// </summary>
    public void AdvanceTick()
    {
        if (IsPaused) return;
        TickCount++;
        CurrentTime = CurrentTime.AddMinutes(GameConstants.TICKS_PER_MINUTE);
        OnTick?.Invoke(TickCount);
        OnTimeChanged?.Invoke(CurrentTime);
    }
}

/// <summary>
/// WorldService — registry of locations + current active location.
/// Uses LocationData (Core data model). Locations are pre-registered at Start.
/// </summary>
public sealed class WorldService : IWorldService
{
    private readonly Dictionary<string, LocationData> _locations = new();
    private LocationData? _current;

    public LocationData? CurrentLocation => _current;

    public event Action<LocationData>? OnLocationChanged;

    /// <summary>Register a location in the catalogue. Not on interface.</summary>
    public void RegisterLocation(LocationData location)
    {
        if (location == null) throw new ArgumentNullException(nameof(location));
        _locations[location.Id] = location;
    }

    public IReadOnlyList<LocationData> GetAvailableLocations()
    {
        // Return a snapshot list
        var list = new List<LocationData>(_locations.Values.Count);
        foreach (var v in _locations.Values) list.Add(v);
        return list;
    }

    public void SetActiveLocation(string locationId)
    {
        if (!_locations.TryGetValue(locationId, out var loc))
        {
            Console.WriteLine($"[WorldService] SetActiveLocation('{locationId}') — NOT FOUND");
            return;
        }
        var old = _current;
        _current = loc;
        Console.WriteLine($"[WorldService] Active location: {loc.Name} (id={loc.Id})");
        OnLocationChanged?.Invoke(loc);
    }
}

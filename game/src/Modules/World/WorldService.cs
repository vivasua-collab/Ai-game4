#nullable enable
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

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
    [Inject] private readonly IPublisher<TimeSpeedChangedEvent> _speedChangedPub = null!;

    public WorldTime CurrentTime { get; private set; } =
        new WorldTime(GameConstants.START_YEAR, 1, 1, 6, 0); // 06:00 on day 1

    public int TickCount { get; private set; }

    private TimeSpeed _speed = TimeSpeed.Normal;
    public TimeSpeed Speed
    {
        get => _speed;
        set
        {
            if (_speed == value) return;
            _speed = value;
            // Docs (MODULE_STRUCTURE §WorldContracts): speed changes MUST be
            // published on the bus so UI/modules can react. Was missing after
            // the tick-system integration — subscribers saw no speed changes.
            _speedChangedPub?.Publish(new TimeSpeedChangedEvent(_speed));
        }
    }

    public bool IsPaused => Speed == TimeSpeed.Paused;

    // ITimeService — V1 stubs (real values derived from CurrentTime when needed).
    public float DeltaTime { get; private set; }
    public float TotalTime { get; private set; }
    public int CurrentDay => CurrentTime.Day;
    public int CurrentMonth => CurrentTime.Month;
    public int CurrentYear => CurrentTime.Year;
    public int CurrentHour => CurrentTime.Hour;
    public TimeOfDay TimeOfDay => CurrentTime.TimeOfDay;

    public event Action<int>? OnTick;
    public event Action<WorldTime>? OnTimeChanged;

    public void Pause()
    {
        if (Speed == TimeSpeed.Paused) return;
        Speed = TimeSpeed.Paused;
        Console.WriteLine($"[TimeService] Paused at tick {TickCount}");
    }

    public void Resume()
    {
        if (Speed != TimeSpeed.Paused) return;
        Speed = TimeSpeed.Normal;
        Console.WriteLine($"[TimeService] Resumed at tick {TickCount}");
    }

    /// <summary>
    /// Advance time by one tick (= 1 game minute). Called by WorldModule.
    /// NOT on the ITimeService interface — caller must cast to TimeService.
    /// </summary>
    public void AdvanceTick()
    {
        if (IsPaused) return;
        TickCount++;
        DeltaTime = 1f / 60f; // V1 placeholder: 1 minute per real-time tick.
        TotalTime += DeltaTime;
        CurrentTime = CurrentTime.AddMinutes(GameConstants.TICKS_PER_MINUTE);
        OnTick?.Invoke(TickCount);
        OnTimeChanged?.Invoke(CurrentTime);
    }
}

/// <summary>
/// WorldService — registry of locations + current active location.
/// Uses LocationData (Core data model). Implements <see cref="IWorldService"/>.
/// </summary>
public sealed class WorldService : IWorldService
{
    [Inject] private readonly IPublisher<LocationChangedEvent> _locationChangedPub = null!;
    [Inject] private readonly IPublisher<TravelStartedEvent> _travelStartedPub = null!;

    private readonly Dictionary<string, LocationData> _locations = new();
    private readonly Dictionary<string, FactionInfo> _factions = new();
    private readonly HashSet<string> _discoveredSectors = new();
    private LocationData? _current;

    /// <summary>Internal — current active location data.</summary>
    public LocationData? CurrentLocation => _current;

    public string CurrentLocationId => _current?.Id ?? string.Empty;
    public string CurrentSectorId => _current?.ParentSectorId ?? "0_0";

    public event Action<LocationData>? OnLocationChanged;

    /// <summary>Register a location in the catalogue. Not on interface.</summary>
    public void RegisterLocation(LocationData location)
    {
        if (location == null) throw new ArgumentNullException(nameof(location));
        _locations[location.Id] = location;
    }

    /// <summary>Register a faction. Not on interface.</summary>
    public void RegisterFaction(FactionInfo faction)
    {
        if (string.IsNullOrEmpty(faction.Id)) return;
        _factions[faction.Id] = faction;
    }

    /// <summary>Internal — set active location by ID.</summary>
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
        _locationChangedPub.Publish(new LocationChangedEvent(old?.Id ?? string.Empty, loc.Id));
        OnLocationChanged?.Invoke(loc);
    }

    public IReadOnlyList<LocationData> GetAvailableLocations()
    {
        var list = new List<LocationData>(_locations.Values.Count);
        foreach (var v in _locations.Values) list.Add(v);
        return list;
    }

    // === IWorldService ===

    public bool TryTravel(string locationId)
    {
        if (!_locations.ContainsKey(locationId))
        {
            Console.WriteLine($"[WorldService] TryTravel('{locationId}') — unknown location");
            return false;
        }
        var from = _current?.Id ?? string.Empty;
        _travelStartedPub.Publish(new TravelStartedEvent(from, locationId, 1f));
        SetActiveLocation(locationId);
        return true;
    }

    public LocationInfo GetLocation(string locationId)
    {
        if (_locations.TryGetValue(locationId, out var loc))
        {
            return new LocationInfo(loc.Id, loc.Name, loc.LocationType,
                BiomeType.Plains, loc.QiDensity, 0, loc.ParentSectorId);
        }
        return new LocationInfo(locationId, locationId, LocationType.Village,
            BiomeType.Plains, 0, 0, "0_0");
    }

    public FactionInfo GetFaction(string factionId)
    {
        if (_factions.TryGetValue(factionId, out var f)) return f;
        return new FactionInfo(factionId, factionId, string.Empty, 0);
    }

    public FactionRelationType GetFactionRelation(string factionA, string factionB)
    {
        // V1 stub: neutral unless identical.
        if (factionA == factionB) return FactionRelationType.Ally;
        return FactionRelationType.Neutral;
    }

    public IReadOnlyList<string> GetDiscoveredSectors()
    {
        var list = new List<string>(_discoveredSectors.Count);
        foreach (var s in _discoveredSectors) list.Add(s);
        return list;
    }

    public bool IsSectorDiscovered(string sectorId) => _discoveredSectors.Contains(sectorId);

    /// <summary>Internal — mark a sector as discovered. Not on interface.</summary>
    public void DiscoverSector(string sectorId)
    {
        if (!string.IsNullOrEmpty(sectorId)) _discoveredSectors.Add(sectorId);
    }
}

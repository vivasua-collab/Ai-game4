#nullable enable
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Charger;

/// <summary>
/// ChargerState — internal representation of a qi-charger artifact.
/// </summary>
public sealed class ChargerState
{
    public int Id { get; set; }
    public Position2D Position { get; set; }
    public int MaxSlots { get; set; } = 4;
    public string?[] Slots { get; set; } = new string?[4];
    public float Heat { get; set; }
    public bool IsOverheated { get; set; }
}

/// <summary>
/// ChargerService — Qi-charger artifacts. Each charger has slots for qi stones
/// and a heat value. Overheating forces cooldown.
/// V1 stub: stores ChargerState in a Dictionary keyed by chargerId.
/// </summary>
public sealed class ChargerService : IChargerService
{
    private int _nextId = 1;
    private readonly Dictionary<int, ChargerState> _chargers = new();
    private readonly ChargerConfig _config;

    public ChargerService(ChargerConfig? config = null) => _config = config ?? new ChargerConfig();

    public void RegisterCharger(int chargerId, Position2D position, int maxSlots)
    {
        if (maxSlots <= 0) maxSlots = _config.DefaultMaxSlots;
        // Use provided ID if >0, else allocate
        int id = chargerId > 0 ? chargerId : _nextId++;
        if (id >= _nextId) _nextId = id + 1;

        _chargers[id] = new ChargerState
        {
            Id = id,
            Position = position,
            MaxSlots = maxSlots,
            Slots = new string?[maxSlots],
            Heat = 0f,
            IsOverheated = false
        };
        Console.WriteLine($"[ChargerService] Registered charger {id} @ {position}, {maxSlots} slots");
    }

    public bool InsertStone(int chargerId, int slotIndex, string stoneId)
    {
        if (!_chargers.TryGetValue(chargerId, out var c)) return false;
        if (slotIndex < 0 || slotIndex >= c.MaxSlots) return false;
        if (!string.IsNullOrEmpty(c.Slots[slotIndex])) return false;
        if (c.IsOverheated) return false;
        c.Slots[slotIndex] = stoneId;
        Console.WriteLine($"[ChargerService] Charger {chargerId} slot {slotIndex} ← stone '{stoneId}'");
        return true;
    }

    public void ProcessTick()
    {
        foreach (var kv in _chargers)
        {
            var c = kv.Value;
            int filledSlots = 0;
            for (int i = 0; i < c.MaxSlots; i++) if (!string.IsNullOrEmpty(c.Slots[i])) filledSlots++;

            if (filledSlots > 0 && !c.IsOverheated)
            {
                c.Heat += filledSlots * _config.HeatPerStonePerTick;
                if (c.Heat >= _config.OverheatThreshold)
                {
                    c.IsOverheated = true;
                    Console.WriteLine($"[ChargerService] Charger {c.Id} OVERHEATED");
                }
            }
            else
            {
                c.Heat = Math.Max(0f, c.Heat - _config.HeatDissipationPerTick);
                if (c.IsOverheated && c.Heat <= _config.CoolDownThreshold)
                {
                    c.IsOverheated = false;
                    Console.WriteLine($"[ChargerService] Charger {c.Id} cooled down");
                }
            }
        }
    }
}

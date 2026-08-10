#nullable enable
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Formation;

/// <summary>
/// FormationState — internal representation of an active formation.
/// </summary>
public sealed class FormationState
{
    public int Id { get; set; }
    public Position2D Center { get; set; }
    public FormationType Type { get; set; }
    public int Level { get; set; } = 1;
    public long CurrentQi { get; set; }
    public long MaxCapacity { get; set; } = 1000L;
    public int Stage { get; set; }
    public int CreatorId { get; set; }
    public bool IsActive { get; set; } = true;
    public HashSet<int> Participants { get; set; } = new();
}

/// <summary>
/// FormationService — multi-participant qi-pooling formations. Each formation
/// has a center, type, level, current qi pool, max capacity, stage, creator.
/// V1 stub: stores FormationState in Dictionary keyed by formationId.
/// </summary>
public sealed class FormationService : IFormationService
{
    private int _nextId = 1;
    private readonly Dictionary<int, FormationState> _formations = new();
    private readonly FormationConfig _config;

    public FormationService(FormationConfig? config = null) => _config = config ?? new FormationConfig();

    public void CreateFormation(int creatorId, Position2D center, FormationType type, int level)
    {
        int id = _nextId++;
        _formations[id] = new FormationState
        {
            Id = id,
            Center = center,
            Type = type,
            Level = Math.Max(1, level),
            CurrentQi = 0,
            MaxCapacity = _config.DefaultMaxCapacity * Math.Max(1, level),
            Stage = 0,
            CreatorId = creatorId,
            IsActive = true,
            Participants = new HashSet<int> { creatorId }
        };
        Console.WriteLine($"[FormationService] Created formation {id} ({type} L{level}) @ {center} by {creatorId}");
    }

    public void DissolveFormation(int formationId)
    {
        if (_formations.Remove(formationId))
            Console.WriteLine($"[FormationService] Dissolved formation {formationId}");
    }

    public void ContributeQi(int formationId, int contributorId, long amount)
    {
        if (!_formations.TryGetValue(formationId, out var f)) return;
        if (!f.IsActive) return;
        if (amount <= 0) return;
        f.Participants.Add(contributorId);
        f.CurrentQi = Math.Min(f.MaxCapacity, f.CurrentQi + amount);
    }

    public void ProcessDrain()
    {
        foreach (var kv in _formations)
        {
            var f = kv.Value;
            if (!f.IsActive) continue;
            long drain = f.Participants.Count * _config.DrainPerTickPerParticipant;
            f.CurrentQi = Math.Max(0, f.CurrentQi - drain);
            if (f.CurrentQi == 0 && f.Stage > 0)
            {
                f.Stage--;
            }
        }
    }
}

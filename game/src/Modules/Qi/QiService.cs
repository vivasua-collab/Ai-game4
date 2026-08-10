#nullable enable
using System;
using System.Collections.Generic;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Qi;

/// <summary>
/// QiState — per-entity qi state. Uses `long` (L9 ~524M effectiveQi needs it).
/// </summary>
public sealed class QiState
{
    public int EntityId { get; set; }
    public long CurrentQi { get; set; }
    public long CoreCapacity { get; set; } = 100L;
    public int CultivationLevel { get; set; } = 1;
    public float Conductivity { get; set; } = 1f;
}

/// <summary>
/// QiService — per-entity qi state. All Qi values are long (no float).
/// V1 stub: stores QiState in a Dictionary keyed by entityId.
/// </summary>
public sealed class QiService : IQiService
{
    private readonly Dictionary<int, QiState> _states = new();
    private readonly QiConfig _config;

    public QiService(QiConfig? config = null) => _config = config ?? new QiConfig();

    /// <summary>Register an entity. Not on interface.</summary>
    public void RegisterEntity(int entityId, long coreCapacity, int cultivationLevel = 1)
    {
        if (_states.ContainsKey(entityId)) return;
        _states[entityId] = new QiState
        {
            EntityId = entityId,
            CurrentQi = 0,
            CoreCapacity = Math.Max(coreCapacity, 100L),
            CultivationLevel = cultivationLevel,
            Conductivity = _config.BaseConductivity
        };
        Console.WriteLine($"[QiService] Registered entity {entityId}: cap={coreCapacity}, level={cultivationLevel}");
    }

    private QiState? Get(int entityId) => _states.TryGetValue(entityId, out var s) ? s : null;

    public long GetCurrentQi(int entityId) => Get(entityId)?.CurrentQi ?? 0L;
    public long GetCoreCapacity(int entityId) => Get(entityId)?.CoreCapacity ?? 0L;
    public int GetCultivationLevel(int entityId) => Get(entityId)?.CultivationLevel ?? 1;

    public void AddQi(int entityId, long amount)
    {
        var s = Get(entityId);
        if (s == null) return;
        s.CurrentQi = Math.Min(s.CoreCapacity, s.CurrentQi + amount);
    }

    public bool ConsumeQi(int entityId, long amount)
    {
        var s = Get(entityId);
        if (s == null || s.CurrentQi < amount) return false;
        s.CurrentQi -= amount;
        return true;
    }

    public void ProcessRegenBatch()
    {
        foreach (var kv in _states)
        {
            var s = kv.Value;
            if (s.CurrentQi >= s.CoreCapacity) continue;
            long regen = (long)(s.CoreCapacity * _config.RegenFractionPerBatch * s.Conductivity);
            s.CurrentQi = Math.Min(s.CoreCapacity, s.CurrentQi + regen);
        }
    }

    /// <summary>Internal — entity IDs for batch iteration. Not on interface.</summary>
    public IReadOnlyCollection<int> GetRegisteredEntityIds() => _states.Keys;
}

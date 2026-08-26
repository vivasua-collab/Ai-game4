#nullable enable
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Player;

/// <summary>
/// StatService — per-stat value store with additive bonuses, virtual delta
/// (for sleep consolidation) and threshold gates (CanAdvance).
///
/// Implements <see cref="IStatService"/>. All operations are keyed by
/// <see cref="StatType"/> (single player scope in V1; multi-entity support
/// to be added when NPC stats are migrated).
/// </summary>
public sealed class StatService : IStatService
{
    private readonly Dictionary<StatType, float> _base = new();
    private readonly Dictionary<StatType, float> _bonus = new();
    private readonly Dictionary<StatType, float> _virtualDelta = new();
    private readonly Dictionary<StatType, float> _threshold = new();

    /// <summary>Internal — set base value (e.g. from CharacterData on spawn).</summary>
    public void SetBaseStat(StatType type, float value) => _base[type] = value;

    /// <summary>Internal — add a bonus (e.g. from equipment).</summary>
    public void AddBonus(StatType type, float value)
    {
        _bonus.TryGetValue(type, out var cur);
        _bonus[type] = cur + value;
    }

    /// <summary>Internal — remove a bonus.</summary>
    public void RemoveBonus(StatType type, float value)
    {
        if (!_bonus.TryGetValue(type, out var cur)) return;
        float newVal = cur - value;
        if (System.Math.Abs(newVal) < 1e-6f) _bonus.Remove(type);
        else _bonus[type] = newVal;
    }

    /// <summary>Internal — set threshold (e.g. from config).</summary>
    public void SetThreshold(StatType type, float value) => _threshold[type] = value;

    // === IStatService ===

    public float GetStat(StatType type)
        => _base.TryGetValue(type, out var v) ? v : 0f;

    public float GetStatBonus(StatType type)
        => _bonus.TryGetValue(type, out var v) ? v : 0f;

    public void ModifyStat(StatType type, float delta)
    {
        _base.TryGetValue(type, out var cur);
        _base[type] = cur + delta;
    }

    public void SetStat(StatType type, float value) => _base[type] = value;

    public StatDomain GetStatDomain(StatType type) => type switch
    {
        StatType.Intelligence or StatType.Perception or StatType.Luck
            or StatType.CritChance or StatType.CritDamage
            or StatType.Conductivity or StatType.QiEfficiency or StatType.QiCost
            or StatType.QiRestoration or StatType.Cooldown => StatDomain.Soul,
        _ => StatDomain.Body,
    };

    public float GetVirtualDelta(StatType type)
        => _virtualDelta.TryGetValue(type, out var v) ? v : 0f;

    public void AddVirtualDelta(StatType type, float amount)
    {
        _virtualDelta.TryGetValue(type, out var cur);
        _virtualDelta[type] = cur + amount;
    }

    public void ConsolidateSleep(float hours)
    {
        // V1: convert virtual delta to base stat at the configured rate.
        // Real formula lives in StatCalculator; this is a placeholder.
        if (hours < 4f) return;
        foreach (var kvp in _virtualDelta)
        {
            float consolidate = kvp.Value * 0.20f;
            _base.TryGetValue(kvp.Key, out var cur);
            _base[kvp.Key] = cur + consolidate;
        }
        _virtualDelta.Clear();
    }

    public float GetThreshold(StatType type)
        => _threshold.TryGetValue(type, out var v) ? v : 100f;

    public bool CanAdvance(StatType type)
    {
        if (!_virtualDelta.TryGetValue(type, out var delta)) return false;
        return delta >= GetThreshold(type);
    }
}

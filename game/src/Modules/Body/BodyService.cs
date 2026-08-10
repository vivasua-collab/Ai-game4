#nullable enable
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Body;

/// <summary>
/// BodyPartState — Kenshi-style dual-HP body part.
/// </summary>
public sealed class BodyPartState
{
    public BodyPartType Type { get; set; }
    public float Health { get; set; }
    public float MaxHealth { get; set; } = 100f;
    public bool IsSevered { get; set; }
    public float Bleeding { get; set; }
    public float Pain { get; set; }
}

/// <summary>
/// BodyService — per-entity body-part registry. Kenshi-style dual HP
/// (Health/MaxHealth) + severed flag + bleeding + pain.
/// V1 stub: stores BodyPartState in nested dictionaries.
/// </summary>
public sealed class BodyService : IBodyService
{
    private readonly Dictionary<int, Dictionary<BodyPartType, BodyPartState>> _bodies = new();

    private static readonly BodyPartType[] DefaultHumanParts =
    {
        BodyPartType.Head, BodyPartType.Torso, BodyPartType.LeftArm,
        BodyPartType.RightArm, BodyPartType.LeftLeg, BodyPartType.RightLeg,
        BodyPartType.Heart
    };

    private readonly BodyConfig _config;

    public BodyService(BodyConfig? config = null) => _config = config ?? new BodyConfig();

    /// <summary>Register a body for an entity. Not on interface.</summary>
    public void RegisterBody(int entityId)
    {
        if (_bodies.ContainsKey(entityId)) return;
        var parts = new Dictionary<BodyPartType, BodyPartState>();
        foreach (var pt in DefaultHumanParts)
        {
            parts[pt] = new BodyPartState
            {
                Type = pt,
                Health = _config.DefaultPartMaxHealth,
                MaxHealth = _config.DefaultPartMaxHealth,
                IsSevered = false,
                Bleeding = 0f,
                Pain = 0f
            };
        }
        _bodies[entityId] = parts;
        Console.WriteLine($"[BodyService] Registered body for entity {entityId} ({parts.Count} parts)");
    }

    public void DamagePart(int entityId, BodyPartType part, float damage, DamageType type)
    {
        if (!_bodies.TryGetValue(entityId, out var parts)) return;
        if (!parts.TryGetValue(part, out var st) || st.IsSevered) return;

        st.Health = Math.Max(0f, st.Health - damage);
        st.Pain = Math.Min(1f, st.Pain + damage / st.MaxHealth);
        if (type == DamageType.Slashing || type == DamageType.Piercing)
            st.Bleeding += damage * 0.1f;

        Console.WriteLine($"[BodyService] {entityId}.{part} took {damage:F1} {type} dmg → hp {st.Health:F1}/{st.MaxHealth:F1}");
    }

    public void HealPart(int entityId, BodyPartType part, float amount)
    {
        if (!_bodies.TryGetValue(entityId, out var parts)) return;
        if (!parts.TryGetValue(part, out var st) || st.IsSevered) return;
        st.Health = Math.Min(st.MaxHealth, st.Health + amount);
        st.Pain = Math.Max(0f, st.Pain - amount * 0.05f);
    }

    public bool IsPartSevered(int entityId, BodyPartType part)
    {
        if (!_bodies.TryGetValue(entityId, out var parts)) return false;
        return parts.TryGetValue(part, out var st) && st.IsSevered;
    }

    public float GetPartHealth(int entityId, BodyPartType part)
    {
        if (!_bodies.TryGetValue(entityId, out var parts)) return 0f;
        return parts.TryGetValue(part, out var st) ? st.Health : 0f;
    }

    public void ProcessRegeneration(int entityId)
    {
        if (!_bodies.TryGetValue(entityId, out var parts)) return;
        foreach (var kv in parts)
        {
            var st = kv.Value;
            if (st.IsSevered) continue;
            if (st.Bleeding > 0f)
            {
                st.Health = Math.Max(0f, st.Health - st.Bleeding * _config.BleedDamagePerTick * 0.1f);
                st.Bleeding = Math.Max(0f, st.Bleeding - 0.05f);
            }
            if (st.Health > 0f && st.Health < st.MaxHealth)
            {
                st.Health = Math.Min(st.MaxHealth, st.Health + _config.RegenPerTick);
            }
            if (st.Pain > 0f) st.Pain = Math.Max(0f, st.Pain - 0.01f);
        }
    }

    /// <summary>Internal — entity IDs for batch iteration. Not on interface.</summary>
    public IReadOnlyCollection<int> GetRegisteredEntityIds() => _bodies.Keys;
}

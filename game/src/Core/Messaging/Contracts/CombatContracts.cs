#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Messaging.Contracts;

public readonly struct CombatStartedEvent
{
    public readonly int AttackerId;
    public readonly int TargetId;
    public CombatStartedEvent(int attackerId, int targetId)
    {
        AttackerId = attackerId; TargetId = targetId;
    }
}

/// <summary>Combat ends (no parameters — single global combat context for v1).</summary>
public readonly struct CombatEndedEvent
{
    public readonly long DurationTicks;
    public CombatEndedEvent(long durationTicks) { DurationTicks = durationTicks; }
}

public readonly struct DamageDealtEvent
{
    public readonly int AttackerId;
    public readonly int TargetId;
    public readonly float Damage;
    public readonly DamageType Type;
    public DamageDealtEvent(int attackerId, int targetId, float damage, DamageType type)
    {
        AttackerId = attackerId; TargetId = targetId; Damage = damage; Type = type;
    }
}

public readonly struct EntityDeathEvent
{
    public readonly int EntityId;
    public readonly DamageType Cause;
    public EntityDeathEvent(int entityId, DamageType cause)
    {
        EntityId = entityId; Cause = cause;
    }
}

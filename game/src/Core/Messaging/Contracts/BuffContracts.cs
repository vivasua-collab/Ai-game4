#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Messaging.Contracts;

// Created: 2026-05-09 (Ai-game3) — migrated 2026-08-15.
// Buff / status-effect contracts: applied, removed, expired, ticked, stat-modifier changes.

// BuffType is defined in CultivationGame.Core.Data (canonical).

/// <summary>
/// Событие: бафф наложен на сущность.
/// </summary>
public readonly struct BuffAppliedEvent
{
    public readonly string EntityId;
    public readonly string BuffId;
    public readonly BuffType Type;
    public readonly float Duration;
    public readonly float Potency;
    public BuffAppliedEvent(string entityId, string buffId, BuffType type, float duration, float potency)
        { EntityId = entityId; BuffId = buffId; Type = type; Duration = duration; Potency = potency; }
}

/// <summary>
/// Событие: бафф снят с сущности (вручную).
/// </summary>
public readonly struct BuffRemovedEvent
{
    public readonly string EntityId;
    public readonly string BuffId;
    public readonly BuffType Type;
    public BuffRemovedEvent(string entityId, string buffId, BuffType type)
        { EntityId = entityId; BuffId = buffId; Type = type; }
}

/// <summary>
/// Событие: бафф истёк по таймеру.
/// </summary>
public readonly struct BuffExpiredEvent
{
    public readonly string EntityId;
    public readonly string BuffId;
    public readonly BuffType Type;
    public BuffExpiredEvent(string entityId, string buffId, BuffType type)
        { EntityId = entityId; BuffId = buffId; Type = type; }
}

/// <summary>
/// Событие: тик периодического эффекта (DoT/HoT).
/// </summary>
public readonly struct BuffTickedEvent
{
    public readonly string EntityId;
    public readonly string BuffId;
    public readonly BuffType Type;
    public readonly float TickValue;
    public BuffTickedEvent(string entityId, string buffId, BuffType type, float tickValue)
        { EntityId = entityId; BuffId = buffId; Type = type; TickValue = tickValue; }
}

/// <summary>
/// Событие: изменился модификатор характеристики.
/// </summary>
public readonly struct StatModifierChangedEvent
{
    public readonly string EntityId;
    public readonly StatType Stat;
    public readonly float NewModifier;
    public StatModifierChangedEvent(string entityId, StatType stat, float newModifier)
        { EntityId = entityId; Stat = stat; NewModifier = newModifier; }
}

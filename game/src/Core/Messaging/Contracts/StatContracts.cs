#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Messaging.Contracts;

// Created: 2026-05-08 (Ai-game3) — migrated 2026-08-15.
// Stat contracts: stat value changes.
// P0-05 FIX: Type→StatType (avoid conflict with object.GetType()).

// === ХАРАКТЕРИСТИКИ ===

/// <summary>
/// Изменилось значение характеристики
/// </summary>
public readonly struct StatChangedEvent
{
    public readonly string EntityId;
    public readonly StatType StatType;
    public readonly float OldValue;
    public readonly float NewValue;
    public StatChangedEvent(string entityId, StatType statType, float oldValue, float newValue)
        { EntityId = entityId; StatType = statType; OldValue = oldValue; NewValue = newValue; }
}

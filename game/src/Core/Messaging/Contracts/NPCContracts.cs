#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Messaging.Contracts;

// Created: 2026-05-08 (Ai-game3) — migrated 2026-08-15.
// NPC contracts: spawn/despawn, attitude, death, interaction, AI state, damage.
// V-03: PresetId → SpeciesId + RoleId in NPCSpawnedEvent.

// NPCRole, Attitude, NPCAIState are defined in CultivationGame.Core.Data (canonical).

// === NPC ===

/// <summary>
/// NPC появился в мире (заспавнен)
/// V-03: PresetId заменён на SpeciesId + RoleId
/// </summary>
public readonly struct NPCSpawnedEvent
{
    public readonly string NpcId;
    public readonly string SpeciesId;
    public readonly NPCRole RoleId;
    /// <summary>Обратная совместимость: PresetId = SpeciesId</summary>
    public string PresetId => SpeciesId;
    public NPCSpawnedEvent(string npcId, string speciesId, NPCRole roleId)
        { NpcId = npcId; SpeciesId = speciesId; RoleId = roleId; }
}

/// <summary>
/// NPC исчез из мира (деспавн)
/// </summary>
public readonly struct NPCDespawnedEvent
{
    public readonly string NpcId;
    public NPCDespawnedEvent(string npcId) { NpcId = npcId; }
}

/// <summary>
/// Изменилось отношение NPC к цели
/// </summary>
public readonly struct AttitudeChangedEvent
{
    public readonly string NpcId;
    public readonly string TargetId;
    public readonly Attitude OldAttitude;
    public readonly Attitude NewAttitude;
    public AttitudeChangedEvent(string npcId, string targetId, Attitude oldAttitude, Attitude newAttitude)
        { NpcId = npcId; TargetId = targetId; OldAttitude = oldAttitude; NewAttitude = newAttitude; }
}

/// <summary>
/// NPC умер
/// </summary>
public readonly struct NPCDeathEvent
{
    public readonly string NpcId;
    public readonly string KillerId;
    public NPCDeathEvent(string npcId, string killerId)
        { NpcId = npcId; KillerId = killerId; }
}

/// <summary>
/// Взаимодействие с NPC
/// </summary>
public readonly struct NPCInteractedEvent
{
    public readonly string NpcId;
    public readonly string InitiatorId;
    public readonly string InteractionType; // "talk", "trade", "attack", "gift" и т.д.
    public NPCInteractedEvent(string npcId, string initiatorId, string interactionType)
        { NpcId = npcId; InitiatorId = initiatorId; InteractionType = interactionType; }
}

/// <summary>
/// Изменилось AI-состояние NPC
/// </summary>
public readonly struct NPCAIStateChangedEvent
{
    public readonly string NpcId;
    public readonly NPCAIState OldState;
    public readonly NPCAIState NewState;
    public NPCAIStateChangedEvent(string npcId, NPCAIState oldState, NPCAIState newState)
        { NpcId = npcId; OldState = oldState; NewState = newState; }
}

/// <summary>
/// NPC получил урон (для рефлексов AI)
/// </summary>
public readonly struct NPCDamagedEvent
{
    public readonly string NpcId;
    public readonly string SourceId;
    public readonly int Damage;
    public readonly float HealthRatio; // Текущий HP / MaxHP
    public NPCDamagedEvent(string npcId, string sourceId, int damage, float healthRatio)
        { NpcId = npcId; SourceId = sourceId; Damage = damage; HealthRatio = healthRatio; }
}

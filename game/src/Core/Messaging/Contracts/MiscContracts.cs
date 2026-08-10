#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Messaging.Contracts;

// ── Body ────────────────────────────────────────────────────────────────
public readonly struct BodyPartDamagedEvent
{
    public readonly int EntityId;
    public readonly BodyPartType Part;
    public readonly float Damage;
    public readonly DamageType Type;
    public BodyPartDamagedEvent(int entityId, BodyPartType part, float damage, DamageType type)
    {
        EntityId = entityId; Part = part; Damage = damage; Type = type;
    }
}

// ── Buff ────────────────────────────────────────────────────────────────
public readonly struct BuffAppliedEvent
{
    public readonly int EntityId;
    public readonly string BuffId;
    public readonly float Duration;
    public BuffAppliedEvent(int entityId, string buffId, float duration)
    {
        EntityId = entityId; BuffId = buffId; Duration = duration;
    }
}

public readonly struct BuffRemovedEvent
{
    public readonly int EntityId;
    public readonly string BuffId;
    public BuffRemovedEvent(int entityId, string buffId)
    {
        EntityId = entityId; BuffId = buffId;
    }
}

// ── Formation ───────────────────────────────────────────────────────────
public readonly struct FormationCreatedEvent
{
    public readonly int FormationId;
    public readonly Position2D Center;
    public readonly FormationType Type;
    public FormationCreatedEvent(int formationId, Position2D center, FormationType type)
    {
        FormationId = formationId; Center = center; Type = type;
    }
}

public readonly struct FormationDissolvedEvent
{
    public readonly int FormationId;
    public FormationDissolvedEvent(int formationId) { FormationId = formationId; }
}

// ── Inventory ───────────────────────────────────────────────────────────
public readonly struct ItemAddedEvent
{
    public readonly string ItemId;
    public readonly int Count;
    public ItemAddedEvent(string itemId, int count) { ItemId = itemId; Count = count; }
}

public readonly struct ItemRemovedEvent
{
    public readonly string ItemId;
    public readonly int Count;
    public ItemRemovedEvent(string itemId, int count) { ItemId = itemId; Count = count; }
}

// ── NPC ─────────────────────────────────────────────────────────────────
public readonly struct NPCSpawnedEvent
{
    public readonly int EntityId;
    public readonly Position2D Position;
    public NPCSpawnedEvent(int entityId, Position2D position)
    {
        EntityId = entityId; Position = position;
    }
}

public readonly struct NPCDespawnedEvent
{
    public readonly int EntityId;
    public NPCDespawnedEvent(int entityId) { EntityId = entityId; }
}

// ── Quest ───────────────────────────────────────────────────────────────
public readonly struct QuestStartedEvent
{
    public readonly string QuestId;
    public QuestStartedEvent(string questId) { QuestId = questId; }
}

public readonly struct QuestCompletedEvent
{
    public readonly string QuestId;
    public QuestCompletedEvent(string questId) { QuestId = questId; }
}

public readonly struct QuestProgressEvent
{
    public readonly string QuestId;
    public readonly int Progress;
    public QuestProgressEvent(string questId, int progress)
    {
        QuestId = questId; Progress = progress;
    }
}

// ── Time ────────────────────────────────────────────────────────────────
public readonly struct TimeTickEvent
{
    public readonly int TickCount;
    public readonly WorldTime Time;
    public TimeTickEvent(int tickCount, WorldTime time)
    {
        TickCount = tickCount; Time = time;
    }
}

public readonly struct TimeSpeedChangedEvent
{
    public readonly TimeSpeed NewSpeed;
    public TimeSpeedChangedEvent(TimeSpeed newSpeed) { NewSpeed = newSpeed; }
}

// ── Charger ─────────────────────────────────────────────────────────────
public readonly struct ChargerRegisteredEvent
{
    public readonly int ChargerId;
    public readonly Position2D Position;
    public ChargerRegisteredEvent(int chargerId, Position2D position)
    {
        ChargerId = chargerId; Position = position;
    }
}

public readonly struct ChargerSlotChangedEvent
{
    public readonly int ChargerId;
    public readonly int SlotIndex;
    public readonly string? StoneId;
    public ChargerSlotChangedEvent(int chargerId, int slotIndex, string? stoneId)
    {
        ChargerId = chargerId; SlotIndex = slotIndex; StoneId = stoneId;
    }
}

// ── Techniques ──────────────────────────────────────────────────────────
public readonly struct TechniqueUsedEvent
{
    public readonly int EntityId;
    public readonly string TechniqueId;
    public TechniqueUsedEvent(int entityId, string techniqueId)
    {
        EntityId = entityId; TechniqueId = techniqueId;
    }
}

public readonly struct TechniqueLearnedEvent
{
    public readonly int EntityId;
    public readonly string TechniqueId;
    public TechniqueLearnedEvent(int entityId, string techniqueId)
    {
        EntityId = entityId; TechniqueId = techniqueId;
    }
}

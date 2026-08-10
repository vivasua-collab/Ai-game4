#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Messaging.Contracts;

public readonly struct PlayerMovedEvent
{
    public readonly int EntityId;
    public readonly Position2D OldPos;
    public readonly Position2D NewPos;
    public PlayerMovedEvent(int entityId, Position2D oldPos, Position2D newPos)
    {
        EntityId = entityId; OldPos = oldPos; NewPos = newPos;
    }
}

public readonly struct PlayerSpawnedEvent
{
    public readonly int EntityId;
    public readonly Position2D Position;
    public PlayerSpawnedEvent(int entityId, Position2D position)
    {
        EntityId = entityId; Position = position;
    }
}

public readonly struct PlayerInteractEvent
{
    public readonly int EntityId;
    public readonly int TargetId;
    public PlayerInteractEvent(int entityId, int targetId)
    {
        EntityId = entityId; TargetId = targetId;
    }
}

public readonly struct PlayerDamagedEvent
{
    public readonly int EntityId;
    public readonly float Damage;
    public readonly DamageType Type;
    public PlayerDamagedEvent(int entityId, float damage, DamageType type)
    {
        EntityId = entityId; Damage = damage; Type = type;
    }
}

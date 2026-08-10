#nullable enable
namespace CultivationGame.Core.Messaging.Contracts;

public readonly struct QiChangedEvent
{
    public readonly int EntityId;
    public readonly long OldQi;
    public readonly long NewQi;
    public QiChangedEvent(int entityId, long oldQi, long newQi)
    {
        EntityId = entityId; OldQi = oldQi; NewQi = newQi;
    }
}

public readonly struct QiConsumedEvent
{
    public readonly int EntityId;
    public readonly long Amount;
    public QiConsumedEvent(int entityId, long amount)
    {
        EntityId = entityId; Amount = amount;
    }
}

public readonly struct QiAddedEvent
{
    public readonly int EntityId;
    public readonly long Amount;
    public QiAddedEvent(int entityId, long amount)
    {
        EntityId = entityId; Amount = amount;
    }
}

public readonly struct QiBreakthroughEvent
{
    public readonly int EntityId;
    public readonly int NewLevel;
    public QiBreakthroughEvent(int entityId, int newLevel)
    {
        EntityId = entityId; NewLevel = newLevel;
    }
}

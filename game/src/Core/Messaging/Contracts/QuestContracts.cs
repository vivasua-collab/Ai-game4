#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Messaging.Contracts;

// Created: 2026-05-08 (Ai-game3) — migrated 2026-08-15.
// Quest contracts: started, objective-updated, completed, failed, abandoned, reward-granted.
// C02 (FIX): QuestStartedEvent carries QuestType for UI without IQuestService call.

// QuestType, QuestRewardType are defined in CultivationGame.Core.Data (canonical).

/// <summary>
/// Квест начат.
/// C02 (FIX): Добавлен QuestType для UI без необходимости вызова IQuestService.
/// </summary>
public readonly struct QuestStartedEvent
{
    public readonly string QuestId;
    public readonly QuestType Type;
    public QuestStartedEvent(string questId, QuestType type)
        { QuestId = questId; Type = type; }
}

public readonly struct QuestObjectiveUpdatedEvent
{
    public readonly string QuestId;
    public readonly string ObjectiveId;
    public readonly int Progress;
    public readonly int Target;
    public QuestObjectiveUpdatedEvent(string questId, string objectiveId, int progress, int target)
        { QuestId = questId; ObjectiveId = objectiveId; Progress = progress; Target = target; }
}

public readonly struct QuestCompletedEvent
{
    public readonly string QuestId;
    public QuestCompletedEvent(string questId) { QuestId = questId; }
}

/// <summary>
/// Квест провален (истёк срок, условие провала)
/// </summary>
public readonly struct QuestFailedEvent
{
    public readonly string QuestId;
    public readonly string Reason;
    public QuestFailedEvent(string questId, string reason = "")
        { QuestId = questId; Reason = reason; }
}

/// <summary>
/// Квест брошен игроком
/// </summary>
public readonly struct QuestAbandonedEvent
{
    public readonly string QuestId;
    public QuestAbandonedEvent(string questId) { QuestId = questId; }
}

/// <summary>
/// Награда за квест выдана.
/// Публикуется QuestRewardService после успешной выдачи.
/// </summary>
public readonly struct QuestRewardGrantedEvent
{
    public readonly string QuestId;
    public readonly string RewardId;
    public readonly QuestRewardType RewardType;
    public readonly int Amount;
    public QuestRewardGrantedEvent(string questId, string rewardId, QuestRewardType rewardType, int amount)
        { QuestId = questId; RewardId = rewardId; RewardType = rewardType; Amount = amount; }
}

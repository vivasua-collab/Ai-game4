#nullable enable
// Создано: 2026-05-09 — Phase 12: реализация IQuestRewardService
// Migrated from Ai-game3 (Unity) to Ai-game4 (Godot) 2026-08-15.
//
// Выдача наград за квесты через command-события EventBus.
// EVT-01: НЕ инжектит IInventoryService, IQiService — публикует command-события.
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Modules.Quest.Data;

namespace CultivationGame.Modules.Quest;

/// <summary>
/// Реализация IQuestRewardService.
/// Выдаёт награды за квесты через публикацию command-событий:
/// - ItemAddRequestEvent — для наград предметами
/// - QiAddRequestEvent — для наград Ци
///
/// АРХИТЕКТУРА (EVT-01): QuestRewardService НЕ инжектит сервисы других модулей.
/// </summary>
public class QuestRewardService : IQuestRewardService, IDisposable
{
    // === EventBus: паблишеры ===
    [Inject] private readonly IPublisher<ItemAddRequestEvent> _itemAddRequestPub = null!;
    [Inject] private readonly IPublisher<QiAddRequestEvent> _qiAddRequestPub = null!;
    [Inject] private readonly IPublisher<QuestRewardGrantedEvent> _rewardGrantedPub = null!;

    // Подписка на QuestCompletedEvent для автовойды наград
    [Inject] private readonly ISubscriber<QuestCompletedEvent> _questCompletedSub = null!;
    private IDisposable? _questCompletedSubscription;

    // === Ссылка на QuestService (внутримодульная) ===
    [Inject] private readonly QuestService _questService = null!;

    /// <summary>
    /// Инициализация: подписка на QuestCompletedEvent.
    /// Вызывается из QuestModule.Start().
    /// </summary>
    public void Initialize()
    {
        _questCompletedSubscription = _questCompletedSub.Subscribe(OnQuestCompleted);
    }

    private void OnQuestCompleted(in QuestCompletedEvent e)
    {
        GrantRewards(e.QuestId);
    }

    /// <summary>
    /// Выдать все награды за квест.
    /// Публикует command-события для каждого типа награды.
    /// </summary>
    public bool GrantRewards(string questId)
    {
        if (AreRewardsGranted(questId)) return false;

        var quest = _questService.GetQuestData(questId);
        if (quest == null) return false;
        if (quest.Status != QuestStatus.Completed) return false;

        for (int i = 0; i < quest.Rewards.Count; i++)
        {
            var reward = quest.Rewards[i];
            GrantSingleReward(questId, reward);
        }

        _questService.MarkRewardsGranted(questId);
        return true;
    }

    /// <summary>
    /// Были ли награды уже выданы за квест.
    /// </summary>
    public bool AreRewardsGranted(string questId)
    {
        return _questService.AreRewardsGrantedInternal(questId);
    }

    /// <summary>
    /// Выдать одну награду.
    /// A04 (FIX): Событие публикуется ТОЛЬКО для фактически обработанных типов.
    /// </summary>
    private void GrantSingleReward(string questId, QuestReward reward)
    {
        bool granted = false;

        switch (reward.Type)
        {
            case QuestRewardType.Item:
                _itemAddRequestPub.Publish(new ItemAddRequestEvent(
                    reward.TargetId, reward.Amount, "quest_reward"));
                granted = true;
                break;

            case QuestRewardType.Qi:
                _qiAddRequestPub.Publish(new QiAddRequestEvent(
                    reward.Amount, "quest_reward", string.Empty));
                granted = true;
                break;

            case QuestRewardType.Experience:
            case QuestRewardType.Technique:
            case QuestRewardType.FactionRep:
                // Будущее расширение — не публикуем событие, пока не реализовано
                break;
        }

        if (granted)
        {
            _rewardGrantedPub.Publish(new QuestRewardGrantedEvent(
                questId, reward.RewardId, reward.Type, reward.Amount));
        }
    }

    public void Dispose()
    {
        _questCompletedSubscription?.Dispose();
        _questCompletedSubscription = null;
    }
}

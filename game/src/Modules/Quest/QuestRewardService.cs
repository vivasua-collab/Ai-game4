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
    // Review этап 7 (P1-2): валидация item-наград ДО публикации (паттерн
    // санкционированных исключений — IItemDatabaseService инжектят InventoryModule,
    // StorageRingService, CraftingService; ItemDatabase — общие данные).
    [Inject] private readonly IItemDatabaseService? _itemDb = null;
    // Review этап 7: тост при отказе (не молчать — игрок должен знать).
    [Inject] private readonly IPublisher<ToastShownEvent>? _toastPub = null;

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
    /// Review этап 7 (P1-2): item-награды валидируются через IItemDatabaseService
    /// ДО публикации — неизвестные базе предметы не «выдаются в пустоту»;
    /// квест НЕ помечается выданным (повторная попытка возможна после починки контента).
    /// BUG-024 (Этап-7 аудита): превалидация ВСЕГО набора наград ДО выдачи —
    /// неподдерживаемые типы (Experience/Technique/FactionRep: ветки без
    /// реальной выдачи) ранее молча терялись, а квест ПОМЕЧАЛСЯ
    /// награждённым (MarkRewardsGranted) — повторная выдача блокировалась.
    /// Теперь: неподдерживаемый тип = явный отказ с диагностикой, квест
    /// остаётся НЕ-награждённым до починки контента или реализации типа.
    /// </summary>
    public bool GrantRewards(string questId)
    {
        if (AreRewardsGranted(questId)) return false;

        var quest = _questService.GetQuestData(questId);
        if (quest == null) return false;
        if (quest.Status != QuestStatus.Completed) return false;

        // Review этап 7 (P1-2) + BUG-024 (Этап-7): предварительная валидация
        // ВСЕХ наград: поддержанные типы (Item/Qi) + известность предмета.
        for (int i = 0; i < quest.Rewards.Count; i++)
        {
            var reward = quest.Rewards[i];

            // BUG-024: реализованы только Item и Ци. Прежде Experience/
            // Technique/FactionRep проходили в GrantSingleReward, там
            // молча не выдавались — а весь набор помечался выданным.
            if (reward.Type != QuestRewardType.Item && reward.Type != QuestRewardType.Qi)
            {
                Console.WriteLine($"[QuestRewardService] Награда '{reward.RewardId}' квеста '{questId}': " +
                                  $"тип {reward.Type} не реализован — выдача отклонена ЦЕЛИКОМ " +
                                  $"(квест останется непогашенным, повтор после реализации/починки контента)");
                _toastPub?.Publish(new ToastShownEvent(
                    $"⚠ Награда «{reward.Type}» не реализована: выдача отложена " +
                    $"(квест не помечен награждённым)", 4.0f));
                return false;
            }

            if (reward.Type == QuestRewardType.Item
                && _itemDb != null && !_itemDb.TryGetItem(reward.TargetId, out _))
            {
                Console.WriteLine($"[QuestRewardService] Награда '{reward.RewardId}' квеста '{questId}': " +
                                  $"предмет '{reward.TargetId}' не найден в ItemDatabase — выдача отклонена");
                _toastPub?.Publish(new ToastShownEvent(
                    $"⚠ Награда «{reward.TargetId}» не выдана: предмет не зарегистрирован", 3.0f));
                return false;
            }
        }

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
                // BUG-024: недостижимо — превалидация GrantRewards отклоняет
                // выдачу целиком, пока тип не реализован. Ветка осталась как
                // защита от новых точек входа (never-grant вместо was-granted).
                Console.WriteLine($"[QuestRewardService] GrantSingleReward: тип {reward.Type} " +
                                  $"не реализован — награда '{reward.RewardId}' НЕ выдана");
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

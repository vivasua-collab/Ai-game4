#nullable enable
// Создано: 2026-05-09 — Phase 12: реализация IQuestService
// Migrated from Ai-game3 (Unity) to Ai-game4 (Godot) 2026-08-15.
//
// Управление жизненным циклом квестов: старт, прогресс, завершение, провал.
// EVT-01: Все кросс-модульные взаимодействия — через EventBus.
// Hub-and-Spoke: QuestService НЕ инжектит сервисы других модулей.
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Modules.Quest.Data;

namespace CultivationGame.Modules.Quest;

/// <summary>
/// Реализация IQuestService. Управляет жизненным циклом квестов.
///
/// АРХИТЕКТУРА (EVT-01): Quest модуль НЕ инжектит сервисы других модулей.
/// Все кросс-модульные взаимодействия — ТОЛЬКО через EventBus:
/// - QuestStartedEvent, QuestCompletedEvent, QuestFailedEvent, QuestAbandonedEvent — публикация
/// - EnemyKilledEvent, ItemAddedEvent, LocationChangedEvent и др. — подписка (через QuestProgressTracker)
/// </summary>
public class QuestService : IQuestService, IDisposable
{
    // === EventBus: паблишеры ===
    [Inject] private readonly IPublisher<QuestStartedEvent> _questStartedPub = null!;
    [Inject] private readonly IPublisher<QuestObjectiveUpdatedEvent> _objectiveUpdatedPub = null!;
    [Inject] private readonly IPublisher<QuestCompletedEvent> _questCompletedPub = null!;
    [Inject] private readonly IPublisher<QuestFailedEvent> _questFailedPub = null!;
    [Inject] private readonly IPublisher<QuestAbandonedEvent> _questAbandonedPub = null!;

    // === Хранилище квестов ===
    private readonly Dictionary<string, QuestData> _allQuests = new();
    private readonly List<string> _activeQuestIds = new();
    private readonly HashSet<string> _rewardedQuestIds = new();

    // === Зависимости (внутримодульные) ===
    [Inject] private readonly QuestProgressTracker? _progressTracker;

    // === Конфигурация ===
    private QuestConfig? _config;

    // === Текущий игровой день (для проверки сроков) ===
    private int _currentDay;

    /// <summary>
    /// Инициализация с конфигурацией и базовыми квестами.
    /// Вызывается из QuestModule.Start().
    /// </summary>
    public void Initialize(QuestConfig config)
    {
        _config = config;
        RegisterDefaultQuests();
        _progressTracker?.Initialize(this);
    }

    // === IQuestService ===

    public bool StartQuest(string questId)
    {
        if (!_allQuests.TryGetValue(questId, out var quest)) return false;
        if (quest.Status != QuestStatus.NotStarted) return false;
        if (_config != null && _activeQuestIds.Count >= _config.MaxActiveQuests) return false;

        // Проверка предпосылки
        if (!string.IsNullOrEmpty(quest.PrerequisiteQuestId))
        {
            if (!IsQuestComplete(quest.PrerequisiteQuestId)) return false;
        }

        quest.Status = QuestStatus.Active;
        quest.StartDay = _currentDay;
        _activeQuestIds.Add(questId);

        _questStartedPub.Publish(new QuestStartedEvent(questId, quest.Type));
        return true;
    }

    public bool AbandonQuest(string questId)
    {
        if (!_allQuests.TryGetValue(questId, out var quest)) return false;
        if (quest.Status != QuestStatus.Active) return false;

        quest.Status = QuestStatus.Abandoned;
        _activeQuestIds.Remove(questId);

        // Сброс прогресса целей
        for (int i = 0; i < quest.Objectives.Count; i++)
        {
            quest.Objectives[i].Reset();
        }

        _questAbandonedPub.Publish(new QuestAbandonedEvent(questId));
        return true;
    }

    public bool CompleteQuest(string questId)
    {
        if (!_allQuests.TryGetValue(questId, out var quest)) return false;
        if (quest.Status != QuestStatus.Active) return false;
        if (!quest.AllObjectivesComplete) return false;

        quest.Status = QuestStatus.Completed;
        _activeQuestIds.Remove(questId);

        _questCompletedPub.Publish(new QuestCompletedEvent(questId));
        return true;
    }

    public bool FailQuest(string questId, string reason = "")
    {
        if (!_allQuests.TryGetValue(questId, out var quest)) return false;
        if (quest.Status != QuestStatus.Active) return false;

        quest.Status = QuestStatus.Failed;
        _activeQuestIds.Remove(questId);

        _questFailedPub.Publish(new QuestFailedEvent(questId, reason));
        return true;
    }

    public IReadOnlyList<string> GetActiveQuestIds() => _activeQuestIds;

    public bool IsQuestComplete(string questId)
    {
        if (!_allQuests.TryGetValue(questId, out var quest)) return false;
        return quest.Status == QuestStatus.Completed;
    }

    public QuestStatus GetQuestStatus(string questId)
    {
        if (!_allQuests.TryGetValue(questId, out var quest)) return QuestStatus.NotStarted;
        return quest.Status;
    }

    public bool QuestExists(string questId) => _allQuests.ContainsKey(questId);

    public QuestType GetQuestType(string questId)
    {
        if (!_allQuests.TryGetValue(questId, out var quest)) return QuestType.Side;
        return quest.Type;
    }

    /// <summary>
    /// 2026-09-04 S1: сводки всех квестов для UI (окно квестов Q).
    /// Map module QuestData → Core-level QuestSummary (read-only DTO).
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<Core.Interfaces.QuestSummary> GetQuestSummaries()
    {
        var list = new System.Collections.Generic.List<Core.Interfaces.QuestSummary>(_allQuests.Count);
        foreach (var q in _allQuests.Values)
        {
            var summary = new Core.Interfaces.QuestSummary
            {
                QuestId = q.QuestId,
                DisplayName = q.DisplayName,
                Description = q.Description,
                Status = q.Status,
                OverallProgress = q.OverallProgress,
                Objectives = new (string, int, int, bool)[q.Objectives.Count],
            };
            for (int i = 0; i < q.Objectives.Count; i++)
            {
                var o = q.Objectives[i];
                string desc = !string.IsNullOrEmpty(o.Description)
                    ? o.Description.Replace("{progress}", o.Progress.ToString())
                                      .Replace("{target}", o.Target.ToString())
                    : $"{o.Type} {o.TargetId}";
                summary.Objectives[i] = (desc, o.Progress, o.Target, o.IsComplete);
            }
            summary.RewardTexts = new string[q.Rewards.Count];
            for (int i = 0; i < q.Rewards.Count; i++)
            {
                var r = q.Rewards[i];
                summary.RewardTexts[i] = r.Type switch
                {
                    QuestRewardType.Qi     => $"Ци +{r.Amount}",
                    QuestRewardType.Item   => $"Предмет {r.TargetId} ×{r.Amount}",
                    QuestRewardType.Technique => $"Свиток техники {r.TargetId}",
                    QuestRewardType.Experience => $"Опыт +{r.Amount}",
                    _ => $"{r.Type} +{r.Amount}",
                };
            }
            list.Add(summary);
        }
        return list;
    }

    // === Дополнительные методы (внутримодульные) ===

    /// <summary>
    /// Получить QuestData по идентификатору (для QuestProgressTracker/QuestRewardService).
    /// </summary>
    internal QuestData? GetQuestData(string questId)
    {
        _allQuests.TryGetValue(questId, out var quest);
        return quest;
    }

    /// <summary>
    /// Итерация по всем активным квестам с callback. Zero-allocation.
    /// </summary>
    internal void ForEachActiveQuest(Action<QuestData> action)
    {
        for (int i = 0; i < _activeQuestIds.Count; i++)
        {
            if (_allQuests.TryGetValue(_activeQuestIds[i], out var quest))
                action(quest);
        }
    }

    /// <summary>
    /// Обновить прогресс цели и опубликовать событие.
    /// </summary>
    internal void UpdateObjectiveProgress(string questId, string objectiveId, int progress, int target)
    {
        _objectiveUpdatedPub.Publish(new QuestObjectiveUpdatedEvent(questId, objectiveId, progress, target));
    }

    /// <summary>Отметить награду как выданную.</summary>
    internal void MarkRewardsGranted(string questId) => _rewardedQuestIds.Add(questId);

    /// <summary>Были ли награды выданы.</summary>
    internal bool AreRewardsGrantedInternal(string questId) => _rewardedQuestIds.Contains(questId);

    /// <summary>
    /// Обновить текущий день (для проверки сроков).
    /// </summary>
    internal void UpdateDay(int day)
    {
        _currentDay = day;
        for (int i = _activeQuestIds.Count - 1; i >= 0; i--)
        {
            var questId = _activeQuestIds[i];
            if (_allQuests.TryGetValue(questId, out var quest))
            {
                if (quest.IsExpired(_currentDay))
                {
                    FailQuest(questId, "time_expired");
                }
            }
        }
    }

    /// <summary>Зарегистрировать квест в системе.</summary>
    internal void RegisterQuest(QuestData questData)
    {
        if (questData == null || string.IsNullOrEmpty(questData.QuestId)) return;
        _allQuests[questData.QuestId] = questData;
    }

    /// <summary>
    /// Регистрация базовых квестов по умолчанию.
    /// </summary>
    private void RegisterDefaultQuests()
    {
        RegisterQuest(new QuestData
        {
            QuestId = "quest_kill_wolves",
            DisplayName = "Охота на волков",
            Description = "Волки терроризируют окрестности. Убей 3 волков.",
            Type = QuestType.AutoGenerated,
            Status = QuestStatus.NotStarted,
            Objectives =
            {
                new QuestObjective
                {
                    ObjectiveId = "kill_wolves",
                    Type = QuestObjectiveType.KillEnemy,
                    TargetId = "wolf",
                    Target = 3,
                    Description = "Убей волков: {progress}/{target}"
                }
            },
            Rewards =
            {
                new QuestReward
                {
                    RewardId = "quest_kill_wolves_qi",
                    Type = QuestRewardType.Qi,
                    Amount = 100
                }
            }
        });

        RegisterQuest(new QuestData
        {
            QuestId = "quest_gather_iron",
            DisplayName = "Железная жила",
            Description = "Кузнецу нужно железо. Собери 5 единиц железной руды.",
            Type = QuestType.AutoGenerated,
            Status = QuestStatus.NotStarted,
            Objectives =
            {
                new QuestObjective
                {
                    ObjectiveId = "gather_iron",
                    Type = QuestObjectiveType.GatherItem,
                    TargetId = "iron",
                    Target = 5,
                    Description = "Собери железо: {progress}/{target}"
                }
            },
            Rewards =
            {
                new QuestReward
                {
                    RewardId = "quest_gather_iron_item",
                    Type = QuestRewardType.Item,
                    TargetId = "steel",
                    Amount = 2
                }
            }
        });

        RegisterQuest(new QuestData
        {
            QuestId = "quest_reach_forest",
            DisplayName = "Исследование леса",
            Description = "Старейшина просит разведать Тёмный лес.",
            Type = QuestType.Side,
            Status = QuestStatus.NotStarted,
            Objectives =
            {
                new QuestObjective
                {
                    ObjectiveId = "reach_forest",
                    Type = QuestObjectiveType.ReachLocation,
                    TargetId = "forest",
                    Target = 1,
                    Description = "Достигни Тёмного леса"
                }
            },
            Rewards =
            {
                new QuestReward
                {
                    RewardId = "quest_reach_forest_qi",
                    Type = QuestRewardType.Qi,
                    Amount = 200
                }
            }
        });

        RegisterQuest(new QuestData
        {
            QuestId = "quest_talk_elder",
            DisplayName = "Совет старейшины",
            Description = "Старейшина деревни хочет с тобой поговорить.",
            Type = QuestType.Side,
            Status = QuestStatus.NotStarted,
            Objectives =
            {
                new QuestObjective
                {
                    ObjectiveId = "talk_elder",
                    Type = QuestObjectiveType.TalkToNPC,
                    TargetId = "elder_01",
                    Target = 1,
                    Description = "Поговори со старейшиной"
                }
            },
            Rewards =
            {
                new QuestReward
                {
                    RewardId = "quest_talk_elder_qi",
                    Type = QuestRewardType.Qi,
                    Amount = 50
                }
            }
        });
    }

    public void Dispose()
    {
        _progressTracker?.Dispose();
        _activeQuestIds.Clear();
        _allQuests.Clear();
        _rewardedQuestIds.Clear();
    }
}

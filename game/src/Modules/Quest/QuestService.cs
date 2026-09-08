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
using CultivationGame.Core.Helpers;
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
    // Review этап 7 (P2-5): тосты обратной связи приёма квестов (паттерн QiStone).
    [Inject] private readonly IPublisher<ToastShownEvent>? _toastPub = null;

    // === EventBus: подписки (Review этап 7) ===
    // P1-3: QuestStartRequestedEvent — команда «принять квест» из диалога.
    [Inject] private readonly ISubscriber<QuestStartRequestedEvent>? _questStartRequestSub = null;
    // P2-5: кэш уровня культивации игрока из QiChangedEvent (EVT-01: без
    // инъекции IQiService — тот же паттерн кэша, что в CombatService).
    [Inject] private readonly ISubscriber<QiChangedEvent>? _qiChangedSub = null;
    private IDisposable? _questStartRequestSubscription;
    private IDisposable? _qiChangedForLevelSubscription;
    private int _playerCultivationLevel = 1;

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

        // Review этап 7 (P1-3): команда принятия квеста из игрового контента
        // (выбор «Конечно, помогу» в диалоге старейшины).
        _questStartRequestSubscription?.Dispose();
        _questStartRequestSubscription = _questStartRequestSub?.Subscribe(OnQuestStartRequested);

        // Review этап 7 (P2-5): кэш уровня игрока для проверки
        // RequiredCultivationLevel (QiChangedEvent от QiService игрока).
        _qiChangedForLevelSubscription?.Dispose();
        _qiChangedForLevelSubscription = _qiChangedSub?.Subscribe(OnQiChangedForLevel);
    }

    private void OnQuestStartRequested(in QuestStartRequestedEvent e)
    {
        // P1-3: команда из диалога — с честной обратной связью (тост).
        bool started = StartQuest(e.QuestId);
        if (_toastPub != null)
        {
            if (started)
            {
                _toastPub.Publish(new ToastShownEvent($"📜 Квест принят: {QuestDisplayName(e.QuestId)}", 2.5f));
            }
            else
            {
                string why = StartQuestRejectionReason(e.QuestId);
                _toastPub.Publish(new ToastShownEvent($"📜 Квест «{QuestDisplayName(e.QuestId)}» не принят: {why}", 2.5f));
            }
        }
    }

    private void OnQiChangedForLevel(in QiChangedEvent e)
    {
        if (string.IsNullOrEmpty(e.EntityId) || PlayerIdResolver.IsPlayer(e.EntityId))
            _playerCultivationLevel = e.CultivationLevel;
    }

    private string QuestDisplayName(string questId)
        => _allQuests.TryGetValue(questId, out var q) ? q.DisplayName : questId;

    /// <summary>Review этап 7: человекочитаемая причина отказа StartQuest (для тоста).</summary>
    private string StartQuestRejectionReason(string questId)
    {
        if (!_allQuests.TryGetValue(questId, out var quest)) return "квест не найден";
        if (quest.Status != QuestStatus.NotStarted) return "уже взят или завершён";
        if (_config != null && _activeQuestIds.Count >= _config.MaxActiveQuests) return "слишком много активных квестов";
        if (!string.IsNullOrEmpty(quest.PrerequisiteQuestId) && !IsQuestComplete(quest.PrerequisiteQuestId))
            return "не выполнено условие (предквест)";
        if (quest.RequiredCultivationLevel > 0 && _playerCultivationLevel < quest.RequiredCultivationLevel)
            return $"нужен уровень культивации {quest.RequiredCultivationLevel}";
        return "неизвестная причина";
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

        // Review этап 7 (P2-5): гейт уровня культивации (поле существовало,
        // но не проверялось — gated-квесты можно было взять раньше срока).
        if (quest.RequiredCultivationLevel > 0 && _playerCultivationLevel < quest.RequiredCultivationLevel)
            return false;

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
    /// Review этап 7 (P0-1): TargetId квестов — РЕАЛЬНЫЕ доменные идентификаторы
    /// (инстанс-ID сущностей динамические и не могут быть семантической целью):
    /// - волки: конвенция AnimalService animal_{species}_{n} → нормализация в
    ///   QuestProgressTracker ("animal_wolf_5" → "wolf");
    /// - руда: canonical item ID material_iron_ore (было "iron" — совпадений нет);
    /// - старейшина: роль NPC "Elder" (NPCInteractedEvent.RoleId; спавн —
    ///   HumanNPCSpawnPhase);
    /// - quest_reach_forest НЕ регистрируется: физический переход между
    ///   локациями не реализован (review-6 P1-2) — невыполнимый квест в UI
    ///   сломан бы как обещание (перерегистрируем с travel-pipeline).
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
                    // Review этап 7 (P0-1): canonical item ID (было "iron" —
                    // ItemAddedEvent.ItemId = material_iron_ore, совпадений нет).
                    TargetId = "material_iron_ore",
                    Target = 5,
                    Description = "Собери железную руду: {progress}/{target}"
                }
            },
            Rewards =
            {
                new QuestReward
                {
                    RewardId = "quest_gather_iron_item",
                    Type = QuestRewardType.Item,
                    // Review этап 7 (P1-2): «steel» НЕ существовал в ItemDatabase
                    // (steel — только материалная характеристика MaterialService) —
                    // награда «выдавалась» в пустоту. Теперь: канонический слиток,
                    // регистрируется StartingGearPhase.
                    TargetId = "material_steel_ingot",
                    Amount = 2
                }
            }
        });

        // Review этап 7 (P0-1): quest_reach_forest НЕ регистрируется —
        // физический travel не реализован (см. шапку метода).

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
                    // Review этап 7 (P0-1): семантическая ЦЕЛЬ — роль NPC
                    // (было "elder_01" — фиксированный ID, который не спавнился;
                    // реальные NPC имеют динамические npc_xxx). NPCInteractedEvent
                    // несёт RoleId; старейшина спавнится HumanNPCSpawnPhase (Elder).
                    TargetId = "Elder",
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
        _questStartRequestSubscription?.Dispose();
        _questStartRequestSubscription = null;
        _qiChangedForLevelSubscription?.Dispose();
        _qiChangedForLevelSubscription = null;
        _progressTracker?.Dispose();
        _activeQuestIds.Clear();
        _allQuests.Clear();
        _rewardedQuestIds.Clear();
    }
}

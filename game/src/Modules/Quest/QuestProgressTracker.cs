#nullable enable
// Создано: 2026-05-09 — Phase 12: отслеживание прогресса квестов через EventBus
// Migrated from Ai-game3 (Unity) to Ai-game4 (Godot) 2026-08-15.
//
// Подписывается на кросс-модульные события и обновляет прогресс целей.
// EVT-01: НЕ инжектит сервисы других модулей — только подписки EventBus.
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Modules.Quest.Data;

namespace CultivationGame.Modules.Quest;

/// <summary>
/// Отслеживание прогресса квестов.
/// Подписывается на кросс-модульные события через EventBus
/// и обновляет прогресс соответствующих целей.
/// </summary>
public class QuestProgressTracker : IDisposable
{
    // === EventBus: подписки ===
    [Inject] private readonly ISubscriber<EnemyKilledEvent> _enemyKilledSub = null!;
    [Inject] private readonly ISubscriber<ItemAddedEvent> _itemAddedSub = null!;
    [Inject] private readonly ISubscriber<LocationChangedEvent> _locationChangedSub = null!;
    [Inject] private readonly ISubscriber<NPCInteractedEvent> _npcInteractedSub = null!;
    [Inject] private readonly ISubscriber<CultivationBreakthroughEvent> _breakthroughSub = null!;
    [Inject] private readonly ISubscriber<DayChangedEvent> _dayChangedSub = null!;

    // === IDisposable подписок ===
    private IDisposable? _enemyKilledSubscription;
    private IDisposable? _itemAddedSubscription;
    private IDisposable? _locationChangedSubscription;
    private IDisposable? _npcInteractedSubscription;
    private IDisposable? _breakthroughSubscription;
    private IDisposable? _dayChangedSubscription;

    // === Ссылка на QuestService (внутримодульная) ===
    private QuestService? _questService;

    /// <summary>
    /// Инициализация подписок. Вызывается из QuestService.Initialize().
    /// </summary>
    public void Initialize(QuestService questService)
    {
        _questService = questService;

        _enemyKilledSubscription = _enemyKilledSub.Subscribe(OnEnemyKilled);
        _itemAddedSubscription = _itemAddedSub.Subscribe(OnItemAdded);
        _locationChangedSubscription = _locationChangedSub.Subscribe(OnLocationChanged);
        _npcInteractedSubscription = _npcInteractedSub.Subscribe(OnNPCInteracted);
        _breakthroughSubscription = _breakthroughSub.Subscribe(OnBreakthrough);
        _dayChangedSubscription = _dayChangedSub.Subscribe(OnDayChanged);
    }

    /// <summary>
    /// Обобщённый обработчик обновления целей.
    /// </summary>
    private void ProcessObjectiveUpdate(QuestObjectiveType type, string? targetId, int amount)
    {
        if (_questService == null) return;
        _questService.ForEachActiveQuest(quest =>
        {
            for (int o = 0; o < quest.Objectives.Count; o++)
            {
                var obj = quest.Objectives[o];
                if (obj.Type == type && (targetId == null || obj.TargetId == targetId))
                {
                    bool justCompleted = obj.AddProgress(amount);
                    _questService.UpdateObjectiveProgress(quest.QuestId, obj.ObjectiveId, obj.Progress, obj.Target);

                    if (justCompleted && quest.AllObjectivesComplete)
                    {
                        _questService.CompleteQuest(quest.QuestId);
                    }
                }
            }
        });
    }

    private void OnEnemyKilled(in EnemyKilledEvent e)
        => ProcessObjectiveUpdate(QuestObjectiveType.KillEnemy, e.EnemyId, 1);

    private void OnItemAdded(in ItemAddedEvent e)
        => ProcessObjectiveUpdate(QuestObjectiveType.GatherItem, e.ItemId, e.Count);

    private void OnLocationChanged(in LocationChangedEvent e)
        => ProcessObjectiveUpdate(QuestObjectiveType.ReachLocation, e.NewLocationId, 1);

    private void OnNPCInteracted(in NPCInteractedEvent e)
        => ProcessObjectiveUpdate(QuestObjectiveType.TalkToNPC, e.NpcId, 1);

    private void OnBreakthrough(in CultivationBreakthroughEvent e)
    {
        if (!e.Success) return;
        if (_questService == null) return;
        int breakthroughLevel = e.Level;
        _questService.ForEachActiveQuest(quest =>
        {
            for (int o = 0; o < quest.Objectives.Count; o++)
            {
                var obj = quest.Objectives[o];
                if (obj.Type == QuestObjectiveType.ReachCultivationLevel)
                {
                    if (int.TryParse(obj.TargetId, out int requiredLevel) && breakthroughLevel >= requiredLevel)
                    {
                        bool justCompleted = obj.AddProgress(1);
                        _questService.UpdateObjectiveProgress(quest.QuestId, obj.ObjectiveId, obj.Progress, obj.Target);

                        if (justCompleted && quest.AllObjectivesComplete)
                        {
                            _questService.CompleteQuest(quest.QuestId);
                        }
                    }
                }
            }
        });
    }

    private void OnDayChanged(in DayChangedEvent e)
    {
        if (_questService == null) return;
        _questService.UpdateDay(e.Day);
        ProcessObjectiveUpdate(QuestObjectiveType.SurviveDays, null, 1);
    }

    public void Dispose()
    {
        _enemyKilledSubscription?.Dispose();
        _itemAddedSubscription?.Dispose();
        _locationChangedSubscription?.Dispose();
        _npcInteractedSubscription?.Dispose();
        _breakthroughSubscription?.Dispose();
        _dayChangedSubscription?.Dispose();

        _enemyKilledSubscription = null;
        _itemAddedSubscription = null;
        _locationChangedSubscription = null;
        _npcInteractedSubscription = null;
        _breakthroughSubscription = null;
        _dayChangedSubscription = null;
    }
}

#nullable enable
// Создано: 2026-05-09 — Phase 12: точка входа модуля Quest
// Migrated from Ai-game3 (Unity) to Ai-game4 (Godot) 2026-08-15.
//
// IModule — инициализация, ITickable — per-tick проверка (если нужна),
// IDisposable — очистка.
using System;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Quest;

/// <summary>
/// Точка входа модуля Quest.
/// Инициализирует сервисы конфигурацией.
///
/// Подписки сервисов (через EventBus):
/// - QuestProgressTracker: EnemyKilledEvent, ItemAddedEvent, LocationChangedEvent,
///   NPCInteractedEvent, CultivationBreakthroughEvent, DayChangedEvent
///
/// Публикации (через EventBus):
/// - QuestService: QuestStartedEvent, QuestObjectiveUpdatedEvent, QuestCompletedEvent,
///   QuestFailedEvent, QuestAbandonedEvent
/// - QuestRewardService: ItemAddRequestEvent, QiAddRequestEvent, QuestRewardGrantedEvent
///
/// АРХИТЕКТУРА (EVT-01): Модуль Quest полностью независим.
/// Все кросс-модульные взаимодействия — через EventBus.
/// </summary>
public sealed class QuestModule : IModule
{
    public string ModuleName => "Quest";

    [Inject] private readonly IQuestService _questService = null!;
    [Inject] private readonly QuestService _questServiceImpl = null!;
    [Inject] private readonly IQuestRewardService _rewardService = null!;
    [Inject] private readonly QuestRewardService _rewardServiceImpl = null!;

    private QuestConfig _config = new();
    private bool _isConfigured;

    /// <summary>
    /// Установить конфигурацию модуля. Вызывается из сборщика сцены до Start().
    /// </summary>
    public void SetConfig(QuestConfig config)
    {
        _config = config;
        _isConfigured = true;
    }

    public void Start()
    {
        if (!_isConfigured)
        {
            _config = new QuestConfig();
            _isConfigured = true;
        }

        _questServiceImpl.Initialize(_config);
        _rewardServiceImpl.Initialize();
        Console.WriteLine("[QuestModule] Started");
    }

    public void Tick(int tickCount)
    {
        // Quests are event-driven — no per-tick work in V1.
    }

    public void Dispose()
    {
        _questServiceImpl.Dispose();
        _rewardServiceImpl.Dispose();
        Console.WriteLine("[QuestModule] Disposed");
    }
}

/// <summary>
/// Делегат регистрации публичных сервисов модуля Quest.
/// </summary>
public static class QuestModuleServices
{
    public static void Register(IContainerBuilder builder)
    {
        builder.Register<QuestConfig>(Lifetime.Singleton);
        builder.Register<QuestProgressTracker>(Lifetime.Singleton);
        builder.Register<QuestService>(Lifetime.Singleton);
        builder.Register<IQuestService, QuestService>(Lifetime.Singleton);
        builder.Register<QuestRewardService>(Lifetime.Singleton);
        builder.Register<IQuestRewardService, QuestRewardService>(Lifetime.Singleton);
        builder.Register<QuestModule>(Lifetime.Singleton);
    }
}

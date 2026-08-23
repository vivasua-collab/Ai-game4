#nullable enable
// Создано: 2026-05-09 04:35:17 UTC
// Точка входа модуля Ци.
// IStartable — инициализация, ITickable — кадровая регенерация
// Migrated from Ai-game3 (Unity+VContainer) to Ai-game4 (Godot+DI+EventBus) 2026-08-15:
//   - IStartable.Start() → IModule.Start()
//   - ITickable.Tick() → IModule.Tick(int tickCount)
//   - Uses ITimeService.DeltaTime (engine-agnostic, NOT UnityEngine.Time).
// 2026-08-23 — Этап 1 внедрения ЦИ: медитация (QI_SYSTEM.md §5.2).
//   QiModule владеет состоянием медитации игрока: подписка на
//   MeditationToggleRequestedEvent, поглощение из среды в Tick,
//   публикация MeditationStateChangedEvent. Отмена при бое (CombatStartedEvent).
using System;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Qi;

/// <summary>
/// Точка входа модуля Qi.
/// Инициализирует QiService конфигурацией и запускает регенерацию.
/// BD-42 урок: Использует ITimeService.DeltaTime вместо UnityEngine.Time.deltaTime.
/// </summary>
public class QiModule : IModule, IDisposable
{
    [Inject] private readonly IQiService _qiService = null!;
    [Inject] private readonly QiService _qiServiceImpl = null!;
    [Inject] private readonly IQiBufferService _qiBufferService = null!;
    [Inject] private readonly ITimeService _timeService = null!;

    // IMPL-3: Config injected via DI (replaces obsolete SetConfig()).
    [Inject] private readonly QiConfig _config = null!;

    // === Медитация (этап 1 внедрения ЦИ) ===
    [Inject] private readonly IPublisher<MeditationStateChangedEvent> _meditationStatePub = null!;
    [Inject] private readonly ISubscriber<MeditationToggleRequestedEvent> _meditationToggleSub = null!;
    [Inject] private readonly ISubscriber<CombatStartedEvent> _combatStartedSub = null!;

    private IDisposable? _meditationToggleSubscription;
    private IDisposable? _combatStartedSubscription;
    private bool _meditationActive;
    private float _meditationRate;          // ед/сек (кэшируется при активации)
    private double _meditationAccumulator;  // точность малых скоростей

    public string ModuleName => "Qi";

    public void Start()
    {
        _qiServiceImpl.Initialize(_config);

        _meditationToggleSubscription = _meditationToggleSub.Subscribe(OnMeditationToggle);
        _combatStartedSubscription = _combatStartedSub.Subscribe(OnCombatStarted);
    }

    public void Tick(int tickCount)
    {
        // BD-42: Регенерация через ITimeService.DeltaTime (не UnityEngine.Time)
        _qiService.Regenerate(_timeService.DeltaTime);

        // Медитация: поглощение из среды = conductivity × environmentMult (QI_SYSTEM.md §5.2).
        // Скорость ограничена проводимостью меридиан по построению (rate = conductivity × mult).
        if (_meditationActive && _meditationRate > 0f)
        {
            float dt = _timeService.DeltaTime;
            _meditationAccumulator += _meditationRate * dt;
            if (_meditationAccumulator >= 1.0)
            {
                long absorbed = (long)_meditationAccumulator;
                _meditationAccumulator -= absorbed;
                _qiService.AddQi(absorbed);
            }

            // Ядро заполнено — медитация бессмысленна, авто-завершение.
            if (_qiService.IsFull)
            {
                SetMeditation(false);
            }
        }
    }

    /// <summary>Обработка команды переключения медитации.</summary>
    private void OnMeditationToggle(in MeditationToggleRequestedEvent e)
    {
        // Toggle-семантика: DesiredState == текущему → игнор (анти-дребезг).
        if (e.DesiredState == _meditationActive) return;
        SetMeditation(e.DesiredState);
    }

    /// <summary>Бой прерывает медитацию (концентрация на противнике).</summary>
    private void OnCombatStarted(in CombatStartedEvent e)
    {
        if (_meditationActive) SetMeditation(false);
    }

    private void SetMeditation(bool active)
    {
        _meditationActive = active;
        _meditationAccumulator = 0.0;
        _meditationRate = active ? _qiService.Conductivity * GameConstants.ENVIRONMENT_MULT_NORMAL : 0f;

        _meditationStatePub.Publish(new MeditationStateChangedEvent(_meditationActive, _meditationRate));
    }

    public void Dispose()
    {
        _meditationToggleSubscription?.Dispose();
        _meditationToggleSubscription = null;
        _combatStartedSubscription?.Dispose();
        _combatStartedSubscription = null;
        // Services own their subscriptions and dispose themselves.
    }
}

#nullable enable
// Создано: 2026-05-09
// Точка входа модуля формаций.
// Migrated from Ai-game3 (Unity+VContainer+MessagePipe) to Ai-game4 (Godot+DI+EventBus) 2026-08-15.
// Этап 5 внедрения ЦИ: StartDrawing требует свежий кэш Qi создателя; модуль Qi
// стартует РАНЬШЕ (порядок DI) и публикует начальное QiChangedEvent до нашей
// подписки → пере-запрашиваем состояние (QiAddRequestEvent(0) → AddQi(0) →
// QiChangedEvent с текущими значениями, без изменения Ци).
using System;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Formation;

/// <summary>
/// Точка входа модуля формаций.
/// Инициализирует FormationService конфигурацией и обрабатывает тики.
/// </summary>
public class FormationModule : IModule
{
    [Inject] private readonly IFormationService _formationService = null!;
    [Inject] private readonly FormationService _formationServiceImpl = null!;
    [Inject] private readonly ITimeService _timeService = null!;
    [Inject] private readonly IPublisher<QiAddRequestEvent> _qiAddRequestPub = null!;

    // IMPL-3: Config injected via DI (replaces obsolete SetConfig()).
    [Inject] private readonly FormationConfig _config = null!;
    private float _realTimeAccumulator;

    public string ModuleName => "Formation";

    public void Start()
    {
        _formationServiceImpl.Initialize(_config);
        // Этап 5: обновить кэш Qi создателя (см. комментарий в шапке файла).
        _qiAddRequestPub.Publish(new QiAddRequestEvent(0, "FormationModule.Start"));
    }

    public void Tick(int tickCount)
    {
        float delta = _timeService.DeltaTime;

        // Этап 5 внедрения ЦИ: автонаполнение от создателя (conductivity ед/сек).
        _formationServiceImpl.AutoFillTick(delta);

        _realTimeAccumulator += delta;

        if (_realTimeAccumulator >= 1.0f)
        {
            int wholeSeconds = (int)_realTimeAccumulator;
            _realTimeAccumulator -= wholeSeconds;

            int gameMinutes = wholeSeconds;
            if (gameMinutes > 0)
            {
                _formationServiceImpl.ProcessDrainTick(gameMinutes);
            }
        }
    }

    public void Dispose()
    {
        _formationServiceImpl.Dispose();
    }
}

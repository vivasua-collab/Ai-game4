#nullable enable
// Создано: 2026-05-09
// Точка входа модуля формаций.
// Migrated from Ai-game3 (Unity+VContainer+MessagePipe) to Ai-game4 (Godot+DI+EventBus) 2026-08-15.
using System;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

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

    private FormationConfig? _config;
    private bool _isConfigured;
    private float _realTimeAccumulator;

    public string ModuleName => "Formation";

    public void SetConfig(FormationConfig config)
    {
        _config = config;
        _isConfigured = true;
    }

    public void Start()
    {
        if (_isConfigured && _config != null)
        {
            _formationServiceImpl.Initialize(_config);
        }
    }

    public void Tick(int tickCount)
    {
        if (!_isConfigured) return;

        float delta = _timeService.DeltaTime;
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

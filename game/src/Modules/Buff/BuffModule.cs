#nullable enable
// Создано: 2026-05-09 05:15:31 UTC
// Точка входа модуля баффов.
// IStartable — инициализация, ITickable — тикание баффов
// Migrated from Ai-game3 (Unity+VContainer+MessagePipe) to Ai-game4 (Godot+DI+EventBus) 2026-08-15.
using System;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Buff;

/// <summary>
/// Точка входа модуля баффов.
/// Инициализирует BuffService конфигурацией и запускает тикание.
/// BD-42 урок: Использует ITimeService.DeltaTime вместо UnityEngine.Time.deltaTime.
/// </summary>
public class BuffModule : IModule
{
    [Inject] private readonly IBuffService _buffService = null!;
    // Phase 17C: прямая инъекция вместо concrete-cast
    [Inject] private readonly BuffService _buffServiceImpl = null!;
    [Inject] private readonly ITimeService _timeService = null!;

    private BuffConfig? _config;
    private bool _isConfigured;

    public string ModuleName => "Buff";

    /// <summary>
    /// Установить конфигурацию модуля.
    /// Вызывается из BuffModuleServices.Register() до Start().
    /// </summary>
    public void SetConfig(BuffConfig config)
    {
        _config = config;
        _isConfigured = true;
    }

    public void Start()
    {
        // Phase 17C: прямая инъекция вместо concrete-cast
        if (_isConfigured && _config != null)
        {
            _buffServiceImpl.Configure(_config);
        }
    }

    public void Tick(int tickCount)
    {
        // BD-42: Тикание через ITimeService.DeltaTime
        _buffService.TickBuffs(_timeService.DeltaTime);
    }

    public void Dispose()
    {
        // Services own their subscriptions and dispose themselves.
    }
}

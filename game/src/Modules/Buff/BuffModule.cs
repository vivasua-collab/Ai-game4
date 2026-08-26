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

    // IMPL-3: Config injected via DI (replaces obsolete SetConfig()).
    [Inject] private readonly BuffConfig _config = null!;

    public string ModuleName => "Buff";

    public void Start()
    {
        // Phase 17C: прямая инъекция вместо concrete-cast
        _buffServiceImpl.Configure(_config);
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

#nullable enable
// Создано: 2026-05-09 04:35:17 UTC
// Точка входа модуля Ци.
// IStartable — инициализация, ITickable — кадровая регенерация
// Migrated from Ai-game3 (Unity+VContainer) to Ai-game4 (Godot+DI) 2026-08-15:
//   - IStartable.Start() → IModule.Start()
//   - ITickable.Tick() → IModule.Tick(int tickCount)
//   - Uses ITimeService.DeltaTime (engine-agnostic, NOT UnityEngine.Time).
using System;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Qi;

/// <summary>
/// Точка входа модуля Qi.
/// Инициализирует QiService конфигурацией и запускает регенерацию.
/// BD-42 урок: Использует ITimeService.DeltaTime вместо UnityEngine.Time.deltaTime.
/// </summary>
public class QiModule : IModule
{
    [Inject] private readonly IQiService _qiService = null!;
    [Inject] private readonly QiService _qiServiceImpl = null!;
    [Inject] private readonly IQiBufferService _qiBufferService = null!;
    [Inject] private readonly ITimeService _timeService = null!;

    // IMPL-3: Config injected via DI (replaces obsolete SetConfig()).
    [Inject] private readonly QiConfig _config = null!;

    public string ModuleName => "Qi";

    public void Start()
    {
        _qiServiceImpl.Initialize(_config);
    }

    public void Tick(int tickCount)
    {
        // BD-42: Регенерация через ITimeService.DeltaTime (не UnityEngine.Time)
        _qiService.Regenerate(_timeService.DeltaTime);
    }

    public void Dispose()
    {
        // Services own their subscriptions and dispose themselves.
    }
}

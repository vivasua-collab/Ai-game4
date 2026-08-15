#nullable enable
// Создано: 2026-05-09 — Phase 14: точка входа модуля UI
// Migrated from Ai-game3 (Unity) to Ai-game4 (Godot) 2026-08-15.
using System;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.UI;

/// <summary>
/// Точка входа модуля UI.
/// Инициализирует сервисы конфигурацией и обрабатывает тики (Toast TTL).
/// </summary>
public sealed class UIModule : IModule
{
    public string ModuleName => "UI";

    [Inject] private readonly IUIService _uiService = null!;
    [Inject] private readonly UIService _uiServiceImpl = null!;
    [Inject] private readonly ToastService _toastService = null!;
    [Inject] private readonly InputLogService _inputLogService = null!;
    [Inject] private readonly ITimeService _timeService = null!;

    private UIConfig _config = new();
    private bool _isConfigured;

    /// <summary>Установить конфигурацию модуля.</summary>
    public void SetConfig(UIConfig config)
    {
        _config = config;
        _isConfigured = true;
    }

    public void Start()
    {
        if (!_isConfigured)
        {
            _config = new UIConfig();
            _isConfigured = true;
        }

        _uiServiceImpl.Initialize(_config);
        _toastService.Initialize(_config);
        _inputLogService.Initialize();

        // Show HUD by default.
        _uiService.ShowView("HUD");
        Console.WriteLine("[UIModule] Started — HUD shown");
    }

    public void Tick(int tickCount)
    {
        float dt = _timeService.DeltaTime;
        _toastService.Tick(dt);
        _inputLogService.Tick(dt);
    }

    public void Dispose()
    {
        _uiServiceImpl.Dispose();
        _toastService.Dispose();
        _inputLogService.Dispose();
        Console.WriteLine("[UIModule] Disposed");
    }
}

/// <summary>
/// Делегат регистрации публичных сервисов модуля UI.
/// </summary>
public static class UIModuleServices
{
    public static void Register(IContainerBuilder builder)
    {
        builder.Register<UIConfig>(Lifetime.Singleton);
        builder.Register<UIService>(Lifetime.Singleton);
        builder.Register<IUIService, UIService>(Lifetime.Singleton);
        builder.Register<ToastService>(Lifetime.Singleton);
        builder.Register<InputLogService>(Lifetime.Singleton);
        builder.Register<IInputLogService, InputLogService>(Lifetime.Singleton);
        builder.Register<UIModule>(Lifetime.Singleton);
    }
}

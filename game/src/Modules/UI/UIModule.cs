#nullable enable
using System;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.UI;

/// <summary>UI module — V1 stub. View lifecycle is event-driven.</summary>
public sealed class UIModule : IModule
{
    public string ModuleName => "UI";

    [Inject] private readonly IUIService _uiService = null!;

    public void Start()
    {
        // Show HUD by default
        _uiService.ShowView("HUD");
        Console.WriteLine("[UIModule] Started — HUD shown");
    }

    public void Tick(int tickCount)
    {
        // V1 stub — adapter-layer presenter renders notifications.
    }

    public void Dispose()
    {
        Console.WriteLine("[UIModule] Disposed");
    }
}

public static class UIModuleServices
{
    public static void Register(IContainerBuilder builder)
    {
        builder.Register<UIConfig>(Lifetime.Singleton);
        builder.Register<UIService>(Lifetime.Singleton);
        builder.Register<IUIService, UIService>(Lifetime.Singleton);
        builder.Register<UIModule>(Lifetime.Singleton);
    }
}

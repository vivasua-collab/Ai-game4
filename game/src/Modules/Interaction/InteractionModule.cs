#nullable enable
using System;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Interaction;

/// <summary>Interaction module — V1 stub, no per-tick work.</summary>
public sealed class InteractionModule : IModule
{
    public string ModuleName => "Interaction";

    [Inject] private readonly IInteractionService _interactionService = null!;

    public void Start()
    {
        // V1 stub: InteractionService is wired but not yet used per-tick.
        Console.WriteLine($"[InteractionModule] Started — svc wired={_interactionService != null}");
    }

    public void Tick(int tickCount)
    {
        // V1 stub — no per-tick work. Real impl listens for player input flags
        // and triggers Interact on the nearest interactable.
    }

    public void Dispose()
    {
        Console.WriteLine("[InteractionModule] Disposed");
    }
}

public static class InteractionModuleServices
{
    public static void Register(IContainerBuilder builder)
    {
        builder.Register<InteractionConfig>(Lifetime.Singleton);
        builder.Register<InteractionService>(Lifetime.Singleton);
        builder.Register<IInteractionService, InteractionService>(Lifetime.Singleton);
        builder.Register<InteractionModule>(Lifetime.Singleton);
    }
}

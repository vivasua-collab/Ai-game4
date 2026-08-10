#nullable enable
using System;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Body;

/// <summary>Body module — ticks ProcessRegeneration for all registered entities.</summary>
public sealed class BodyModule : IModule
{
    public string ModuleName => "Body";

    [Inject] private readonly IBodyService _bodyService = null!;

    public void Start()
    {
        Console.WriteLine("[BodyModule] Started");
    }

    public void Tick(int tickCount)
    {
        if (_bodyService is BodyService bs)
        {
            foreach (var entityId in bs.GetRegisteredEntityIds())
            {
                bs.ProcessRegeneration(entityId);
            }
        }
    }

    public void Dispose()
    {
        Console.WriteLine("[BodyModule] Disposed");
    }
}

public static class BodyModuleServices
{
    public static void Register(IContainerBuilder builder)
    {
        builder.Register<BodyConfig>(Lifetime.Singleton);
        builder.Register<BodyService>(Lifetime.Singleton);
        builder.Register<IBodyService, BodyService>(Lifetime.Singleton);
        builder.Register<BodyModule>(Lifetime.Singleton);
    }
}

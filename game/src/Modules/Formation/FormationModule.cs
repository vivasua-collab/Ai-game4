#nullable enable
using System;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Formation;

public sealed class FormationModule : IModule
{
    public string ModuleName => "Formation";

    [Inject] private readonly IFormationService _formationService = null!;

    public void Start()
    {
        Console.WriteLine("[FormationModule] Started");
    }

    public void Tick(int tickCount)
    {
        _formationService.ProcessDrain();
    }

    public void Dispose()
    {
        Console.WriteLine("[FormationModule] Disposed");
    }
}

public static class FormationModuleServices
{
    public static void Register(IContainerBuilder builder)
    {
        builder.Register<FormationConfig>(Lifetime.Singleton);
        builder.Register<FormationService>(Lifetime.Singleton);
        builder.Register<IFormationService, FormationService>(Lifetime.Singleton);
        builder.Register<FormationModule>(Lifetime.Singleton);
    }
}

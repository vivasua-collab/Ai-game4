#nullable enable
using System;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Charger;

public sealed class ChargerModule : IModule
{
    public string ModuleName => "Charger";

    [Inject] private readonly IChargerService _chargerService = null!;

    public void Start()
    {
        Console.WriteLine("[ChargerModule] Started");
    }

    public void Tick(int tickCount)
    {
        _chargerService.ProcessTick();
    }

    public void Dispose()
    {
        Console.WriteLine("[ChargerModule] Disposed");
    }
}

public static class ChargerModuleServices
{
    public static void Register(IContainerBuilder builder)
    {
        builder.Register<ChargerConfig>(Lifetime.Singleton);
        builder.Register<ChargerService>(Lifetime.Singleton);
        builder.Register<IChargerService, ChargerService>(Lifetime.Singleton);
        builder.Register<ChargerModule>(Lifetime.Singleton);
    }
}

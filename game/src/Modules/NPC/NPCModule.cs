#nullable enable
using System;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.NPC;

public sealed class NPCModule : IModule
{
    public string ModuleName => "NPC";

    [Inject] private readonly INPCService _npcService = null!;

    public void Start()
    {
        Console.WriteLine("[NPCModule] Started");
    }

    public void Tick(int tickCount)
    {
        _npcService.ProcessTick();
    }

    public void Dispose()
    {
        Console.WriteLine("[NPCModule] Disposed");
    }
}

public static class NPCModuleServices
{
    public static void Register(IContainerBuilder builder)
    {
        builder.Register<NPCConfig>(Lifetime.Singleton);
        builder.Register<NPCService>(Lifetime.Singleton);
        builder.Register<INPCService, NPCService>(Lifetime.Singleton);
        builder.Register<NPCModule>(Lifetime.Singleton);
    }
}

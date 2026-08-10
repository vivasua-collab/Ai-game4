#nullable enable
using System;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Buff;

/// <summary>Buff module — ticks TickBuffs for all entities each tick.</summary>
public sealed class BuffModule : IModule
{
    public string ModuleName => "Buff";

    [Inject] private readonly IBuffService _buffService = null!;

    public void Start()
    {
        Console.WriteLine("[BuffModule] Started");
    }

    public void Tick(int tickCount)
    {
        if (_buffService is BuffService bs)
        {
            bs.TickAllBuffs();
        }
    }

    public void Dispose()
    {
        Console.WriteLine("[BuffModule] Disposed");
    }
}

public static class BuffModuleServices
{
    public static void Register(IContainerBuilder builder)
    {
        builder.Register<BuffConfig>(Lifetime.Singleton);
        builder.Register<BuffService>(Lifetime.Singleton);
        builder.Register<IBuffService, BuffService>(Lifetime.Singleton);
        builder.Register<BuffModule>(Lifetime.Singleton);
    }
}

#nullable enable
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Qi;

/// <summary>
/// Qi module — ticks ProcessRegenBatch every N ticks
/// (default = GameConstants.QI_REGEN_BATCH_TICKS = 10).
/// </summary>
public sealed class QiModule : IModule
{
    public string ModuleName => "Qi";

    [Inject] private readonly IQiService _qiService = null!;

    public void Start()
    {
        Console.WriteLine("[QiModule] Started");
    }

    public void Tick(int tickCount)
    {
        if (tickCount % GameConstants.QI_REGEN_BATCH_TICKS != 0) return;
        _qiService.ProcessRegenBatch();
    }

    public void Dispose()
    {
        Console.WriteLine("[QiModule] Disposed");
    }
}

public static class QiModuleServices
{
    public static void Register(IContainerBuilder builder)
    {
        builder.Register<QiConfig>(Lifetime.Singleton);
        builder.Register<QiService>(Lifetime.Singleton);
        builder.Register<IQiService, QiService>(Lifetime.Singleton);
        builder.Register<QiModule>(Lifetime.Singleton);
    }
}

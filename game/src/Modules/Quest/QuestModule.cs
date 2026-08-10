#nullable enable
using System;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Quest;

/// <summary>
/// Quest module — per-tick progress check (stub). Real impl subscribes to
/// cross-module events (EntityDeath, ItemAdded, etc.) for objective updates.
/// </summary>
public sealed class QuestModule : IModule
{
    public string ModuleName => "Quest";

    [Inject] private readonly IQuestService _questService = null!;
    private QuestConfig _config = new();

    public void Start()
    {
        Console.WriteLine("[QuestModule] Started");
    }

    public void Tick(int tickCount)
    {
        if (tickCount % _config.ProgressCheckEveryTicks != 0) return;
        var active = _questService.GetActiveQuests();
        if (active.Count > 0 && tickCount % 60 == 0)
        {
            Console.WriteLine($"[QuestModule] tick {tickCount} — {active.Count} active quests");
        }
    }

    public void Dispose()
    {
        Console.WriteLine("[QuestModule] Disposed");
    }
}

public static class QuestModuleServices
{
    public static void Register(IContainerBuilder builder)
    {
        builder.Register<QuestConfig>(Lifetime.Singleton);
        builder.Register<QuestService>(Lifetime.Singleton);
        builder.Register<IQuestService, QuestService>(Lifetime.Singleton);
        builder.Register<QuestModule>(Lifetime.Singleton);
    }
}

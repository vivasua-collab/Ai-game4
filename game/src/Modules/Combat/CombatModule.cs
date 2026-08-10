#nullable enable
using System;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Combat;

/// <summary>
/// Combat module — ticks only when there's at least one active combat.
/// V1: just logs. Real impl applies queued damage via body/qi services.
/// </summary>
public sealed class CombatModule : IModule
{
    public string ModuleName => "Combat";

    [Inject] private readonly ICombatService _combatService = null!;

    public void Start()
    {
        Console.WriteLine("[CombatModule] Started");
    }

    public void Tick(int tickCount)
    {
        if (_combatService is CombatService cs)
        {
            var combatants = cs.GetActiveCombatantIds();
            if (combatants.Count == 0) return;
            if (tickCount % 30 == 0)
            {
                Console.WriteLine($"[CombatModule] tick {tickCount} — {combatants.Count} combatants in combat");
            }
        }
    }

    public void Dispose()
    {
        Console.WriteLine("[CombatModule] Disposed");
    }
}

public static class CombatModuleServices
{
    public static void Register(IContainerBuilder builder)
    {
        builder.Register<CombatConfig>(Lifetime.Singleton);
        builder.Register<CombatService>(Lifetime.Singleton);
        builder.Register<ICombatService, CombatService>(Lifetime.Singleton);
        builder.Register<CombatModule>(Lifetime.Singleton);
    }
}

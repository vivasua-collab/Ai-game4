#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CultivationGame.Entry.Phases;

/// <summary>
/// Phase 7 — charger (qi-stone slot) initialisation. Stub for v1.
/// </summary>
public sealed class ChargerInitPhase : AbstractSceneAssemblyPhase
{
    public override string PhaseName => "ChargerInit";
    public override int PhaseOrder => 7;

    public override Task ExecuteAsync(CancellationToken ct = default)
    {
        Console.WriteLine($"[Phase {PhaseOrder}] {PhaseName} complete (stub)");
        return Task.CompletedTask;
    }
}

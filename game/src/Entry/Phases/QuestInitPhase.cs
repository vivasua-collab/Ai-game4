#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CultivationGame.Entry.Phases;

/// <summary>
/// Phase 8 — quest system initialisation. Stub for v1.
/// </summary>
public sealed class QuestInitPhase : AbstractSceneAssemblyPhase
{
    public override string PhaseName => "QuestInit";
    public override int PhaseOrder => 8;

    public override Task ExecuteAsync(CancellationToken ct = default)
    {
        Console.WriteLine($"[Phase {PhaseOrder}] {PhaseName} complete (stub)");
        return Task.CompletedTask;
    }
}

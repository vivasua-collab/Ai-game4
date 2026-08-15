#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CultivationGame.Entry.Phases;

/// <summary>
/// Phase 6 — formation system initialisation. Stub for v1.
/// </summary>
public sealed class FormationInitPhase : AbstractSceneAssemblyPhase
{
    public override string PhaseName => "FormationInit";
    public override int PhaseOrder => 6;

    public override Task ExecuteAsync()
    {
        Console.WriteLine($"[Phase {PhaseOrder}] {PhaseName} complete (stub)");
        return Task.CompletedTask;
    }
}

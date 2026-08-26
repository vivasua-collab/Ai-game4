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
    // 2026-08-26 (аудит-1 A-1): 6 → 8 — уникальный порядок (был дубль с HumanNPCSpawn).
    public override int PhaseOrder => 8;

    public override Task ExecuteAsync()
    {
        Console.WriteLine($"[Phase {PhaseOrder}] {PhaseName} complete (stub)");
        return Task.CompletedTask;
    }
}

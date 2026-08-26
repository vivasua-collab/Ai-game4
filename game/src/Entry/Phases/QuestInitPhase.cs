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
    // 2026-08-26 (аудит-1 A-1): 8 → 10 — уникальные порядки после перенумерации.
    public override int PhaseOrder => 10;

    public override Task ExecuteAsync()
    {
        Console.WriteLine($"[Phase {PhaseOrder}] {PhaseName} complete (stub)");
        return Task.CompletedTask;
    }
}

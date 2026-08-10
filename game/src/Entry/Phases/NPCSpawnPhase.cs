#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CultivationGame.Entry.Phases;

/// <summary>
/// Phase 5 — NPC spawn. Stub for v1: the test polygon is intentionally
/// empty so the player can walk around without AI load.
/// </summary>
public sealed class NPCSpawnPhase : AbstractSceneAssemblyPhase
{
    public override string PhaseName => "NPCSpawn";
    public override int PhaseOrder => 5;

    public override Task ExecuteAsync(CancellationToken ct = default)
    {
        Console.WriteLine($"[Phase {PhaseOrder}] {PhaseName} — No NPCs in test polygon");
        return Task.CompletedTask;
    }
}

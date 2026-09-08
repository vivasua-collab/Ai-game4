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
    // 2026-09-08 (ревью-1 P2-1): 8 → 9 — сдвиг из-за уникализации AnimalSpawn(6).
    public override int PhaseOrder => 9;

    /// <summary>
    /// 2026-09-08 (ревью-1 P1-3): инициализация системы — wiring-фаза,
    /// выполняется и при загрузке сейва (SkipOnLoad=false).
    /// </summary>
    public override bool SkipOnLoad => false;

    public override Task ExecuteAsync()
    {
        Console.WriteLine($"[Phase {PhaseOrder}] {PhaseName} complete (stub)");
        return Task.CompletedTask;
    }
}

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
    // 2026-09-08 (ревью-1 P2-1): 10 → 11 — сдвиг из-за уникализации порядков.
    public override int PhaseOrder => 11;

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

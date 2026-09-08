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
    // 2026-08-26 (аудит-1 A-1): 7 → 9 — уникальный порядок (был дубль с GroupSpawn).
    // 2026-09-08 (ревью-1 P2-1): 9 → 10 — сдвиг из-за уникализации порядков.
    public override int PhaseOrder => 10;

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

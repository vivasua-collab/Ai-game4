#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Entry.Phases;

/// <summary>
/// Phase 1 — validates that all core service interfaces are wired in the
/// DI container by actively resolving them. Any failed resolution throws
/// and aborts scene assembly (caught by <see cref="SceneOrchestrator"/>).
/// </summary>
public sealed class CoreValidationPhase : AbstractSceneAssemblyPhase
{
    public override string PhaseName => "CoreValidation";
    public override int PhaseOrder => 1;

    public override Task ExecuteAsync()
    {
        // Actively resolve each core interface — if any is missing the
        // container will throw and the orchestrator will fail-fast.
        _ = _resolver.Resolve<ITimeService>();
        _ = _resolver.Resolve<IWorldService>();
        _ = _resolver.Resolve<IPlayerService>();
        _ = _resolver.Resolve<ITileService>();
        _ = _resolver.Resolve<ISaveService>();
        _ = _resolver.Resolve<IGameSession>();

        Console.WriteLine($"[Phase {PhaseOrder}] {PhaseName} complete — Core validated");
        return Task.CompletedTask;
    }
}

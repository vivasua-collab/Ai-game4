#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Entry.Phases;

/// <summary>
/// Phase 10 — finalisation. Publishes <see cref="SceneReadyEvent"/> and
/// logs completion. The <see cref="SceneOrchestrator"/> also publishes
/// <see cref="SceneReadyEvent"/> after this phase returns; both publishes
/// are intentional so late subscribers can react either way.
/// </summary>
public sealed class FinalizePhase : AbstractSceneAssemblyPhase
{
    public override string PhaseName => "Finalize";
    public override int PhaseOrder => 10;

    [Inject] private readonly IPublisher<SceneReadyEvent> _readyPub = null!;

    public override Task ExecuteAsync()
    {
        _readyPub.Publish(new SceneReadyEvent(1, 0, 0));
        Console.WriteLine($"[Phase {PhaseOrder}] {PhaseName} complete — Scene assembly complete");
        return Task.CompletedTask;
    }
}

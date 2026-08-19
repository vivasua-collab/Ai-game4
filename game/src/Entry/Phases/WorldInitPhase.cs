#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Entry.Phases;

/// <summary>
/// Phase 3 — activates the test-polygon location on the world service
/// and sets the time speed to Normal.
/// </summary>
public sealed class WorldInitPhase : AbstractSceneAssemblyPhase
{
    public override string PhaseName => "WorldInit";
    public override int PhaseOrder => 3;

    [Inject] private readonly IWorldService _world = null!;
    [Inject] private readonly ITimeService _time = null!;
    [Inject] private readonly IGameSession _session = null!;

    public override Task ExecuteAsync()
    {
        var locId = _session.Data?.WorldId ?? LocationCatalog.TestPolygon.Id;
        _world.SetActiveLocation(locId);
        _time.Speed = TimeSpeed.Normal;
        Console.WriteLine(
            $"[Phase {PhaseOrder}] {PhaseName} complete — location={locId}, speed=Normal");
        return Task.CompletedTask;
    }
}

#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Entry.Phases;

/// <summary>
/// Phase 4 — spawns the player avatar at the centre of the test polygon.
/// </summary>
public sealed class PlayerSpawnPhase : AbstractSceneAssemblyPhase
{
    public override string PhaseName => "PlayerSpawn";
    public override int PhaseOrder => 4;

    [Inject] private readonly IPlayerService _player = null!;
    [Inject] private readonly IGameSession _session = null!;

    public override Task ExecuteAsync()
    {
        // Resolve location from session data (fallback to TestPolygon).
        var locId = _session.Data?.WorldId ?? LocationCatalog.TestPolygon.Id;
        var loc = LocationCatalog.Find(locId) ?? LocationCatalog.TestPolygon;
        var center = new Position2D(loc.Width / 2, loc.Height / 2);
        _player.Spawn(center);
        Console.WriteLine($"[Phase {PhaseOrder}] {PhaseName} complete — player at {center} in {loc.Id}");
        return Task.CompletedTask;
    }
}

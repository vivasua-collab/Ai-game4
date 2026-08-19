#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Entry.Phases;

/// <summary>
/// Phase 2 — generates the tile map for the active location.
/// Default: test_polygon (50×50). Switch to large_world (500×500) via
/// <c>GameSession.NewGame(variant, "large_world")</c>.
/// </summary>
public sealed class TileMapGenPhase : AbstractSceneAssemblyPhase
{
    public override string PhaseName => "TileMapGen";
    public override int PhaseOrder => 2;

    [Inject] private readonly ITileService _tile = null!;
    [Inject] private readonly IGameSession _session = null!;

    public override Task ExecuteAsync()
    {
        // Resolve location from session data (fallback to TestPolygon).
        var locId = _session.Data?.WorldId ?? LocationCatalog.TestPolygon.Id;
        var loc = LocationCatalog.Find(locId) ?? LocationCatalog.TestPolygon;
        _tile.Generate(loc.Seed, loc.Width, loc.Height, loc.TerrainType);
        Console.WriteLine(
            $"[Phase {PhaseOrder}] {PhaseName} complete — {loc.Id}: {loc.Width}×{loc.Height} tiles, seed={loc.Seed}");
        return Task.CompletedTask;
    }
}

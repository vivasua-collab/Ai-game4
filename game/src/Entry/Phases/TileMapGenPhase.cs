#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Entry.Phases;

/// <summary>
/// Phase 2 — generates the test-polygon tile map (50×50 tiles, grass,
/// seed = 12345) via <see cref="ITileService"/>.
/// </summary>
public sealed class TileMapGenPhase : AbstractSceneAssemblyPhase
{
    public override string PhaseName => "TileMapGen";
    public override int PhaseOrder => 2;

    [Inject] private readonly ITileService _tile = null!;

    public override Task ExecuteAsync(CancellationToken ct = default)
    {
        var loc = LocationCatalog.TestPolygon;
        _tile.Generate(loc.Seed, loc.Width, loc.Height, loc.TerrainType);
        Console.WriteLine(
            $"[Phase {PhaseOrder}] {PhaseName} complete — {loc.Width}×{loc.Height} tiles, seed={loc.Seed}");
        return Task.CompletedTask;
    }
}

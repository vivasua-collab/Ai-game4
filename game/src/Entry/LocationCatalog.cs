#nullable enable
using System.Collections.Generic;
using CultivationGame.Core.Data;

namespace CultivationGame.Entry;

/// <summary>
/// Hardcoded catalogue of starting locations for v1.
/// Test polygon is the only fully playable scene; <c>WorldMap</c> is a
/// placeholder for the future open-world fast-travel view.
/// </summary>
public static class LocationCatalog
{
    /// <summary>
    /// Bounded test location: 50×50 tiles (= 100×100 m), flat grass,
    /// deterministic seed. Used by <c>TileMapGenPhase</c> and
    /// <c>WorldInitPhase</c>.
    /// </summary>
    public static readonly LocationData TestPolygon = new()
    {
        Id = "test_polygon",
        Name = "Тестовый полигон",
        Description = "Ограниченная локация для тестирования. 100×100 м, трава.",
        X = 0,
        Y = 0,
        Z = 0,
        DistanceFromCenter = 0,
        LocationType = LocationType.Farm,
        Width = 50,
        Height = 50,
        Seed = 12345,
        TerrainType = TerrainType.Grass,
        QiDensity = 100,
        QiFlowRate = 1,
    };

    /// <summary>
    /// World map placeholder. Not a tile location (Width/Height = 0).
    /// Reserved for the fast-travel / region view.
    /// </summary>
    public static readonly LocationData WorldMap = new()
    {
        Id = "world_map",
        Name = "Карта мира",
        Description = "Мир культивации. 200000×200000 км.",
        X = 0,
        Y = 0,
        Z = 0,
        DistanceFromCenter = 0,
        LocationType = LocationType.WildLands,
        Width = 0,
        Height = 0,
        Seed = 0,
        TerrainType = TerrainType.Grass,
        QiDensity = 0,
        QiFlowRate = 0,
    };

    /// <summary>All registered locations.</summary>
    public static IReadOnlyList<LocationData> GetAll() => new[] { TestPolygon, WorldMap };

    /// <summary>Look up a location by id. Returns null if not found.</summary>
    public static LocationData? Find(string id)
    {
        foreach (var loc in GetAll())
        {
            if (loc.Id == id) return loc;
        }
        return null;
    }
}

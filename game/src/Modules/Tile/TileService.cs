#nullable enable
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Tile;

/// <summary>
/// TileService — owns the GameTile[,] grid for the current location.
/// Implements <see cref="ITileService"/>. <see cref="Generate"/> is an
/// internal helper (not on the interface) used by TileModule / TileMapGenPhase.
/// </summary>
public sealed class TileService : ITileService
{
    private GameTile[,] _grid = new GameTile[0, 0];

    [Inject] private readonly IPublisher<TileChangedEvent> _tileChangedPub = null!;
    [Inject] private readonly IPublisher<ResourceHarvestedEvent> _harvestedPub = null!;
    [Inject] private readonly IPublisher<ResourceDepletedEvent> _depletedPub = null!;
    [Inject] private readonly IResourceService? _resourceService = null;

    /// <summary>Internal — grid width. Exposed on interface as MapWidth.</summary>
    public int MapWidth => _grid.GetLength(0);

    /// <summary>Internal — grid height. Exposed on interface as MapHeight.</summary>
    public int MapHeight => _grid.GetLength(1);

    // === ITileService ===

    public GameTile GetTile(int x, int y)
    {
        if (!IsInBounds(x, y))
            return GameTile.CreateTerrain(x, y, TerrainType.Void);
        return _grid[x, y];
    }

    public void SetTile(int x, int y, in GameTile data)
    {
        if (!IsInBounds(x, y)) return;
        var old = _grid[x, y];
        // GameTile is a struct — copy via assignment.
        var newTile = data;
        newTile.X = x;
        newTile.Y = y;
        _grid[x, y] = newTile;
        _tileChangedPub.Publish(new TileChangedEvent(x, y, in old, in newTile));
    }

    public bool TryHarvest(int x, int y, out HarvestResult result)
    {
        result = HarvestResult.Empty;
        if (!IsInBounds(x, y)) return false;
        var tile = _grid[x, y];
        if (!tile.IsHarvestable || tile.ResourceAmount <= 0f) return false;

        if (_resourceService != null)
        {
            result = _resourceService.Harvest(x, y, in tile);
        }
        else
        {
            // Fallback: simple 1-unit harvest if ResourceService is not wired.
            float remaining = tile.ResourceAmount - 1f;
            bool depleted = remaining <= 0f;
            result = new HarvestResult(tile.ResourceId, 1, remaining, depleted);
            var updated = tile;
            updated.ResourceAmount = remaining;
            if (depleted)
            {
                updated.IsHarvestable = false;
                updated.ResourceId = string.Empty;
            }
            _grid[x, y] = updated;
        }

        _harvestedPub.Publish(new ResourceHarvestedEvent(
            x, y, _grid[x, y].ResourceId, result.ItemId, result.Amount, result.ResourceRemaining));
        if (result.Depleted)
        {
            _depletedPub.Publish(new ResourceDepletedEvent(x, y, _grid[x, y].ResourceId));
        }
        return true;
    }

    public bool IsWalkable(int x, int y)
    {
        if (!IsInBounds(x, y)) return false;
        return _grid[x, y].IsWalkable;
    }

    // === Internal helpers (not on interface) ===

    /// <summary>Internal — bounds check.</summary>
    public bool IsInBounds(int x, int y) => x >= 0 && y >= 0 && x < MapWidth && y < MapHeight;

    /// <summary>
    /// Internal — generate a procedural grid using value noise (fBm).
    /// Inspired by Godot's FastNoiseLite + Whittaker biome mapping.
    ///
    /// Algorithm (per docs_v2/10_godot_reference/godot_procedural_index.md):
    ///   1. Sample elevation noise (fBm, 4 octaves, domain-warped)
    ///   2. Sample moisture noise (different seed, lower frequency)
    ///   3. Map (elevation, moisture) → terrain type via thresholds
    ///   4. Add sand beaches at water/land transition
    ///   5. Scatter resources based on terrain
    ///
    /// This replaces the previous blob+CA approach with cleaner noise-based generation.
    /// Reference: https://docs.godotengine.org/en/stable/classes/class_fastnoiselite.html
    /// </summary>
    public void Generate(int seed, int width, int height, TerrainType baseTerrain)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentException($"TileService.Generate: bad dims {width}x{height}");

        _grid = new GameTile[width, height];
        var rng = new SeededRandom(seed);

        // Noise generators — elevation (main terrain) + moisture (biome variation).
        // Config from docs: Simplex Smooth equivalent, fBm, frequency 0.015, 4 octaves.
        var elevationNoise = new ValueNoise(seed, octaves: 4, frequency: 0.025f);
        var moistureNoise = new ValueNoise(seed + 7777, octaves: 3, frequency: 0.015f);

        // Step 1+2+3: sample noise → terrain type per tile.
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Domain-warped elevation for organic coastlines.
                float elevation = elevationNoise.SampleWarped(x, y, warpStrength: 0.8f);
                float moisture = moistureNoise.Sample(x, y);

                var biome = MapToBiome(elevation);
                var surface = MapToSurface(elevation, moisture, baseTerrain);
                var tile = GameTile.CreateTerrain(x, y, surface);
                tile.Biome = biome;
                _grid[x, y] = tile;
            }
        }

        // Step 4: add sand beaches at water→land transitions.
        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                if (_grid[x, y].Terrain == TerrainType.Grass)
                {
                    // Check if any neighbor is water → convert to sand (beach).
                    bool hasWaterNeighbor = false;
                    for (int dx = -1; dx <= 1 && !hasWaterNeighbor; dx++)
                        for (int dy = -1; dy <= 1 && !hasWaterNeighbor; dy++)
                            if (_grid[x + dx, y + dy].Terrain == TerrainType.Water_Shallow ||
                                _grid[x + dx, y + dy].Terrain == TerrainType.Water_Deep)
                                hasWaterNeighbor = true;

                    if (hasWaterNeighbor && rng.Next(0, 100) < 70)
                        _grid[x, y] = GameTile.CreateTerrain(x, y, TerrainType.Sand);
                }
            }
        }

        // Step 5: scatter resources based on terrain.
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var terrain = _grid[x, y].Terrain;

                // Trees on grass (5% chance).
                if (terrain == TerrainType.Grass && rng.Next(0, 100) < 5)
                {
                    _grid[x, y] = GameTile.CreateWithObject(x, y, terrain,
                        ObjectType.Tree_Oak, resourceMax: 3f, resourceId: "wood", hp: 0f);
                }
                // Rocks on stone (8% chance).
                else if (terrain == TerrainType.Stone && rng.Next(0, 100) < 8)
                {
                    _grid[x, y] = GameTile.CreateWithObject(x, y, terrain,
                        ObjectType.Rock_Medium, resourceMax: 5f, resourceId: "stone", hp: 0f);
                }
                // Bushes on dirt (4% chance).
                else if (terrain == TerrainType.Dirt && rng.Next(0, 100) < 4)
                {
                    _grid[x, y] = GameTile.CreateWithObject(x, y, terrain,
                        ObjectType.Bush, resourceMax: 2f, resourceId: "berries", hp: 0f);
                }
            }
        }

        Console.WriteLine($"[TileService] Generated {width}x{height} grid, seed={seed}, baseTerrain={baseTerrain} (noise-based)");

        // Debug: print terrain distribution.
        var dist = new System.Collections.Generic.Dictionary<TerrainType, int>();
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                var t = _grid[x, y].Terrain;
                dist[t] = dist.TryGetValue(t, out var c) ? c + 1 : 1;
            }
        foreach (var kv in dist)
            Console.WriteLine($"  {kv.Key}: {kv.Value} tiles ({kv.Value * 100 / (width * height)}%)");
    }

    /// <summary>
    /// Map (elevation, moisture) → terrain type using threshold rules.
    /// Inspired by Whittaker biome diagram (simplified for our terrain types).
    ///
    /// Elevation ranges:
    ///   0.00-0.30 → Deep water (oceans/lakes)
    ///   0.30-0.40 → Shallow water (coasts)
    ///   0.40-0.50 → Sand (beaches) — will be refined by neighbor check
    ///   0.50-0.75 → Grass/Dirt (based on moisture)
    ///   0.75-0.90 → Stone (mountains)
    ///   0.90-1.00 → Snow (peaks)
    /// </summary>
    /// <summary>Stratum 0: biome from elevation only (color + Qi).</summary>
    private static BiomeType MapToBiome(float elevation)
    {
        if (elevation < 0.30f) return BiomeType.Ocean;
        if (elevation < 0.40f) return BiomeType.Sea;
        if (elevation < 0.45f) return BiomeType.Coast;
        if (elevation < 0.65f) return BiomeType.Grassland;
        if (elevation < 0.82f) return BiomeType.Highlands;
        if (elevation < 0.92f) return BiomeType.Mountains;
        return BiomeType.Peak;
    }

    /// <summary>Stratum 1: surface from elevation + moisture (moveCost, walkability).</summary>
    private static TerrainType MapToSurface(float elevation, float moisture, TerrainType baseTerrain)
    {
        if (elevation < 0.30f) return TerrainType.Water_Deep;
        if (elevation < 0.40f) return TerrainType.Water_Shallow;
        if (elevation < 0.45f) return TerrainType.Sand;
        if (elevation < 0.65f)
        {
            if (moisture < 0.35f) return TerrainType.Dirt;
            return TerrainType.Grass;
        }
        if (elevation < 0.82f) return TerrainType.Stone;
        if (elevation < 0.92f) return TerrainType.Snow;
        return TerrainType.Ice;
    }
}


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
    /// Internal — generate a procedural grid with blob-based terrain.
    /// Uses cellular automata to create natural-looking terrain patches
    /// (several adjacent tiles of the same type, then transition to another).
    ///
    /// Algorithm:
    ///   1. Seed random "blob centers" for each terrain type
    ///   2. Grow blobs outward (cellular automata — majority rule)
    ///   3. 3 iterations of smoothing for organic shapes
    ///   4. Base terrain fills remaining space
    /// </summary>
    public void Generate(int seed, int width, int height, TerrainType baseTerrain)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentException($"TileService.Generate: bad dims {width}x{height}");

        _grid = new GameTile[width, height];
        var rng = new SeededRandom(seed);

        // Step 1: start with all base terrain.
        var terrainMap = new TerrainType[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                terrainMap[x, y] = baseTerrain;

        // Step 2: seed blob centers.
        // Each terrain type gets several seed points scattered across the map.
        // Blob size is random (radius 3-8 tiles) → patches of varying size.
        var terrainSeeds = new (TerrainType type, int count, int minR, int maxR)[]
        {
            (TerrainType.Dirt,          4, 3, 6),   // 4 dirt patches, radius 3-6
            (TerrainType.Stone,         3, 2, 5),   // 3 stone patches, radius 2-5
            (TerrainType.Water_Shallow, 2, 3, 7),   // 2 shallow water patches
            (TerrainType.Water_Deep,    1, 2, 4),   // 1 deep water patch (inside shallow)
            (TerrainType.Sand,          3, 2, 4),   // 3 sand patches (beaches)
            (TerrainType.Road,          2, 4, 8),   // 2 road strips
        };

        foreach (var (type, count, minR, maxR) in terrainSeeds)
        {
            for (int i = 0; i < count; i++)
            {
                int cx = rng.Next(2, width - 2);
                int cy = rng.Next(2, height - 2);
                int radius = rng.Next(minR, maxR + 1);
                SeedBlob(terrainMap, cx, cy, radius, type, rng);
            }
        }

        // Step 3: cellular automata smoothing (3 iterations).
        // Each tile becomes the majority type among its 8 neighbors.
        // This creates organic, natural-looking terrain transitions.
        for (int iter = 0; iter < 3; iter++)
        {
            terrainMap = SmoothTerrain(terrainMap, width, height, baseTerrain);
        }

        // Step 4: deep water inside shallow water (lakes).
        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                if (terrainMap[x, y] == TerrainType.Water_Shallow)
                {
                    // Count shallow water neighbors — if surrounded, make it deep.
                    int shallowCount = 0;
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                            if (terrainMap[x + dx, y + dy] == TerrainType.Water_Shallow)
                                shallowCount++;
                    if (shallowCount >= 8 && rng.Next(0, 100) < 60)
                        terrainMap[x, y] = TerrainType.Water_Deep;
                }
            }
        }

        // Step 5: build GameTile grid + scatter resources.
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var terrain = terrainMap[x, y];
                _grid[x, y] = GameTile.CreateTerrain(x, y, terrain);

                // Scatter trees on grass (5% chance, clustered).
                if (terrain == TerrainType.Grass && rng.Next(0, 100) < 5)
                {
                    _grid[x, y] = GameTile.CreateWithObject(x, y, terrain,
                        ObjectType.Tree_Oak, resourceMax: 3f, resourceId: "wood", hp: 0f);
                }
                // Scatter rocks on stone (8% chance).
                else if (terrain == TerrainType.Stone && rng.Next(0, 100) < 8)
                {
                    _grid[x, y] = GameTile.CreateWithObject(x, y, terrain,
                        ObjectType.Rock_Medium, resourceMax: 5f, resourceId: "stone", hp: 0f);
                }
            }
        }
        Console.WriteLine($"[TileService] Generated {width}x{height} grid, seed={seed}, baseTerrain={baseTerrain} (blob-based)");

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
    /// Seed a blob of terrain at (cx, cy) with given radius.
    /// Uses a rough circle + noise for organic shape.
    /// </summary>
    private static void SeedBlob(TerrainType[,] map, int cx, int cy, int radius, TerrainType type, SeededRandom rng)
    {
        int w = map.GetLength(0);
        int h = map.GetLength(1);
        int r2 = radius * radius;

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int x = cx + dx;
                int y = cy + dy;
                if (x < 0 || x >= w || y < 0 || y >= h) continue;

                // Distance squared + noise for organic edge.
                int dist2 = dx * dx + dy * dy;
                int noise = rng.Next(-2, 3);
                if (dist2 + noise <= r2)
                {
                    map[x, y] = type;
                }
            }
        }
    }

    /// <summary>
    /// One pass of cellular automata smoothing.
    /// Each tile becomes the majority type among its 3×3 neighborhood.
    /// </summary>
    private static TerrainType[,] SmoothTerrain(TerrainType[,] input, int w, int h, TerrainType baseTerrain)
    {
        var output = new TerrainType[w, h];
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                // Count terrain types in 3×3 neighborhood.
                var counts = new System.Collections.Generic.Dictionary<TerrainType, int>();
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int nx = x + dx;
                        int ny = y + dy;
                        if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                        var t = input[nx, ny];
                        counts[t] = counts.TryGetValue(t, out var c) ? c + 1 : 1;
                    }
                }

                // Find majority (prefer non-base terrain to encourage patch growth).
                TerrainType majority = input[x, y];
                int maxCount = 0;
                foreach (var kv in counts)
                {
                    // Bias: non-base terrain needs only 4 neighbors, base needs 6.
                    int threshold = kv.Key == baseTerrain ? 6 : 4;
                    if (kv.Value >= threshold && kv.Value > maxCount)
                    {
                        maxCount = kv.Value;
                        majority = kv.Key;
                    }
                }
                output[x, y] = majority;
            }
        }
        return output;
    }
}

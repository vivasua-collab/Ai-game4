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
    /// Internal — generate a procedural grid. Called by TileModule.Start
    /// and TileMapGenPhase. Not on the ITileService interface.
    /// </summary>
    public void Generate(int seed, int width, int height, TerrainType baseTerrain)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentException($"TileService.Generate: bad dims {width}x{height}");

        _grid = new GameTile[width, height];
        var rng = new SeededRandom(seed);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var terrain = PickTerrain(rng, baseTerrain);
                _grid[x, y] = GameTile.CreateTerrain(x, y, terrain);
                // V1: scatter some bushes / trees for resource testing
                if (rng.Next(0, 100) < 5)
                {
                    _grid[x, y] = GameTile.CreateWithObject(x, y, terrain,
                        ObjectType.Tree_Oak, resourceMax: 3f, resourceId: "wood", hp: 0f);
                }
            }
        }
        Console.WriteLine($"[TileService] Generated {width}x{height} grid, seed={seed}, baseTerrain={baseTerrain}");
    }

    private static TerrainType PickTerrain(SeededRandom rng, TerrainType baseTerrain)
    {
        int r = rng.Next(0, 100);
        if (r < 60) return baseTerrain;
        if (r < 75) return TerrainType.Dirt;
        if (r < 85) return TerrainType.Stone;
        if (r < 92) return TerrainType.Grass;
        if (r < 97) return TerrainType.Water_Shallow;
        if (r < 99) return TerrainType.Road;
        return TerrainType.Water_Deep;
    }
}

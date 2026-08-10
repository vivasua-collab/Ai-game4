#nullable enable
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Tile;

/// <summary>
/// TileService — owns the TileData[,] grid for the current location.
/// Generate uses SeededRandom for deterministic procedural generation.
/// Event-driven (no tick).
/// </summary>
public sealed class TileService : ITileService
{
    private TileData[,] _grid = new TileData[0, 0];

    public event Action<int, int>? OnTileChanged;

    /// <summary>Internal — grid width. Not on interface.</summary>
    public int Width => _grid.GetLength(0);
    /// <summary>Internal — grid height. Not on interface.</summary>
    public int Height => _grid.GetLength(1);

    public void Generate(int seed, int width, int height, TerrainType baseTerrain)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentException($"TileService.Generate: bad dims {width}x{height}");

        _grid = new TileData[width, height];
        var rng = new SeededRandom(seed);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var t = new TileData
                {
                    X = x,
                    Y = y,
                    Z = 0,
                    Terrain = PickTerrain(rng, baseTerrain),
                    MoveCost = 1f,
                    HasImpassableObject = false,
                    BlocksVision = false,
                    BaseQiDensity = 100,
                    CurrentQiDensity = 100,
                    QiModifier = 0.5f + rng.NextFloat() * 1.5f,
                    BaseTemperature = 20f,
                    CurrentTemperature = 20f,
                    TempModifier = 1f,
                    HasWater = false,
                    WaterType = WaterType.None,
                    IsExplored = false,
                    IsVisible = false,
                };
                // Water tiles are impassable
                if (t.Terrain == TerrainType.Water || t.Terrain == TerrainType.DeepWater
                    || t.Terrain == TerrainType.Mountain || t.Terrain == TerrainType.Lava)
                {
                    t.HasImpassableObject = true;
                    t.MoveCost = 0f;
                }
                _grid[x, y] = t;
            }
        }
        Console.WriteLine($"[TileService] Generated {width}x{height} grid, seed={seed}, baseTerrain={baseTerrain}");
    }

    private static TerrainType PickTerrain(SeededRandom rng, TerrainType baseTerrain)
    {
        // V1 simple distribution around the base terrain
        int r = rng.Next(0, 100);
        if (r < 60) return baseTerrain;
        if (r < 75) return TerrainType.Dirt;
        if (r < 85) return TerrainType.Stone;
        if (r < 92) return TerrainType.Bush;
        if (r < 97) return TerrainType.ShallowWater;
        if (r < 99) return TerrainType.Mountain;
        return TerrainType.DeepWater;
    }

    public TileData GetTile(int x, int y)
    {
        if (!IsInBounds(x, y))
            return new TileData { X = x, Y = y, Terrain = TerrainType.Void, HasImpassableObject = true };
        return _grid[x, y];
    }

    public void SetTile(int x, int y, TileData tile)
    {
        if (!IsInBounds(x, y)) return;
        tile.X = x; tile.Y = y;
        _grid[x, y] = tile;
        OnTileChanged?.Invoke(x, y);
    }

    public bool IsWalkable(int x, int y)
    {
        if (!IsInBounds(x, y)) return false;
        var t = _grid[x, y];
        return !t.HasImpassableObject && t.MoveCost > 0f;
    }

    /// <summary>Internal — bounds check. Not on interface.</summary>
    public bool IsInBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
}

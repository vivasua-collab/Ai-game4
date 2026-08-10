#nullable enable
using System;
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Interfaces;

/// <summary>Tile map read/write + deterministic generation.</summary>
public interface ITileService
{
    TileData GetTile(int x, int y);
    void SetTile(int x, int y, TileData tile);
    bool IsWalkable(int x, int y);

    void Generate(int seed, int width, int height, TerrainType baseTerrain);

    event Action<int, int>? OnTileChanged;
}

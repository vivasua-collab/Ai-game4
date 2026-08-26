#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Tile;

public sealed class TileConfig
{
    public int DefaultWidth { get; set; } = 50;
    public int DefaultHeight { get; set; } = 50;
    public int DefaultSeed { get; set; } = 12345;
    public TerrainType DefaultTerrain { get; set; } = TerrainType.Grass;
}

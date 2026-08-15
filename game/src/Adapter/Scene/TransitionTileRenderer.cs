#nullable enable
using Godot;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Transition tile renderer — draws smooth transitions between terrain types
/// using pre-generated quarter-circle sprite overlays.
///
/// For each tile corner (NW/NE/SW/SE), if the diagonal neighbor has a different
/// terrain AND the two adjacent neighbors match current, draw a quarter-circle
/// overlay sprite of the neighbor's color.
///
/// Uses TransitionSpriteGenerator for cached sprites (no per-frame allocation).
/// </summary>
public partial class TransitionTileRenderer : Node2D
{
    private ITileService? _tileService;
    private int _tileSize;
    private int _cornersDrawn;

    public void Initialize(ITileService tileService, int tileSize)
    {
        _tileService = tileService;
        _tileSize = tileSize;
        ZIndex = (int)RenderLayer.Terrain + 1;
        GD.Print($"[TransitionTiles] Init: {tileService.MapWidth}×{tileService.MapHeight}, tileSize={tileSize}");
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_tileService == null) return;

        int width = _tileService.MapWidth;
        int height = _tileService.MapHeight;
        _cornersDrawn = 0;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var curTerrain = _tileService.GetTile(x, y).Terrain;

                if (DrawCorner(x, y, x - 1, y - 1, curTerrain, TransitionSpriteGenerator.CornerDir.NW)) _cornersDrawn++;
                if (DrawCorner(x, y, x + 1, y - 1, curTerrain, TransitionSpriteGenerator.CornerDir.NE)) _cornersDrawn++;
                if (DrawCorner(x, y, x - 1, y + 1, curTerrain, TransitionSpriteGenerator.CornerDir.SW)) _cornersDrawn++;
                if (DrawCorner(x, y, x + 1, y + 1, curTerrain, TransitionSpriteGenerator.CornerDir.SE)) _cornersDrawn++;
            }
        }

        GD.Print($"[TransitionTiles] Drew {_cornersDrawn} corner overlays");
    }

    private bool DrawCorner(int curX, int curY, int diagX, int diagY,
        TerrainType curTerrain, TransitionSpriteGenerator.CornerDir corner)
    {
        if (_tileService == null) return false;
        if (diagX < 0 || diagX >= _tileService.MapWidth ||
            diagY < 0 || diagY >= _tileService.MapHeight) return false;

        var diagTerrain = _tileService.GetTile(diagX, diagY).Terrain;
        if (diagTerrain == curTerrain) return false;

        // Only draw if both adjacent neighbors match current (inside corner).
        bool shouldDraw = corner switch
        {
            TransitionSpriteGenerator.CornerDir.NW => IsSameOrOOB(curX, curY - 1, curTerrain) && IsSameOrOOB(curX - 1, curY, curTerrain),
            TransitionSpriteGenerator.CornerDir.NE => IsSameOrOOB(curX, curY - 1, curTerrain) && IsSameOrOOB(curX + 1, curY, curTerrain),
            TransitionSpriteGenerator.CornerDir.SW => IsSameOrOOB(curX, curY + 1, curTerrain) && IsSameOrOOB(curX - 1, curY, curTerrain),
            TransitionSpriteGenerator.CornerDir.SE => IsSameOrOOB(curX, curY + 1, curTerrain) && IsSameOrOOB(curX + 1, curY, curTerrain),
            _ => false,
        };
        if (!shouldDraw) return false;

        // Draw sprite overlay.
        var tex = TransitionSpriteGenerator.GetSprite(diagTerrain, corner);
        float px = curX * _tileSize;
        float py = curY * _tileSize;
        DrawTexture(tex, new Vector2(px, py));
        return true;
    }

    private bool IsSameOrOOB(int x, int y, TerrainType terrain)
    {
        if (_tileService == null) return true;
        if (x < 0 || x >= _tileService.MapWidth || y < 0 || y >= _tileService.MapHeight) return true;
        return _tileService.GetTile(x, y).Terrain == terrain;
    }
}

/// <summary>Terrain color palette — shared between SceneBuilder and TransitionTileRenderer.</summary>
public static class TerrainColors
{
    public static Color Get(TerrainType terrain) => terrain switch
    {
        TerrainType.Grass => new Color(0.28f, 0.48f, 0.22f),
        TerrainType.Dirt  => new Color(0.45f, 0.35f, 0.20f),
        TerrainType.Stone => new Color(0.50f, 0.50f, 0.50f),
        TerrainType.Water_Shallow => new Color(0.30f, 0.45f, 0.65f),
        TerrainType.Water_Deep => new Color(0.10f, 0.20f, 0.50f),
        TerrainType.Sand  => new Color(0.85f, 0.80f, 0.55f),
        TerrainType.Snow  => new Color(0.92f, 0.95f, 0.98f),
        TerrainType.Ice   => new Color(0.70f, 0.85f, 0.95f),
        TerrainType.Lava  => new Color(0.85f, 0.25f, 0.10f),
        TerrainType.Void  => new Color(0.05f, 0.02f, 0.10f),
        TerrainType.Road  => new Color(0.55f, 0.45f, 0.30f),
        _                 => new Color(0.28f, 0.48f, 0.22f),
    };
}

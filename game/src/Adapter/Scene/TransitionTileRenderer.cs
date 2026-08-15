#nullable enable
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Transition tile renderer — draws smooth transitions between terrain types.
///
/// Uses _draw() on a Node2D (not TileMapLayer — TileMapLayer is for TileSet cells,
/// not arbitrary Polygon2D children). This is the Godot 4.7 canonical way to do
/// custom 2D drawing per docs_v2/10_godot_reference/godot_2d_terrain_index.md.
///
/// Algorithm (autotiling via neighbor analysis):
///   For each tile, check 8 neighbors.
///   For each corner (NE, NW, SE, SW), if the diagonal neighbor is a different terrain,
///   AND the two adjacent (orthogonal) neighbors are the same as current:
///     → draw a quarter-circle overlay of the neighbor's color.
///   This creates rounded "inside" corners (RPG Maker autotile style).
/// </summary>
public partial class TransitionTileRenderer : Node2D
{
    private ITileService? _tileService;
    private int _tileSize;

    /// <summary>
    /// Initialize the transition renderer.
    /// Call after TileService.Generate has populated the grid.
    /// </summary>
    public void Initialize(ITileService tileService, int tileSize)
    {
        _tileService = tileService;
        _tileSize = tileSize;
        ZIndex = (int)RenderLayer.Terrain + 1;  // above base terrain, below objects
        QueueRedraw();  // trigger _Draw() call
    }

    public override void _Draw()
    {
        if (_tileService == null) return;

        int width = _tileService.MapWidth;
        int height = _tileService.MapHeight;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var tile = _tileService.GetTile(x, y);
                var curTerrain = tile.Terrain;

                // Check each corner for terrain transition.
                DrawCornerIfDifferent(x, y, x - 1, y - 1, curTerrain, CornerPos.NW);
                DrawCornerIfDifferent(x, y, x + 1, y - 1, curTerrain, CornerPos.NE);
                DrawCornerIfDifferent(x, y, x - 1, y + 1, curTerrain, CornerPos.SW);
                DrawCornerIfDifferent(x, y, x + 1, y + 1, curTerrain, CornerPos.SE);
            }
        }
    }

    private enum CornerPos { NW, NE, SW, SE }

    /// <summary>
    /// Draw a quarter-circle overlay at the corner if the diagonal neighbor
    /// has a different terrain AND adjacent neighbors match current.
    /// </summary>
    private void DrawCornerIfDifferent(int curX, int curY, int diagX, int diagY,
        TerrainType curTerrain, CornerPos corner)
    {
        if (_tileService == null) return;

        // Check diagonal neighbor bounds.
        if (diagX < 0 || diagX >= _tileService.MapWidth ||
            diagY < 0 || diagY >= _tileService.MapHeight) return;

        var diagTerrain = _tileService.GetTile(diagX, diagY).Terrain;
        if (diagTerrain == curTerrain) return;

        // Check the two adjacent (orthogonal) neighbors — both must match current.
        bool shouldDraw = corner switch
        {
            CornerPos.NW => IsSameOrOOB(curX, curY - 1, curTerrain) && IsSameOrOOB(curX - 1, curY, curTerrain),
            CornerPos.NE => IsSameOrOOB(curX, curY - 1, curTerrain) && IsSameOrOOB(curX + 1, curY, curTerrain),
            CornerPos.SW => IsSameOrOOB(curX, curY + 1, curTerrain) && IsSameOrOOB(curX - 1, curY, curTerrain),
            CornerPos.SE => IsSameOrOOB(curX, curY + 1, curTerrain) && IsSameOrOOB(curX + 1, curY, curTerrain),
            _ => false,
        };

        if (!shouldDraw) return;

        // Draw quarter-circle with diagonal neighbor's color.
        var overlayColor = TerrainColors.Get(diagTerrain);
        float cx = curX * _tileSize;
        float cy = curY * _tileSize;

        // Quarter circle center is at the corner of the tile.
        var center = corner switch
        {
            CornerPos.NW => new Vector2(cx, cy),
            CornerPos.NE => new Vector2(cx + _tileSize, cy),
            CornerPos.SW => new Vector2(cx, cy + _tileSize),
            CornerPos.SE => new Vector2(cx + _tileSize, cy + _tileSize),
            _ => Vector2.Zero,
        };

        // Draw filled quarter-circle using DrawColoredPolygon.
        float radius = _tileSize * 0.5f;
        var points = CreateQuarterCirclePoints(center, radius, corner);
        DrawColoredPolygon(points, overlayColor);
    }

    /// <summary>
    /// Check if tile at (x, y) has given terrain, or is out of bounds (OOB = same).
    /// </summary>
    private bool IsSameOrOOB(int x, int y, TerrainType terrain)
    {
        if (_tileService == null) return true;
        if (x < 0 || x >= _tileService.MapWidth || y < 0 || y >= _tileService.MapHeight) return true;
        return _tileService.GetTile(x, y).Terrain == terrain;
    }

    /// <summary>
    /// Create quarter-circle polygon points for a corner.
    /// </summary>
    private static Vector2[] CreateQuarterCirclePoints(Vector2 center, float radius, CornerPos corner)
    {
        var points = new List<Vector2> { center };
        int segments = 12;

        float startAngle, endAngle;
        switch (corner)
        {
            case CornerPos.NW:  // top-left: from left (PI) to up (3PI/2)
                startAngle = Mathf.Pi;
                endAngle = 3f * Mathf.Pi / 2f;
                break;
            case CornerPos.NE:  // top-right: from up (3PI/2) to right (2PI)
                startAngle = 3f * Mathf.Pi / 2f;
                endAngle = 2f * Mathf.Pi;
                break;
            case CornerPos.SW:  // bottom-left: from down (PI/2) to left (PI)
                startAngle = Mathf.Pi / 2f;
                endAngle = Mathf.Pi;
                break;
            case CornerPos.SE:  // bottom-right: from right (0) to down (PI/2)
                startAngle = 0f;
                endAngle = Mathf.Pi / 2f;
                break;
            default:
                startAngle = 0f;
                endAngle = Mathf.Pi / 2f;
                break;
        }

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float angle = Mathf.Lerp(startAngle, endAngle, t);
            points.Add(new Vector2(
                center.X + Mathf.Cos(angle) * radius,
                center.Y + Mathf.Sin(angle) * radius
            ));
        }

        return points.ToArray();
    }
}

/// <summary>
/// Terrain color palette — shared between SceneBuilder and TransitionTileRenderer.
/// </summary>
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

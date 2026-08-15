#nullable enable
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Transition tile renderer — draws smooth transitions between terrain types.
///
/// Instead of flat colored squares, this system draws:
///   1. Base tile color (full square)
///   2. Quarter-circle "corner" overlays where a different terrain meets this one
///
/// This creates the visual effect of rounded terrain transitions,
/// similar to RPG Maker's autotiles or Godot's TileSet Terrains.
///
/// Algorithm:
///   For each tile, check 8 neighbors.
///   For each corner (NE, NW, SE, SW), if the diagonal neighbor is a different terrain,
///   draw a quarter-circle overlay of the neighbor's color.
///
/// This is a pure rendering technique — no game logic, no data changes.
/// </summary>
public partial class TransitionTileRenderer : Node
{
    private TileMapLayer _overlayLayer = null!;
    private ITileService? _tileService;
    private int _tileSize;

    /// <summary>
    /// Initialize the transition renderer.
    /// Call after TileService.Generate has populated the grid.
    /// </summary>
    public void Initialize(ITileService tileService, int tileSize, Node2D worldRoot)
    {
        _tileService = tileService;
        _tileSize = tileSize;

        _overlayLayer = new TileMapLayer
        {
            Name = "TransitionOverlay",
            ZIndex = (int)RenderLayer.Terrain + 1,  // above base terrain, below objects
        };
        worldRoot.AddChild(_overlayLayer);

        RenderTransitions();
    }

    /// <summary>
    /// Render transition overlays for all tiles.
    /// Uses Polygon2D quarter-circles at corners where terrain changes.
    /// </summary>
    private void RenderTransitions()
    {
        if (_tileService == null) return;

        int width = _tileService.MapWidth;
        int height = _tileService.MapHeight;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var tile = _tileService.GetTile(x, y);
                var baseColor = TerrainColors.Get(tile.Terrain);

                // Check each corner for terrain transition.
                // Corner positions (in tile-local coords):
                //   NW(0,0)  NE(tile,0)
                //   SW(0,tile) SE(tile,tile)
                DrawCornerIfDifferent(x, y, x - 1, y - 1, x, y, baseColor, CornerPos.NW);
                DrawCornerIfDifferent(x, y, x,     y - 1, x, y, baseColor, CornerPos.NE, checkX: x + 1, checkY: y - 1);
                DrawCornerIfDifferent(x, y, x - 1, y,     x, y, baseColor, CornerPos.SW, checkX: x - 1, checkY: y + 1);
                DrawCornerIfDifferent(x, y, x,     y,     x, y, baseColor, CornerPos.SE, checkX: x + 1, checkY: y + 1);
            }
        }
    }

    private enum CornerPos { NW, NE, SW, SE }

    /// <summary>
    /// Draw a quarter-circle overlay at the corner if the diagonal neighbor
    /// has a different terrain than the current tile.
    /// </summary>
    private void DrawCornerIfDifferent(int curX, int curY, int diagX, int diagY,
        int tileX, int tileY, Color baseColor, CornerPos corner,
        int? checkX = null, int checkY = 0)
    {
        if (_tileService == null) return;

        // Check the diagonal neighbor.
        if (diagX < 0 || diagX >= _tileService.MapWidth ||
            diagY < 0 || diagY >= _tileService.MapHeight) return;

        var diagTile = _tileService.GetTile(diagX, diagY);
        if (diagTile.Terrain == _tileService.GetTile(curX, curY).Terrain) return;

        // Only draw if the two adjacent (non-diagonal) neighbors are the same as current.
        // This prevents drawing corners on edges (only draw on "inside" corners).
        // For NW corner: check N and W neighbors.
        var curTerrain = _tileService.GetTile(curX, curY).Terrain;
        bool shouldDraw = corner switch
        {
            CornerPos.NW => IsSameOrOOB(curX, curY - 1, curTerrain) && IsSameOrOOB(curX - 1, curY, curTerrain),
            CornerPos.NE => IsSameOrOOB(curX, curY - 1, curTerrain) && IsSameOrOOB(curX + 1, curY, curTerrain),
            CornerPos.SW => IsSameOrOOB(curX, curY + 1, curTerrain) && IsSameOrOOB(curX - 1, curY, curTerrain),
            CornerPos.SE => IsSameOrOOB(curX, curY + 1, curTerrain) && IsSameOrOOB(curX + 1, curY, curTerrain),
            _ => false,
        };

        if (!shouldDraw) return;

        // Draw quarter-circle overlay with the diagonal neighbor's color.
        var overlayColor = TerrainColors.Get(diagTile.Terrain);
        float cx = tileX * _tileSize;
        float cy = tileY * _tileSize;

        // Quarter circle center is at the corner of the tile.
        var center = corner switch
        {
            CornerPos.NW => new Vector2(cx, cy),
            CornerPos.NE => new Vector2(cx + _tileSize, cy),
            CornerPos.SW => new Vector2(cx, cy + _tileSize),
            CornerPos.SE => new Vector2(cx + _tileSize, cy + _tileSize),
            _ => Vector2.Zero,
        };

        // Draw a small filled circle (overlay) at the corner.
        var circle = new Polygon2D
        {
            Name = $"Trans_{tileX}_{tileY}_{corner}",
            Polygon = CreateQuarterCircle(center, _tileSize * 0.5f, corner),
            Color = overlayColor,
            ZIndex = (int)RenderLayer.Terrain + 1,
        };
        _overlayLayer.AddChild(circle);
    }

    /// <summary>
    /// Check if the tile at (x, y) has the given terrain, or is out of bounds.
    /// Out-of-bounds counts as "same" (no transition drawn at map edges).
    /// </summary>
    private bool IsSameOrOOB(int x, int y, TerrainType terrain)
    {
        if (_tileService == null) return true;
        if (x < 0 || x >= _tileService.MapWidth || y < 0 || y >= _tileService.MapHeight) return true;
        return _tileService.GetTile(x, y).Terrain == terrain;
    }

    /// <summary>
    /// Create a quarter-circle polygon for a corner overlay.
    /// The quarter circle spans 90° from the corner point.
    /// </summary>
    private static Vector2[] CreateQuarterCircle(Vector2 center, float radius, CornerPos corner)
    {
        var points = new List<Vector2> { center };
        int segments = 8;

        // Determine start and end angles based on corner.
        // Angles in radians: 0 = right, PI/2 = down (Godot Y-down).
        float startAngle, endAngle;
        switch (corner)
        {
            case CornerPos.NW:  // top-left: quarter from left (PI) to up (3PI/2 or -PI/2)
                startAngle = Mathf.Pi;        // left
                endAngle = 3f * Mathf.Pi / 2f; // up (in Godot, up = -Y = 270°)
                break;
            case CornerPos.NE:  // top-right: quarter from up to right
                startAngle = 3f * Mathf.Pi / 2f;  // up
                endAngle = 2f * Mathf.Pi;          // right
                break;
            case CornerPos.SW:  // bottom-left: quarter from down to left
                startAngle = Mathf.Pi / 2f;  // down
                endAngle = Mathf.Pi;          // left
                break;
            case CornerPos.SE:  // bottom-right: quarter from right to down
                startAngle = 0f;              // right
                endAngle = Mathf.Pi / 2f;     // down
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

#nullable enable
using Godot;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Renders transition sprites on stratum 1 (surface).
///
/// For each tile, checks 8 neighbors. If a neighbor has a different biome
/// AND higher priority, draws a transition sprite on this tile.
///
/// 8 directions: N, S, E, W (straight) + NW, NE, SW, SE (diagonal).
/// Priority: sprite drawn on LOWER biome's tile (higher biome bleeds in).
/// </summary>
public partial class SurfaceTransitionRenderer : Node2D
{
    private ITileService? _tileService;
    private int _tileSize;

    public void Initialize(ITileService tileService, int tileSize)
    {
        _tileService = tileService;
        _tileSize = tileSize;
        ZIndex = (int)RenderLayer.Terrain + 1;  // above stratum 0, below objects
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_tileService == null) return;

        int w = _tileService.MapWidth;
        int h = _tileService.MapHeight;
        int drawn = 0;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                var curBiome = _tileService.GetTile(x, y).Biome;
                int curPriority = TransitionSpriteGenerator.GetBiomePriority(curBiome);

                // Check 8 neighbors for higher-priority biomes.
                // Straight directions (N, S, E, W).
                if (DrawTransition(x, y, x, y - 1, curBiome, curPriority, TransitionSpriteGenerator.Direction.N, w, h)) drawn++;
                if (DrawTransition(x, y, x, y + 1, curBiome, curPriority, TransitionSpriteGenerator.Direction.S, w, h)) drawn++;
                if (DrawTransition(x, y, x + 1, y, curBiome, curPriority, TransitionSpriteGenerator.Direction.E, w, h)) drawn++;
                if (DrawTransition(x, y, x - 1, y, curBiome, curPriority, TransitionSpriteGenerator.Direction.W, w, h)) drawn++;

                // Diagonal directions (NW, NE, SW, SE).
                // Only draw diagonal if both adjacent straight neighbors are same biome.
                if (ShouldDrawDiagonal(x, y, x - 1, y - 1, x, y - 1, x - 1, y, curBiome, curPriority, w, h))
                    if (DrawTransition(x, y, x - 1, y - 1, curBiome, curPriority, TransitionSpriteGenerator.Direction.NW, w, h)) drawn++;
                if (ShouldDrawDiagonal(x, y, x + 1, y - 1, x, y - 1, x + 1, y, curBiome, curPriority, w, h))
                    if (DrawTransition(x, y, x + 1, y - 1, curBiome, curPriority, TransitionSpriteGenerator.Direction.NE, w, h)) drawn++;
                if (ShouldDrawDiagonal(x, y, x - 1, y + 1, x, y + 1, x - 1, y, curBiome, curPriority, w, h))
                    if (DrawTransition(x, y, x - 1, y + 1, curBiome, curPriority, TransitionSpriteGenerator.Direction.SW, w, h)) drawn++;
                if (ShouldDrawDiagonal(x, y, x + 1, y + 1, x, y + 1, x + 1, y, curBiome, curPriority, w, h))
                    if (DrawTransition(x, y, x + 1, y + 1, curBiome, curPriority, TransitionSpriteGenerator.Direction.SE, w, h)) drawn++;
            }
        }

        GD.Print($"[SurfaceTransitions] Drew {drawn} transition sprites");
    }

    /// <summary>
    /// Draw a transition sprite if neighbor has higher priority biome.
    /// Returns true if drawn.
    /// </summary>
    private bool DrawTransition(int curX, int curY, int nbX, int nbY,
        BiomeType curBiome, int curPriority,
        TransitionSpriteGenerator.Direction dir, int w, int h)
    {
        if (_tileService == null) return false;
        if (nbX < 0 || nbX >= w || nbY < 0 || nbY >= h) return false;

        var nbBiome = _tileService.GetTile(nbX, nbY).Biome;
        if (nbBiome == curBiome) return false;

        int nbPriority = TransitionSpriteGenerator.GetBiomePriority(nbBiome);
        // Only draw if neighbor has HIGHER priority (its sprite bleeds into our tile).
        if (nbPriority <= curPriority) return false;

        // Draw the transition sprite.
        var tex = TransitionSpriteGenerator.GetSprite(nbBiome, dir);
        float px = curX * _tileSize;
        float py = curY * _tileSize;
        DrawTexture(tex, new Vector2(px, py));
        return true;
    }

    /// <summary>
    /// Check if a diagonal transition should be drawn.
    /// Only when: diagonal neighbor has higher priority AND
    /// both adjacent straight neighbors have same biome as current.
    /// </summary>
    private bool ShouldDrawDiagonal(int curX, int curY,
        int diagX, int diagY, int adj1X, int adj1Y, int adj2X, int adj2Y,
        BiomeType curBiome, int curPriority, int w, int h)
    {
        if (_tileService == null) return false;
        if (diagX < 0 || diagX >= w || diagY < 0 || diagY >= h) return false;
        if (adj1X < 0 || adj1X >= w || adj1Y < 0 || adj1Y >= h) return false;
        if (adj2X < 0 || adj2X >= w || adj2Y < 0 || adj2Y >= h) return false;

        var diagBiome = _tileService.GetTile(diagX, diagY).Biome;
        if (diagBiome == curBiome) return false;

        int diagPriority = TransitionSpriteGenerator.GetBiomePriority(diagBiome);
        if (diagPriority <= curPriority) return false;

        // Both adjacent neighbors must be same biome as current.
        var adj1 = _tileService.GetTile(adj1X, adj1Y).Biome;
        var adj2 = _tileService.GetTile(adj2X, adj2Y).Biome;
        return adj1 == curBiome && adj2 == curBiome;
    }
}

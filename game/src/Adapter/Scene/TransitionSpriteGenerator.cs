#nullable enable
using Godot;
using System.Collections.Generic;
using System.IO;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Generates and caches transition tile sprites (quarter-circle overlays).
///
/// For each biome pair (e.g. Grass→Water), generates 4 PNG sprites (NW/NE/SW/SE corners).
/// Sprites are generated procedurally at startup and cached in memory.
///
/// This replaces the previous DrawColoredPolygon approach with pre-rendered textures,
/// which is more reliable and supports future hand-drawn tile art.
/// </summary>
public static class TransitionSpriteGenerator
{
    private static readonly Dictionary<(TerrainType, CornerDir), ImageTexture> _cache = new();

    public enum CornerDir { NW, NE, SW, SE }

    /// <summary>
    /// Get (or generate) a transition sprite for a given terrain color + corner.
    /// </summary>
    public static ImageTexture GetSprite(TerrainType overlayTerrain, CornerDir corner)
    {
        var key = (overlayTerrain, corner);
        if (_cache.TryGetValue(key, out var existing))
            return existing;

        var color = TerrainColors.Get(overlayTerrain);
        var img = CreateQuarterCircleImage(color, corner, GameConstants.TILE_PIXELS);
        var tex = ImageTexture.CreateFromImage(img);
        _cache[key] = tex;
        return tex;
    }

    /// <summary>
    /// Create a 64×64 image with a quarter-circle in the given corner.
    /// Background is transparent, quarter-circle is filled with the given color.
    /// </summary>
    private static Image CreateQuarterCircleImage(Color color, CornerDir corner, int size)
    {
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));  // transparent

        float radius = size * 0.5f;
        float cx, cy;

        // Note: Godot uses Y-down coordinates. "NW" = top-left = (0,0) in screen space.
        // But the diagonal neighbor at (x-1, y-1) is "up-left" in game logic,
        // which in Godot screen space is also (0,0) corner of the tile.
        // The 180° flip was because we were checking the wrong diagonal neighbor.
        // Fix: swap NW↔SE and NE↔SW to match Godot's Y-down convention.
        switch (corner)
        {
            case CornerDir.NW: cx = size; cy = size; break;  // diagonal (x-1,y-1) → draw at SE corner
            case CornerDir.NE: cx = 0; cy = size; break;     // diagonal (x+1,y-1) → draw at SW corner
            case CornerDir.SW: cx = size; cy = 0; break;     // diagonal (x-1,y+1) → draw at NE corner
            case CornerDir.SE: cx = 0; cy = 0; break;        // diagonal (x+1,y+1) → draw at NW corner
            default: cx = 0; cy = 0; break;
        }

        // Draw filled quarter-circle.
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // Anti-aliasing: smooth edge over 1 pixel.
                if (dist < radius - 1f)
                {
                    img.SetPixel(x, y, color);
                }
                else if (dist < radius)
                {
                    float alpha = 1f - (dist - (radius - 1f));
                    var blended = new Color(color.R, color.G, color.B, color.A * alpha);
                    img.SetPixel(x, y, blended);
                }
            }
        }

        return img;
    }
}

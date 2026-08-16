#nullable enable
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.Data;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Generates and caches transition tile sprites for stratum 1 (surface).
///
/// 8 directions per biome pair:
///   N, S, E, W — straight edges (half-tile overlay)
///   NW, NE, SW, SE — diagonal corners (quarter-circle overlay)
///
/// Priority: sprite is drawn on the LOWER biome's tile.
/// Hierarchy: Peak > Mountains > Highlands > Forest > Grassland > Steppe > Coast > Sea > Ocean
///
/// Total: 10 pairs × 8 directions = 80 sprites (cached in memory).
/// </summary>
public static class TransitionSpriteGenerator
{
    public enum Direction { N, S, E, W, NW, NE, SW, SE }

    private static readonly Dictionary<(BiomeType, Direction), ImageTexture> _cache = new();

    /// <summary>
    /// Biome priority — higher = drawn on top, lower = gets the transition sprite.
    /// </summary>
    public static int GetBiomePriority(BiomeType biome) => biome switch
    {
        BiomeType.Ocean      => 0,
        BiomeType.Sea        => 1,
        BiomeType.Coast      => 2,
        BiomeType.Steppe     => 3,
        BiomeType.Grassland  => 4,
        BiomeType.Forest     => 5,
        BiomeType.Highlands  => 6,
        BiomeType.Mountains  => 7,
        BiomeType.Peak       => 8,
        _                    => 0,
    };

    /// <summary>
    /// Get (or generate) a transition sprite for overlay biome + direction.
    /// The sprite represents the overlay biome bleeding into the base tile.
    /// </summary>
    public static ImageTexture GetSprite(BiomeType overlayBiome, Direction dir)
    {
        var key = (overlayBiome, dir);
        if (_cache.TryGetValue(key, out var existing))
            return existing;

        var color = BiomePalette.Get(overlayBiome);
        var img = CreateTransitionImage(color, dir, GameConstants.TILE_PIXELS);
        var tex = ImageTexture.CreateFromImage(img);
        _cache[key] = tex;
        return tex;
    }

    /// <summary>
    /// Create a transition sprite image.
    /// Straight directions (N/S/E/W): half-tile filled.
    /// Diagonal directions (NW/NE/SW/SE): quarter-circle filled.
    /// </summary>
    private static Image CreateTransitionImage(Color color, Direction dir, int size)
    {
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));  // transparent

        switch (dir)
        {
            case Direction.N:
                // Overlay on top half (neighbor is above).
                FillHalf(img, color, size, topHalf: true);
                break;
            case Direction.S:
                FillHalf(img, color, size, topHalf: false);
                break;
            case Direction.E:
                FillHalfVertical(img, color, size, rightHalf: true);
                break;
            case Direction.W:
                FillHalfVertical(img, color, size, rightHalf: false);
                break;
            case Direction.NW:
                FillQuarterCircle(img, color, size, cx: 0, cy: 0);
                break;
            case Direction.NE:
                FillQuarterCircle(img, color, size, cx: size, cy: 0);
                break;
            case Direction.SW:
                FillQuarterCircle(img, color, size, cx: 0, cy: size);
                break;
            case Direction.SE:
                FillQuarterCircle(img, color, size, cx: size, cy: size);
                break;
        }

        return img;
    }

    /// <summary>Fill top or bottom half of the image.</summary>
    private static void FillHalf(Image img, Color color, int size, bool topHalf)
    {
        int startY = topHalf ? 0 : size / 2;
        int endY = topHalf ? size / 2 : size;
        for (int y = startY; y < endY; y++)
            for (int x = 0; x < size; x++)
                img.SetPixel(x, y, color);
    }

    /// <summary>Fill left or right half of the image.</summary>
    private static void FillHalfVertical(Image img, Color color, int size, bool rightHalf)
    {
        int startX = rightHalf ? size / 2 : 0;
        int endX = rightHalf ? size : size / 2;
        for (int y = 0; y < size; y++)
            for (int x = startX; x < endX; x++)
                img.SetPixel(x, y, color);
    }

    /// <summary>Fill quarter-circle at given corner (cx, cy).</summary>
    private static void FillQuarterCircle(Image img, Color color, int size, float cx, float cy)
    {
        float radius = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist < radius - 1f)
                {
                    img.SetPixel(x, y, color);
                }
                else if (dist < radius)
                {
                    // Anti-aliasing edge.
                    float alpha = 1f - (dist - (radius - 1f));
                    img.SetPixel(x, y, new Color(color.R, color.G, color.B, color.A * alpha));
                }
            }
        }
    }
}

/// <summary>
/// Biome color palette for stratum 0 (background) and transition overlays.
/// Muted colors — stratum 1 surface tiles will be drawn on top.
/// </summary>
public static class BiomePalette
{
    public static Color Get(BiomeType biome) => biome switch
    {
        BiomeType.Ocean      => new Color(0.10f, 0.15f, 0.30f),
        BiomeType.Sea        => new Color(0.15f, 0.25f, 0.45f),
        BiomeType.Coast      => new Color(0.70f, 0.65f, 0.45f),
        BiomeType.Grassland  => new Color(0.20f, 0.35f, 0.15f),
        BiomeType.Steppe     => new Color(0.40f, 0.32f, 0.18f),
        BiomeType.Forest     => new Color(0.12f, 0.25f, 0.10f),
        BiomeType.Highlands  => new Color(0.38f, 0.36f, 0.34f),
        BiomeType.Mountains  => new Color(0.65f, 0.65f, 0.68f),
        BiomeType.Peak       => new Color(0.85f, 0.88f, 0.92f),
        _                    => new Color(0.20f, 0.35f, 0.15f),
    };
}

/// <summary>Terrain (stratum 1) color palette — for surface tiles.</summary>
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

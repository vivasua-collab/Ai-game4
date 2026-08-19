#nullable enable
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Renders stratum 3+ (environment objects: trees, rocks, bushes, ore).
///
/// Uses PROCEDURAL textures generated at startup (Image → ImageTexture).
/// No PNG files needed — placeholders that will be replaced with quality sprites later
/// (per SPRITE_PROMPTS_OBJECTS.md).
///
/// Rendering: one _Draw() call, DrawTexture per non-empty object tile.
/// ZIndex = RenderLayer.Objects (3) — above terrain (2), below player (4).
/// </summary>
public partial class ObjectLayerRenderer : Node2D
{
    private ITileService? _tileService;
    private int _tileSize;
    private Dictionary<ObjectType, Texture2D> _textures = new();

    /// <summary>Initialize with tile service and tile pixel size.</summary>
    public void Initialize(ITileService tileService, int tileSize)
    {
        _tileService = tileService;
        _tileSize = tileSize;
        _textures = GenerateObjectTextures(tileSize);
        ZIndex = (int)RenderLayer.Objects;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_tileService == null) return;

        int w = _tileService.MapWidth;
        int h = _tileService.MapHeight;
        int drawn = 0;

        // Viewport culling: only draw objects on visible tiles.
        GetVisibleTileRange(out int xMin, out int yMin, out int xMax, out int yMax, w, h);

        for (int x = xMin; x <= xMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                var tile = _tileService.GetTile(x, y);
                if (tile.Object == ObjectType.None) continue;

                if (_textures.TryGetValue(tile.Object, out var tex))
                {
                    // Center object sprite on tile (offset by half tile minus half texture).
                    var pos = new Vector2(x * _tileSize, y * _tileSize);
                    DrawTexture(tex, pos);
                    drawn++;
                }
            }
        }

        if (drawn > 0)
            GD.Print($"[ObjectLayer] Drew {drawn} object sprites (culled to {xMax-xMin+1}×{yMax-yMin+1})");
    }

    /// <summary>
    /// Compute visible tile range from viewport + camera transform.
    /// </summary>
    private void GetVisibleTileRange(out int xMin, out int yMin, out int xMax, out int yMax, int w, int h)
    {
        var canvasXform = GetGlobalTransformWithCanvas();
        var vpRectScreen = GetViewportRect();
        var topLeft = canvasXform.AffineInverse() * vpRectScreen.Position;
        var botRight = canvasXform.AffineInverse() * (vpRectScreen.Position + vpRectScreen.Size);

        xMin = Mathf.Clamp((int)(topLeft.X / _tileSize), 0, w - 1);
        yMin = Mathf.Clamp((int)(topLeft.Y / _tileSize), 0, h - 1);
        xMax = Mathf.Clamp((int)(botRight.X / _tileSize) + 1, 0, w - 1);
        yMax = Mathf.Clamp((int)(botRight.Y / _tileSize) + 1, 0, h - 1);
    }

    /// <summary>Refresh after tile changes (harvest/depletion).</summary>
    public void Refresh()
    {
        QueueRedraw();
    }

    // === Procedural texture generation ===

    /// <summary>
    /// Generate placeholder textures for each ObjectType using Godot Image API.
    /// Each texture is tileSize×tileSize with alpha, drawn as simple shapes:
    /// - Trees: brown trunk + green canopy circle
    /// - Rocks: gray polygon
    /// - Bushes: green ellipse cluster
    /// - Ore: gray rock + colored specks
    /// </summary>
    private static Dictionary<ObjectType, Texture2D> GenerateObjectTextures(int size)
    {
        var dict = new Dictionary<ObjectType, Texture2D>();

        dict[ObjectType.Tree_Oak] = MakeTreeTexture(size, trunkColor: new Color(0.45f, 0.30f, 0.15f),
            canopyColor: new Color(0.20f, 0.45f, 0.15f), canopyRadius: 0.38f);
        dict[ObjectType.Tree_Pine] = MakeTreeTexture(size, trunkColor: new Color(0.35f, 0.25f, 0.12f),
            canopyColor: new Color(0.10f, 0.35f, 0.12f), canopyRadius: 0.32f, triangular: true);
        dict[ObjectType.Tree_Birch] = MakeTreeTexture(size, trunkColor: new Color(0.85f, 0.80f, 0.70f),
            canopyColor: new Color(0.35f, 0.55f, 0.20f), canopyRadius: 0.34f);

        dict[ObjectType.Rock_Small] = MakeRockTexture(size, rockColor: new Color(0.55f, 0.55f, 0.52f),
            scale: 0.4f);
        dict[ObjectType.Rock_Medium] = MakeRockTexture(size, rockColor: new Color(0.50f, 0.50f, 0.48f),
            scale: 0.6f);
        dict[ObjectType.Rock_Large] = MakeRockTexture(size, rockColor: new Color(0.45f, 0.45f, 0.43f),
            scale: 0.8f);

        dict[ObjectType.Bush] = MakeBushTexture(size, berryColor: null);
        dict[ObjectType.Bush_Berry] = MakeBushTexture(size, berryColor: new Color(0.7f, 0.1f, 0.1f));

        dict[ObjectType.OreVein] = MakeOreTexture(size, oreColor: new Color(0.6f, 0.4f, 0.2f));

        dict[ObjectType.Herb] = MakeHerbTexture(size);

        dict[ObjectType.Chest] = MakeChestTexture(size);

        return dict;
    }

    private static Texture2D MakeTreeTexture(int size, Color trunkColor, Color canopyColor,
        float canopyRadius, bool triangular = false)
    {
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(new Color(0, 0, 0, 0)); // transparent

        int cx = size / 2;
        int cy = size / 2;

        // Trunk: brown rectangle at bottom-center.
        int trunkW = size / 8;
        int trunkH = size / 3;
        int trunkX = cx - trunkW / 2;
        int trunkY = cy + size / 6;
        FillRect(image, trunkX, trunkY, trunkW, trunkH, trunkColor);

        // Canopy.
        int radius = (int)(size * canopyRadius);
        if (triangular)
        {
            // Pine: triangle (3 stacked triangles for layered look).
            int topY = cy - size / 3;
            for (int layer = 0; layer < 3; layer++)
            {
                int layerY = topY + layer * (size / 6);
                int layerR = radius - layer * (size / 12);
                FillTriangle(image, cx, layerY, cx - layerR, layerY + size / 3, cx + layerR, layerY + size / 3, canopyColor);
            }
        }
        else
        {
            // Oak/Birch: filled circle.
            FillCircle(image, cx, cy - size / 8, radius, canopyColor);
            // Darker outline for depth.
            DrawCircleOutline(image, cx, cy - size / 8, radius, canopyColor.Darkened(0.3f));
        }

        return ImageTexture.CreateFromImage(image);
    }

    private static Texture2D MakeRockTexture(int size, Color rockColor, float scale)
    {
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(new Color(0, 0, 0, 0));

        int cx = size / 2;
        int cy = size / 2;
        int r = (int)(size * scale * 0.5f);

        // Irregular polygon (approximated as filled circle with bumps).
        FillCircle(image, cx, cy, r, rockColor);

        // Highlight (lighter top-left).
        FillCircle(image, cx - r / 4, cy - r / 4, r / 3, rockColor.Lightened(0.2f));

        // Shadow (darker bottom-right).
        FillCircle(image, cx + r / 4, cy + r / 4, r / 3, rockColor.Darkened(0.2f));

        // Outline.
        DrawCircleOutline(image, cx, cy, r, rockColor.Darkened(0.4f));

        return ImageTexture.CreateFromImage(image);
    }

    private static Texture2D MakeBushTexture(int size, Color? berryColor)
    {
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(new Color(0, 0, 0, 0));

        int cx = size / 2;
        int cy = size / 2;
        int r = (int)(size * 0.3f);

        // Cluster of 3 green circles for bush body.
        var bushGreen = new Color(0.15f, 0.40f, 0.15f);
        FillCircle(image, cx - r / 2, cy, r, bushGreen);
        FillCircle(image, cx + r / 2, cy, r, bushGreen);
        FillCircle(image, cx, cy - r / 2, r, bushGreen);

        // Darker spots for depth.
        var darkGreen = bushGreen.Darkened(0.3f);
        FillCircle(image, cx - r / 3, cy + r / 4, r / 4, darkGreen);
        FillCircle(image, cx + r / 3, cy + r / 4, r / 4, darkGreen);

        // Berries (red dots) if berry bush.
        if (berryColor.HasValue)
        {
            var rng = new System.Random(42);
            for (int i = 0; i < 5; i++)
            {
                int bx = cx + rng.Next(-r, r + 1);
                int by = cy + rng.Next(-r, r + 1);
                FillCircle(image, bx, by, 2, berryColor.Value);
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    private static Texture2D MakeOreTexture(int size, Color oreColor)
    {
        // Base: gray rock.
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(new Color(0, 0, 0, 0));

        int cx = size / 2;
        int cy = size / 2;
        int r = (int)(size * 0.35f);

        var rockColor = new Color(0.50f, 0.50f, 0.48f);
        FillCircle(image, cx, cy, r, rockColor);
        DrawCircleOutline(image, cx, cy, r, rockColor.Darkened(0.4f));

        // Ore veins: colored specks scattered on rock.
        var rng = new System.Random(99);
        for (int i = 0; i < 8; i++)
        {
            int ox = cx + rng.Next(-r, r + 1);
            int oy = cy + rng.Next(-r, r + 1);
            int orad = rng.Next(2, 4);
            FillCircle(image, ox, oy, orad, oreColor);
        }

        return ImageTexture.CreateFromImage(image);
    }

    private static Texture2D MakeHerbTexture(int size)
    {
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(new Color(0, 0, 0, 0));

        int cx = size / 2;
        int cy = size / 2;

        // Stem (thin green line).
        var stemColor = new Color(0.2f, 0.5f, 0.15f);
        FillRect(image, cx - 1, cy, 2, size / 3, stemColor);

        // Leaves (small green circles on stem).
        var leafColor = new Color(0.3f, 0.6f, 0.2f);
        FillCircle(image, cx - 4, cy + size / 8, 4, leafColor);
        FillCircle(image, cx + 4, cy + size / 8, 4, leafColor);
        FillCircle(image, cx, cy - 2, 5, leafColor);

        // Flower (small colored dot on top).
        var flowerColor = new Color(0.9f, 0.8f, 0.2f);
        FillCircle(image, cx, cy - size / 6, 3, flowerColor);

        return ImageTexture.CreateFromImage(image);
    }

    private static Texture2D MakeChestTexture(int size)
    {
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(new Color(0, 0, 0, 0));

        int cx = size / 2;
        int cy = size / 2;
        int w = (int)(size * 0.5f);
        int h = (int)(size * 0.4f);

        // Chest body (brown).
        var woodColor = new Color(0.45f, 0.30f, 0.15f);
        FillRect(image, cx - w / 2, cy - h / 2, w, h, woodColor);

        // Lid (darker brown, top half).
        var lidColor = woodColor.Darkened(0.2f);
        FillRect(image, cx - w / 2, cy - h / 2, w, h / 3, lidColor);

        // Gold trim (metal band).
        var goldColor = new Color(0.85f, 0.65f, 0.15f);
        FillRect(image, cx - w / 2, cy - h / 6, w, 2, goldColor);

        // Lock (small gold square).
        FillRect(image, cx - 2, cy, 4, 4, goldColor);

        return ImageTexture.CreateFromImage(image);
    }

    // === Low-level pixel drawing helpers ===

    private static void FillRect(Image image, int x, int y, int w, int h, Color color)
    {
        int maxX = Mathf.Min(x + w, image.GetWidth());
        int maxY = Mathf.Min(y + h, image.GetHeight());
        for (int py = Mathf.Max(0, y); py < maxY; py++)
        {
            for (int px = Mathf.Max(0, x); px < maxX; px++)
            {
                image.SetPixel(px, py, color);
            }
        }
    }

    private static void FillCircle(Image image, int cx, int cy, int r, Color color)
    {
        int r2 = r * r;
        for (int y = -r; y <= r; y++)
        {
            for (int x = -r; x <= r; x++)
            {
                if (x * x + y * y <= r2)
                {
                    int px = cx + x;
                    int py = cy + y;
                    if (px >= 0 && px < image.GetWidth() && py >= 0 && py < image.GetHeight())
                    {
                        image.SetPixel(px, py, color);
                    }
                }
            }
        }
    }

    private static void DrawCircleOutline(Image image, int cx, int cy, int r, Color color)
    {
        for (int angle = 0; angle < 360; angle += 5)
        {
            float rad = Mathf.DegToRad(angle);
            int px = cx + (int)(r * Mathf.Cos(rad));
            int py = cy + (int)(r * Mathf.Sin(rad));
            if (px >= 0 && px < image.GetWidth() && py >= 0 && py < image.GetHeight())
            {
                image.SetPixel(px, py, color);
            }
        }
    }

    private static void FillTriangle(Image image, int x1, int y1, int x2, int y2, int x3, int y3, Color color)
    {
        // Bounding box.
        int minX = Mathf.Min(x1, Mathf.Min(x2, x3));
        int maxX = Mathf.Max(x1, Mathf.Max(x2, x3));
        int minY = Mathf.Min(y1, Mathf.Min(y2, y3));
        int maxY = Mathf.Max(y1, Mathf.Max(y2, y3));

        for (int py = minY; py <= maxY; py++)
        {
            for (int px = minX; px <= maxX; px++)
            {
                if (px < 0 || px >= image.GetWidth() || py < 0 || py >= image.GetHeight()) continue;
                if (PointInTriangle(px, py, x1, y1, x2, y2, x3, y3))
                {
                    image.SetPixel(px, py, color);
                }
            }
        }
    }

    private static bool PointInTriangle(int px, int py, int x1, int y1, int x2, int y2, int x3, int y3)
    {
        int d1 = (px - x2) * (y1 - y2) - (x1 - x2) * (py - y2);
        int d2 = (px - x3) * (y2 - y3) - (x2 - x3) * (py - y3);
        int d3 = (px - x1) * (y3 - y1) - (x3 - x1) * (py - y1);
        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
        return !(hasNeg && hasPos);
    }
}

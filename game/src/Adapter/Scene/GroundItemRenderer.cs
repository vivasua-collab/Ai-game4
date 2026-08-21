#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Core.DI;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Renders items dropped on the ground (overflow from inventory + player throw).
///
/// Listens to ItemDroppedEvent / ItemPickedUpEvent from EventBus.
/// Each ground item = small procedural sprite (16×16) at world position.
/// ZIndex = RenderLayer.Objects (3) — same as environment objects.
/// </summary>
public partial class GroundItemRenderer : Node2D
{
    [Inject] private IGroundItemService GroundItemService { get; set; } = null!;
    [Inject] private IItemDatabaseService ItemDatabase { get; set; } = null!;
    [Inject] private ISubscriber<ItemDroppedEvent> DroppedSub { get; set; } = null!;
    [Inject] private ISubscriber<ItemPickedUpEvent> PickedUpSub { get; set; } = null!;

    private readonly Dictionary<long, Sprite2D> _sprites = new();
    private Dictionary<ItemCategory, Texture2D> _categoryTextures = new();
    private IDisposable? _droppedToken;
    private IDisposable? _pickedUpToken;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
        {
            ContainerAdapter.InjectProperties(this, container);
        }

        _categoryTextures = GenerateCategoryTextures();
        ZIndex = (int)RenderLayer.Objects + 1; // above environment objects

        // Subscribe to events.
        _droppedToken = DroppedSub.Subscribe(OnItemDropped);
        _pickedUpToken = PickedUpSub.Subscribe(OnItemPickedUp);

        // Render any existing ground items (e.g. after load).
        RefreshAll();

        GD.Print("[GroundItemRenderer] Ready");
    }

    public override void _ExitTree()
    {
        _droppedToken?.Dispose();
        _pickedUpToken?.Dispose();
    }

    private void OnItemDropped(in ItemDroppedEvent e)
    {
        CreateSprite(e.DropId, e.ItemId, e.Count, e.WorldX, e.WorldY);
    }

    private void OnItemPickedUp(in ItemPickedUpEvent e)
    {
        RemoveSprite(e.DropId);
    }

    private void CreateSprite(long dropId, string itemId, int count, float worldX, float worldY)
    {
        if (_sprites.ContainsKey(dropId)) return;

        // Resolve item category for texture.
        ItemCategory category = ItemCategory.Misc;
        if (ItemDatabase.TryGetItem(itemId, out var itemData))
        {
            category = itemData.Category;
        }

        Texture2D tex;
        if (!_categoryTextures.TryGetValue(category, out var catTex))
        {
            catTex = _categoryTextures[ItemCategory.Misc];
        }
        tex = catTex;

        var sprite = new Sprite2D
        {
            Name = $"GroundItem_{dropId}",
            Texture = tex,
            Position = new Vector2(worldX, worldY),
            Scale = new Vector2(0.5f, 0.5f), // 16×16 sprite scaled to 8×8 on ground
            ZIndex = ZIndex,
        };
        AddChild(sprite);
        _sprites[dropId] = sprite;
    }

    private void RemoveSprite(long dropId)
    {
        if (_sprites.TryGetValue(dropId, out var sprite))
        {
            sprite.QueueFree();
            _sprites.Remove(dropId);
        }
    }

    private void RefreshAll()
    {
        // Clear existing.
        foreach (var kvp in _sprites)
        {
            kvp.Value.QueueFree();
        }
        _sprites.Clear();

        // Recreate from service state.
        foreach (var item in GroundItemService.GetAllGroundItems())
        {
            CreateSprite(item.DropId, item.ItemId, item.Count, item.WorldX, item.WorldY);
        }
    }

    // === Procedural texture generation (16×16 small icons) ===

    private static Dictionary<ItemCategory, Texture2D> GenerateCategoryTextures()
    {
        var dict = new Dictionary<ItemCategory, Texture2D>();
        int size = 16;

        dict[ItemCategory.Weapon] = MakeIcon(size, new Color(0.7f, 0.7f, 0.8f), new Color(0.4f, 0.4f, 0.5f), "sword");
        dict[ItemCategory.Armor] = MakeIcon(size, new Color(0.6f, 0.6f, 0.65f), new Color(0.3f, 0.3f, 0.35f), "shield");
        dict[ItemCategory.Accessory] = MakeIcon(size, new Color(0.9f, 0.8f, 0.2f), new Color(0.6f, 0.5f, 0.1f), "ring");
        dict[ItemCategory.Consumable] = MakeIcon(size, new Color(0.8f, 0.3f, 0.3f), new Color(0.5f, 0.1f, 0.1f), "potion");
        dict[ItemCategory.Material] = MakeIcon(size, new Color(0.6f, 0.5f, 0.3f), new Color(0.3f, 0.25f, 0.15f), "cube");
        dict[ItemCategory.Technique] = MakeIcon(size, new Color(0.5f, 0.3f, 0.7f), new Color(0.3f, 0.15f, 0.5f), "scroll");
        dict[ItemCategory.Quest] = MakeIcon(size, new Color(0.9f, 0.7f, 0.2f), new Color(0.6f, 0.4f, 0.1f), "star");
        dict[ItemCategory.Misc] = MakeIcon(size, new Color(0.5f, 0.5f, 0.5f), new Color(0.3f, 0.3f, 0.3f), "circle");

        return dict;
    }

    private static Texture2D MakeIcon(int size, Color mainColor, Color darkColor, string shape)
    {
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(new Color(0, 0, 0, 0)); // transparent

        int cx = size / 2;
        int cy = size / 2;

        switch (shape)
        {
            case "sword":
                // Vertical line (blade) + horizontal line (guard).
                FillRect(image, cx - 1, 2, 2, 10, mainColor);
                FillRect(image, cx - 4, cy, 8, 2, darkColor);
                FillRect(image, cx - 1, 12, 2, 2, darkColor);
                break;
            case "shield":
                // Shield shape (rounded rect).
                FillRect(image, cx - 4, 2, 8, 10, mainColor);
                FillRect(image, cx - 3, 3, 6, 8, darkColor);
                break;
            case "ring":
                // Ring (circle outline).
                DrawCircleOutline(image, cx, cy, 5, mainColor);
                DrawCircleOutline(image, cx, cy, 3, darkColor);
                break;
            case "potion":
                // Bottle shape.
                FillRect(image, cx - 1, 1, 2, 3, darkColor); // neck
                FillCircle(image, cx, cy + 2, 4, mainColor); // body
                FillCircle(image, cx, cy + 2, 2, darkColor); // liquid
                break;
            case "cube":
                // Cube (material).
                FillRect(image, cx - 4, cy - 4, 8, 8, mainColor);
                FillRect(image, cx - 3, cy - 3, 6, 6, darkColor);
                break;
            case "scroll":
                // Scroll (horizontal rect).
                FillRect(image, 2, cy - 2, size - 4, 4, mainColor);
                FillRect(image, 2, cy - 2, size - 4, 1, darkColor);
                FillRect(image, 2, cy + 1, size - 4, 1, darkColor);
                break;
            case "star":
                // Star (5 points).
                DrawStar(image, cx, cy, 5, mainColor);
                break;
            default: // circle
                FillCircle(image, cx, cy, 5, mainColor);
                DrawCircleOutline(image, cx, cy, 5, darkColor);
                break;
        }

        return ImageTexture.CreateFromImage(image);
    }

    private static void FillRect(Image image, int x, int y, int w, int h, Color color)
    {
        int maxX = Mathf.Min(x + w, image.GetWidth());
        int maxY = Mathf.Min(y + h, image.GetHeight());
        for (int py = Mathf.Max(0, y); py < maxY; py++)
            for (int px = Mathf.Max(0, x); px < maxX; px++)
                image.SetPixel(px, py, color);
    }

    private static void FillCircle(Image image, int cx, int cy, int r, Color color)
    {
        int r2 = r * r;
        for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
                if (x * x + y * y <= r2)
                {
                    int px = cx + x, py = cy + y;
                    if (px >= 0 && px < image.GetWidth() && py >= 0 && py < image.GetHeight())
                        image.SetPixel(px, py, color);
                }
    }

    private static void DrawCircleOutline(Image image, int cx, int cy, int r, Color color)
    {
        for (int angle = 0; angle < 360; angle += 8)
        {
            float rad = Mathf.DegToRad(angle);
            int px = cx + (int)(r * Mathf.Cos(rad));
            int py = cy + (int)(r * Mathf.Sin(rad));
            if (px >= 0 && px < image.GetWidth() && py >= 0 && py < image.GetHeight())
                image.SetPixel(px, py, color);
        }
    }

    private static void DrawStar(Image image, int cx, int cy, int r, Color color)
    {
        // Simple 5-point star: 5 triangles from center.
        for (int i = 0; i < 5; i++)
        {
            float angle1 = Mathf.DegToRad(i * 72 - 90);
            float angle2 = Mathf.DegToRad((i + 1) * 72 - 90);
            int x1 = cx + (int)(r * Mathf.Cos(angle1));
            int y1 = cy + (int)(r * Mathf.Sin(angle1));
            int x2 = cx + (int)(r * Mathf.Cos(angle2));
            int y2 = cy + (int)(r * Mathf.Sin(angle2));
            // Draw line from center to point.
            DrawLine(image, cx, cy, x1, y1, color);
            DrawLine(image, cx, cy, x2, y2, color);
        }
    }

    private static void DrawLine(Image image, int x1, int y1, int x2, int y2, Color color)
    {
        int dx = Math.Abs(x2 - x1);
        int dy = Math.Abs(y2 - y1);
        int sx = x1 < x2 ? 1 : -1;
        int sy = y1 < y2 ? 1 : -1;
        int err = dx - dy;
        int x = x1, y = y1;
        while (true)
        {
            if (x >= 0 && x < image.GetWidth() && y >= 0 && y < image.GetHeight())
                image.SetPixel(x, y, color);
            if (x == x2 && y == y2) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }
        }
    }
}

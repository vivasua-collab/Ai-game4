#nullable enable
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.Data;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Procedural sprite generator for characters, animals, and equipment.
/// All sprites are generated at runtime via Image API — no PNG files needed.
///
/// Categories:
/// - Player: humanoid silhouette (cultivator robe)
/// - NPC: role-based colors (guard, merchant, elder, cultivator, enemy, passerby)
/// - Animals: species-based (wolf, deer, rabbit)
/// - Equipment: category-based icons (weapon, armor, accessory)
///
/// Design: SPRITE_CATALOG.md §19 (fallback procedural sprites).
/// </summary>
public static class ProceduralSpriteGenerator
{
    // === Character sprites ===

    /// <summary>Generate player sprite (cultivator in robe, 48×48).</summary>
    public static Texture2D CreatePlayerSprite()
    {
        int size = 48;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(new Color(0, 0, 0, 0));

        int cx = size / 2;
        var robeColor = new Color(0.3f, 0.2f, 0.5f); // purple robe (cultivator)
        var skinColor = new Color(0.9f, 0.75f, 0.6f); // skin
        var hairColor = new Color(0.15f, 0.1f, 0.05f); // dark hair

        // Head (circle).
        FillCircle(image, cx, 12, 6, skinColor);
        // Hair (top half of head).
        FillCircle(image, cx, 10, 5, hairColor);
        // Torso (robe — trapezoid).
        FillRect(image, cx - 5, 18, 10, 14, robeColor);
        FillRect(image, cx - 7, 28, 14, 8, robeColor); // widen at bottom
        // Belt.
        FillRect(image, cx - 6, 26, 12, 2, new Color(0.4f, 0.3f, 0.1f));
        // Arms (robe sleeves).
        FillRect(image, cx - 8, 19, 3, 10, robeColor);
        FillRect(image, cx + 5, 19, 3, 10, robeColor);
        // Hands.
        FillCircle(image, cx - 6, 30, 2, skinColor);
        FillCircle(image, cx + 6, 30, 2, skinColor);
        // Legs (under robe).
        FillRect(image, cx - 4, 36, 3, 8, new Color(0.2f, 0.15f, 0.1f));
        FillRect(image, cx + 1, 36, 3, 8, new Color(0.2f, 0.15f, 0.1f));
        // Boots.
        FillRect(image, cx - 5, 43, 4, 3, new Color(0.1f, 0.08f, 0.05f));
        FillRect(image, cx + 1, 43, 4, 3, new Color(0.1f, 0.08f, 0.05f));

        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>Generate NPC sprite based on role (48×48).</summary>
    public static Texture2D CreateNPCSprite(NPCRole role)
    {
        int size = 48;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(new Color(0, 0, 0, 0));

        int cx = size / 2;
        var (robeColor, skinColor) = role switch
        {
            NPCRole.Guard      => (new Color(0.2f, 0.3f, 0.5f), new Color(0.9f, 0.75f, 0.6f)),  // blue guard
            NPCRole.Merchant   => (new Color(0.5f, 0.4f, 0.15f), new Color(0.9f, 0.75f, 0.6f)), // gold merchant
            NPCRole.Elder      => (new Color(0.4f, 0.35f, 0.25f), new Color(0.85f, 0.7f, 0.55f)), // brown elder
            NPCRole.Cultivator => (new Color(0.2f, 0.4f, 0.3f), new Color(0.9f, 0.75f, 0.6f)),  // green cultivator
            NPCRole.Enemy      => (new Color(0.5f, 0.15f, 0.15f), new Color(0.85f, 0.65f, 0.55f)), // red enemy
            NPCRole.Passerby   => (new Color(0.4f, 0.4f, 0.4f), new Color(0.9f, 0.75f, 0.6f)),  // grey passerby
            NPCRole.Disciple   => (new Color(0.25f, 0.3f, 0.45f), new Color(0.9f, 0.75f, 0.6f)), // blue-grey disciple
            _                  => (new Color(0.4f, 0.4f, 0.4f), new Color(0.9f, 0.75f, 0.6f)),
        };
        var hairColor = new Color(0.15f, 0.1f, 0.05f);

        // Head.
        FillCircle(image, cx, 12, 6, skinColor);
        FillCircle(image, cx, 10, 5, hairColor);
        // Torso.
        FillRect(image, cx - 5, 18, 10, 14, robeColor);
        FillRect(image, cx - 7, 28, 14, 8, robeColor);
        // Belt.
        FillRect(image, cx - 6, 26, 12, 2, new Color(0.3f, 0.25f, 0.1f));
        // Arms.
        FillRect(image, cx - 8, 19, 3, 10, robeColor);
        FillRect(image, cx + 5, 19, 3, 10, robeColor);
        // Hands.
        FillCircle(image, cx - 6, 30, 2, skinColor);
        FillCircle(image, cx + 6, 30, 2, skinColor);
        // Legs.
        FillRect(image, cx - 4, 36, 3, 8, new Color(0.2f, 0.15f, 0.1f));
        FillRect(image, cx + 1, 36, 3, 8, new Color(0.2f, 0.15f, 0.1f));
        // Boots.
        FillRect(image, cx - 5, 43, 4, 3, new Color(0.1f, 0.08f, 0.05f));
        FillRect(image, cx + 1, 43, 4, 3, new Color(0.1f, 0.08f, 0.05f));

        // Role indicator (small colored dot above head).
        var indicatorColor = role switch
        {
            NPCRole.Guard      => new Color(0.3f, 0.5f, 0.8f),
            NPCRole.Merchant   => new Color(0.8f, 0.7f, 0.2f),
            NPCRole.Elder      => new Color(0.6f, 0.5f, 0.3f),
            NPCRole.Cultivator => new Color(0.3f, 0.7f, 0.4f),
            NPCRole.Enemy      => new Color(0.8f, 0.2f, 0.2f),
            _                  => new Color(0.5f, 0.5f, 0.5f),
        };
        FillCircle(image, cx, 4, 2, indicatorColor);

        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>Generate animal sprite based on species (48×48).</summary>
    public static Texture2D CreateAnimalSprite(string species, SizeClass sizeClass = SizeClass.Medium)
    {
        int imgSize = 48;
        var image = Image.CreateEmpty(imgSize, imgSize, false, Image.Format.Rgba8);
        image.Fill(new Color(0, 0, 0, 0));

        var (bodyColor, accentColor) = species switch
        {
            "wolf"   => (new Color(0.35f, 0.35f, 0.38f), new Color(0.15f, 0.15f, 0.18f)), // grey
            "deer"   => (new Color(0.55f, 0.4f, 0.25f), new Color(0.35f, 0.25f, 0.15f)),  // brown
            "rabbit" => (new Color(0.85f, 0.82f, 0.78f), new Color(0.6f, 0.55f, 0.5f)),   // white
            _        => (new Color(0.5f, 0.5f, 0.5f), new Color(0.3f, 0.3f, 0.3f)),
        };

        // Scale by SizeClass.
        float scale = sizeClass switch
        {
            SizeClass.Small  => 0.7f,
            SizeClass.Medium => 1.0f,
            SizeClass.Large  => 1.3f,
            _                => 1.0f,
        };

        int cx = 24;
        int bodyW = (int)(14 * scale);
        int bodyH = (int)(10 * scale);

        // Body (horizontal ellipse — quadruped).
        FillEllipse(image, cx, 28, bodyW, bodyH, bodyColor);
        DrawEllipseOutline(image, cx, 28, bodyW, bodyH, accentColor);

        // Head (front circle).
        int headX = cx - bodyW + 2;
        int headR = (int)(6 * scale);
        FillCircle(image, headX, 26, headR, bodyColor);
        DrawCircleOutline(image, headX, 26, headR, accentColor);

        // Ears (wolf: pointed, deer: small, rabbit: long).
        if (species == "rabbit")
        {
            FillRect(image, headX - 1, 18, 2, 6, bodyColor);
            FillRect(image, headX + 2, 18, 2, 6, bodyColor);
        }
        else if (species == "wolf")
        {
            FillTriangle(image, headX - 3, 20, headX - 1, 16, headX + 1, 20, accentColor);
            FillTriangle(image, headX + 1, 20, headX + 3, 16, headX + 5, 20, accentColor);
        }
        else // deer
        {
            // Antlers.
            DrawLine(image, headX, 20, headX - 3, 14, accentColor, 1);
            DrawLine(image, headX, 20, headX + 3, 14, accentColor, 1);
            DrawLine(image, headX - 3, 14, headX - 5, 12, accentColor, 1);
            DrawLine(image, headX + 3, 14, headX + 5, 12, accentColor, 1);
        }

        // Eye.
        FillCircle(image, headX - 1, 25, 1, new Color(0, 0, 0));

        // Legs (4 lines).
        int legY1 = 28 + bodyH - 2;
        int legY2 = 28 + bodyH + 4;
        DrawLine(image, cx - bodyW + 4, legY1, cx - bodyW + 4, legY2, accentColor, 2);
        DrawLine(image, cx - bodyW + 8, legY1, cx - bodyW + 8, legY2, accentColor, 2);
        DrawLine(image, cx + bodyW - 8, legY1, cx + bodyW - 8, legY2, accentColor, 2);
        DrawLine(image, cx + bodyW - 4, legY1, cx + bodyW - 4, legY2, accentColor, 2);

        // Tail.
        if (species == "wolf")
        {
            FillTriangle(image, cx + bodyW, 26, cx + bodyW + 6, 22, cx + bodyW + 6, 30, accentColor);
        }
        else if (species == "deer")
        {
            FillRect(image, cx + bodyW, 25, 4, 3, accentColor);
        }
        else // rabbit
        {
            FillCircle(image, cx + bodyW, 26, 3, bodyColor);
        }

        return ImageTexture.CreateFromImage(image);
    }

    // === Equipment icon sprites ===

    /// <summary>Generate equipment icon based on category + slot (32×32).</summary>
    public static Texture2D CreateEquipmentIcon(ItemCategory category, EquipmentSlot slot, ItemRarity rarity)
    {
        int size = 32;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(new Color(0, 0, 0, 0));

        var rarityColor = GetRarityColor(rarity);
        var darkColor = rarityColor.Darkened(0.4f);

        switch (category)
        {
            case ItemCategory.Weapon:
                DrawWeaponIcon(image, size, slot, rarityColor, darkColor);
                break;
            case ItemCategory.Armor:
                DrawArmorIcon(image, size, slot, rarityColor, darkColor);
                break;
            case ItemCategory.Accessory:
                DrawAccessoryIcon(image, size, slot, rarityColor, darkColor);
                break;
            default:
                // Generic item (material, consumable, etc.)
                FillRect(image, 8, 8, 16, 16, rarityColor);
                DrawRectOutline(image, 8, 8, 16, 16, darkColor);
                break;
        }

        return ImageTexture.CreateFromImage(image);
    }

    private static void DrawWeaponIcon(Image image, int size, EquipmentSlot slot, Color main, Color dark)
    {
        int cx = size / 2;
        if (slot == EquipmentSlot.WeaponMain || slot == EquipmentSlot.WeaponOff)
        {
            // Sword: vertical blade + crossguard + handle.
            FillRect(image, cx - 1, 4, 2, 16, main);   // blade
            FillRect(image, cx - 5, 18, 10, 2, dark);  // crossguard
            FillRect(image, cx - 1, 20, 2, 6, dark);   // handle
            FillRect(image, cx - 2, 25, 4, 2, dark);   // pommel
        }
        else
        {
            // Generic weapon (bow/staff).
            DrawLine(image, cx, 4, cx, 26, main, 2);
            FillCircle(image, cx, 4, 2, dark);
        }
    }

    private static void DrawArmorIcon(Image image, int size, EquipmentSlot slot, Color main, Color dark)
    {
        int cx = size / 2;
        switch (slot)
        {
            case EquipmentSlot.Head:
                // Helmet.
                FillEllipse(image, cx, 12, 8, 6, main);
                DrawEllipseOutline(image, cx, 12, 8, 6, dark);
                FillRect(image, cx - 2, 14, 4, 4, dark); // face guard
                break;
            case EquipmentSlot.Torso:
                // Breastplate.
                FillRect(image, cx - 7, 6, 14, 18, main);
                DrawRectOutline(image, cx - 7, 6, 14, 18, dark);
                FillRect(image, cx - 1, 8, 2, 14, dark); // center seam
                break;
            case EquipmentSlot.Legs:
                // Greaves.
                FillRect(image, cx - 5, 4, 4, 22, main);
                FillRect(image, cx + 1, 4, 4, 22, main);
                DrawRectOutline(image, cx - 5, 4, 4, 22, dark);
                DrawRectOutline(image, cx + 1, 4, 4, 22, dark);
                break;
            case EquipmentSlot.Feet:
                // Boots.
                FillRect(image, cx - 6, 4, 5, 18, main);
                FillRect(image, cx + 1, 4, 5, 18, main);
                FillRect(image, cx - 7, 20, 6, 4, dark); // sole
                FillRect(image, cx + 1, 20, 6, 4, dark);
                break;
            case EquipmentSlot.Belt:
                // Belt.
                FillRect(image, 4, 12, 24, 4, main);
                FillRect(image, cx - 2, 12, 4, 8, dark); // buckle
                break;
            case EquipmentSlot.Hands:
                // Gauntlets.
                FillRect(image, cx - 8, 6, 6, 12, main);
                FillRect(image, cx + 2, 6, 6, 12, main);
                DrawRectOutline(image, cx - 8, 6, 6, 12, dark);
                DrawRectOutline(image, cx + 2, 6, 6, 12, dark);
                // Fingers.
                for (int i = 0; i < 3; i++)
                {
                    FillRect(image, cx - 8 + i * 2, 18, 1, 4, dark);
                    FillRect(image, cx + 2 + i * 2, 18, 1, 4, dark);
                }
                break;
            default:
                FillRect(image, 8, 8, 16, 16, main);
                DrawRectOutline(image, 8, 8, 16, 16, dark);
                break;
        }
    }

    private static void DrawAccessoryIcon(Image image, int size, EquipmentSlot slot, Color main, Color dark)
    {
        int cx = size / 2;
        int cy = size / 2;
        switch (slot)
        {
            case EquipmentSlot.Amulet:
                // Amulet: chain + pendant.
                DrawCircleOutline(image, cx, cy - 4, 6, dark); // chain
                FillCircle(image, cx, cy + 4, 4, main); // pendant
                DrawCircleOutline(image, cx, cy + 4, 4, dark);
                break;
            case EquipmentSlot.RingLeft1:
            case EquipmentSlot.RingRight1:
            case EquipmentSlot.RingLeft2:
            case EquipmentSlot.RingRight2:
                // Ring.
                DrawCircleOutline(image, cx, cy, 7, main);
                DrawCircleOutline(image, cx, cy, 4, dark);
                break;
            case EquipmentSlot.Back:
                // Cloak/cape.
                FillTriangle(image, cx, 4, 4, 28, cx - 2, 28, main);
                FillTriangle(image, cx, 4, 28, 28, cx + 2, 28, main);
                DrawLine(image, cx, 4, 4, 28, dark, 1);
                DrawLine(image, cx, 4, 28, 28, dark, 1);
                break;
            default:
                FillCircle(image, cx, cy, 6, main);
                DrawCircleOutline(image, cx, cy, 6, dark);
                break;
        }
    }

    // === Helpers ===

    private static Color GetRarityColor(ItemRarity rarity)
    {
        return rarity switch
        {
            ItemRarity.Common    => new Color(0.6f, 0.6f, 0.6f),
            ItemRarity.Uncommon  => new Color(0.2f, 0.7f, 0.3f),
            ItemRarity.Rare      => new Color(0.3f, 0.5f, 0.9f),
            ItemRarity.Epic      => new Color(0.7f, 0.3f, 0.9f),
            ItemRarity.Legendary => new Color(0.9f, 0.7f, 0.1f),
            ItemRarity.Mythic    => new Color(0.9f, 0.2f, 0.2f),
            _                    => new Color(0.5f, 0.5f, 0.5f),
        };
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

    private static void FillEllipse(Image image, int cx, int cy, int rx, int ry, Color color)
    {
        for (int y = -ry; y <= ry; y++)
            for (int x = -rx; x <= rx; x++)
                if (x * x * ry * ry + y * y * rx * rx <= rx * rx * ry * ry)
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

    private static void DrawEllipseOutline(Image image, int cx, int cy, int rx, int ry, Color color)
    {
        for (int angle = 0; angle < 360; angle += 8)
        {
            float rad = Mathf.DegToRad(angle);
            int px = cx + (int)(rx * Mathf.Cos(rad));
            int py = cy + (int)(ry * Mathf.Sin(rad));
            if (px >= 0 && px < image.GetWidth() && py >= 0 && py < image.GetHeight())
                image.SetPixel(px, py, color);
        }
    }

    private static void DrawRectOutline(Image image, int x, int y, int w, int h, Color color)
    {
        // Top + bottom.
        FillRect(image, x, y, w, 1, color);
        FillRect(image, x, y + h - 1, w, 1, color);
        // Left + right.
        FillRect(image, x, y, 1, h, color);
        FillRect(image, x + w - 1, y, 1, h, color);
    }

    private static void FillTriangle(Image image, int x1, int y1, int x2, int y2, int x3, int y3, Color color)
    {
        int minX = Mathf.Min(x1, Mathf.Min(x2, x3));
        int maxX = Mathf.Max(x1, Mathf.Max(x2, x3));
        int minY = Mathf.Min(y1, Mathf.Min(y2, y3));
        int maxY = Mathf.Max(y1, Mathf.Max(y2, y3));
        for (int py = minY; py <= maxY; py++)
            for (int px = minX; px <= maxX; px++)
            {
                if (px < 0 || px >= image.GetWidth() || py < 0 || py >= image.GetHeight()) continue;
                if (PointInTriangle(px, py, x1, y1, x2, y2, x3, y3))
                    image.SetPixel(px, py, color);
            }
    }

    private static bool PointInTriangle(int px, int py, int x1, int y1, int x2, int y2, int x3, int y3)
    {
        int d1 = (px - x2) * (y1 - y2) - (x1 - x2) * (py - y2);
        int d2 = (px - x3) * (y2 - y3) - (x2 - x3) * (py - y3);
        int d3 = (px - x1) * (y3 - y1) - (x3 - x1) * (py - y1);
        bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
        bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(hasNeg && hasPos);
    }

    private static void DrawLine(Image image, int x1, int y1, int x2, int y2, Color color, int thickness = 1)
    {
        int dx = System.Math.Abs(x2 - x1);
        int dy = System.Math.Abs(y2 - y1);
        int sx = x1 < x2 ? 1 : -1;
        int sy = y1 < y2 ? 1 : -1;
        int err = dx - dy;
        int x = x1, y = y1;
        while (true)
        {
            if (thickness > 1)
                FillCircle(image, x, y, thickness / 2, color);
            else if (x >= 0 && x < image.GetWidth() && y >= 0 && y < image.GetHeight())
                image.SetPixel(x, y, color);
            if (x == x2 && y == y2) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }
        }
    }
}

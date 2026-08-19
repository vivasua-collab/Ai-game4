#nullable enable
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.UI;

/// <summary>
/// Inventory window — line-model inventory (list of items, not grid).
/// Opens with B key. Shows: item name, quantity, weight, total weight/volume.
///
/// Design per docs_v2/06_player/INVENTORY_SYSTEM.md:
/// - Line model: list of items + maxWeight + maxVolume
/// - No grid (Tetris-style) — items are indexed by position in list
/// - Rarity colors for item borders
///
/// This is a simple v1 implementation:
/// - Read-only display (no drag&drop yet)
/// - Shows items from IInventoryService
/// - Closes with B or Esc
/// </summary>
public partial class InventoryWindow : Control
{
    [Inject] private IInventoryService InventoryService { get; set; } = null!;

    private bool _isVisible;
    private Panel _panel = null!;
    private VBoxContainer _itemList = null!;
    private Label _headerLabel = null!;
    private Label _weightLabel = null!;
    private ScrollContainer _scroll = null!;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
        {
            ContainerAdapter.InjectProperties(this, container);
        }

        BuildUI();
        _isVisible = false;
        Visible = false;
        GD.Print("[Inventory] Ready");
    }

    private void BuildUI()
    {
        // Apply parchment theme.
        Theme = ParchmentTheme.Create();

        // Full-screen overlay (clickable background to close).
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        // Semi-transparent dark background.
        var bg = new ColorRect
        {
            Name = "Background",
            Color = new Color(0.05f, 0.03f, 0.02f, 0.7f),
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Stop;
        AddChild(bg);

        // Main panel (centered, 600×500).
        _panel = new Panel
        {
            Name = "InventoryPanel",
        };
        _panel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        _panel.OffsetLeft = -300;
        _panel.OffsetRight = 300;
        _panel.OffsetTop = -250;
        _panel.OffsetBottom = 250;
        _panel.MouseFilter = MouseFilterEnum.Stop;
        AddChild(_panel);

        // Container for content.
        var content = new VBoxContainer();
        content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        content.OffsetLeft = 16;
        content.OffsetRight = -16;
        content.OffsetTop = 16;
        content.OffsetBottom = -16;
        content.AddThemeConstantOverride("separation", 8);
        _panel.AddChild(content);

        // Header.
        _headerLabel = new Label
        {
            Text = "◆ Инвентарь ◆",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _headerLabel.AddThemeFontSizeOverride("font_size", 24);
        _headerLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkBlack);
        content.AddChild(_headerLabel);

        // Weight/volume summary.
        _weightLabel = new Label
        {
            Text = "Вес: 0.0 / 10.0 кг | Объём: 0.0 / 20.0",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _weightLabel.AddThemeFontSizeOverride("font_size", 14);
        _weightLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        content.AddChild(_weightLabel);

        // Separator.
        var sep = new HSeparator();
        content.AddChild(sep);

        // Scrollable item list.
        _scroll = new ScrollContainer
        {
            Name = "ItemScroll",
            CustomMinimumSize = new Vector2(560, 350),
        };
        content.AddChild(_scroll);

        _itemList = new VBoxContainer
        {
            Name = "ItemList",
        };
        _itemList.AddThemeConstantOverride("separation", 4);
        _scroll.AddChild(_itemList);

        // Footer hint.
        var footer = new Label
        {
            Text = "B или Esc — закрыть",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        footer.AddThemeFontSizeOverride("font_size", 13);
        footer.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        content.AddChild(footer);
    }

    // Note: B and Esc handling is done by GameWorldController.HandleStickyInput
    // to avoid double-toggle (both _Input and HandleStickyInput fire on same frame).
    // GameWorldController calls Toggle() directly.

    /// <summary>Toggle inventory visibility.</summary>
    public void Toggle()
    {
        _isVisible = !_isVisible;
        Visible = _isVisible;
        if (_isVisible)
        {
            RefreshItems();
            GD.Print("[Inventory] Opened");
        }
        else
        {
            GD.Print("[Inventory] Closed");
        }
    }

    /// <summary>Refresh item list from IInventoryService.</summary>
    private void RefreshItems()
    {
        // Clear existing items.
        foreach (var child in _itemList.GetChildren())
        {
            child.QueueFree();
        }

        // Get items from service.
        var slots = InventoryService?.GetAllSlots();
        if (slots == null || slots.Count == 0)
        {
            var empty = new Label
            {
                Text = "◇ Инвентарь пуст",
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            empty.AddThemeFontSizeOverride("font_size", 16);
            empty.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
            _itemList.AddChild(empty);
        }
        else
        {
            foreach (var slot in slots)
            {
                var row = CreateItemRow(slot);
                _itemList.AddChild(row);
            }
        }

        // Update weight/volume.
        float curWeight = InventoryService?.GetCurrentWeight() ?? 0f;
        float maxWeight = InventoryService?.GetEffectiveMaxWeight() ?? 10f;
        float curVol = InventoryService?.GetCurrentVolume() ?? 0f;
        float maxVol = InventoryService?.GetEffectiveMaxVolume() ?? 20f;
        _weightLabel.Text = $"Вес: {curWeight:F1} / {maxWeight:F1} кг | Объём: {curVol:F1} / {maxVol:F1}";
    }

    /// <summary>Create a single item row (name + quantity + weight).</summary>
    private HBoxContainer CreateItemRow(InventorySlot slot)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);

        // Rarity color for border.
        var rarityColor = slot.Rarity switch
        {
            ItemRarity.Uncommon  => ParchmentTheme.AccentGreen,
            ItemRarity.Rare      => new Color(0.3f, 0.5f, 0.9f),
            ItemRarity.Epic      => ParchmentTheme.AccentPurple,
            ItemRarity.Legendary => ParchmentTheme.AccentGold,
            ItemRarity.Mythic    => ParchmentTheme.AccentRed,
            _                    => ParchmentTheme.InkFaded,
        };

        // Rarity indicator (colored square).
        var indicator = new ColorRect
        {
            Color = rarityColor,
            CustomMinimumSize = new Vector2(8, 24),
        };
        row.AddChild(indicator);

        // Item name.
        var nameLabel = new Label
        {
            Text = slot.ItemId,
            CustomMinimumSize = new Vector2(200, 24),
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 15);
        nameLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkBlack);
        row.AddChild(nameLabel);

        // Quantity.
        var qtyLabel = new Label
        {
            Text = $"×{slot.Count}",
            CustomMinimumSize = new Vector2(60, 24),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        qtyLabel.AddThemeFontSizeOverride("font_size", 15);
        qtyLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        row.AddChild(qtyLabel);

        // Weight.
        var weightLabel = new Label
        {
            Text = $"{slot.Weight:F1} кг",
            CustomMinimumSize = new Vector2(80, 24),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        weightLabel.AddThemeFontSizeOverride("font_size", 13);
        weightLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        row.AddChild(weightLabel);

        return row;
    }
}

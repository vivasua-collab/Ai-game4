#nullable enable
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.UI;

/// <summary>
/// Inventory window — line-model inventory + character doll panel.
/// Opens with B key.
///
/// Layout:
///   ┌─────────────────────────────────────────────┐
///   │              ◆ Инвентарь ◆                   │
///   │   Вес: 12.5 / 50.0 кг | Объём: 30 / 100     │
///   ├──────────────────────────┬──────────────────┤
///   │  Предметы (list, drag src)│  Кукла (drop tgt)│
///   │  ──────                   │  ────            │
///   │  ⚔ Железный меч-цзянь ×1  │  Голова: шлем    │
///   │  🛡 Стальной нагрудник ×1 │  Торс: нагрудник │
///   │  💊 Пилюля лечения ×5     │  ...             │
///   │                           │                  │
///   └──────────────────────────┴──────────────────┘
///   B или Esc — закрыть | ЛКМ на кукле — снять
///
/// Drag&drop:
///   - Drag equipment item row → drop on doll slot = equip
///   - Drag doll slot → drop on inventory list = unequip
///   - LMB click on occupied doll slot = quick unequip
///
/// Design per docs_v2/06_player/INVENTORY_SYSTEM.md.
/// </summary>
public partial class InventoryWindow : Control
{
    [Inject] private IInventoryService InventoryService { get; set; } = null!;
    [Inject] private IItemDatabaseService ItemDatabase { get; set; } = null!;

    private bool _isVisible;
    private Panel _panel = null!;
    private VBoxContainer _itemList = null!;
    private Label _headerLabel = null!;
    private Label _weightLabel = null!;
    private ScrollContainer _scroll = null!;
    private CharacterDollPanel _dollPanel = null!;
    private HBoxContainer _contentRow = null!;

    private static bool _itemsSeeded = false;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
        {
            ContainerAdapter.InjectProperties(this, container);
        }

        // Seed test items on first open (debug/test data).
        if (!_itemsSeeded)
        {
            TestItemSeeder.Seed(ItemDatabase, InventoryService);
            _itemsSeeded = true;
            GD.Print("[Inventory] Test items seeded");
        }

        BuildUI();
        _isVisible = false;
        Visible = false;
        GD.Print("[Inventory] Ready");
    }

    private void BuildUI()
    {
        Theme = ParchmentTheme.Create();

        // Full-screen overlay (clickable background to close on outside click).
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        // Semi-transparent dark background.
        var bg = new ColorRect
        {
            Name = "Background",
            Color = new Color(0.05f, 0.03f, 0.02f, 0.7f),
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Stop; // consume clicks on background (close + don't propagate to world)
        AddChild(bg);

        // Main panel (centered, 880×560 — wider to fit doll + inventory side by side).
        _panel = new Panel
        {
            Name = "InventoryPanel",
        };
        _panel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        _panel.OffsetLeft = -440;
        _panel.OffsetRight = 440;
        _panel.OffsetTop = -280;
        _panel.OffsetBottom = 280;
        _panel.MouseFilter = MouseFilterEnum.Stop;
        AddChild(_panel);

        // Outer VBox: header / content-row / footer.
        var outer = new VBoxContainer();
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        outer.OffsetLeft = 16;
        outer.OffsetRight = -16;
        outer.OffsetTop = 12;
        outer.OffsetBottom = -12;
        outer.AddThemeConstantOverride("separation", 8);
        _panel.AddChild(outer);

        // Header.
        _headerLabel = new Label
        {
            Text = "◆ Инвентарь ◆",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _headerLabel.AddThemeFontSizeOverride("font_size", 24);
        _headerLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkBlack);
        outer.AddChild(_headerLabel);

        // Weight/volume summary.
        _weightLabel = new Label
        {
            Text = "Вес: 0.0 / 50.0 кг | Объём: 0.0 / 100.0",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _weightLabel.AddThemeFontSizeOverride("font_size", 14);
        _weightLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        outer.AddChild(_weightLabel);

        var sep = new HSeparator();
        outer.AddChild(sep);

        // Content row: left = item list, right = doll panel.
        _contentRow = new HBoxContainer
        {
            Name = "ContentRow",
        };
        _contentRow.AddThemeConstantOverride("separation", 12);
        _contentRow.SizeFlagsVertical = SizeFlags.ExpandFill;
        outer.AddChild(_contentRow);

        // ── Left: inventory list (drag source) ──
        var leftWrap = new VBoxContainer
        {
            Name = "InventoryListWrap",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _contentRow.AddChild(leftWrap);

        var leftTitle = new Label
        {
            Text = "Предметы (тащи на куклу →)",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        leftTitle.AddThemeFontSizeOverride("font_size", 13);
        leftTitle.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        leftWrap.AddChild(leftTitle);

        _scroll = new ScrollContainer
        {
            Name = "ItemScroll",
            CustomMinimumSize = new Vector2(560, 440),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        leftWrap.AddChild(_scroll);

        _itemList = new VBoxContainer
        {
            Name = "ItemList",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _itemList.AddThemeConstantOverride("separation", 3);
        _scroll.AddChild(_itemList);

        // ── Right: character doll (drop target) ──
        _dollPanel = new CharacterDollPanel
        {
            Name = "DollPanel",
            CustomMinimumSize = new Vector2(260, 440),
        };
        _contentRow.AddChild(_dollPanel);

        // Footer hint.
        var footer = new Label
        {
            Text = "B или Esc — закрыть | ЛКМ на кукле — снять | Перетащи предмет на слот — надеть",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        footer.AddThemeFontSizeOverride("font_size", 12);
        footer.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        outer.AddChild(footer);

        // Background click → close (only if click is on bg, not panel).
        bg.GuiInput += OnBackgroundClick;
    }

    private void OnBackgroundClick(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            Toggle();
        }
    }

    // Note: B and Esc handling done by GameWorldController.HandleStickyInput.

    /// <summary>Toggle inventory visibility.</summary>
    public void Toggle()
    {
        _isVisible = !_isVisible;
        Visible = _isVisible;
        if (_isVisible)
        {
            RefreshItems();
            _dollPanel?.RefreshAll();
            GD.Print("[Inventory] Opened");
        }
        else
        {
            GD.Print("[Inventory] Closed");
        }
    }

    /// <summary>Refresh item list (called on open + after drag&drop).</summary>
    public void RefreshExternally()
    {
        if (_isVisible) RefreshItems();
    }

    /// <summary>Expose doll panel for double-click equip (InventoryItemRow uses this).</summary>
    public CharacterDollPanel? GetDollPanel() => _dollPanel;

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
        float maxWeight = InventoryService?.GetEffectiveMaxWeight() ?? 50f;
        float curVol = InventoryService?.GetCurrentVolume() ?? 0f;
        float maxVol = InventoryService?.GetEffectiveMaxVolume() ?? 100f;
        _weightLabel.Text = $"Вес: {curWeight:F1} / {maxWeight:F1} кг | Объём: {curVol:F1} / {maxVol:F1}";
    }

    /// <summary>Create a single draggable item row.</summary>
    private InventoryItemRow CreateItemRow(InventorySlot slot)
    {
        var row = new InventoryItemRow(slot, this, ItemDatabase);
        return row;
    }
}

/// <summary>
/// Single inventory item row — DRAG SOURCE.
/// Drag with LMB to a doll slot to equip. RMB to use (consumables).
/// </summary>
public partial class InventoryItemRow : HBoxContainer
{
    private readonly InventorySlot _slot;
    private readonly InventoryWindow _parent;
    private readonly IItemDatabaseService _itemDb;

    private ColorRect _rarityIndicator = null!;
    private Label _nameLabel = null!;
    private Label _qtyLabel = null!;
    private Label _weightLabel = null!;

    public InventoryItemRow(InventorySlot slot, InventoryWindow parent, IItemDatabaseService db)
    {
        _slot = slot;
        _parent = parent;
        _itemDb = db;
        Name = $"Item_{slot.ItemId}";
        MouseFilter = MouseFilterEnum.Stop;
    }

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 12);

        var rarityColor = CharacterDollPanel.GetRarityColor(_slot.Rarity);

        _rarityIndicator = new ColorRect
        {
            Color = rarityColor,
            CustomMinimumSize = new Vector2(6, 22),
        };
        AddChild(_rarityIndicator);

        // Resolve display name from database (fallback to itemId).
        string displayName = _slot.ItemId;
        if (_itemDb.TryGetItem(_slot.ItemId, out var itemData))
        {
            displayName = itemData.NameRu;
        }

        _nameLabel = new Label
        {
            Text = displayName,
            CustomMinimumSize = new Vector2(220, 22),
        };
        _nameLabel.AddThemeFontSizeOverride("font_size", 14);
        _nameLabel.AddThemeColorOverride("font_color", rarityColor);
        AddChild(_nameLabel);

        _qtyLabel = new Label
        {
            Text = $"×{_slot.Count}",
            CustomMinimumSize = new Vector2(50, 22),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        _qtyLabel.AddThemeFontSizeOverride("font_size", 14);
        _qtyLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        AddChild(_qtyLabel);

        _weightLabel = new Label
        {
            Text = $"{_slot.Weight:F1} кг",
            CustomMinimumSize = new Vector2(70, 22),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        _weightLabel.AddThemeFontSizeOverride("font_size", 12);
        _weightLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        AddChild(_weightLabel);
    }

    // === Drag source: provide drag data ===

    public override Variant _GetDragData(Vector2 atPosition)
    {
        // Only equipment is draggable (consumables cannot be equipped).
        if (!_itemDb.TryGetItem(_slot.ItemId, out var itemData))
            return new Variant();

        bool isEquipment = itemData.Category == ItemCategory.Weapon
                        || itemData.Category == ItemCategory.Armor
                        || itemData.Category == ItemCategory.Accessory;
        if (!isEquipment)
        {
            // Show feedback for consumables (not draggable to doll).
            SetDragPreview(BuildConsumablePreview(itemData.NameRu));
            return new Variant(); // empty = no drag
        }

        var dragData = CharacterDollPanel.CreateDragData(itemData, "inventory");
        SetDragPreview(CharacterDollPanel.BuildDragPreview(itemData.NameRu,
            CharacterDollPanel.GetRarityColor(itemData.Rarity)));
        return dragData;
    }

    private static Control BuildConsumablePreview(string name)
    {
        var preview = new Label
        {
            Text = $"💊 {name} — нельзя надеть",
        };
        preview.AddThemeFontSizeOverride("font_size", 13);
        preview.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        return preview;
    }

    // === Double-click: equip item to its designated slot ===

    private double _lastClickTime = -1;
    private const double DoubleClickInterval = 0.35; // seconds

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.Right)
            {
                // RMB: info log
                if (_itemDb.TryGetItem(_slot.ItemId, out var itemData))
                {
                    GD.Print($"[Inventory] RMB on {itemData.NameRu} (category={itemData.Category}, rarity={itemData.Rarity})");
                }
            }
            else if (mb.ButtonIndex == MouseButton.Left)
            {
                // LMB: detect double-click → equip
                double now = Time.GetTicksMsec() / 1000.0;
                if (now - _lastClickTime < DoubleClickInterval)
                {
                    // Double-click → equip
                    TryEquipFromInventory();
                    _lastClickTime = -1; // reset to prevent triple-click as double
                }
                else
                {
                    _lastClickTime = now;
                }
            }
        }
    }

    /// <summary>
    /// Equip this item to its designated equipment slot (double-click action).
    /// Resolves slot from EquipmentData.Slot, removes from inventory, equips.
    /// </summary>
    private void TryEquipFromInventory()
    {
        if (!_itemDb.TryGetItem(_slot.ItemId, out var itemData))
            return;

        if (itemData is not EquipmentData eq)
        {
            GD.Print($"[Inventory] Cannot equip {_slot.ItemId} — not equipment");
            return;
        }

        // Get the parent InventoryWindow to access equipment service via doll panel.
        var inventoryWindow = _parent;
        if (inventoryWindow == null) return;

        // Resolve slot: use item's designated slot.
        // For 1H weapons, prefer WeaponMain if empty, else WeaponOff.
        var targetSlot = eq.Slot;
        if (eq.Category == ItemCategory.Weapon && eq.HandType == WeaponHandType.OneHand)
        {
            // Try WeaponMain first, fall back to WeaponOff.
            // The doll panel's HandleDropOnSlot handles this flexibility,
            // but for double-click we pick the designated slot.
            targetSlot = EquipmentSlot.WeaponMain;
        }

        // Use the doll panel's equip logic by calling HandleDropOnSlot.
        var dollPanel = inventoryWindow.GetDollPanel();
        if (dollPanel == null)
        {
            GD.Print("[Inventory] Doll panel not found");
            return;
        }

        bool success = dollPanel.HandleDropOnSlot(targetSlot, itemData);
        if (success)
        {
            inventoryWindow.RefreshExternally();
            GD.Print($"[Inventory] Double-click equipped {eq.NameRu} → {targetSlot}");
        }
    }
}

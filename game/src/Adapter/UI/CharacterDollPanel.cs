#nullable enable
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.UI;

/// <summary>
/// Character doll panel — equipment slots panel.
///
/// Shows 7 visible equipment slots (Head, Torso, Belt, Legs, Feet,
/// WeaponMain, WeaponOff) + 4 hidden (Amulet, RingLeft1, Hands, Back) for v1.
///
/// Drag&drop:
/// - Drag equipment FROM inventory list → drop on doll slot = equip
/// - Drag equipment FROM doll slot → drop on inventory = unequip
/// - Click on occupied slot = unequip (quick action)
///
/// Backend: IEquipmentService (TryEquip/TryUnequip/GetEquipped).
/// Events: subscribes to EquipmentChangedEvent to refresh single slot.
///
/// Design per docs_v2/06_player/INVENTORY_SYSTEM.md §4 (Body Doll).
/// Ported from Ai-game3 BodyDollPanel.cs (Unity uGUI → Godot Control).
/// </summary>
public partial class CharacterDollPanel : Control
{
    [Inject] private IEquipmentService EquipmentService { get; set; } = null!;
    [Inject] private IItemDatabaseService ItemDatabase { get; set; } = null!;
    [Inject] private IInventoryService InventoryService { get; set; } = null!;

    private VBoxContainer _slotList = null!;
    private Label _titleLabel = null!;
    private Label _statsLabel = null!;

    // Slot UIs indexed by EquipmentSlot enum.
    private readonly Dictionary<EquipmentSlot, DollSlotRow> _slotRows = new();

    // Visible slots (per INVENTORY_SYSTEM.md §4.1 — 7 visible).
    // Hidden slots added for v1 test coverage (amulet/ring/hands/back).
    private static readonly EquipmentSlot[] VisibleSlots =
    {
        EquipmentSlot.Head,
        EquipmentSlot.Torso,
        EquipmentSlot.Belt,
        EquipmentSlot.Legs,
        EquipmentSlot.Feet,
        EquipmentSlot.WeaponMain,
        EquipmentSlot.WeaponOff,
    };

    private static readonly EquipmentSlot[] HiddenSlots =
    {
        EquipmentSlot.Amulet,
        EquipmentSlot.RingLeft1,
        EquipmentSlot.Hands,
        EquipmentSlot.Back,
    };

    // Slot labels (Russian, per Ai-game3 EquipmentSlotUI.SlotLabels).
    private static readonly Dictionary<EquipmentSlot, string> SlotLabels = new()
    {
        { EquipmentSlot.Head,       "Голова" },
        { EquipmentSlot.Torso,      "Торс" },
        { EquipmentSlot.Belt,       "Пояс" },
        { EquipmentSlot.Legs,       "Ноги" },
        { EquipmentSlot.Feet,       "Ступни" },
        { EquipmentSlot.WeaponMain, "Осн. рука" },
        { EquipmentSlot.WeaponOff,  "Доп. рука" },
        { EquipmentSlot.Amulet,     "Амулет" },
        { EquipmentSlot.RingLeft1,  "Кольцо Л1" },
        { EquipmentSlot.Hands,      "Руки" },
        { EquipmentSlot.Back,       "Спина" },
    };

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
        {
            ContainerAdapter.InjectProperties(this, container);
        }

        BuildUI();
        RefreshAll();
        GD.Print("[CharacterDoll] Ready");
    }

    private void BuildUI()
    {
        Theme = ParchmentTheme.Create();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var content = new VBoxContainer();
        content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        content.OffsetLeft = 8;
        content.OffsetRight = -8;
        content.OffsetTop = 8;
        content.OffsetBottom = -8;
        content.AddThemeConstantOverride("separation", 6);
        AddChild(content);

        // Title.
        _titleLabel = new Label
        {
            Text = "◆ Кукла ◆",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", 20);
        _titleLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkBlack);
        content.AddChild(_titleLabel);

        // Stats summary (armor/damage).
        _statsLabel = new Label
        {
            Text = "Броня: 0 | Урон: 0",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _statsLabel.AddThemeFontSizeOverride("font_size", 13);
        _statsLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        content.AddChild(_statsLabel);

        var sep = new HSeparator();
        content.AddChild(sep);

        // Slot rows in a scroll container (in case all 11 don't fit).
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(240, 380),
        };
        content.AddChild(scroll);

        _slotList = new VBoxContainer
        {
            Name = "SlotList",
        };
        _slotList.AddThemeConstantOverride("separation", 3);
        scroll.AddChild(_slotList);

        // Build visible slot rows.
        foreach (var slot in VisibleSlots)
        {
            var row = new DollSlotRow(slot, this);
            _slotRows[slot] = row;
            _slotList.AddChild(row);
        }

        // Separator + hidden slots section.
        var hiddenSep = new HSeparator();
        _slotList.AddChild(hiddenSep);

        var hiddenLabel = new Label
        {
            Text = "─ скрытые слоты ─",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        hiddenLabel.AddThemeFontSizeOverride("font_size", 12);
        hiddenLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        _slotList.AddChild(hiddenLabel);

        foreach (var slot in HiddenSlots)
        {
            var row = new DollSlotRow(slot, this);
            _slotRows[slot] = row;
            _slotList.AddChild(row);
        }
    }

    /// <summary>Refresh all slots (called on panel open).</summary>
    public void RefreshAll()
    {
        foreach (var kvp in _slotRows)
        {
            kvp.Value.Refresh(EquipmentService, ItemDatabase);
        }
        UpdateStats();
    }

    /// <summary>Refresh a single slot (called on EquipmentChangedEvent).</summary>
    public void RefreshSlot(EquipmentSlot slot)
    {
        if (_slotRows.TryGetValue(slot, out var row))
        {
            row.Refresh(EquipmentService, ItemDatabase);
        }
        UpdateStats();
    }

    private void UpdateStats()
    {
        float armor = EquipmentService?.GetTotalArmor() ?? 0f;
        float damage = EquipmentService?.GetTotalDamage() ?? 0f;
        string handType = EquipmentService?.GetWeaponHandType().ToString() ?? "None";
        _statsLabel.Text = $"Броня: {armor:F0} | Урон: {damage:F0} | Хват: {handType}";
    }

    // === Drag&drop handlers (called by DollSlotRow) ===

    /// <summary>
    /// Equip an item dragged from inventory onto a doll slot.
    /// Validates: item must be EquipmentData, slot must match item.Slot.
    /// </summary>
    internal bool HandleDropOnSlot(EquipmentSlot slot, ItemData item)
    {
        if (item is not EquipmentData eq)
        {
            GD.Print($"[CharacterDoll] Drop rejected: {item.ItemId} is not equipment");
            return false;
        }

        // Slot match check (weapon can go to WeaponMain or WeaponOff).
        if (eq.Slot != slot)
        {
            // Allow 1H weapon in either hand.
            bool weaponFlexible = eq.Category == ItemCategory.Weapon &&
                                  eq.HandType == WeaponHandType.OneHand &&
                                  (slot == EquipmentSlot.WeaponMain || slot == EquipmentSlot.WeaponOff);
            if (!weaponFlexible)
            {
                GD.Print($"[CharacterDoll] Drop rejected: {eq.NameRu} belongs to {eq.Slot}, not {slot}");
                return false;
            }
        }

        // Remove from inventory first.
        if (!InventoryService.TryRemoveItem(eq.ItemId, 1))
        {
            GD.Print($"[CharacterDoll] Cannot remove {eq.ItemId} from inventory");
            return false;
        }

        // If slot occupied — return old item to inventory.
        var oldItem = EquipmentService.GetEquipped(slot);
        if (oldItem != null)
        {
            InventoryService.TryAddItem(oldItem, 1);
        }

        // 2H weapon: also unequip off-hand if equipping 2H in main.
        if (eq.HandType == WeaponHandType.TwoHand && slot == EquipmentSlot.WeaponMain)
        {
            var offItem = EquipmentService.GetEquipped(EquipmentSlot.WeaponOff);
            if (offItem != null)
            {
                InventoryService.TryAddItem(offItem, 1);
                EquipmentService.TryUnequip(EquipmentSlot.WeaponOff, out _);
                RefreshSlot(EquipmentSlot.WeaponOff);
            }
        }

        bool success = EquipmentService.TryEquip(slot, eq);
        if (!success)
        {
            // Rollback: put item back in inventory.
            InventoryService.TryAddItem(eq, 1);
            GD.Print($"[CharacterDoll] Equip failed for {eq.NameRu}");
            return false;
        }

        GD.Print($"[CharacterDoll] Equipped {eq.NameRu} → {slot}");
        RefreshSlot(slot);
        return true;
    }

    /// <summary>
    /// Unequip: drag from doll slot back to inventory (or click).
    /// Returns the dragged item data for Godot drag preview.
    /// </summary>
    internal bool HandleUnequip(EquipmentSlot slot)
    {
        var item = EquipmentService.GetEquipped(slot);
        if (item == null) return false;

        if (!InventoryService.TryAddItem(item, 1))
        {
            GD.Print($"[CharacterDoll] Cannot add {item.NameRu} back to inventory (full?)");
            return false;
        }

        EquipmentService.TryUnequip(slot, out _);
        GD.Print($"[CharacterDoll] Unequipped {item.NameRu} from {slot}");
        RefreshSlot(slot);
        return true;
    }

    /// <summary>Get item currently equipped in slot (for drag source).</summary>
    internal EquipmentData? GetEquippedItem(EquipmentSlot slot)
    {
        return EquipmentService?.GetEquipped(slot);
    }

    /// <summary>Static label lookup for slot.</summary>
    internal static string GetSlotLabel(EquipmentSlot slot)
    {
        return SlotLabels.TryGetValue(slot, out var label) ? label : slot.ToString();
    }

    /// <summary>Rarity color helper.</summary>
    internal static Color GetRarityColor(ItemRarity rarity)
    {
        return rarity switch
        {
            ItemRarity.Common    => ParchmentTheme.RarityCommon,
            ItemRarity.Uncommon  => ParchmentTheme.RarityUncommon,
            ItemRarity.Rare      => ParchmentTheme.RarityRare,
            ItemRarity.Epic      => ParchmentTheme.RarityEpic,
            ItemRarity.Legendary => ParchmentTheme.RarityLegendary,
            ItemRarity.Mythic    => ParchmentTheme.RarityMythic,
            _                    => ParchmentTheme.InkFaded,
        };
    }

    // === Custom Variant wrapper for drag data ===

    /// <summary>
    /// Custom Godot Variant to carry dragged item data between controls.
    /// Stored as a Dictionary for Godot's _GetDragData() return.
    /// </summary>
    internal static Godot.Collections.Dictionary CreateDragData(ItemData item, string source)
    {
        var dict = new Godot.Collections.Dictionary
        {
            { "item_id", item.ItemId },
            { "source", source },
            { "category", (int)item.Category },
            { "rarity", (int)item.Rarity },
            { "name", item.NameRu },
        };
        return dict;
    }

    internal static bool TryParseDragData(Variant data, out string itemId, out string source)
    {
        itemId = string.Empty;
        source = string.Empty;
        if (data.VariantType != Variant.Type.Dictionary) return false;
        var dict = data.As<Godot.Collections.Dictionary>();
        itemId = dict.ContainsKey("item_id") ? dict["item_id"].AsString() : string.Empty;
        source = dict.ContainsKey("source") ? dict["source"].AsString() : string.Empty;
        return !string.IsNullOrEmpty(itemId);
    }

    /// <summary>Build a drag preview label (shown next to cursor while dragging).</summary>
    internal static Control BuildDragPreview(string name, Color rarityColor)
    {
        var preview = new Label
        {
            Text = $"⚔ {name}",
        };
        preview.AddThemeFontSizeOverride("font_size", 14);
        preview.AddThemeColorOverride("font_color", rarityColor);
        // Shadow offset in Godot 4 is set via theme override on "font_shadow_offset"
        // but the Label convenience method is not exposed; skip for simplicity.
        return preview;
    }

    /// <summary>Expose ItemDatabase for DollSlotRow (internal accessor).</summary>
    internal IItemDatabaseService GetItemDatabase() => ItemDatabase;
}

/// <summary>
/// Single equipment slot row in the doll panel.
/// Shows: slot label + item name (or "—пусто—") + rarity indicator.
/// Supports: drag from inventory (drop target), click to unequip, drag out to unequip.
/// </summary>
public partial class DollSlotRow : HBoxContainer
{
    private readonly EquipmentSlot _slot;
    private readonly CharacterDollPanel _parent;

    private ColorRect _rarityIndicator = null!;
    private Label _slotLabel = null!;
    private Label _itemLabel = null!;

    public DollSlotRow(EquipmentSlot slot, CharacterDollPanel parent)
    {
        _slot = slot;
        _parent = parent;
        Name = $"Slot_{slot}";
        MouseFilter = MouseFilterEnum.Pass;
    }

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 6);

        // Rarity color indicator (8×24, dim if empty).
        _rarityIndicator = new ColorRect
        {
            Color = ParchmentTheme.InkFaded,
            CustomMinimumSize = new Vector2(6, 22),
        };
        AddChild(_rarityIndicator);

        // Slot label (fixed width).
        _slotLabel = new Label
        {
            Text = CharacterDollPanel.GetSlotLabel(_slot),
            CustomMinimumSize = new Vector2(90, 22),
        };
        _slotLabel.AddThemeFontSizeOverride("font_size", 13);
        _slotLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        AddChild(_slotLabel);

        // Item name label (fill remaining space).
        _itemLabel = new Label
        {
            Text = "—пусто—",
            CustomMinimumSize = new Vector2(120, 22),
            MouseFilter = MouseFilterEnum.Stop, // capture for clicks
        };
        _itemLabel.AddThemeFontSizeOverride("font_size", 13);
        _itemLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        AddChild(_itemLabel);
    }

    /// <summary>Refresh slot display from equipment service.</summary>
    public void Refresh(IEquipmentService equip, IItemDatabaseService db)
    {
        var item = equip?.GetEquipped(_slot);
        if (item == null)
        {
            _itemLabel.Text = "—пусто—";
            _itemLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
            _rarityIndicator.Color = new Color(0.3f, 0.25f, 0.2f, 0.4f);
        }
        else
        {
            _itemLabel.Text = item.NameRu;
            var rarityColor = CharacterDollPanel.GetRarityColor(item.Rarity);
            _itemLabel.AddThemeColorOverride("font_color", rarityColor);
            _rarityIndicator.Color = rarityColor;
        }
    }

    // === Drag&drop: this row is a DROP TARGET ===

    public override Variant _GetDragData(Vector2 atPosition)
    {
        // Dragging FROM this slot (unequip by drag).
        var item = _parent.GetEquippedItem(_slot);
        if (item == null) return new Variant();

        var dragData = CharacterDollPanel.CreateDragData(item, "doll:" + _slot);
        SetDragPreview(CharacterDollPanel.BuildDragPreview(item.NameRu,
            CharacterDollPanel.GetRarityColor(item.Rarity)));
        return dragData;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        // Accept drops from inventory.
        if (!CharacterDollPanel.TryParseDragData(data, out _, out var source))
            return false;
        return source == "inventory";
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (!CharacterDollPanel.TryParseDragData(data, out var itemId, out _))
            return;

        // Resolve item from database.
        if (!_parent.GetItemDatabase().TryGetItem(itemId, out var item))
        {
            GD.Print($"[DollSlot] Unknown item id: {itemId}");
            return;
        }

        _parent.HandleDropOnSlot(_slot, item);
        // Refresh parent inventory window (so the removed item disappears from list).
        var inventoryWindow = _parent.GetParent()?.GetParent() as InventoryWindow;
        inventoryWindow?.RefreshExternally();
    }

    // === Click to unequip (quick action) ===

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            // LMB click on occupied slot → unequip.
            var item = _parent.GetEquippedItem(_slot);
            if (item != null)
            {
                _parent.HandleUnequip(_slot);
                var inventoryWindow = _parent.GetParent()?.GetParent() as InventoryWindow;
                inventoryWindow?.RefreshExternally();
            }
        }
        else if (@event is InputEventMouseButton mbR && mbR.Pressed && mbR.ButtonIndex == MouseButton.Right)
        {
            // RMB click → print item info (debug).
            var item = _parent.GetEquippedItem(_slot);
            if (item != null)
            {
                GD.Print($"[DollSlot] {_slot}: {item.NameRu} (rarity={item.Rarity}, slot={item.Slot})");
            }
        }
    }
}

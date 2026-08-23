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
    [Inject] private IGroundItemService GroundItems { get; set; } = null!;
    [Inject] private IEquipmentService EquipmentService { get; set; } = null!;
    [Inject] private IPlayerService PlayerService { get; set; } = null!;
    [Inject] private IEquipmentGenerator EquipmentGenerator { get; set; } = null!;
    [Inject] private IQiService QiService { get; set; } = null!;
    [Inject] private IBodyService BodyService { get; set; } = null!;
    [Inject] private CultivationGame.Core.Events.IPublisher<CultivationGame.Core.Messaging.Contracts.ToastShownEvent> ToastPub { get; set; } = null!;

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

        // Generate starting equipment via EquipmentGenerator (replaces hardcoded TestItemSeeder).
        // Generates: 3 weapons + 3 armor + 2 accessories + 4 consumables + 4 materials.
#if DEBUG
        if (!_itemsSeeded)
        {
            SeedGeneratedItems();
            _itemsSeeded = true;
            GD.Print("[Inventory] Generated items seeded via EquipmentGenerator");
        }
#endif

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

        // Trash/basket zone — drag item here to drop on ground.
        var trashZone = new TrashDropZone
        {
            Name = "TrashZone",
            CustomMinimumSize = new Vector2(260, 50),
        };
        _contentRow.AddChild(trashZone);

        // Belt quick-slot strip (2026-08-22): visible only with an equipped
        // belt; accepts consumable drops from the item list.
        var beltRow = new BeltSlotRow
        {
            Name = "BeltSlotRow",
            CustomMinimumSize = new Vector2(0, 72),
        };
        outer.AddChild(beltRow);

        // Footer hint.
        var footer = new Label
        {
            Text = "B/Esc — закрыть | Dbl-click — надеть | Перетащи на 🗑 — выбросить",
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

    /// <summary>
    /// Generate starting items via EquipmentGenerator (replaces TestItemSeeder).
    /// Generates diverse equipment + materials + consumables.
    /// </summary>
    private void SeedGeneratedItems()
    {
        long seed = 1000;

        // === Equipment (via EquipmentGenerator "Matryoshka") ===
        // 4 weapons (varied subtypes).
        for (int i = 0; i < 4; i++)
        {
            var weapon = EquipmentGenerator.GenerateWeapon(level: 1 + i, seed: seed + i);
            if (weapon != null)
            {
                ItemDatabase.Register(weapon);
                InventoryService.TryAddItem(weapon, 1);
            }
        }

        // 4 armor pieces (varied slots).
        for (int i = 0; i < 4; i++)
        {
            var armor = EquipmentGenerator.GenerateArmor(level: 1 + i, seed: seed + 100 + i);
            if (armor != null)
            {
                ItemDatabase.Register(armor);
                InventoryService.TryAddItem(armor, 1);
            }
        }

        // 2 random equipment (could be weapon or armor).
        for (int i = 0; i < 2; i++)
        {
            var eq = EquipmentGenerator.GenerateRandom(level: 2, seed: seed + 200 + i);
            if (eq != null)
            {
                ItemDatabase.Register(eq);
                InventoryService.TryAddItem(eq, 1);
            }
        }

        // === Materials (for crafting + harvest resolution) ===
        var materials = new[]
        {
            ("material_wood", "Древесина", 0.5f, 1.0f, ItemRarity.Common, 100),
            ("material_stone", "Камень", 1.0f, 1.0f, ItemRarity.Common, 100),
            ("material_iron_ore", "Железная руда", 1.5f, 1.0f, ItemRarity.Uncommon, 50),
            ("material_fiber", "Растительное волокно", 0.05f, 0.2f, ItemRarity.Common, 100),
        };

        foreach (var (id, name, weight, volume, rarity, maxStack) in materials)
        {
            var item = new ItemData
            {
                ItemId = id,
                NameRu = name,
                NameEn = name,
                Description = "Материал",
                Category = ItemCategory.Material,
                ItemType = "Material",
                Rarity = rarity,
                Stackable = true,
                MaxStack = maxStack,
                Weight = weight,
                Volume = volume,
                Value = 5,
                HasDurability = false,
            };
            ItemDatabase.Register(item);
            InventoryService.TryAddItem(item, 5);
        }

        // === Consumables ===
        var consumables = new[]
        {
            ("consumable_berry", "Ягоды", 0.05f, 0.1f, ItemRarity.Common, 50, "heal", 5),
            ("consumable_herb", "Лекарственная трава", 0.03f, 0.1f, ItemRarity.Uncommon, 50, "material", 0),
            ("con_pill_healing", "Пилюля лечения", 0.05f, 0.1f, ItemRarity.Common, 20, "heal", 30),
            ("con_pill_qi", "Пилюля Ци", 0.05f, 0.1f, ItemRarity.Uncommon, 20, "qi_restore", 50),
        };

        foreach (var (id, name, weight, volume, rarity, maxStack, effect, value) in consumables)
        {
            var item = new ItemData
            {
                ItemId = id,
                NameRu = name,
                NameEn = name,
                Description = "Расходник",
                Category = ItemCategory.Consumable,
                ItemType = "Consumable",
                Rarity = rarity,
                Stackable = true,
                MaxStack = maxStack,
                Weight = weight,
                Volume = volume,
                Value = value,
                HasDurability = false,
            };
            ItemDatabase.Register(item);
            InventoryService.TryAddItem(item, 5);
        }

        // === Этап 7 внедрения ЦИ: камни Ци (GENERATORS_SYSTEM.md §10) ===
        // Регистрируем все 10 канонических камней (5 размеров × calm/chaotic)
        // в БД предметов. Игроку выдаём 3 стартовых камня (calm: dust, pebble, shard).
        QiStoneSeeder.Seed(ItemDatabase);
        if (ItemDatabase.TryGetItem("qistone_dust_calm", out var qDust))
            InventoryService.TryAddItem(qDust, 3);
        if (ItemDatabase.TryGetItem("qistone_pebble_calm", out var qPebble))
            InventoryService.TryAddItem(qPebble, 2);
        if (ItemDatabase.TryGetItem("qistone_shard_calm", out var qShard))
            InventoryService.TryAddItem(qShard, 1);
        // Один хаотичный камень — для теста риска.
        if (ItemDatabase.TryGetItem("qistone_dust_chaotic", out var qChaotic))
            InventoryService.TryAddItem(qChaotic, 1);
    }

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

    /// <summary>
    /// Этап 7 внедрения ЦИ: использовать камень Ци (RMB в инвентаре → Use).
    /// v1 — мгновенное поглощение всего Ци камня:
    ///   • +QiAmount к CurrentQi игрока (IQiService.AddQi).
    ///   • chaotic: 10% шанс −10% MaxHP (опасность хаотичной Ци, §10.2).
    ///   • камень расходуется (1 шт. снимается с инвентаря).
    /// </summary>
    public bool TryUseQiStone(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return false;
        if (!ItemDatabase.TryGetItem(itemId, out var itemData)) return false;
        if (itemData is not QiStoneData stone) return false;

        int count = InventoryService.GetItemCount(itemId);
        if (count <= 0)
        {
            PublishToast("Нет камня для использования");
            return false;
        }

        long before = QiService?.CurrentQi ?? 0;

        // Снять 1 шт. с инвентаря.
        if (!InventoryService.TryRemoveItem(itemId, 1))
        {
            PublishToast("Не удалось использовать камень");
            return false;
        }

        // Поглотить Ци (мгновенно для v1).
        QiService?.AddQi(stone.QiAmount);

        long after = QiService?.CurrentQi ?? 0;
        long gained = after - before;

        // Хаотичная Ци: 10% шанс −10% MaxHP (риск по канону §10.2).
        bool tookDamage = false;
        if (stone.IsChaotic)
        {
            var rng = new System.Random((int)System.DateTime.UtcNow.Ticks);
            double roll = rng.NextDouble();
            if (roll < 0.10) // 10% риск
            {
                ApplyChaoticDamage();
                tookDamage = true;
            }
        }

        // Формирование отзыва.
        string stoneName = stone.NameRu;
        if (tookDamage)
        {
            PublishToast($"💥 {stoneName}: +{gained} Ци, но хаотичная Ци ранила вас! (−10% HP)");
            GD.Print($"[Inventory] Used Qi stone {itemId}: +{gained} Qi, chaotic damage applied");
        }
        else if (stone.IsChaotic)
        {
            PublishToast($"⚡ {stoneName}: +{gained} Ци (хаос сдержан — повезло)");
            GD.Print($"[Inventory] Used chaotic Qi stone {itemId}: +{gained} Qi, no damage");
        }
        else
        {
            PublishToast($"✦ {stoneName}: +{gained} Ци");
            GD.Print($"[Inventory] Used Qi stone {itemId}: +{gained} Qi");
        }

        RefreshExternally();
        return true;
    }

    /// <summary>
    /// Применить урон хаотичной Ци: −10% от максимального HP игрока.
    /// HP = сумма MaxRedHP по частям тела (Q4). Урон наносится в торс
    /// (витальная часть) — представляет нагрузку на культивационное ядро.
    /// </summary>
    private void ApplyChaoticDamage()
    {
        if (BodyService == null) return;
        int maxHp = 0;
        var parts = BodyService.GetAllParts();
        if (parts == null) return;
        foreach (var p in parts) maxHp += p.MaxRedHP;
        if (maxHp <= 0) return;

        int damage = (int)System.Math.Max(1, maxHp * 0.10f);
        BodyService.ApplyDamage(BodyPartType.Torso, damage);
        GD.Print($"[Inventory] Chaotic Qi damage: -{damage} HP (10% of {maxHp})");
    }

    /// <summary>Опубликовать toast (показывается GameWorldController).</summary>
    private void PublishToast(string message)
    {
        GD.Print($"[Inventory] {message}");
        ToastPub?.Publish(new CultivationGame.Core.Messaging.Contracts.ToastShownEvent(message, 2.5f));
    }

    /// <summary>
    /// Drop an item from inventory onto the ground near the player.
    /// Called by TrashDropZone when item is dragged to trash basket.
    /// Removes ALL count of the item from inventory, drops as ground item.
    /// </summary>
    public void DropItemOnGround(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return;

        // Get current count of this item in inventory.
        int count = InventoryService.GetItemCount(itemId);
        if (count <= 0)
        {
            GD.Print($"[Inventory] Cannot drop {itemId} — not in inventory");
            return;
        }

        // Remove from inventory.
        if (!InventoryService.TryRemoveItem(itemId, count))
        {
            GD.Print($"[Inventory] Failed to remove {itemId}×{count} from inventory");
            return;
        }

        // Get player position (tile → pixel).
        var playerPos = PlayerService.Position;
        float pixelX = playerPos.X * GameConstants.TILE_PIXELS + GameConstants.TILE_PIXELS / 2f;
        float pixelY = playerPos.Y * GameConstants.TILE_PIXELS + GameConstants.TILE_PIXELS / 2f;

        // Small random offset.
        var rng = new System.Random((int)System.DateTime.UtcNow.Ticks);
        float offsetX = (float)(rng.NextDouble() - 0.5) * 30f;
        float offsetY = (float)(rng.NextDouble() - 0.5) * 30f;

        // Drop on ground.
        long dropId = GroundItems.DropItem(itemId, count, pixelX + offsetX, pixelY + offsetY);

        // Resolve display name.
        string displayName = itemId;
        if (ItemDatabase.TryGetItem(itemId, out var itemData))
            displayName = itemData.NameRu;

        GD.Print($"[Inventory] Dropped {displayName}×{count} on ground (dropId={dropId})");
        RefreshExternally();
    }

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

        // Update weight/volume — show GLOBAL weight (inventory + equipment).
        float invWeight = InventoryService?.GetCurrentWeight() ?? 0f;
        float equipWeight = EquipmentService?.GetTotalWeight() ?? 0f;
        float curWeight = invWeight + equipWeight;  // global weight
        float maxWeight = InventoryService?.GetEffectiveMaxWeight() ?? 50f;
        float curVol = InventoryService?.GetCurrentVolume() ?? 0f;
        float maxVol = InventoryService?.GetEffectiveMaxVolume() ?? 100f;
        bool isOverweight = curWeight > maxWeight;
        bool isVolumeFull = curVol >= maxVol;

        string weightStatus = isOverweight ? " ⚠ ПЕРЕВЕС" : "";
        string volStatus = isVolumeFull ? " ⚠ ПОЛНО" : "";
        _weightLabel.Text = $"Вес: {curWeight:F1} / {maxWeight:F1} кг{weightStatus} (ргкз: {invWeight:F1}+экп: {equipWeight:F1}) | Объём: {curVol:F1} / {maxVol:F1}{volStatus}";

        // Color: red if overweight or volume full, gold if near limit, else faded.
        Color weightColor;
        if (isOverweight || isVolumeFull)
            weightColor = ParchmentTheme.AccentRed;
        else if (curWeight > maxWeight * 0.8f || curVol > maxVol * 0.8f)
            weightColor = ParchmentTheme.AccentGold;
        else
            weightColor = ParchmentTheme.InkFaded;
        _weightLabel.AddThemeColorOverride("font_color", weightColor);
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
            Text = GetItemWeightVolumeText(),
            CustomMinimumSize = new Vector2(120, 22),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        _weightLabel.AddThemeFontSizeOverride("font_size", 12);
        _weightLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        AddChild(_weightLabel);
    }

    /// <summary>
    /// Get weight + volume text for this item row.
    /// Reads from ItemDatabase (InventorySlot.Weight may be 0 if created
    /// via the category/rarity constructor).
    /// </summary>
    private string GetItemWeightVolumeText()
    {
        float weight = _slot.Weight;
        float volume = _slot.Volume;

        // If slot weight is 0, try to resolve from ItemDatabase.
        if (weight <= 0 && _itemDb != null && _itemDb.TryGetItem(_slot.ItemId, out var item))
        {
            weight = item.Weight * _slot.Count;
            volume = item.Volume * _slot.Count;
        }
        else
        {
            weight *= _slot.Count;
            volume *= _slot.Count;
        }

        return $"{weight:F1} кг | {volume:F1} л";
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
                // RMB: Этап 7 — для камня Ци: использовать (мгновенное поглощение).
                // Для остальных категорий: только лог-инфо.
                if (_itemDb.TryGetItem(_slot.ItemId, out var itemData))
                {
                    if (itemData.Category == ItemCategory.QiStone)
                    {
                        _parent.TryUseQiStone(_slot.ItemId);
                    }
                    else
                    {
                        GD.Print($"[Inventory] RMB on {itemData.NameRu} (category={itemData.Category}, rarity={itemData.Rarity})");
                    }
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

/// <summary>
/// Trash/basket drop zone — drag inventory item here to drop it on ground near player.
/// Accepts drops from InventoryItemRow (source="inventory").
/// </summary>
public partial class TrashDropZone : Panel
{
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        // Visual: dark panel with trash icon + label.
        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        vbox.OffsetLeft = 4;
        vbox.OffsetRight = -4;
        vbox.OffsetTop = 4;
        vbox.OffsetBottom = -4;
        vbox.AddThemeConstantOverride("separation", 2);
        vbox.Alignment = BoxContainer.AlignmentMode.Center;
        AddChild(vbox);

        var icon = new Label
        {
            Text = "🗑",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        icon.AddThemeFontSizeOverride("font_size", 24);
        icon.AddThemeColorOverride("font_color", new Color(0.6f, 0.3f, 0.2f));
        vbox.AddChild(icon);

        var label = new Label
        {
            Text = "Выбросить",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        vbox.AddChild(label);

        // Style: dark red background.
        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.2f, 0.1f, 0.08f, 0.8f);
        style.SetBorderWidthAll(1);
        style.SetBorderColor(new Color(0.5f, 0.2f, 0.15f));
        style.SetCornerRadiusAll(4);
        AddThemeStyleboxOverride("panel", style);
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

        // Find InventoryWindow parent to access services.
        var inventoryWindow = FindParentInventoryWindow();
        if (inventoryWindow == null) return;

        inventoryWindow.DropItemOnGround(itemId);
    }

    private InventoryWindow? FindParentInventoryWindow()
    {
        Node? parent = GetParent();
        while (parent != null)
        {
            if (parent is InventoryWindow win)
                return win;
            parent = parent.GetParent();
        }
        return null;
    }
}

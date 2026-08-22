#nullable enable
// Создано: 2026-08-22 — слоты пояса в инвентаре.
// BeltSlotRow — горизонтальный ряд из 7 слотов пояса: drag&drop расходника
// из списка инвентаря → слот (весь стек); правый клик по слоту → вернуть
// в инвентарь. Ряд виден только при надетом поясе (BeltService.IsBeltEquipped).
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;
using CultivationGame.Modules.Inventory;
using CoreContracts = CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Adapter.UI;

/// <summary>
/// Belt quick-slot strip inside the inventory window. Accepts consumable
/// drops from the item list (source="inventory"), syncs with BeltService
/// via BeltSlotsChangedEvent / EquipmentChangedEvent.
/// </summary>
public partial class BeltSlotRow : Panel
{
    [Inject] private BeltService Belt = null!;
    [Inject] private IItemDatabaseService ItemDb = null!;
    [Inject] private Core.Events.ISubscriber<CoreContracts.BeltSlotsChangedEvent> SlotsSub = null!;
    [Inject] private Core.Events.ISubscriber<CoreContracts.EquipmentChangedEvent> EquipSub = null!;

    private readonly Label[] _labels = new Label[BeltService.SlotCount];
    private System.IDisposable? _slotsToken;
    private System.IDisposable? _equipToken;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        BuildUI();

        _slotsToken = SlotsSub?.Subscribe(OnBeltSlotsChanged);
        _equipToken = EquipSub?.Subscribe(OnEquipChanged);

        RefreshAll();
    }

    public override void _ExitTree()
    {
        _slotsToken?.Dispose();
        _equipToken?.Dispose();
    }

    private void BuildUI()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.16f, 0.12f, 0.08f, 0.9f),
        };
        style.SetBorderWidthAll(1);
        style.SetBorderColor(new Color(0.5f, 0.4f, 0.25f, 0.9f));
        style.SetCornerRadiusAll(6);
        AddThemeStyleboxOverride("panel", style);

        var outer = new VBoxContainer();
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        outer.OffsetLeft = 6; outer.OffsetRight = -6;
        outer.OffsetTop = 4; outer.OffsetBottom = -4;
        outer.AddThemeConstantOverride("separation", 2);
        AddChild(outer);

        var title = new Label
        {
            Text = "Пояс — слоты быстрого доступа (3–9). Перетащи расходник · ПКМ — вернуть",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 12);
        title.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        outer.AddChild(title);

        var hbox = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        hbox.AddThemeConstantOverride("separation", 4);
        outer.AddChild(hbox);

        for (int i = 0; i < BeltService.SlotCount; i++)
        {
            _dropSlots[i] = new BeltDropSlot { BeltIndex = i };
            hbox.AddChild(_dropSlots[i]);
        }
    }

    private readonly BeltDropSlot[] _dropSlots = new BeltDropSlot[BeltService.SlotCount];

    private void OnBeltSlotsChanged(in CoreContracts.BeltSlotsChangedEvent e) => RefreshAll();

    private void OnEquipChanged(in CoreContracts.EquipmentChangedEvent e)
    {
        if (e.Slot == Core.Data.EquipmentSlot.Belt) RefreshAll();
    }

    /// <summary>Row is visible only with an equipped belt; slots re-read state.</summary>
    public void RefreshAll()
    {
        Visible = Belt is { IsBeltEquipped: true };
        foreach (var s in _dropSlots)
            s?.Refresh();
    }

    /// <summary>One belt drop slot: accepts inventory consumable drops.</summary>
    private sealed partial class BeltDropSlot : Panel
    {
        public int BeltIndex;

        [Inject] private BeltService Belt = null!;
        [Inject] private IItemDatabaseService ItemDb = null!;

        private Label _label = null!;

        public override void _Ready()
        {
            var container = Scene.GameBoot.Container;
            if (container != null)
                ContainerAdapter.InjectProperties(this, container);

            CustomMinimumSize = new Vector2(64, 40);
            MouseFilter = MouseFilterEnum.Stop;

            var style = new StyleBoxFlat { BgColor = new Color(0.2f, 0.2f, 0.24f, 0.9f) };
            style.SetBorderWidthAll(1);
            style.SetBorderColor(new Color(0.55f, 0.55f, 0.6f));
            style.SetCornerRadiusAll(4);
            AddThemeStyleboxOverride("panel", style);

            _label = new Label
            {
                Text = (BeltIndex + BeltService.HotbarFirstIndex).ToString(),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _label.AddThemeFontSizeOverride("font_size", 11);
            _label.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.75f));
            _label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(_label);

            Refresh();
        }

        public void Refresh()
        {
            var slots = Belt?.GetSlots();
            if (slots == null || BeltIndex >= slots.Count) return;

            var s = slots[BeltIndex];
            if (s is { Count: > 0 } && ItemDb != null && ItemDb.TryGetItem(s.ItemId, out var item))
                _label.Text = $"{ShortName(item.NameRu)} ×{s.Count}";
            else
                _label.Text = (BeltIndex + BeltService.HotbarFirstIndex).ToString();
        }

        public override bool _CanDropData(Vector2 atPosition, Variant data)
        {
            if (!CharacterDollPanel.TryParseDragData(data, out var itemId, out var source))
                return false;
            if (source != "inventory") return false;
            // Consumables only, belt must be on.
            return Belt is { IsBeltEquipped: true }
                && ItemDb != null
                && ItemDb.TryGetItem(itemId, out var it)
                && it.Category == ItemCategory.Consumable;
        }

        public override void _DropData(Vector2 atPosition, Variant data)
        {
            if (!CharacterDollPanel.TryParseDragData(data, out var itemId, out _)) return;
            int moved = Belt?.TryAssign(BeltIndex, itemId) ?? 0;
            if (moved > 0) GD.Print($"[BeltSlotRow] Assigned {itemId}×{moved} → slot {BeltIndex}");
        }

        public override void _GuiInput(InputEvent @event)
        {
            // RMB on a filled slot → return stack to inventory.
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
            {
                if (Belt?.TryTakeBack(BeltIndex) == true)
                    GD.Print($"[BeltSlotRow] Took back slot {BeltIndex}");
            }
        }

        private static string ShortName(string name) =>
            name.Length <= 8 ? name : name.Substring(0, 8) + "…";
    }
}

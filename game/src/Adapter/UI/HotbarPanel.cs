#nullable enable
// Создано: 2026-08-22 — хотбар (UI_DESIGN §6.1 View #3, HOTKEYS §8).
// HotbarPanel — 9 слотов внизу экрана: 1-2 зеркалируют оружие,
// 3-7+3=3..9 — слоты пояса (расходники). Ряд пояса появляется только
// когда надет пояс (BeltService.IsBeltEquipped).
// Клик по слоту пояса / клавиши 3-9 → использовать расходник.
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;
using CultivationGame.Modules.Inventory;

namespace CultivationGame.Adapter.UI;

/// <summary>
/// Hotbar HUD panel: slots 1-2 = weapons (info only), slots 3-9 = belt
/// consumables. Belt slots are only visible and clickable when a belt is
/// equipped. Subscribes to BeltSlotsChangedEvent and EquipmentChangedEvent
/// to stay in sync.
/// </summary>
public partial class HotbarPanel : Panel
{
    [Inject] private BeltService Belt = null!;
    [Inject] private IEquipmentService Equipment = null!;
    [Inject] private IItemDatabaseService ItemDb = null!;
    [Inject] private Core.Events.ISubscriber<Core.Messaging.Contracts.BeltSlotsChangedEvent> SlotsSub = null!;
    [Inject] private Core.Events.ISubscriber<Core.Messaging.Contracts.EquipmentChangedEvent> EquipSub = null!;

    private readonly Label[] _slotLabels = new Label[9];
    private readonly Panel[] _slotPanels = new Panel[9];
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
        GD.Print("[HotbarPanel] Ready");
    }

    public override void _ExitTree()
    {
        _slotsToken?.Dispose();
        _equipToken?.Dispose();
    }

    private void BuildUI()
    {
        // Bottom-center bar: 9 slots 52×52, parchment style.
        SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        CustomMinimumSize = new Vector2(9 * 56 + 8, 60);
        OffsetLeft = -CustomMinimumSize.X / 2f;
        OffsetRight = CustomMinimumSize.X / 2f;
        OffsetTop = -64;
        OffsetBottom = -4;
        MouseFilter = MouseFilterEnum.Pass;

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.09f, 0.06f, 0.85f),
        };
        style.SetBorderWidthAll(1);
        style.SetBorderColor(new Color(0.45f, 0.35f, 0.2f, 0.8f));
        style.SetCornerRadiusAll(6);
        AddThemeStyleboxOverride("panel", style);

        var hbox = new HBoxContainer();
        hbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        hbox.OffsetLeft = 4; hbox.OffsetRight = -4;
        hbox.OffsetTop = 4; hbox.OffsetBottom = -4;
        hbox.AddThemeConstantOverride("separation", 4);
        hbox.Alignment = BoxContainer.AlignmentMode.Center;
        AddChild(hbox);

        for (int i = 0; i < 9; i++)
        {
            int hotbarIndex = i + 1;
            bool isWeapon = i < 2;

            var slotPanel = new Panel
            {
                CustomMinimumSize = new Vector2(52, 52),
                MouseFilter = MouseFilterEnum.Stop,
            };
            var slotStyle = new StyleBoxFlat
            {
                BgColor = isWeapon
                    ? new Color(0.25f, 0.2f, 0.1f, 0.9f)   // weapon: orange-ish
                    : new Color(0.18f, 0.18f, 0.2f, 0.9f), // belt: grey
            };
            slotStyle.SetBorderWidthAll(1);
            slotStyle.SetBorderColor(isWeapon
                ? new Color(0.8f, 0.55f, 0.25f)
                : new Color(0.5f, 0.5f, 0.55f));
            slotStyle.SetCornerRadiusAll(4);
            slotPanel.AddThemeStyleboxOverride("panel", slotStyle);

            var label = new Label
            {
                Text = hotbarIndex.ToString(),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            label.AddThemeFontSizeOverride("font_size", 13);
            label.AddThemeColorOverride("font_color", new Color(0.85f, 0.75f, 0.55f));
            label.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
            slotPanel.AddChild(label);

            if (!isWeapon)
            {
                int beltIndex = hotbarIndex - BeltService.HotbarFirstIndex;
                slotPanel.GuiInput += @event =>
                {
                    if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                        Belt?.Use(beltIndex);
                };
            }

            _slotLabels[i] = label;
            _slotPanels[i] = slotPanel;
            hbox.AddChild(slotPanel);
        }
    }

    private void OnBeltSlotsChanged(in Core.Messaging.Contracts.BeltSlotsChangedEvent e)
    {
        // Slot 0-6 → hotbar 3-9.
        int hotbarIdx = e.SlotIndex + BeltService.HotbarFirstIndex - 1;
        if (hotbarIdx is < 2 or > 8) return;
        RefreshSlot(hotbarIdx);
    }

    private void OnEquipChanged(in Core.Messaging.Contracts.EquipmentChangedEvent e)
    {
        if (e.Slot != Core.Data.EquipmentSlot.Belt) return;
        RefreshAll();
    }

    private void RefreshAll()
    {
        // Weapons (1-2).
        RefreshWeapon(0, Core.Data.EquipmentSlot.WeaponMain);
        RefreshWeapon(1, Core.Data.EquipmentSlot.WeaponOff);

        // Belt (3-9) — visibility gate.
        bool beltOn = Belt is { IsBeltEquipped: true };
        var slots = Belt?.GetSlots();
        for (int i = 0; i < BeltService.SlotCount; i++)
        {
            var panel = _slotPanels[i + 2];
            panel.Visible = beltOn;
            if (beltOn && slots != null && i < slots.Count)
                SetSlotText(i + 2, slots[i]);
        }
    }

    private void RefreshSlot(int hotbarIdx)
    {
        if (hotbarIdx < 2) return;
        var slots = Belt?.GetSlots();
        int beltIndex = hotbarIdx - BeltService.HotbarFirstIndex;
        if (slots != null && beltIndex >= 0 && beltIndex < slots.Count)
            SetSlotText(hotbarIdx, slots[beltIndex]);
    }

    private void RefreshWeapon(int idx, Core.Data.EquipmentSlot slot)
    {
        var equipped = Equipment?.GetEquipped(slot);
        _slotLabels[idx].Text = equipped != null
            ? ShortName(equipped.NameRu)
            : (idx + 1).ToString();
    }

    private void SetSlotText(int hotbarIdx, BeltSlot slot)
    {
        string text;
        if (slot is { Count: > 0 } && ItemDb != null && ItemDb.TryGetItem(slot.ItemId, out var item))
            text = $"{ShortName(item.NameRu)}×{slot.Count}";
        else
            text = (hotbarIdx + 1).ToString();
        _slotLabels[hotbarIdx].Text = text;
    }

    private static string ShortName(string name) =>
        name.Length <= 6 ? name : name.Substring(0, 6) + "…";
}

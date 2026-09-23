#nullable enable
// Создано: 2026-09-23 — R37-c (баг-репорт пользователя 23.09):
// «не могу добавить камни Ци в зарядник, лекарство добавляется без проблем».
//
// Окно зарядника Ци (клавиша H — канон CHARGER_SYSTEM + тост ItemUseService
// «вставьте в зарядник (H)», который раньше вёл в никуда):
//   • Слоты камней (N из ChargerService.Configure) — drag&drop камня Ци из
//     инвентаря (source="inventory"), ПКМ по слоту — извлечь (контракт
//     анти-дюпа моста: полный камень; пустой рассыпается; частичный — тост).
//   • Буфер Ци (текущий/ёмкость, прогресс), тепловой баланс (уровень/состояние).
//   • Режим Вкл/Выкл (ChargerService.Activate/Deactivate — §4.2 канон: только
//     два режима; слоты работают при On и не перегретом заряднике).
//   • Гейт: содержимое видно только при надетом заряднике (слот Belt +
//     ItemType=="Charger", ChargerItemBridge.IsChargerEquipped); без него —
//     подсказка «Зарядник не надет».
//
// Пауза: модальное окно (как инвентарь B) — GWC HandleModalPauseOnOpen/
// HandleModalResumeOnClose + Closed-событие (Esc/фон — единая точка).
using Godot;
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Core.DI;
using CultivationGame.Adapter.Di;
using CultivationGame.Modules.Charger;

namespace CultivationGame.Adapter.UI;

/// <summary>
/// Модальное окно зарядника Ци (H): слоты камней, буфер, тепло, режим.
/// Обновляется событиями домена (ChargerStateChanged/BufferChanged/
/// HeatChanged) и экипировки (EquipmentChanged).
/// </summary>
public partial class ChargerWindow : Control
{
    [Inject] private ChargerItemBridge Bridge { get; set; } = null!;
    [Inject] private IChargerService Charger { get; set; } = null!;
    [Inject] private IItemDatabaseService ItemDb { get; set; } = null!;
    [Inject] private ISubscriber<ChargerStateChangedEvent> SlotSub { get; set; } = null!;
    [Inject] private ISubscriber<ChargerBufferChangedEvent> BufferSub { get; set; } = null!;
    [Inject] private ISubscriber<ChargerHeatChangedEvent> HeatSub { get; set; } = null!;
    [Inject] private ISubscriber<EquipmentChangedEvent> EquipSub { get; set; } = null!;

    /// <summary>Закрытие окна (Esc/фон/повторное H) — резюм тиков из GWC.</summary>
    public event Action? Closed;

    // R37-c QA (GODOT_CHARGERQA_DEBUG): контент виден (зарядник надет).
    public bool SlotsVisibleForQA => _slotsRow is { Visible: true };
    /// <summary>Снимок слота для сима (через мост, без мутации).</summary>
    public Modules.Charger.ChargerSlotSnapshot SlotForQA(int index) => Bridge?.GetSlot(index) ?? default;

    private Panel _panel = null!;
    private Label _headerLabel = null!;
    private Label _gateLabel = null!;
    private HBoxContainer _slotsRow = null!;
    private Label _bufferLabel = null!;
    private ProgressBar _bufferBar = null!;
    private Label _heatLabel = null!;
    private ProgressBar _heatBar = null!;
    private Button _modeButton = null!;
    private Label _hintLabel = null!;
    private VBoxContainer _contentBox = null!;

    private ChargerStoneSlot[] _stoneSlots = Array.Empty<ChargerStoneSlot>();
    private IDisposable? _slotToken;
    private IDisposable? _bufferToken;
    private IDisposable? _heatToken;
    private IDisposable? _equipToken;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null) ContainerAdapter.InjectProperties(this, container);

        BuildUI();

        _slotToken = SlotSub?.Subscribe(OnSlotChanged);
        _bufferToken = BufferSub?.Subscribe(OnBufferChanged);
        _heatToken = HeatSub?.Subscribe(OnHeatChanged);
        _equipToken = EquipSub?.Subscribe(OnEquipChanged);

        Visible = false;
        GD.Print("[ChargerWindow] Ready");
    }

    public override void _ExitTree()
    {
        _slotToken?.Dispose();
        _bufferToken?.Dispose();
        _heatToken?.Dispose();
        _equipToken?.Dispose();
    }

    // === Открытие/закрытие ==========================================

    public void Toggle()
    {
        if (Visible) Close();
        else
        {
            Visible = true;
            RefreshAll();
        }
    }

    public void Close()
    {
        if (!Visible) return;
        Visible = false;
        Closed?.Invoke();
    }

    private void OnSlotChanged(in ChargerStateChangedEvent e) { if (Visible) RefreshSlots(); }
    private void OnBufferChanged(in ChargerBufferChangedEvent e) { if (Visible) RefreshBuffer(); }
    private void OnHeatChanged(in ChargerHeatChangedEvent e) { if (Visible) RefreshHeat(); }
    private void OnEquipChanged(in EquipmentChangedEvent e)
    {
        if (e.Slot != EquipmentSlot.Belt) return;
        if (Visible) RefreshAll();
    }

    // === UI =========================================================

    private void BuildUI()
    {
        Theme = ParchmentTheme.Create();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var bg = new ColorRect { Name = "Background", Color = new Color(0.05f, 0.03f, 0.02f, 0.7f) };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Stop;
        bg.GuiInput += OnBackgroundInput;
        AddChild(bg);

        _panel = new Panel { Name = "ChargerPanel" };
        _panel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        _panel.OffsetLeft = -240;
        _panel.OffsetRight = 240;
        _panel.OffsetTop = -210;
        _panel.OffsetBottom = 210;
        _panel.MouseFilter = MouseFilterEnum.Stop;
        AddChild(_panel);

        var outer = new VBoxContainer();
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        outer.OffsetLeft = 16;
        outer.OffsetRight = -16;
        outer.OffsetTop = 12;
        outer.OffsetBottom = -12;
        outer.AddThemeConstantOverride("separation", 8);
        _panel.AddChild(outer);
        _contentBox = outer;

        _headerLabel = new Label
        {
            Text = "⌁ Зарядник Ци ⌁",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _headerLabel.AddThemeFontSizeOverride("font_size", 24);
        _headerLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkBlack);
        outer.AddChild(_headerLabel);

        // Гейт-лейбл: показывается вместо контента, если зарядник не надет.
        _gateLabel = new Label
        {
            Text = "Зарядник не надет.\nНаденьте зарядник Ци в слот пояса (кукла C или двойной клик по предмету).",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(430, 60),
        };
        _gateLabel.AddThemeFontSizeOverride("font_size", 15);
        _gateLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        outer.AddChild(_gateLabel);

        outer.AddChild(new HSeparator());

        // Слоты камней.
        _slotsRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        _slotsRow.AddThemeConstantOverride("separation", 10);
        outer.AddChild(_slotsRow);

        int slotCount = Charger?.SlotCount ?? 0;
        Array.Resize(ref _stoneSlots, Math.Max(0, slotCount));
        for (int i = 0; i < slotCount; i++)
        {
            _stoneSlots[i] = new ChargerStoneSlot { SlotIndex = i, BridgeRef = this };
            _slotsRow.AddChild(_stoneSlots[i]);
        }

        var slotsHint = new Label
        {
            Text = "Перетащи камень Ци из инвентаря · ПКМ по слоту — извлечь",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        slotsHint.AddThemeFontSizeOverride("font_size", 12);
        slotsHint.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        outer.AddChild(slotsHint);

        outer.AddChild(new HSeparator());

        // Буфер.
        _bufferLabel = new Label
        {
            Text = "Буфер Ци: 0 / 0",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _bufferLabel.AddThemeFontSizeOverride("font_size", 15);
        _bufferLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkBlack);
        outer.AddChild(_bufferLabel);

        _bufferBar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            Value = 0,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(430, 14),
        };
        outer.AddChild(_bufferBar);

        // Тепло.
        _heatLabel = new Label
        {
            Text = "Тепло: 0% (Cool)",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _heatLabel.AddThemeFontSizeOverride("font_size", 15);
        _heatLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkBlack);
        outer.AddChild(_heatLabel);

        _heatBar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            Value = 0,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(430, 14),
        };
        outer.AddChild(_heatBar);

        // Режим + статус.
        var modeRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        modeRow.AddThemeConstantOverride("separation", 10);
        outer.AddChild(modeRow);

        _modeButton = new Button { Text = "⏻ Режим: Вкл" };
        _modeButton.Pressed += OnModePressed;
        modeRow.AddChild(_modeButton);

        _hintLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(430, 30),
        };
        _hintLabel.AddThemeFontSizeOverride("font_size", 12);
        _hintLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        outer.AddChild(_hintLabel);
    }

    private void OnBackgroundInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: Godot.MouseButton.Left })
            Close();
    }

    private void OnModePressed()
    {
        if (Charger == null) return;
        if (Charger.Mode == ChargerMode.On) Charger.Deactivate();
        else Charger.Activate();
        RefreshMode();
    }

    // === Refresh ====================================================

    private void RefreshAll()
    {
        bool equipped = Bridge is { IsChargerEquipped: true };
        _gateLabel.Visible = !equipped;
        _slotsRow.Visible = equipped;
        _bufferLabel.Visible = equipped;
        _bufferBar.Visible = equipped;
        _heatLabel.Visible = equipped;
        _heatBar.Visible = equipped;
        _modeButton.Visible = equipped;

        if (!equipped)
        {
            _hintLabel.Text = "Камни Ци вставляются только в НАДЕТЫЙ зарядник.";
            return;
        }

        RefreshSlots();
        RefreshBuffer();
        RefreshHeat();
        RefreshMode();
    }

    private void RefreshSlots()
    {
        if (Bridge == null) return;
        foreach (var slot in _stoneSlots)
            slot?.Refresh();
    }

    private void RefreshBuffer()
    {
        if (Charger == null) return;
        long cap = Math.Max(1, Charger.BufferCapacity);
        long qi = Charger.BufferQi;
        _bufferLabel.Text = $"Буфер Ци: {qi} / {cap}";
        _bufferBar.MaxValue = cap;
        _bufferBar.Value = Math.Min(qi, cap);
    }

    private void RefreshHeat()
    {
        if (Charger == null) return;
        float heat = Math.Clamp(Charger.HeatLevel, 0f, 1f);
        _heatLabel.Text = $"Тепло: {heat * 100f:F0}% ({Charger.HeatState})";
        _heatBar.Value = heat * 100f;
        // Цвет полосы тепла: Cool → зелёный, Warm → янтарь, Hot/Critical → красный.
        _heatBar.Modulate = Charger.HeatState switch
        {
            HeatState.Cool => new Color(0.5f, 0.8f, 0.5f),
            HeatState.Warm => new Color(0.9f, 0.75f, 0.4f),
            _ => new Color(0.9f, 0.4f, 0.3f),
        };
    }

    private void RefreshMode()
    {
        if (Charger == null) return;
        bool on = Charger.Mode == ChargerMode.On;
        _modeButton.Text = on ? "⏻ Режим: Вкл" : "⏻ Режим: Выкл";
        _hintLabel.Text = Charger.IsOverheated
            ? "⚠ ПЕРЕГРЕВ — зарядник заблокирован до остывания"
            : on
                ? "Включён: камни отдают Ци в буфер, буфер — практику (вне боя)."
                : "Выключен: Ци не извлекается и не передаётся.";
    }

    // === Слот камня (nested) =======================================

    /// <summary>
    /// Один слот камня: drag&drop из инвентаря (только QiStoneData),
    /// ПКМ — извлечение (контракт моста: полный/пустой/тост-причина).
    /// </summary>
    private sealed partial class ChargerStoneSlot : Panel
    {
        public int SlotIndex;
        public ChargerWindow? BridgeRef;

        private Label _label = null!;

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(120, 64);
            MouseFilter = MouseFilterEnum.Stop;

            var style = new StyleBoxFlat { BgColor = new Color(0.16f, 0.12f, 0.08f, 0.9f) };
            style.SetBorderWidthAll(1);
            style.SetBorderColor(new Color(0.5f, 0.4f, 0.25f, 0.9f));
            style.SetCornerRadiusAll(6);
            AddThemeStyleboxOverride("panel", style);

            _label = new Label
            {
                Text = "пусто",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            _label.AddThemeFontSizeOverride("font_size", 12);
            _label.AddThemeColorOverride("font_color", new Color(0.75f, 0.68f, 0.55f));
            _label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(_label);

            Refresh();
        }

        public void Refresh()
        {
            var bridge = BridgeRef?.Bridge;
            if (bridge == null) return;
            var snap = bridge.GetSlot(SlotIndex);
            _label.Text = snap.HasStone
                ? $"💎 {snap.CurrentQi}/{snap.MaxQi}"
                : "пусто";
        }

        public override bool _CanDropData(Vector2 atPosition, Variant data)
        {
            var win = BridgeRef;
            if (win?.Bridge is not { IsChargerEquipped: true }) return false;
            if (!CharacterDollPanel.TryParseDragData(data, out var itemId, out var source)) return false;
            if (source != "inventory") return false;
            // Только камни Ци (категория предмета).
            return win.ItemDb != null
                && win.ItemDb.TryGetItem(itemId, out var it)
                && it is QiStoneData;
        }

        public override void _DropData(Vector2 atPosition, Variant data)
        {
            // R10 P1-SlotId: слот-адресная вставка (стабильный Guid кучки).
            if (!CharacterDollPanel.TryParseDragData(data, out var itemId, out _, out var slotId)) return;
            BridgeRef?.Bridge?.TryInsertStone(slotId, itemId, out _);
            // Слоты обновятся по ChargerStateChangedEvent (домен публикует).
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: Godot.MouseButton.Right })
            {
                BridgeRef?.Bridge?.TryExtractStone(SlotIndex, out _);
            }
        }
    }
}

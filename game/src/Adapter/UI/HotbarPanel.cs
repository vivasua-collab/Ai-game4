#nullable enable
// Создано: 2026-08-22 — хотбар (UI_DESIGN §6.1 View #3, HOTKEYS §8).
// Редактировано: 2026-09-04 S4 — Hotbar v2: слоты 3-9 показывают ТЕХНИКИ
// (клавиши 3-9 без Shift кастуют именно их — раньше UI показывал пояс,
// что вводило игрока в заблуждение), с кулдаун-оверлеем и Qi-достаточностью.
// Пояс (Shift+3..9) вынесен в отдельный компактный ряд сверху (клик работает).
//
// Layout (bottom-center):
//   [ Пояс·⇧ | b3 b4 b5 b6 b7 b8 b9 ]   — виден только при надетом поясе
//   [ 1 оружие | 2 оружие | 3..9 техники ]
//
// Слот техники (52×52):
//   • имя + emoji стихии, рамка цвета стихии (ElementStyle);
//   • кулдаун: тёмный «занавес» сверху (высота = remaining/cooldown) +
//     крупные янтарные цифры секунд по центру;
//   • недостаток Ци — строка «Ци N» красная (иначе зелёная);
//   • TooltipText: полное описание (урон/дальность/мастерство);
//   • клик = каст (аналог клавиши 3-9, позиция курсора как у Z).
// Редактировано: 2026-09-10 R15 — слоты 1-2 оружия: ИКОНКА (32×32,
// WeaponVisualCatalog) в центре слота + короткая подпись снизу (дефолт
// ответа §6.3 плана R15: «иконка + короткая подпись под ней»); пустой
// слот — цифра, иконки нет. Подпись по-прежнему из NameRu.
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Events;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Adapter.Di;
using CultivationGame.Modules.Combat;
using CultivationGame.Modules.Player;
using CultivationGame.Modules.Inventory;

namespace CultivationGame.Adapter.UI;

/// <summary>
/// Hotbar HUD panel v2 (S4, 2026-09-04). Main row: slots 1-2 = weapons,
/// slots 3-9 = techniques (cooldown overlay + Qi affordability + tooltip).
/// Belt row above: compact consumable slots, visible only when a belt is
/// equipped (click = use, same as Shift+3..9 keys).
///
/// R26 (2026-09-21, идея 1 «слот-центричная», выбор пользователя):
/// индикация ЗАРЯДКИ и УДЕРЖАНИЯ техники в ауре — слот показывает:
///   • заряд идёт: изумрудная полоса снизу-вверх + % по центру + пульс
///     рамки; overcharge (ChargedQi > QiCost) — янтарная полоска сверху
///     и подпись «×N.N» (potency);
///   • удержание в ауре: рамка цвета стихии (HeldTechniqueChanged),
///     метка «◉» в углу, тултип «повторный Z/клик = выпуск».
/// Спуск = повторное нажатие той же клавиши (Z/3-9/клик слота — механика
/// PlayerTechniqueCaster/AuraHoldService, без изменений).
/// Источник: план R23 §6.5 (checkpoints/plans/2026-09-20_r23_...md).
/// </summary>
public partial class HotbarPanel : Panel
{
    [Inject] private BeltService Belt = null!;
    [Inject] private IEquipmentService Equipment = null!;
    [Inject] private IItemDatabaseService ItemDb = null!;
    [Inject] private TechniqueService Techniques = null!;
    [Inject] private TechniqueSlotService TechniqueSlots = null!;
    [Inject] private IQiService Qi = null!;
    [Inject] private IPublisher<TechniqueCastRequestedEvent> CastPub = null!;
    [Inject] private ISubscriber<TechniqueSlotAssignedEvent> TechAssignedSub = null!;
    [Inject] private ISubscriber<TechniqueSlotClearedEvent> TechClearedSub = null!;
    [Inject] private ISubscriber<Core.Messaging.Contracts.BeltSlotsChangedEvent> SlotsSub = null!;
    [Inject] private ISubscriber<Core.Messaging.Contracts.EquipmentChangedEvent> EquipSub = null!;
    // R26: события зарядки/удержания (раньше их слушал только QA-дебаггер).
    [Inject] private ISubscriber<TechniqueChargeStartedEvent> ChargeStartedSub = null!;
    [Inject] private ISubscriber<TechniqueChargeProgressEvent> ChargeProgressSub = null!;
    [Inject] private ISubscriber<TechniqueChargeCompletedEvent> ChargeCompletedSub = null!;
    [Inject] private ISubscriber<TechniqueChargeCancelledEvent> ChargeCancelledSub = null!;
    [Inject] private ISubscriber<HeldTechniqueChangedEvent> HeldChangedSub = null!;
    // П5 (репорт 21.09): спец-слот медитации — тумблер + состояние + изучение.
    [Inject] private IPublisher<Core.Messaging.Contracts.MeditationToggleRequestedEvent> _meditationTogglePub = null!;
    [Inject] private ISubscriber<Core.Messaging.Contracts.MeditationStateChangedEvent> _meditationStateSub = null!;
    [Inject] private ISubscriber<TechniqueLearnedEvent> _learnedSub = null!;

    // === Layout constants ===
    private const float MainSlotSize = 52f;
    private const float BeltSlotSize = 40f;
    private const float RowGap = 4f;
    private const float PanelPad = 4f;

    // === Main row (9 slots: 0-1 weapons, 2-8 = techniques for keys 3-9) ===
    private readonly Panel[] _slotPanels = new Panel[9];
    private readonly Label[] _slotLabels = new Label[9];       // имя/цифра
    private readonly Label?[] _keyLabels = new Label?[9];      // цифра клавиши (угол)
    private readonly Label?[] _qiLabels = new Label?[9];       // Ци-строка (низ)
    private readonly ColorRect?[] _cdOverlays = new ColorRect?[9];
    private readonly Label?[] _cdLabels = new Label?[9];
    private readonly StyleBoxFlat[] _slotStyles = new StyleBoxFlat[9];
    private readonly Label[] _weaponQiLabels = new Label[2];   // 0-1: не используется (оружие), зарезервировано
    // R15: иконки оружия в слотах 1-2 (WeaponVisualCatalog).
    private readonly TextureRect?[] _weaponIcons = new TextureRect?[2];
    private readonly string?[] _weaponIconKeys = new string?[2]; // QA-ключи

    // === R26: индикация зарядки/удержания ===
    private readonly ColorRect?[] _chargeOverlays = new ColorRect?[9];   // полоса заряда (низ→верх)
    private readonly ColorRect?[] _ocTopBars = new ColorRect?[9];        // overcharge-полоска (верх, 4px)
    private readonly Label?[] _chargeLabels = new Label?[9];             // «87%» / «×1.3»
    private readonly Label?[] _heldMarks = new Label?[9];                // «◉» метка удержания

    // Состояние зарядки игрока (только одна одновременно — модель заполнения).
    private string? _chargingTechId;
    private long _chargeQi;
    private long _chargeCost;
    private long _chargeCapacity;   // потолок перезарядки (potency 2000‰)
    private int _chargePotency;

    // Удержание в ауре (HeldTechniqueChangedEvent).
    private string? _heldTechId;
    private Element _heldElement = Element.Neutral;
    private int _heldPotency;

    private float _pulseTime;       // фаза пульса рамки
    private readonly Color[] _baseBorders = new Color[9]; // дефолт рамок (возврат)

    // === Belt row (7 slots) ===
    private readonly Panel[] _beltPanels = new Panel[BeltService.SlotCount];
    private readonly Label[] _beltLabels = new Label[BeltService.SlotCount];
    private HBoxContainer? _beltRow;

    private System.IDisposable? _techAssignedToken;
    private System.IDisposable? _techClearedToken;
    private System.IDisposable? _slotsToken;
    private System.IDisposable? _equipToken;

    // П5 (репорт 21.09): спец-слот медитации — поля.
    private System.IDisposable? _meditationStateToken;
    private System.IDisposable? _learnedToken;
    private Panel? _meditationPanel;
    private Label? _meditationGlyph;
    private Label? _meditationNameLabel;
    private bool _meditationActive;
    private string? _meditationTechId;
    // R26: подписки зарядки/удержания.
    private System.IDisposable? _chargeStartedToken;
    private System.IDisposable? _chargeProgressToken;
    private System.IDisposable? _chargeCompletedToken;
    private System.IDisposable? _chargeCancelledToken;
    private System.IDisposable? _heldChangedToken;

    // Кэш текста (чтобы не спамить Text-сеттер каждый кадр — грязный рендер).
    private readonly string?[] _nameCache = new string?[9];
    private readonly string?[] _cdCache = new string?[9];

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        BuildUI();

        _techAssignedToken = TechAssignedSub?.Subscribe(OnTechSlotAssigned);
        _techClearedToken = TechClearedSub?.Subscribe(OnTechSlotCleared);
        _slotsToken = SlotsSub?.Subscribe(OnBeltSlotsChanged);
        _equipToken = EquipSub?.Subscribe(OnEquipChanged);

        // R26: зарядка/удержание → индикация в слотах.
        _chargeStartedToken = ChargeStartedSub?.Subscribe(OnChargeStarted);
        _chargeProgressToken = ChargeProgressSub?.Subscribe(OnChargeProgress);
        _chargeCompletedToken = ChargeCompletedSub?.Subscribe(OnChargeCompleted);
        _chargeCancelledToken = ChargeCancelledSub?.Subscribe(OnChargeCancelled);
        _heldChangedToken = HeldChangedSub?.Subscribe(OnHeldChanged);

        // П5 (репорт 21.09): спец-слот медитации — состояние + изучение техник.
        _meditationStateToken = _meditationStateSub?.Subscribe(OnMeditationStateChanged);
        _learnedToken = _learnedSub?.Subscribe(OnTechniqueLearned);

        RefreshAll();
        GD.Print("[HotbarPanel] Ready (v2: techniques + cooldowns + belt row + charge/aura R26 + медитация-слот П5)");
    }

    public override void _ExitTree()
    {
        _techAssignedToken?.Dispose();
        _techClearedToken?.Dispose();
        _slotsToken?.Dispose();
        _equipToken?.Dispose();
        _chargeStartedToken?.Dispose();
        _chargeProgressToken?.Dispose();
        _chargeCompletedToken?.Dispose();
        _chargeCancelledToken?.Dispose();
        _heldChangedToken?.Dispose();
        _meditationStateToken?.Dispose();
        _learnedToken?.Dispose();
    }

    private void BuildUI()
    {
        SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        MouseFilter = MouseFilterEnum.Pass;

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.09f, 0.06f, 0.85f),
        };
        style.SetBorderWidthAll(1);
        style.SetBorderColor(new Color(0.45f, 0.35f, 0.2f, 0.8f));
        style.SetCornerRadiusAll(6);
        AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        vbox.OffsetLeft = PanelPad; vbox.OffsetRight = -PanelPad;
        vbox.OffsetTop = PanelPad; vbox.OffsetBottom = -PanelPad;
        vbox.AddThemeConstantOverride("separation", (int)RowGap);
        vbox.Alignment = BoxContainer.AlignmentMode.End;
        AddChild(vbox);

        // === Belt row (верх, компактный) ===
        _beltRow = new HBoxContainer();
        _beltRow.AddThemeConstantOverride("separation", 4);
        _beltRow.Alignment = BoxContainer.AlignmentMode.Center;
        _beltRow.Visible = false; // gate: IsBeltEquipped (RefreshAll)
        vbox.AddChild(_beltRow);

        var beltCaption = new Label
        {
            Text = "Пояс\n⇧+3..9",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(44, BeltSlotSize),
        };
        beltCaption.AddThemeFontSizeOverride("font_size", 9);
        beltCaption.AddThemeColorOverride("font_color", new Color(0.75f, 0.65f, 0.5f, 0.9f));
        _beltRow.AddChild(beltCaption);

        for (int i = 0; i < BeltService.SlotCount; i++)
        {
            int beltIndex = i;
            var slotPanel = new Panel
            {
                CustomMinimumSize = new Vector2(BeltSlotSize, BeltSlotSize),
                MouseFilter = MouseFilterEnum.Stop,
            };
            var slotStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.18f, 0.16f, 0.14f, 0.9f),
            };
            slotStyle.SetBorderWidthAll(1);
            slotStyle.SetBorderColor(new Color(0.5f, 0.45f, 0.38f));
            slotStyle.SetCornerRadiusAll(3);
            slotPanel.AddThemeStyleboxOverride("panel", slotStyle);

            var label = new Label
            {
                Text = (BeltService.HotbarFirstIndex + i).ToString(),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            label.AddThemeFontSizeOverride("font_size", 10);
            label.AddThemeColorOverride("font_color", new Color(0.85f, 0.75f, 0.55f));
            label.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
            slotPanel.AddChild(label);

            slotPanel.GuiInput += @event =>
            {
                if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == Godot.MouseButton.Left)
                    Belt?.Use(beltIndex);
            };

            _beltPanels[i] = slotPanel;
            _beltLabels[i] = label;
            _beltRow.AddChild(slotPanel);
        }

        // === Main row (9 слотов: 1-2 оружие, 3-9 техники) ===
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 4);
        hbox.Alignment = BoxContainer.AlignmentMode.Center;
        vbox.AddChild(hbox);

        for (int i = 0; i < 9; i++)
        {
            int hotbarIndex = i + 1;
            bool isWeapon = i < 2;

            var slotPanel = new Panel
            {
                CustomMinimumSize = new Vector2(MainSlotSize, MainSlotSize),
                MouseFilter = MouseFilterEnum.Stop,
            };
            var slotStyle = new StyleBoxFlat
            {
                BgColor = isWeapon
                    ? new Color(0.25f, 0.2f, 0.1f, 0.9f)   // weapon: orange-ish
                    : new Color(0.16f, 0.15f, 0.12f, 0.9f), // technique: тёмно-нейтральный
            };
            slotStyle.SetBorderWidthAll(1);
            slotStyle.SetBorderColor(isWeapon
                ? new Color(0.8f, 0.55f, 0.25f)
                : new Color(0.42f, 0.38f, 0.32f));
            slotStyle.SetCornerRadiusAll(4);
            slotPanel.AddThemeStyleboxOverride("panel", slotStyle);
            _slotStyles[i] = slotStyle;

            // Кулдаун-занавес (только техники).
            ColorRect? cdOverlay = null;
            if (!isWeapon)
            {
                cdOverlay = new ColorRect
                {
                    Color = new Color(0.02f, 0.02f, 0.04f, 0.6f),
                    MouseFilter = MouseFilterEnum.Ignore,
                    Visible = false,
                };
                cdOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
                cdOverlay.OffsetBottom = 0; // высота управляется из _Process
                slotPanel.AddChild(cdOverlay);
                _cdOverlays[i] = cdOverlay;
            }

            // Имя (центр).
            var label = new Label
            {
                Text = hotbarIndex.ToString(),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            label.AddThemeFontSizeOverride("font_size", 11);
            label.AddThemeColorOverride("font_color", new Color(0.9f, 0.86f, 0.78f));
            if (isWeapon)
            {
                // R15: оружие — иконка в центре, подпись снизу (§6.3 плана).
                label.VerticalAlignment = VerticalAlignment.Bottom;
                label.AddThemeFontSizeOverride("font_size", 8);
                label.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
                label.OffsetTop = -13; label.OffsetBottom = -1;
            }
            else
            {
                label.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
            }
            slotPanel.AddChild(label);

            // R15: иконка оружия (слоты 1-2, 32×32 по центру).
            if (isWeapon)
            {
                var icon = new TextureRect
                {
                    CustomMinimumSize = new Vector2(32, 32),
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    MouseFilter = MouseFilterEnum.Ignore,
                    TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                    Visible = false,
                };
                icon.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
                icon.OffsetLeft = -16; icon.OffsetRight = 16;
                icon.OffsetTop = -19; icon.OffsetBottom = 13;
                slotPanel.AddChild(icon);
                _weaponIcons[i] = icon;
            }

            // Цифра клавиши (верхний правый угол) — для техник и оружия.
            var keyLabel = new Label
            {
                Text = hotbarIndex.ToString(),
                MouseFilter = MouseFilterEnum.Ignore,
            };
            keyLabel.AddThemeFontSizeOverride("font_size", 9);
            keyLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.68f, 0.45f, 0.85f));
            keyLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
            keyLabel.OffsetLeft = -14; keyLabel.OffsetRight = -2;
            keyLabel.OffsetTop = 1; keyLabel.OffsetBottom = 12;
            slotPanel.AddChild(keyLabel);
            _keyLabels[i] = keyLabel;

            // Qi-строка (низ) — только техники.
            Label? qiLabel = null;
            Label? cdLabel = null;
            if (!isWeapon)
            {
                qiLabel = new Label
                {
                    Text = "",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                qiLabel.AddThemeFontSizeOverride("font_size", 9);
                qiLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
                qiLabel.OffsetTop = -14; qiLabel.OffsetBottom = -2;
                slotPanel.AddChild(qiLabel);
                _qiLabels[i] = qiLabel;

                cdLabel = new Label
                {
                    Text = "",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    MouseFilter = MouseFilterEnum.Ignore,
                    ZIndex = 5,
                };
                cdLabel.AddThemeFontSizeOverride("font_size", 17);
                cdLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.82f, 0.35f));
                cdLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
                cdLabel.AddThemeConstantOverride("outline_size", 2);
                cdLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
                slotPanel.AddChild(cdLabel);
                _cdLabels[i] = cdLabel;

                // === R26: индикация зарядки ===
                // Полоса заряда: снизу вверх, изумрудная (BottomWide).
                var chargeOverlay = new ColorRect
                {
                    Color = new Color(0.22f, 0.8f, 0.45f, 0.38f),
                    MouseFilter = MouseFilterEnum.Ignore,
                    Visible = false,
                    ZIndex = 2,
                };
                chargeOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
                chargeOverlay.OffsetTop = 0; // высота = прогресс (управляется из _Process)
                chargeOverlay.OffsetBottom = 0;
                slotPanel.AddChild(chargeOverlay);
                _chargeOverlays[i] = chargeOverlay;

                // Overcharge-полоска (верх слота, 4px, янтарь) — ChargedQi > QiCost.
                var ocBar = new ColorRect
                {
                    Color = new Color(0.98f, 0.72f, 0.3f, 0.9f),
                    MouseFilter = MouseFilterEnum.Ignore,
                    Visible = false,
                    ZIndex = 3,
                };
                ocBar.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
                ocBar.OffsetLeft = 2; ocBar.OffsetRight = -2;
                ocBar.OffsetTop = 2; ocBar.OffsetBottom = 6;
                slotPanel.AddChild(ocBar);
                _ocTopBars[i] = ocBar;

                // Подпись «87%» / «×1.3» (под центром, над Qi-строкой).
                var chargeLabel = new Label
                {
                    Text = "",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    MouseFilter = MouseFilterEnum.Ignore,
                    ZIndex = 6,
                };
                chargeLabel.AddThemeFontSizeOverride("font_size", 12);
                chargeLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.95f, 0.7f));
                chargeLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
                chargeLabel.AddThemeConstantOverride("outline_size", 2);
                chargeLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
                chargeLabel.OffsetTop = -6; chargeLabel.OffsetBottom = 10;
                slotPanel.AddChild(chargeLabel);
                _chargeLabels[i] = chargeLabel;

                // Метка удержания в ауре «◉» (левый-верхний угол).
                var heldMark = new Label
                {
                    Text = "◉",
                    MouseFilter = MouseFilterEnum.Ignore,
                    ZIndex = 6,
                    Visible = false,
                };
                heldMark.AddThemeFontSizeOverride("font_size", 11);
                heldMark.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.45f));
                heldMark.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
                heldMark.AddThemeConstantOverride("outline_size", 1);
                heldMark.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
                heldMark.OffsetLeft = 2; heldMark.OffsetRight = 14;
                heldMark.OffsetTop = 1; heldMark.OffsetBottom = 13;
                slotPanel.AddChild(heldMark);
                _heldMarks[i] = heldMark;

                // Клик по слоту техники = каст (аналог клавиши 3..9).
                int slotIndexForCast = hotbarIndex; // 3..9
                slotPanel.GuiInput += @event =>
                {
                    if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == Godot.MouseButton.Left)
                        CastFromSlot(slotIndexForCast);
                };
            }

            _slotLabels[i] = label;
            _slotPanels[i] = slotPanel;
            _baseBorders[i] = slotStyle.BorderColor; // R26: дефолт рамки (возврат)
            hbox.AddChild(slotPanel);
        }

        // === П5 (репорт 21.09): спец-слот МЕДИТАЦИИ (без номера) ===
        // Медитация — базовое действие культиватора, не техника слота 3-9:
        // пассивная техника Культивации показывается ЗДЕСЬ (☯ + V), не
        // занимая номерной слот быстрого доступа. Клик = тумблер медитации
        // (Publish MeditationToggleRequestedEvent — тот же путь, что V).
        _meditationPanel = new Panel
        {
            CustomMinimumSize = new Vector2(MainSlotSize, MainSlotSize),
            MouseFilter = MouseFilterEnum.Stop,
        };
        var medStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.13f, 0.16f, 0.13f, 0.92f), // медитативный тёмно-зелёный
        };
        medStyle.SetBorderWidthAll(1);
        medStyle.SetBorderColor(new Color(0.45f, 0.62f, 0.45f));
        medStyle.SetCornerRadiusAll(4);
        _meditationPanel.AddThemeStyleboxOverride("panel", medStyle);

        _meditationGlyph = new Label
        {
            Text = "☯",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _meditationGlyph.AddThemeFontSizeOverride("font_size", 22);
        _meditationGlyph.AddThemeColorOverride("font_color", new Color(0.72f, 0.9f, 0.72f));
        _meditationGlyph.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        _meditationGlyph.OffsetTop = -8; _meditationGlyph.OffsetBottom = 14;
        _meditationPanel.AddChild(_meditationGlyph);

        var medKeyLabel = new Label
        {
            Text = "V",
            MouseFilter = MouseFilterEnum.Ignore,
        };
        medKeyLabel.AddThemeFontSizeOverride("font_size", 9);
        medKeyLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.88f, 0.75f, 0.9f));
        medKeyLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
        medKeyLabel.OffsetLeft = -14; medKeyLabel.OffsetRight = -2;
        medKeyLabel.OffsetTop = 1; medKeyLabel.OffsetBottom = 12;
        _meditationPanel.AddChild(medKeyLabel);

        _meditationNameLabel = new Label
        {
            Text = "медитация",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _meditationNameLabel.AddThemeFontSizeOverride("font_size", 8);
        _meditationNameLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.88f, 0.8f));
        _meditationNameLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        _meditationNameLabel.OffsetTop = -12; _meditationNameLabel.OffsetBottom = -1;
        _meditationPanel.AddChild(_meditationNameLabel);

        // Клик по спец-слоту = тумблер медитации (как V).
        _meditationPanel.GuiInput += @event =>
        {
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == Godot.MouseButton.Left)
                _meditationTogglePub?.Publish(
                    new Core.Messaging.Contracts.MeditationToggleRequestedEvent(!_meditationActive));
        };
        hbox.AddChild(_meditationPanel);

        UpdatePanelSize(beltVisible: false);
    }

    /// <summary>Каст техники из слота (клик мышью = клавише 3..9).</summary>
    private void CastFromSlot(int slotIndex)
    {
        string? techId = TechniqueSlots?.GetTechniqueAtSlot(slotIndex);
        if (string.IsNullOrEmpty(techId) || CastPub == null) return;
        var mouse = GetViewport().GetMousePosition();
        CastPub.Publish(new TechniqueCastRequestedEvent(techId, (int)(mouse.X * 1000), (int)(mouse.Y * 1000)));
    }

    /// <summary>
    /// Ежекадровое обновление кулдаунов/Qi-достаточности техник (7 слотов —
    /// дёшево; текст обновляется только при изменении — кэш).
    /// </summary>
    public override void _Process(double delta)
    {
        if (TechniqueSlots == null || Techniques == null) return;
        for (int i = 2; i < 9; i++)
        {
            int slotIndex = i + 1; // 3..9
            string? techId = TechniqueSlots.GetTechniqueAtSlot(slotIndex);
            var tech = techId != null ? Techniques.GetTechnique(techId) : null;

            // 1) Кулдаун-занавес + цифры.
            float remaining = tech != null ? Techniques.GetCooldown(techId!) : 0f;
            float total = tech?.Cooldown ?? 0f;
            var overlay = _cdOverlays[i];
            var cdLabel = _cdLabels[i];
            if (overlay != null && cdLabel != null)
            {
                if (tech != null && remaining > 0f)
                {
                    float ratio = total > 0f ? Godot.Mathf.Min(remaining / total, 1f) : 1f;
                    overlay.Visible = true;
                    float h = MainSlotSize * ratio;
                    overlay.OffsetTop = 0;
                    overlay.OffsetBottom = h;
                    string cdText = remaining >= 10f
                        ? remaining.ToString("F0")
                        : remaining.ToString("F1");
                    if (_cdCache[i] != cdText)
                    {
                        _cdCache[i] = cdText;
                        cdLabel.Text = cdText;
                    }
                    cdLabel.Visible = true;
                }
                else
                {
                    overlay.Visible = false;
                    cdLabel.Visible = false;
                    if (_cdCache[i] != null)
                    {
                        _cdCache[i] = null;
                        cdLabel.Text = "";
                    }
                }
            }

            // 2) Qi-достаточность.
            var qiLabel = _qiLabels[i];
            if (qiLabel != null && tech != null)
            {
                bool affordable = Qi != null && Qi.CurrentQi >= tech.QiCost;
                string qiText = $"Ци {FormatQi(tech.QiCost)}";
                if (qiLabel.Text != qiText)
                    qiLabel.Text = qiText;
                qiLabel.AddThemeColorOverride("font_color", affordable
                    ? new Color(0.55f, 0.85f, 0.55f, 0.95f)
                    : new Color(0.95f, 0.35f, 0.3f, 0.95f));
            }

            // === R26: индикация зарядки / удержания в ауре ===
            UpdateChargeVisuals(i, techId, delta);
        }
    }

    /// <summary>
    /// R26: визуал зарядки/удержания СЛОТА (идея 1 «слот-центричная»):
    /// заряд — изумрудная полоса снизу + «%» (overcharge: янтарная полоска
    /// сверху + «×N.N»), пульс рамки; удержание — рамка цвета стихии +
    /// метка «◉». Прочие слоты — сброс к дефолту (кэш-гварды против
    /// ежекадровых Text/BorderColor-мутаций).
    /// </summary>
    private void UpdateChargeVisuals(int i, string? techId, double delta)
    {
        _pulseTime += (float)delta;
        var chargeOverlay = _chargeOverlays[i];
        var ocBar = _ocTopBars[i];
        var chargeLabel = _chargeLabels[i];
        var heldMark = _heldMarks[i];
        var style = _slotStyles[i];
        if (chargeOverlay == null || ocBar == null || chargeLabel == null
            || heldMark == null || style == null) return;

        bool charging = techId != null && techId == _chargingTechId;
        bool held = techId != null && techId == _heldTechId;

        // --- Заряд идёт ---
        if (charging && _chargeCost > 0)
        {
            // Полоса: 0..QiCost = вся высота слота (потолок Capacity — выше 100%
            // полоса НЕ растёт: overcharge живёт отдельной янтарной полоской).
            long fill = System.Math.Min(_chargeQi, _chargeCost);
            float ratio = (float)((double)fill / _chargeCost);
            float h = MainSlotSize * Godot.Mathf.Clamp(ratio, 0f, 1f);
            chargeOverlay.Visible = true;
            chargeOverlay.OffsetTop = -h;   // BottomWide: растём вверх от низа
            chargeOverlay.OffsetBottom = 0;

            // Overcharge: ChargedQi > QiCost → potency 1000..2000‰.
            bool over = _chargeQi > _chargeCost;
            ocBar.Visible = over;

            string text;
            if (over && _chargePotency > GameConstants.POTENCY_BASE_PERMIL)
            {
                // «×1.3» — множитель мощности (potency/1000).
                text = $"×{(Godot.Mathf.Round(_chargePotency / 100f) / 10f):F1}";
                chargeLabel.AddThemeColorOverride("font_color", new Color(0.98f, 0.78f, 0.35f));
            }
            else
            {
                int pct = (int)(ratio * 100f);
                text = $"{pct}%";
                chargeLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.95f, 0.7f));
            }
            if (_nameCache[i] != text) { chargeLabel.Text = text; }

            // Пульс рамки (изумруд).
            float pulse = 0.6f + 0.4f * System.Math.Abs(Godot.Mathf.Sin(_pulseTime * 3.2f));
            style.BorderColor = new Color(0.3f * pulse + 0.2f, 0.85f * pulse, 0.5f * pulse, 0.9f);
        }
        else
        {
            chargeOverlay.Visible = false;
            ocBar.Visible = false;
            if (_nameCache[i] != null && chargeLabel.Text != "") chargeLabel.Text = "";
        }

        // --- Удержание в ауре ---
        heldMark.Visible = held;
        if (held)
        {
            // Рамка цвета стихии удерживаемой техники (пульс медленнее).
            var elemColor = ElementStyle.ElementColor(_heldElement);
            float pulse = 0.55f + 0.45f * System.Math.Abs(Godot.Mathf.Sin(_pulseTime * 1.6f));
            if (!charging) // заряд приоритетнее по рамке
                style.BorderColor = new Color(
                    elemColor.R, elemColor.G, elemColor.B, 0.55f + 0.45f * pulse);
            heldMark.SelfModulate = new Color(1f, 1f, 1f, 0.6f + 0.4f * pulse);
        }

        // --- Сброс рамки к дефолту (нет заряда/удержания) ---
        if (!charging && !held)
        {
            style.BorderColor = _baseBorders[i];
        }
    }

    // === R26: обработчики событий зарядки/удержания ===

    private void OnChargeStarted(in TechniqueChargeStartedEvent e)
    {
        if (e.EntityId is not ("player" or "player_0")) return; // HUD только игрока
        _chargingTechId = e.TechniqueId;
        _chargeQi = 0;
        _chargeCost = System.Math.Max(1, e.QiCost);
        _chargeCapacity = e.Capacity > 0 ? e.Capacity : e.QiCost;
        _chargePotency = GameConstants.POTENCY_BASE_PERMIL;
    }

    private void OnChargeProgress(in TechniqueChargeProgressEvent e)
    {
        if (e.EntityId is not ("player" or "player_0")) return;
        if (e.TechniqueId != _chargingTechId) return; // чужая/устаревшая
        _chargeQi = e.ChargedQi;
        _chargeCost = System.Math.Max(1, e.QiCost);
        _chargePotency = e.PotencyPermil;
    }

    private void OnChargeCompleted(in TechniqueChargeCompletedEvent e)
    {
        if (e.EntityId is not ("player" or "player_0")) return;
        // Зарядка завершена: либо Hold в ауре (придёт HeldTechniqueChanged),
        // либо немедленный выпуск — индикатор заряда гасим.
        if (e.TechniqueId == _chargingTechId)
            _chargingTechId = null;
    }

    private void OnChargeCancelled(in TechniqueChargeCancelledEvent e)
    {
        if (e.EntityId is not ("player" or "player_0")) return;
        if (e.TechniqueId == _chargingTechId)
            _chargingTechId = null;
    }

    private void OnHeldChanged(in HeldTechniqueChangedEvent e)
    {
        if (e.EntityId is not ("player" or "player_0")) return;
        if (string.IsNullOrEmpty(e.TechniqueId))
        {
            _heldTechId = null; // аура пуста (release/dissipate)
        }
        else
        {
            _heldTechId = e.TechniqueId;
            _heldElement = e.Element;
            _heldPotency = e.PotencyPermil;
        }
    }

    // === П5 (репорт 21.09): спец-слот медитации ======================

    /// <summary>Медитация вкл/выкл — пульс глифа и подсветка рамки.</summary>
    private void OnMeditationStateChanged(in Core.Messaging.Contracts.MeditationStateChangedEvent e)
    {
        _meditationActive = e.IsActive;
        UpdateMeditationVisuals();
    }

    /// <summary>Изучена техника — если Культивация, обновить спец-слот.</summary>
    private void OnTechniqueLearned(in TechniqueLearnedEvent e)
    {
        if (e.Type == Core.Data.TechniqueType.Cultivation)
            RefreshMeditationSlot();
    }

    /// <summary>Найти изученную пассивную технику Культивации и показать
    /// её в спец-слоте (медитация не занимает номерные слоты 3-9 — П5).</summary>
    private void RefreshMeditationSlot()
    {
        if (_meditationPanel == null) return;
        string? found = null;
        string? name = null;
        var all = Techniques?.GetAllTechniques();
        if (all != null)
        {
            foreach (var kvp in all)
            {
                if (kvp.Value is { Type: Core.Data.TechniqueType.Cultivation })
                {
                    found = kvp.Key;
                    name = kvp.Value.Name;
                    break;
                }
            }
        }
        _meditationTechId = found;
        if (_meditationNameLabel != null)
            _meditationNameLabel.Text = found != null && !string.IsNullOrEmpty(name)
                ? Truncate(name, 12)
                : "медитация";
        UpdateMeditationVisuals();
    }

    /// <summary>Активная медитация — золотая рамка + тёплый глиф.</summary>
    private void UpdateMeditationVisuals()
    {
        if (_meditationPanel == null) return;
        if (_meditationPanel.GetThemeStylebox("panel") is StyleBoxFlat sb)
        {
            sb.BorderColor = _meditationActive
                ? new Color(0.95f, 0.85f, 0.45f)
                : new Color(0.45f, 0.62f, 0.45f);
            sb.BgColor = _meditationActive
                ? new Color(0.16f, 0.2f, 0.15f, 0.95f)
                : new Color(0.13f, 0.16f, 0.13f, 0.92f);
        }
        if (_meditationGlyph != null)
            _meditationGlyph.AddThemeColorOverride("font_color",
                _meditationActive ? new Color(0.98f, 0.92f, 0.55f) : new Color(0.72f, 0.9f, 0.72f));
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s.Substring(0, max - 1) + "…";

    private static string FormatQi(long qi) =>
        qi >= 10_000 ? $"{qi / 1000}к" : qi.ToString();

    // === События ===

    private void OnTechSlotAssigned(in TechniqueSlotAssignedEvent e)
    {
        if (e.SlotIndex < 3 || e.SlotIndex > 9) return;
        RefreshTechSlot(e.SlotIndex);
    }

    private void OnTechSlotCleared(in TechniqueSlotClearedEvent e)
    {
        if (e.SlotIndex < 3 || e.SlotIndex > 9) return;
        RefreshTechSlot(e.SlotIndex);
    }

    private void OnBeltSlotsChanged(in Core.Messaging.Contracts.BeltSlotsChangedEvent e)
    {
        // Slot 0-6 → hotbar 3-9.
        int hotbarIdx = e.SlotIndex + BeltService.HotbarFirstIndex - 1;
        if (hotbarIdx is < 2 or > 8) return;
        RefreshBeltSlot(e.SlotIndex);
    }

    private void OnEquipChanged(in Core.Messaging.Contracts.EquipmentChangedEvent e)
    {
        if (e.Slot == Core.Data.EquipmentSlot.Belt) { RefreshAll(); return; }
        // R15: смена оружия → иконки слотов 1-2.
        if (e.Slot is Core.Data.EquipmentSlot.WeaponMain or Core.Data.EquipmentSlot.WeaponOff)
        {
            RefreshWeapon(0, Core.Data.EquipmentSlot.WeaponMain);
            RefreshWeapon(1, Core.Data.EquipmentSlot.WeaponOff);
        }
    }

    // === RefreshAll ===

    private void RefreshAll()
    {
        // Weapons (1-2).
        RefreshWeapon(0, Core.Data.EquipmentSlot.WeaponMain);
        RefreshWeapon(1, Core.Data.EquipmentSlot.WeaponOff);

        // Techniques (3-9).
        for (int slotIndex = 3; slotIndex <= 9; slotIndex++)
            RefreshTechSlot(slotIndex);

        // П5: спец-слот медитации (пассивная техника Культивации).
        RefreshMeditationSlot();

        // Belt row — visibility gate + содержимое.
        bool beltOn = Belt is { IsBeltEquipped: true };
        if (_beltRow != null)
        {
            _beltRow.Visible = beltOn;
            UpdatePanelSize(beltOn);
        }
        var slots = Belt?.GetSlots();
        for (int i = 0; i < BeltService.SlotCount; i++)
        {
            _beltPanels[i].Visible = beltOn;
            if (beltOn && slots != null && i < slots.Count)
                SetBeltSlotText(i, slots[i]);
            else
                _beltLabels[i].Text = (BeltService.HotbarFirstIndex + i).ToString();
        }
    }

    private void RefreshTechSlot(int slotIndex)
    {
        int i = slotIndex - 1; // panel index 2..8
        if (i < 2 || i > 8) return;
        string? techId = TechniqueSlots?.GetTechniqueAtSlot(slotIndex);
        var tech = techId != null ? Techniques?.GetTechnique(techId) : null;

        if (tech != null)
        {
            string shortName = ShortName(tech.Name, 5);
            string nameText = $"{ElementStyle.ElementEmoji(tech.Element)} {shortName}";
            if (_nameCache[i] != nameText)
            {
                _nameCache[i] = nameText;
                _slotLabels[i].Text = nameText;
            }
            // Рамка = цвет стихии (готовность видна по кулдаун-оверлею).
            _slotStyles[i].SetBorderColor(ElementStyle.ElementColor(tech.Element));
            _slotPanels[i].TooltipText =
                $"{tech.Name} ({ElementStyle.ElementName(tech.Element)}{ElementStyle.GradeName(tech.Grade)})\n" +
                $"Ци: {tech.QiCost}   Кулдаун: {tech.Cooldown:F1}с\n" +
                $"Урон: {tech.BaseDamage}   Дальность: {tech.Range:F1}м\n" +
                $"Мастерство: {tech.Mastery:F1}/100" +
                (tech.IsUltimate ? "\n★ Ultimate" : "") +
                $"\nКлавиша {slotIndex} или клик — применить";
        }
        else
        {
            // Безусловно: изначальный текст — цифра клавиши; пустой слот всегда «—».
            _nameCache[i] = null;
            _slotLabels[i].Text = "—";
            _slotStyles[i].SetBorderColor(new Color(0.42f, 0.38f, 0.32f));
            _slotPanels[i].TooltipText = $"Слот {slotIndex}: пусто — назначьте технику в окне Культивации (K)";
            if (_qiLabels[i] != null) _qiLabels[i].Text = "";
        }
    }

    private void RefreshWeapon(int idx, Core.Data.EquipmentSlot slot)
    {
        var equipped = Equipment?.GetEquipped(slot);
        var icon = _weaponIcons[idx];
        if (equipped != null)
        {
            _slotLabels[idx].Text = ShortName(equipped.NameRu, 6);
            // R15: иконка из каталога (класс+тир+редкость).
            var visuals = CultivationGame.Adapter.Scene.WeaponVisualCatalog.Resolve(equipped);
            if (icon != null)
            {
                icon.Texture = visuals?.Icon;
                icon.Visible = visuals != null;
            }
            _weaponIconKeys[idx] = visuals?.Key;
        }
        else
        {
            _slotLabels[idx].Text = (idx + 1).ToString();
            if (icon != null) { icon.Texture = null; icon.Visible = false; }
            _weaponIconKeys[idx] = null;
        }
        _slotPanels[idx].TooltipText = equipped != null
            ? $"{equipped.NameRu} (слот {idx + 1})\n1/2 — выбор режима атаки"
            : $"Оружие не экипировано (слот {idx + 1})\n1/2 — выбор режима атаки";
    }

    private void SetBeltSlotText(int beltIndex, BeltSlot slot)
    {
        string text;
        if (slot is { Count: > 0 } && ItemDb != null && ItemDb.TryGetItem(slot.ItemId, out var item))
        {
            text = $"{ShortName(item.NameRu, 4)}×{slot.Count}";
            _beltPanels[beltIndex].TooltipText = $"{item.NameRu} ×{slot.Count}\nСлот пояса {BeltService.HotbarFirstIndex + beltIndex} (⇧+цифра или клик) — использовать";
        }
        else
        {
            text = (BeltService.HotbarFirstIndex + beltIndex).ToString();
            _beltPanels[beltIndex].TooltipText = null;
        }
        _beltLabels[beltIndex].Text = text;
    }

    private void RefreshBeltSlot(int beltIndex)
    {
        if (beltIndex < 0 || beltIndex >= BeltService.SlotCount) return;
        var slots = Belt?.GetSlots();
        if (slots != null && beltIndex < slots.Count)
            SetBeltSlotText(beltIndex, slots[beltIndex]);
    }

    /// <summary>Пересчёт размеров панели при появлении/скрытии ряда пояса.</summary>
    private void UpdatePanelSize(bool beltVisible)
    {
        float mainWidth = 9 * MainSlotSize + 8 * 4 + 2 * PanelPad;
        float beltWidth = 44 + 4 + BeltService.SlotCount * BeltSlotSize + (BeltService.SlotCount - 1) * 4 + 2 * PanelPad;
        float width = Godot.Mathf.Max(mainWidth, beltWidth);
        float height = beltVisible
            ? MainSlotSize + BeltSlotSize + RowGap + 2 * PanelPad
            : MainSlotSize + 2 * PanelPad;

        CustomMinimumSize = new Vector2(width, height);
        OffsetLeft = -width / 2f;
        OffsetRight = width / 2f;
        OffsetTop = -height - 4;
        OffsetBottom = -4;
    }

    private static string ShortName(string name, int maxChars) =>
        name.Length <= maxChars ? name : name.Substring(0, maxChars) + "…";

    // === Public QA API (headless-верификация GODOT_HOTBAR_DEBUG=1) ===

    /// <summary>Текст слота техники (имя или «—»; hotbarSlot 3..9).</summary>
    public string? TechSlotName(int hotbarSlot)
    {
        int i = hotbarSlot - 1;
        return i is >= 2 and <= 8 ? _slotLabels[i]?.Text : null;
    }

    /// <summary>Отношение оставшегося кулдауна к полному (0..1; hotbarSlot 3..9).</summary>
    public float TechCooldownRatio(int hotbarSlot)
    {
        int i = hotbarSlot - 1;
        if (i is < 2 or > 8) return -1f;
        string? techId = TechniqueSlots?.GetTechniqueAtSlot(hotbarSlot);
        if (techId == null) return -1f;
        var tech = Techniques?.GetTechnique(techId);
        if (tech == null || tech.Cooldown <= 0f) return -1f;
        float remaining = Techniques!.GetCooldown(techId);
        return Godot.Mathf.Min(remaining / tech.Cooldown, 1f);
    }

    /// <summary>Цифры кулдауна на слоте (пусто = готов; hotbarSlot 3..9).</summary>
    public string? TechCooldownLabel(int hotbarSlot)
    {
        int i = hotbarSlot - 1;
        if (i is < 2 or > 8) return null;
        var cd = _cdLabels[i];
        return cd is { Visible: true } ? cd.Text : "";
    }

    /// <summary>Не хватает Ци на технику в слоте (hotbarSlot 3..9).</summary>
    public bool TechQiInsufficient(int hotbarSlot)
    {
        int i = hotbarSlot - 1;
        if (i is < 2 or > 8) return false;
        string? techId = TechniqueSlots?.GetTechniqueAtSlot(hotbarSlot);
        if (techId == null) return false;
        var tech = Techniques?.GetTechnique(techId);
        return tech != null && Qi != null && Qi.CurrentQi < tech.QiCost;
    }

    /// <summary>Видимость ряда пояса (гейт по наличию пояса).</summary>
    public bool BeltRowVisible => _beltRow?.Visible ?? false;

    // === R15: QA-доступ (GODOT_WEAPONVIS_DEBUG) ===

    /// <summary>Ключ иконки оружия в слоте 1-2 (null — пусто; hotbarSlot 1..2).</summary>
    public string? WeaponIconTextureId(int hotbarSlot)
    {
        int i = hotbarSlot - 1;
        return i is >= 0 and <= 1 ? _weaponIconKeys[i] : null;
    }

    /// <summary>Текст слота пояса (beltIndex 0..6).</summary>
    public string? BeltSlotText(int beltIndex)
    {
        return beltIndex >= 0 && beltIndex < BeltService.SlotCount
            ? _beltLabels[beltIndex]?.Text
            : null;
    }
}

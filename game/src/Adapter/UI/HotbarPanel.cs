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
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.DI;
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

    // === Belt row (7 slots) ===
    private readonly Panel[] _beltPanels = new Panel[BeltService.SlotCount];
    private readonly Label[] _beltLabels = new Label[BeltService.SlotCount];
    private HBoxContainer? _beltRow;

    private System.IDisposable? _techAssignedToken;
    private System.IDisposable? _techClearedToken;
    private System.IDisposable? _slotsToken;
    private System.IDisposable? _equipToken;

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

        RefreshAll();
        GD.Print("[HotbarPanel] Ready (v2: techniques + cooldowns + belt row)");
    }

    public override void _ExitTree()
    {
        _techAssignedToken?.Dispose();
        _techClearedToken?.Dispose();
        _slotsToken?.Dispose();
        _equipToken?.Dispose();
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
            label.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
            slotPanel.AddChild(label);

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
            hbox.AddChild(slotPanel);
        }

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
        }
    }

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
        if (e.Slot != Core.Data.EquipmentSlot.Belt) return;
        RefreshAll();
    }

    // === Обновление ===

    private void RefreshAll()
    {
        // Weapons (1-2).
        RefreshWeapon(0, Core.Data.EquipmentSlot.WeaponMain);
        RefreshWeapon(1, Core.Data.EquipmentSlot.WeaponOff);

        // Techniques (3-9).
        for (int slotIndex = 3; slotIndex <= 9; slotIndex++)
            RefreshTechSlot(slotIndex);

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
        _slotLabels[idx].Text = equipped != null
            ? ShortName(equipped.NameRu, 6)
            : (idx + 1).ToString();
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

    /// <summary>Текст слота пояса (beltIndex 0..6).</summary>
    public string? BeltSlotText(int beltIndex)
    {
        return beltIndex >= 0 && beltIndex < BeltService.SlotCount
            ? _beltLabels[beltIndex]?.Text
            : null;
    }
}

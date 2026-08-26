#nullable enable
// Создано: 2026-08-26 — C3-C7 (окно Культивации Ци, этап C).
//
// CultivationWindow — отдельное окно (как инвентарь), открывается клавишей K.
// Содержит 3 вкладки + нижнюю панель слотов техник:
//
//   ┌─ CultivationWindow (Control, скрыт по умолчанию) ───────────┐
//   │  [Вкладки: Техники | Меридианы | Ядро]      [× Закрыть]      │
//   ├──────────────────────────────────────────────────────────────┤
//   │ ┌─ Левая (40%) ───────┐  ┌─ Правая (60%) ──────────────┐  │
//   │ │ Список изученных    │  │ Характеристики выбранной    │  │
//   │ │ техник (scroll)     │  │ техники: тип, грейд, ур-нь, │  │
//   │ │  • Меч Ветра L1      │  │ стихия, qiCost, кулдаун,   │  │
//   │ │  • Щит Земли L1      │  │ мощность, мастерство        │  │
//   │ │  • ...               │  │                              │  │
//   │ │                      │  │ [Установить в слот ▼] → 3-9  │  │
//   │ └──────────────────────┘  └──────────────────────────────┘  │
//   ├──────────────────────────────────────────────────────────────┤
//   │ Панель слотов техник (3..9, горизонтальная, 7 ячеек)        │
//   │  [3] [4] [5] [6] [7] [8] [9]                                │
//   └──────────────────────────────────────────────────────────────┘
//
// Архитектура: Adapter слой (Godot Control). Подписки на события через EventBus.
// Данные: TechniqueService (список/детали), QiChangedEvent (меридианы/ядро),
// TechniqueSlotService (слоты). Паузит игру при открытии (как InventoryWindow).
using Godot;
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Helpers;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Adapter.Di;
using CultivationGame.Modules.Combat;
using CultivationGame.Modules.Player;

namespace CultivationGame.Adapter.UI;

/// <summary>
/// Окно Культивации Ци (C3-C7, 2026-08-26).
/// Открывается клавишей K (CultivationWindowToggleRequestedEvent).
/// 3 вкладки: Техники / Меридианы / Ядро + панель слотов техник (3-9).
/// </summary>
public partial class CultivationWindow : Control
{
    // === DI ===
    [Inject] private TechniqueService Techniques = null!;
    [Inject] private TechniqueSlotService Slots = null!;
    [Inject] private IQiService Qi = null!;
    [Inject] private IPlayerService Player = null!;
    [Inject] private IPublisher<TechniqueSlotAssignedEvent> SlotAssignedPub = null!;
    [Inject] private IPublisher<TechniqueSlotClearedEvent> SlotClearedPub = null!;
    [Inject] private IPublisher<ToastShownEvent> ToastPub = null!;
    [Inject] private ISubscriber<CultivationWindowToggleRequestedEvent> ToggleSub = null!;
    [Inject] private ISubscriber<TechniqueLearnedEvent> LearnedSub = null!;
    [Inject] private ISubscriber<TechniqueForgottenEvent> ForgottenSub = null!;
    [Inject] private ISubscriber<QiChangedEvent> QiSub = null!;
    [Inject] private ISubscriber<TechniqueSlotAssignedEvent> SlotAssignedSub = null!;
    [Inject] private ISubscriber<TechniqueSlotClearedEvent> SlotClearedSub = null!;

    // === UI nodes ===
    private TabContainer _tabs = null!;
    private ItemList _techniqueList = null!;
    private Label _techniqueDetails = null!;
    private OptionButton _slotSelector = null!;
    private Button _assignButton = null!;
    private Label _meridiansLabel = null!;
    private Label _coreLabel = null!;
    private HBoxContainer _slotBar = null!;
    private readonly Label[] _slotLabels = new Label[TechniqueSlotService.SlotCount];
    private readonly Panel[] _slotPanels = new Panel[TechniqueSlotService.SlotCount];

    // === Подписки ===
    private IDisposable? _toggleToken;
    private IDisposable? _learnedToken;
    private IDisposable? _forgottenToken;
    private IDisposable? _qiToken;
    private IDisposable? _slotAssignedToken;
    private IDisposable? _slotClearedToken;

    // === Состояние ===
    private string? _selectedTechId;
    private long _cachedCurrentQi;
    private long _cachedMaxQi;
    private float _cachedConductivity;
    private int _cachedCultivationLevel = 1;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        BuildUI();

        _toggleToken = ToggleSub?.Subscribe(OnToggle);
        _learnedToken = LearnedSub?.Subscribe(OnLearned);
        _forgottenToken = ForgottenSub?.Subscribe(OnForgotten);
        _qiToken = QiSub?.Subscribe(OnQiChanged);
        _slotAssignedToken = SlotAssignedSub?.Subscribe(OnSlotAssigned);
        _slotClearedToken = SlotClearedSub?.Subscribe(OnSlotCleared);

        Visible = false;
        RebuildTechniqueList();
        RefreshSlots();
        RefreshMeridiansAndCore();
        GD.Print("[CultivationWindow] Ready");
    }

    public override void _ExitTree()
    {
        _toggleToken?.Dispose();
        _learnedToken?.Dispose();
        _forgottenToken?.Dispose();
        _qiToken?.Dispose();
        _slotAssignedToken?.Dispose();
        _slotClearedToken?.Dispose();
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        // Обновляем кулдауны на лету (как TechniquesPanel).
        RefreshSelectedTechniqueDetails();
    }

    // === UI Construction ===

    private void BuildUI()
    {
        // Центр экрана, большой размер.
        SetAnchorsPreset(Control.LayoutPreset.Center);
        CustomMinimumSize = new Vector2(820, 560);
        OffsetLeft = -410; OffsetRight = 410;
        OffsetTop = -280; OffsetBottom = 280;
        MouseFilter = MouseFilterEnum.Stop;

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.10f, 0.08f, 0.05f, 0.95f),
        };
        style.SetBorderWidthAll(2);
        style.SetBorderColor(new Color(0.55f, 0.42f, 0.20f, 0.9f));
        style.SetCornerRadiusAll(8);
        AddThemeStyleboxOverride("panel", style);

        var root = new VBoxContainer();
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 8; root.OffsetRight = -8;
        root.OffsetTop = 8; root.OffsetBottom = -8;
        root.AddThemeConstantOverride("separation", 6);
        AddChild(root);

        // Header: title + close button
        var header = new HBoxContainer();
        header.Alignment = BoxContainer.AlignmentMode.Begin;
        var title = new Label { Text = "◆  Культивация Ци  ◆" };
        title.AddThemeFontSizeOverride("font_size", 20);
        title.AddThemeColorOverride("font_color", new Color(0.95f, 0.80f, 0.45f));
        header.AddChild(title);

        var spacer = new Control();
        spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(spacer);

        var closeBtn = new Button { Text = "× Закрыть (K)" };
        closeBtn.Pressed += Close;
        header.AddChild(closeBtn);
        root.AddChild(header);

        // Tabs: Техники / Меридианы / Ядро
        _tabs = new TabContainer();
        _tabs.SizeFlagsVertical = SizeFlags.ExpandFill;
        root.AddChild(_tabs);

        BuildTechniquesTab();
        BuildMeridiansTab();
        BuildCoreTab();

        // Bottom: slot bar (3-9)
        var slotLabel = new Label { Text = "Слоты быстрого доступа техник (клавиши 3–9):" };
        slotLabel.AddThemeFontSizeOverride("font_size", 14);
        slotLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.75f, 0.55f));
        root.AddChild(slotLabel);

        _slotBar = new HBoxContainer();
        _slotBar.Alignment = BoxContainer.AlignmentMode.Center;
        _slotBar.AddThemeConstantOverride("separation", 4);
        root.AddChild(_slotBar);

        for (int i = 0; i < TechniqueSlotService.SlotCount; i++)
        {
            int slotIndex = TechniqueSlotService.MinSlot + i;
            var panel = new Panel
            {
                CustomMinimumSize = new Vector2(108, 56),
                MouseFilter = MouseFilterEnum.Stop,
            };
            var slotStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.18f, 0.16f, 0.10f, 0.9f),
            };
            slotStyle.SetBorderWidthAll(1);
            slotStyle.SetBorderColor(new Color(0.55f, 0.42f, 0.20f));
            slotStyle.SetCornerRadiusAll(4);
            panel.AddThemeStyleboxOverride("panel", slotStyle);

            var vbox = new VBoxContainer();
            vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            vbox.OffsetLeft = 4; vbox.OffsetRight = -4;
            vbox.OffsetTop = 2; vbox.OffsetBottom = -2;
            vbox.AddThemeConstantOverride("separation", 1);
            vbox.Alignment = BoxContainer.AlignmentMode.Center;
            panel.AddChild(vbox);

            var hotkeyLabel = new Label
            {
                Text = slotIndex.ToString(),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            hotkeyLabel.AddThemeFontSizeOverride("font_size", 14);
            hotkeyLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.75f, 0.55f));
            vbox.AddChild(hotkeyLabel);

            var techNameLabel = new Label
            {
                Text = "—",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            techNameLabel.AddThemeFontSizeOverride("font_size", 10);
            techNameLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.88f, 0.80f));
            techNameLabel.CustomMinimumSize = new Vector2(96, 28);
            vbox.AddChild(techNameLabel);

            // ЛКМ по слоту → снять технику (если занят)
            int captured = slotIndex;
            panel.GuiInput += @event =>
            {
                if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == Godot.MouseButton.Left)
                {
                    var techId = Slots?.GetTechniqueAtSlot(captured);
                    if (!string.IsNullOrEmpty(techId) && Slots != null)
                    {
                        Slots.ClearSlot(captured);
                        ToastPub?.Publish(new ToastShownEvent($"Слот {captured}: очищен", 1.5f));
                    }
                }
            };

            _slotLabels[i] = techNameLabel;
            _slotPanels[i] = panel;
            _slotBar.AddChild(panel);
        }
    }

    private void BuildTechniquesTab()
    {
        var tab = new VBoxContainer { Name = "Техники" };
        _tabs.AddChild(tab);

        var split = new HSplitContainer();
        split.SizeFlagsVertical = SizeFlags.ExpandFill;
        split.SplitOffset = 320;
        tab.AddChild(split);

        // Left: list
        _techniqueList = new ItemList
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _techniqueList.ItemSelected += idx =>
        {
            if (idx >= 0 && idx < _techniqueList.ItemCount)
            {
                _selectedTechId = (string?)_techniqueList.GetItemMetadata((int)idx);
                RefreshSelectedTechniqueDetails();
            }
        };
        split.AddChild(_techniqueList);

        // Right: details + assign
        var right = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        split.AddChild(right);

        _techniqueDetails = new Label
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _techniqueDetails.AddThemeFontSizeOverride("font_size", 13);
        right.AddChild(_techniqueDetails);

        var assignRow = new HBoxContainer();
        assignRow.AddThemeConstantOverride("separation", 6);
        var assignLabel = new Label { Text = "В слот:" };
        assignLabel.AddThemeFontSizeOverride("font_size", 13);
        assignRow.AddChild(assignLabel);

        _slotSelector = new OptionButton();
        for (int i = TechniqueSlotService.MinSlot; i <= TechniqueSlotService.MaxSlot; i++)
            _slotSelector.AddItem(i.ToString());
        assignRow.AddChild(_slotSelector);

        _assignButton = new Button { Text = "Установить" };
        _assignButton.Pressed += OnAssignPressed;
        assignRow.AddChild(_assignButton);
        right.AddChild(assignRow);
    }

    private void BuildMeridiansTab()
    {
        var tab = new VBoxContainer { Name = "Меридианы" };
        _tabs.AddChild(tab);

        _meridiansLabel = new Label
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _meridiansLabel.AddThemeFontSizeOverride("font_size", 14);
        tab.AddChild(_meridiansLabel);
    }

    private void BuildCoreTab()
    {
        var tab = new VBoxContainer { Name = "Ядро" };
        _tabs.AddChild(tab);

        _coreLabel = new Label
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _coreLabel.AddThemeFontSizeOverride("font_size", 14);
        tab.AddChild(_coreLabel);
    }

    // === Event handlers ===

    private void OnToggle(in CultivationWindowToggleRequestedEvent e)
    {
        if (e.Open) Open();
        else Close();
    }

    /// <summary>Открыть окно (вызов из GameWorldController по клавише K или из события).</summary>
    public void Open()
    {
        if (Visible) return;
        Visible = true;
        RebuildTechniqueList();
        RefreshSlots();
        RefreshMeridiansAndCore();
        ToastPub?.Publish(new ToastShownEvent("Окно Культивации открыто", 1.0f));
    }

    /// <summary>Закрыть окно.</summary>
    public void Close()
    {
        Visible = false;
    }

    /// <summary>Переключить видимость (K / кнопка «Закрыть»).</summary>
    public void Toggle()
    {
        if (Visible) Close();
        else Open();
    }

    private void OnLearned(in TechniqueLearnedEvent e)
    {
        if (!Visible) return;
        RebuildTechniqueList();
    }

    private void OnForgotten(in TechniqueForgottenEvent e)
    {
        if (!Visible) return;
        RebuildTechniqueList();
        if (_selectedTechId == e.TechniqueId) _selectedTechId = null;
        RefreshSelectedTechniqueDetails();
        RefreshSlots();
    }

    private void OnQiChanged(in QiChangedEvent e)
    {
        // B1: нормализация ID (QiChangedEvent публикуется под "player", PlayerId = "player_0").
        if (!PlayerIdResolver.AreSameEntity(e.EntityId, Player.PlayerId)) return;
        _cachedCurrentQi = e.Current;
        _cachedMaxQi = e.Max;
        _cachedConductivity = e.Conductivity;
        _cachedCultivationLevel = e.CultivationLevel;
        if (Visible) RefreshMeridiansAndCore();
    }

    private void OnSlotAssigned(in TechniqueSlotAssignedEvent e)
    {
        if (!Visible) return;
        RefreshSlots();
        ToastPub?.Publish(new ToastShownEvent($"Слот {e.SlotIndex}: назначен \"{e.TechniqueId}\"", 1.5f));
    }

    private void OnSlotCleared(in TechniqueSlotClearedEvent e)
    {
        if (!Visible) return;
        RefreshSlots();
    }

    private void OnAssignPressed()
    {
        if (string.IsNullOrEmpty(_selectedTechId))
        {
            ToastPub?.Publish(new ToastShownEvent("Сначала выберите технику", 1.5f));
            return;
        }
        int slotIndex = TechniqueSlotService.MinSlot + _slotSelector.Selected;
        bool ok = Slots?.AssignSlot(slotIndex, _selectedTechId) ?? false;
        if (!ok)
        {
            ToastPub?.Publish(new ToastShownEvent($"Не удалось установить в слот {slotIndex}", 1.5f));
        }
    }

    // === Refresh helpers ===

    private void RebuildTechniqueList()
    {
        if (_techniqueList == null) return;
        _techniqueList.Clear();
        var ordered = Techniques?.GetOrderedIds();
        if (ordered == null) return;
        foreach (var id in ordered)
        {
            var tech = Techniques.GetTechnique(id);
            if (tech == null) continue;
            string label = $"{TechName(tech)}  L{tech.Level}";
            _techniqueList.AddItem(label);
            _techniqueList.SetItemMetadata(_techniqueList.ItemCount - 1, id);
            if (id == _selectedTechId)
                _techniqueList.Select(_techniqueList.ItemCount - 1);
        }
        RefreshSelectedTechniqueDetails();
    }

    private void RefreshSelectedTechniqueDetails()
    {
        if (_techniqueDetails == null) return;
        if (string.IsNullOrEmpty(_selectedTechId))
        {
            _techniqueDetails.Text = "Выберите технику слева.";
            return;
        }
        var tech = Techniques?.GetTechnique(_selectedTechId);
        if (tech == null)
        {
            _techniqueDetails.Text = "Техника удалена.";
            return;
        }
        int currentSlot = Slots?.FindSlotForTechnique(_selectedTechId) ?? -1;
        string slotInfo = currentSlot >= 0 ? $"Слот {currentSlot}" : "не назначен";
        float cd = Techniques?.GetCooldown(_selectedTechId) ?? 0f;
        string cdInfo = cd > 0f ? $"  ⏳{cd:F0}с" : "";

        _techniqueDetails.Text =
            $"{TechName(tech)}\n" +
            $"Тип: {TechTypeLabel(tech.Type)}   Грейд: {tech.Grade}\n" +
            $"Уровень: {tech.Level}   Стихия: {ElementLabel(tech.Element)}\n" +
            $"Стоимость Ци: {tech.QiCost}   Ёмкость: {tech.CapacityCost}\n" +
            $"Базовый урон: {tech.BaseDamage}   Пробитие: {tech.ArmorPenetration}\n" +
            $"Кулдаун: {tech.Cooldown:F1}с{cdInfo}   Дальность: {tech.Range:F1}м\n" +
            $"Мастерство: {tech.Mastery:F2}/100   Ultimate: {(tech.IsUltimate ? "да" : "нет")}\n" +
            $"Слот быстрого доступа: {slotInfo}";
    }

    private void RefreshSlots()
    {
        if (_slotBar == null) return;
        var slots = Slots?.GetAllSlots();
        for (int i = 0; i < TechniqueSlotService.SlotCount; i++)
        {
            int slotIndex = TechniqueSlotService.MinSlot + i;
            string? techId = null;
            slots?.TryGetValue(slotIndex, out techId);
            string text = "—";
            if (!string.IsNullOrEmpty(techId))
            {
                var tech = Techniques?.GetTechnique(techId);
                text = tech != null ? ShortName(tech.Name) : "?";
            }
            if (_slotLabels[i] != null) _slotLabels[i].Text = text;
        }
    }

    private void RefreshMeridiansAndCore()
    {
        // === Меридианы ===
        if (_meridiansLabel != null)
        {
            int K = GameConstants.COMBAT_CHANNEL_MULT;
            float chargeRate = _cachedConductivity * K;
            long permilleFill = _cachedMaxQi > 0 ? (_cachedCurrentQi * 1000 / _cachedMaxQi) : 0;

            _meridiansLabel.Text =
                "◆  Меридианы (каналы Ци в теле практика)  ◆\n\n" +
                $"Проводимость меридиан:    {_cachedConductivity:F2} Ци/с\n" +
                $"  (медитативный масштаб — поглощение из среды за 1 секунду)\n\n" +
                $"Боевой множитель канала:  ×{K} (CombatChannelMult)\n" +
                $"  (боевой прогон меридиан — форсированное пропускание Ци)\n\n" +
                $"Скорость зарядки техник: {chargeRate:F2} Ци/тик\n" +
                $"  = проводимость × K × (1 + мастерство × 0.005)\n" +
                $"  (лёгкие техники заряжаются < 1 тика; тяжёлые — несколько тиков)\n\n" +
                $"Уровень культивации:      {_cachedCultivationLevel}\n";
        }

        // === Ядро ===
        if (_coreLabel != null)
        {
            long permilleFill = _cachedMaxQi > 0 ? (_cachedCurrentQi * 1000 / _cachedMaxQi) : 0;
            long percentFill = permilleFill / 10;
            long remainderFill = permilleFill - percentFill * 10;
            int currentStage = GetCurrentBreakthroughStage(_cachedCultivationLevel);
            string stageName = GetBreakthroughStageName(currentStage);

            _coreLabel.Text =
                "◆  Ядро культивации (даньтянь)  ◆\n\n" +
                $"Текущее Ци:               {_cachedCurrentQi:N0}\n" +
                $"Ёмкость ядра:             {_cachedMaxQi:N0}\n" +
                $"Заполнение:               {percentFill}.{remainderFill}%\n" +
                $"  (целевое — заполнить перед прорывом уровня)\n\n" +
                $"Уровень ядра:             {_cachedCultivationLevel}\n" +
                $"Этап \"прокачки\":           {stageName} (этап {currentStage})\n" +
                $"  (1-3 Закалка, 4-6 Формирование ядра, 7-9 Золотое ядро, 10+ Сокровенное)\n";
        }
    }

    // === Helpers ===

    private static string TechName(LearnedTechnique tech) =>
        string.IsNullOrEmpty(tech.Name) ? tech.TechniqueId : tech.Name;

    private static string ShortName(string name) =>
        name.Length <= 12 ? name : name.Substring(0, 12) + "…";

    private static string TechTypeLabel(TechniqueType t) => t switch
    {
        TechniqueType.Combat => "Боевая",
        TechniqueType.Defense => "Защитная",
        TechniqueType.Healing => "Лечебная",
        TechniqueType.Movement => "Перемещение",
        TechniqueType.Sensory => "Сенсорная",
        TechniqueType.Support => "Поддержка",
        TechniqueType.Curse => "Проклятие",
        TechniqueType.Cultivation => "Культивация",
        TechniqueType.Formation => "Формация",
        _ => t.ToString()
    };

    private static string ElementLabel(Element e) => e switch
    {
        Element.Fire => "Огонь",
        Element.Water => "Вода",
        Element.Earth => "Земля",
        Element.Air => "Воздух",
        Element.Lightning => "Молния",
        Element.Void => "Пустота",
        Element.Light => "Свет",
        Element.Poison => "Яд",
        Element.Neutral => "Нейтрально",
        _ => e.ToString()
    };

    private static int GetCurrentBreakthroughStage(int level)
    {
        if (level <= 0) return 1;
        if (level <= 3) return 1; // Закалка тела (Qi Condensation)
        if (level <= 6) return 2; // Формирование ядра (Foundation Building)
        if (level <= 9) return 3; // Золотое ядро (Golden Core)
        return 4;                  // Сокровенное и выше
    }

    private static string GetBreakthroughStageName(int stage) => stage switch
    {
        1 => "Закалка тела",
        2 => "Формирование ядра",
        3 => "Золотое ядро",
        4 => "Сокровенное ядро",
        _ => "Неизвестно"
    };
}

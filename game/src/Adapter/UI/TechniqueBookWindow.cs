#nullable enable
// Создано: 2026-08-28 — Книга Техник (этап «Библиотека + Лодаут», решение
// пользователя от 2026-08-28: Old School RPG-книга, матричная навигация).
//
// TechniqueBookWindow — отдельное окно (как инвентарь), открывается клавишей T.
// Матричная навигация по библиотеке техник:
//   • Вкладки = уровни техник (живое окно резонанса L..L−4) + «Все» + «Архив» + «Свитки».
//   • Блоки = типы техник (Атака / Защита / Поддержка / …) с цветовым выделением.
//   • Строки внутри блока = стихии; чипы = конкретные техники.
//
//   ┌─ ◆ Книга Техник ◆ ──────────── Библиотека: 6/8 ── [×] ─┐
//   │ [Все][L5][L4][L3][L2][L1][Архив][Свитки]                │
//   │ ┌─ матрица (scroll) ────────┬─ детали выбранной ──────┐ │
//   │ │ ▓▓ АТАКА (2) ▓▓           │ имя, статы, кулдаун     │ │
//   │ │  🔥 Огонь: [чип][чип]     │ [3][4][5][6][7][8][9]   │ │
//   │ │  ⚡ Молния: [чип]          │ [Записать на свиток]    │ │
//   │ │ ▓▓ ЗАЩИТА (1) ▓▓           │ [Забыть (+осмысление)]  │ │
//   │ └────────────────────────────┴─────────────────────────┘ │
//   │ Слоты быстрого доступа: [3] [4] [5] [6] [7] [8] [9]      │
//   └───────────────────────────────────────────────────────────┘
//
// Правила:
//   • Техники Культивации НЕ показываются (их дом — CultivationWindow, вкладка
//     «Техники»): решение пользователя 2026-08-28.
//   • Архив — техники ниже окна резонанса (L−4): хранить можно, изучить заново
//     нельзя — только со свитка.
//   • Свитки — записанные базовые формы техник (Mastery=0), изучение со свитка
//     обходит окно резонанса, свиток расходуется.
//   • Паузит игру при открытии (как инвентарь — планирование, Old School).
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Helpers;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Adapter.Di;
using CultivationGame.Modules.Combat;
using CultivationGame.Modules.Player;

namespace CultivationGame.Adapter.UI;

/// <summary>
/// Книга Техник (T) — матричный браузер библиотеки техник игрока.
/// Вкладки = уровни, блоки = типы, строки = стихии. Управление слотами
/// быстрого доступа (3–9), свитками и забвением с эхом мастерства.
/// </summary>
public partial class TechniqueBookWindow : Control
{
    // === DI ===
    [Inject] private TechniqueService Techniques = null!;
    [Inject] private TechniqueSlotService Slots = null!;
    [Inject] private IQiService Qi = null!;
    [Inject] private IPlayerService Player = null!;
    [Inject] private IPublisher<ToastShownEvent> ToastPub = null!;
    [Inject] private ISubscriber<TechniqueLearnedEvent> LearnedSub = null!;
    [Inject] private ISubscriber<TechniqueForgottenEvent> ForgottenSub = null!;
    [Inject] private ISubscriber<TechniqueSelectionChangedEvent> SelectionSub = null!;
    [Inject] private ISubscriber<TechniqueSlotAssignedEvent> SlotAssignedSub = null!;
    [Inject] private ISubscriber<TechniqueSlotClearedEvent> SlotClearedSub = null!;
    [Inject] private ISubscriber<QiChangedEvent> QiSub = null!;

    // === UI nodes ===
    private Label _libraryLabel = null!;
    private TabContainer _tabs = null!;
    private Label _detailsLabel = null!;
    private Button _inscribeButton = null!;
    private Button _forgetButton = null!;
    private ConfirmationDialog _forgetDialog = null!;
    private readonly Button[] _slotButtons = new Button[TechniqueSlotService.SlotCount];
    private readonly Label[] _slotLabels = new Label[TechniqueSlotService.SlotCount];
    private readonly Panel[] _slotPanels = new Panel[TechniqueSlotService.SlotCount];
    private readonly StyleBoxFlat[] _slotStyles = new StyleBoxFlat[TechniqueSlotService.SlotCount];

    /// <summary>Чипы техник по ID (подсветка выбранной).</summary>
    private readonly Dictionary<string, Button> _chipById = new();

    // === Подписки ===
    private IDisposable? _learnedToken;
    private IDisposable? _forgottenToken;
    private IDisposable? _selectionToken;
    private IDisposable? _slotAssignedToken;
    private IDisposable? _slotClearedToken;
    private IDisposable? _qiToken;

    // === Состояние ===
    private string? _selectedTechId;
    private int _cachedLevel = 1;
    private long _cachedQi;
    private bool _initialized;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        BuildUI();

        _learnedToken = LearnedSub?.Subscribe(OnLearned);
        _forgottenToken = ForgottenSub?.Subscribe(OnForgotten);
        _selectionToken = SelectionSub?.Subscribe(OnSelectionChanged);
        _slotAssignedToken = SlotAssignedSub?.Subscribe(OnSlotAssigned);
        _slotClearedToken = SlotClearedSub?.Subscribe(OnSlotCleared);
        _qiToken = QiSub?.Subscribe(OnQiChanged);

        Visible = false;
        _initialized = true;
        GD.Print("[TechniqueBook] Ready");
    }

    public override void _ExitTree()
    {
        _learnedToken?.Dispose();
        _forgottenToken?.Dispose();
        _selectionToken?.Dispose();
        _slotAssignedToken?.Dispose();
        _slotClearedToken?.Dispose();
        _qiToken?.Dispose();
    }

    public override void _Process(double delta)
    {
        if (!_initialized || !Visible) return;
        // Живой статус выбранной техники (кулдаун) + доступность записи на свиток.
        RefreshDetails();
    }

    // === Открытие/закрытие ===

    public void Open()
    {
        if (Visible) return;
        Visible = true;
        _cachedLevel = Math.Max(1, (int)Qi.CultivationLevel);
        _cachedQi = Qi.CurrentQi;
        RebuildAll();
    }

    public void Close() => Visible = false;

    public void Toggle()
    {
        if (Visible) Close();
        else Open();
    }

    // === UI Construction ===

    private void BuildUI()
    {
        // Полноэкранный оверлей с тёмным фоном (как инвентарь) — «бумага» книги
        // не просвечивает сквозь окружение.
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var bg = new ColorRect
        {
            Name = "Background",
            Color = new Color(0.05f, 0.03f, 0.02f, 0.75f),
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Stop;
        AddChild(bg);

        var panel = new Panel { Name = "BookPanel" };
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        panel.OffsetLeft = -510; panel.OffsetRight = 510;
        panel.OffsetTop = -320; panel.OffsetBottom = 320;
        panel.MouseFilter = MouseFilterEnum.Stop;
        AddChild(panel);

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.10f, 0.08f, 0.05f, 0.97f),
        };
        style.SetBorderWidthAll(2);
        style.SetBorderColor(new Color(0.55f, 0.42f, 0.20f, 0.9f));
        style.SetCornerRadiusAll(8);
        panel.AddThemeStyleboxOverride("panel", style);

        var root = new VBoxContainer();
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 10; root.OffsetRight = -10;
        root.OffsetTop = 8; root.OffsetBottom = -8;
        root.AddThemeConstantOverride("separation", 6);
        panel.AddChild(root);

        // ── Заголовок: название + счётчик библиотеки + закрыть ──
        var header = new HBoxContainer();
        var title = new Label { Text = "◆  Книга Техник  ◆" };
        title.AddThemeFontSizeOverride("font_size", 20);
        title.AddThemeColorOverride("font_color", new Color(0.95f, 0.80f, 0.45f));
        header.AddChild(title);

        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddChild(spacer);

        _libraryLabel = new Label { Text = "Библиотека: 0/0" };
        _libraryLabel.AddThemeFontSizeOverride("font_size", 14);
        _libraryLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.75f, 0.55f));
        header.AddChild(_libraryLabel);

        var closeBtn = new Button { Text = "×" };
        closeBtn.Pressed += Close;
        header.AddChild(closeBtn);
        root.AddChild(header);

        // ── Вкладки (уровни/Все/Архив/Свитки) ──
        _tabs = new TabContainer
        {
            Name = "BookTabs",
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddChild(_tabs);

        // ── Панель деталей + действий (под вкладками, над слотами) ──
        BuildDetailsRow(root);

        // ── Нижний бар слотов 3–9 ──
        var slotCaption = new Label { Text = "Слоты быстрого доступа техник:" };
        slotCaption.AddThemeFontSizeOverride("font_size", 14);
        slotCaption.AddThemeColorOverride("font_color", new Color(0.85f, 0.75f, 0.55f));
        root.AddChild(slotCaption);

        var slotBar = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        slotBar.AddThemeConstantOverride("separation", 4);
        root.AddChild(slotBar);

        for (int i = 0; i < TechniqueSlotService.SlotCount; i++)
        {
            int slotIndex = TechniqueSlotService.MinSlot + i;
            var slotPanel = new Panel
            {
                CustomMinimumSize = new Vector2(108, 52),
                MouseFilter = MouseFilterEnum.Stop,
            };
            var slotStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.18f, 0.16f, 0.10f, 0.9f),
            };
            slotStyle.SetBorderWidthAll(1);
            slotStyle.SetBorderColor(new Color(0.55f, 0.42f, 0.20f));
            slotStyle.SetCornerRadiusAll(4);
            slotPanel.AddThemeStyleboxOverride("panel", slotStyle);
            _slotStyles[i] = slotStyle;

            var vbox = new VBoxContainer();
            vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            vbox.OffsetLeft = 4; vbox.OffsetRight = -4;
            vbox.OffsetTop = 2; vbox.OffsetBottom = -2;
            vbox.AddThemeConstantOverride("separation", 1);
            vbox.Alignment = BoxContainer.AlignmentMode.Center;
            slotPanel.AddChild(vbox);

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
            techNameLabel.CustomMinimumSize = new Vector2(96, 26);
            vbox.AddChild(techNameLabel);

            // ЛКМ по занятому слоту → снять технику.
            int captured = slotIndex;
            slotPanel.GuiInput += @event =>
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
            _slotPanels[i] = slotPanel;
            slotBar.AddChild(slotPanel);
        }

        // ── Диалог подтверждения забвения ──
        _forgetDialog = new ConfirmationDialog
        {
            Title = "Забыть технику",
            OkButtonText = "Забыть",
            CancelButtonText = "Отмена",
        };
        AddChild(_forgetDialog);
        _forgetDialog.Confirmed += OnForgetConfirmed;
    }

    // === Построение вкладок ===

    /// <summary>Полная перестройка: вкладки + детали + слоты + счётчик.</summary>
    private void RebuildAll()
    {
        RebuildTabs();
        RefreshDetails();
        RefreshSlots();
        RefreshLibraryCounter();
    }

    private void RebuildTabs()
    {
        if (_tabs == null) return;
        _chipById.Clear();
        foreach (var child in _tabs.GetChildren())
            child.QueueFree();

        var all = Techniques?.GetAllTechniques();
        if (all == null) return;

        // Техники Культивации не показываем в Книге (их дом — CultivationWindow).
        IEnumerable<LearnedTechnique> bookTechniques = all.Values
            .Where(t => t.Type != TechniqueType.Cultivation);

        int minLevel = Techniques?.ResonanceMinLevel ?? 1;

        // 1. «Все» — вся книга (кроме культ-техник).
        BuildMatrixTab("Все", bookTechniques, null);

        // 2. Живые уровни: L вниз до max(1, L−4) — окно резонанса §8.1.
        for (int lvl = _cachedLevel; lvl >= minLevel; lvl--)
        {
            int captured = lvl;
            BuildMatrixTab($"L{lvl}", bookTechniques.Where(t => t.Level == captured), null);
        }

        // 3. «Архив» — ниже окна резонанса (только если непусто).
        var archive = bookTechniques.Where(t => t.Level < minLevel).ToList();
        if (archive.Count > 0)
        {
            BuildMatrixTab("Архив", archive,
                "Техники вне окна резонанса (ниже L−4): хранить можно, изучить заново нельзя — только со свитка.");
        }

        // 4. «Свитки».
        BuildScrollsTab();
    }

    /// <summary>Вкладка с матрицей тип→стихия.</summary>
    private void BuildMatrixTab(string tabName, IEnumerable<LearnedTechnique> techniques, string? notice)
    {
        var list = techniques.ToList();

        var outer = new VBoxContainer { Name = tabName };
        _tabs.AddChild(outer);

        if (notice != null)
        {
            var noticeLabel = new Label
            {
                Text = "⚠ " + notice,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            noticeLabel.AddThemeFontSizeOverride("font_size", 12);
            noticeLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.65f, 0.35f));
            outer.AddChild(noticeLabel);
        }

        if (list.Count == 0)
        {
            var empty = new Label { Text = "— пусто —" };
            empty.AddThemeFontSizeOverride("font_size", 13);
            empty.AddThemeColorOverride("font_color", new Color(0.55f, 0.50f, 0.42f));
            outer.AddChild(empty);
            return;
        }

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        outer.AddChild(scroll);

        var blocks = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        blocks.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(blocks);

        // Блоки по типам в каноническом порядке (Атака первой — Old School).
        TechniqueType[] typeOrder =
        {
            TechniqueType.Combat, TechniqueType.Defense, TechniqueType.Support,
            TechniqueType.Healing, TechniqueType.Movement, TechniqueType.Sensory,
            TechniqueType.Poison, TechniqueType.Curse, TechniqueType.Formation,
        };

        foreach (var type in typeOrder)
        {
            var byType = list.Where(t => t.Type == type).ToList();
            if (byType.Count == 0) continue; // прогрессивное раскрытие: пустое не рисуем
            blocks.AddChild(BuildTypeBlock(type, byType));
        }
    }

    /// <summary>Блок одного типа: заголовок с выделением + строки стихий.</summary>
    private Panel BuildTypeBlock(TechniqueType type, List<LearnedTechnique> techniques)
    {
        var accent = ElementStyle.TypeAccent(type);

        var block = new Panel
        {
            CustomMinimumSize = new Vector2(0, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.14f, 0.11f, 0.08f, 0.85f),
        };
        style.SetBorderWidthAll(1);
        style.SetBorderColor(accent);
        // Левый бордюр толще — «условное выделение» блока (решение пользователя).
        style.BorderWidthLeft = 4;
        style.SetCornerRadiusAll(4);
        style.ContentMarginLeft = 8;
        style.ContentMarginRight = 8;
        style.ContentMarginTop = 6;
        style.ContentMarginBottom = 6;
        block.AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        vbox.AddThemeConstantOverride("separation", 3);
        block.AddChild(vbox);

        var header = new Label
        {
            Text = $"▓ {ElementStyle.TypeName(type).ToUpperInvariant()} ({techniques.Count})",
        };
        header.AddThemeFontSizeOverride("font_size", 14);
        header.AddThemeColorOverride("font_color", accent.Lightened(0.15f));
        vbox.AddChild(header);

        // Строки по стихиям (только непустые), канонический порядок.
        Element[] elementOrder =
        {
            Element.Fire, Element.Water, Element.Earth, Element.Air,
            Element.Lightning, Element.Light, Element.Void, Element.Poison, Element.Neutral,
        };
        foreach (var element in elementOrder)
        {
            // Сортировка внутри строки: грейд ↓ → мастерство ↓ → урон ↓.
            var row = techniques
                .Where(t => t.Element == element)
                .OrderByDescending(t => t.Grade)
                .ThenByDescending(t => t.Mastery)
                .ThenByDescending(t => t.BaseDamage)
                .ToList();
            if (row.Count == 0) continue;
            vbox.AddChild(BuildElementRow(element, row));
        }
        return block;
    }

    /// <summary>Строка одной стихии: подпись + чипы техник (с переносом).</summary>
    private VBoxContainer BuildElementRow(Element element, List<LearnedTechnique> row)
    {
        var color = ElementStyle.ElementColor(element);

        var vbox = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        vbox.AddThemeConstantOverride("separation", 2);

        var caption = new Label
        {
            Text = $"{ElementStyle.ElementEmoji(element)} {ElementStyle.ElementName(element)} ({row.Count})",
        };
        caption.AddThemeFontSizeOverride("font_size", 12);
        caption.AddThemeColorOverride("font_color", color.Lightened(0.2f));
        vbox.AddChild(caption);

        var chips = new HFlowContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        chips.AddThemeConstantOverride("h_separation", 4);
        chips.AddThemeConstantOverride("v_separation", 4);
        vbox.AddChild(chips);

        foreach (var tech in row)
        {
            string id = tech.TechniqueId;
            var chip = new Button
            {
                Text = ChipTitle(tech),
                Alignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(250, 26),
                ClipText = true,
            };
            chip.AddThemeFontSizeOverride("font_size", 12);
            chip.Pressed += () => OnChipClicked(id);
            chips.AddChild(chip);
            _chipById[id] = chip;
        }
        return vbox;
    }

    /// <summary>Вкладка «Свитки».</summary>
    private void BuildScrollsTab()
    {
        var outer = new VBoxContainer { Name = "Свитки" };
        _tabs.AddChild(outer);

        var intro = new Label
        {
            Text = "Записанные базовые формы техник (без наработанного мастерства). " +
                   "Изучение со свитка обходит окно резонанса; свиток расходуется.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        intro.AddThemeFontSizeOverride("font_size", 12);
        intro.AddThemeColorOverride("font_color", new Color(0.6f, 0.55f, 0.45f));
        outer.AddChild(intro);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        outer.AddChild(scroll);

        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(list);

        var scrolls = Techniques?.GetAllScrolls();
        if (scrolls == null || scrolls.Count == 0)
        {
            var empty = new Label { Text = "— нет свитков: выберите технику и запишите её —" };
            empty.AddThemeFontSizeOverride("font_size", 13);
            empty.AddThemeColorOverride("font_color", new Color(0.55f, 0.50f, 0.42f));
            list.AddChild(empty);
            return;
        }

        foreach (var data in scrolls.OrderBy(d => d.Type).ThenBy(d => d.Level))
        {
            string scrollId = TechniqueService.ScrollIdFor(data.TechniqueId);
            string captured = scrollId;

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            var info = new Label
            {
                Text = $"{ElementStyle.ElementEmoji(data.Element)} {data.NameRu} L{data.Level} ·{ElementStyle.GradeName(data.Grade)} | ⚔{data.BaseDamage} Ци:{data.QiCost}",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                ClipText = true,
                CustomMinimumSize = new Vector2(360, 0),
            };
            info.AddThemeFontSizeOverride("font_size", 13);
            info.AddThemeColorOverride("font_color",
                ElementStyle.ElementColor(data.Element).Lightened(0.25f));
            row.AddChild(info);

            var learnBtn = new Button { Text = "Изучить" };
            learnBtn.AddThemeFontSizeOverride("font_size", 12);
            bool known = Techniques?.IsLearned(data.TechniqueId) ?? true;
            bool noSpace = (Techniques?.LibraryFree ?? 0) <= 0;
            if (known) { learnBtn.Text = "Уже изучена"; learnBtn.Disabled = true; }
            else if (noSpace) { learnBtn.Text = "Библиотека полна"; learnBtn.Disabled = true; }
            learnBtn.Pressed += () => OnLearnFromScroll(captured);
            row.AddChild(learnBtn);

            list.AddChild(row);
        }
    }

    // === Панель деталей (под вкладками): детали слева + действия справа ===

    private void BuildDetailsRow(VBoxContainer parent)
    {
        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        row.AddThemeConstantOverride("separation", 8);
        parent.AddChild(row);

        _detailsLabel = new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(560, 96),
        };
        _detailsLabel.AddThemeFontSizeOverride("font_size", 13);
        _detailsLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.88f, 0.80f));
        row.AddChild(_detailsLabel);

        var actions = new VBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        actions.AddThemeConstantOverride("separation", 4);
        row.AddChild(actions);

        // Слоты 3–9: клик — назначить выбранную технику / снять, если та же.
        var slotRow = new HBoxContainer();
        slotRow.AddThemeConstantOverride("separation", 3);
        for (int i = 0; i < TechniqueSlotService.SlotCount; i++)
        {
            int slotIndex = TechniqueSlotService.MinSlot + i;
            var btn = new Button
            {
                Text = slotIndex.ToString(),
                CustomMinimumSize = new Vector2(34, 28),
            };
            btn.AddThemeFontSizeOverride("font_size", 13);
            int captured = slotIndex;
            btn.Pressed += () => OnSlotButtonPressed(captured);
            _slotButtons[i] = btn;
            slotRow.AddChild(btn);
        }
        actions.AddChild(slotRow);

        var slotHint = new Label { Text = "назначить в слот быстрого доступа" };
        slotHint.AddThemeFontSizeOverride("font_size", 10);
        slotHint.AddThemeColorOverride("font_color", new Color(0.6f, 0.55f, 0.45f));
        actions.AddChild(slotHint);

        _inscribeButton = new Button { Text = "Записать на свиток" };
        _inscribeButton.AddThemeFontSizeOverride("font_size", 12);
        _inscribeButton.Pressed += OnInscribePressed;
        actions.AddChild(_inscribeButton);

        _forgetButton = new Button { Text = "Забыть" };
        _forgetButton.AddThemeFontSizeOverride("font_size", 12);
        _forgetButton.Pressed += OnForgetPressed;
        actions.AddChild(_forgetButton);
    }

    // === Действия ===

    private void OnChipClicked(string id)
    {
        _selectedTechId = id;
        Techniques?.SelectTechnique(id); // выбор активной техники для Z-каста
        RefreshChipHighlight();
        RefreshDetails();
    }

    private void OnSlotButtonPressed(int slotIndex)
    {
        if (string.IsNullOrEmpty(_selectedTechId) || Slots == null) return;
        string? current = Slots.GetTechniqueAtSlot(slotIndex);
        if (current == _selectedTechId)
        {
            Slots.ClearSlot(slotIndex);
            ToastPub?.Publish(new ToastShownEvent($"Слот {slotIndex}: очищен", 1.5f));
        }
        else if (Slots.AssignSlot(slotIndex, _selectedTechId))
        {
            ToastPub?.Publish(new ToastShownEvent(
                $"Слот {slotIndex}: «{Techniques?.GetTechnique(_selectedTechId)?.Name ?? "?"}»", 1.5f));
        }
    }

    private void OnInscribePressed()
    {
        if (string.IsNullOrEmpty(_selectedTechId) || Techniques == null) return;
        var tech = Techniques.GetTechnique(_selectedTechId);
        if (tech == null) return;

        long cost = TechniqueService.ScrollQiCost(tech);
        if (_cachedQi < cost)
        {
            ToastPub?.Publish(new ToastShownEvent($"Недостаточно Ци: нужно {cost}", 2.0f));
            return;
        }
        if (Techniques.InscribeScroll(_selectedTechId))
        {
            ToastPub?.Publish(new ToastShownEvent(
                $"«{tech.Name}» записана на свиток (−{cost} Ци)", 2.0f));
            RebuildTabs();
            RefreshDetails();
        }
    }

    private void OnForgetPressed()
    {
        if (string.IsNullOrEmpty(_selectedTechId) || Techniques == null) return;
        var tech = Techniques.GetTechnique(_selectedTechId);
        if (tech == null) return;

        float echo = tech.Mastery * TechniqueService.EchoTransferRatio;
        float existing = Techniques.GetMasteryEcho(tech.Type, tech.Element);
        float total = MathF.Min(TechniqueService.EchoMasteryCap, existing + echo);

        _forgetDialog.DialogText =
            $"Забыть «{tech.Name}»?\n\n" +
            $"Мастерство {tech.Mastery:F1} не пропадёт полностью: осмысление профиля\n" +
            $"({ElementStyle.TypeName(tech.Type)} · {ElementStyle.ElementName(tech.Element)}) " +
            $"вырастет до {total:F1} — следующая техника того же профиля начнёт с него.";
        _forgetDialog.PopupCentered();
    }

    private void OnForgetConfirmed()
    {
        if (string.IsNullOrEmpty(_selectedTechId) || Techniques == null) return;
        var tech = Techniques.GetTechnique(_selectedTechId);
        if (tech == null) return;

        string name = tech.Name;
        float echoBefore = Techniques.GetMasteryEcho(tech.Type, tech.Element);
        if (Techniques.ForgetTechnique(_selectedTechId))
        {
            float echoAfter = Techniques.GetMasteryEcho(tech.Type, tech.Element);
            ToastPub?.Publish(new ToastShownEvent(
                $"«{name}» забыта. Осмысление: {echoBefore:F1} → {echoAfter:F1}", 2.5f));
            _selectedTechId = null;
            RebuildTabs();
            RefreshDetails();
            RefreshSlots();
            RefreshLibraryCounter();
        }
    }

    private void OnLearnFromScroll(string scrollId)
    {
        if (Techniques == null) return;
        var data = Techniques.GetScroll(scrollId);
        if (data == null) return;
        if (Techniques.LearnFromScroll(scrollId))
        {
            ToastPub?.Publish(new ToastShownEvent(
                $"«{data.NameRu}» изучена со свитка (свиток израсходован)", 2.5f));
            RebuildAll();
        }
        else
        {
            ToastPub?.Publish(new ToastShownEvent(
                "Не удалось изучить: библиотека полна / лимит категории / уже изучена", 2.5f));
        }
    }

    // === Refresh ===

    private void RefreshLibraryCounter()
    {
        if (_libraryLabel == null || Techniques == null) return;
        _libraryLabel.Text = $"Библиотека: {Techniques.LibraryUsed}/{Techniques.LibraryCapacity}";
        _libraryLabel.AddThemeColorOverride("font_color",
            Techniques.LibraryFree <= 0
                ? new Color(0.90f, 0.45f, 0.30f)
                : new Color(0.85f, 0.75f, 0.55f));
    }

    private void RefreshChipHighlight()
    {
        string? sel = Techniques?.SelectedTechniqueId ?? _selectedTechId;
        foreach (var kvp in _chipById)
        {
            kvp.Value.Modulate = kvp.Key == sel
                ? new Color(1.0f, 0.9f, 0.5f)
                : new Color(1f, 1f, 1f);
        }
    }

    private void RefreshDetails()
    {
        if (_detailsLabel == null) return;
        if (string.IsNullOrEmpty(_selectedTechId))
        {
            _detailsLabel.Text = "Выберите технику в матрице.\n\n" +
                "ЛКМ по чипу — выбрать активной (каст: Z).\n" +
                "Кнопки слотов назначают технику в слоты быстрого доступа.";
            if (_inscribeButton != null) _inscribeButton.Disabled = true;
            if (_forgetButton != null) _forgetButton.Disabled = true;
            return;
        }
        var tech = Techniques?.GetTechnique(_selectedTechId);
        if (tech == null)
        {
            _detailsLabel.Text = "Техника удалена.";
            if (_inscribeButton != null) _inscribeButton.Disabled = true;
            if (_forgetButton != null) _forgetButton.Disabled = true;
            return;
        }

        int currentSlot = Slots?.FindSlotForTechnique(_selectedTechId) ?? -1;
        string slotInfo = currentSlot >= 0 ? $"Слот {currentSlot}" : "не назначен";
        float cd = Techniques?.GetCooldown(_selectedTechId) ?? 0f;
        string cdInfo = cd > 0f ? $"  ⏳{cd:F0}с" : "";
        bool inArchive = tech.Level < (Techniques?.ResonanceMinLevel ?? 1);

        _detailsLabel.Text =
            $"{ElementStyle.ElementEmoji(tech.Element)} {tech.Name} L{tech.Level}" +
            (inArchive ? "  [АРХИВ]" : "") + "\n" +
            $"Тип: {ElementStyle.TypeName(tech.Type)}   Грейд: {ElementStyle.GradeName(tech.Grade)}\n" +
            $"Стихия: {ElementStyle.ElementName(tech.Element)}   Ultimate: {(tech.IsUltimate ? "да" : "нет")}\n" +
            $"Урон: {tech.BaseDamage}   Пробитие: {tech.ArmorPenetration}   Дальность: {tech.Range:F1}м\n" +
            $"Ци: {tech.QiCost}   Кулдаун: {tech.Cooldown:F1}с{cdInfo}   Каст: {tech.CastTime:F1}с\n" +
            $"Мастерство: {tech.Mastery:F1}/100   Слот: {slotInfo}";

        // Кнопка свитка.
        if (_inscribeButton != null)
        {
            long cost = TechniqueService.ScrollQiCost(tech);
            bool hasScroll = Techniques?.HasScrollFor(_selectedTechId) ?? false;
            _inscribeButton.Text = hasScroll
                ? "Свиток уже записан"
                : $"Записать на свиток (−{cost} Ци)";
            _inscribeButton.Disabled = hasScroll || _cachedQi < cost;
        }
        // Кнопка забвения.
        if (_forgetButton != null)
        {
            float echo = tech.Mastery * TechniqueService.EchoTransferRatio;
            _forgetButton.Text = $"Забыть (+{echo:F1} осмысления)";
            _forgetButton.Disabled = false;
        }
    }

    private void RefreshSlots()
    {
        if (_slotPanels[0] == null || Slots == null) return;
        var slots = Slots.GetAllSlots();
        for (int i = 0; i < TechniqueSlotService.SlotCount; i++)
        {
            int slotIndex = TechniqueSlotService.MinSlot + i;
            string? techId = null;
            slots.TryGetValue(slotIndex, out techId);
            string text = "—";
            Color border = new(0.55f, 0.42f, 0.20f);
            bool holdsSelected = techId == _selectedTechId;
            if (!string.IsNullOrEmpty(techId))
            {
                var tech = Techniques?.GetTechnique(techId);
                text = tech != null ? ShortName(tech.Name) : "?";
                border = ElementStyle.ElementColor(tech?.Element ?? Element.Neutral);
            }
            if (_slotLabels[i] != null) _slotLabels[i].Text = text;
            if (_slotStyles[i] != null) _slotStyles[i].BorderColor = border;
            if (_slotButtons[i] != null)
                _slotButtons[i].Modulate = holdsSelected
                    ? new Color(1.0f, 0.9f, 0.5f)
                    : new Color(1f, 1f, 1f);
        }
    }

    // === Обработчики событий ===

    private void OnLearned(in TechniqueLearnedEvent e)
    {
        if (!Visible) return;
        RebuildTabs();
        RefreshLibraryCounter();
        RefreshDetails();
    }

    private void OnForgotten(in TechniqueForgottenEvent e)
    {
        if (!Visible) return;
        if (_selectedTechId == e.TechniqueId) _selectedTechId = null;
        RebuildTabs();
        RefreshLibraryCounter();
        RefreshDetails();
        RefreshSlots();
    }

    private void OnSelectionChanged(in TechniqueSelectionChangedEvent e)
    {
        if (!Visible) return;
        if (_selectedTechId != e.TechniqueId) _selectedTechId = e.TechniqueId;
        RefreshChipHighlight();
    }

    private void OnSlotAssigned(in TechniqueSlotAssignedEvent e)
    {
        if (!Visible) return;
        RefreshSlots();
    }

    private void OnSlotCleared(in TechniqueSlotClearedEvent e)
    {
        if (!Visible) return;
        RefreshSlots();
    }

    private void OnQiChanged(in QiChangedEvent e)
    {
        // B1-паттерн: фильтруем по сущности (событие публикуется и для NPC).
        if (Player != null && !PlayerIdResolver.AreSameEntity(e.EntityId, Player.PlayerId)) return;
        _cachedQi = e.Current;
        int level = Math.Max(1, e.CultivationLevel);
        if (Visible && level != _cachedLevel)
        {
            // Уровень вырос → окно резонанса сдвинулось → перестроить вкладки.
            _cachedLevel = level;
            RebuildTabs();
            RefreshLibraryCounter();
        }
        else _cachedLevel = level;
    }

    // === Helpers ===

    private static string ChipTitle(LearnedTechnique tech)
    {
        string grade = tech.Grade switch
        {
            TechniqueGrade.Refined => " ·О",
            TechniqueGrade.Perfect => " ·С",
            TechniqueGrade.Transcendent => " ·Т",
            _ => ""
        };
        string ulti = tech.IsUltimate ? "⚡" : "";
        return $"{ulti}{tech.Name} L{tech.Level}{grade} | ⚔{tech.BaseDamage} Ци:{tech.QiCost} М:{tech.Mastery:F0}%";
    }

    private static string ShortName(string name) =>
        name.Length <= 14 ? name : name.Substring(0, 14) + "…";
}

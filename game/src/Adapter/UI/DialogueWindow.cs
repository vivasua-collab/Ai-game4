#nullable enable
// Создано: 2026-08-22 — NPC_COMBAT_PREP Phase 2: окно диалога.
// Переписано: 2026-09-06 — S6 UX-аудит:
//   1) FIX layout: BottomWide (якоря 0/1 = ширина экрана!) → CenterBottom,
//      панель была шириной экран+900px и вылезала за края.
//   2) FIX переполнение: высота панели динамическая (ResizeToFit) —
//      3+ варианта ответа больше не выталкиваются за нижний край.
//   3) FIX подсказка: была «ЛКМ — выбрать ответ · далее» (клик по панели
//      не работал) → честная «1-9 — выбор · E — далее · Esc — выход».
//   4) Клик по панели = Advance (тот же путь, что E).
//   5) Индикатор «▼» пока typewriter печатает текст.
//   6) Клавиши выбора расширены 1..9 (было 1..4).
// DialogueWindow — чат с NPC: текст узла (typewriter) + варианты ответа.
// Backend: Modules/Interaction/DialogueService (ветвящиеся деревья).
using Godot;
using System;
using System.Collections.Generic;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;
using CultivationGame.Modules.Interaction;

namespace CultivationGame.Adapter.UI;

/// <summary>
/// Dialogue window (UI_DESIGN §19): bottom-anchored panel with the NPC name,
/// typewriter text, and choice buttons. Opened by GameWorldController when the
/// player presses E near an NPC; closes on dialogue end or Esc.
/// Pauses the tick simulation while open (set by the controller).
/// </summary>
public partial class DialogueWindow : Control
{
    [Inject] private DialogueService Dialogue = null!;
    [Inject] private INPCService NpcService = null!;
    [Inject] private ITimeService Time = null!;

    private Panel _panel = null!;
    private Label _npcNameLabel = null!;
    private Label _textLabel = null!;
    private Label _typingLabel = null!;
    private Label _hintLabel = null!;
    private VBoxContainer _choicesBox = null!;
    private readonly List<Button> _choiceButtons = new();

    // Ширина панели (900) и отступ от низа экрана (40) — см. BuildUI/ResizeToFit.
    private const float PanelWidth = 900f;
    // S6 (VLM): 40px было мало — хотбар (до ~108px с поясом, по центру низа)
    // перекрывал нижнюю часть окна (подсказку/кнопки). Поднимаем над хотбаром.
    private const float PanelBottomMargin = 118f;

    public bool IsOpen => Visible;

    // === Диагностика (public: headless-QA GODOT_DIALOGUE_DEBUG=1) ===

    /// <summary>Число кнопок-вариантов ответа (QA).</summary>
    public int ChoiceButtonCount => _choiceButtons.Count;

    /// <summary>Текст шапки с именем NPC (QA).</summary>
    public string NpcNameText => _npcNameLabel?.Text ?? "";

    /// <summary>Текст подсказки управления (QA: упоминает E/Esc/цифры).</summary>
    public string HintText => _hintLabel?.Text ?? "";

    /// <summary>Виден ли индикатор печати «▼» (QA).</summary>
    public bool TypingIndicatorVisible => _typingLabel?.Visible ?? false;

    /// <summary>Отображаемый текст узла (QA).</summary>
    public string BodyText => _textLabel?.Text ?? "";

    /// <summary>Полный текст текущего узла (QA: сравнение после Advance).</summary>
    public string FullTextForQA => Dialogue?.CurrentFullText ?? "";

    /// <summary>Фактическая ширина панели (QA: ≈900 после CenterBottom-фикса).</summary>
    public float PanelWidthActual => _panel?.Size.X ?? 0f;

    /// <summary>Фактическая высота панели (QA: динамическая, ≥190).</summary>
    public float PanelHeightActual => _panel?.Size.Y ?? 0f;

    /// <summary>Все ли варианты уложились в панель по вертикали (QA).</summary>
    public bool ChoicesFitPanel
    {
        get
        {
            if (_choicesBox == null || _panel == null || !IsInsideTree()) return true;
            float panelBottom = _panel.GlobalPosition.Y + _panel.Size.Y;
            float boxBottom = _choicesBox.GlobalPosition.Y + _choicesBox.Size.Y;
            return boxBottom <= panelBottom + 1f;
        }
    }

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
        {
            ContainerAdapter.InjectProperties(this, container);
        }

        BuildUI();
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        GD.Print("[DialogueWindow] Ready");
    }

    private void BuildUI()
    {
        Theme = ParchmentTheme.Create();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        // Bottom-anchored панель, 900 шириной, по центру.
        // S6 FIX: был BottomWide (якоря 0/1 = вся ширина экрана) + оффсеты
        // ±450 → фактическая ширина «экран+900», панель вылезала за края.
        _panel = new Panel { Name = "DialoguePanel" };
        _panel.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _panel.OffsetLeft = -PanelWidth / 2f;
        _panel.OffsetRight = PanelWidth / 2f;
        _panel.OffsetBottom = -PanelBottomMargin;
        _panel.MouseFilter = MouseFilterEnum.Stop;
        // S6: клик по панели = Advance (тот же путь, что клавиша E).
        _panel.GuiInput += OnPanelGuiInput;
        AddChild(_panel);

        var outer = new VBoxContainer();
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        outer.OffsetLeft = 18;
        outer.OffsetRight = -18;
        outer.OffsetTop = 12;
        outer.OffsetBottom = -12;
        outer.AddThemeConstantOverride("separation", 6);
        _panel.AddChild(outer);

        _npcNameLabel = new Label
        {
            Text = "???",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _npcNameLabel.AddThemeFontSizeOverride("font_size", 18);
        _npcNameLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkBlack);
        outer.AddChild(_npcNameLabel);

        _textLabel = new Label
        {
            Text = string.Empty,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _textLabel.AddThemeFontSizeOverride("font_size", 16);
        _textLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkBlack);
        _textLabel.CustomMinimumSize = new Vector2(0, 72);
        outer.AddChild(_textLabel);

        // S6: индикатор печати — виден, пока typewriter не допечатал текст.
        _typingLabel = new Label
        {
            Text = "▼",
            HorizontalAlignment = HorizontalAlignment.Right,
            Visible = false,
        };
        _typingLabel.AddThemeFontSizeOverride("font_size", 12);
        _typingLabel.AddThemeColorOverride("font_color", ParchmentTheme.AccentGold);
        outer.AddChild(_typingLabel);

        _choicesBox = new VBoxContainer();
        _choicesBox.AddThemeConstantOverride("separation", 4);
        outer.AddChild(_choicesBox);

        // S6: честная подсказка — цифры/E/Esc (клавиши реально работают).
        // Прежняя «ЛКМ — выбрать ответ · далее» вводила в заблуждение:
        // клик по панели ничего не делал.
        _hintLabel = new Label
        {
            Text = "1-9 — выбор · E — далее · Esc — выход",
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        _hintLabel.AddThemeFontSizeOverride("font_size", 12);
        _hintLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        outer.AddChild(_hintLabel);
    }

    /// <summary>S6: клик по панели = Advance() — как клавиша E (кнопки поглощают свой клик сами).</summary>
    private void OnPanelGuiInput(InputEvent @event)
    {
        if (!Visible) return;
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            Advance();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Open the window — called after DialogueService.StartDialogue succeeded.</summary>
    public void Open(string npcId)
    {
        var npc = NpcService?.GetNPC(npcId);
        _npcNameLabel.Text = npc != null
            ? $"{npc.DisplayName} ({npc.Role})"
            : npcId;
        Visible = true;
        _panel.MouseFilter = MouseFilterEnum.Stop;
        Refresh();
    }

    public void Close()
    {
        if (Dialogue != null && Dialogue.IsInDialogue)
            Dialogue.EndDialogue();
        Visible = false;
        _panel.MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>Advance dialogue (E key / click on panel).</summary>
    public void Advance()
    {
        if (Dialogue == null || !Dialogue.IsInDialogue) { Close(); return; }
        Dialogue.AdvanceDialogue();
        if (!Dialogue.IsInDialogue) Close();
        else Refresh();
    }

    /// <summary>Select a choice by index (кнопка/клавиша 1-9/QA).</summary>
    public void Select(int index)
    {
        Dialogue?.SelectChoice(index);
        if (Dialogue == null || !Dialogue.IsInDialogue) Close();
        else Refresh();
    }

    /// <summary>Refresh text + choices from the current dialogue node.</summary>
    private void Refresh()
    {
        if (Dialogue == null || !Dialogue.IsInDialogue) return;

        // Poll typewriter display text — typewriter advances in InteractionModule.Tick.
        _textLabel.Text = Dialogue.CurrentDisplayText;

        foreach (var btn in _choiceButtons)
        {
            btn.QueueFree();
        }
        _choiceButtons.Clear();

        var choices = Dialogue.CurrentChoices;
        if (choices is { Count: > 0 })
        {
            foreach (var choice in choices)
            {
                int index = choice.Index;
                var btn = new Button
                {
                    Text = $"{index + 1}. {choice.Text}",
                };
                btn.AddThemeFontSizeOverride("font_size", 15);
                btn.Pressed += () => Select(index);
                _choicesBox.AddChild(btn);
                _choiceButtons.Add(btn);
            }
        }

        ResizeToFit();
    }

    /// <summary>
    /// S6: динамическая высота панели. Фиксированные 190px не вмещали
    /// 3+ кнопок ответа (старейшина: 3 варианта) — низ выталкивался за край.
    /// Оценка по полному тексту узла + числу кнопок; клампы [190; 460].
    /// </summary>
    private void ResizeToFit()
    {
        if (_panel == null) return;

        float textH = EstimateTextHeight(Dialogue?.CurrentFullText ?? "");
        int n = _choiceButtons.Count;
        float choicesH = n * 40f + MathF.Max(0, n - 1) * 4f;
        // шапка 26 + текст + индикатор 18 + кнопки + подсказка 20 + поля/сепараторы ~56
        float h = 26f + textH + 18f + choicesH + 20f + 56f;
        h = Mathf.Clamp(h, 190f, 460f);

        _panel.OffsetTop = -(PanelBottomMargin + h);
        _panel.OffsetBottom = -PanelBottomMargin;
    }

    /// <summary>Оценка высоты многострочного текста узла (autowrap ~864px, шрифт 16).</summary>
    private static float EstimateTextHeight(string fullText)
    {
        if (string.IsNullOrEmpty(fullText)) return 72f;
        const float width = 864f;      // панель 900 − поля 2×18
        const float fontPx = 16f;
        const float charW = fontPx * 0.62f;   // консервативно (кириллица)
        const float lineH = fontPx * 1.45f;
        int charsPerLine = Math.Max(20, (int)(width / charW));
        int lines = (fullText.Length + charsPerLine - 1) / charsPerLine;
        return MathF.Max(72f, lines * lineH + 6f);
    }

    public override void _Process(double delta)
    {
        if (Visible && Dialogue is { IsInDialogue: true })
        {
            // S6-BUGFIX: диалог ставит игровые тики на паузу (GameBoot
            // гонит InteractionModule.Tick только при !IsPaused) — typewriter
            // в игровых тиках был заморожен всё время диалога: текст не
            // печатался, пока игрок не нажмёт E. Пока игра на паузе — окно
            // двигает typewriter реальным временем.
            if (Time is { IsPaused: true })
                Dialogue.TickTypewriter((float)delta);

            string display = Dialogue.CurrentDisplayText;
            if (_textLabel.Text != display)
                _textLabel.Text = display;

            // S6: индикатор печати виден, пока текст не допечатан.
            _typingLabel.Visible = !Dialogue.IsTypewriterComplete
                                   && !string.IsNullOrEmpty(Dialogue.CurrentFullText);
        }
    }

    /// <summary>
    /// Number keys 1..9 select a choice while the window is open.
    /// </summary>
    public override void _Input(InputEvent @event)
    {
        if (!Visible || Dialogue == null || !Dialogue.IsInDialogue) return;

        if (@event is InputEventKey { Pressed: true, Echo: false } key)
        {
            int index = key.Keycode switch
            {
                Godot.Key.Key1 => 0,
                Godot.Key.Key2 => 1,
                Godot.Key.Key3 => 2,
                Godot.Key.Key4 => 3,
                Godot.Key.Key5 => 4,
                Godot.Key.Key6 => 5,
                Godot.Key.Key7 => 6,
                Godot.Key.Key8 => 7,
                Godot.Key.Key9 => 8,
                _ => -1,
            };
            if (index >= 0)
            {
                var choices = Dialogue.CurrentChoices;
                if (choices != null && choices.Count > index)
                {
                    GetViewport().SetInputAsHandled();
                    Select(index);
                }
            }
        }
    }
}

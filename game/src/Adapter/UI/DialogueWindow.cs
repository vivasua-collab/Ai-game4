#nullable enable
// Создано: 2026-08-22 — NPC_COMBAT_PREP Phase 2: окно диалога.
// DialogueWindow — простой чат с NPC: текст узла (typewriter) + варианты
// ответа кнопками 1..N или кликом. Esc/E — закрыть/продвинуть.
// Backend: Modules/Interaction/DialogueService (ветвящиеся деревья).
// Источник: docs/docs_v2/09_workflow/NPC_COMBAT_PREP.md §Phase 2
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;
using CultivationGame.Modules.Interaction;
using CultivationGame.Modules.Interaction.Data;

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

    private Panel _panel = null!;
    private Label _npcNameLabel = null!;
    private Label _textLabel = null!;
    private VBoxContainer _choicesBox = null!;
    private readonly List<Button> _choiceButtons = new();

    public bool IsOpen => Visible;

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

        // Bottom-anchored panel (900 wide, 230 tall, 40 px from bottom).
        _panel = new Panel { Name = "DialoguePanel" };
        _panel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        _panel.OffsetLeft = -450;
        _panel.OffsetRight = 450;
        _panel.OffsetTop = -230;
        _panel.OffsetBottom = -40;
        _panel.MouseFilter = MouseFilterEnum.Stop;
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

        _choicesBox = new VBoxContainer();
        _choicesBox.AddThemeConstantOverride("separation", 4);
        outer.AddChild(_choicesBox);

        var hint = new Label
        {
            // 2026-08-28: клавиши — в окне-справке (F1); здесь только мышь.
            Text = "ЛКМ — выбрать ответ · далее",
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        hint.AddThemeFontSizeOverride("font_size", 12);
        hint.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        outer.AddChild(hint);
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

    /// <summary>Advance dialogue (E key / click on text panel).</summary>
    public void Advance()
    {
        if (Dialogue == null || !Dialogue.IsInDialogue) { Close(); return; }
        Dialogue.AdvanceDialogue();
        if (!Dialogue.IsInDialogue) Close();
        else Refresh();
    }

    private void Select(int index)
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
    }

    public override void _Process(double delta)
    {
        // Keep the typewriter text flowing while the node is shown.
        if (Visible && Dialogue is { IsInDialogue: true })
        {
            string display = Dialogue.CurrentDisplayText;
            if (_textLabel.Text != display)
                _textLabel.Text = display;
        }
    }

    /// <summary>
    /// Number keys 1..4 select a choice while the window is open.
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

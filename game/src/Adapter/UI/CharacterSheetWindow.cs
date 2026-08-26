#nullable enable
using Godot;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.UI;

/// <summary>
/// Character Sheet window (View #12, hotkey C).
/// Shows: body status (silhouette + HP), stats, cultivation level.
///
/// Layout: left = BodyStatusPanel, right = stats/cultivation.
/// Opens with C key, closes with C or Esc.
/// Pauses game when open (same as inventory).
///
/// Design per docs_v2/07_ui/UI_DESIGN.md §6.2.
/// </summary>
public partial class CharacterSheetWindow : Control
{
    [Inject] private IBodyService BodyService { get; set; } = null!;
    [Inject] private IPlayerService Player { get; set; } = null!;
    [Inject] private IQiService QiService { get; set; } = null!;

    private bool _isVisible;
    private Panel _panel = null!;
    private BodyStatusPanel _bodyPanel = null!;
    private Label _statsLabel = null!;
    private Label _cultivationLabel = null!;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
        {
            ContainerAdapter.InjectProperties(this, container);
        }

        BuildUI();
        _isVisible = false;
        Visible = false;
        GD.Print("[CharacterSheet] Ready");
    }

    private void BuildUI()
    {
        Theme = ParchmentTheme.Create();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        // Background.
        var bg = new ColorRect
        {
            Name = "Background",
            Color = new Color(0.05f, 0.03f, 0.02f, 0.7f),
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Stop;
        AddChild(bg);

        // Main panel (centered, 880×560).
        _panel = new Panel { Name = "CharacterSheetPanel" };
        _panel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        _panel.OffsetLeft = -440;
        _panel.OffsetRight = 440;
        _panel.OffsetTop = -280;
        _panel.OffsetBottom = 280;
        _panel.MouseFilter = MouseFilterEnum.Stop;
        AddChild(_panel);

        // Content.
        var outer = new VBoxContainer();
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        outer.OffsetLeft = 16;
        outer.OffsetRight = -16;
        outer.OffsetTop = 12;
        outer.OffsetBottom = -12;
        outer.AddThemeConstantOverride("separation", 8);
        _panel.AddChild(outer);

        // Header.
        var header = new Label
        {
            Text = "◆ Лист персонажа ◆",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        header.AddThemeFontSizeOverride("font_size", 24);
        header.AddThemeColorOverride("font_color", ParchmentTheme.InkBlack);
        outer.AddChild(header);

        // Content row: left = body, right = stats.
        var contentRow = new HBoxContainer
        {
            Name = "ContentRow",
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        contentRow.AddThemeConstantOverride("separation", 12);
        outer.AddChild(contentRow);

        // ── Left: Body status panel ──
        _bodyPanel = new BodyStatusPanel
        {
            Name = "BodyPanel",
            CustomMinimumSize = new Vector2(520, 440),
        };
        contentRow.AddChild(_bodyPanel);

        // ── Right: Stats + cultivation ──
        var rightWrap = new VBoxContainer
        {
            Name = "StatsWrap",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(300, 440),
        };
        contentRow.AddChild(rightWrap);

        // Stats label.
        var statsTitle = new Label
        {
            Text = "Характеристики:",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        statsTitle.AddThemeFontSizeOverride("font_size", 16);
        statsTitle.AddThemeColorOverride("font_color", ParchmentTheme.InkBlack);
        rightWrap.AddChild(statsTitle);

        _statsLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _statsLabel.AddThemeFontSizeOverride("font_size", 14);
        _statsLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkBlack);
        rightWrap.AddChild(_statsLabel);

        // Cultivation label.
        var cultTitle = new Label
        {
            Text = "Культивация:",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        cultTitle.AddThemeFontSizeOverride("font_size", 16);
        cultTitle.AddThemeColorOverride("font_color", ParchmentTheme.InkBlack);
        rightWrap.AddChild(cultTitle);

        _cultivationLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _cultivationLabel.AddThemeFontSizeOverride("font_size", 14);
        _cultivationLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkBlack);
        rightWrap.AddChild(_cultivationLabel);

        // Footer.
        var footer = new Label
        {
            Text = "C или Esc — закрыть",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        footer.AddThemeFontSizeOverride("font_size", 12);
        footer.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        outer.AddChild(footer);

        // Background click → close.
        bg.GuiInput += OnBackgroundClick;
    }

    private void OnBackgroundClick(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            Toggle();
        }
    }

    /// <summary>Toggle character sheet visibility.</summary>
    public void Toggle()
    {
        _isVisible = !_isVisible;
        Visible = _isVisible;
        if (_isVisible)
        {
            RefreshAll();
            GD.Print("[CharacterSheet] Opened");
        }
        else
        {
            GD.Print("[CharacterSheet] Closed");
        }
    }

    private void RefreshAll()
    {
        _bodyPanel?.RefreshFromBody();

        // Stats.
        if (Player != null)
        {
            _statsLabel.Text = $"ID: {Player.PlayerId}\n" +
                               $"Позиция: ({Player.Position.X}, {Player.Position.Y})\n" +
                               $"Состояние: {(Player.IsAlive ? "Жив" : "Мёртв")}";
        }

        // Cultivation.
        if (QiService != null)
        {
            _cultivationLabel.Text = $"Уровень: {QiService.CultivationLevel}\n" +
                                     $"Под-уровень: {QiService.SubLevel}\n" +
                                     $"Ци: {QiService.CurrentQi} / {QiService.MaxQi}\n" +
                                     $"Ёмкость ядра: {QiService.CoreCapacity}\n" +
                                     $"Проводимость: {QiService.Conductivity:F2}";
        }
    }
}

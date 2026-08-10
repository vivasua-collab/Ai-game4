#nullable enable
using Godot;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.UI;

/// <summary>
/// Main menu scene controller. Attached to the root Control of MainMenu.tscn.
/// Builds the UI programmatically (UI Builder pattern per docs_v2/07_ui/UI_DESIGN.md §1.5)
/// with the parchment theme, and routes button presses to <see cref="IGameSession"/> /
/// <see cref="ISaveService"/>.
/// </summary>
public partial class MainMenuController : Control
{
    [Inject] private IGameSession  Session    { get; set; } = null!;
    [Inject] private ISaveService  SaveService { get; set; } = null!;

    private Button  _newGameBtn  = null!;
    private Button  _loadGameBtn = null!;
    private Button  _settingsBtn = null!;
    private Button  _quitBtn     = null!;
    private Label   _titleLabel  = null!;

    private const string GameWorldScenePath = "res://scenes/GameWorld.tscn";

    public override void _Ready()
    {
        // Wire DI from the global GameBoot container (set by the autoload).
        var container = Scene.GameBoot.Container;
        if (container != null)
        {
            ContainerAdapter.InjectProperties(this, container);
        }
        else
        {
            GD.PushWarning("[MainMenu] GameBoot.Container is null — DI not wired.");
        }

        BuildUI();

        GD.Print("[MainMenu] Ready");
    }

    private void BuildUI()
    {
        // Apply parchment theme globally for this control subtree.
        Theme = ParchmentTheme.Create();

        // Ensure root Control fills the viewport.
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // ---- Background fill ----
        var bg = new ColorRect
        {
            Name = "Background",
            Color = ParchmentTheme.ParchmentBase,
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Stop;
        AddChild(bg);

        // ---- Title (top, full width, y=80..160) ----
        _titleLabel = new Label
        {
            Name = "Title",
            Text = "Симулятор Мира Культивации",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", 48);
        _titleLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkBlack);
        // Top-wide anchor: left=0, top=0, right=1, bottom=0; offsets position it.
        _titleLabel.AnchorLeft = 0f;
        _titleLabel.AnchorTop = 0f;
        _titleLabel.AnchorRight = 1f;
        _titleLabel.AnchorBottom = 0f;
        _titleLabel.OffsetLeft = 0;
        _titleLabel.OffsetTop = 80;
        _titleLabel.OffsetRight = 0;
        _titleLabel.OffsetBottom = 160;
        AddChild(_titleLabel);

        // ---- Subtitle (below title, full width, y=170..210) ----
        var subtitle = new Label
        {
            Name = "Subtitle",
            Text = "◆ Вознесение через Ци ◆",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        subtitle.AddThemeFontSizeOverride("font_size", 24);
        subtitle.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        subtitle.AnchorLeft = 0f;
        subtitle.AnchorTop = 0f;
        subtitle.AnchorRight = 1f;
        subtitle.AnchorBottom = 0f;
        subtitle.OffsetLeft = 0;
        subtitle.OffsetTop = 170;
        subtitle.OffsetRight = 0;
        subtitle.OffsetBottom = 210;
        AddChild(subtitle);

        // ---- Button container (centered, 400 wide) ----
        var buttonContainer = new VBoxContainer { Name = "Buttons" };
        buttonContainer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        buttonContainer.CustomMinimumSize = new Vector2(400, 300);
        buttonContainer.OffsetLeft = -200;
        buttonContainer.OffsetRight = 200;
        buttonContainer.OffsetTop = -150;
        buttonContainer.OffsetBottom = 150;
        buttonContainer.AddThemeConstantOverride("separation", 16);
        AddChild(buttonContainer);

        // ---- Buttons (with 4.7 hover-lift + press-shrink animations) ----
        _newGameBtn = UIFactory.CreateButton("NewGame",   "◆ Новая игра",        400, 50);
        _newGameBtn.Pressed += OnNewGame;
        UIFactory.AddHoverLift(_newGameBtn);
        UIFactory.AddPressShrink(_newGameBtn);
        buttonContainer.AddChild(_newGameBtn);

        _loadGameBtn = UIFactory.CreateButton("LoadGame", "◇ Загрузить игру",    400, 50);
        _loadGameBtn.Pressed += OnLoadGame;
        UIFactory.AddHoverLift(_loadGameBtn);
        UIFactory.AddPressShrink(_loadGameBtn);
        buttonContainer.AddChild(_loadGameBtn);

        _settingsBtn = UIFactory.CreateButton("Settings", "○ Настройки",          400, 50);
        _settingsBtn.Pressed += OnSettings;
        UIFactory.AddHoverLift(_settingsBtn);
        UIFactory.AddPressShrink(_settingsBtn);
        buttonContainer.AddChild(_settingsBtn);

        _quitBtn = UIFactory.CreateButton("Quit",     "✗ Выйти",                  400, 50);
        _quitBtn.Pressed += OnQuit;
        UIFactory.AddHoverLift(_quitBtn);
        UIFactory.AddPressShrink(_quitBtn);
        buttonContainer.AddChild(_quitBtn);

        // ---- Version label (bottom-right corner) ----
        var version = new Label
        {
            Name = "Version",
            Text = "v0.1.0 — Godot 4.7.1",
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        version.AddThemeFontSizeOverride("font_size", 14);
        version.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        version.AnchorLeft = 1f;
        version.AnchorTop = 1f;
        version.AnchorRight = 1f;
        version.AnchorBottom = 1f;
        version.OffsetLeft = -260;
        version.OffsetRight = -20;
        version.OffsetTop = -40;
        version.OffsetBottom = -10;
        AddChild(version);
    }

    // ---- Button handlers ----

    private void OnNewGame()
    {
        GD.Print("[MainMenu] New Game selected");
        try
        {
            Session?.NewGame(1);
            GetTree().ChangeSceneToFile(GameWorldScenePath);
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[MainMenu] NewGame failed: {ex}");
        }
    }

    private void OnLoadGame()
    {
        GD.Print("[MainMenu] Load Game selected");
        if (SaveService == null)
        {
            GD.PrintErr("[MainMenu] SaveService not wired");
            return;
        }

        var saves = SaveService.GetAllSaves();
        foreach (var s in saves)
            GD.Print($"  Save slot: {s}");

        if (SaveService.HasSave("quicksave"))
        {
            try
            {
                Session?.LoadGame("quicksave");
                GetTree().ChangeSceneToFile(GameWorldScenePath);
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[MainMenu] LoadGame failed: {ex}");
            }
        }
        else
        {
            GD.Print("[MainMenu] No quicksave slot — starting new game instead.");
            OnNewGame();
        }
    }

    private void OnSettings()
    {
        GD.Print("[MainMenu] Settings (stub)");
    }

    private void OnQuit()
    {
        GD.Print("[MainMenu] Quit selected");
        GetTree().Quit();
    }
}

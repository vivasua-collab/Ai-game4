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

        // ---- Background fill ----
        var bg = new ColorRect
        {
            Name = "Background",
            Color = ParchmentTheme.ParchmentBase,
        };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Stop;
        AddChild(bg);

        // ---- Title ----
        _titleLabel = new Label
        {
            Name = "Title",
            Text = "Симулятор Мира Культивации",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", 48);
        _titleLabel.AddThemeColorOverride("font_color", ParchmentTheme.InkBlack);
        _titleLabel.SetAnchorsPreset(LayoutPreset.CenterTop);
        _titleLabel.Size = new Vector2(1920, 80);
        _titleLabel.Position = new Vector2(0, 80);
        AddChild(_titleLabel);

        // ---- Subtitle ----
        var subtitle = new Label
        {
            Name = "Subtitle",
            Text = "◆ Вознесение через Ци ◆",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        subtitle.AddThemeFontSizeOverride("font_size", 24);
        subtitle.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        subtitle.SetAnchorsPreset(LayoutPreset.CenterTop);
        subtitle.Size = new Vector2(1920, 30);
        subtitle.Position = new Vector2(0, 160);
        AddChild(subtitle);

        // ---- Button container ----
        var buttonContainer = new VBoxContainer
        {
            Name = "Buttons",
        };
        buttonContainer.SetAnchorsPreset(LayoutPreset.Center);
        buttonContainer.CustomMinimumSize = new Vector2(400, 300);
        buttonContainer.Position = new Vector2(-200, -50);
        buttonContainer.AddThemeConstantOverride("separation", 16);
        AddChild(buttonContainer);

        // ---- Buttons ----
        _newGameBtn = UIFactory.CreateButton("NewGame",   "◆ Новая игра",        400, 50);
        _newGameBtn.Pressed += OnNewGame;
        buttonContainer.AddChild(_newGameBtn);

        _loadGameBtn = UIFactory.CreateButton("LoadGame", "◇ Загрузить игру",    400, 50);
        _loadGameBtn.Pressed += OnLoadGame;
        buttonContainer.AddChild(_loadGameBtn);

        _settingsBtn = UIFactory.CreateButton("Settings", "○ Настройки",          400, 50);
        _settingsBtn.Pressed += OnSettings;
        buttonContainer.AddChild(_settingsBtn);

        _quitBtn = UIFactory.CreateButton("Quit",     "✗ Выйти",                  400, 50);
        _quitBtn.Pressed += OnQuit;
        buttonContainer.AddChild(_quitBtn);

        // ---- Version label ----
        var version = new Label
        {
            Name = "Version",
            Text = "v0.1.0 — Iteration 4 (Godot 4)",
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        version.AddThemeFontSizeOverride("font_size", 14);
        version.AddThemeColorOverride("font_color", ParchmentTheme.InkFaded);
        version.SetAnchorsPreset(LayoutPreset.BottomRight);
        version.Size = new Vector2(200, 20);
        version.Position = new Vector2(1700, 1050);
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

#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;
using CultivationGame.Adapter.Input;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Main GameWorld scene controller. Attached to the root Node2D of GameWorld.tscn.
/// Sets up the camera, world root, tile rendering, player sprite, InputAdapter
/// child, SceneBuilder child, and HUD canvas layer.
///
/// Movement is applied directly to <see cref="IPlayerService"/> for v1 —
/// in a future iteration this will be moved into PlayerModule via events.
/// Sticky input flags (pause, inventory, save, etc.) are also processed here.
/// </summary>
public partial class GameWorldController : Node2D
{
    [Inject] private IGameSession        Session     { get; set; } = null!;
    [Inject] private IPlayerService      Player      { get; set; } = null!;
    [Inject] private IPlayerInputService PlayerInput { get; set; } = null!;
    [Inject] private ITimeService        Time        { get; set; } = null!;
    [Inject] private ITileService        Tiles       { get; set; } = null!;
    [Inject] private ISaveService        SaveService { get; set; } = null!;

    private Node2D        _worldRoot     = null!;
    private Camera2D      _camera        = null!;
    private Sprite2D      _playerSprite  = null!;
    private InputAdapter  _inputAdapter  = null!;
    private SceneBuilder  _sceneBuilder  = null!;
    private CanvasLayer   _hudCanvas     = null!;
    private Label         _timeLabel     = null!;
    private Label         _hudLabel      = null!;

    // Cached bounds of the test polygon (50x50) for player movement clamping.
    // In a future iteration these come from Entry.LocationCatalog.TestPolygon.
    private int _worldWidth  = 50;
    private int _worldHeight = 50;

    public override void _Ready()
    {
        // Wire DI from the global GameBoot container.
        var container = Scene.GameBoot.Container;
        if (container != null)
        {
            ContainerAdapter.InjectProperties(this, container);
        }
        else
        {
            GD.PushWarning("[GameWorld] GameBoot.Container is null — DI not wired.");
        }

        SetupWorld();
        SetupHUD();
        GD.Print("[GameWorldController] Ready");
    }

    // ---- World setup ----

    private void SetupWorld()
    {
        _worldRoot = new Node2D { Name = "WorldRoot" };
        AddChild(_worldRoot);

        // Camera: zoomed in 2x for visibility of 64px tiles.
        _camera = new Camera2D
        {
            Name = "MainCamera",
            Zoom = new Vector2(2f, 2f),
            PositionSmoothingEnabled = true,
            PositionSmoothingSpeed = 5.0f,
        };
        _worldRoot.AddChild(_camera);
        _camera.MakeCurrent();

        // Player sprite (procedural texture).
        _playerSprite = new Sprite2D
        {
            Name = "PlayerSprite",
            Texture = CreatePlayerTexture(),
            ZIndex = (int)RenderLayer.Player,
        };
        _worldRoot.AddChild(_playerSprite);

        // Render test polygon background (single big ColorRect for v1 — faster than 2500 quads).
        RenderTilesBackground();

        // Input adapter child node.
        _inputAdapter = new InputAdapter { Name = "InputAdapter" };
        AddChild(_inputAdapter);

        // Scene builder child node (it will use our _worldRoot as its parent).
        _sceneBuilder = new SceneBuilder { Name = "SceneBuilder" };
        _worldRoot.AddChild(_sceneBuilder);
    }

    private static ImageTexture CreatePlayerTexture()
    {
        var img = Image.CreateEmpty(48, 48, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        // Robe body (purple).
        for (int y = 16; y < 40; y++)
            for (int x = 12; x < 36; x++)
                img.SetPixel(x, y, new Color(0.30f, 0.20f, 0.50f));
        // Head (skin).
        for (int y = 4; y < 16; y++)
            for (int x = 16; x < 32; x++)
                img.SetPixel(x, y, new Color(0.90f, 0.75f, 0.60f));
        // Hair (black).
        for (int y = 4; y < 10; y++)
            for (int x = 16; x < 32; x++)
                img.SetPixel(x, y, new Color(0.10f, 0.05f, 0.02f));
        return ImageTexture.CreateFromImage(img);
    }

    private void RenderTilesBackground()
    {
        int tileSize = GameConstants.TILE_PIXELS;

        // Single grass-green ColorRect covering the whole polygon — fast.
        var bg = new ColorRect
        {
            Name = "TerrainBackground",
            Color = new Color(0.30f, 0.50f, 0.25f),
            Size = new Vector2(_worldWidth * tileSize, _worldHeight * tileSize),
            Position = new Vector2(0, 0),
            ZIndex = (int)RenderLayer.Terrain,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _worldRoot.AddChild(bg);

        // Decorative border around the polygon to make bounds visible.
        int borderThickness = 4;
        var borderColor = new Color(0.5f, 0.4f, 0.2f, 0.7f);
        var borderSize = new Vector2(_worldWidth * tileSize + borderThickness * 2,
                                     _worldHeight * tileSize + borderThickness * 2);

        AddBorderRect("BorderTop",    new Vector2(-borderThickness, -borderThickness),
                      new Vector2(borderSize.X, borderThickness), borderColor);
        AddBorderRect("BorderBottom", new Vector2(-borderThickness, _worldHeight * tileSize),
                      new Vector2(borderSize.X, borderThickness), borderColor);
        AddBorderRect("BorderLeft",   new Vector2(-borderThickness, -borderThickness),
                      new Vector2(borderThickness, borderSize.Y), borderColor);
        AddBorderRect("BorderRight",  new Vector2(_worldWidth * tileSize, -borderThickness),
                      new Vector2(borderThickness, borderSize.Y), borderColor);
    }

    private void AddBorderRect(string name, Vector2 pos, Vector2 size, Color color)
    {
        var rect = new ColorRect
        {
            Name = name,
            Color = color,
            Position = pos,
            Size = size,
            ZIndex = (int)RenderLayer.Objects,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _worldRoot.AddChild(rect);
    }

    // ---- HUD setup ----

    private void SetupHUD()
    {
        _hudCanvas = new CanvasLayer { Name = "HUDCanvas", Layer = 10 };
        AddChild(_hudCanvas);

        _hudLabel = new Label
        {
            Name = "HudHint",
            Text = "Тестовый полигон | Esc — пауза | B — инвентарь (stub) | F5 — сохранить",
        };
        _hudLabel.AddThemeFontSizeOverride("font_size", 16);
        _hudLabel.AddThemeColorOverride("font_color", new Color(0.94f, 0.83f, 0.66f));
        _hudLabel.Position = new Vector2(20, 20);
        _hudCanvas.AddChild(_hudLabel);

        _timeLabel = new Label { Name = "TimeLabel" };
        _timeLabel.AddThemeFontSizeOverride("font_size", 18);
        _timeLabel.AddThemeColorOverride("font_color", new Color(0.94f, 0.83f, 0.66f));
        _timeLabel.Position = new Vector2(20, 50);
        _hudCanvas.AddChild(_timeLabel);
    }

    // ---- Per-frame logic ----

    public override void _PhysicsProcess(double delta)
    {
        // Update player sprite position from PlayerService.
        if (_playerSprite != null && Player != null)
        {
            var pos = Player.Position;
            _playerSprite.Position = new Vector2(pos.X * GameConstants.TILE_PIXELS, pos.Y * GameConstants.TILE_PIXELS);
        }

        // Camera follows player.
        if (_camera != null && _playerSprite != null)
        {
            _camera.Position = _playerSprite.Position;
        }

        // Update time HUD label.
        if (_timeLabel != null && Time != null)
        {
            var t = Time.CurrentTime;
            _timeLabel.Text = $"{t.Year} г. {t.Month:D2}/{t.Day:D2} {t.Hour:D2}:{t.Minute:D2} | Скорость: {Time.Speed}";
        }

        HandleMovement();
        HandleStickyInput();
    }

    private void HandleMovement()
    {
        if (Player == null || PlayerInput == null) return;

        var frame = PlayerInput.CurrentFrame;
        if (frame.MoveDirection.X == 0f && frame.MoveDirection.Y == 0f) return;

        // Direct move 1 tile per tick (v1 simplification).
        var pos = Player.Position;
        int newX = pos.X + (int)frame.MoveDirection.X;
        int newY = pos.Y + (int)frame.MoveDirection.Y;
        newX = Mathf.Clamp(newX, 0, _worldWidth  - 1);
        newY = Mathf.Clamp(newY, 0, _worldHeight - 1);
        if (newX != pos.X || newY != pos.Y)
        {
            Player.MoveTo(newX, newY);
        }
    }

    private void HandleStickyInput()
    {
        if (PlayerInput == null || Time == null) return;

        // Esc (sticky "escape") → toggle pause.
        if (PlayerInput.IsPausePressed)
        {
            if (Time.IsPaused) Time.Resume();
            else               Time.Pause();
            GD.Print($"[GameWorld] Pause toggled: {Time.IsPaused}");
        }

        if (PlayerInput.IsInventoryPressed)
        {
            GD.Print("[GameWorld] Inventory (stub)");
        }

        if (PlayerInput.IsQuickSavePressed)
        {
            GD.Print("[GameWorld] Quick save (stub)");
            // SaveService?.Save("quicksave", SaveSlotType.QuickSave);
        }

        if (PlayerInput.IsQuickLoadPressed)
        {
            GD.Print("[GameWorld] Quick load (stub)");
            // SaveService?.Load("quicksave");
        }
    }

    // ---- Public accessors for child nodes / tests ----

    public Node2D WorldRoot => _worldRoot;
    public Camera2D Camera => _camera;
    public Sprite2D PlayerSprite => _playerSprite;
    public IReadOnlyList<Node> HudChildren => _hudCanvas?.GetChildren() as IReadOnlyList<Node> ?? Array.Empty<Node>();
}

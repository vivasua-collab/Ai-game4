#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;
using CultivationGame.Adapter.Input;
using CultivationGame.Modules.Player;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Main GameWorld scene controller. Attached to the root Node2D of GameWorld.tscn.
/// Sets up the camera, world root, tile rendering (delegated to <see cref="SceneBuilder"/>),
/// player sprite, InputAdapter child, and HUD canvas layer.
///
/// Movement is applied directly to <see cref="IPlayerService"/> for v1 —
/// in a future iteration this will be moved into PlayerModule via events.
/// Sticky input flags (pause, inventory, save, etc.) are also processed here.
///
/// Godot 4.7 notes:
///  • Camera2D.PositionSmoothingEnabled + ProcessCallback=Physics for stable follow.
///  • CanvasLayer for HUD (not ScreenOverlay) — keeps HUD in screen space.
///  • Labels use AddThemeFontSizeOverride + AddThemeColorOverride (theme system).
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
    private Sprite2D      _playerShadow  = null!;
    private InputAdapter  _inputAdapter  = null!;
    private SceneBuilder  _sceneBuilder  = null!;
    private CanvasLayer   _hudCanvas     = null!;
    private Label         _timeLabel     = null!;
    private Label         _hudLabel      = null!;

    // Cached bounds of the test polygon (50×50) for camera limits.
    private int _worldWidth  = 50;
    private int _worldHeight = 50;
    private int _debugFrameCount;  // for periodic debug logging

    // Speed change debounce — prevents rapid cycling when key held.
    // Minimum 1 real second between speed changes.
    private float _speedChangeCooldown;
    private const float SpeedChangeCooldownSec = 1.0f;

    // NOTE: Movement is handled by PlayerModule.Tick() — tied to the tick system,
    // NOT to _PhysicsProcess. This ensures movement scales with TimeSpeed
    // (Normal=1 tile/sec, Fast=5 tiles/sec, Quick=15 tiles/sec).
    // GameWorldController only renders the player position from PlayerService.

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

        // Camera: zoomed in 3× for better view of player + tiles.
        _camera = new Camera2D
        {
            Name = "MainCamera",
            Zoom = new Vector2(3f, 3f),
            PositionSmoothingEnabled = true,
            PositionSmoothingSpeed = 8.0f,
            ProcessCallback = Camera2D.Camera2DProcessCallback.Physics,
            LimitLeft = -100,
            LimitTop = -100,
            LimitRight = 50 * 64 + 100,
            LimitBottom = 50 * 64 + 100,
        };
        _worldRoot.AddChild(_camera);
        _camera.MakeCurrent();

        // Player shadow (simple ellipse beneath player).
        var shadow = new Sprite2D
        {
            Name = "PlayerShadow",
            Texture = CreateShadowTexture(),
            ZIndex = (int)RenderLayer.Player - 1,
            Modulate = new Color(0, 0, 0, 0.35f),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
        _worldRoot.AddChild(shadow);
        _playerShadow = shadow;

        // Player sprite (procedural texture — centered on tile center).
        _playerSprite = new Sprite2D
        {
            Name = "PlayerSprite",
            Texture = CreatePlayerTexture(),
            ZIndex = (int)RenderLayer.Player,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
        _worldRoot.AddChild(_playerSprite);

        // Render decorative border around the polygon to make bounds visible.
        RenderBorder();

        // Input adapter child node.
        _inputAdapter = new InputAdapter { Name = "InputAdapter" };
        AddChild(_inputAdapter);

        // Scene builder child node (creates terrain MultiMesh + camera follow logic).
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
        // Eyes.
        img.SetPixel(20, 12, new Color(0.05f, 0.05f, 0.05f));
        img.SetPixel(27, 12, new Color(0.05f, 0.05f, 0.05f));
        // Robe trim (gold accent).
        for (int x = 12; x < 36; x++)
        {
            img.SetPixel(x, 16, new Color(0.72f, 0.53f, 0.04f));
            img.SetPixel(x, 38, new Color(0.72f, 0.53f, 0.04f));
        }
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>Simple ellipse shadow texture (32×16).</summary>
    private static ImageTexture CreateShadowTexture()
    {
        var img = Image.CreateEmpty(32, 16, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        // Ellipse: centered, wider than tall.
        float cx = 16f, cy = 8f;
        float rx = 13f, ry = 6f;
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float dx = (x - cx) / rx;
                float dy = (y - cy) / ry;
                if (dx * dx + dy * dy <= 1.0f)
                    img.SetPixel(x, y, new Color(0, 0, 0, 0.5f));
            }
        }
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>
    /// Decorative border around the test polygon — 4 thin ColorRects.
    /// Uses 4.7 Control.MouseFilterEnum.Ignore so they don't block input.
    /// </summary>
    private void RenderBorder()
    {
        int tileSize = GameConstants.TILE_PIXELS;
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

        // HUD hint label (top-left).
        _hudLabel = new Label
        {
            Name = "HudHint",
            Text = "Тестовый полигон | Esc — пауза | B — инвентарь (stub) | F5 — сохранить",
        };
        _hudLabel.AddThemeFontSizeOverride("font_size", 16);
        _hudLabel.AddThemeColorOverride("font_color", new Color(0.94f, 0.83f, 0.66f));
        // 4.7: Label.Position works for CanvasLayer children (screen-space).
        _hudLabel.Position = new Vector2(20, 20);
        _hudCanvas.AddChild(_hudLabel);

        // Time label (below HUD hint).
        _timeLabel = new Label { Name = "TimeLabel" };
        _timeLabel.AddThemeFontSizeOverride("font_size", 18);
        _timeLabel.AddThemeColorOverride("font_color", new Color(0.94f, 0.83f, 0.66f));
        _timeLabel.Position = new Vector2(20, 50);
        _hudCanvas.AddChild(_timeLabel);
    }

    // ---- Per-frame logic ----

    public override void _PhysicsProcess(double delta)
    {
        // Update player sprite + shadow position from PlayerService (centered on tile).
        if (_playerSprite != null && Player != null)
        {
            var pos = Player.Position;
            float px = pos.X * GameConstants.TILE_PIXELS + GameConstants.TILE_PIXELS / 2f;
            float py = pos.Y * GameConstants.TILE_PIXELS + GameConstants.TILE_PIXELS / 2f;
            _playerSprite.Position = new Vector2(px, py);
            // Shadow slightly below + offset for depth illusion (2.5D hint).
            if (_playerShadow != null)
                _playerShadow.Position = new Vector2(px, py + 8f);
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

        // Debug: log player position + input every 60 frames (~1 sec).
        _debugFrameCount++;
        if (_debugFrameCount >= 60 && Player != null && PlayerInput != null)
        {
            _debugFrameCount = 0;
            var pos = Player.Position;
            var frame = PlayerInput.CurrentFrame;
            GD.Print($"[Debug] Player @ ({pos.X},{pos.Y}) | input=({frame.MoveDirection.X:F2},{frame.MoveDirection.Y:F2}) | speed={Time?.Speed}");
        }

        // Movement is handled by PlayerModule.Tick() (tick-based, not FPS-based).
        // This controller only renders the player sprite from PlayerService.Position.
        HandleStickyInput();
        HandleMouseClick();
    }

    /// <summary>
    /// Mouse click movement: left-click on map → set destination tile.
    /// Player will move towards it each tick (handled by PlayerModule).
    /// Any keyboard movement input clears the mouse destination.
    /// </summary>
    private void HandleMouseClick()
    {
        if (Player == null || _camera == null) return;

        // Left mouse button click → set destination (one-shot, not while held).
        // Uses "mouse_click" action (LMB) registered in InputMapInitializer.
        if (!Godot.Input.IsActionJustPressed("mouse_click")) return;

        // Get mouse position — screen + world.
        var mouseScreenPos = GetViewport().GetMousePosition();
        var mouseWorldPos = GetGlobalMousePosition();

        // Convert world position to tile coordinates.
        int tileX = (int)(mouseWorldPos.X / GameConstants.TILE_PIXELS);
        int tileY = (int)(mouseWorldPos.Y / GameConstants.TILE_PIXELS);
        tileX = Mathf.Clamp(tileX, 0, 49);
        tileY = Mathf.Clamp(tileY, 0, 49);

        // Player current position.
        var playerPos = Player.Position;
        int dx = tileX - playerPos.X;
        int dy = tileY - playerPos.Y;
        int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));  // Chebyshev distance

        // Full debug log with movement vector.
        GD.Print($"[Mouse] Click detected!");
        GD.Print($"[Mouse] Screen pos: ({mouseScreenPos.X:F0}, {mouseScreenPos.Y:F0})");
        GD.Print($"[Mouse] World pos: ({mouseWorldPos.X:F0}, {mouseWorldPos.Y:F0})");
        GD.Print($"[Mouse] Tile target: ({tileX}, {tileY})");
        GD.Print($"[Mouse] Player at: ({playerPos.X}, {playerPos.Y})");
        GD.Print($"[Mouse] Movement vector: dx={dx}, dy={dy}, distance={dist} tiles");

        // Set destination on PlayerModule via container.
        var container = Scene.GameBoot.Container;
        if (container != null)
        {
            var playerModule = container.Resolve<PlayerModule>();
            playerModule.SetMouseDestination(tileX, tileY);
            GD.Print($"[Mouse] Destination set on PlayerModule ✓");
        }
        else
        {
            GD.PrintErr("[Mouse] ERROR: GameBoot.Container is null!");
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

        // Time speed control: +/PageUp = faster, -/PageDown = slower.
        // Debounce: max 1 change per real second (prevents rapid cycling).
        // Does NOT include Paused — pause is only via Esc.
        _speedChangeCooldown -= (float)GetPhysicsProcessDeltaTime();
        if (_speedChangeCooldown <= 0)
        {
            if (PlayerInput.IsTimeSpeedUpPressed)
            {
                if (Time.IsPaused) { Time.Resume(); Time.Speed = TimeSpeed.Normal; }
                else Time.Speed = CycleSpeedUp(Time.Speed);
                GD.Print($"[GameWorld] Time speed UP → {Time.Speed} ({(int)Time.Speed} tps)");
                _speedChangeCooldown = SpeedChangeCooldownSec;
            }
            else if (PlayerInput.IsTimeSpeedDownPressed)
            {
                if (Time.IsPaused) { Time.Resume(); Time.Speed = TimeSpeed.Normal; }
                else Time.Speed = CycleSpeedDown(Time.Speed);
                GD.Print($"[GameWorld] Time speed DOWN → {Time.Speed} ({(int)Time.Speed} tps)");
                _speedChangeCooldown = SpeedChangeCooldownSec;
            }
        }
    }

    /// <summary>Cycle speed up: Normal → Fast → Quick (no Paused).</summary>
    private static TimeSpeed CycleSpeedUp(TimeSpeed current)
    {
        return current switch
        {
            TimeSpeed.Normal => TimeSpeed.Fast,
            TimeSpeed.Fast   => TimeSpeed.Quick,
            TimeSpeed.Quick  => TimeSpeed.Quick,  // max
            _ => TimeSpeed.Normal,
        };
    }

    /// <summary>Cycle speed down: Quick → Fast → Normal (no Paused).</summary>
    private static TimeSpeed CycleSpeedDown(TimeSpeed current)
    {
        return current switch
        {
            TimeSpeed.Quick  => TimeSpeed.Fast,
            TimeSpeed.Fast   => TimeSpeed.Normal,
            TimeSpeed.Normal => TimeSpeed.Normal,  // min (no pause)
            _ => TimeSpeed.Normal,
        };
    }

    // ---- Public accessors for child nodes / tests ----

    public Node2D WorldRoot => _worldRoot;
    public Camera2D Camera => _camera;
    public Sprite2D PlayerSprite => _playerSprite;
    public IReadOnlyList<Node> HudChildren => _hudCanvas?.GetChildren() as IReadOnlyList<Node> ?? Array.Empty<Node>();
}

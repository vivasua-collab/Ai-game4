#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;
using CultivationGame.Adapter.Input;
using CultivationGame.Adapter.UI;
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
    private InventoryWindow _inventoryWindow = null!;
    private CanvasLayer   _hudCanvas     = null!;
    private Label         _timeLabel     = null!;
    private Label         _hudLabel      = null!;

    // Cached bounds of the test polygon (50×50) for camera limits.
    private int _worldWidth  = 50;
    private int _worldHeight = 50;
    private int _debugFrameCount;  // for periodic debug logging

    // Free movement — pixel-based, not tile-snap.
    // Visual position is continuous (float). Tile position derived from it.
    private Vector2 _visualPosition;
    private Vector2? _mouseTarget;  // null = keyboard, non-null = mouse click target
    private const float MoveSpeedPixels = 180.0f;  // pixels per second at Normal speed
    private const float RunSpeedMultiplier = 1.8f;
    private bool _positionInitialized;

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

        // Resolve map dimensions from TileService (fall back to defaults).
        // Audit issue #15: replace hardcoded "50".
        int mapW = Tiles != null && Tiles.MapWidth > 0 ? Tiles.MapWidth : GameConstants.DEFAULT_MAP_WIDTH;
        int mapH = Tiles != null && Tiles.MapHeight > 0 ? Tiles.MapHeight : GameConstants.DEFAULT_MAP_HEIGHT;
        _worldWidth = mapW;
        _worldHeight = mapH;

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
            LimitRight = _worldWidth * GameConstants.TILE_PIXELS + 100,
            LimitBottom = _worldHeight * GameConstants.TILE_PIXELS + 100,
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

        // Scene builder child node (creates terrain sprites + transition renderer).
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

        // Time label — TOP line (visible, parchment color).
        _timeLabel = new Label { Name = "TimeLabel" };
        _timeLabel.AddThemeFontSizeOverride("font_size", 18);
        _timeLabel.AddThemeColorOverride("font_color", new Color(0.94f, 0.83f, 0.66f));
        _timeLabel.Position = new Vector2(20, 10);
        _hudCanvas.AddChild(_timeLabel);

        // Hotkey legend — BOTTOM of screen, black color.
        _hudLabel = new Label
        {
            Name = "HudHint",
            Text = "WASD — движение | Shift — бег | ЛКМ — идти к точке | Колесо — зум\n" +
                   "Esc — пауза | PageUp/PageDown — скорость | F5 — сохранение | F9 — загрузка\n" +
                   "E — взаимодействие | B — инвентарь | R — отдых | F — добыча\n" +
                   "J — журнал | T — техники | C — персонаж | Q — квесты | M — карта | N — миникарта",
        };
        _hudLabel.AddThemeFontSizeOverride("font_size", 13);
        _hudLabel.AddThemeColorOverride("font_color", new Color(0.1f, 0.08f, 0.05f));  // near-black
        _hudLabel.Position = new Vector2(20, 1020);  // bottom of 1080p screen
        _hudCanvas.AddChild(_hudLabel);

        // Inventory window (opens with B key) — must be created AFTER _hudCanvas.
        _inventoryWindow = new InventoryWindow { Name = "InventoryWindow" };
        _hudCanvas.AddChild(_inventoryWindow);
    }

    // ---- Per-frame logic ----

    /// <summary>
    /// Handle mouse wheel for camera zoom.
    /// Wheel up = zoom in (Zoom += 0.5), wheel down = zoom out (Zoom -= 0.5).
    /// Range: 1.0 (far) to 8.0 (close).
    /// </summary>
    public override void _Input(InputEvent @event)
    {
        if (_camera == null) return;

        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            switch (mb.ButtonIndex)
            {
                case MouseButton.WheelUp:
                    var zoomIn = _camera.Zoom with { X = _camera.Zoom.X + 0.5f, Y = _camera.Zoom.Y + 0.5f };
                    if (zoomIn.X <= 8.0f)
                        _camera.Zoom = zoomIn;
                    break;
                case MouseButton.WheelDown:
                    var zoomOut = _camera.Zoom with { X = _camera.Zoom.X - 0.5f, Y = _camera.Zoom.Y - 0.5f };
                    if (zoomOut.X >= 1.0f)
                        _camera.Zoom = zoomOut;
                    break;
                case MouseButton.Middle:
                    // Middle click = reset zoom to 3× and center on player.
                    _camera.Zoom = new Vector2(3f, 3f);
                    break;
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // Initialize visual position on first frame (snap to tile center).
        if (!_positionInitialized && Player != null)
        {
            var pos = Player.Position;
            _visualPosition = new Vector2(
                pos.X * GameConstants.TILE_PIXELS + GameConstants.TILE_PIXELS / 2f,
                pos.Y * GameConstants.TILE_PIXELS + GameConstants.TILE_PIXELS / 2f);
            _positionInitialized = true;
        }

        // Free movement — continuous pixel-based, inspired by 2.5D demo.
        HandleFreeMovement(delta);

        // Update sprite + shadow from visual position.
        if (_playerSprite != null)
        {
            _playerSprite.Position = _visualPosition;
            if (_playerShadow != null)
                _playerShadow.Position = new Vector2(_visualPosition.X, _visualPosition.Y + 8f);
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

        HandleStickyInput();
        HandleMouseClick();

        // PLR-E06: Reset sticky frame flags AFTER all Adapter consumers
        // (HandleStickyInput, HandleMouseClick) have read them. Previously
        // this was called from PlayerModule.Tick() which runs BEFORE the
        // main scene's _PhysicsProcess (via GameBoot autoload ordering),
        // clearing flags before the Adapter could read them.
        PlayerInput?.ResetFrameFlags();
    }

    /// <summary>
    /// Free pixel-based movement. Reads WASD or moves towards mouse target.
    /// Updates _visualPosition continuously. Syncs tile position to PlayerService
    /// when crossing tile boundary.
    /// </summary>
    private void HandleFreeMovement(double delta)
    {
        if (Player == null) return;

        // Check if paused — no movement when paused.
        if (Time != null && Time.IsPaused) return;

        // Get input vector (normalized -1..1 per axis).
        Vector2 moveVec = Godot.Input.GetVector("move_left", "move_right", "move_up", "move_down");

        // Speed: base pixels/sec × delta × run multiplier × time speed multiplier.
        float speedMult = 1.0f;
        if (Godot.Input.IsActionPressed("run")) speedMult = RunSpeedMultiplier;

        // Time speed affects movement (faster game = faster movement).
        if (Time != null) speedMult *= (int)Time.Speed;

        if (moveVec != Vector2.Zero)
        {
            // Keyboard movement — clear mouse target.
            _mouseTarget = null;
            _visualPosition += moveVec * MoveSpeedPixels * speedMult * (float)delta;

            // Clamp to world bounds. Use TileService dimensions when available,
            // falling back to GameConstants.DEFAULT_MAP_* (audit issue #15).
            int mapW = Tiles != null && Tiles.MapWidth > 0 ? Tiles.MapWidth : GameConstants.DEFAULT_MAP_WIDTH;
            int mapH = Tiles != null && Tiles.MapHeight > 0 ? Tiles.MapHeight : GameConstants.DEFAULT_MAP_HEIGHT;
            float maxX = mapW * GameConstants.TILE_PIXELS - GameConstants.TILE_PIXELS / 2f;
            float maxY = mapH * GameConstants.TILE_PIXELS - GameConstants.TILE_PIXELS / 2f;
            _visualPosition = new Vector2(
                Mathf.Clamp(_visualPosition.X, GameConstants.TILE_PIXELS / 2f, maxX),
                Mathf.Clamp(_visualPosition.Y, GameConstants.TILE_PIXELS / 2f, maxY));
        }
        else if (_mouseTarget.HasValue)
        {
            // Mouse click movement — move towards target pixel position.
            var target = _mouseTarget.Value;
            var diff = target - _visualPosition;
            float dist = diff.Length();

            if (dist < 4f)  // close enough — snap
            {
                _visualPosition = target;
                _mouseTarget = null;
            }
            else
            {
                var dir = diff.Normalized();
                _visualPosition += dir * MoveSpeedPixels * speedMult * (float)delta;
            }
        }

        // Sync tile position to PlayerService (for game logic).
        int mapWidth = Tiles != null && Tiles.MapWidth > 0 ? Tiles.MapWidth : GameConstants.DEFAULT_MAP_WIDTH;
        int mapHeight = Tiles != null && Tiles.MapHeight > 0 ? Tiles.MapHeight : GameConstants.DEFAULT_MAP_HEIGHT;
        int tileX = (int)(_visualPosition.X / GameConstants.TILE_PIXELS);
        int tileY = (int)(_visualPosition.Y / GameConstants.TILE_PIXELS);
        tileX = Mathf.Clamp(tileX, 0, mapWidth - 1);
        tileY = Mathf.Clamp(tileY, 0, mapHeight - 1);
        var currentTile = Player.Position;
        if (currentTile.X != tileX || currentTile.Y != tileY)
        {
            Player.MoveTo(tileX, tileY);
        }
    }

    /// <summary>
    /// Mouse click movement: left-click on map → set destination tile.
    /// Player will move towards it each tick (handled by PlayerModule).
    /// Any keyboard movement input clears the mouse destination.
    /// </summary>
    private void HandleMouseClick()
    {
        if (Player == null || _camera == null) return;

        // Left mouse button click → set pixel target for free movement.
        if (!Godot.Input.IsActionJustPressed("mouse_click")) return;

        var mouseWorldPos = GetGlobalMousePosition();

        // Clamp to world bounds.
        int mapW = Tiles != null && Tiles.MapWidth > 0 ? Tiles.MapWidth : GameConstants.DEFAULT_MAP_WIDTH;
        int mapH = Tiles != null && Tiles.MapHeight > 0 ? Tiles.MapHeight : GameConstants.DEFAULT_MAP_HEIGHT;
        float maxX = mapW * GameConstants.TILE_PIXELS - GameConstants.TILE_PIXELS / 2f;
        float maxY = mapH * GameConstants.TILE_PIXELS - GameConstants.TILE_PIXELS / 2f;
        var target = new Vector2(
            Mathf.Clamp(mouseWorldPos.X, GameConstants.TILE_PIXELS / 2f, maxX),
            Mathf.Clamp(mouseWorldPos.Y, GameConstants.TILE_PIXELS / 2f, maxY));

        _mouseTarget = target;

        // Debug logging (disabled — uncomment to re-enable).
        //int tileX = (int)(target.X / GameConstants.TILE_PIXELS);
        //int tileY = (int)(target.Y / GameConstants.TILE_PIXELS);
        //var playerPos = Player.Position;
        //int dx = tileX - playerPos.X;
        //int dy = tileY - playerPos.Y;
        //int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
        //GD.Print($"[Mouse] Click → world ({target.X:F0}, {target.Y:F0}) → tile ({tileX}, {tileY})");
        //GD.Print($"[Mouse] Player at ({playerPos.X}, {playerPos.Y}), dx={dx}, dy={dy}, dist={dist}");
    }

    private void HandleStickyInput()
    {
        if (PlayerInput == null || Time == null) return;

        // Esc (sticky "escape") → toggle pause (but not when inventory is open).
        if (PlayerInput.IsPausePressed && (_inventoryWindow == null || !_inventoryWindow.Visible))
        {
            if (Time.IsPaused) Time.Resume();
            else               Time.Pause();
            GD.Print($"[GameWorld] Pause toggled: {Time.IsPaused}");
        }
        // If inventory is open and Esc pressed, close it instead of pausing.
        else if (PlayerInput.IsPausePressed && _inventoryWindow != null && _inventoryWindow.Visible)
        {
            _inventoryWindow.Toggle();
        }

        if (PlayerInput.IsInventoryPressed)
        {
            _inventoryWindow?.Toggle();
        }

        // Suppress game input when inventory is open.
        if (_inputAdapter != null && _inventoryWindow != null)
        {
            _inputAdapter.SetOverUI(_inventoryWindow.Visible);
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

        // Time speed control: PageUp = faster, PageDown = slower.
        // Debounce: max 1 change per real second (prevents rapid cycling).
        // Does NOT include Paused — pause is only via Esc.
        _speedChangeCooldown -= (float)GetPhysicsProcessDeltaTime();
        if (_speedChangeCooldown <= 0)
        {
            if (PlayerInput.IsTimeSpeedUpPressed)
            {
                if (Time.IsPaused) { Time.Resume(); Time.Speed = TimeSpeed.Normal; }
                else Time.Speed = CycleSpeedUp(Time.Speed);
#if DEBUG_SPEED_LOG
                GD.Print($"[GameWorld] Time speed UP → {Time.Speed} ({(int)Time.Speed} tps)");
#endif
                _speedChangeCooldown = SpeedChangeCooldownSec;
            }
            else if (PlayerInput.IsTimeSpeedDownPressed)
            {
                if (Time.IsPaused) { Time.Resume(); Time.Speed = TimeSpeed.Normal; }
                else Time.Speed = CycleSpeedDown(Time.Speed);
#if DEBUG_SPEED_LOG
                GD.Print($"[GameWorld] Time speed DOWN → {Time.Speed} ({(int)Time.Speed} tps)");
#endif
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

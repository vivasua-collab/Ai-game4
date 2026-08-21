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
    [Inject] private IItemDatabaseService ItemDatabase { get; set; } = null!;
    [Inject] private IInventoryService   Inventory   { get; set; } = null!;
    [Inject] private IGroundItemService  GroundItems { get; set; } = null!;

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
    private Label         _toastLabel    = null!;
    private float         _toastTimer    = 0f;

    // Cached bounds of the test polygon (50×50) for camera limits.
    private int _worldWidth  = 50;
    private int _worldHeight = 50;
    private int _debugFrameCount;  // for periodic debug logging

    // Free movement — pixel-based, not tile-snap.
    // Visual position is continuous (float). Tile position derived from it.
    private Vector2 _visualPosition;
    private Vector2? _mouseTarget;  // null = keyboard, non-null = mouse click target
    private const float MoveSpeedPixels = 180.0f;  // pixels per second at Normal speed

    // Redraw throttle for viewport-culled renderers (~10 Hz).
    private const float RedrawIntervalSec = 0.1f;
    private float _redrawCooldown = 0f;
    private const float RunSpeedMultiplier = 1.8f;
    private bool _positionInitialized;
    private bool _wasPausedBeforeInventory; // track if game was paused before opening inventory
    private bool _overweightNotified; // debounce overweight toast

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

        // Toast label — top-center, for harvest/interaction feedback.
        _toastLabel = new Label
        {
            Name = "ToastLabel",
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false,
        };
        _toastLabel.AddThemeFontSizeOverride("font_size", 18);
        _toastLabel.AddThemeColorOverride("font_color", new Color(0.98f, 0.85f, 0.3f));
        _toastLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
        _toastLabel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _toastLabel.OffsetTop = 60;
        _toastLabel.OffsetLeft = -200;
        _toastLabel.OffsetRight = 200;
        _hudCanvas.AddChild(_toastLabel);

        // Inventory window (opens with B key) — must be created AFTER _hudCanvas.
        _inventoryWindow = new InventoryWindow { Name = "InventoryWindow" };
        _hudCanvas.AddChild(_inventoryWindow);
    }

    // ---- Per-frame logic ----

    // _Input() REMOVED — mouse wheel zoom moved to _UnhandledInput.
    // Old _Input received ALL events (including wheel) before UI processed them,
    // causing zoom to fire when scrolling inventory list.
    // Now _UnhandledInput only fires if UI (ScrollContainer) didn't consume.

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

        // Redraw tile renderers every few frames so viewport culling updates
        // as camera/player moves. Throttle to ~10 Hz to avoid per-frame redraw cost.
        _redrawCooldown -= (float)delta;
        if (_redrawCooldown <= 0 && _sceneBuilder != null)
        {
            _sceneBuilder.QueueRedrawAll();
            _redrawCooldown = RedrawIntervalSec;
        }

        // Update time HUD label.
        if (_timeLabel != null && Time != null)
        {
            var t = Time.CurrentTime;
            _timeLabel.Text = $"{t.Year} г. {t.Month:D2}/{t.Day:D2} {t.Hour:D2}:{t.Minute:D2} | Скорость: {Time.Speed}";
        }

        // Toast timer: hide toast after expiry.
        if (_toastLabel != null && _toastLabel.Visible)
        {
            _toastTimer -= (float)delta;
            if (_toastTimer <= 0)
            {
                _toastLabel.Visible = false;
            }
        }

        HandleStickyInput();
        // LMB movement is handled in _UnhandledInput (respects UI consumption).
        // HandleMouseClick() was removed — it used polling which bypassed Godot's
        // input propagation chain, causing player to move when clicking UI.

        // PLR-E06: Reset sticky frame flags AFTER all Adapter consumers
        // (HandleStickyInput) have read them. Previously
        // this was called from PlayerModule.Tick() which runs BEFORE the
        // main scene's _PhysicsProcess (via GameBoot autoload ordering),
        // clearing flags before the Adapter could read them.
        PlayerInput?.ResetFrameFlags();
    }

    /// <summary>
    /// Unhandled input: receives input events NOT consumed by UI Controls.
    /// Handles: LMB click (move), mouse wheel (zoom), middle click (reset zoom).
    /// When inventory is open, ScrollContainer consumes wheel events → zoom doesn't fire.
    ///
    /// Design: docs/docs_v2/07_ui/MOUSE_INPUT_SCHEME.md
    /// </summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (Player == null || _camera == null) return;

        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            switch (mb.ButtonIndex)
            {
                case MouseButton.Left:
                {
                    // LMB click → set pixel target for free movement.
                    var mouseWorldPos = GetGlobalMousePosition();
                    int mapW = Tiles != null && Tiles.MapWidth > 0 ? Tiles.MapWidth : GameConstants.DEFAULT_MAP_WIDTH;
                    int mapH = Tiles != null && Tiles.MapHeight > 0 ? Tiles.MapHeight : GameConstants.DEFAULT_MAP_HEIGHT;
                    float maxX = mapW * GameConstants.TILE_PIXELS - GameConstants.TILE_PIXELS / 2f;
                    float maxY = mapH * GameConstants.TILE_PIXELS - GameConstants.TILE_PIXELS / 2f;
                    var target = new Vector2(
                        Mathf.Clamp(mouseWorldPos.X, GameConstants.TILE_PIXELS / 2f, maxX),
                        Mathf.Clamp(mouseWorldPos.Y, GameConstants.TILE_PIXELS / 2f, maxY));
                    _mouseTarget = target;
                    break;
                }
                case MouseButton.WheelUp:
                {
                    var zoomIn = _camera.Zoom with { X = _camera.Zoom.X + 0.5f, Y = _camera.Zoom.Y + 0.5f };
                    if (zoomIn.X <= 8.0f)
                        _camera.Zoom = zoomIn;
                    break;
                }
                case MouseButton.WheelDown:
                {
                    var zoomOut = _camera.Zoom with { X = _camera.Zoom.X - 0.5f, Y = _camera.Zoom.Y - 0.5f };
                    if (zoomOut.X >= 1.0f)
                        _camera.Zoom = zoomOut;
                    break;
                }
                case MouseButton.Middle:
                    _camera.Zoom = new Vector2(3f, 3f);
                    break;
            }
        }
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

        // Overweight penalty: movement speed drops as carry weight exceeds max.
        // Ratio 0 = no penalty, 1.0 (2× max) = 0.5× speed, 3.0 (4× max) = 0.25× speed.
        // Formula: speedMult *= 1.0 / (1.0 + ratio). Capped ratio at 3.0 → min 0.25× speed.
        if (Inventory != null && Inventory.IsOverweight)
        {
            float ratio = Inventory.OverweightRatio;
            float overweightPenalty = 1.0f / (1.0f + ratio);
            speedMult *= overweightPenalty;
            // Show overweight toast once when crossing threshold (debounced).
            if (!_overweightNotified)
            {
                _overweightNotified = true;
                float curW = Inventory.GetCurrentWeight();
                float maxW = Inventory.GetEffectiveMaxWeight();
                ShowToast($"⚠ Перевес! {curW:F1}/{maxW:F1} кг — скорость снижена");
            }
        }
        else if (_overweightNotified)
        {
            // Reset notification when back to normal weight.
            _overweightNotified = false;
            ShowToast("Вес в норме");
        }

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

    // HandleMouseClick() REMOVED — replaced by _UnhandledInput override above.
    // Old impl used Godot.Input.IsActionJustPressed (polling) which bypassed
    // Godot's input propagation chain, causing player to move when clicking UI.
    // See docs/docs_v2/07_ui/MOUSE_INPUT_SCHEME.md for details.

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
            // Pause game time when inventory opens, resume when closes.
            // Rationale: inventory management is a planning activity (like Kenshi/RimWorld).
            // Player should be able to examine items, equip, drag&drop without time pressure.
            // This does NOT affect real-time input (mouse, keyboard) — only tick-based simulation.
            if (_inventoryWindow != null && Time != null)
            {
                if (_inventoryWindow.Visible)
                {
                    _wasPausedBeforeInventory = Time.IsPaused;
                    if (!Time.IsPaused) Time.Pause();
                }
                else
                {
                    // Only resume if we paused for inventory (not if already paused before).
                    if (!_wasPausedBeforeInventory && Time.IsPaused)
                        Time.Resume();
                }
            }
        }

        // F key: harvest resource from tile under cursor (within distance).
        if (PlayerInput.IsHarvestPressed)
        {
            HandleHarvest();
        }

        // E key: pick up nearest ground item (within pickup distance).
        if (PlayerInput.IsInteractPressed)
        {
            HandlePickup();
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

    /// <summary>
    /// Handle F-key harvest: find tile under cursor, check distance, call TryHarvest.
    /// Shows toast with result (+N itemId) or error message.
    /// </summary>
    private void HandleHarvest()
    {
        if (Tiles == null || _camera == null) return;

        // Get cursor world position.
        var mouseWorldPos = GetGlobalMousePosition();
        int targetX = (int)(mouseWorldPos.X / GameConstants.TILE_PIXELS);
        int targetY = (int)(mouseWorldPos.Y / GameConstants.TILE_PIXELS);

        // Player tile position.
        int playerX = (int)(_visualPosition.X / GameConstants.TILE_PIXELS);
        int playerY = (int)(_visualPosition.Y / GameConstants.TILE_PIXELS);

        // Chebyshev distance (max of dx, dy) — allows diagonal reach.
        int distX = System.Math.Abs(targetX - playerX);
        int distY = System.Math.Abs(targetY - playerY);
        int distance = System.Math.Max(distX, distY);

        const int MaxHarvestDistance = 3;
        if (distance > MaxHarvestDistance)
        {
            ShowToast($"Слишком далеко (дистанция {distance}, максимум {MaxHarvestDistance})");
            return;
        }

        // Check bounds.
        if (targetX < 0 || targetY < 0 || targetX >= Tiles.MapWidth || targetY >= Tiles.MapHeight)
        {
            ShowToast("За пределами карты");
            return;
        }

        var tile = Tiles.GetTile(targetX, targetY);
        if (tile.Object == ObjectType.None)
        {
            ShowToast($"Тайл ({targetX},{targetY}) — нет объекта");
            return;
        }

        if (!tile.IsHarvestable || tile.ResourceAmount <= 0f)
        {
            // Object exists but no resource (e.g., plain Bush, or depleted).
            var objName = tile.Object.ToString();
            if (tile.ResourceAmount <= 0f && tile.Object != ObjectType.None)
            {
                ShowToast($"{objName} — ресурс исчерпан");
            }
            else
            {
                ShowToast($"{objName} — нельзя собрать");
            }
            return;
        }

        // Try harvest.
        if (Tiles.TryHarvest(targetX, targetY, out var result))
        {
            // Resolve display name from ItemDatabase (fallback to itemId).
            string displayName = result.ItemId;
            if (ItemDatabase != null && ItemDatabase.TryGetItem(result.ItemId, out var harvestedItem))
            {
                displayName = harvestedItem.NameRu;
            }
            ShowToast($"+{result.Amount} {displayName} (осталось: {result.ResourceRemaining:F0})");

            // Refresh object layer (object may have been removed if depleted).
            _sceneBuilder?.RefreshObjectLayer();
            // Refresh inventory window if open (so item count updates immediately).
            _inventoryWindow?.RefreshExternally();

            if (result.Depleted)
            {
                ShowToast($"Объект исчерпан! +{result.Amount} {displayName}");
            }
            GD.Print($"[Harvest] +{result.Amount} {displayName} ({result.ItemId}) at ({targetX},{targetY}), remaining={result.ResourceRemaining}, depleted={result.Depleted}");
        }
        else
        {
            ShowToast("Не удалось добыть ресурс");
        }
    }

    /// <summary>
    /// Handle E-key pickup: find nearest ground item within pickup distance.
    /// Picks up item → adds to inventory (may overflow again → drops back).
    /// </summary>
    private void HandlePickup()
    {
        if (GroundItems == null) return;

        // Player pixel position.
        float px = _visualPosition.X;
        float py = _visualPosition.Y;

        // Pickup distance: 1.5 tiles in pixels.
        const float PickupDistance = 1.5f * 96f; // ~144 px (96 = TILE_PIXELS approx)

        bool picked = GroundItems.TryPickupNearest(px, py, PickupDistance);
        if (picked)
        {
            ShowToast("Подобран предмет");
            _inventoryWindow?.RefreshExternally();
        }
        else
        {
            // No ground item near — check if there are any at all.
            if (GroundItems.Count > 0)
            {
                ShowToast("Рядом нет предметов (подойди ближе)");
            }
            // else: silent (no items on ground at all)
        }
    }

    /// <summary>Show a toast message at top-center of screen for 2.5 seconds.</summary>
    private void ShowToast(string message)
    {
        if (_toastLabel == null) return;
        _toastLabel.Text = message;
        _toastLabel.Visible = true;
        _toastTimer = 2.5f;
        GD.Print($"[GameWorld] Toast: {message}");
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

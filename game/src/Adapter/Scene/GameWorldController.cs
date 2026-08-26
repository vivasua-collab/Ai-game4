#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
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
    [Inject] private IEquipmentService   Equipment   { get; set; } = null!;
    [Inject] private INPCService         Npcs        { get; set; } = null!;
    [Inject] private Modules.Interaction.DialogueService DialogueService { get; set; } = null!;
    [Inject] private Modules.Player.PlayerCombatAdapter CombatAdapter { get; set; } = null!;
    [Inject] private Modules.Inventory.BeltService BeltService { get; set; } = null!;
    [Inject] private IBodyService BodyService { get; set; } = null!;
    [Inject] private IQiService QiService { get; set; } = null!;
    [Inject] private Modules.Combat.TechniqueService TechniqueSvc { get; set; } = null!;
    [Inject] private Modules.Player.PlayerTechniqueCaster TechniqueCaster { get; set; } = null!;
    [Inject] private IPublisher<Core.Messaging.Contracts.MeditationToggleRequestedEvent> MeditationTogglePub { get; set; } = null!;
    [Inject] private IPublisher<Core.Messaging.Contracts.TechniqueCastRequestedEvent> TechniqueCastPub { get; set; } = null!;
    [Inject] private ISubscriber<Core.Messaging.Contracts.MeditationStateChangedEvent> MeditationStateSub { get; set; } = null!;
    [Inject] private ISubscriber<Core.Messaging.Contracts.TechniqueCastResultEvent> TechniqueCastResultSub { get; set; } = null!;
    [Inject] private ISubscriber<Core.Messaging.Contracts.FormationStageChangedEvent> FormationStageSub { get; set; } = null!;
    [Inject] private ISubscriber<Core.Messaging.Contracts.FormationActivatedEvent> FormationActivatedSub2 { get; set; } = null!;
    [Inject] private ISubscriber<Core.Messaging.Contracts.PlayerDeathEvent> PlayerDeathSub { get; set; } = null!;
    [Inject] private ISubscriber<Core.Messaging.Contracts.DamageAppliedEvent> DamageSub { get; set; } = null!;
    [Inject] private ISubscriber<Core.Messaging.Contracts.DialogueEndedEvent> DialogueEndedSub { get; set; } = null!;
    [Inject] private ISubscriber<Core.Messaging.Contracts.TradeOpenedEvent> TradeOpenedSub { get; set; } = null!;
    [Inject] private ISubscriber<Core.Messaging.Contracts.TradeClosedEvent> TradeClosedSub { get; set; } = null!;
    [Inject] private ISubscriber<Core.Messaging.Contracts.ToastShownEvent> ToastShownSub { get; set; } = null!;

    private Node2D        _worldRoot     = null!;
    private Camera2D      _camera        = null!;
    private Sprite2D      _playerSprite  = null!;
    private Sprite2D      _playerShadow  = null!;
    private InputAdapter  _inputAdapter  = null!;
    private SceneBuilder  _sceneBuilder  = null!;
    private TechniqueEffectRenderer _techniqueEffectRenderer = null!;
    private InventoryWindow _inventoryWindow = null!;
    private CharacterSheetWindow _characterSheetWindow = null!;
    private UI.DialogueWindow _dialogueWindow = null!;
    private UI.TradeWindow _tradeWindow = null!;
    private UI.HotbarPanel _hotbarPanel = null!;
    private UI.TechniquesPanel _techniquesPanel = null!;
#if DEBUG
    private UI.CheatPanel? _cheatPanel; // Этап 7: чит-меню (F1).
#endif
    private Godot.ProgressBar _hpBar = null!;
    private Godot.ProgressBar _qiBar = null!;
    private Label _qiLabel = null!;
    private Label _meditationLabel = null!;
    private bool _meditationActive;           // кэш из MeditationStateChangedEvent
    private System.IDisposable? _meditationStateToken;
    private System.IDisposable? _techniqueCastResultToken;
    private System.IDisposable? _formationStageToken;
    private System.IDisposable? _formationActivatedToken2;
    private System.IDisposable? _playerDeathToken;
    private System.IDisposable? _playerDamageToken;
    private System.IDisposable? _toastShownToken;
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

    /// <summary>Max Chebyshev distance (tiles) for E-key NPC talk. Phase 2.</summary>
    private const float TalkRangeTiles = 2.5f;
    private bool _positionInitialized;
    private bool _wasPausedBeforeInventory; // track if game was paused before opening inventory
    private System.IDisposable? _dialogueEndedToken;
    private System.IDisposable? _tradeOpenedToken;
    private System.IDisposable? _tradeClosedToken;
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
        // Phase 6: subscribe the combat bridge (attack intent → combat module).
        CombatAdapter?.Start();
        // Этап 2 внедрения ЦИ: кастер техник (TechniqueCastRequestedEvent → эффекты).
        TechniqueCaster?.Start();
        // Этап 4 (2026-08-22): смерть игрока → респавн; урон игроку → тост.
        _playerDeathToken = PlayerDeathSub?.Subscribe(OnPlayerDeath);
        _playerDamageToken = DamageSub?.Subscribe(OnPlayerDamaged);
        // Этап 7: тосты от модулей (InventoryWindow.TryUseQiStone и др.)
        _toastShownToken = ToastShownSub?.Subscribe(OnToastShown);
        // Phase 2 fix: dialogue can end from MANY paths (E advance, Esc, choice
        // button click, digit key 1-4) — each closed the window its own way and
        // only E/Esc resumed time, leaving the game silently paused after a
        // choice-click finish. Single authoritative resume point: the bus event.
        _dialogueEndedToken = DialogueEndedSub?.Subscribe(OnDialogueEnded);
        // NPC_COMBAT_PREP Phase 4-5: торговля — пауза/резюм тиков по шине
        // (TradeOpened/TradeClosedEvent; единая точка резюма, как у диалогов).
        _tradeOpenedToken = TradeOpenedSub?.Subscribe(OnTradeOpened);
        _tradeClosedToken = TradeClosedSub?.Subscribe(OnTradeClosed);
        // Этап 1 внедрения ЦИ: индикация медитации (V).
        _meditationStateToken = MeditationStateSub?.Subscribe(OnMeditationStateChanged);
        // Этап 2 внедрения ЦИ: результат каста техники (тосты).
        _techniqueCastResultToken = TechniqueCastResultSub?.Subscribe(OnTechniqueCastResult);
        // Этап 5 внедрения ЦИ: стадии формации (тосты).
        _formationStageToken = FormationStageSub?.Subscribe(OnFormationStageChanged);
        _formationActivatedToken2 = FormationActivatedSub2?.Subscribe(OnFormationActivated);
        // NPC_COMBAT_PREP P0/Phase 8: headless-верификация боевого пайплайна
        // (GODOT_COMBAT_SIM=1) — урон NPC→игрок и игрок→NPC + wiring статов.
        if (System.Environment.GetEnvironmentVariable("GODOT_COMBAT_SIM") == "1")
        {
            var combatSim = new CombatSimDebug { Name = "CombatSimDebug" };
            AddChild(combatSim);
        }
        // Stage 0+1 (2026-08-25, GLM-5.3): верификация модели заполнения +
        // ауры-задержки (вариант В): зарядка → hold → release → урон.
        if (System.Environment.GetEnvironmentVariable("GODOT_CHARGE_SIM") == "1")
        {
            var chargeSim = new ChargeSimDebug { Name = "ChargeSimDebug" };
            AddChild(chargeSim);
        }
        GD.Print("[GameWorldController] Ready");
    }

    /// <summary>Этап 1 внедрения ЦИ: обновление индикатора медитации.</summary>
    private void OnMeditationStateChanged(in Core.Messaging.Contracts.MeditationStateChangedEvent e)
    {
        _meditationActive = e.IsActive;
        if (_meditationLabel != null) _meditationLabel.Visible = e.IsActive;
        ShowToast(e.IsActive
            ? $"☯ Медитация начата (+{e.RatePerSecond:F1} Ци/с)"
            : "☯ Медитация завершена");
    }

    /// <summary>Этап 2 внедрения ЦИ: тост результата каста техники.</summary>
    private void OnTechniqueCastResult(in Core.Messaging.Contracts.TechniqueCastResultEvent e)
    {
        if (!e.Success)
        {
            ShowToast($"✖ {e.Reason}");
            return;
        }
        string label = e.Type switch
        {
            Core.Data.TechniqueType.Healing => "Лечение",
            Core.Data.TechniqueType.Defense => "Щит",
            Core.Data.TechniqueType.Movement => "Рывок",
            Core.Data.TechniqueType.Sensory => "Восприятие",
            Core.Data.TechniqueType.Support => "Поддержка",
            Core.Data.TechniqueType.Curse => "Проклятие",
            Core.Data.TechniqueType.Formation => "Формация",
            _ => "Техника"
        };
        ShowToast($"✴ {label} применено");
    }

    /// <summary>Этап 5: тосты стадий формации.</summary>
    private void OnFormationStageChanged(in Core.Messaging.Contracts.FormationStageChangedEvent e)
    {
        string msg = e.NewStage switch
        {
            Core.Data.FormationStage.Drawing => "◈ Контур формации рисуется…",
            Core.Data.FormationStage.Filling => "◈ Формация наполняется Ци…",
            Core.Data.FormationStage.Depleted => "◈ Формация истощена",
            _ => null
        };
        if (msg != null) ShowToast(msg);
    }

    /// <summary>Этап 5: тост активации формации.</summary>
    private void OnFormationActivated(in Core.Messaging.Contracts.FormationActivatedEvent e)
    {
        string type = e.Type switch
        {
            Core.Data.FormationType.Barrier => "Барьер",
            Core.Data.FormationType.Amplification => "Усиление",
            Core.Data.FormationType.Suppression => "Подавление",
            Core.Data.FormationType.Gathering => "Сбор Ци",
            Core.Data.FormationType.Trap => "Ловушка",
            Core.Data.FormationType.Detection => "Обнаружение",
            _ => e.Type.ToString()
        };
        ShowToast($"✦ Формация активна: {type}");
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

        // Этап 3 внедрения ЦИ: схематические эффекты техник (_Draw, без PNG).
        _techniqueEffectRenderer = new TechniqueEffectRenderer { Name = "TechniqueEffectRenderer" };
        _worldRoot.AddChild(_techniqueEffectRenderer);
    }

    private static Texture2D CreatePlayerTexture()
    {
        return ProceduralSpriteGenerator.CreatePlayerSprite();
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

        // Player HP bar (этап 4, 2026-08-22): под временем. HP = Σ RedHP по
        // частям тела (Q4). Цвет от зелёного к красному по мере потерь.
        _hpBar = new ProgressBar
        {
            Name = "HpBar",
            MinValue = 0,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(260, 18),
            Position = new Vector2(20, 38),
        };
        _hpBar.AddThemeFontSizeOverride("font_size", 12);
        _hudCanvas.AddChild(_hpBar);

        // Qi bar (этап 1 внедрения ЦИ, 2026-08-23): под HP-баром, золотой цвет.
        // Ци игрока из QiService (long — отображаем как double в ProgressBar).
        _qiBar = new ProgressBar
        {
            Name = "QiBar",
            MinValue = 0,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(260, 14),
            Position = new Vector2(20, 60),
        };
        _qiBar.AddThemeFontSizeOverride("font_size", 11);
        _qiBar.AddThemeColorOverride("font_color", new Color(0.98f, 0.85f, 0.3f));
        _hudCanvas.AddChild(_qiBar);

        // Подпись Ци (значения + уровень культивации).
        _qiLabel = new Label { Name = "QiLabel", Position = new Vector2(288, 60) };
        _qiLabel.AddThemeFontSizeOverride("font_size", 12);
        _qiLabel.AddThemeColorOverride("font_color", new Color(0.94f, 0.83f, 0.66f));
        _hudCanvas.AddChild(_qiLabel);

        // Индикатор медитации (V): статус под Ци-баром.
        _meditationLabel = new Label { Name = "MeditationLabel", Position = new Vector2(20, 78), Visible = false };
        _meditationLabel.AddThemeFontSizeOverride("font_size", 13);
        _meditationLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.75f, 0.95f));
        _meditationLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
        _meditationLabel.Text = "☯ Медитация — поглощение Ци (движение прерывает)";
        _hudCanvas.AddChild(_meditationLabel);

        // Hotkey legend — BOTTOM of screen, black color.
        _hudLabel = new Label
        {
            Name = "HudHint",
            Text = "WASD — движение | Shift — бег | ЛКМ — идти к точке | Колесо — зум\n" +
                   "Esc — пауза | PageUp/PageDown — скорость\n" +
                   "E — подобрать | B — инвентарь | F — добыча | V — медитация | Z — каст | X — выбор техники\n" +
                   "C — персонаж | J — журнал | T — техники | Q — квесты | M — карта | N — миникарта"
#if DEBUG
                   + "\nF1 — чит-меню (dev)"
#endif
            ,
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

        // Character sheet window (opens with C key).
        _characterSheetWindow = new CharacterSheetWindow { Name = "CharacterSheetWindow" };
        _hudCanvas.AddChild(_characterSheetWindow);

        // Dialogue window (opens with E key near an NPC) — NPC_COMBAT_PREP Phase 2.
        _dialogueWindow = new UI.DialogueWindow { Name = "DialogueWindow" };
        _hudCanvas.AddChild(_dialogueWindow);

        // Trade window (merchant shop) — NPC_COMBAT_PREP Phase 5. Открывается
        // по TradeOpenedEvent (выбор «Покажи товары» в диалоге торговца),
        // закрывается по TradeClosedEvent / Esc. Пауза — OnTradeOpened ниже.
        _tradeWindow = new UI.TradeWindow { Name = "TradeWindow" };
        _hudCanvas.AddChild(_tradeWindow);

        // Hotbar (2026-08-22): 9 quick slots bottom-center; belt slots 3-9
        // appear when a belt is equipped.
        _hotbarPanel = new UI.HotbarPanel { Name = "HotbarPanel" };
        _hudCanvas.AddChild(_hotbarPanel);

        // Этап 2 внедрения ЦИ: панель техник (T).
        _techniquesPanel = new UI.TechniquesPanel { Name = "TechniquesPanel" };
        _hudCanvas.AddChild(_techniquesPanel);

#if DEBUG
        // Этап 7 внедрения ЦИ: чит-меню разработки (F1).
        // Видимость переключается в HandleStickyInput по PlayerInput.IsCheatMenuPressed.
        _cheatPanel = new UI.CheatPanel { Name = "CheatPanel" };
        _hudCanvas.AddChild(_cheatPanel);
#endif
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

        // Этап 4: HP bar — сумма RedHP по частям тела (Q4).
        if (_hpBar != null && BodyService != null)
        {
            int cur = 0, max = 0;
            var parts = BodyService.GetAllParts();
            if (parts != null)
            {
                foreach (var p in parts) { cur += p.CurrentRedHP; max += p.MaxRedHP; }
            }
            if (max > 0)
            {
                _hpBar.MaxValue = max;
                _hpBar.Value = cur;
                float ratio = (float)cur / max;
                _hpBar.Modulate = new Color(
                    0.35f + 0.65f * (1f - ratio), 0.3f + 0.6f * ratio, 0.25f);
            }
        }

        // Этап 1 внедрения ЦИ: Qi bar — текущее Ци / MaxQi (long → double).
        if (_qiBar != null && QiService != null)
        {
            double qiMax = QiService.MaxQi;
            if (qiMax > 0)
            {
                _qiBar.MaxValue = qiMax;
                _qiBar.Value = QiService.CurrentQi;
                float qiRatio = (float)(QiService.CurrentQi / qiMax);
                // Золотой → тускло-серый при истощении.
                _qiBar.Modulate = new Color(
                    0.55f + 0.45f * qiRatio, 0.45f + 0.4f * qiRatio, 0.15f + 0.15f * qiRatio);
                _qiLabel.Text = $"Ци {QiService.CurrentQi}/{qiMax} | L{(int)QiService.CultivationLevel}.{QiService.SubLevel}" +
                                $" | пров. {QiService.Conductivity:F1}/с";
            }
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

        // Этап 1 внедрения ЦИ: V — переключить медитацию (поглощение Ци из среды).
        if (PlayerInput is { IsMeditatePressed: true })
        {
            MeditationTogglePub.Publish(
                new Core.Messaging.Contracts.MeditationToggleRequestedEvent(!_meditationActive));
        }

        // Этап 2 внедрения ЦИ: T — панель техник, X — следующая техника, Z — каст.
        if (PlayerInput is { IsTechniquesPressed: true } && _techniquesPanel != null)
        {
            _techniquesPanel.Visible = !_techniquesPanel.Visible;
            if (_techniquesPanel.Visible) _techniquesPanel.QueueRedraw();
        }
        if (PlayerInput is { IsCycleTechniquePressed: true })
        {
            TechniqueSvc?.CycleSelection();
            var sel = TechniqueSvc?.SelectedTechnique;
            if (sel != null) ShowToast($"▣ Выбрано: {sel.Name} L{sel.Level}");
        }
        if (PlayerInput is { IsCastTechniquePressed: true } && TechniqueSvc != null)
        {
            var sel = TechniqueSvc.SelectedTechnique;
            if (sel != null)
            {
                var mouse = GetGlobalMousePosition();
                TechniqueCastPub.Publish(new Core.Messaging.Contracts.TechniqueCastRequestedEvent(
                    sel.TechniqueId, (int)(mouse.X * 1000), (int)(mouse.Y * 1000)));
            }
            else
            {
                ShowToast("✖ Нет выбранной техники (T — панель, X — выбор)");
            }
        }

        // Phase 6: player combat bridge — Space publishes AttackIntentEvent
        // with the nearest NPC target. Must run BEFORE ResetFrameFlags below.
        CombatAdapter?.Tick((float)delta);
        if (PlayerInput is { IsAttackPressed: true })
        {
            // Feedback for the attack press (target resolution logs in combat module).
            ShowToast("⚔ Атака!");
        }

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

        // Any modal window open (inventory / character sheet / dialogue) —
        // world mouse handling is off. The wheel case matters most: a
        // ScrollContainer consumes wheel events only while it CAN scroll;
        // at the list end the event leaks to _UnhandledInput and used to
        // change the camera zoom (user report 2026-08-22).
        bool modalOpen = (_inventoryWindow is { Visible: true })
                      || (_characterSheetWindow is { Visible: true })
                      || (_dialogueWindow is { IsOpen: true })
                      || (_tradeWindow is { IsOpen: true });

        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            switch (mb.ButtonIndex)
            {
                case MouseButton.Left:
                {
                    if (modalOpen) break; // clicks belong to the window, not the world
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
                    if (modalOpen) break; // no zoom while inventory is scrolled to the end
                    var zoomIn = _camera.Zoom with { X = _camera.Zoom.X + 0.5f, Y = _camera.Zoom.Y + 0.5f };
                    if (zoomIn.X <= 8.0f)
                        _camera.Zoom = zoomIn;
                    break;
                }
                case MouseButton.WheelDown:
                {
                    if (modalOpen) break;
                    var zoomOut = _camera.Zoom with { X = _camera.Zoom.X - 0.5f, Y = _camera.Zoom.Y - 0.5f };
                    if (zoomOut.X >= 1.0f)
                        _camera.Zoom = zoomOut;
                    break;
                }
                case MouseButton.Middle:
                    if (modalOpen) break;
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

        // Этап 1 внедрения ЦИ: движение прерывает медитацию (концентрация).
        if (_meditationActive && moveVec != Vector2.Zero)
        {
            MeditationTogglePub.Publish(
                new Core.Messaging.Contracts.MeditationToggleRequestedEvent(false));
        }

        // Speed: base pixels/sec × delta × run multiplier.
        // Q7 (refined 2026-08-22 per user report): movement must feel faster
        // at higher game speeds, but the OLD linear multiplier (×5/×15) caused
        // extreme speeds and camera lag. Moderated curve instead:
        // Normal ×1.0, Fast ×2.0, Quick ×3.5 — perceptible, stays controllable.
        float gameSpeedMult = Time?.Speed switch
        {
            TimeSpeed.Fast   => 2.0f,
            TimeSpeed.Quick  => 3.5f,
            _                => 1.0f,
        };
        float speedMult = gameSpeedMult;
        if (Godot.Input.IsActionPressed("run")) speedMult *= RunSpeedMultiplier;

        // Camera must keep up with the faster player — scale smoothing too.
        if (_camera != null)
            _camera.PositionSmoothingSpeed = 8.0f * gameSpeedMult;

        // === Global weight penalty (inventory + equipment) ===
        // Weight = inventory items + equipped items. Both contribute to overweight.
        // Also applies equipment MoveSpeedPenalty (e.g. heavy armor = -15% speed).
        float invWeight = Inventory?.GetCurrentWeight() ?? 0f;
        float equipWeight = Equipment?.GetTotalWeight() ?? 0f;
        float totalWeight = invWeight + equipWeight;
        float maxWeight = Inventory?.GetEffectiveMaxWeight() ?? 50f;

        // Equipment move speed penalty (negative %, e.g. -15 → multiply by 0.85).
        float equipPenaltyPercent = Equipment?.GetTotalMoveSpeedPenalty() ?? 0f;
        if (equipPenaltyPercent < 0f)
        {
            speedMult *= 1.0f + (equipPenaltyPercent / 100f); // pen=-15 → ×0.85
        }

        // Overweight penalty: total weight > max → speed drops.
        if (totalWeight > maxWeight)
        {
            float ratio = maxWeight > 0f
                ? System.Math.Min(3.0f, (totalWeight - maxWeight) / maxWeight)
                : 3.0f;
            float overweightPenalty = 1.0f / (1.0f + ratio);
            speedMult *= overweightPenalty;

            if (!_overweightNotified)
            {
                _overweightNotified = true;
                ShowToast($"⚠ Перевес! {totalWeight:F1}/{maxWeight:F1} кг — скорость снижена");
            }
        }
        else if (_overweightNotified)
        {
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

#if DEBUG
        // Этап 7: F1 — чит-меню (только в DEBUG-сборке).
        // Работает независимо от состояния UI (modalOpen, пауза).
        if (PlayerInput.IsCheatMenuPressed && _cheatPanel != null)
        {
            _cheatPanel.Visible = !_cheatPanel.Visible;
            GD.Print($"[GameWorld] CheatPanel: {(_cheatPanel.Visible ? "open" : "closed")}");
        }
#endif

        // Esc while trading → close the shop (Phase 5). Authoritative resume
        // happens in OnTradeClosed (bus event), like dialogues.
        if (PlayerInput.IsPausePressed && _tradeWindow is { IsOpen: true })
        {
            _tradeWindow.Close();
        }
        // Esc while a dialogue is open → close it and resume ticks (Phase 2).
        else if (PlayerInput.IsPausePressed && _dialogueWindow is { IsOpen: true })
        {
            _dialogueWindow.Close();
            if (!_wasPausedBeforeInventory && Time is { IsPaused: true })
                Time.Resume();
        }
        // Esc (sticky "escape") → toggle pause (but not when inventory is open).
        else if (PlayerInput.IsPausePressed && (_inventoryWindow == null || !_inventoryWindow.Visible))
        {
            if (Time.IsPaused) Time.Resume();
            else               Time.Pause();
            GD.Print($"[GameWorld] Pause toggled: {Time.IsPaused}");
        }
        // If inventory is open and Esc pressed, close it instead of pausing.
        else if (PlayerInput.IsPausePressed && _inventoryWindow != null && _inventoryWindow.Visible)
        {
            _inventoryWindow.Toggle();
            // Resume game time when closing inventory via Esc (same as B-close path).
            if (!_wasPausedBeforeInventory && Time != null && Time.IsPaused)
                Time.Resume();
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

        // C key: toggle Character Sheet (body status + stats).
        if (PlayerInput.IsCharacterSheetPressed)
        {
            _characterSheetWindow?.Toggle();
            // Pause game when character sheet opens (same as inventory).
            if (_characterSheetWindow != null && Time != null)
            {
                if (_characterSheetWindow.Visible)
                {
                    _wasPausedBeforeInventory = Time.IsPaused;
                    if (!Time.IsPaused) Time.Pause();
                }
                else
                {
                    if (!_wasPausedBeforeInventory && Time.IsPaused)
                        Time.Resume();
                }
            }
        }

        // E key: dialogue takes priority when open or an NPC is in range;
        // otherwise pick up the nearest ground item.
        // NPC_COMBAT_PREP Phase 2: E near NPC → open role dialogue (pause ticks).
        // Phase 5: при открытой лавке E не действует (торговля — модальность).
        if (PlayerInput.IsInteractPressed)
        {
            if (_tradeWindow is { IsOpen: true })
            {
                // Лавка открыта — E съедается модальностью торговли.
            }
            else if (_dialogueWindow != null && _dialogueWindow.IsOpen)
            {
                _dialogueWindow.Advance();
                if (_dialogueWindow is { IsOpen: false } && !_wasPausedBeforeInventory && Time is { IsPaused: true })
                    Time.Resume();
            }
            else if (!HandleNpcTalk())
            {
                HandlePickup();
            }
        }

        // Suppress game input when inventory OR trade window is open (Phase 5).
        if (_inputAdapter != null && _inventoryWindow != null)
        {
            _inputAdapter.SetOverUI(_inventoryWindow.Visible || _tradeWindow is { IsOpen: true });
        }

        // Save/load DISABLED (Q8: user decision — saves invalid after each fix).
        // Will be re-enabled when save system is stable.
        // F5/F9 do nothing — no toast, no log, no action.
        // if (PlayerInput.IsQuickSavePressed) { ... }
        // if (PlayerInput.IsQuickLoadPressed) { ... }

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
                ShowToast($"⏩ Скорость: {SpeedLabel(Time.Speed)} ({(int)Time.Speed} тик/сек)");
                _speedChangeCooldown = SpeedChangeCooldownSec;
            }
            else if (PlayerInput.IsTimeSpeedDownPressed)
            {
                if (Time.IsPaused) { Time.Resume(); Time.Speed = TimeSpeed.Normal; }
                else Time.Speed = CycleSpeedDown(Time.Speed);
                ShowToast($"⏪ Скорость: {SpeedLabel(Time.Speed)} ({(int)Time.Speed} тик/сек)");
                _speedChangeCooldown = SpeedChangeCooldownSec;
            }
        }

        // Hotbar keys 1-9 (HOTKEYS §8): 1-2 select weapons (info), 3-9 use
        // the belt consumable (gated by equipped belt inside BeltService).
        int hotbarSlot = PlayerInput.SelectedTechniqueSlot;
        if (hotbarSlot >= Modules.Inventory.BeltService.HotbarFirstIndex && BeltService != null)
        {
            int beltIndex = hotbarSlot - Modules.Inventory.BeltService.HotbarFirstIndex;
            if (BeltService.Use(beltIndex))
            {
                var slots = BeltService.GetSlots();
                var used = slots[beltIndex];
                ShowToast(used.Count > 0
                    ? $"Использовано ({hotbarSlot}): осталось {used.Count}"
                    : $"Использовано ({hotbarSlot}) — слот пуст");
            }
            else if (BeltService.IsBeltEquipped)
            {
                ShowToast($"Слот {hotbarSlot} пуст");
            }
        }
    }

    /// <summary>
    /// Этап 4 (2026-08-22): урон игроку → тост-фидбек.
    /// </summary>
    private void OnPlayerDamaged(in Core.Messaging.Contracts.DamageAppliedEvent e)
    {
        if (e.TargetId is not ("player_0" or "player")) return;
        ShowToast($"💥 −{e.Damage} HP");
    }

    /// <summary>Этап 7: тост от модуля (QiStone use, чит-меню и т.д.).</summary>
    private void OnToastShown(in Core.Messaging.Contracts.ToastShownEvent e)
    {
        if (!string.IsNullOrEmpty(e.Message)) ShowToast(e.Message);
    }

    /// <summary>
    /// Этап 4: смерть игрока → респавн через 3 секунды: полное лечение
    /// частей тела, Revive, телепорт в центр карты.
    /// </summary>
    private void OnPlayerDeath(in Core.Messaging.Contracts.PlayerDeathEvent e)
    {
        ShowToast($"☠ Вы погибли ({e.Cause}) — возрождение...");
        GD.Print($"[GameWorld] Player death: {e.Cause} — respawn in 3s");
        CallDeferred(nameof(RespawnAfterDeath));
    }

    private async void RespawnAfterDeath()
    {
        await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);

        // Полное лечение всех частей (Q4: HP = Σ RedHP).
        if (BodyService != null)
        {
            var parts = BodyService.GetAllParts();
            if (parts != null)
            {
                foreach (var p in parts)
                {
                    int missing = p.MaxRedHP - p.CurrentRedHP;
                    if (missing > 0) BodyService.HealPart(p.Type, missing);
                }
            }
        }
        (Player as Modules.Player.PlayerService)?.Revive();

        // Телепорт в центр карты.
        int cx = (Tiles is { MapWidth: > 0 } ? Tiles.MapWidth : 50) / 2;
        int cy = (Tiles is { MapHeight: > 0 } ? Tiles.MapHeight : 50) / 2;
        _visualPosition = new Vector2(
            cx * GameConstants.TILE_PIXELS + GameConstants.TILE_PIXELS / 2f,
            cy * GameConstants.TILE_PIXELS + GameConstants.TILE_PIXELS / 2f);
        Player?.SetPosition(new Position2D(cx, cy));
        _mouseTarget = null;
        ShowToast("✦ Вы возродились");
    }

    /// <summary>
    /// Phase 2 fix: authoritative resume point for the dialogue pause.
    /// DialogueEndedEvent fires from EVERY end path (E advance, Esc, choice
    /// button click, digit-key selection) — resume ticks here regardless of
    /// which UI path closed the window.
    /// </summary>
    private void OnDialogueEnded(in Core.Messaging.Contracts.DialogueEndedEvent e)
    {
        if (_dialogueWindow is { IsOpen: true })
            _dialogueWindow.Close();
        if (!_wasPausedBeforeInventory && Time is { IsPaused: true })
            Time.Resume();
    }

    /// <summary>
    /// NPC_COMBAT_PREP Phase 4-5: лавка открыта — пауза тиков (торговля —
    /// планирующая активность, как инвентарь/диалог). Окно показывает себя
    /// по тому же событию (TradeWindow.OnTradeOpened).
    /// </summary>
    private void OnTradeOpened(in Core.Messaging.Contracts.TradeOpenedEvent e)
    {
        _wasPausedBeforeInventory = Time is { IsPaused: true };
        if (Time is { IsPaused: false })
            Time.Pause();
        GD.Print($"[GameWorld] Trade opened: {e.NpcId} — ticks paused");
    }

    /// <summary>
    /// Лавка закрыта — авторитетная точка резюма (как OnDialogueEnded):
    /// TradeClosedEvent стреляет из любого пути закрытия (Esc, конец
    /// отладочного сценария и т.д.).
    /// </summary>
    private void OnTradeClosed(in Core.Messaging.Contracts.TradeClosedEvent e)
    {
        if (!_wasPausedBeforeInventory && Time is { IsPaused: true })
            Time.Resume();
        GD.Print("[GameWorld] Trade closed — ticks resumed");
    }

    /// <summary>
    /// NPC_COMBAT_PREP Phase 2 — E near an NPC opens the role dialogue.
    /// Uses the nearest NPC within TalkRange tiles of the player.
    /// Returns true when a dialogue started (E should not fall through to pickup).
    /// Pauses the tick simulation while the dialogue is open (planning activity).
    /// </summary>
    private bool HandleNpcTalk()
    {
        if (Npcs == null || Player == null || _dialogueWindow == null) return false;

        var playerPos = Player.Position;
        var nearby = Npcs.GetNearbyNPCIds(playerPos, TalkRangeTiles);
        if (nearby is not { Count: > 0 }) return false;

        // Nearest first (GetNearbyNPCIds order is not guaranteed).
        string? best = null;
        int bestDist = int.MaxValue;
        foreach (var id in nearby)
        {
            var npc = Npcs.GetNPC(id);
            if (npc == null || !Npcs.IsAlive(id)) continue;
            int dx = System.Math.Abs(npc.Position.X - playerPos.X);
            int dy = System.Math.Abs(npc.Position.Y - playerPos.Y);
            int dist = System.Math.Max(dx, dy);
            if (dist < bestDist) { bestDist = dist; best = id; }
        }
        if (best == null) return false;

        var dialogueSvc = DialogueService;
        if (dialogueSvc == null || !dialogueSvc.TryStartNpcDialogue(best))
        {
            ShowToast("Нечего сказать друг другу");
            return true; // NPC was in range — don't fall through to item pickup.
        }

        _wasPausedBeforeInventory = Time is { IsPaused: true };
        if (Time is { IsPaused: false }) Time.Pause();
        _dialogueWindow.Open(best);
        return true;
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

        // Pickup distance: 1.5 tiles in pixels (use canonical TILE_PIXELS, not hard-coded 96).
        const float PickupDistance = 1.5f * GameConstants.TILE_PIXELS;

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

    /// <summary>Human-readable speed name for HUD/toast (matches GLOSSARY TimeSpeed).</summary>
    private static string SpeedLabel(TimeSpeed speed) => speed switch
    {
        TimeSpeed.Paused => "Пауза",
        TimeSpeed.Normal => "Обычно",
        TimeSpeed.Fast   => "Быстро",
        TimeSpeed.Quick  => "Очень быстро",
        _ => speed.ToString(),
    };

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

    /// <summary>
    /// NPC_COMBAT_PREP Phase 5: dispose trade-подписок при уходе сцены
    /// (правило «токены подписок диспозятся» — BeltSlotRow._ExitTree паттерн).
    /// </summary>
    public override void _ExitTree()
    {
        _tradeOpenedToken?.Dispose();
        _tradeClosedToken?.Dispose();
        _tradeOpenedToken = null;
        _tradeClosedToken = null;
    }

    public Node2D WorldRoot => _worldRoot;
    public Camera2D Camera => _camera;
    public Sprite2D PlayerSprite => _playerSprite;
    public IReadOnlyList<Node> HudChildren => _hudCanvas?.GetChildren() as IReadOnlyList<Node> ?? Array.Empty<Node>();
}

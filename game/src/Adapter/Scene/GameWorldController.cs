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
using CultivationGame.Adapter.Persistence;
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
    // R13 FULL-LOOT (2026-09-10): трупы NPC (контейнеры лута).
    [Inject] private ICorpseService      Corpses     { get; set; } = null!;
    [Inject] private Modules.Interaction.DialogueService DialogueService { get; set; } = null!;
    [Inject] private Modules.Player.PlayerCombatAdapter CombatAdapter { get; set; } = null!;
    [Inject] private Modules.Inventory.BeltService BeltService { get; set; } = null!;
    [Inject] private IBodyService BodyService { get; set; } = null!;
    [Inject] private IQiService QiService { get; set; } = null!;
    [Inject] private Modules.Combat.TechniqueService TechniqueSvc { get; set; } = null!;
    // D (2026-08-26): слоты техник 3-9 (для каста по клавише N) + тосты.
    [Inject] private Modules.Player.TechniqueSlotService TechniqueSlots { get; set; } = null!;
    // П3 (репорт 21.09): полное описание формации в тостах активации/наполнения
    [Inject] private Modules.Formation.FormationService? FormationSvc { get; set; }
    [Inject] private IPublisher<Core.Messaging.Contracts.ToastShownEvent> ToastPub { get; set; } = null!;
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
    // M2 (2026-09-03): отклонение атаки игрока (C-5 аудита-3) → тост причины
    // («Каст уже идёт: …») вместо удалённого polling-тоста «⚔ Атака!».
    [Inject] private ISubscriber<Core.Messaging.Contracts.AttackRejectedEvent> AttackRejectedSub { get; set; } = null!;
    // 2026-09-04 S3: kill-feed — смерть NPC в физическом бою (NPCCombatAdapter
    // публикует NPCDeathEvent; ранее UI не был подписан — убийства невидимы:
    // игроку не было никакого отклика, только лут падал).
    [Inject] private ISubscriber<Core.Messaging.Contracts.NPCDeathEvent> NpcDeathSub { get; set; } = null!;
    // R13 FULL-LOOT (2026-09-10): труп удалён (обыскан/TTL) → закрыть LootWindow,
    // если он открыт для этого трупа (авторитетная точка закрытия, как у диалогов).
    [Inject] private ISubscriber<Core.Messaging.Contracts.CorpseRemovedEvent> CorpseRemovedSub { get; set; } = null!;
    // R15 (2026-09-10): экипировка оружия → обновить спрайт в руке игрока.
    [Inject] private ISubscriber<Core.Messaging.Contracts.EquipmentChangedEvent> EquipmentChangedSub { get; set; } = null!;
    // R16 (2026-09-10): замах оружия игрока (AttackIntentEvent, melee) +
    // тост стойки защиты (DefenseIntentEvent, клавиша G).
    [Inject] private ISubscriber<Core.Messaging.Contracts.AttackIntentEvent> AttackIntentSub { get; set; } = null!;
    [Inject] private ISubscriber<Core.Messaging.Contracts.DefenseIntentEvent> DefenseIntentSub { get; set; } = null!;

    private Node2D        _worldRoot     = null!;
    private Camera2D      _camera        = null!;
    private Sprite2D      _playerSprite  = null!;
    private Sprite2D      _playerShadow  = null!;
    // R15: композит игрока — overlay оружия в основной руке поверх тела
    // (RimWorld-слои; тело остаётся _playerSprite, MainHand — отдельный Sprite2D
    // с offset из WeaponVisualCatalog и зеркалированием по facing).
    private Sprite2D      _playerMainHand = null!;
    private bool          _facingLeft;              // R15: направление взгляда
    private string?       _mainHandCacheItemId;     // R15: кэш синхронизации
    private float         _mainHandSyncCooldown;    // R15: страховка 0.5с (событие до _Ready)

    // === R16 (2026-09-10): замах оружия игрока (анимация удара) ===
    // AttackIntentEvent (melee, attacker=player) → таймер 0.42с → выпад
    // PlayerMainHand к цели (sin-кривая: разгон-удар-возврат). Статичные
    // спрайты: «замах» = смещение позиции + Scale-пульс, без поворота
    // (Rotation конфликтует с FlipH-зеркалированием R15).
    private Vector2       _mainHandSwingDir = Vector2.Zero;
    private float         _mainHandSwingAge = -1f;   // <0 = нет анимации
    private const float   MainHandSwingSec = 0.42f;  // ≈ каст базовой атаки
    private const float   MainHandSwingLungePx = 12f;

    // === G0 (2026-09-25): подготовка спрайтов — аниматор игрока (I-1) ====
    // PNG-листы отсутствуют → процедурная статика КАК СЕЙЧАС (fallback по
    // контракту PROCEDURAL_SPRITES §5); при доставке PNG в
    // resources/sprites/characters/player/ анимация включается без правок.
    private PlayerAnimator? _playerAnimator;
    private bool          _lastMoving;              // снимок движения для аниматора
    private bool          _lastRunning;             // Shift-бег
    private float         _lastSpeedMult = 1f;      // gameSpeed×бег×штрафы (walk fps)
    private InputAdapter  _inputAdapter  = null!;
    private SceneBuilder  _sceneBuilder  = null!;
    private TechniqueEffectRenderer _techniqueEffectRenderer = null!;
    private InventoryWindow _inventoryWindow = null!;
    private CharacterSheetWindow _characterSheetWindow = null!;
    private UI.DialogueWindow _dialogueWindow = null!;
    private UI.TradeWindow _tradeWindow = null!;
    // R13 FULL-LOOT: окно обыска трупа (E рядом с трупом → пауза + список лута).
    private UI.LootWindow _lootWindow = null!;
    private UI.HotbarPanel _hotbarPanel = null!;
    // 2026-08-28: Книга Техник (T) — матричный браузер библиотеки
    // (вкладки-уровни / блоки-типы / строки-стихии) вместо HUD-панели.
    private UI.TechniqueBookWindow _techniqueBook = null!;
    // 2026-08-28: окно-справка горячих клавиш (F1).
    private UI.HotkeysWindow _hotkeysWindow = null!;
    // 2026-09-04 S1: журнал событий (J).
    private UI.EventLogWindow _eventLogWindow = null!;
    // 2026-09-04 S1: окно квестов (Q).
    private UI.QuestWindow _questWindow = null!;
    // C3 (2026-08-26): окно Культивации Ци (K) — 3 вкладки + панель слотов техник 3-9.
    private UI.CultivationWindow _cultivationWindow = null!;
    // R37-c (баг-репорт 23.09): окно зарядника Ци (H) — слоты камней/буфер/
    // тепло/режим; гейт — надетый зарядник (слот Belt, ItemType="Charger").
    private UI.ChargerWindow _chargerWindow = null!;
#if DEBUG
    private UI.CheatPanel? _cheatPanel; // Этап 7: чит-меню (F2, с 2026-08-28).
#endif
    private Godot.ProgressBar _hpBar = null!;
    private Godot.ProgressBar _qiBar = null!;
    // 2026-09-04 S1: числовые подписи на барах + индикатор боевой готовности.
    private Label _hpBarText = null!;
    private Label _qiBarText = null!;
    private Label _attackStateLabel = null!;
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
    private System.IDisposable? _npcDeathToken; // 2026-09-04 S3: kill-feed
    private System.IDisposable? _corpseRemovedToken; // R13: закрытие LootWindow по CorpseRemovedEvent
    private System.IDisposable? _attackRejectedToken; // M2: тост причины отклонения атаки
    private System.IDisposable? _equipmentChangedToken; // R15: оружие в руке
    // R16: замах оружия + тост стойки защиты.
    private System.IDisposable? _attackIntentToken;
    private System.IDisposable? _defenseIntentToken;
    private CanvasLayer   _hudCanvas     = null!;
    private Label         _timeLabel     = null!;

    // 2026-09-04 S2: тост-стек — несколько сообщений одновременно (было: один
    // Label, новое сообщение затирало предыдущее). Повторы агрегируются ×N,
    // строки затухают fade-out'ом, стек максимум MaxToastLines строк.
    private VBoxContainer _toastStack    = null!;
    // 2026-09-19: индикатор паузы (INP-1 диагностика видимости) — см. UpdatePauseIndicator.
    private Label? _pauseIndicator;
    private string? _lastPauseText;
    // П6 (репорт 21.09): FPS-счётчик (F3 — тумбл, персист).
    private Label? _fpsLabel;
    private float _fpsAccum;
    private string? _lastFpsText;
    private sealed class ToastLine
    {
        public Label Label = null!;
        public float Remaining;
        public float Total;
        public string BaseText = "";
        public int RepeatCount = 1;
    }
    private readonly List<ToastLine> _toastLines = new();
    private const int   MaxToastLines  = 5;      // видимых строк стека
    private const float ToastFadeSec   = 0.45f;  // fade-out в конце жизни
    private const float ToastFadeInSec = 0.12f;  // fade-in появления

    // 2026-09-04 S5: стрелки направления атакующих вне экрана (см. DamageDirectionIndicator).
    private UI.DamageDirectionIndicator _dmgDirIndicator = null!;
    private const float DamageDirScreenMarginPx = 48f; // отступ стрелки от края экрана

    /// <summary>2026-09-04 S2: QA-доступ (GODOT_TOAST_DEBUG) — число строк стека.</summary>
    public int ToastLineCount => _toastLines?.Count ?? 0;

    /// <summary>2026-09-04 S2: QA-доступ — текст последней (нижней) строки.</summary>
    public string? LastToastText => _toastLines is { Count: > 0 } ? _toastLines[^1].Label.Text : null;

    /// <summary>2026-09-04 S2: QA — число строк стека с заданным префиксом
    /// (изоляция проверок от боевых тостов при комбинированных прогонах).</summary>
    public int ToastLinesWithPrefix(string prefix)
    {
        if (_toastLines == null) return 0;
        int n = 0;
        for (int i = 0; i < _toastLines.Count; i++)
            if (_toastLines[i].BaseText.StartsWith(prefix, System.StringComparison.Ordinal)) n++;
        return n;
    }

    // === 2026-09-04 S5: QA-доступ для индикатора направления урона ===

    /// <summary>Число активных стрелок направления (GODOT_DAMAGEDIR_DEBUG).</summary>
    public int DamageDirCount => _dmgDirIndicator?.ActiveCount ?? 0;

    /// <summary>Угол стрелки npcId в градусах (null — стрелки нет; 0°=восток, -90°=север).</summary>
    public float? DamageDirAngleOf(string npcId) => _dmgDirIndicator?.AngleOf(npcId);

    // === 2026-09-06 S6: QA-доступ для UX диалога и лавки ===

    /// <summary>Диалоговое окно (GODOT_DIALOGUE_DEBUG).</summary>
    public UI.DialogueWindow? DialogueWindowForQA => _dialogueWindow;

    /// <summary>Торговое окно (GODOT_TRADEUX_DEBUG).</summary>
    public UI.TradeWindow? TradeWindowForQA => _tradeWindow;

    // === R15: QA-доступ (GODOT_WEAPONVIS_DEBUG) ===

    /// <summary>Ключ текстуры hand-спрайта игрока (null — рука пуста).</summary>
    private string? _mainHandTextureKey;

    /// <summary>Ключ текущего hand-спрайта "class|tier|rarity" (QA).</summary>
    public string? MainHandTextureId => _mainHandTextureKey;

    /// <summary>Видимость hand-спрайта (QA: экип/анэкип).</summary>
    public bool MainHandVisible => _playerMainHand?.Visible ?? false;

    /// <summary>Направление взгляда игрока (QA: зеркалирование).</summary>
    public bool PlayerFacingLeft => _facingLeft;

    /// <summary>QA: принудительный facing (headless — ввода нет).</summary>
    public void DEBUG_SetFacingLeft(bool left) => _facingLeft = left;

    // === G0: QA-доступ (GODOT_ANIMQA_DEBUG) — аниматор игрока ============

    /// <summary>Аниматор игрока (QA: кадры/листы/оверлеи).</summary>
    public PlayerAnimator? PlayerAnimatorForQA => _playerAnimator;

    /// <summary>Текущая анимация игрока ("player_idle"…; QA).</summary>
    public string PlayerAnimId => _playerAnimator?.AnimId ?? "player_idle";

    /// <summary>Кадр анимации игрока (QA; 0 — процедурная статика).</summary>
    public int PlayerAnimFrame => _playerAnimator?.Frame ?? 0;

    /// <summary>Анимация из PNG-листа (QA; false — процедурный fallback).</summary>
    public bool PlayerAnimIsPng => _playerAnimator?.IsPng ?? false;

    /// <summary>Кадров в активном листе игрока (QA; 0 — fallback).</summary>
    public int PlayerAnimFrameCount => _playerAnimator?.FrameCount ?? 0;

    /// <summary>
    /// QA (AnimalCombatSimDebug): программный телепорт игрока — ЛОГИКА и
    /// ВИЗУАЛ. Прямой PlayerService.SetPosition «не прилипает»: _PhysicsProcess
    /// каждый кадр синхронизирует логику к _visualPosition (визуал — источник
    /// истины движения) → позиция откатывается к визуальной в тот же кадр.
    /// </summary>
    public void DEBUG_TeleportPlayer(int tileX, int tileY)
    {
        _visualPosition = new Vector2(
            tileX * GameConstants.TILE_PIXELS + GameConstants.TILE_PIXELS / 2f,
            tileY * GameConstants.TILE_PIXELS + GameConstants.TILE_PIXELS / 2f);
        _mouseTarget = null;
        Player?.SetPosition(new Position2D(tileX, tileY));
    }

    // === R13 FULL-LOOT: QA-доступ (GODOT_LOOT_DEBUG) ===

    /// <summary>Окно обыска трупа (GODOT_LOOT_DEBUG).</summary>
    public UI.LootWindow? LootWindowForQA => _lootWindow;

    /// <summary>Инвентарное окно (GODOT_TRASHDROP_DEBUG).</summary>
    public UI.InventoryWindow? InventoryWindowForQA => _inventoryWindow;

    // === Аудит-0915 A8 (INP1-QA): доступ к модальным окнам для
    // GODOT_MODALQA_DEBUG — верификация инварианта «пауза ⇔ стек окон ∨
    // Esc»: «×»-закрытие (мимо GWC-веток) обязано доходить до единой
    // точки резюма тиков. ===
    public UI.CharacterSheetWindow? CharacterSheetWindowForQA => _characterSheetWindow;
    // R20 (баг №6): K-окно — Esc-закрытие (не-паузящее окно).
    public UI.CultivationWindow? CultivationWindowForQA => _cultivationWindow;
    public UI.QuestWindow? QuestWindowForQA => _questWindow;
    public UI.EventLogWindow? EventLogWindowForQA => _eventLogWindow;
    public UI.HotkeysWindow? HotkeysWindowForQA => _hotkeysWindow;
    public UI.TechniqueBookWindow? TechniqueBookWindowForQA => _techniqueBook;

#if DEBUG
    // R37 (баг-репорт 23.09 №1): чит-панель — геометрический дифф и скрины
    // (GODOT_CHEATEQUIP_DEBUG / GODOT_CHEAT_SHOT).
    public UI.CheatPanel? CheatPanelForQA => _cheatPanel;
#endif

    // R37-c: окно зарядника для headless-симов (GODOT_CHARGERQA_DEBUG).
    public UI.ChargerWindow? ChargerWindowForQA => _chargerWindow;

    /// <summary>
    /// Открыть диалог с NPC по QA-пути (пауза+окно) — те же действия,
    /// что и HandleNpcTalk, без поиска ближнего NPC (GODOT_DIALOGUE_DEBUG).
    /// </summary>
    public bool DialogueDebugOpen(string npcId)
    {
        var dialogueSvc = DialogueService;
        if (dialogueSvc == null || _dialogueWindow == null) return false;
        if (string.IsNullOrEmpty(npcId)) return false;
        if (!dialogueSvc.TryStartNpcDialogue(npcId)) return false;

        // INP-1: центральный хелпер (снапшот только при входе первого окна).
        HandleModalPauseOnOpen(AnyModalWindowOpen());
        _dialogueWindow.Open(npcId);
        return true;
    }

    /// <summary>2026-09-04 S2: красная виньетка опасности при низком HP.</summary>
    private ColorRect _lowHpOverlay = null!;
    private float _lowHpPulseTime; // для пульсации при критическом HP

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
    // INP-1 (2026-09-16): семантика флага — «мир уже был запаузен ВНЕ стека
    // модальных окон в момент открытия ПЕРВОГО окна стека». Снапшот и резюм —
    // только через HandleModalPauseOnOpen / HandleModalResumeOnClose.
    private bool _wasPausedBeforeInventory;
    private System.IDisposable? _dialogueEndedToken;
    private System.IDisposable? _tradeOpenedToken;
    private System.IDisposable? _tradeClosedToken;
    private bool _overweightNotified; // debounce overweight toast

    // Speed change debounce — prevents rapid cycling when key held.
    // Minimum 1 real second between speed changes.
    private float _speedChangeCooldown;
    private const float SpeedChangeCooldownSec = 1.0f;

    // === INP-1 (2026-09-16): модальная пауза — единая точка ===
    // Баг-репорт пользователя: инвентарь, закрытый кликом по тёмному фону
    // (OnBackgroundClick → Toggle() мимо GameWorldController), НЕ снимал
    // паузу тиков — мир оставался заморожен: движение (клавиши И мышь)
    // мертво, персонаж стоит (HandleFreeMovement гейтится Time.IsPaused).
    // Тот же путь у листа персонажа (C). Плюс stacked-окна (диалог поверх
    // инвентаря и т.п.) перетирали флаг-снапшот — закрытие стека не
    // снимало паузу. Фикс по паттерну R13-audit P1-2 (LootWindow.Closed):
    // (1) Closed-события у окон с внутренними путями закрытия;
    // (2) снапшот флага ТОЛЬКО при входе первого окна стека;
    // (3) резюм ТОЛЬКО когда стек опустел и пауза ставилась ради окон.

    /// <summary>Открыто ли хоть одно модальное окно, паузящее мир.</summary>
    private bool AnyModalWindowOpen() =>
        (_inventoryWindow is { Visible: true })
        || (_characterSheetWindow is { Visible: true })
        || (_questWindow is { Visible: true })
        || (_eventLogWindow is { Visible: true })
        || (_hotkeysWindow is { Visible: true })
        || (_techniqueBook is { Visible: true })
        || (_lootWindow is { IsOpen: true })
        || (_tradeWindow is { IsOpen: true })
        || (_dialogueWindow is { IsOpen: true })
        // П4 (репорт 21.09): K-окно культивации — полноценное модальное
        // (прежде было только в списке клик-блокировки, паузы не ставило).
        || (_cultivationWindow is { Visible: true })
        // R37-c: окно зарядника (H) — модальное с паузой (управление
        // камнями/режимом — планирование, как инвентарь).
        || (_chargerWindow is { Visible: true });

    /// <summary>
    /// Аудит-0915 A3 (INP1-2): предикат «модальное окно открыто КРОМЕ лавки».
    /// Прежнее выражение в OnTradeOpened `AnyModalWindowOpen() &amp;&amp;
    /// _tradeWindow is not { IsOpen: true }` было инвертировано: TradeWindow
    /// подписан на TradeOpenedEvent РАНЬШЕ GWC (EventBus синхронный, порядок
    /// подписок) → к моменту хендлера IsOpen уже true → второй операнд
    /// всегда false → снапшот снимался ВСЕГДА, и после закрытия стека
    /// «диалог+лавка» мир оставался заморожен. Правильная семантика —
    /// перечислить остальные 8 окон явно.
    /// </summary>
    private bool AnyModalWindowOpenExceptTrade() =>
        (_inventoryWindow is { Visible: true })
        || (_characterSheetWindow is { Visible: true })
        || (_questWindow is { Visible: true })
        || (_eventLogWindow is { Visible: true })
        || (_hotkeysWindow is { Visible: true })
        || (_techniqueBook is { Visible: true })
        || (_lootWindow is { IsOpen: true })
        || (_dialogueWindow is { IsOpen: true });

    /// <summary>
    /// Открытие модального окна: пауза тиков, если ещё не стоит. Снапшот
    /// «мир был запаузен до окон» делается только при входе ПЕРВОГО окна
    /// стека (otherModalAlreadyOpen=false) — открытие поверх существующих
    /// окон флаг НЕ перетирает, иначе закрытие стека не сняло бы паузу.
    /// </summary>
    private void HandleModalPauseOnOpen(bool otherModalAlreadyOpen)
    {
        if (!otherModalAlreadyOpen)
            _wasPausedBeforeInventory = Time is { IsPaused: true };
        // П4 (репорт 21.09): тумблер «пауза при открытых окнах» (настройки,
        // default ON). Снапшот делается всегда — резюм при закрытии честен;
        // при OFF мир течёт, пока игрок изучает окна (промотка времени).
        if (!Persistence.GameSettings.PauseOnModalWindows) return;
        if (Time is { IsPaused: false })
            Time.Pause();
    }

    /// <summary>
    /// Закрытие модального окна (ЛЮБОЙ путь: клавиша, Esc, клик по фону,
    /// событие шины). Резюм тиков — только когда стек окон опустел и пауза
    /// ставилась ради окон (не игроком Esc'ом до их открытия). Идемпотентен:
    /// безопасно вызывать из нескольких точек одного закрытия
    /// (Closed-событие окна + inline-ветка клавиши).
    /// </summary>
    private void HandleModalResumeOnClose()
    {
        if (!AnyModalWindowOpen() && !_wasPausedBeforeInventory && Time is { IsPaused: true })
            Time.Resume();
    }

    /// <summary>
    /// 2026-09-19 (повторный репорт INP-1): HUD-индикатор паузы с ПРИЧИНОЙ.
    /// Пауза была невидима — «зависло» неотличимо от «пауза», из-за чего
    /// два баг-репорта о «сломанном движении» не могли быть диагностированы
    /// игроком. Теперь: «⏸ ПАУЗА — B·инвентарь + Q·квесты» (стек окон) или
    /// «⏸ ПАУЗА (Esc)» (пауза игрока). Обновление — только при изменении
    /// текста (zero-GC в steady-state); переход в лог (GD.Print).
    /// </summary>
    private void UpdatePauseIndicator()
    {
        if (Time == null || _pauseIndicator == null) return;
        string? text = null;
        if (Time.IsPaused)
        {
            var open = OpenModalWindowNames();
            text = open.Count > 0
                ? "⏸ ПАУЗА — " + string.Join(" + ", open)
                : "⏸ ПАУЗА (Esc)";
        }
        if (text == _lastPauseText) return; // steady-state: дёшево
        _lastPauseText = text;
        _pauseIndicator.Text = text ?? "";
        _pauseIndicator.Visible = text != null;
        if (text != null) GD.Print($"[GameWorld] {text}"); // диагностика причины в лог
    }

    /// <summary>П6 (репорт 21.09): FPS-счётчик — троттл 0.25с, цвет по диапазону
    /// (зелёный ≥55, янтарный 30-54, красный <30). Обновление текста только при
    /// изменении (аллокации минимизированы).</summary>
    private void UpdateFpsCounter(double delta)
    {
        if (_fpsLabel == null || !_fpsLabel.Visible) return;
        _fpsAccum += (float)delta;
        if (_fpsAccum < 0.25f) return;
        _fpsAccum = 0f;
        int fps = (int)Engine.GetFramesPerSecond();
        string text = $"FPS {fps}";
        if (text == _lastFpsText) return;
        _lastFpsText = text;
        _fpsLabel.Text = text;
        _fpsLabel.AddThemeColorOverride("font_color",
            fps >= 55 ? new Color(0.6f, 0.9f, 0.6f)
            : fps >= 30 ? new Color(0.95f, 0.8f, 0.4f)
            : new Color(0.95f, 0.45f, 0.4f));
    }

    /// <summary>Открытые модальные окна — короткие подписи для индикатора.</summary>
    private List<string> OpenModalWindowNames()
    {
        var names = new List<string>(4);
        if (_inventoryWindow is { Visible: true }) names.Add("B·инвентарь");
        if (_characterSheetWindow is { Visible: true }) names.Add("C·лист");
        if (_questWindow is { Visible: true }) names.Add("Q·квесты");
        if (_eventLogWindow is { Visible: true }) names.Add("J·журнал");
        if (_hotkeysWindow is { Visible: true }) names.Add("F1·справка");
        if (_techniqueBook is { Visible: true }) names.Add("T·книга");
        if (_lootWindow is { IsOpen: true }) names.Add("обыск");
        if (_tradeWindow is { IsOpen: true }) names.Add("лавка");
        if (_dialogueWindow is { IsOpen: true }) names.Add("диалог");
        if (_cultivationWindow is { Visible: true }) names.Add("K·культивация");
        if (_chargerWindow is { Visible: true }) names.Add("H·зарядник");
        return names;
    }

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
        // 2026-09-04 S3: kill-feed — смерти NPC → тост «☠ Имя повержен».
        _npcDeathToken = NpcDeathSub?.Subscribe(OnNpcDied);
        // R13 FULL-LOOT: труп удалён → закрыть окно обыска (авторитетная точка).
        _corpseRemovedToken = CorpseRemovedSub?.Subscribe(OnCorpseRemoved);
        // R15: смена оружия (WeaponMain/Off) → обновить hand-спрайт игрока.
        // Страховка на случай экипировки ДО _Ready (фазы сборки) — поллинг 0.5с
        // в _PhysicsProcess (сравнение по ItemId, дёшево).
        _equipmentChangedToken = EquipmentChangedSub?.Subscribe(OnEquipmentChanged);
        // R16: замах оружия игрока + тост стойки защиты (G).
        _attackIntentToken = AttackIntentSub?.Subscribe(OnAttackIntentForSwing);
        _defenseIntentToken = DefenseIntentSub?.Subscribe(OnDefenseIntent);
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
        // 2026-09-04 S2: headless-верификация тост-стека (GODOT_TOAST_DEBUG=1)
        // — стек не затирает сообщения, агрегирует повторы ×N, кап 5 строк.
        if (System.Environment.GetEnvironmentVariable("GODOT_TOAST_DEBUG") == "1")
        {
            var toastSim = new ToastSimDebug { Name = "ToastSimDebug" };
            AddChild(toastSim);
        }
        // 2026-09-08 (review этап 4): headless-верификация инвентарных
        // транзакций (GODOT_STORAGE_DEBUG=1) — spirit retrieve/stacking,
        // ring Qi, craft overflow, pickup integrity/граница.
        if (System.Environment.GetEnvironmentVariable("GODOT_STORAGE_DEBUG") == "1")
        {
            var storageSim = new StorageSimDebug { Name = "StorageSimDebug" };
            AddChild(storageSim);
        }
        // 2026-09-08 (review этап 5): headless-верификация DoT-пайплайна
        // (GODOT_DOT_DEBUG=1) — Poison/Burn/Bleed/Freeze наносят реальный
        // урон через DamageAppliedEvent (игрок + NPC).
        if (System.Environment.GetEnvironmentVariable("GODOT_DOT_DEBUG") == "1")
        {
            var dotSim = new DotSimDebug { Name = "DotSimDebug" };
            AddChild(dotSim);
        }
        // 2026-09-08 (review этап 6): headless-верификация мира/тайлов
        // (GODOT_RESPAWN_DEBUG=1) — respawn ресурсов, честный travel,
        // согласованность TimeChangedEvent.Delta.
        if (System.Environment.GetEnvironmentVariable("GODOT_RESPAWN_DEBUG") == "1")
        {
            var respawnSim = new RespawnSimDebug { Name = "RespawnSimDebug" };
            AddChild(respawnSim);
        }
        // 2026-09-08 (review этап 7): headless-верификация квестов
        // (GODOT_QUEST_DEBUG=1) — полный цикл: accept (через реальный
        // диалог) → событие → complete → reward; гейт уровня.
        if (System.Environment.GetEnvironmentVariable("GODOT_QUEST_DEBUG") == "1")
        {
            var questSim = new QuestSimDebug { Name = "QuestSimDebug" };
            AddChild(questSim);
        }
        // 2026-09-09: контекстное меню/диалог разделения стака инвентаря
        // (GODOT_CONTEXT_DEBUG=1) — ПКМ-свойства, слайдер деления кучек,
        // слот-адресный выброс в корзину.
        if (System.Environment.GetEnvironmentVariable("GODOT_CONTEXT_DEBUG") == "1")
        {
            var contextSim = new ContextMenuSimDebug { Name = "ContextMenuSimDebug" };
            AddChild(contextSim);
        }
        // 2026-09-09 (R11, ревью Save/Load): round-trip верификация
        // persistence (GODOT_SAVELOAD_DEBUG=1) — 8 ISaveable-блоков,
        // типизированный RestoreState, IncludeFields, честный success.
        if (System.Environment.GetEnvironmentVariable("GODOT_SAVELOAD_DEBUG") == "1")
        {
            var saveLoadSim = new SaveLoadSimDebug { Name = "SaveLoadSimDebug" };
            AddChild(saveLoadSim);
        }
        // 2026-09-08 (баг-репорт пользователя): headless-верификация
        // инвентарного drag&drop в корзину (GODOT_TRASHDROP_DEBUG=1) —
        // материалы draggable, корзина выбрасывает, кукла отклоняет.
        if (System.Environment.GetEnvironmentVariable("GODOT_TRASHDROP_DEBUG") == "1")
        {
            var trashDropSim = new TrashDropSimDebug { Name = "TrashDropSimDebug" };
            AddChild(trashDropSim);
        }
        // Аудит-0915 A8 (P2-3): модальные окна — верификация инварианта
        // «пауза ⇔ стек окон ∨ Esc» (GODOT_MODALQA_DEBUG=1): «×»/bg-click
        // всех 6 окон, идемпотентность резюма, стек инвентарь+лавка.
        if (System.Environment.GetEnvironmentVariable("GODOT_MODALQA_DEBUG") == "1")
        {
            var modalSim = new ModalSimDebug { Name = "ModalSimDebug" };
            AddChild(modalSim);
        }
        // 2026-09-19 (повторный репорт INP-1): MODAL2 — исчерпывающий перебор
        // 30×2 пар стеков + Esc-инвариант + тройной стек (GODOT_MODAL2_DEBUG=1).
        if (System.Environment.GetEnvironmentVariable("GODOT_MODAL2_DEBUG") == "1")
        {
            var modalStackSim = new ModalStackSimDebug { Name = "ModalStackSimDebug" };
            AddChild(modalStackSim);
        }
#if DEBUG
        // R37 (баг-репорт 23.09): чит-панель + экипировка сапогов —
        // (GODOT_CHEATEQUIP_DEBUG=1) headless-вердикт: A back-end гейты
        // экипировки (Feet L1/L5 vs Torso), B UI double-click по строке,
        // C геометрический дифф чит-панели до/после нажатий (сползание
        // надписей), D скриншоты (GODOT_CHEAT_SHOT=prefix, Xvfb+opengl3).
        if (System.Environment.GetEnvironmentVariable("GODOT_CHEATEQUIP_DEBUG") == "1")
        {
            var cheatEquipSim = new CheatEquipSimDebug { Name = "CheatEquipSimDebug" };
            AddChild(cheatEquipSim);
        }
#endif
        // R37-c (баг-репорт 23.09 «Зарядник не принимает камни Ци»):
        // (GODOT_CHARGERQA_DEBUG=1) полный путь — генерация зарядника →
        // экипировка → окно H → вставка камня (мост) → буфер/Ци →
        // анти-дюп (полный/частичный/пустой) → сейв round-trip → пояс.
#if DEBUG
        if (System.Environment.GetEnvironmentVariable("GODOT_CHARGERQA_DEBUG") == "1")
        {
            var chargerSim = new ChargerSimDebug { Name = "ChargerSimDebug" };
            AddChild(chargerSim);
        }
#endif
        // L500 (2026-09-15): мир 500×500 — интеграция генераций NPC
        // (GODOT_L500_DEBUG=1, в связке с GODOT_NEWGAME_WORLD=large_world).
        if (System.Environment.GetEnvironmentVariable("GODOT_L500_DEBUG") == "1")
        {
            var l500Sim = new L500SimDebug { Name = "L500SimDebug" };
            AddChild(l500Sim);
        }
        // 2026-09-04 S2: headless-верификация виньетки опасности
        // (GODOT_LOWHP_DEBUG=1) — alpha/пульс оверлея при HP < 35%/15%.
        if (System.Environment.GetEnvironmentVariable("GODOT_LOWHP_DEBUG") == "1")
        {
            var lowHpSim = new LowHpSimDebug { Name = "LowHpSimDebug" };
            AddChild(lowHpSim);
        }
        // 2026-09-04 S3: headless-верификация kill-feed (GODOT_KILLFEED_DEBUG=1)
        // — тост/журнал/дедуп смертей NPC в физическом бою.
        if (System.Environment.GetEnvironmentVariable("GODOT_KILLFEED_DEBUG") == "1")
        {
            var killFeedSim = new KillFeedSimDebug { Name = "KillFeedSimDebug" };
            AddChild(killFeedSim);
        }
        // 2026-09-08 (ревью-1 P1-3): headless-верификация ПОВТОРНОЙ сборки
        // сцены в одном процессе (GODOT_REASSEMBLY_DEBUG=1) — Reset фаз
        // оркестратора + world-scoped сброс реестра техник.
        if (System.Environment.GetEnvironmentVariable("GODOT_REASSEMBLY_DEBUG") == "1")
        {
            var reAssemblySim = new ReAssemblySimDebug { Name = "ReAssemblySimDebug" };
            AddChild(reAssemblySim);
        }
        // 2026-09-04 S4: headless-верификация хотбара v2 (GODOT_HOTBAR_DEBUG=1)
        // — техники в слотах 3-9, кулдаун-оверлей, Qi-гейт, пояс-ряд.
        if (System.Environment.GetEnvironmentVariable("GODOT_HOTBAR_DEBUG") == "1")
        {
            var hotbarSim = new HotbarSimDebug { Name = "HotbarSimDebug" };
            AddChild(hotbarSim);
        }
        // 2026-09-04 S5: headless-верификация стрелок направления урона
        // (GODOT_DAMAGEDIR_DEBUG=1) — показать/следовать/на-экране/TTL.
        if (System.Environment.GetEnvironmentVariable("GODOT_DAMAGEDIR_DEBUG") == "1")
        {
            var dmgDirSim = new DamageDirSimDebug { Name = "DamageDirSimDebug" };
            AddChild(dmgDirSim);
        }
        // 2026-09-06 S6: headless-верификация UX окна диалога
        // (GODOT_DIALOGUE_DEBUG=1) — подсказка/геометрия/индикатор/выбор/закрытие.
        if (System.Environment.GetEnvironmentVariable("GODOT_DIALOGUE_DEBUG") == "1")
        {
            var dlgSim = new DialogueSimDebug { Name = "DialogueSimDebug" };
            AddChild(dlgSim);
        }
        // 2026-09-06 S6: headless-верификация UX лавки
        // (GODOT_TRADEUX_DEBUG=1) — пометки «не хватает камней»/тултипы/hover/футер.
        if (System.Environment.GetEnvironmentVariable("GODOT_TRADEUX_DEBUG") == "1")
        {
            var tradeUxSim = new TradeUXSimDebug { Name = "TradeUXSimDebug" };
            AddChild(tradeUxSim);
        }
        // Stage 0+1 (2026-08-25, GLM-5.3): верификация модели заполнения +
        // ауры-задержки (вариант В): зарядка → hold → release → урон.
        if (System.Environment.GetEnvironmentVariable("GODOT_CHARGE_SIM") == "1")
        {
            var chargeSim = new ChargeSimDebug { Name = "ChargeSimDebug" };
            AddChild(chargeSim);
        }
        // Аудит 09.22 (внешний, upload/audit_09_22_07_30.txt): инфраструктурные
        // контракты — EventBus exception/re-entrancy (P1-5), санация имён слотов
        // (P1-8), DI override prune (P1-6), честный DeleteSave (P1-7),
        // Trade/Corpse IWorldResettable (P1-1/P1-4) + Фаза 4 того же аудита
        // (DI/lifecycle, доставлена отдельно): ResolveAll registration order
        // (P2-12), startup fail-closed (P2-13), cycle detection (P2-14).
        // До фиксов прогон = FAIL (runtime-подтверждение аудита), после —
        // регрессионный страж.
        if (System.Environment.GetEnvironmentVariable("GODOT_AUDIT0922_DEBUG") == "1")
        {
            var auditSim = new Audit0922SimDebug { Name = "Audit0922SimDebug" };
            AddChild(auditSim);
        }
        // R30-эпизод 2 (репорт 21.09): формации П1/П2/П3/П7 — радиус зарядки
        // (headless-вердикт GODOT_FORM_DEBUG=1) + скриншоты GODOT_FORM_SHOT.
        if (System.Environment.GetEnvironmentVariable("GODOT_FORM_DEBUG") == "1"
            || System.Environment.GetEnvironmentVariable("GODOT_FORM_SHOT") != null)
        {
            var formShot = new FormationShotSimDebug { Name = "FormationShotSimDebug" };
            AddChild(formShot);
        }
        // R13 FULL-LOOT (2026-09-10): headless-верификация спауна через
        // генерацию + трупов/обыска/full loot (GODOT_LOOT_DEBUG=1) —
        // состав населения из генератора, смерть → труп-контейнер,
        // ЛКМ-взятие по SlotId, «Забрать всё», TTL, респаун популяции.
        if (System.Environment.GetEnvironmentVariable("GODOT_LOOT_DEBUG") == "1")
        {
            var lootSim = new LootSimDebug { Name = "LootSimDebug" };
            AddChild(lootSim);
        }
        // R15 (2026-09-10): headless-верификация «оружие в руках»
        // (GODOT_WEAPONVIS_DEBUG=1) — WeaponClassId у генерации, спрайты
        // icon/hand по классам/тирам, композит игрока (экип/анэкип/фейсинг),
        // иконки хотбара, overlay NPC.
        if (System.Environment.GetEnvironmentVariable("GODOT_WEAPONVIS_DEBUG") == "1")
        {
            var weaponVisSim = new WeaponVisSimDebug { Name = "WeaponVisSimDebug" };
            AddChild(weaponVisSim);
        }
        // G0 (2026-09-25): headless-верификация подготовки спрайтов
        // (GODOT_ANIMQA_DEBUG=1) — SpriteSheetCache (fallback/загрузка/сетка),
        // PlayerAnimator (кадры/состояния/оверлей), PNG-миграция оружия,
        // аниматоры NPC/зверей.
        if (System.Environment.GetEnvironmentVariable("GODOT_ANIMQA_DEBUG") == "1")
        {
            var animQaSim = new SpriteAnimSimDebug { Name = "SpriteAnimSimDebug" };
            AddChild(animQaSim);
        }
        // R16 (2026-09-10): headless-верификация ИИ NPC в бою
        // (GODOT_COMBATAI_DEBUG=1) — месть на атаку игрока, урон в обе
        // стороны, селектор защит NPC, бегство HP<20%, leash, стойка
        // игрока (G), счётчики StrikeFX. GODOT_STRIKEFX_HOLD=1 — визуальный
        // режим (живая драка для Xvfb-скриншотов).
        if (System.Environment.GetEnvironmentVariable("GODOT_COMBATAI_DEBUG") == "1"
            || System.Environment.GetEnvironmentVariable("GODOT_STRIKEFX_HOLD") == "1")
        {
            var combatAiSim = new CombatAISimDebug { Name = "CombatAISimDebug" };
            AddChild(combatAiSim);
        }
        // 2026-09-11 (аудит боя с животными): headless-верификация контура
        // игрок↔животное (GODOT_ANIMALQA_DEBUG=1) — таргетинг Space по волку,
        // урон/месть/чейз, смерть → труп «Волк» (DEATH_AND_LOOT §5), de-aggro,
        // мирный кролик.
        if (System.Environment.GetEnvironmentVariable("GODOT_ANIMALQA_DEBUG") == "1")
        {
            var animalQaSim = new AnimalCombatSimDebug { Name = "AnimalCombatSimDebug" };
            AddChild(animalQaSim);
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
        // П3 (репорт 21.09): Filling-тост несёт радиус зарядки (П2) — игрок
        // знает правило «отошёл дальше — автонаполнение встало».
        string msg = e.NewStage switch
        {
            Core.Data.FormationStage.Drawing => "◈ Контур формации рисуется…",
            Core.Data.FormationStage.Filling => $"◈ Формация наполняется Ци… (зарядка в радиусе {FormationSvc?.ChargingRadiusTiles ?? 8} тайлов от контура)",
            Core.Data.FormationStage.Depleted => "◈ Формация истощена",
            _ => null
        };
        if (msg != null) ShowToast(msg);
    }

    /// <summary>Этап 5 + П3 (репорт 21.09): тост активации — ПОЛНОЕ описание
    /// (тип действия + эффекты + радиус + правило зарядки), а не просто
    /// «Формация активна: Усиление». Прежде игрок не понимал, что делает
    /// формация («светлая формация создалась, но не понятно, что делает»).</summary>
    private void OnFormationActivated(in Core.Messaging.Contracts.FormationActivatedEvent e)
    {
        string desc;
        var f = FormationSvc?.CurrentFormation;
        if (f != null && FormationSvc != null)
        {
            desc = Modules.Formation.FormationDescriptions.FullDescription(f, FormationSvc.ChargingRadiusTiles);
        }
        else
        {
            desc = $"{Modules.Formation.FormationDescriptions.TypeLabel(e.Type)} — " +
                   Modules.Formation.FormationDescriptions.TypeAction(e.Type);
        }
        ShowToast($"✦ Формация активна: {desc}", 4.5f);
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
        var playerTex = CreatePlayerTexture();
        _playerSprite = new Sprite2D
        {
            Name = "PlayerSprite",
            Texture = playerTex,
            ZIndex = (int)RenderLayer.Player,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
        _worldRoot.AddChild(_playerSprite);

        // G0: аниматор игрока (sprite-swap; fallback — процедурная статика).
        _playerAnimator = new PlayerAnimator(_playerSprite, playerTex);

        // R15: MainHand — hand-спрайт оружия (48×48, диагональ) поверх тела.
        // Texture/Visible управляются RefreshMainHand(); позиция и FlipH —
        // в _PhysicsProcess по _visualPosition и _facingLeft.
        _playerMainHand = new Sprite2D
        {
            Name = "PlayerMainHand",
            ZIndex = (int)RenderLayer.Player + 1,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            Visible = false,
        };
        _worldRoot.AddChild(_playerMainHand);

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

        // 2026-09-04 S2: виньетка опасности — полноэкранный тёмно-красный
        // оверлей при HP < 35% (пульсация при HP < 15%). Добавлен ПЕРВЫМ —
        // рисуется под всеми элементами HUD; не блокирует ввод (Ignore).
        _lowHpOverlay = new ColorRect
        {
            Name = "LowHpOverlay",
            Color = new Color(0.55f, 0.05f, 0.05f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _lowHpOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _hudCanvas.AddChild(_lowHpOverlay);

        // Time label — TOP line (visible, parchment color).
        // 2026-09-04 S1: + тёмная тень (VLM-аудит: светлый текст на светлом
        // фоне воды/неба был нечитаем — критичный контраст-баг).
        _timeLabel = new Label { Name = "TimeLabel" };
        _timeLabel.AddThemeFontSizeOverride("font_size", 18);
        _timeLabel.AddThemeColorOverride("font_color", new Color(0.94f, 0.83f, 0.66f));
        _timeLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.85f));
        _timeLabel.Position = new Vector2(20, 10);
        _hudCanvas.AddChild(_timeLabel);

        // Player HP bar (этап 4, 2026-08-22): под временем. HP = Σ RedHP по
        // частям тела (Q4). Цвет от зелёного к красному по мере потерь.
        // 2026-09-04 S1: + тёмная подложка-рамка (VLM: бары терялись на фоне).
        _hpBar = new ProgressBar
        {
            Name = "HpBar",
            MinValue = 0,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(260, 18),
            Position = new Vector2(20, 38),
        };
        _hpBar.AddThemeFontSizeOverride("font_size", 12);
        var hpBgStyle = new StyleBoxFlat { BgColor = new Color(0.08f, 0.05f, 0.03f, 0.85f) };
        hpBgStyle.SetBorderWidthAll(1);
        hpBgStyle.SetBorderColor(new Color(0.45f, 0.30f, 0.15f, 0.9f));
        hpBgStyle.SetCornerRadiusAll(3);
        _hpBar.AddThemeStyleboxOverride("background", hpBgStyle);
        _hudCanvas.AddChild(_hpBar);

        // 2026-09-04 S1: цифры HP поверх бара (VLM: нет числового HP игрока).
        _hpBarText = new Label
        {
            Name = "HpBarText",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _hpBarText.AddThemeFontSizeOverride("font_size", 12);
        _hpBarText.AddThemeColorOverride("font_color", new Color(0.95f, 0.92f, 0.85f));
        _hpBarText.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
        _hpBarText.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _hpBar.AddChild(_hpBarText);

        // Qi bar (этап 1 внедрения ЦИ, 2026-08-23): под HP-баром, золотой цвет.
        // Ци игрока из QiService (long — отображаем как double в ProgressBar).
        // 2026-09-04 S1: + тёмная подложка-рамка + цифры на баре.
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
        var qiBgStyle = new StyleBoxFlat { BgColor = new Color(0.08f, 0.05f, 0.03f, 0.85f) };
        qiBgStyle.SetBorderWidthAll(1);
        qiBgStyle.SetBorderColor(new Color(0.45f, 0.38f, 0.15f, 0.9f));
        qiBgStyle.SetCornerRadiusAll(3);
        _qiBar.AddThemeStyleboxOverride("background", qiBgStyle);
        _hudCanvas.AddChild(_qiBar);

        _qiBarText = new Label
        {
            Name = "QiBarText",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _qiBarText.AddThemeFontSizeOverride("font_size", 10);
        _qiBarText.AddThemeColorOverride("font_color", new Color(0.98f, 0.92f, 0.75f));
        _qiBarText.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
        _qiBarText.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _qiBar.AddChild(_qiBarText);

        // Подпись Ци (уровень культивации + проводимость — без дубля цифр,
        // они теперь на самом баре). 2026-09-04 S1: + тень (контраст).
        _qiLabel = new Label { Name = "QiLabel", Position = new Vector2(288, 60) };
        _qiLabel.AddThemeFontSizeOverride("font_size", 12);
        _qiLabel.AddThemeColorOverride("font_color", new Color(0.94f, 0.83f, 0.66f));
        _qiLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.85f));
        _hudCanvas.AddChild(_qiLabel);

        // 2026-09-04 S1: индикатор оружия/атаки под Qi-баром (VLM: не хватало
        // статуса боевой готовности): режим + кулдаун атаки (M2 §8.1).
        _attackStateLabel = new Label { Name = "AttackStateLabel", Position = new Vector2(20, 78) };
        _attackStateLabel.AddThemeFontSizeOverride("font_size", 12);
        _attackStateLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.78f, 0.6f));
        _attackStateLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.85f));
        _attackStateLabel.Text = "⚔ Ближний бой — атака готова";
        _hudCanvas.AddChild(_attackStateLabel);

        // Индикатор медитации (V): статус под Ци-баром.
        // Meditation label position shift (was 78): теперь на 96 — под новым
        // индикатором атаки.
        _meditationLabel = new Label { Name = "MeditationLabel", Position = new Vector2(20, 96), Visible = false };
        _meditationLabel.AddThemeFontSizeOverride("font_size", 13);
        _meditationLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.75f, 0.95f));
        _meditationLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
        _meditationLabel.Text = "☯ Медитация — поглощение Ци (движение прерывает)";
        _hudCanvas.AddChild(_meditationLabel);

        // 2026-09-11: легенда клавиш (HudHint) УДАЛЕНА с главного экрана —
        // решение пользователя 2026-08-28: ВСЕ подсказки клавиш живут в окне
        // справки F1 (HotkeysWindow, полный канонический перечень).
        // Легенда к тому же протухла: рекламировала нереализованные M/N-карты
        // и называла F1 чит-меню (F1 — справка, чит-меню — F2 с 2026-08-28).

        // 2026-09-04 S2: тост-стек (top-center): до 5 сообщений одновременно,
        // новое появляется снизу, старые поднимаются вверх и затухают.
        // Повторное сообщение не создаёт новую строку, а обновляет счётчик ×N.
        _toastStack = new VBoxContainer
        {
            Name = "ToastStack",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _toastStack.AddThemeConstantOverride("separation", 2);
        _toastStack.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        _toastStack.OffsetTop = 56;
        _toastStack.OffsetLeft = -260;
        _toastStack.OffsetRight = 260;
        _hudCanvas.AddChild(_toastStack);

        // 2026-09-19 (повторный репорт INP-1): индикатор паузы — игрок
        // ВИДИТ, почему мир стоит: «⏸ ПАУЗА — B·инвентарь» / «⏸ ПАУЗА (Esc)».
        // До этого пауза не отображалась никак — фриз от паузы были
        // неотличимы, что и породило два «ложных» баг-репорта о движении.
        _pauseIndicator = new Label
        {
            Name = "PauseIndicator",
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _pauseIndicator.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        _pauseIndicator.OffsetTop = 24;
        _pauseIndicator.AddThemeFontSizeOverride("font_size", 18);
        _pauseIndicator.AddThemeColorOverride("font_color", new Color(0.95f, 0.82f, 0.45f));
        _hudCanvas.AddChild(_pauseIndicator);

        // П6 (репорт 21.09, «заметил подлагивания — нужен счётчик FPS»):
        // FPS-счётчик в левом-верхнем углу. F3 — тумбл (персистится),
        // обновление текста — троттл 0.25с (zero-GC в steady-state не выйдет,
        // но минимизируем аллокации строк). Цвет честный: зелёный ≥55,
        // янтарный 30-54, красный <30 — игрок сразу видит «подлагивания».
        _fpsLabel = new Label
        {
            Name = "FpsCounter",
            Visible = Persistence.GameSettings.ShowFpsCounter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _fpsLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        _fpsLabel.OffsetLeft = 8;
        _fpsLabel.OffsetTop = 6;
        _fpsLabel.OffsetRight = 110;
        _fpsLabel.OffsetBottom = 26;
        _fpsLabel.AddThemeFontSizeOverride("font_size", 13);
        _fpsLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.9f, 0.6f));
        _fpsLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.8f));
        _fpsLabel.AddThemeConstantOverride("outline_size", 2);
        _hudCanvas.AddChild(_fpsLabel);

        // 2026-09-04 S5: стрелки направления атакующих вне экрана — игрок
        // видит, ОТКУДА прилетает урон, даже когда источник за кадром.
        _dmgDirIndicator = new UI.DamageDirectionIndicator { Name = "DamageDirIndicator" };
        _hudCanvas.AddChild(_dmgDirIndicator);

        // Inventory window (opens with B key) — must be created AFTER _hudCanvas.
        // INP-1: Closed-событие — окно закрывается и кликом по тёмному фону
        // (OnBackgroundClick мимо GWC) — резюм тиков из единой точки.
        _inventoryWindow = new InventoryWindow { Name = "InventoryWindow" };
        _inventoryWindow.Closed += HandleModalResumeOnClose;
        _hudCanvas.AddChild(_inventoryWindow);

        // Character sheet window (opens with C key).
        // INP-1: Closed-событие — то же (bg-click-закрытие без ведома GWC).
        _characterSheetWindow = new CharacterSheetWindow { Name = "CharacterSheetWindow" };
        _characterSheetWindow.Closed += HandleModalResumeOnClose;
        _hudCanvas.AddChild(_characterSheetWindow);

        // Dialogue window (opens with E key near an NPC) — NPC_COMBAT_PREP Phase 2.
        _dialogueWindow = new UI.DialogueWindow { Name = "DialogueWindow" };
        _hudCanvas.AddChild(_dialogueWindow);

        // Trade window (merchant shop) — NPC_COMBAT_PREP Phase 5. Открывается
        // по TradeOpenedEvent (выбор «Покажи товары» в диалоге торговца),
        // закрывается по TradeClosedEvent / Esc. Пауза — OnTradeOpened ниже.
        _tradeWindow = new UI.TradeWindow { Name = "TradeWindow" };
        _hudCanvas.AddChild(_tradeWindow);

        // R13 FULL-LOOT: окно обыска трупа. Открывается по E рядом с трупом
        // (HandleCorpseSearchOrNpcTalk ниже), закрывается Esc/кнопкой «Уйти»/
        // CorpseRemovedEvent. Пауза тиков — при открытии (обыск = планирование).
        // R13-audit (P1-1/P1-2): Closed-событие — единая авторитетная точка
        // резюма тиков (все пути закрытия проходят через Close()).
        _lootWindow = new UI.LootWindow { Name = "LootWindow" };
        _lootWindow.Closed += OnLootWindowClosed;
        _hudCanvas.AddChild(_lootWindow);

        // Hotbar (2026-08-22): 9 quick slots bottom-center; belt slots 3-9
        // appear when a belt is equipped.
        _hotbarPanel = new UI.HotbarPanel { Name = "HotbarPanel" };
        _hudCanvas.AddChild(_hotbarPanel);

        // 2026-08-28: Книга Техник (T) — матрица вкладки-уровни / блоки-типы /
        // строки-стихии + свитки + архив. Заменяет HUD-панель техник.
        // Аудит-0915 A2 (INP1-1): Closed — «×»-закрытие доходит до единой
        // точки резюма тиков (пауза T-окна больше не переживает закрытие).
        _techniqueBook = new UI.TechniqueBookWindow { Name = "TechniqueBookWindow" };
        _techniqueBook.Closed += HandleModalResumeOnClose;
        _hudCanvas.AddChild(_techniqueBook);

        // 2026-08-28: окно-справка горячих клавиш (F1).
        // Аудит-0915 A2: Closed — то же («×»-путь).
        _hotkeysWindow = new UI.HotkeysWindow { Name = "HotkeysWindow" };
        _hotkeysWindow.Closed += HandleModalResumeOnClose;
        _hudCanvas.AddChild(_hotkeysWindow);

        // 2026-09-04 S1: Журнал событий (J) — был рекламирован в легенде HUD
        // и F1-справке, но не существовал («мёртвая проводка» клавиши journal).
        // Аудит-0915 A2: Closed — то же («×»-путь).
        _eventLogWindow = new UI.EventLogWindow { Name = "EventLogWindow" };
        _eventLogWindow.Closed += HandleModalResumeOnClose;
        _hudCanvas.AddChild(_eventLogWindow);

        // 2026-09-04 S1: Журнал заданий (Q) — вторая «мёртвая проводка».
        // Аудит-0915 A2: Closed — то же («×»-путь).
        _questWindow = new UI.QuestWindow { Name = "QuestWindow" };
        _questWindow.Closed += HandleModalResumeOnClose;
        _hudCanvas.AddChild(_questWindow);

        // C3 (2026-08-26): окно Культивации Ци (K) — 3 вкладки (Техники / Меридианы / Ядро)
        // + нижняя панель слотов техник (3-9). Открывается клавишей K через
        // CultivationWindowToggleRequestedEvent из InputAdapter (этап D).
        _cultivationWindow = new UI.CultivationWindow { Name = "CultivationWindow" };
        _hudCanvas.AddChild(_cultivationWindow);

        // R37-c (баг-репорт 23.09 «Зарядник не принимает камни Ци»): окно
        // зарядника Ци (H) — слоты камней (drag&drop из инвентаря, ПКМ —
        // извлечь), буфер/тепло/режим. Гейт: зарядник надет в слот пояса
        // (ChargerItemBridge.IsChargerEquipped). Closed — единая точка
        // резюма тиков (Esc/фон/повторное H).
        _chargerWindow = new UI.ChargerWindow { Name = "ChargerWindow" };
        _chargerWindow.Closed += HandleModalResumeOnClose;
        _hudCanvas.AddChild(_chargerWindow);

#if DEBUG
        // Этап 7: чит-меню разработки (F2, с 2026-08-28; раньше F1).
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

        // G0: тик аниматора игрока. Пауза времени — стоп-кадр (§4.3 плана):
        // таймеры one-shot замирают вместе с миром.
        if (_playerAnimator != null && (Time == null || !Time.IsPaused))
        {
            _playerAnimator.Update(delta, new PlayerAnimator.FrameState(
                _lastMoving, _lastRunning, _lastSpeedMult, _meditationActive));
        }

        // R15: композит — оружие в руке. Позиция = тело + HandOffset
        // (зеркалирование ТОЛЬКО по X: offset → (-X, Y)); FlipH зеркалит
        // содержимое текстуры вокруг центра спрайта (SPRITE_CATALOG §16).
        if (_playerMainHand != null)
        {
            var off = _mainHandOffset;

            // R16: анимация замаха — выпад к цели (sin-кривая) + Scale-пульс.
            float swingDx = 0f, swingDy = 0f;
            float scalePulse = 0f;
            if (_mainHandSwingAge >= 0f)
            {
                _mainHandSwingAge += (float)delta;
                if (_mainHandSwingAge >= MainHandSwingSec)
                {
                    _mainHandSwingAge = -1f; // анимация закончена
                }
                else
                {
                    float k = _mainHandSwingAge / MainHandSwingSec;
                    float lunge = Mathf.Sin(k * Mathf.Pi) * MainHandSwingLungePx;
                    swingDx = _mainHandSwingDir.X * lunge;
                    swingDy = _mainHandSwingDir.Y * lunge * 0.6f; // вертикаль мягче
                    scalePulse = Mathf.Sin(k * Mathf.Pi) * 0.18f;
                }
            }

            _playerMainHand.Position = new Vector2(
                _visualPosition.X + (_facingLeft ? -off.X : off.X) + swingDx,
                _visualPosition.Y + off.Y + swingDy);
            _playerMainHand.Scale = new Vector2(1f + scalePulse, 1f + scalePulse * 0.5f);
            _playerMainHand.FlipH = _facingLeft;
            _playerSprite.FlipH = _facingLeft;

            // Страховка синхронизации: экип мог произойти до подписки
            // (фазы сборки/загрузка сейва) → сверяем ItemId раз в 0.5с.
            _mainHandSyncCooldown -= (float)delta;
            if (_mainHandSyncCooldown <= 0f)
            {
                _mainHandSyncCooldown = 0.5f;
                string? currentId = Equipment?.GetEquipped(EquipmentSlot.WeaponMain)?.ItemId;
                if (currentId != _mainHandCacheItemId) RefreshMainHand();
            }
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
        // 2026-09-04 S1: + числовая подпись на баре.
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
                if (_hpBarText != null)
                    _hpBarText.Text = $"HP {cur}/{max}";

                // 2026-09-04 S2: виньетка опасности — «экран краснеет» при
                // HP < 35%, пульсация при HP < 15% (информирование без
                // чтения цифр — периферийное зрение).
                if (_lowHpOverlay != null)
                {
                    float danger = ratio < 0.35f
                        ? Mathf.Clamp((0.35f - ratio) / 0.30f, 0f, 1f)
                        : 0f;
                    if (ratio < 0.15f)
                    {
                        _lowHpPulseTime += (float)delta;
                        danger *= 0.72f + 0.28f * Mathf.Sin(_lowHpPulseTime * 5f);
                    }
                    _lowHpOverlay.Color = new Color(0.55f, 0.05f, 0.05f, 0.32f * danger);
                }
            }
        }

        // Этап 1 внедрения ЦИ: Qi bar — текущее Ци / MaxQi (long → double).
        // 2026-09-04 S1: цифры на баре; подпись справа = только прогресс
        // культивации (без дубля значений Ци).
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
                if (_qiBarText != null)
                    _qiBarText.Text = $"Ци {QiService.CurrentQi}/{qiMax}";
                _qiLabel.Text = $"L{(int)QiService.CultivationLevel}.{QiService.SubLevel}" +
                                $" | пров. {QiService.Conductivity:F1}/с";
            }
        }

        // 2026-09-04 S1: индикатор боевой готовности — режим оружия +
        // кулдаун атаки (M2 §8.1). Информативность: игрок видит, когда удар
        // снова доступен, и в каком он режиме (melee/ranged).
        if (_attackStateLabel != null && CombatAdapter != null)
        {
            bool ranged = CombatAdapter.CurrentWeaponMode == Modules.Player.PlayerCombatAdapter.WeaponMode.Ranged;
            float cd = CombatAdapter.AttackCooldownRemaining;
            _attackStateLabel.Text = cd > 0.05f
                ? $"⚔ {(ranged ? "Дальний бой" : "Ближний бой")} — удар через {cd:F1}с"
                : $"⚔ {(ranged ? "Дальний бой" : "Ближний бой")} — атака готова";
            // Готовность = ярче; перезарядка = приглушено.
            _attackStateLabel.Modulate = cd > 0.05f
                ? new Color(0.75f, 0.7f, 0.6f)
                : Colors.White;
        }

        // 2026-09-04 S2: тик тост-стека — таймеры, fade-in/out, удаление истёкших.
        for (int i = _toastLines.Count - 1; i >= 0; i--)
        {
            var line = _toastLines[i];
            line.Remaining -= (float)delta;
            if (line.Remaining <= 0f)
            {
                _toastStack.RemoveChild(line.Label);
                line.Label.QueueFree();
                _toastLines.RemoveAt(i);
                continue;
            }
            float elapsed = line.Total - line.Remaining;
            float alpha = 1f;
            if (elapsed < ToastFadeInSec) alpha = elapsed / ToastFadeInSec;
            else if (line.Remaining < ToastFadeSec) alpha = line.Remaining / ToastFadeSec;
            line.Label.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(alpha, 0f, 1f));
        }

        // 2026-09-04 S5: тик стрелок направления — обновить позы живых стрелок
        // (NPC движется), убрать стрелки вернувшихся на экран/умерших.
        UpdateDamageDirIndicators();

        HandleStickyInput();

        // 2026-09-19: видимость паузы + причина (только при изменении — дёшево).
        UpdatePauseIndicator();
        // П6 (репорт 21.09): FPS-счётчик (троттл 0.25с, только при изменении текста).
        UpdateFpsCounter(delta);
        // Этап 1 внедрения ЦИ: V — переключить медитацию (поглощение Ци из среды).
        if (PlayerInput is { IsMeditatePressed: true })
        {
            MeditationTogglePub.Publish(
                new Core.Messaging.Contracts.MeditationToggleRequestedEvent(!_meditationActive));
        }

        // 2026-08-28: T — Книга Техник (модальное окно с паузой, как инвентарь:
        // планирование арсенала — Old School). X — следующая техника, Z — каст.
        // R13-audit (P2-2): при открытом окне обыска T/F1/J/Q/B/C ЗАПРЕЩЕНЫ —
        // они перезаписывали бы общий флаг паузы _wasPausedBeforeInventory
        // (пауза «залипала» после закрытия окон). Обыск — полная модальность
        // (как E); сначала закрыть окно обыска (Esc/«Уйти»).
        if (PlayerInput is { IsTechniquesPressed: true } && _techniqueBook != null && Time != null
            && _lootWindow is not { IsOpen: true })
        {
            bool otherModalOpen = AnyModalWindowOpen();
            _techniqueBook.Toggle();
            if (_techniqueBook.Visible)
                HandleModalPauseOnOpen(otherModalOpen);
            else
                HandleModalResumeOnClose();
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
        // M2 (2026-09-03): тост «⚔ Атака!» при polling убран — спамил каждый
        // кадр при удержании Space (~60/сек). Фидбек атаки теперь честный:
        // урон — цифрами (DamageNumberRenderer), отклонение — тостом по
        // AttackRejectedEvent (подписка при первом нажатии Space), попадание
        // «вхолостую» (нет цели в радиусе) — без тоста, как у NPC.
        CombatAdapter?.Tick((float)delta);
        if (PlayerInput is { IsAttackPressed: true } && _attackRejectedToken == null)
        {
            // Lazy-подписка на отклонения атак (первое нажатие Space):
            // C-5 аудита-3 публикует AttackRejectedEvent при _isCasting.
            _attackRejectedToken = AttackRejectedSub?.Subscribe(OnAttackRejected);
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
        // INP-1: + журнал заданий (Q) и журнал событий (J) — их оверлеи не
        // всегда потребляют колесо (утечка зума при открытом окне).
        bool modalOpen = (_inventoryWindow is { Visible: true })
                      || (_characterSheetWindow is { Visible: true })
                      || (_dialogueWindow is { IsOpen: true })
                      || (_tradeWindow is { IsOpen: true })
                      || (_lootWindow is { IsOpen: true })
                      || (_techniqueBook is { Visible: true })
                      || (_hotkeysWindow is { Visible: true })
                      || (_questWindow is { Visible: true })
                      || (_eventLogWindow is { Visible: true })
#if DEBUG
                      || (_cheatPanel is { Visible: true })
#endif
                      || (_cultivationWindow is { Visible: true });

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
        if (Time != null && Time.IsPaused)
        {
            _lastMoving = false; // G0: аниматор — стоп-кадр
            return;
        }

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

            // R15: направление взгляда по горизонтальной компоненте ввода.
            if (moveVec.X > 0.15f) _facingLeft = false;
            else if (moveVec.X < -0.15f) _facingLeft = true;

            // G0: снимок для аниматора (walk/run + множитель fps).
            _lastMoving = true;
            _lastRunning = Godot.Input.IsActionPressed("run");
            _lastSpeedMult = speedMult;

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

            // G0: снимок для аниматора (мышиное движение = walk).
            _lastMoving = dist >= 4f;
            _lastRunning = false;
            _lastSpeedMult = speedMult;

            if (dist < 4f)  // close enough — snap
            {
                _visualPosition = target;
                _mouseTarget = null;
            }
            else
            {
                var dir = diff.Normalized();
                _visualPosition += dir * MoveSpeedPixels * speedMult * (float)delta;
                // R15: клик-движение — взгляд по направлению к цели.
                if (Mathf.Abs(diff.X) > 8f) _facingLeft = diff.X < 0f;
            }
        }

        else
        {
            // G0: ни ввода, ни цели — покой.
            _lastMoving = false;
            _lastRunning = false;
            _lastSpeedMult = 1f;
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
        // Этап 7: F2 — чит-меню (только в DEBUG-сборке).
        // Работает независимо от состояния UI (modalOpen, пауза).
        // M2 (2026-09-03): гейт по настройке — пользователь может отключить
        // чит-меню в настройках главного меню (user://settings.json).
        if (PlayerInput.IsCheatMenuPressed && _cheatPanel != null)
        {
            GameSettings.EnsureLoaded();
            if (!GameSettings.CheatsEnabled)
            {
                ShowToast("✖ Чит-меню отключено (Настройки в главном меню)");
                return;
            }
            _cheatPanel.Visible = !_cheatPanel.Visible;
            GD.Print($"[GameWorld] CheatPanel: {(_cheatPanel.Visible ? "open" : "closed")}");
        }
#endif

        // 2026-09-19 R18-1: F7 — тумблер индикации врагов (HP-бары над
        // врагами + цифры урона над ними). Настройка игрока (не DEBUG):
        // работает всегда (как cheat_menu), мгновенно персистится
        // (user://settings.json). Подготовка высокой сложности — там
        // индикация противника выключена по дизайну.
        if (PlayerInput.IsEnemyVitalsTogglePressed)
        {
            GameSettings.EnsureLoaded();
            bool newValue = !GameSettings.ShowEnemyVitals;
            GameSettings.SetShowEnemyVitals(newValue);
            ShowToast(newValue
                ? "👁 Индикация врагов: ВКЛ (HP-бары и урон)"
                : "🚫 Индикация врагов: ВЫКЛ (как на высокой сложности)");
            GD.Print($"[GameWorld] ShowEnemyVitals = {newValue} (F7, saved to user://settings.json)");
        }

        // П6 (репорт 21.09): F3 — тумбл FPS-счётчика (диагностика подлагиваний).
        if (PlayerInput.IsFpsCounterTogglePressed)
        {
            GameSettings.EnsureLoaded();
            bool newValue = !GameSettings.ShowFpsCounter;
            GameSettings.SetShowFpsCounter(newValue);
            if (_fpsLabel != null) _fpsLabel.Visible = newValue;
            ShowToast(newValue
                ? "📊 FPS-счётчик: ВКЛ (левый-верхний угол)"
                : "📊 FPS-счётчик: ВЫКЛ");
            GD.Print($"[GameWorld] ShowFpsCounter = {newValue} (F3, saved to user://settings.json)");
        }

        // 2026-08-28: F1 — окно-справка горячих клавиш (с паузой — чтение).
        // R13-audit (P2-2): гвард модальности обыска (см. комментарий у T).
        if (PlayerInput.IsHelpHotkeysPressed && _hotkeysWindow != null && Time != null
            && _lootWindow is not { IsOpen: true })
        {
            bool otherModalOpen = AnyModalWindowOpen();
            _hotkeysWindow.Toggle();
            if (_hotkeysWindow.Visible)
                HandleModalPauseOnOpen(otherModalOpen);
            else
                HandleModalResumeOnClose();
        }

        // 2026-09-04 S1: J — Журнал событий (модальное окно, пауза как F1).
        // R13-audit (P2-2): гвард модальности обыска (см. комментарий у T).
        if (PlayerInput.IsJournalPressed && _eventLogWindow != null && Time != null
            && _lootWindow is not { IsOpen: true })
        {
            bool otherModalOpen = AnyModalWindowOpen();
            _eventLogWindow.Toggle();
            if (_eventLogWindow.Visible)
                HandleModalPauseOnOpen(otherModalOpen);
            else
                HandleModalResumeOnClose();
        }

        // 2026-09-04 S1: Q — Журнал заданий (модальное окно, пауза как F1).
        // R13-audit (P2-2): гвард модальности обыска (см. комментарий у T).
        if (PlayerInput.IsQuestLogPressed && _questWindow != null && Time != null
            && _lootWindow is not { IsOpen: true })
        {
            bool otherModalOpen = AnyModalWindowOpen();
            _questWindow.Toggle();
            if (_questWindow.Visible)
                HandleModalPauseOnOpen(otherModalOpen);
            else
                HandleModalResumeOnClose();
        }

        // R37-c (баг-репорт 23.09): H — окно зарядника Ци (модальное с
        // паузой; гейт содержимого — надетый зарядник, без него окно
        // показывает подсказку). Гвард модальности обыска — как у Q/J.
        if (PlayerInput.IsChargerWindowPressed && _chargerWindow != null && Time != null
            && _lootWindow is not { IsOpen: true })
        {
            bool otherModalOpen = AnyModalWindowOpen();
            _chargerWindow.Toggle();
            if (_chargerWindow.Visible)
                HandleModalPauseOnOpen(otherModalOpen);
            else
                HandleModalResumeOnClose();
        }

        // 2026-08-28: Esc сначала закрывает окна новой волны (справка/книга/чит).
        if (PlayerInput.IsPausePressed && _hotkeysWindow is { Visible: true })
        {
            _hotkeysWindow.Close();
            HandleModalResumeOnClose(); // INP-1: центральный резюм
        }
        // 2026-09-04 S1: Esc закрывает журнал событий (J).
        else if (PlayerInput.IsPausePressed && _eventLogWindow is { Visible: true })
        {
            _eventLogWindow.Toggle();
            HandleModalResumeOnClose(); // INP-1: центральный резюм
        }
        // 2026-09-04 S1: Esc закрывает окно квестов (Q).
        else if (PlayerInput.IsPausePressed && _questWindow is { Visible: true })
        {
            _questWindow.Toggle();
            HandleModalResumeOnClose(); // INP-1: центральный резюм
        }
        else if (PlayerInput.IsPausePressed && _techniqueBook is { Visible: true })
        {
            _techniqueBook.Close();
            HandleModalResumeOnClose(); // INP-1: центральный резюм
        }
#if DEBUG
        else if (PlayerInput.IsPausePressed && _cheatPanel is { Visible: true })
        {
            _cheatPanel.Visible = false;
        }
#endif
        // Esc while trading → close the shop (Phase 5). Authoritative resume
        // happens in OnTradeClosed (bus event), like dialogues.
        else if (PlayerInput.IsPausePressed && _tradeWindow is { IsOpen: true })
        {
            _tradeWindow.Close();
        }
        // R13 FULL-LOOT: Esc при открытом окне обыска → закрыть (резюм тиков —
        // в OnLootWindowClosed по Closed-событию, единая точка для всех путей).
        else if (PlayerInput.IsPausePressed && _lootWindow is { IsOpen: true })
        {
            _lootWindow.Close();
        }
        // Esc while a dialogue is open → close it and resume ticks (Phase 2).
        else if (PlayerInput.IsPausePressed && _dialogueWindow is { IsOpen: true })
        {
            _dialogueWindow.Close();
            HandleModalResumeOnClose(); // INP-1: центральный резюм
        }
        // INP-1: Esc закрывает лист персонажа (C) — раньше Esc под этим окном
        // снимал паузу (ветка ниже), окно висело поверх бегущего мира.
        else if (PlayerInput.IsPausePressed && _characterSheetWindow is { Visible: true })
        {
            _characterSheetWindow.Toggle();
            HandleModalResumeOnClose();
        }
        // R20 (баг №6, запрос 09_09_22_40): Esc закрывает окно Культивации (K).
        // Репорт: «не закрывается по ESC, требует повторного нажатия K».
        // Правило пользователя: два типа закрытия — повторная клавиша вызова
        // И классический Esc. П4 (репорт 21.09): K теперь модальное с паузой —
        // снятие паузы при Esc-закрытии (как у инвентаря).
        else if (PlayerInput.IsPausePressed && _cultivationWindow is { Visible: true })
        {
            _cultivationWindow.Toggle();
            HandleModalResumeOnClose();
        }
        // R37-c: Esc закрывает окно зарядника (H) — правило пользователя
        // «два типа закрытия: повторная клавиша вызова И классический Esc».
        // Resume — через Closed-событие окна (единая точка, как у инвентаря).
        else if (PlayerInput.IsPausePressed && _chargerWindow is { Visible: true })
        {
            _chargerWindow.Close();
        }
        // Esc (sticky "escape") → toggle pause. INP-1: только когда НЕ открыто
        // ни одного модального окна (раньше гард проверял только инвентарь —
        // Esc под Q/J/F1/T снимал паузу под висящим окном).
        else if (PlayerInput.IsPausePressed && !AnyModalWindowOpen())
        {
            if (Time.IsPaused) Time.Resume();
            else               Time.Pause();
            GD.Print($"[GameWorld] Pause toggled: {Time.IsPaused}");
        }
        // If inventory is open and Esc pressed, close it instead of pausing.
        // 2026-09-09: Esc сначала закрывает верхний попап (диалог разделения
        // стака → контекстное меню), и только потом — само окно инвентаря.
        else if (PlayerInput.IsPausePressed && _inventoryWindow != null && _inventoryWindow.Visible)
        {
            if (_inventoryWindow.CloseTopmostPopup())
            {
                GD.Print("[GameWorld] Esc закрыл попап инвентаря (меню/диалог)");
            }
            else
            {
                _inventoryWindow.Toggle();
                // INP-1: центральный резюм (то же, что B-close и bg-click).
                HandleModalResumeOnClose();
            }
        }

        // R13-audit (P2-2): гвард модальности обыска (см. комментарий у T) —
        // B при открытом LootWindow съедается: иначе общий флаг паузы
        // перезаписывается и мир остаётся заморожен после закрытия окон.
        if (PlayerInput.IsInventoryPressed && _lootWindow is not { IsOpen: true })
        {
            // INP-1: пауза/резюм — центральные хелперы. Закрытие кликом по
            // фону резюмит через Closed-событие окна ещё до этой ветки
            // (идемпотентно); флаг-снапшот не перетирается поверх других окон.
            // Rationale: inventory management is a planning activity (like Kenshi/RimWorld).
            bool otherModalOpen = AnyModalWindowOpen();
            _inventoryWindow?.Toggle();
            if (_inventoryWindow is { Visible: true })
                HandleModalPauseOnOpen(otherModalOpen);
            else
                HandleModalResumeOnClose();
        }

        // F key: harvest resource from tile under cursor (within distance).
        if (PlayerInput.IsHarvestPressed)
        {
            HandleHarvest();
        }

        // C key: toggle Character Sheet (body status + stats).
        // R13-audit (P2-2): гвард модальности обыска (см. комментарий у T).
        if (PlayerInput.IsCharacterSheetPressed && _lootWindow is not { IsOpen: true })
        {
            bool otherModalOpen = AnyModalWindowOpen();
            _characterSheetWindow?.Toggle();
            // INP-1: центральные пауза/резюм (bg-click-закрытие — через
            // Closed-событие окна).
            if (_characterSheetWindow is { Visible: true })
                HandleModalPauseOnOpen(otherModalOpen);
            else
                HandleModalResumeOnClose();
        }

        // D (2026-08-26): K — окно Культивации Ци (3 вкладки + слоты техник 3-9).
        // П4 (репорт 21.09): теперь — модальное с паузой (как инвентарь/книга);
        // тумблер «пауза при открытых окнах» в настройках позволяет времени
        // течь, пока игрок изучает техники (см. HandleModalPauseOnOpen).
        if (PlayerInput.IsCultivationWindowPressed)
        {
            bool wasOpen = _cultivationWindow is { Visible: true };
            if (!wasOpen)
                HandleModalPauseOnOpen(AnyModalWindowOpen()); // K ещё не виден
            _cultivationWindow?.Toggle();
            if (wasOpen)
                HandleModalResumeOnClose();
        }

        // D: 1 — выбор ближнего оружия. Phase 8 ч.2 (2026-09-03): реальный
        // режим ближнего боя (кулаки/меч) вместо «зарезервировано».
        if (PlayerInput.IsWeaponMeleePressed)
        {
            CombatAdapter.SwitchToMeleeMode();
            ToastPub?.Publish(new Core.Messaging.Contracts.ToastShownEvent(
                "Режим: ближний бой (Space — удар)", 1.5f));
        }

        // D: 2 — выбор дальнего оружия. Phase 8 ч.2: переключение в ranged
        // при экипированном луке/арбалете (WeaponMain, AttackRange > 2).
        if (PlayerInput.IsWeaponRangedPressed)
        {
            if (CombatAdapter.SwitchToRangedMode())
            {
                var bow = CombatAdapter.GetRangedWeapon();
                ToastPub?.Publish(new Core.Messaging.Contracts.ToastShownEvent(
                    $"Режим: дальний бой — {bow!.NameRu} (дальность {bow.AttackRange} м)", 2.0f));
            }
            else
            {
                ToastPub?.Publish(new Core.Messaging.Contracts.ToastShownEvent(
                    "Нет дальнобойного оружия — экипируйте лук в слот оружия (I)", 2.5f));
            }
        }

        // D: 3..9 — каст техники из назначенного слота (CultivationWindow → slot → technique).
        if (PlayerInput.TechniqueSlotIndex is int slotIdx and >= 3 and <= 9)
        {
            string? techId = TechniqueSlots?.GetTechniqueAtSlot(slotIdx);
            if (string.IsNullOrEmpty(techId))
            {
                ToastPub?.Publish(new Core.Messaging.Contracts.ToastShownEvent(
                    $"Слот {slotIdx}: пусто — назначьте технику в окне Культивации (K)", 2.0f));
            }
            else
            {
                // Публикуем TechniqueCastRequestedEvent — PlayerTechniqueCaster обработает
                // (валидация, зарядка, эффект). Position курсора — как в Z-касте.
                // AUDIT-0921 A3 (P1): МИРОВЫЕ координаты (GetGlobalMousePosition),
                // не экранные (GetViewport().GetMousePosition) — при смещённой
                // камере экранные координаты давали уехавший эпицентр AoE.
                var mousePos = GetGlobalMousePosition();
                int mx = (int)(mousePos.X * 1000);
                int my = (int)(mousePos.Y * 1000);
                TechniqueCastPub?.Publish(new Core.Messaging.Contracts.TechniqueCastRequestedEvent(
                    techId, mx, my));
            }
        }

        // E key: dialogue takes priority when open or an NPC is in range;
        // otherwise pick up the nearest ground item.
        // NPC_COMBAT_PREP Phase 2: E near NPC → open role dialogue (pause ticks).
        // Phase 5: при открытой лавке E не действует (торговля — модальность).
        // R13 FULL-LOOT: E рядом с трупом → окно обыска (труп ближе живого NPC
        // или NPC рядом нет); при открытом окне обыска E не действует (модальность).
        if (PlayerInput.IsInteractPressed)
        {
            if (_tradeWindow is { IsOpen: true })
            {
                // Лавка открыта — E съедается модальностью торговли.
            }
            else if (_lootWindow is { IsOpen: true })
            {
                // R13: окно обыска открыто — E съедается модальностью (взятие — ЛКМ/кнопка).
            }
            else if (_dialogueWindow != null && _dialogueWindow.IsOpen)
            {
                _dialogueWindow.Advance();
                if (_dialogueWindow is { IsOpen: false })
                    HandleModalResumeOnClose(); // INP-1: центральный резюм
            }
            else if (!HandleCorpseSearchOrNpcTalk())
            {
                HandlePickup();
            }
        }

        // Suppress game input when inventory OR trade window is open (Phase 5).
        // 2026-08-28: + Книга Техник, справка, чит-окно, окно Культивации.
        // FIX (аудит 2026-09-06): + диалог, лист персонажа (C), журнал событий (J),
        // журнал заданий (Q) — Space (атака) и Z (каст) работали во время
        // диалога/чтения, вопреки HOTKEYS.md §9.2 «Dialogue/Journal: Атака ❌».
        // Движение уже было заблокировано через Time.IsPaused (все эти окна
        // паузят время; Культивация K сознательно НЕ паузит — она уже была здесь).
        if (_inputAdapter != null && _inventoryWindow != null)
        {
            _inputAdapter.SetOverUI(_inventoryWindow.Visible
                || _tradeWindow is { IsOpen: true }
                || _lootWindow is { IsOpen: true }
                || _techniqueBook is { Visible: true }
                || _hotkeysWindow is { Visible: true }
                || _dialogueWindow is { IsOpen: true }
                || _characterSheetWindow is { Visible: true }
                || _eventLogWindow is { Visible: true }
                || _questWindow is { Visible: true }
#if DEBUG
                || _cheatPanel is { Visible: true }
#endif
                || _cultivationWindow is { Visible: true });
        }

        // Save/load DISABLED (Q8: user decision — saves invalid after each fix).
        // Will be re-enabled when save system is stable.
        // Аудит 2026-09-06 (P1, честность UI): тихий обман → явный тост.
        // Игрок жмёт F5/F9 и не понимает, почему ничего не происходит.
        if (PlayerInput.IsQuickSavePressed)
            ShowToast("⏳ Сейвы отключены (этап разработки)");
        if (PlayerInput.IsQuickLoadPressed)
            ShowToast("⏳ Сейвы отключены (этап разработки)");

        // Time speed control: PageUp = faster, PageDown = slower.
        // Debounce: max 1 change per real second (prevents rapid cycling).
        // Does NOT include Paused — pause is only via Esc.
        // Аудит-0915 A5 (INP2-1): аварийный резюм PageUp/PageDown гейтимся
        // модальными окнами — раньше они снимали паузу ПОД открытым окном,
        // нарушая инвариант INP-1 «пауза ⇔ стек модальных ∨ Esc игрока»
        // (движение оживает под планирующим окном).
        _speedChangeCooldown -= (float)GetPhysicsProcessDeltaTime();
        if (_speedChangeCooldown <= 0 && !AnyModalWindowOpen())
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
    /// 2026-09-04 S3: атрибуция атакующего — «💥 −12 HP — Разбойник»:
    /// раньше игрок не видел, КТО наносит урон (в замесе непонятно,
    /// кого бить в ответ).
    /// </summary>
    private void OnPlayerDamaged(in Core.Messaging.Contracts.DamageAppliedEvent e)
    {
        if (e.TargetId is not ("player_0" or "player")) return;
        // G0: поза получения урона (player_hit, one-shot 0.17с).
        _playerAnimator?.NotifyHit();
        string attacker = e.SourceId is ("player_0" or "player")
            ? ""
            : $" — {Npcs?.GetNPCState(e.SourceId)?.DisplayName ?? "?"}";
        ShowToast($"💥 −{e.Damage} HP{attacker}");
        // 2026-09-04 S5: если атакующий вне экрана — стрелка его направления.
        if (e.SourceId is not ("player_0" or "player"))
            ShowDamageDirection(e.SourceId);
    }

    // === 2026-09-04 S5: стрелка направления атакующего вне экрана ==========

    /// <summary>
    /// Показать/продлить стрелку направления атакующего (вызывается при уроне
    /// игроку). Стрелка — только если источник ВНЕ видимой области; угол — от
    /// игрока к источнику; точка — клэмп к краю экрана с отступом.
    /// public: headless-QA (GODOT_DAMAGEDIR_DEBUG) вызывает напрямую, не
    /// публикуя DamageAppliedEvent (его слушают 10+ боевых сервисов).
    /// </summary>
    public bool ShowDamageDirection(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId) || _dmgDirIndicator == null) return false;
        if (sourceId is "player_0" or "player") return false;
        var st = Npcs?.GetNPCState(sourceId);
        if (st == null || !st.IsAlive) return false;

        var world = TileToWorldCenter(st.Position);
        var (screen, angle, onScreen) = WorldToScreen(world);
        if (onScreen) return false;

        _dmgDirIndicator.ShowOrUpdate(sourceId, screen, angle);
        return true;
    }

    /// <summary>
    /// Тик стрелок (каждый физ-кадр): позы обновляются (NPC движется),
    /// стрелки источников, вернувшихся на экран / умерших — убираются.
    /// TTL НЕ продлевается (только новый урон через ShowDamageDirection).
    /// </summary>
    private void UpdateDamageDirIndicators()
    {
        if (_dmgDirIndicator == null || _dmgDirIndicator.ActiveCount == 0) return;
        foreach (var id in _dmgDirIndicator.GetActiveIds())
        {
            var st = Npcs?.GetNPCState(id);
            if (st == null || !st.IsAlive)
            {
                _dmgDirIndicator.FadeOut(id);
                continue;
            }
            var world = TileToWorldCenter(st.Position);
            var (screen, angle, onScreen) = WorldToScreen(world);
            if (onScreen)
                _dmgDirIndicator.FadeOut(id); // источник виден глазами — стрелка не нужна
            else
                _dmgDirIndicator.UpdatePose(id, screen, angle);
        }
    }

    /// <summary>Тайл → центр тайла в мировых пикселях.</summary>
    private static Vector2 TileToWorldCenter(Core.Data.Position2D tile) =>
        new(tile.X * GameConstants.TILE_PIXELS + GameConstants.TILE_PIXELS / 2f,
            tile.Y * GameConstants.TILE_PIXELS + GameConstants.TILE_PIXELS / 2f);

    /// <summary>
    /// Мир → экран: screen = (world − camera) × zoom + viewport/2 (Camera2D
    /// в центре экрана, оси экрана совпадают с миром). Возвращает точку,
    /// угол направления от игрока к цели (рад) и флаг «на экране».
    /// </summary>
    private (Vector2 screen, float angleRad, bool onScreen) WorldToScreen(Vector2 world)
    {
        var vp = GetViewport().GetVisibleRect().Size;
        var cam = _camera != null ? _camera.Position : _visualPosition;
        var zoom = _camera != null ? _camera.Zoom : Vector2.One;
        var screen = (world - cam) * zoom + vp / 2f;

        bool onScreen = screen.X >= DamageDirScreenMarginPx
                        && screen.X <= vp.X - DamageDirScreenMarginPx
                        && screen.Y >= DamageDirScreenMarginPx
                        && screen.Y <= vp.Y - DamageDirScreenMarginPx;

        var dir = world - _visualPosition;
        float angle = dir.LengthSquared() > 1f ? Mathf.Atan2(dir.Y, dir.X) : 0f;

        var clamped = new Vector2(
            Mathf.Clamp(screen.X, DamageDirScreenMarginPx, vp.X - DamageDirScreenMarginPx),
            Mathf.Clamp(screen.Y, DamageDirScreenMarginPx, vp.Y - DamageDirScreenMarginPx));
        return (clamped, angle, onScreen);
    }

    /// <summary>
    /// 2026-09-04 S3: kill-feed — смерть NPC (физический бой) → тост.
    /// Ранее NPCDeathEvent не потреблялся UI: убийство NPC не давало
    /// никакого отклика (только лут падал) — игрок не понимал, что убил.
    /// </summary>
    private void OnNpcDied(in Core.Messaging.Contracts.NPCDeathEvent e)
    {
        string name = Npcs?.GetNPCState(e.NpcId)?.DisplayName ?? "Существо";
        if (e.KillerId is ("player_0" or "player"))
            ShowToast($"☠ {name} повержен", 3.0f);
        else if (e.KillerId == "old_age")
            ShowToast($"✝ {name} ушёл из мира (старость)", 2.5f);
        // Смерть от другого NPC — без тоста (анти-спам npc-npc боя),
        // но в EventLogWindow пишется (см. OnNpcDeathFeed).
        GD.Print($"[GameWorld] NPC death: {e.NpcId} '{name}' by {e.KillerId}");
    }

    /// <summary>Этап 7: тост от модуля (QiStone use, чит-меню и т.д.).
    /// 2026-09-04 S2: передаём длительность из события (если задана).</summary>
    private void OnToastShown(in Core.Messaging.Contracts.ToastShownEvent e)
    {
        if (!string.IsNullOrEmpty(e.Message))
            ShowToast(e.Message, e.Duration > 0f ? e.Duration : 2.5f);
    }

    /// <summary>
    /// M2 (2026-09-03): отклонение атаки игрока → тост причины.
    /// C-5 аудита-3: CombatService публикует AttackRejectedEvent при попытке
    /// атаковать во время каста. Раньше игрок не понимал, почему удар не прошёл
    /// (тихий return). Спам невозможен: PlayerCombatAdapter теперь гейтит
    /// базовые атаки кулдауном §8.1 (1 интент/сек, не 60/сек).
    /// </summary>
    private void OnAttackRejected(in Core.Messaging.Contracts.AttackRejectedEvent e)
    {
        // Только атаки игрока — NPC-отклонения не тостим (шум).
        if (CultivationGame.Core.Helpers.PlayerIdResolver.IsPlayer(e.AttackerId))
            ShowToast($"✖ Атака отклонена: {e.Reason}");
    }

    /// <summary>
    /// Этап 4: смерть игрока → респавн через 3 секунды: полное лечение
    /// частей тела, Revive, телепорт в центр карты.
    /// </summary>
    private void OnPlayerDeath(in Core.Messaging.Contracts.PlayerDeathEvent e)
    {
        ShowToast($"☠ Вы погибли ({e.Cause}) — возрождение...");
        GD.Print($"[GameWorld] Player death: {e.Cause} — respawn in 3s");
        _playerAnimator?.NotifyDeath(); // G0: поза смерти (hold до респавна)
        CallDeferred(nameof(RespawnAfterDeath));
    }

    /// <summary>
    /// Аудит 2026-09-06 (P0): async void обёрнут в try/catch — исключение
    /// внутри респавна больше не роняет процесс без лога (async void —
    /// исключение уходит в unhandled-обработчик рантайма).
    /// </summary>
    private async void RespawnAfterDeath()
    {
        try
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
            _playerAnimator?.NotifyRespawn(); // G0: выход из death-hold

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
        catch (Exception ex)
        {
            // P0-фикс аудита: респавн не должен ронять игру молча.
            GD.PrintErr($"[GameWorld] RespawnAfterDeath FAILED: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            ShowToast("⚠ Ошибка возрождения — см. лог");
        }
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
        HandleModalResumeOnClose(); // INP-1: резюм только при опустевшем стеке
    }

    /// <summary>
    /// NPC_COMBAT_PREP Phase 4-5: лавка открыта — пауза тиков (торговля —
    /// планирующая активность, как инвентарь/диалог). Окно показывает себя
    /// по тому же событию (TradeWindow.OnTradeOpened).
    /// </summary>
    private void OnTradeOpened(in Core.Messaging.Contracts.TradeOpenedEvent e)
    {
        // INP-1 + аудит-0915 A3 (INP1-2): снапшот «мир был запаузен до окон»
        // снимается только если ПОД лавкой нет других модальных окон.
        // Аудит-0915 A3 (INP1-2): предикат — AnyModalWindowOpenExceptTrade()
        // (см. комментарий там): прежнее выражение всегда вычислялось в
        // false из-за порядка подписок — снапшот снимался всегда.
        bool otherModalOpen = AnyModalWindowOpenExceptTrade();
        HandleModalPauseOnOpen(otherModalOpen);
        GD.Print($"[GameWorld] Trade opened: {e.NpcId} — ticks paused");
    }

    /// <summary>
    /// Лавка закрыта — авторитетная точка резюма (как OnDialogueEnded):
    /// TradeClosedEvent стреляет из любого пути закрытия (Esc, конец
    /// отладочного сценария и т.д.).
    /// </summary>
    private void OnTradeClosed(in Core.Messaging.Contracts.TradeClosedEvent e)
    {
        HandleModalResumeOnClose(); // INP-1: резюм только при опустевшем стеке
        GD.Print("[GameWorld] Trade closed — ticks resumed");
    }

    /// <summary>
    /// R13 FULL-LOOT: труп удалён (обыскан дочиста / TTL / вручную) → если
    /// окно обыска открыто именно для этого трупа — закрыть (резюм тиков —
    /// в OnLootWindowClosed). Авторитетная точка закрытия (как TradeClosedEvent
    /// для лавки): окно может опустеть из ЛЮБОГО пути — кнопка «Забрать всё»,
    /// последний ЛКМ-взятый предмет, TTL в другом кадре.
    /// R13-audit (P1-1): сравнение — по CurrentCorpseId (имя узла-окна всегда
    /// "LootWindow" — старая проверка по Name НЕ срабатывала никогда).
    /// </summary>
    private void OnCorpseRemoved(in Core.Messaging.Contracts.CorpseRemovedEvent e)
    {
        if (_lootWindow is not { IsOpen: true }) return;
        if (_lootWindow.CurrentCorpseId != e.CorpseId) return;

        _lootWindow.Close();
        GD.Print($"[GameWorld] Corpse removed ({e.Reason}) — loot window closed, ticks resumed");
    }

    /// <summary>
    /// R13-audit (P1-2): LootWindow закрыт ЛЮБЫМ путём (Esc / кнопка «Уйти» /
    /// CorpseRemovedEvent — все проходят через Close()) → авторитетно снять
    /// паузу тиков, если она ставилась ради обыска (E-flow ставит паузу только
    /// при незапаузенной игре — тем же флагом, что и инвентарь).
    /// </summary>
    private void OnLootWindowClosed()
    {
        HandleModalResumeOnClose(); // INP-1: резюм только при опустевшем стеке
    }

    // === R15: оружие в руке игрока (WeaponVisualCatalog) ===

    /// <summary>Offset текущего hand-спрайта (зависит от класса оружия).</summary>
    private Vector2 _mainHandOffset = new(14f, -6f);

    /// <summary>R15: смена экипировки → обновить hand-спрайт (только игрок).</summary>
    private void OnEquipmentChanged(in Core.Messaging.Contracts.EquipmentChangedEvent e)
    {
        // Игрок публикует под ID "player" (InventoryModule.Initialize);
        // фильтр — алиасы игрока (B1: PlayerIdResolver).
        if (!Core.Helpers.PlayerIdResolver.IsPlayer(e.EntityId)
            && !string.IsNullOrEmpty(e.EntityId)) return;
        if (e.Slot is not (EquipmentSlot.WeaponMain or EquipmentSlot.WeaponOff)) return;
        RefreshMainHand();
    }

    /// <summary>
    /// R15: перечитать WeaponMain → текстура hand-спрайта / скрытие.
    /// Вызывается: EquipmentChangedEvent + страховка 0.5с в _PhysicsProcess.
    /// </summary>
    private void RefreshMainHand()
    {
        if (_playerMainHand == null) return;

        var weapon = Equipment?.GetEquipped(EquipmentSlot.WeaponMain);
        var visuals = WeaponVisualCatalog.Resolve(weapon);
        if (visuals == null)
        {
            _playerMainHand.Texture = null;
            _playerMainHand.Visible = false;
            _mainHandCacheItemId = null;
            _mainHandTextureKey = null;
            return;
        }

        string classId = WeaponVisualCatalog.WeaponClassOf(weapon!);
        _playerMainHand.Texture = visuals.Hand;
        _playerMainHand.Visible = true;
        _mainHandOffset = WeaponVisualCatalog.HandOffset(classId);
        _mainHandCacheItemId = weapon!.ItemId;
        _mainHandTextureKey = visuals.Key;
    }

    /// <summary>
    /// R16: замах оружия игрока — AttackIntentEvent (melee, attacker=player)
    /// запускает выпад MainHand к ЦЕЛИ (направление = визуальная позиция
    /// игрока → тайл NPC). Дальний бой не анимируем (натяжение лука —
    /// будущий этап). Выпад и слэш-дуга (StrikeFxRenderer) — синхронно с
    /// началом атаки (интент → замах ~0.4с → резолв урона).
    /// </summary>
    private void OnAttackIntentForSwing(in Core.Messaging.Contracts.AttackIntentEvent e)
    {
        // G0: поза атаки игрока — melee (0.42с, синх замаху) или bow (G4).
        if (Core.Helpers.PlayerIdResolver.IsPlayer(e.AttackerId))
        {
            if (e.IsRanged) _playerAnimator?.NotifyRangedAttack();
            else            _playerAnimator?.NotifyMeleeAttack();
        }

        if (e.IsRanged) return;
        if (!Core.Helpers.PlayerIdResolver.IsPlayer(e.AttackerId)) return;
        if (_playerMainHand == null || !PlayerIdResolverProxy()) return;

        var target = Npcs?.GetNPC(e.TargetId);
        if (target == null) return;

        float tile = GameConstants.TILE_PIXELS;
        var to = new Vector2(
            target.Position.X * tile + tile / 2f,
            target.Position.Y * tile + tile / 2f);
        var dir = to - _visualPosition;
        if (dir.LengthSquared() < 1f)
            dir = _facingLeft ? new Vector2(-1f, 0f) : new Vector2(1f, 0f);

        _mainHandSwingDir = dir.Normalized();
        _mainHandSwingAge = 0f;
    }

    /// <summary>Игрок заспавнен (позиция доступна для направления замаха).</summary>
    private bool PlayerIdResolverProxy() => _positionInitialized && Player != null;

    /// <summary>
    /// R16: тост стойки защиты игрока (клавиша G, цикл в PlayerCombatAdapter).
    /// </summary>
    private void OnDefenseIntent(in Core.Messaging.Contracts.DefenseIntentEvent e)
    {
        if (!Core.Helpers.PlayerIdResolver.IsPlayer(e.EntityId)) return;

        string stanceName = e.Defense switch
        {
            DefenseSubtype.Dodge => "уклонение",
            DefenseSubtype.Parry => "парирование",
            DefenseSubtype.Block => "блок",
            DefenseSubtype.Shield => "щит Ци",
            _ => "без защиты",
        };
        ShowToast($"🛡 Стойка: {stanceName}");
    }

    /// <summary>
    /// R13 FULL-LOOT: E рядом с трупом → окно обыска. Приоритет: труп ближе
    /// живого NPC (или живого рядом нет) → обыск; иначе — диалог (HandleNpcTalk).
    /// Мёртвый NPC не говорит, поэтому при равных дистанциях труп выигрывает.
    /// Возвращает true, если взаимодействие состоялось (E не падает в подбор).
    /// Пауза тиков — пока окно открыто (обыск = планирование, как диалоги).
    /// </summary>
    private bool HandleCorpseSearchOrNpcTalk()
    {
        if (Corpses == null || Player == null) return HandleNpcTalk();

        var playerPos = Player.Position;

        // Ближайший труп в радиусе разговора (Чебышёв по тайлам).
        var corpse = Corpses.FindNearestCorpse(playerPos, TalkRangeTiles);
        if (corpse == null) return HandleNpcTalk();

        int corpseDist = System.Math.Max(
            System.Math.Abs(corpse.Position.X - playerPos.X),
            System.Math.Abs(corpse.Position.Y - playerPos.Y));

        // Дистанция ближайшего ЖИВОГО NPC (как в HandleNpcTalk).
        int npcDist = int.MaxValue;
        var nearby = Npcs?.GetNearbyNPCIds(playerPos, TalkRangeTiles);
        if (nearby is { Count: > 0 })
        {
            foreach (var id in nearby)
            {
                var npc = Npcs!.GetNPC(id);
                if (npc == null || !Npcs.IsAlive(id)) continue;
                int dist = System.Math.Max(
                    System.Math.Abs(npc.Position.X - playerPos.X),
                    System.Math.Abs(npc.Position.Y - playerPos.Y));
                if (dist < npcDist) npcDist = dist;
            }
        }

        // Труп ближе (или равен — мёртвые не говорят, но обыск важнее) → обыск.
        if (corpseDist <= npcDist)
        {
            if (corpse.IsEmpty)
            {
                ShowToast($"☠ {corpse.DisplayName} — уже обобран");
                return true; // труп в радиусе — E не падает в подбор предметов
            }

            // INP-1: центральный хелпер (снапшот только при входе первого окна).
            HandleModalPauseOnOpen(AnyModalWindowOpen());
            _lootWindow.Open(corpse.CorpseId);
            return true;
        }

        return HandleNpcTalk();
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

        // Review этап 7 (P0-1): реальный E-путь взаимодействия публикует
        // NPCInteractedEvent (квесты TalkToNPC отслеживают разговор; роль
        // NPC попадает в событие через NPCService.OnNPCInteracted).
        if (Npcs is Modules.NPC.NPCService npcServiceImpl)
        {
            npcServiceImpl.OnNPCInteracted(best, Player?.PlayerId ?? "player", "talk");
        }

        // INP-1: центральный хелпер (снапшот только при входе первого окна).
        HandleModalPauseOnOpen(AnyModalWindowOpen());
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

    /// <summary>
    /// 2026-09-04 S2: показать тост в стеке (top-center). Новые сообщения
    /// НЕ затирают предыдущие: стек до MaxToastLines строк, повтор подряд
    /// агрегируется в «×N» с продлением показа.
    /// </summary>
    private void ShowToast(string message, float duration = 2.5f)
    {
        if (_toastStack == null || string.IsNullOrEmpty(message)) return;
        if (duration <= 0f) duration = 2.5f;

        // Агрегация: тот же текст в последней (нижней) строке → счётчик ×N.
        if (_toastLines.Count > 0 && _toastLines[^1].BaseText == message)
        {
            var last = _toastLines[^1];
            last.RepeatCount++;
            last.Remaining = duration;
            last.Total = duration;
            last.Label.Text = last.RepeatCount > 1 ? $"{message} ×{last.RepeatCount}" : message;
            return;
        }

        // Переполнение стека: убрать самую старую (верхнюю) строку.
        while (_toastLines.Count >= MaxToastLines)
        {
            var oldest = _toastLines[0];
            _toastStack.RemoveChild(oldest.Label);
            oldest.Label.QueueFree();
            _toastLines.RemoveAt(0);
        }

        var label = new Label
        {
            Text = message,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        label.AddThemeFontSizeOverride("font_size", 17);
        label.AddThemeColorOverride("font_color", new Color(0.98f, 0.85f, 0.3f));
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
        label.Modulate = new Color(1f, 1f, 1f, 0f); // появится через fade-in

        _toastStack.AddChild(label);
        _toastLines.Add(new ToastLine
        {
            Label = label,
            Remaining = duration,
            Total = duration,
            BaseText = message,
        });
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
    /// NPC_COMBAT_PREP Phase 5 → 2026-09-08 (ревью-1 P1-4): dispose ВСЕХ
    /// подписок EventBus при уходе сцены (правило «токены подписок диспозятся»
    /// — BeltSlotRow._ExitTree паттерн). Раньше диспозились только 2 торговых
    /// токена из 12: EventBus (процесс-глобальный, GameBoot-контейнер) держал
    /// callbacks уничтоженного controller-узла → повторные обработчики,
    /// обращения к освобождённым Godot-узлам, дублированные тосты, утечка
    /// удерживаемых объектов при возврате в меню/повторном входе в мир.
    /// </summary>
    public override void _ExitTree()
    {
        DisposeSubscriptionTokens();
    }

    /// <summary>
    /// 2026-09-08 (ревью-1 P1-4): единая точка диспоза всех токенов подписок
    /// GameWorldController (11 из _Ready + 1 lazy _attackRejectedToken).
    /// Порядок = порядку создания в _Ready; каждый токен обнуляется.
    /// </summary>
    private void DisposeSubscriptionTokens()
    {
        // Смерть/урон игрока (Этап 4 внедрения ЦИ).
        _playerDeathToken?.Dispose();
        _playerDeathToken = null;
        _playerDamageToken?.Dispose();
        _playerDamageToken = null;
        // Kill-feed (S3).
        _npcDeathToken?.Dispose();
        _npcDeathToken = null;
        // R13 FULL-LOOT: закрытие LootWindow по CorpseRemovedEvent.
        _corpseRemovedToken?.Dispose();
        _corpseRemovedToken = null;
        // Тосты от модулей (Этап 7).
        _toastShownToken?.Dispose();
        _toastShownToken = null;
        // Диалоги (Phase 2 fix).
        _dialogueEndedToken?.Dispose();
        _dialogueEndedToken = null;
        // Торговля (NPC_COMBAT_PREP Phase 4-5).
        _tradeOpenedToken?.Dispose();
        _tradeOpenedToken = null;
        _tradeClosedToken?.Dispose();
        _tradeClosedToken = null;
        // Медитация (Этап 1 внедрения ЦИ).
        _meditationStateToken?.Dispose();
        _meditationStateToken = null;
        // Результаты каста техник (Этап 2).
        _techniqueCastResultToken?.Dispose();
        _techniqueCastResultToken = null;
        // Формации (Этап 5).
        _formationStageToken?.Dispose();
        _formationStageToken = null;
        _formationActivatedToken2?.Dispose();
        _formationActivatedToken2 = null;
        // Lazy-подписка отклонений атак (первое нажатие Space, M2/C-5).
        _attackRejectedToken?.Dispose();
        _attackRejectedToken = null;
        // R15: экипировка → MainHand. R16: замах оружия + стойка защиты.
        _equipmentChangedToken?.Dispose();
        _equipmentChangedToken = null;
        _attackIntentToken?.Dispose();
        _attackIntentToken = null;
        _defenseIntentToken?.Dispose();
        _defenseIntentToken = null;
    }

    public Node2D WorldRoot => _worldRoot;
    public Camera2D Camera => _camera;
    public Sprite2D PlayerSprite => _playerSprite;
    public IReadOnlyList<Node> HudChildren => _hudCanvas?.GetChildren() as IReadOnlyList<Node> ?? Array.Empty<Node>();
}

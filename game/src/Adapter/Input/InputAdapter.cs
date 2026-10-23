#nullable enable
using System.Collections.Generic;
using Godot;
using GodotInput = Godot.Input;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Input;

/// <summary>
/// Adapter Layer 1 (Hardware → InputService).
/// Polls Godot Input every physics frame and converts raw input into an
/// engine-agnostic <see cref="InputFrameData"/> struct, then pushes it into
/// <see cref="IPlayerInputService"/> via <c>UpdateFrame</c>.
///
/// One-shot sticky flags (E, B, R, X, F, Esc, F5, F9, J, T, C, Q, M, N) are
/// collected into a <see cref="HashSet{T}"/> of canonical key names and passed
/// as <c>InputFrameData.StickyKeys</c>. The PlayerInputService implementation
/// derives its <c>IsXxxPressed</c> properties from that set; modules call
/// <c>ResetFrameFlags</c> at the end of the tick to clear them.
///
/// Godot 4.7 notes:
///  • Input polling (IsActionPressed / IsActionJustPressed) is still the
///    recommended approach for game input — event-based (_Input) is for UI.
///  • Mouse capture: if the game captures the mouse (Input.MouseMode = Captured),
///    GetMousePosition returns the center of the screen; use InputEventMouseMotion
///    for relative motion. We don't capture in v1, so viewport position is fine.
///  • Action events with "echo" flag (key repeat) are now filtered by default
///    in IsActionJustPressed — no change needed.
/// </summary>
public partial class InputAdapter : Node
{
    [Inject] private IPlayerInputService PlayerInput { get; set; } = null!;

    // Reusable sticky-key set (cleared each frame, no per-frame allocation).
    private readonly HashSet<string> _stickyKeys = new(16);

    private bool _isOverUI;
    private float _rmbHoldDuration;
    private long _frame;

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
        {
            ContainerAdapter.InjectProperties(this, container);
        }
        else
        {
            GD.PushWarning("[InputAdapter] GameBoot.Container is null — DI not wired.");
        }

        // 4.7: SetProcessInput is true by default, but we keep it explicit.
        SetProcessInput(true);
        // Ensure physics process runs (needed for _PhysicsProcess).
        SetPhysicsProcess(true);
    }

    public override void _PhysicsProcess(double delta)
    {
        _frame++;

        // ---- ST14-001 (Этап-14): владелец клавиатурного фокуса ----
        // Пока текстовое поле (LineEdit имени сейва и т.п.) держит фокус,
        // игровые действия от опроса НЕ запускаются: буквы должны печататься
        // в поле, а не открывать окна/медитацию/экипировку. Проверка —
        // ФАКТИЧЕСКИЙ фокус Godot (GuiGetFocusOwner), не положение мыши
        // (SetOverUI — про курсор; поле может держать фокус при уходе
        // курсора в мир).
        _textFocusBlocked = IsTextEditingFocusOwner(GetViewport().GuiGetFocusOwner());

        // ---- Continuous / held inputs (polling — correct for game movement) ----
        var move = Vector2.Zero;
        if (!_textFocusBlocked)
        {
            if (GodotInput.IsActionPressed("move_up"))    move.Y -= 1f;
            if (GodotInput.IsActionPressed("move_down"))  move.Y += 1f;
            if (GodotInput.IsActionPressed("move_left"))  move.X -= 1f;
            if (GodotInput.IsActionPressed("move_right")) move.X += 1f;
            if (move != Vector2.Zero) move = move.Normalized();
        }

        bool isRun = !_textFocusBlocked && GodotInput.IsActionPressed("run");
        bool isLmb = GodotInput.IsMouseButtonPressed(MouseButton.Left);
        bool isRmb = GodotInput.IsMouseButtonPressed(MouseButton.Right);

        // Track RMB hold duration for short-click vs long-press (> 300 ms = context menu).
        if (isRmb) _rmbHoldDuration += (float)delta;
        else       _rmbHoldDuration  = 0f;

        // Mouse position (viewport-space; GameWorldController converts to world via camera).
        var mousePos = GetViewport().GetMousePosition();

        // Hotbar slot selection (1..9). null when no digit pressed this frame.
        // D3 (2026-08-26): routing по Shift state:
        //   • Shift+1..9 → belt slot (пояс, расходники) — передаётся в HotbarSlot
        //     (существующий путь PlayerInputService.SelectedTechniqueSlot → BeltService.Use)
        //   • 1 (без Shift) → weapon_melee sticky (ближнее оружие)
        //   • 2 (без Shift) → weapon_ranged sticky (дальнее оружие)
        //   • 3..9 (без Shift) → technique_slot_N sticky (техника из слота N)
        //   • cultivation_window (K) → cultivation_window sticky (ниже, в sticky-блоке)
        bool shiftHeld = GodotInput.IsKeyPressed(Key.Shift);
        int? hotbarSlot = null;  // только belt slot (Shift+N) попадает сюда

        // ---- One-shot sticky keys (cleared and rebuilt each frame) ----
        _stickyKeys.Clear();
        if (_textFocusBlocked)
        {
            // ST14-001: фокус в текстовом поле — ВСЕ игровые sticky
            // заглушены (цифры 1-9/Shift — выделение текста; E/B/J/K/H/T/V —
            // символы; Esc-закрытие окна обрабатывает UI-слой).
            PushFrame(move: Vector2.Zero, isRun: false, isLmb, isRmb, hotbarSlot: null);
            return;
        }
        if (GodotInput.IsActionJustPressed("interact"))        _stickyKeys.Add("interact");
        if (GodotInput.IsActionJustPressed("inventory"))       _stickyKeys.Add("inventory");
        if (GodotInput.IsActionJustPressed("rest"))            _stickyKeys.Add("rest");
        if (GodotInput.IsActionJustPressed("harvest"))         _stickyKeys.Add("harvest");
        if (GodotInput.IsActionJustPressed("special_action"))  _stickyKeys.Add("special_action");
        if (GodotInput.IsActionJustPressed("pause"))           _stickyKeys.Add("escape");
        if (GodotInput.IsActionJustPressed("quicksave"))       _stickyKeys.Add("save");
        if (GodotInput.IsActionJustPressed("quickload"))       _stickyKeys.Add("load");
        if (GodotInput.IsActionJustPressed("journal"))         _stickyKeys.Add("journal");
        if (GodotInput.IsActionJustPressed("techniques"))      _stickyKeys.Add("techniques");
        if (GodotInput.IsActionJustPressed("character_sheet")) _stickyKeys.Add("character_sheet");
        if (GodotInput.IsActionJustPressed("quest_log"))       _stickyKeys.Add("quest_log");
        if (GodotInput.IsActionJustPressed("world_map"))       _stickyKeys.Add("world_map");
        if (GodotInput.IsActionJustPressed("minimap"))         _stickyKeys.Add("minimap");
        if (GodotInput.IsActionJustPressed("time_speed_up"))   _stickyKeys.Add("time_speed_up");
        if (GodotInput.IsActionJustPressed("time_speed_down")) _stickyKeys.Add("time_speed_down");
        // Этап 1-2 внедрения ЦИ: медитация (V) и каст техники (Z).
        if (GodotInput.IsActionJustPressed("meditate"))        _stickyKeys.Add("meditate");
        if (!_isOverUI && GodotInput.IsActionJustPressed("cast_technique"))
            _stickyKeys.Add("cast_technique");
        if (!_isOverUI && GodotInput.IsActionJustPressed("attack"))
            _stickyKeys.Add("attack");
        // R16: стойка защиты (G) — как attack: только не поверх UI.
        if (!_isOverUI && GodotInput.IsActionJustPressed("defend"))
            _stickyKeys.Add("defend");
        // R27: цикл выбора цели (Tab) — только не поверх UI.
        if (!_isOverUI && GodotInput.IsActionJustPressed("cycle_target"))
            _stickyKeys.Add("cycle_target");
        // Этап 7 внедрения ЦИ: чит-меню (F2, работает даже поверх UI) —
        // 2026-08-28: переехало с F1 на F2 (F1 — окно-справка).
        if (GodotInput.IsActionJustPressed("cheat_menu"))      _stickyKeys.Add("cheat_menu");

        // 2026-09-19 R18-1: F7 — тумблер индикации врагов (работает поверх UI,
        // как cheat_menu — настройка доступна в любом состоянии).
        if (GodotInput.IsActionJustPressed("toggle_enemy_vitals")) _stickyKeys.Add("toggle_enemy_vitals");

        // П6 (репорт 21.09): F3 — тумблер FPS-счётчика (работает поверх UI).
        if (GodotInput.IsActionJustPressed("toggle_fps_counter")) _stickyKeys.Add("toggle_fps_counter");

        // 2026-08-28: F1 — окно-справка горячих клавиш (работает даже поверх UI,
        // как cheat_menu — справка нужна и при открытом инвентаре).
        if (GodotInput.IsActionJustPressed("help_hotkeys"))    _stickyKeys.Add("help_hotkeys");

        // D3 (2026-08-26): Окно Культивации (K) — открывает CultivationWindow.
        // R41-7: вкладка «Техники» из окна удалена — браузер техник живёт
        // в Книге Техник (T); K-окно — только состояние практика + слот-бар.
        if (GodotInput.IsActionJustPressed("cultivation_window")) _stickyKeys.Add("cultivation_window");

        // R37-c (баг-репорт 23.09): окно зарядника Ци (H) — ChargerWindow
        // (слоты камней/буфер/тепло/режим; гейт по надетому заряднику).
        if (GodotInput.IsActionJustPressed("charger_window")) _stickyKeys.Add("charger_window");

        // D3 (2026-08-26): Hotbar routing — определяется Shift state (см. комментарий выше).
        // Цикл ищет нажатую цифру 1..9 и маршрутизирует:
        //   Shift+N → belt slot (попадает в hotbarSlot для BeltService);
        //   1 → weapon_melee; 2 → weapon_ranged; 3..9 → technique_slot_N.
        // AUDIT-0911 ADP-2 FIX: гейт !_isOverUI (симметрично attack/defend/
        // cast_technique выше) — раньше цифры 1-9 при открытом диалоге/окне
        // давали ДВОЙНОЕ действие: выбор реплики (DialogueWindow) + смена
        // режима боя/каст техники слота («2. Покажи товары» → тост
        // «Режим: дальний бой» поверх диалога).
        if (!_isOverUI)
        {
            for (int i = 1; i <= 9; i++)
            {
                if (GodotInput.IsActionJustPressed($"hotbar_{i}"))
                {
                    if (shiftHeld)
                    {
                        hotbarSlot = i;  // belt slot — пойдёт в InputFrameData.HotbarSlot
                    }
                    else if (i == 1)
                    {
                        _stickyKeys.Add("weapon_melee");
                    }
                    else if (i == 2)
                    {
                        _stickyKeys.Add("weapon_ranged");
                    }
                    else
                    {
                        _stickyKeys.Add($"technique_slot_{i}");
                    }
                    break;
                }
            }
        }

        // ---- Build the engine-agnostic InputFrameData (readonly struct) ----
        PushFrame(move, isRun, isLmb, isRmb, hotbarSlot);
    }

    /// <summary>Сборка+пуш InputFrameData (ST14: единая точка — обычный
    /// кадр, глушение текст-фокуса и сброс при потере фокуса приложения).</summary>
    private void PushFrame(Godot.Vector2 move, bool isRun, bool isLmb, bool isRmb, int? hotbarSlot)
    {
        var mousePos = GetViewport().GetMousePosition();
        var frameData = new InputFrameData(
            moveDirection:   new Vector2f(move.X, move.Y),
            isRun:           isRun,
            isLmbPressed:    isLmb,
            isRmbPressed:    isRmb,
            rmbHoldDuration: _rmbHoldDuration,
            mouseWorldPos:   new Vector2f(mousePos.X, mousePos.Y),
            isOverUI:        _isOverUI,
            hotbarSlot:      hotbarSlot,
            stickyKeys:      _stickyKeys,
            frame:           _frame
        );

        PlayerInput?.UpdateInputState(frameData);
    }

    private bool _textFocusBlocked;

    /// <summary>ST14-001: фокус принадлежит редактору текста?</summary>
    private static bool IsTextEditingFocusOwner(Godot.Control? c)
        => c is Godot.LineEdit or Godot.TextEdit or Godot.CodeEdit or Godot.SpinBox;

    /// <summary>ST14-001 QA: текущее состояние глушения (сим-ассерты).</summary>
    public bool QaTextFocusBlocked => _textFocusBlocked;

    // === ST14-006 (Этап-14): потеря фокуса приложения ===
    // Прежде при alt-tab/клике в другое окно held-состояния (WASD/Shift-бег,
    // ЛКМ/ПКМ, длительность ПКМ) оставались «залипшими» до возврата фокуса:
    // персонаж бежал, держалась стойка защиты, копился rmbHold. Обработка
    // отсутствовала ВООБЩЕ (0 переопределений _Notification в Adapter).
    // OS-уровень в headless не проверяется (физический плейтест PT-U06),
    // но контракт чист: уведомление → мгновенный нулевой кадр.
    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what == NotificationApplicationFocusOut)
        {
            OnApplicationFocusLost();
        }
    }

    /// <summary>Все игровые действия (ST14-006: на потере фокуса каждое
    /// отпускается ЯВНО — опрос следующего кадра вернёт «не нажато», иначе
    /// held-состояния пережили бы потерю фокуса до возврата).</summary>
    private static readonly string[] AllGameActions =
    {
        "move_up", "move_down", "move_left", "move_right", "run",
        "interact", "inventory", "rest", "harvest", "special_action",
        "pause", "quicksave", "quickload", "journal", "techniques",
        "character_sheet", "quest_log", "world_map", "minimap",
        "time_speed_up", "time_speed_down", "meditate", "cast_technique",
        "attack", "defend", "cycle_target", "cheat_menu",
        "toggle_enemy_vitals", "toggle_fps_counter", "help_hotkeys",
        "cultivation_window", "charger_window",
        "hotbar_1", "hotbar_2", "hotbar_3", "hotbar_4", "hotbar_5",
        "hotbar_6", "hotbar_7", "hotbar_8", "hotbar_9",
    };

    private void OnApplicationFocusLost()
    {
        // ST14-006: (1) явный release ВСЕХ игровых действий — иначе опрос
        // следующего физ-кадра снова увидит «нажатые» клавиши и held-стейт
        // переживёт потерю фокуса; (2) немедленный нулевой кадр — сервис
        // ввода не держит движение/бег/ПКМ-длительность ни одного тика.
        foreach (var action in AllGameActions)
            GodotInput.ActionRelease(action);
        _stickyKeys.Clear();
        _rmbHoldDuration = 0f;
        PushFrame(Godot.Vector2.Zero, isRun: false, isLmb: false, isRmb: false, hotbarSlot: null);
        GD.Print("[InputAdapter] ST14-006: потеря фокуса приложения — held-ввод и действия сброшены");
    }

    /// <summary>ST14-006 QA: симуляция потери фокуса (headless не получает
    /// OS-уведомлений — сим вызывает обработчик напрямую).</summary>
    public void QaSimulateFocusLoss() => OnApplicationFocusLost();

    /// <summary>
    /// Event-based input — used for UI-only events (mouse enter/exit on Control nodes).
    /// Game input is handled in _PhysicsProcess via polling (more reliable for movement).
    /// </summary>
    public override void _Input(InputEvent @event)
    {
        // 4.7: InputEventEcho is filtered automatically by IsActionJustPressed,
        // so we don't need to handle echo here.
        if (@event is InputEventMouseMotion mm)
        {
            // Could track mouse velocity for gesture detection (future).
        }
    }

    /// <summary>
    /// Called by UI panels via signal when the cursor enters a Control node,
    /// so movement/attack input is suppressed while interacting with UI.
    /// </summary>
    public void SetOverUI(bool value)
    {
        _isOverUI = value;
    }
}

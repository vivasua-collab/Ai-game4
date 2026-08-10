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

        // ---- Continuous / held inputs (polling — correct for game movement) ----
        var move = Vector2.Zero;
        if (GodotInput.IsActionPressed("move_up"))    move.Y -= 1f;
        if (GodotInput.IsActionPressed("move_down"))  move.Y += 1f;
        if (GodotInput.IsActionPressed("move_left"))  move.X -= 1f;
        if (GodotInput.IsActionPressed("move_right")) move.X += 1f;
        if (move != Vector2.Zero) move = move.Normalized();

        bool isRun = GodotInput.IsActionPressed("run");
        bool isLmb = GodotInput.IsMouseButtonPressed(MouseButton.Left);
        bool isRmb = GodotInput.IsMouseButtonPressed(MouseButton.Right);

        // Track RMB hold duration for short-click vs long-press (> 300 ms = context menu).
        if (isRmb) _rmbHoldDuration += (float)delta;
        else       _rmbHoldDuration  = 0f;

        // Mouse position (viewport-space; GameWorldController converts to world via camera).
        var mousePos = GetViewport().GetMousePosition();

        // Hotbar slot selection (1..9). null when no digit pressed this frame.
        int? hotbarSlot = null;
        for (int i = 1; i <= 9; i++)
        {
            if (GodotInput.IsActionJustPressed($"hotbar_{i}"))
            {
                hotbarSlot = i;
                break;
            }
        }

        // ---- One-shot sticky keys (cleared and rebuilt each frame) ----
        _stickyKeys.Clear();
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
        if (!_isOverUI && GodotInput.IsActionJustPressed("attack"))
            _stickyKeys.Add("attack");

        // ---- Build the engine-agnostic InputFrameData (readonly struct) ----
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

        PlayerInput?.UpdateFrame(frameData);
    }

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

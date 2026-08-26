#nullable enable
using Godot;

namespace CultivationGame.Adapter.Input;

/// <summary>
/// Registers all input actions programmatically at startup.
/// This avoids the Object(InputEventKey,...) syntax in project.godot which
/// breaks C# assembly loading in headless mode.
///
/// Action names and key mappings per docs_v2/07_ui/HOTKEYS.md.
/// </summary>
public static class InputMapInitializer
{
    private static bool _initialized;

    public static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        // Movement (WASD + arrows). Use physical keycodes for layout-independent input.
        AddPhysicalKeyAction("move_up", Key.W);
        AddPhysicalKeyAction("move_up", Key.Up);
        AddPhysicalKeyAction("move_down", Key.S);
        AddPhysicalKeyAction("move_down", Key.Down);
        AddPhysicalKeyAction("move_left", Key.A);
        AddPhysicalKeyAction("move_left", Key.Left);
        AddPhysicalKeyAction("move_right", Key.D);
        AddPhysicalKeyAction("move_right", Key.Right);

        // Run (Shift)
        AddPhysicalKeyAction("run", Key.Shift);

        // Interact (E)
        AddKeyAction("interact", Key.E);

        // Inventory (B)
        AddKeyAction("inventory", Key.B);

        // Rest / Meditation (R)
        AddKeyAction("rest", Key.R);

        // Harvest / Tool (F)
        AddKeyAction("harvest", Key.F);

        // Special action (X) — Этап 2 ЦИ: цикл выбора техники.
        AddKeyAction("special_action", Key.X);

        // Meditate (V) — M is taken by world_map, so V is used for meditate.
        AddKeyAction("meditate", Key.V);

        // Этап 2 внедрения ЦИ: каст выбранной техники (Z).
        AddKeyAction("cast_technique", Key.Z);

        // Pause / Esc
        AddKeyAction("pause", Key.Escape);

        // Quick save / load (F5 / F9)
        AddKeyAction("quicksave", Key.F5);
        AddKeyAction("quickload", Key.F9);

        // Journal (J)
        AddKeyAction("journal", Key.J);

        // Techniques (T)
        AddKeyAction("techniques", Key.T);

        // Character sheet (C)
        AddKeyAction("character_sheet", Key.C);

        // Quest log (Q)
        AddKeyAction("quest_log", Key.Q);

        // World map (M)
        AddKeyAction("world_map", Key.M);

        // Minimap (N)
        AddKeyAction("minimap", Key.N);

        // Attack (Space)
        AddKeyAction("attack", Key.Space);

        // Mouse click action — Left Mouse Button for movement/interaction.
        AddMouseButtonAction("mouse_click", MouseButton.Left);

        // Hotbar slots (1-9)
        for (int i = 1; i <= 9; i++)
        {
            AddKeyAction($"hotbar_{i}", Key.Key0 + i);
        }

        // Input log toggle (backtick / F1)
        AddKeyAction("input_log", Key.Quoteleft);
        AddKeyAction("input_log", Key.F1);

        // Этап 7 внедрения ЦИ: чит-меню разработки (F1, #if DEBUG только в CheatPanel).
        // F1 уже связан с input_log, но input_log нигде не потребляется —
        // оба action'а получают событие, но только cheat_menu используется.
        AddKeyAction("cheat_menu", Key.F1);

        // Time speed control: Page Up = faster, Page Down = slower
        AddPhysicalKeyAction("time_speed_up", Key.Pageup);
        AddPhysicalKeyAction("time_speed_down", Key.Pagedown);

        GD.Print("[InputMap] Registered all input actions.");
    }

    /// <summary>
    /// Register an action with a logical key (respects keyboard layout).
    /// Use for letter/number keys where the label matters (E for interact, B for inventory).
    /// </summary>
    private static void AddKeyAction(string actionName, Key key)
    {
        if (!InputMap.HasAction(actionName))
        {
            InputMap.AddAction(actionName);
        }

        var eventKey = new InputEventKey
        {
            Keycode = key,
            // 4.7: also set PhysicalKeycode as fallback for layout-independent matching.
            PhysicalKeycode = key,
        };
        InputMap.ActionAddEvent(actionName, eventKey);
    }

    /// <summary>
    /// Register an action with a physical key (layout-independent).
    /// Use for movement keys (WASD, arrows, Shift) where physical position matters.
    /// </summary>
    private static void AddPhysicalKeyAction(string actionName, Key key)
    {
        if (!InputMap.HasAction(actionName))
        {
            InputMap.AddAction(actionName);
        }

        var eventKey = new InputEventKey
        {
            PhysicalKeycode = key,
        };
        InputMap.ActionAddEvent(actionName, eventKey);
    }

    /// <summary>
    /// Register an action with a mouse button.
    /// Use for LMB/RMB/Middle clicks.
    /// </summary>
    private static void AddMouseButtonAction(string actionName, MouseButton button)
    {
        if (!InputMap.HasAction(actionName))
        {
            InputMap.AddAction(actionName);
        }

        var eventMouse = new InputEventMouseButton
        {
            ButtonIndex = button,
        };
        InputMap.ActionAddEvent(actionName, eventMouse);
    }
}

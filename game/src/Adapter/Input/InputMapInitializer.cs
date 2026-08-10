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

        // Movement (WASD + arrows)
        AddKeyAction("move_up", Key.W);
        AddKeyAction("move_up", Key.Up);
        AddKeyAction("move_down", Key.S);
        AddKeyAction("move_down", Key.Down);
        AddKeyAction("move_left", Key.A);
        AddKeyAction("move_left", Key.Left);
        AddKeyAction("move_right", Key.D);
        AddKeyAction("move_right", Key.Right);

        // Run (Shift)
        AddKeyAction("run", Key.Shift);

        // Interact (E)
        AddKeyAction("interact", Key.E);

        // Inventory (B)
        AddKeyAction("inventory", Key.B);

        // Rest / Meditation (R)
        AddKeyAction("rest", Key.R);

        // Harvest / Tool (F)
        AddKeyAction("harvest", Key.F);

        // Special action (X)
        AddKeyAction("special_action", Key.X);

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

        // Hotbar slots (1-9)
        for (int i = 1; i <= 9; i++)
        {
            AddKeyAction($"hotbar_{i}", Key.Key0 + i);
        }

        // Input log toggle (backtick / F1)
        AddKeyAction("input_log", Key.Quoteleft);
        AddKeyAction("input_log", Key.F1);

        GD.Print("[InputMap] Registered all input actions.");
    }

    private static void AddKeyAction(string actionName, Key key)
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
}

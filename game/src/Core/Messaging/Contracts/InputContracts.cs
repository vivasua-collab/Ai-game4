#nullable enable
namespace CultivationGame.Core.Messaging.Contracts;

/// <summary>Raw keyboard event (for diagnostic input log).</summary>
public readonly struct InputKeyEvent
{
    public readonly string KeyName;
    public readonly bool IsPressed;
    public InputKeyEvent(string keyName, bool isPressed)
    {
        KeyName = keyName; IsPressed = isPressed;
    }
}

/// <summary>Resolved logical action (e.g. "Interact", "QuickSave").</summary>
public readonly struct InputActionEvent
{
    public readonly string ActionName;
    public readonly bool IsPressed;
    public InputActionEvent(string actionName, bool isPressed)
    {
        ActionName = actionName; IsPressed = isPressed;
    }
}

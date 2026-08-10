#nullable enable
namespace CultivationGame.Core.Messaging.Contracts;

public readonly struct ViewShownEvent
{
    public readonly string ViewId;
    public ViewShownEvent(string viewId) { ViewId = viewId; }
}

public readonly struct ViewHiddenEvent
{
    public readonly string ViewId;
    public ViewHiddenEvent(string viewId) { ViewId = viewId; }
}

public readonly struct NotificationShownEvent
{
    public readonly string Message;
    public readonly float Duration;
    public NotificationShownEvent(string message, float duration)
    {
        Message = message; Duration = duration;
    }
}

public readonly struct TooltipRequestedEvent
{
    public readonly string Text;
    public readonly float X;
    public readonly float Y;
    public TooltipRequestedEvent(string text, float x, float y)
    {
        Text = text; X = x; Y = y;
    }
}

public readonly struct TooltipHiddenEvent
{
    // No payload — single global tooltip for v1.
}

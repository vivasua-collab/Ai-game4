#nullable enable
using System;
using System.Collections.Generic;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.UI;

/// <summary>
/// UIService — pure-C# view state (which views are visible, queued toasts,
/// active tooltip). Actual view instantiation is adapter-layer (Godot Control
/// tree). V1 stub: stores state in a HashSet + Queue.
/// </summary>
public sealed class UIService : IUIService
{
    private readonly HashSet<string> _visible = new();
    private readonly Queue<(string message, float duration)> _notifications = new();
    private string? _tooltipText;
    private float _tooltipX, _tooltipY;
    private readonly UIConfig _config;

    public UIService(UIConfig? config = null) => _config = config ?? new UIConfig();

    public void ShowView(string viewId)
    {
        if (string.IsNullOrEmpty(viewId)) return;
        if (_visible.Add(viewId))
            Console.WriteLine($"[UIService] Show view '{viewId}'");
    }

    public void HideView(string viewId)
    {
        if (_visible.Remove(viewId))
            Console.WriteLine($"[UIService] Hide view '{viewId}'");
    }

    public void HideAllViews()
    {
        _visible.Clear();
        Console.WriteLine("[UIService] Hid all views");
    }

    public bool IsViewVisible(string viewId) => _visible.Contains(viewId);

    public void ShowNotification(string message, float duration = 3f)
    {
        if (_notifications.Count >= _config.MaxQueuedNotifications)
        {
            _notifications.Dequeue();
        }
        _notifications.Enqueue((message, duration > 0 ? duration : _config.DefaultNotificationDuration));
        Console.WriteLine($"[UIService] Notification queued: {message}");
    }

    public void ShowTooltip(string text, float x, float y)
    {
        _tooltipText = text;
        _tooltipX = x;
        _tooltipY = y;
    }

    public void HideTooltip()
    {
        _tooltipText = null;
    }

    /// <summary>Internal — peek at queued notification count. Not on interface.</summary>
    public int QueuedNotificationCount => _notifications.Count;
}

#nullable enable
// Создано: 2026-05-09 — Phase 14: сервис уведомлений (Toast)
// Migrated from Ai-game3 (Unity) to Ai-game4 (Godot) 2026-08-15.
//
// Очередь уведомлений с TTL.
using System;
using System.Collections.Generic;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.UI;

/// <summary>
/// Сервис уведомлений (Toast).
/// Управляет очередью коротких уведомлений.
///
/// Подписки:
/// - ToastShownEvent → добавить в очередь
///
/// Уведомления отображаются последовательно с задержкой.
/// </summary>
public sealed class ToastService : IDisposable
{
    // === EventBus: подписки ===
    [Inject] private readonly ISubscriber<ToastShownEvent> _toastShownSub = null!;

    // === Конфигурация ===
    private UIConfig _config = new();

    // === Очередь уведомлений ===
    private readonly Queue<ToastEntry> _queue = new();

    /// <summary>Текущие активные уведомления</summary>
    private readonly List<ToastEntry> _active = new();

    /// <summary>Текущие активные уведомления (readonly)</summary>
    public IReadOnlyList<ToastEntry> ActiveToasts => _active;

    private IDisposable? _toastSubscription;

    /// <summary>
    /// Инициализация с конфигурацией.
    /// </summary>
    public void Initialize(UIConfig config)
    {
        _config = config ?? new UIConfig();
        _toastSubscription?.Dispose();
        _toastSubscription = _toastShownSub.Subscribe(OnToastShown);
    }

    /// <summary>
    /// Обработка тика (обновление TTL активных уведомлений).
    /// </summary>
    public void Tick(float deltaTime)
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            _active[i].RemainingTime -= deltaTime;
            if (_active[i].RemainingTime <= 0)
            {
                _active.RemoveAt(i);
            }
        }

        int maxActive = _config.MaxToastCount;
        while (_queue.Count > 0 && _active.Count < maxActive)
        {
            var entry = _queue.Dequeue();
            _active.Add(entry);
        }
    }

    private void OnToastShown(in ToastShownEvent e)
    {
        _queue.Enqueue(new ToastEntry(e.Message, e.Duration));
    }

    public void Dispose()
    {
        _toastSubscription?.Dispose();
        _toastSubscription = null;
        _queue.Clear();
        _active.Clear();
    }
}

/// <summary>
/// Запись уведомления (message + TTL)
/// </summary>
public sealed class ToastEntry
{
    public readonly string Message;
    public float RemainingTime;

    public ToastEntry(string message, float duration)
    {
        Message = message;
        RemainingTime = duration;
    }
}

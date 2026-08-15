#nullable enable
// Создано: 2026-05-19 — Input Logging System: реализация IInputLogService
// Migrated from Ai-game3 (Unity) to Ai-game4 (Godot) 2026-08-15.
//
// Кольцевой буфер записей о нажатиях клавиш и действиях.
// Подписывается на InputKeyEvent/InputActionEvent через EventBus.
using System;
using System.Collections.Generic;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.UI;

/// <summary>
/// Реализация IInputLogService.
/// Кольцевой буфер записей о нажатиях клавиш и результирующих действиях.
/// </summary>
public sealed class InputLogService : IInputLogService, IDisposable
{
    private const int DefaultCapacity = 200;

    private readonly List<InputLogEntry> _entries = new(DefaultCapacity);
    private readonly int _capacity = DefaultCapacity;

    // === EventBus: подписки ===
    [Inject] private readonly ISubscriber<InputKeyEvent> _keyEventSub = null!;
    [Inject] private readonly ISubscriber<InputActionEvent> _actionEventSub = null!;
    private IDisposable? _keySubscription;
    private IDisposable? _actionSubscription;

    private bool _isEnabled = true;
    private float _elapsedTime;

    public IReadOnlyList<InputLogEntry> Entries => _entries;
    public int Count => _entries.Count;
    public int Capacity => _capacity;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }

    /// <summary>
    /// Инициализация: подписка на события ввода.
    /// </summary>
    public void Initialize()
    {
        _keySubscription?.Dispose();
        _actionSubscription?.Dispose();
        _keySubscription = _keyEventSub.Subscribe(OnInputKeyEvent);
        _actionSubscription = _actionEventSub.Subscribe(OnInputActionEvent);
    }

    /// <summary>Продвинуть внутренний таймер (вызывается из UIModule.Tick).</summary>
    public void Tick(float deltaTime) => _elapsedTime += deltaTime;

    // === IInputLogService ===

    public void LogKey(string keyName, string description, int frame, float time)
    {
        if (!_isEnabled) return;
        AddEntry(new InputLogEntry
        {
            Type = InputLogEntryType.Key,
            Name = keyName,
            Description = description,
            Frame = frame,
            Time = time
        });
    }

    public void LogAction(string actionName, string description, int frame, float time)
    {
        if (!_isEnabled) return;
        AddEntry(new InputLogEntry
        {
            Type = InputLogEntryType.Action,
            Name = actionName,
            Description = description,
            Frame = frame,
            Time = time
        });
    }

    public void Clear() => _entries.Clear();

    // === Обработчики событий ===

    private void OnInputKeyEvent(in InputKeyEvent e)
    {
        if (!_isEnabled) return;
        string desc = string.IsNullOrEmpty(e.Direction)
            ? e.EventType.ToString()
            : $"{e.EventType} dir={e.Direction}";
        LogKey(e.KeyName, desc, e.Frame, _elapsedTime);
    }

    private void OnInputActionEvent(in InputActionEvent e)
    {
        if (!_isEnabled) return;
        LogAction(e.ActionName, e.Description, e.Frame, _elapsedTime);
    }

    // === Внутренние методы ===

    private void AddEntry(InputLogEntry entry)
    {
        if (_entries.Count >= _capacity)
        {
            _entries.RemoveAt(0);
        }
        _entries.Add(entry);
    }

    public void Dispose()
    {
        _keySubscription?.Dispose();
        _actionSubscription?.Dispose();
        _keySubscription = null;
        _actionSubscription = null;
    }
}

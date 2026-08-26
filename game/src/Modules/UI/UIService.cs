#nullable enable
// Создано: 2026-05-09 — Phase 14: реализация IUIService
// Migrated from Ai-game3 (Unity) to Ai-game4 (Godot) 2026-08-15.
//
// Управление состоянием UI, уведомления, модальные окна, активные виды.
// EVT-01: НЕ инжектит сервисы других модулей — только EventBus.
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.UI;

/// <summary>
/// Реализация IUIService.
/// Управляет состоянием UI (какие экраны открыты), очередью уведомлений и модалок.
///
/// АРХИТЕКТУРА (EVT-01): Модуль UI НЕ инжектит сервисы других модулей.
/// Все кросс-модульные данные — через EventBus:
/// - GameStateChangedEvent → подписка (обновление состояния)
/// - UIStateChangeRequestEvent → подписка (запрос смены экрана)
/// - ToastShownEvent, ModalShownEvent → публикация
/// </summary>
public sealed class UIService : IUIService, IDisposable
{
    // === EventBus: паблишеры ===
    [Inject] private readonly IPublisher<ToastShownEvent> _toastShownPub = null!;
    [Inject] private readonly IPublisher<ModalShownEvent> _modalShownPub = null!;
    [Inject] private readonly IPublisher<GameStateChangedEvent> _gameStateChangedPub = null!;
    [Inject] private readonly IPublisher<GamePausedEvent> _gamePausedPub = null!;
    [Inject] private readonly IPublisher<GameResumedEvent> _gameResumedPub = null!;

    // === EventBus: подписки ===
    [Inject] private readonly ISubscriber<GameStateChangedEvent> _gameStateChangedSub = null!;
    [Inject] private readonly ISubscriber<UIStateChangeRequestEvent> _uiStateChangeRequestSub = null!;
    [Inject] private readonly ISubscriber<UIPauseRequestEvent> _uiPauseRequestSub = null!;
    [Inject] private readonly ISubscriber<UIResumeRequestEvent> _uiResumeRequestSub = null!;

    private IDisposable? _gameStateSubscription;
    private IDisposable? _uiStateChangeSubscription;
    private IDisposable? _pauseSubscription;
    private IDisposable? _resumeSubscription;

    // === Конфигурация ===
    private UIConfig _config = new();

    // === Состояние ===
    private GameState _currentState = GameState.Playing;
    private GameState _previousState = GameState.None;
    private readonly HashSet<string> _visibleViews = new();
    private readonly Queue<(string message, float duration)> _notifications = new();
    private string? _modalTitle;
    private string? _modalMessage;

    /// <summary>
    /// Инициализация с конфигурацией и подписками.
    /// </summary>
    public void Initialize(UIConfig config)
    {
        _config = config ?? new UIConfig();

        _gameStateSubscription?.Dispose();
        _uiStateChangeSubscription?.Dispose();
        _pauseSubscription?.Dispose();
        _resumeSubscription?.Dispose();

        _gameStateSubscription = _gameStateChangedSub.Subscribe(OnGameStateChanged);
        _uiStateChangeSubscription = _uiStateChangeRequestSub.Subscribe(OnUIStateChangeRequest);
        _pauseSubscription = _uiPauseRequestSub.Subscribe(OnPauseRequest);
        _resumeSubscription = _uiResumeRequestSub.Subscribe(OnResumeRequest);
    }

    // === IUIService ===

    public GameState CurrentUIState => _currentState;

    public void SetUIState(GameState state)
    {
        if (_currentState == state) return;
        var oldState = _currentState;
        _previousState = oldState;
        _currentState = state;
        _gameStateChangedPub.Publish(new GameStateChangedEvent(oldState, state));
    }

    public void ShowToast(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        float duration = _config.DefaultToastDuration;
        _notifications.Enqueue((message, duration));
        _toastShownPub.Publish(new ToastShownEvent(message, duration));
    }

    public void ShowModal(string title, string message)
    {
        _modalTitle = title;
        _modalMessage = message;
        _modalShownPub.Publish(new ModalShownEvent(title, message));
    }

    public void ShowView(string viewId)
    {
        if (string.IsNullOrEmpty(viewId)) return;
        _visibleViews.Add(viewId);
    }

    public void HideView(string viewId)
    {
        if (string.IsNullOrEmpty(viewId)) return;
        _visibleViews.Remove(viewId);
    }

    public void HideAllViews() => _visibleViews.Clear();

    public bool IsViewVisible(string viewId) => _visibleViews.Contains(viewId);

    // === Дополнительные методы ===

    /// <summary>Вернуться к предыдущему экрану.</summary>
    public void GoBack()
    {
        if (_previousState != GameState.None)
            SetUIState(_previousState);
        else
            SetUIState(GameState.Playing);
    }

    /// <summary>Текущий заголовок модалки (или null).</summary>
    public string? ModalTitle => _modalTitle;

    /// <summary>Текущее сообщение модалки (или null).</summary>
    public string? ModalMessage => _modalMessage;

    /// <summary>Количество уведомлений в очереди.</summary>
    public int QueuedNotificationCount => _notifications.Count;

    // === Обработчики событий ===

    private void OnGameStateChanged(in GameStateChangedEvent e)
    {
        if (_currentState != e.NewState)
        {
            _previousState = e.OldState;
            _currentState = e.NewState;
        }
    }

    private void OnUIStateChangeRequest(in UIStateChangeRequestEvent e)
        => SetUIState(e.TargetState);

    private void OnPauseRequest(in UIPauseRequestEvent e)
    {
        SetUIState(GameState.Paused);
        _gamePausedPub.Publish(new GamePausedEvent());
    }

    private void OnResumeRequest(in UIResumeRequestEvent e)
    {
        SetUIState(GameState.Playing);
        _gameResumedPub.Publish(new GameResumedEvent());
    }

    public void Dispose()
    {
        _gameStateSubscription?.Dispose();
        _uiStateChangeSubscription?.Dispose();
        _pauseSubscription?.Dispose();
        _resumeSubscription?.Dispose();

        _gameStateSubscription = null;
        _uiStateChangeSubscription = null;
        _pauseSubscription = null;
        _resumeSubscription = null;
    }
}

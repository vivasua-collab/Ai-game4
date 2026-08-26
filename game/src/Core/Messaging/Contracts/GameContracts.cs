#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Messaging.Contracts;

// Created: 2026-05-08 (Ai-game3) — migrated 2026-08-15.
// Game session lifecycle: state changes, pause/resume, new/load/quit requests.

// GameState is defined in CultivationGame.Core.Data (canonical).

public readonly struct GameStateChangedEvent
{
    public readonly GameState OldState;
    public readonly GameState NewState;
    public GameStateChangedEvent(GameState oldState, GameState newState)
        { OldState = oldState; NewState = newState; }
}

public readonly struct GamePausedEvent { }

public readonly struct GameResumedEvent { }

/// <summary>
/// Событие: сессия запущена (после сборки сцены).
/// </summary>
public readonly struct SessionStartedEvent
{
    /// <summary>Это новая игра (true) или загрузка (false)</summary>
    public readonly bool IsNewGame;
    public SessionStartedEvent(bool isNewGame) { IsNewGame = isNewGame; }
}

/// <summary>
/// Событие: запрошена новая игра.
/// GameSession подписан и запускает StartNewGame().
/// </summary>
public readonly struct NewGameRequestedEvent { }

/// <summary>
/// Событие: запрошена загрузка игры.
/// GameSession подписан и запускает LoadGame().
/// </summary>
public readonly struct LoadGameRequestedEvent
{
    public readonly SaveSlot Slot;
    public LoadGameRequestedEvent(SaveSlot slot) { Slot = slot; }
}

/// <summary>
/// Событие: запрошен выход из игры.
/// </summary>
public readonly struct QuitGameRequestedEvent
{
    /// <summary>Сохранить перед выходом?</summary>
    public readonly bool SaveBeforeQuit;
    public QuitGameRequestedEvent(bool saveBeforeQuit) { SaveBeforeQuit = saveBeforeQuit; }
}

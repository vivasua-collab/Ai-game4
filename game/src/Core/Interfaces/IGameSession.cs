#nullable enable
using System;
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Interfaces;

/// <summary>Lifecycle states for a single play session.</summary>
public enum SessionState
{
    MainMenu,
    Loading,
    Playing,
    Paused,
    Saving,
    Quitting,
}

/// <summary>
/// Top-level session manager. Owns the <see cref="GameSessionData"/> and
/// transitions between states via <c>NewGame / LoadGame / Pause / ...</c>.
/// </summary>
public interface IGameSession
{
    SessionState State { get; }
    GameSessionData Data { get; }

    void NewGame(int startVariant);
    void LoadGame(string slotName);
    void Pause();
    void Resume();
    void SaveAndQuit();
    void QuitWithoutSaving();

    event Action<SessionState>? OnStateChanged;
}

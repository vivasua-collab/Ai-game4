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
/// Этап-4 (P1, 20.10.2026): результат последнего LoadGame — для пост-фактум
/// маршрутизации UI (GWC/MainMenu): «Reject» = мир НЕ трогали (сцена жива),
/// «RolledBack» = неудачная загрузка ОТКАЧЕНА, предыдущий мир восстановлен.
/// </summary>
public enum LoadGameOutcome
{
    /// <summary>LoadGame ещё не вызывался в этом процессе.</summary>
    None,
    /// <summary>Мир восстановлен из сейва, сессия Playing.</summary>
    Success,
    /// <summary>Pre-flight отказ: слот повреждён/неполон/неподдерживаем.
    /// СБРОСА НЕ БЫЛО — текущий мир цел, сессия в прежнем состоянии.</summary>
    RejectedByPrecheck,
    /// <summary>Загрузка упала ПОСЛЕ сброса, но транзакция откатилась:
    /// домены пересброшены + восстановлены из снимка, сессия Playing.</summary>
    RolledBack,
    /// <summary>Fail-closed: мир сброшен, откат невозможен/провален —
    /// сессия в MainMenu (или осталась в меню, если загрузка шла из меню).</summary>
    Failed,
}

/// <summary>
/// Top-level session manager. Owns the <see cref="GameSessionData"/> and
/// transitions between states via <c>NewGame / LoadGame / Pause / ...</c>.
/// </summary>
public interface IGameSession
{
    SessionState State { get; }
    GameSessionData Data { get; }

    /// <summary>Этап-4: исход последнего LoadGame (маршрутизация UI).</summary>
    LoadGameOutcome LastLoadOutcome { get; }

    void NewGame(int startVariant);
    /// <summary>Start a new game with a specific location (e.g. test_polygon, large_world).</summary>
    void NewGame(int startVariant, string locationId);
    /// <summary>Load a save by slot name (treated as Manual slot).</summary>
    void LoadGame(string slotName);
    /// <summary>
    /// 2026-09-08 (ревью-1): загрузка полного слота. UI передаёт тот же
    /// <see cref="SaveSlot"/>, что проверял в ISaveService.HasSave — тип слота
    /// из UI совпадает с типом при чтении (маршрутизация файла — по Name).
    /// </summary>
    void LoadGame(SaveSlot slot);
    void Pause();
    void Resume();
    void SaveAndQuit();
    void QuitWithoutSaving();

    event Action<SessionState>? OnStateChanged;
}

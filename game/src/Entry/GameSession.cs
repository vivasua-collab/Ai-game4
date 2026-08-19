#nullable enable
using System;
using System.Threading;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Entry;

/// <summary>
/// Manages the lifecycle of a single play session: state machine, owned
/// <see cref="GameSessionData"/>, and delegation to the
/// <see cref="SceneOrchestrator"/> and <see cref="ISaveService"/>.
/// </summary>
/// <remarks>
/// <para><b>State machine:</b> <c>MainMenu → Loading → Playing ⇄ Paused → Saving → Quitting</c>.</para>
/// <para><b>Events published:</b> <see cref="GamePausedEvent"/>,
/// <see cref="GameResumedEvent"/>. State transitions are also surfaced via
/// the <see cref="OnStateChanged"/> event and logged to stdout for v1.</para>
/// <para>The <c>IGameSession</c> interface is synchronous (void-returning).
/// <see cref="SceneOrchestrator.RunAssembly"/> is async, so the New/Load
/// paths block on it via <c>GetAwaiter().GetResult()</c>. This is safe in
/// the v1 pure-C# host (no sync-context deadlock risk); the Godot adapter
/// should call these from a non-UI thread or wrap them in a task.</para>
/// </remarks>
public sealed class GameSession : IGameSession
{
    [Inject] private readonly SceneOrchestrator _orchestrator = null!;
    [Inject] private readonly ISaveService _save = null!;
    [Inject] private readonly IPublisher<GamePausedEvent> _pausedPub = null!;
    [Inject] private readonly IPublisher<GameResumedEvent> _resumedPub = null!;

    private long _frameCounter;

    /// <inheritdoc />
    public SessionState State { get; private set; } = SessionState.MainMenu;

    /// <inheritdoc />
    public GameSessionData Data { get; private set; } = new();

    /// <inheritdoc />
    public event Action<SessionState>? OnStateChanged;

    private void SetState(SessionState next)
    {
        if (State == next) return;
        State = next;
        try { OnStateChanged?.Invoke(next); }
        catch (Exception ex) { Console.WriteLine($"[GameSession] OnStateChanged handler threw: {ex.GetType().Name}: {ex.Message}"); }
    }

    // ──────────────────────────────────────────────────────────────────
    //  New / Load
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void NewGame(int startVariant)
    {
        NewGame(startVariant, LocationCatalog.TestPolygon.Id);
    }

    /// <inheritdoc />
    public void NewGame(int startVariant, string locationId)
    {
        if (State != SessionState.MainMenu && State != SessionState.Quitting)
        {
            Console.WriteLine($"[GameSession] NewGame rejected — state={State}");
            return;
        }

        // Resolve location from catalog.
        var loc = LocationCatalog.Find(locationId) ?? LocationCatalog.TestPolygon;

        SetState(SessionState.Loading);
        Data = new GameSessionData
        {
            Id = Guid.NewGuid().ToString("N"),
            WorldId = loc.Id,
            WorldName = loc.Name,
            StartVariant = startVariant,
            WorldTime = new WorldTime(GameConstants.START_YEAR, 1, 1, 6, 0),
            DaysSinceStart = 0,
            IsPaused = false,
        };

        Console.WriteLine($"[GameSession] NewGame variant={startVariant} location={loc.Id} ({loc.Width}×{loc.Height}) — assembling scene...");
        try
        {
            _orchestrator.RunAssembly(CancellationToken.None).GetAwaiter().GetResult();
            SetState(SessionState.Playing);
            Console.WriteLine("[GameSession] NewGame ready — state=Playing");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameSession] NewGame failed: {ex.GetType().Name}: {ex.Message}");
            SetState(SessionState.MainMenu);
        }
    }

    /// <inheritdoc />
    public void LoadGame(string slotName)
    {
        if (State != SessionState.MainMenu && State != SessionState.Quitting)
        {
            Console.WriteLine($"[GameSession] LoadGame rejected — state={State}");
            return;
        }

        SetState(SessionState.Loading);
        Console.WriteLine($"[GameSession] LoadGame slot='{slotName}' — loading...");
        try
        {
            // ISaveService.Load triggers ISaveable.RestoreState on every
            // registered saveable. GameSession.Data is refreshed minimally
            // here; full restoration happens inside the save module.
            _save.Load(new SaveSlot(slotName, SaveSlotType.Manual));

            Data = new GameSessionData
            {
                Id = slotName,
                WorldId = LocationCatalog.TestPolygon.Id,
                WorldName = LocationCatalog.TestPolygon.Name,
                StartVariant = 1,
                WorldTime = new WorldTime(GameConstants.START_YEAR, 1, 1, 6, 0),
                DaysSinceStart = 0,
                IsPaused = false,
            };

            _orchestrator.RunAssembly(CancellationToken.None).GetAwaiter().GetResult();
            SetState(SessionState.Playing);
            Console.WriteLine("[GameSession] LoadGame ready — state=Playing");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameSession] LoadGame failed: {ex.GetType().Name}: {ex.Message}");
            SetState(SessionState.MainMenu);
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  Pause / Resume
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void Pause()
    {
        if (State != SessionState.Playing) return;
        SetState(SessionState.Paused);
        Data.IsPaused = true;
        _pausedPub.Publish(new GamePausedEvent());
        Console.WriteLine("[GameSession] Paused");
    }

    /// <inheritdoc />
    public void Resume()
    {
        if (State != SessionState.Paused) return;
        SetState(SessionState.Playing);
        Data.IsPaused = false;
        _resumedPub.Publish(new GameResumedEvent());
        Console.WriteLine("[GameSession] Resumed");
    }

    /// <summary>Advance the internal frame counter (used for event payloads).</summary>
    internal void AdvanceFrame() => _frameCounter++;

    // ──────────────────────────────────────────────────────────────────
    //  Save+Quit / Quit
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void SaveAndQuit()
    {
        if (State == SessionState.Quitting) return;

        SetState(SessionState.Saving);
        Console.WriteLine("[GameSession] SaveAndQuit — saving...");
        try
        {
            _save.Save(new SaveSlot(Data.Id, SaveSlotType.Manual));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameSession] Save failed (proceeding to quit): {ex.GetType().Name}: {ex.Message}");
        }

        SetState(SessionState.Quitting);
        Console.WriteLine("[GameSession] Quitting (saved)");
    }

    /// <inheritdoc />
    public void QuitWithoutSaving()
    {
        if (State == SessionState.Quitting) return;
        SetState(SessionState.Quitting);
        Console.WriteLine("[GameSession] Quitting (no save)");
    }
}

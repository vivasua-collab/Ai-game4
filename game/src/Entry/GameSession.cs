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
    // P1-2 (аудит 09.22): честный результат SaveAndQuit — сбой сейва виден
    // игроку (тост), а не тонет в логе.
    [Inject] private readonly IPublisher<Core.Messaging.Contracts.ToastShownEvent> _toastPub = null!;
    // R17 (аудит-0911 E-1): сброс ВСЕХ world-scoped доменов при тёплой
    // загрузке (до RestoreState) — контракт IWorldResettable.
    [Inject] private readonly IResolver _resolver = null!;
    // R17 (E-3): идентичность мира/время — из восстановленного состояния,
    // а не хардкод TestPolygon/06:00.
    [Inject] private readonly IWorldService _worldService = null!;
    [Inject] private readonly ITimeService _timeService = null!;

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
            // 2026-09-08 (ревью-1): режим NewGame — все фазы выполняются
            // (оркестратор сам Reset-ит и ведёт lifecycle фаз).
            _orchestrator.RunAssembly(CancellationToken.None, SceneAssemblyMode.NewGame)
                .GetAwaiter().GetResult();
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
        // 2026-09-08 (ревью-1): обратно-совместимая обёртка — слот по имени
        // трактуется как Manual (канонические слоты UI).
        LoadGame(new SaveSlot(slotName, SaveSlotType.Manual));
    }

    /// <summary>
    /// 2026-09-08 (ревью-1, санитация P1-2): загрузка ПОЛНОГО слота.
    /// Раньше IGameSession.LoadGame(string) внутренне конструировал
    /// SaveSlotType.Manual, а MainMenu проверял HasSave(QuickSave) — тип
    /// не совпадал с проверкой UI. Теперь вызывающая сторона (UI) передаёт
    /// тот же SaveSlot, что и в HasSave. Маршрутизация файла — по
    /// slot.Name (см. SaveService), тип участвует только в семантике слота.
    /// </summary>
    public void LoadGame(SaveSlot slot)
    {
        if (State != SessionState.MainMenu && State != SessionState.Quitting)
        {
            Console.WriteLine($"[GameSession] LoadGame rejected — state={State}");
            return;
        }

        string slotName = slot.Name;
        SetState(SessionState.Loading);
        Console.WriteLine($"[GameSession] LoadGame slot='{slot}' — loading...");
        try
        {
            // R14-аудит (P2-1) → R17 (E-1): сброс ВСЕХ world-scoped доменов
            // ДО RestoreState — в этом же процессе мог жить прошлый мир
            // (NewGame → меню → LoadGame): реестры/провайдеры/домены должны
            // быть пусты ДО того, как сейв наполнит их заново (RestoreState
            // делает merge, не чистит). Аналог WorldDomainResetPhase (фаза 0,
            // NewGame-режим): на Load фазы идут ПОСЛЕ восстановления —
            // сброс в фазе опоздал бы. Раньше сбрасывался только NPC-домен:
            // звери/формации/зарядник/инвентарь/кукла/время/квесты/валюта
            // прошлого мира «переживали» загрузку.
            int resetCount = 0;
            foreach (var domain in _resolver.ResolveAll<IWorldResettable>())
            {
                domain.ResetWorld();
                resetCount++;
            }
            Console.WriteLine($"[GameSession] LoadGame: {resetCount} world-scoped доменов сброшено до RestoreState");

            // ISaveService.Load triggers ISaveable.RestoreState on every
            // registered saveable (порядок — контракт RestoreOrder в
            // SaveDataAggregator: мир → время → каталог → …). GameSession.Data
            // обновляется из ВОССТАНОВЛЕННОГО состояния ниже.
            // Аудит-0915 A4 (SAV-P2-1): результат Load обязателен к проверке —
            // false (файл отсутствует/битый JSON/упавший блок) раньше
            // игнорировался: сессия строилась на СТЁРТЫХ (ResetWorld выше)
            // и не восстановленных доменах, а первый автосейв затирал слот
            // этим пустым состоянием. Теперь — честный отказ в меню.
            if (!_save.Load(slot))
            {
                Console.WriteLine($"[GameSession] LoadGame FAILED — slot='{slot}' damaged or missing; returning to MainMenu");
                SetState(SessionState.MainMenu);
                return;
            }

            // R17 (E-3): идентичность мира — из блока "world" (раньше
            // хардкод TestPolygon: сейв из large_world грузился на сетку
            // 50×50 чужой локации). Время — из блока "world_time" (раньше
            // 06:00 дня 1 независимо от сейва).
            var restoredLocation = _worldService.CurrentLocation ?? LocationCatalog.TestPolygon;
            Data = new GameSessionData
            {
                Id = slotName,
                WorldId = restoredLocation.Id,
                WorldName = restoredLocation.Name,
                StartVariant = 1,
                WorldTime = _timeService.CurrentTime,
                DaysSinceStart = Math.Max(0, _timeService.CurrentTime.Day - 1),
                IsPaused = false,
            };

            // 2026-09-08 (ревью-1): режим LoadGame — оркестратор пропускает
            // генеративные фазы (SkipOnLoad), wiring-фазы (UI, валидация,
            // финализация) выполняются; мировое состояние восстанавливает сейв.
            _orchestrator.RunAssembly(CancellationToken.None, SceneAssemblyMode.LoadGame)
                .GetAwaiter().GetResult();
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
        bool saved;
        try
        {
            // P1-2 (аудит 09.22): результат bool читается. Прежде — игнорировался:
            // при отказе любого CaptureState (транзакционность агрегатора — файл
            // НЕ пишется) или I/O-сбое сессия всё равно завершалась с логом
            // «Quitting (saved)» — data-loss UX: игрок уверен, что прогресс
            // сохранён. Теперь: сбой → остаёмся в игре (Playing), тост с
            // причиной (LastError), решение за игроком (retry / quit без сейва).
            saved = _save.Save(new SaveSlot(Data.Id, SaveSlotType.Manual));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameSession] Save failed: {ex.GetType().Name}: {ex.Message}");
            saved = false;
        }

        if (!saved)
        {
            Console.WriteLine($"[GameSession] Save FAILED — сессия продолжается (LastError: {_save.LastError})");
            _toastPub?.Publish(new Core.Messaging.Contracts.ToastShownEvent(
                $"Не удалось сохранить: {_save.LastError ?? "ошибка записи"}", 5f));
            SetState(SessionState.Playing);
            return;
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

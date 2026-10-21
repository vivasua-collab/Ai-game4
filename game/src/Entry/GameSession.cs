#nullable enable
using System;
using System.Threading;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Entry.Phases;

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

    /// <summary>Этап-4: исход последнего LoadGame (маршрутизация UI).</summary>
    public LoadGameOutcome LastLoadOutcome { get; private set; } = LoadGameOutcome.None;

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
        // Этап-3 (меню сейвов, 2026-10-20): + Playing — внутриигровая загрузка
        // из меню сохранения/загрузки (F5). Этап-4 (P1, 20.10.2026): пайплайн
        // стал ТРАНЗАКЦИОННЫМ — неудачная загрузка больше не уничтожает
        // текущий мир:
        //   1. pre-flight Validate (формат/мета/обязательные блоки) БЕЗ
        //      побочных эффектов — повреждённый слот отвергается ДО сброса;
        //   2. если игрок в мире (Playing) — in-memory снимок ВСЕХ доменов
        //      (CaptureBackup: тот же CaptureState, без файла/SaveStarted);
        //   3. сброс (единый fail-closed координатор) → RestoreState →
        //      пост-загрузочная синхронизация (IPostLoadSync, BUG-013) →
        //      RunAssembly(LoadGame);
        //   4. сбой ПОСЛЕ сброса → повторный сброс (вычистить частично-
        //      восстановленное) + откат из снимка → сессия ВОЗВРАЩАЕТСЯ В ИГРУ,
        //      игрок видит тост с причиной;
        //   5. откат невозможен (снимка нет/не снялся/провалился) — честный
        //      fail-closed в MainMenu (прежнее поведение, теперь — последний
        //      рубеж, а не правило).
        if (State != SessionState.MainMenu && State != SessionState.Quitting
            && State != SessionState.Playing)
        {
            Console.WriteLine($"[GameSession] LoadGame rejected — state={State}");
            return;
        }

        string slotName = slot.Name;
        bool wasPlaying = State == SessionState.Playing;
        var prevData = Data;

        // ── 1. Pre-flight: формат проверяется ДО любого сброса. ────────
        // Аудит Этап-4 (P1): прежний порядок «сброс → Load» уничтожал текущий
        // мир ДО первой проверки сейва: битый/чужой/несуществующий слот =
        // потеря несохранённого прогресса. Теперь домены не трогаются вовсе.
        if (!_save.Validate(slot))
        {
            Console.WriteLine($"[GameSession] LoadGame PRE-REJECTED — slot='{slot}': {_save.LastError ?? "формат не поддерживается"}; " +
                $"текущий мир НЕ тронут (state={State})");
            _toastPub?.Publish(new Core.Messaging.Contracts.ToastShownEvent(
                $"Слот «{slotName}» не прошёл проверку: {_save.LastError ?? "формат не поддерживается"}", 5f));
            LastLoadOutcome = LoadGameOutcome.RejectedByPrecheck;
            return; // состояние сессии НЕ менялось
        }

        // ── 2. Страховочный снимок текущего мира (только из Playing). ───
        bool hasBackup = wasPlaying && _save.CaptureBackup();
        if (wasPlaying && !hasBackup)
            Console.WriteLine("[GameSession] LoadGame WARN — снимок не снялся: при сбое ПОСЛЕ сброса откат невозможен (fail-closed в меню)");

        SetState(SessionState.Loading);
        Console.WriteLine($"[GameSession] LoadGame slot='{slot}' — loading... (backup={(hasBackup ? "yes" : "no")})");
        try
        {
            // R14-аудит (P2-1) → R17 (E-1): сброс ВСЕХ world-scoped доменов
            // ДО RestoreState — в этом же процессе мог жить прошлый мир
            // (NewGame → меню → LoadGame): реестры/провайдеры/домены должны
            // быть пусты ДО того, как сейв наполнит их заново (RestoreState
            // делает merge, не чистит).
            //
            // Этап-3 (дефект 4): сброс — через ЕДИНЫЙ координатор
            // (WorldDomainResetCoordinator, тот же что у фазы 0 NewGame):
            // ВСЕ домены получают ResetWorld() (попытки независимы), ошибки
            // агрегируются → AggregateException → fail-closed ниже.
            int resetCount = WorldDomainResetCoordinator.ResetAll(_resolver);
            Console.WriteLine($"[GameSession] LoadGame: {resetCount} world-scoped доменов сброшено до RestoreState");

            // Аудит-0915 A4 (SAV-P2-1): результат Load обязателен к проверке.
            // Pre-flight отсекает формато-уровневые проблемы; здесь остаются
            // ошибки Restore (блок прошёл проверку наличия, но упал при
            // десериализации/восстановлении) — их ловит откат ниже.
            if (!_save.Load(slot))
                throw new InvalidOperationException($"restore failed: {_save.LastError ?? "см. SaveDataAggregator.LastErrors"}");

            // Этап-4 (BUG-013): производные трекеры доменов — ПОСЛЕ
            // восстановления (NPCSpawnerService.OnAfterLoad синхронизирует
            // _spawnedIds с реестром NPCService: ActiveNPCCount, лимит
            // MaxActiveNPCs и DespawnNPC работают для восстановленных NPC).
            int synced = 0;
            foreach (var sync in _resolver.ResolveAll<IPostLoadSync>())
            {
                sync.OnAfterLoad();
                synced++;
            }
            if (synced > 0)
                Console.WriteLine($"[GameSession] LoadGame: {synced} IPostLoadSync-доменов синхронизировано после RestoreState");

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
            LastLoadOutcome = LoadGameOutcome.Success;
            if (hasBackup) _save.DiscardBackup(); // мир заменён — снимок не нужен
            Console.WriteLine("[GameSession] LoadGame ready — state=Playing");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameSession] LoadGame failed: {ex.GetType().Name}: {ex.Message}");

            // ── 4. Транзакционный откат: предыдущий мир — из снимка. ────
            if (hasBackup)
            {
                try
                {
                    // Повторный сброс вычищает частично-восстановленное
                    // состояние провалившейся загрузки (RestoreState — merge).
                    WorldDomainResetCoordinator.ResetAll(_resolver);
                    if (_save.RestoreBackup())
                    {
                        // Производные трекеры — по восстановленному реестру
                        // (тот же контракт, что и после основной загрузки).
                        foreach (var sync in _resolver.ResolveAll<IPostLoadSync>())
                            sync.OnAfterLoad();

                        Data = prevData;
                        SetState(SessionState.Playing);
                        LastLoadOutcome = LoadGameOutcome.RolledBack;
                        _toastPub?.Publish(new Core.Messaging.Contracts.ToastShownEvent(
                            $"Загрузка «{slotName}» не удалась — текущий мир продолжается ({ex.Message})", 5f));
                        Console.WriteLine("[GameSession] LoadGame ROLLED BACK — предыдущий мир восстановлен из снимка, state=Playing");
                        return;
                    }
                    Console.WriteLine("[GameSession] LoadGame rollback FAILED — RestoreBackup провалился, fail-closed в меню");
                }
                catch (Exception rbEx)
                {
                    Console.WriteLine($"[GameSession] LoadGame rollback CRASHED: {rbEx.GetType().Name}: {rbEx.Message} — fail-closed в меню");
                }
                _save.DiscardBackup();
            }

            // ── 5. Fail-closed: мира нет/он недостоверен — честно в меню. ──
            SetState(SessionState.MainMenu);
            LastLoadOutcome = LoadGameOutcome.Failed;
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
        // R36-a (Фаза 11 / P2-27): единая pause-authority — сессионная пауза
        // ОСТАНАВЛИВАЕТ мировое время. Прежде GameSession.Pause() менял только
        // SessionState, не касаясь TimeService: две независимые истины о паузе
        // (SessionState.Paused vs TimeService.IsPaused) могли расходиться
        // (текущий GameBoot гейтил симуляцию по обоим — латентно, но API врал).
        _timeService.Pause();
        _pausedPub.Publish(new GamePausedEvent());
        Console.WriteLine("[GameSession] Paused");
    }

    /// <inheritdoc />
    public void Resume()
    {
        if (State != SessionState.Paused) return;
        // R36-a (Фаза 11 / P2-27): время возвращается вместе с сессией
        // (Resume восстанавливает скорость ДО паузы, Fast/Quick живы).
        _timeService.Resume();
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

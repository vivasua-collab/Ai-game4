#nullable enable
using System;
using Godot;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Persistence;
using CultivationGame.Entry;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Autoload (registered in project.godot as GameBoot).
/// This is the FIRST node that runs. It bootstraps the DI container,
/// resolves the <see cref="GameEntryPoint"/>, starts all modules,
/// and drives the simulation tick loop from <see cref="_PhysicsProcess"/>.
/// </summary>
public partial class GameBoot : Node
{
    /// <summary>Global DI resolver — accessed by scene controllers for property injection.</summary>
    public static IResolver? Container { get; private set; }

    /// <summary>Resolved game entry point (set after _Ready).</summary>
    public static GameEntryPoint? EntryPoint { get; private set; }

    private GameEntryPoint? _entry;
    private ITimeService? _timeService;
    // P2-13 (аудит 09.22, Фаза 4): провал fail-closed старта — тик-луп и
    // всю пост-стартовую автоматизацию глушим: полуинициализированные модули
    // не должны симулировать/автосейвить. Раньше исключение Start()
    // глоталось внутри GameEntryPoint — GameBoot об провале даже не знал.
    private bool _bootFailed;
    // R17 (аудит-0911 E-4): гейт тик-лупа по состоянию сессии — симуляция
    // (и автосейв) живут ТОЛЬКО в активной игровой сессии. Раньше тики шли
    // с бутстрапа: в главном меню fallback-мир симулировал, SaveModule писал
    // мусорные autosave-слоты, а NewGame наследовал время, проведённое в меню.
    private IGameSession? _session;

    // Tick driving state.
    // R35 (Фаза 11 / P1-13): математика fixed-timestep catch-up извлечена в
    // TickCatchUpClock (тестируемость — сим №19/M). _currentTick остаётся
    // здесь: process-tick (каденция автосейва SaveModule), синхронно
    // продвигается и при bulk-скачке.
    private readonly TickCatchUpClock _catchUpClock = new();
    private int _currentTick;

    public override async void _Ready()
    {
        // Register input actions programmatically (avoids Object() syntax in project.godot).
        CultivationGame.Adapter.Input.InputMapInitializer.EnsureInitialized();

        // Build the DI container (registers all 16 modules + entry + scene phases).
        // The Adapter-override hook registers Adapter.Persistence.SaveFileHandler
        // (Godot-aware, ProjectSettings.GlobalizePath) as ISaveFileHandler in
        // place of the Modules-layer default (AppContext.BaseDirectory).
        // See audit issue #6 (08_15_code_audit.md).
        //
        // We use RegisterInstance (pre-built) because Adapter.Persistence.SaveFileHandler
        // has a parameterless ctor that calls ProjectSettings.GlobalizePath — we want
        // THAT ctor, not the (string saveRoot) test ctor that DI's "greediest ctor"
        // heuristic would otherwise pick.
        Container = GameLifetimeScope.Build(configureAdapter: builder =>
        {
            var godotSaveHandler = new SaveFileHandler();
            builder.RegisterInstance(godotSaveHandler);
            builder.RegisterInstance<ISaveFileHandler>(godotSaveHandler);
        });

        // Resolve GameEntryPoint — the IStartable/ITickable root.
        _entry = Container.Resolve<GameEntryPoint>();
        EntryPoint = _entry;

        // Cache the time service so we can read Speed/IsPaused every physics frame.
        _timeService = Container.Resolve<ITimeService>();

        // R17 (E-4): кэшируем сессию для гейта тиков (см. _PhysicsProcess).
        _session = Container.Resolve<IGameSession>();

        // Start all IStartable modules.
        // P2-13: startup fail-closed — AggregateException = бут провалился:
        // громко логируем каждый провал (PushError — видно и в Godot-консоли,
        // и в headless-логах QA), глушим тик-луп (_bootFailed) и НЕ входим в
        // игру/автоматизацию. В headless-QA такой бут не напечатает VERDICT —
        // сим честно падает по таймауту как FAIL (прежде — тихо «зелёный»
        // прогон с проглоченным стартом).
        try
        {
            _entry.Start();
        }
        catch (Exception ex)
        {
            _bootFailed = true;
            GD.PushError($"[GameBoot] Startup FAILED (fail-closed, P2-13): {ex.Message}");
            switch (ex)
            {
                case AggregateException agg:
                    foreach (var inner in agg.InnerExceptions)
                    {
                        var root = inner;
                        while (root.InnerException != null) root = root.InnerException;
                        GD.PushError($"[GameBoot]   • {inner.Message} — {root.GetType().Name}: {root.Message}");
                    }
                    break;
                default:
                    GD.PushError(ex.ToString());
                    break;
            }
            return;
        }

        GD.Print("[GameBoot] Game initialized. Container built and entry point started.");

        // Screenshot automation: if GODOT_SCREENSHOT env is set, wait for render then capture and quit.
        // GODOT_SCREENSHOT_DELAY (сек, default 2) — задержка кадра (например, чтобы
        // поймать бой при GODOT_COMBAT_SIM=1: урон идёт с ~2.9с, берите 3.2+).
        var screenshotPath = System.Environment.GetEnvironmentVariable("GODOT_SCREENSHOT");
        if (!string.IsNullOrEmpty(screenshotPath))
        {
            float shotDelay = 2.0f;
            var delayRaw = System.Environment.GetEnvironmentVariable("GODOT_SCREENSHOT_DELAY");
            if (float.TryParse(delayRaw, System.Globalization.CultureInfo.InvariantCulture,
                    out float parsed) && parsed > 0f && parsed <= 60f)
                shotDelay = parsed;

            GD.Print($"[GameBoot] Screenshot mode: will save to {screenshotPath} in {shotDelay:0.#}s...");
            await ToSignal(GetTree().CreateTimer(shotDelay), SceneTreeTimer.SignalName.Timeout);
            var img = GetViewport().GetTexture().GetImage();
            if (img != null)
            {
                var err = img.SavePng(screenshotPath);
                GD.Print($"[GameBoot] Screenshot saved: {err == Godot.Error.Ok} ({img.GetWidth()}x{img.GetHeight()})");
            }
            else
            {
                GD.Print("[GameBoot] Screenshot FAILED: viewport image is null");
            }
            GetTree().Quit();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_entry == null || _timeService == null)
            return;

        // P2-13: проваленный бут не симулируется — ни тиков, ни автосейвов.
        if (_bootFailed)
            return;

        // R17 (E-4): симуляция — только в активной сессии. MainMenu/Loading/
        // Saving/Quitting — мир не тикает (раньше fallback-мир меню
        // симулировал и автосейвил). Внутри игры паузы (Esc/окна) по-прежнему
        // управляются TimeService.IsPaused ниже — сессия остаётся Playing.
        if (_session != null && _session.State != SessionState.Playing)
            return;

        // Speed == 0 (TimeSpeed.Paused) means no ticks.
        // Also honour explicit IsPaused flag on the time service.
        if (_timeService.IsPaused)
            return;

        int speed = (int)_timeService.Speed;
        if (speed <= 0)
            return;

        // R35 (Фаза 11 / P1-13): catch-up — в TickCatchUpClock (сим №19/M).
        // Инвариант учёта: смоделировано + bulk-skipped + остаток долга ==
        // накопленное реальное время. Bulk-скачок (hitch > MaxCatchupSeconds):
        // мировые часы продвигаются скачком (календарь честен), process-tick
        // синхронно +n (каденция автосейва не рвётся), ВИДИМОЕ предупреждение.
        int ranTicks = _catchUpClock.Advance((float)delta, speed, out int bulkSkipped);
        for (int i = 0; i < ranTicks; i++)
        {
            _currentTick++;
            _entry.Tick(_currentTick);
        }

        if (bulkSkipped > 0)
        {
            _currentTick += bulkSkipped;
            // Контракт в ITimeService (Core) — без каста к Modules-типу.
            _timeService.BulkAdvanceTicks(bulkSkipped);
            GD.PushWarning($"[GameBoot] HITCH: {bulkSkipped} игровых минут пропущено без " +
                           "посимвольной симуляции (долг catch-up выше потолка) — календарь продвинут честно");
        }
    }

    /// <summary>
    /// Graceful shutdown: dispose the entry point if it implements IDisposable.
    /// Called by Godot when the autoload is about to be freed (app exit).
    /// </summary>
    public override void _ExitTree()
    {
        // GameEntryPoint doesn't implement IDisposable in v1; modules clean up via their own _ExitTree.
        GD.Print("[GameBoot] Game shutdown.");
    }
}

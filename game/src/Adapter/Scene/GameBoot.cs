#nullable enable
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

    // Tick driving state.
    private float _tickAccumulator;
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

        // Start all IStartable modules.
        _entry.Start();

        GD.Print("[GameBoot] Game initialized. Container built and entry point started.");

        // Screenshot automation: if GODOT_SCREENSHOT env is set, wait for render then capture and quit.
        var screenshotPath = System.Environment.GetEnvironmentVariable("GODOT_SCREENSHOT");
        if (!string.IsNullOrEmpty(screenshotPath))
        {
            GD.Print($"[GameBoot] Screenshot mode: will save to {screenshotPath} in 2s...");
            await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);
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

        // Speed == 0 (TimeSpeed.Paused) means no ticks.
        // Also honour explicit IsPaused flag on the time service.
        if (_timeService.IsPaused)
            return;

        int speed = (int)_timeService.Speed;
        if (speed <= 0)
            return;

        double tickInterval = 1.0 / speed;
        _tickAccumulator += (float)delta;

        // Run as many ticks as fit in the accumulated time (catch-up pattern).
        // Hard cap of 8 ticks per physics frame to avoid spiral-of-death on hitches.
        int ticksRun = 0;
        while (_tickAccumulator >= tickInterval && ticksRun < 8)
        {
            _tickAccumulator -= (float)tickInterval;
            _currentTick++;
            _entry.Tick(_currentTick);
            ticksRun++;
        }

        // If we still have a large backlog (lag spike), drop it to avoid runaway.
        if (_tickAccumulator > tickInterval * 8f)
            _tickAccumulator = 0f;
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

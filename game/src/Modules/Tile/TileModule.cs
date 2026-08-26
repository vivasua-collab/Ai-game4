#nullable enable
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Tile;

/// <summary>
/// Tile module — event-driven (no tick). Subscribes to LocationChanged /
/// LocationLoaded to regenerate the tile grid for the active location.
/// </summary>
public sealed class TileModule : IModule
{
    public string ModuleName => "Tile";

    [Inject] private readonly ITileService _tileService = null!;
    [Inject] private readonly ISubscriber<LocationChangedEvent> _locationChangedSub = null!;
    [Inject] private readonly IPublisher<TileMapGeneratedEvent> _mapGenPublisher = null!;

    private IDisposable? _locationSubToken;
    private TileConfig _config = new();

    public void Start()
    {
        _locationSubToken = _locationChangedSub.Subscribe(OnLocationChanged);

        // Env var override for perf testing: GODOT_MAP_SIZE=500 generates 500×500.
        // Usage: GODOT_MAP_SIZE=500 godot --headless scenes/GameWorld.tscn
        int width = _config.DefaultWidth;
        int height = _config.DefaultHeight;
        int seed = _config.DefaultSeed;
        var envSize = System.Environment.GetEnvironmentVariable("GODOT_MAP_SIZE");
        if (!string.IsNullOrEmpty(envSize) && int.TryParse(envSize, out var envW))
        {
            width = height = envW;
            seed = 67890; // deterministic for large world
            Console.WriteLine($"[TileModule] GODOT_MAP_SIZE={envW} override");
        }

        // Generate a default grid as fallback for direct scene loading
        // (e.g. `godot scenes/GameWorld.tscn` from CLI without going through MainMenu).
        // When NewGame() is called, TileMapGenPhase will regenerate with the
        // selected location (test_polygon 50×50 or large_world 500×500).
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _tileService.Generate(seed, width, height, _config.DefaultTerrain);
        sw.Stop();
        _mapGenPublisher.Publish(new TileMapGeneratedEvent(width, height, seed));
        Console.WriteLine($"[TileModule] Started — generated {width}x{height} grid in {sw.ElapsedMilliseconds} ms");
    }

    public void Tick(int tickCount)
    {
        // Event-driven — no per-tick work in V1.
    }

    private void OnLocationChanged(in LocationChangedEvent e)
    {
        Console.WriteLine($"[TileModule] LocationChanged '{e.PreviousLocationId}' → '{e.NewLocationId}'");
        // Real impl reads location seed/dims from IWorldService and calls Generate.
    }

    public void Dispose()
    {
        _locationSubToken?.Dispose();
        Console.WriteLine("[TileModule] Disposed");
    }
}

public static class TileModuleServices
{
    public static void Register(IContainerBuilder builder)
    {
        builder.Register<TileConfig>(Lifetime.Singleton);
        builder.Register<TileService>(Lifetime.Singleton);
        builder.Register<ResourceService>(Lifetime.Singleton);
        builder.Register<ITileService, TileService>(Lifetime.Singleton);
        builder.Register<IResourceService, ResourceService>(Lifetime.Singleton);
        builder.Register<TileModule>(Lifetime.Singleton);
    }
}

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
        // Generate a default grid so V1 has something playable before scene is ready
        _tileService.Generate(_config.DefaultSeed, _config.DefaultWidth, _config.DefaultHeight, _config.DefaultTerrain);
        _mapGenPublisher.Publish(new TileMapGeneratedEvent(_config.DefaultWidth, _config.DefaultHeight, _config.DefaultSeed));
        Console.WriteLine($"[TileModule] Started — generated {_config.DefaultWidth}x{_config.DefaultHeight} grid");
    }

    public void Tick(int tickCount)
    {
        // Event-driven — no per-tick work in V1.
    }

    private void OnLocationChanged(in LocationChangedEvent e)
    {
        Console.WriteLine($"[TileModule] LocationChanged '{e.OldLocationId}' → '{e.NewLocationId}'");
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
        builder.Register<ITileService, TileService>(Lifetime.Singleton);
        builder.Register<TileModule>(Lifetime.Singleton);
    }
}

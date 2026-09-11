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
    // AUDIT-0911 WT-1: инъекция ResourceService для Initialize (DayChanged-подписка).
    [Inject] private readonly IResourceService _resourceService = null!;
    [Inject] private readonly ISubscriber<LocationChangedEvent> _locationChangedSub = null!;
    [Inject] private readonly IPublisher<TileMapGeneratedEvent> _mapGenPublisher = null!;
    // R11 P2-World/Tile (review): единый источник геометрии карты — АКТИВНАЯ
    // локация WorldService (модуль World стартует первым по каноническому
    // порядку DI_AND_EVENTBUS §1.2 — CurrentLocation уже установлен).
    [Inject] private readonly IWorldService _worldService = null!;

    private IDisposable? _locationSubToken;
    private TileConfig _config = new();

    public void Start()
    {
        _locationSubToken = _locationChangedSub.Subscribe(OnLocationChanged);

        // Review этап 6 (P0-1): TileService подписывается на respawn-события
        // (восстановление истощённых ресурсов в grid).
        if (_tileService is TileService ts)
            ts.Initialize();

        // AUDIT-0911 WT-1 FIX: ResourceService.Initialize — ЕДИНСТВЕННАЯ
        // подписка DayChangedEvent → RespawnCheck. Раньше НИКТО его не
        // вызывал: респаун ресурсов был мёртв в живой игре (QA-сим звал
        // RespawnCheck напрямую и маскировал баг).
        if (_resourceService is ResourceService rs)
            rs.Initialize();

        // R11 P2-World/Tile (review): атомарная связка CurrentLocation ↔
        // CurrentGrid. Раньше геометрия бралась из дубля констант TileConfig
        // (совпадение с test_polygon 50×50/12345 было случайным), а
        // GODOT_MAP_SIZE-override вообще ломал соответствие: CurrentLocation
        // говорил об одной карте, grid — уже о другой. Теперь параметры —
        // из данных активной локации; TileConfig — только последний fallback
        // (активной локации нет); GODOT_MAP_SIZE — явно задокументированный
        // perf-override для тестов.
        int width = _config.DefaultWidth;
        int height = _config.DefaultHeight;
        int seed = _config.DefaultSeed;
        var terrain = _config.DefaultTerrain;

        var active = _worldService.CurrentLocation;
        if (active != null && active.Width > 0 && active.Height > 0)
        {
            width = active.Width;
            height = active.Height;
            seed = active.Seed;
            terrain = active.TerrainType;
        }

        // Env var override for perf testing: GODOT_MAP_SIZE=500 generates 500×500.
        // Usage: GODOT_MAP_SIZE=500 godot --headless scenes/GameWorld.tscn
        // ВНИМАНИЕ: осознанно рассинхронизирует grid с активной локацией —
        // только для замеров производительности.
        var envSize = System.Environment.GetEnvironmentVariable("GODOT_MAP_SIZE");
        if (!string.IsNullOrEmpty(envSize) && int.TryParse(envSize, out var envW))
        {
            width = height = envW;
            seed = 67890; // deterministic for large world
            Console.WriteLine($"[TileModule] GODOT_MAP_SIZE={envW} override (grid ≠ активная локация — perf-тест)");
        }

        // Generate a default grid as fallback for direct scene loading
        // (e.g. `godot scenes/GameWorld.tscn` from CLI without going through MainMenu).
        // When NewGame() is called, TileMapGenPhase will regenerate with the
        // selected location (test_polygon 50×50 or large_world 500×500).
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _tileService.Generate(seed, width, height, terrain);
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
        // Review этап 6 (P1-2): реальная генерация новой карты при смене локации
        // НЕ реализована (travel-pipeline — будущая фаза). TryTravel теперь честно
        // возвращает false БЕЗ смены активной локации, поэтому сюда мы попадаем
        // только при реальных переходах (загрузка сейва / смена сцены).
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

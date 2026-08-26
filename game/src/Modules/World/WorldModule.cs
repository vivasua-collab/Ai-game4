#nullable enable
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.World;

/// <summary>
/// World module — owns time &amp; world (locations) services. Advances time
/// every tick and publishes TimeTickEvent on the bus.
/// </summary>
public sealed class WorldModule : IModule
{
    public string ModuleName => "World";

    [Inject] private readonly ITimeService _timeService = null!;
    [Inject] private readonly IWorldService _worldService = null!;
    [Inject] private readonly IPublisher<TimeTickEvent> _tickPublisher = null!;
    [Inject] private readonly IPublisher<TimeChangedEvent> _timeChangedPublisher = null!;
    [Inject] private readonly IPublisher<DayChangedEvent> _dayChangedPublisher = null!;
    [Inject] private readonly IPublisher<MonthChangedEvent> _monthChangedPublisher = null!;
    [Inject] private readonly IPublisher<YearChangedEvent> _yearChangedPublisher = null!;
    [Inject] private readonly ISubscriber<SaveRequestedEvent> _saveSub = null!;

    private IDisposable? _saveSubToken;
    private WorldConfig _config = new();
    private int _lastDay = -1, _lastMonth = -1, _lastYear = -1;

    public void Start()
    {
        _saveSubToken = _saveSub.Subscribe(OnSaveRequested);

        // Register a default test location and activate it.
        // (Entry layer's LocationCatalog also exists; here we add a module-local
        // fallback so the WorldService always has something to point at.)
        if (_worldService is WorldService ws)
        {
            ws.RegisterLocation(new LocationData
            {
                Id = "test_polygon",
                Name = "Test Polygon",
                Description = "V1 test polygon — flat tile grid.",
                Width = 50,
                Height = 50,
                Seed = 12345,
                TerrainType = TerrainType.Grass,
                LocationType = LocationType.Farm,
                QiDensity = 100,
                QiFlowRate = 1,
            });
            ws.SetActiveLocation("test_polygon");
        }

        _timeService.Speed = _config.DefaultSpeed;
        var t = (_timeService is TimeService ts2) ? ts2.CurrentTime.ToString() : "?";
        Console.WriteLine($"[WorldModule] Started — time {t}, speed {_timeService.Speed}");
    }

    public void Tick(int tickCount)
    {
        // AdvanceTick is not on ITimeService interface — DI-cast to concrete
        // (allowed inside module per DI_AND_EVENTBUS §1.7 rule 2).
        if (_timeService is TimeService ts)
        {
            ts.AdvanceTick();
            var t = ts.CurrentTime;
            _tickPublisher.Publish(new TimeTickEvent(ts.TickCount, t.Day, t.Hour, t.Minute));
            _timeChangedPublisher.Publish(new TimeChangedEvent(1f / 60f, t.Day, t.Hour, t.TimeOfDay));

            // Docs (MODULE_STRUCTURE §WorldContracts): Day/Month/YearChanged fire
            // when the calendar component rolls over. Consumed by quests, buffs,
            // cultivation — anything that reacts to game-calendar boundaries.
            if (t.Day != _lastDay)
            {
                _dayChangedPublisher.Publish(new DayChangedEvent(t.Day));
                if (t.Month != _lastMonth)
                    _monthChangedPublisher.Publish(new MonthChangedEvent(t.Month, t.Year));
                if (t.Year != _lastYear)
                    _yearChangedPublisher.Publish(new YearChangedEvent(t.Year));
                _lastDay = t.Day; _lastMonth = t.Month; _lastYear = t.Year;
            }
        }
    }

    private void OnSaveRequested(in SaveRequestedEvent e)
    {
        Console.WriteLine($"[WorldModule] SaveRequested('{e.SlotName}', {e.SlotType}) noted");
    }

    public void Dispose()
    {
        _saveSubToken?.Dispose();
        Console.WriteLine("[WorldModule] Disposed");
    }
}

public static class WorldModuleServices
{
    public static void Register(IContainerBuilder builder)
    {
        builder.Register<TimeService>(Lifetime.Singleton);
        builder.Register<WorldService>(Lifetime.Singleton);
        builder.Register<ITimeService, TimeService>(Lifetime.Singleton);
        builder.Register<IWorldService, WorldService>(Lifetime.Singleton);
        builder.Register<WorldModule>(Lifetime.Singleton);
    }
}

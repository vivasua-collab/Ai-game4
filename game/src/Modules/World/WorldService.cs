#nullable enable
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.World;

/// <summary>
/// TimeService — engine-agnostic source of game time. Tracks WorldTime
/// (1 tick = 1 game minute), tick counter, and current speed (Pause/Normal/Fast/Quick).
/// Consumers poll ITimeService (DeltaTime/TotalTime/CurrentTime); speed changes
/// go through the bus (TimeSpeedChangedEvent).
///
/// Note: ITimeService (Core) does not expose AdvanceTick on the interface;
/// WorldModule calls it via a concrete cast (DI-cast allowed inside module).
/// 2026-09-06 (аудит, санация мёртвого API): удалены C#-event'ы OnTick/OnTimeChanged —
/// 0 подписчиков, живой паттерн — опрос через ITimeService.
///
/// R17 (аудит-0911 WT-5/E-3): игровое время НЕ сохранялось и НЕ сбрасывалось:
/// LoadGame показывал время прошлой сессии процесса (cold-load маскировался
/// совпадением дефолтов 06:00 день 1), тёплая NewGame #2 начиналась с днём
/// прошлого мира, а время в меню шло вперёд. Теперь: блок "world_time" +
/// IWorldResettable (новый мир = 06:00 день 1, tick 0).
/// </summary>
public sealed class TimeService : ITimeService, ISaveable, IWorldResettable
{
    [Inject] private readonly IPublisher<TimeSpeedChangedEvent> _speedChangedPub = null!;
    // R35 (Фаза 11 / P1-13): bulk-скачок часов при hitch (см. BulkAdvanceTicks).
    [Inject] private readonly IPublisher<TimeHitchedEvent>? _hitchedPub;

    public WorldTime CurrentTime { get; private set; } =
        new WorldTime(GameConstants.START_YEAR, 1, 1, 6, 0); // 06:00 on day 1

    public int TickCount { get; private set; }

    private TimeSpeed _speed = TimeSpeed.Normal;
    public TimeSpeed Speed
    {
        get => _speed;
        set
        {
            if (_speed == value) return;
            _speed = value;
            // Docs (MODULE_STRUCTURE §WorldContracts): speed changes MUST be
            // published on the bus so UI/modules can react. Was missing after
            // the tick-system integration — subscribers saw no speed changes.
            _speedChangedPub?.Publish(new TimeSpeedChangedEvent(_speed));
        }
    }

    public bool IsPaused => Speed == TimeSpeed.Paused;

    // ITimeService — V1 stubs (real values derived from CurrentTime when needed).
    public float DeltaTime { get; private set; }
    public float TotalTime { get; private set; }
    public int CurrentDay => CurrentTime.Day;
    public int CurrentMonth => CurrentTime.Month;
    public int CurrentYear => CurrentTime.Year;
    public int CurrentHour => CurrentTime.Hour;
    public TimeOfDay TimeOfDay => CurrentTime.TimeOfDay;

    public void Pause()
    {
        if (Speed == TimeSpeed.Paused) return;
        Speed = TimeSpeed.Paused;
        Console.WriteLine($"[TimeService] Paused at tick {TickCount}");
    }

    public void Resume()
    {
        if (Speed != TimeSpeed.Paused) return;
        Speed = TimeSpeed.Normal;
        Console.WriteLine($"[TimeService] Resumed at tick {TickCount}");
    }

    /// <summary>
    /// Advance time by one tick (= 1 game minute). Called by WorldModule.
    /// NOT on the ITimeService interface — caller must cast to TimeService.
    /// </summary>
    public void AdvanceTick()
    {
        if (IsPaused) return;
        TickCount++;
        // TIME SYSTEM FIX (2026-08-25, NPC_COMBAT_PREP P0-verification): DeltaTime = 1.0
        // game-second per tick. TIME_SYSTEM.md §5-6: 1 тик = 1 игровая минута =
        // 1 реальная секунда на Normal; все потребители (каст-таймеры CombatService,
        // кулдауны техник, каденции атак NPCModule/TotalTime, регенерация тела,
        // длительности баффов, медитация QiModule «Ци/с») трактуют DeltaTime как
        // «секунды при Normal-скорости». Старый V1-плейсхолдер 1/60 делал ВСЕ
        // таймеры в 60 раз медленнее (каст 0.5с разрешался ~30 реальных секунд,
        // урон не проходил, NPC почти не двигались).
        DeltaTime = 1f;
        TotalTime += DeltaTime;
        CurrentTime = CurrentTime.AddMinutes(GameConstants.TICKS_PER_MINUTE);
    }

    // ══════════════════════════════════════════════════════════════
    // R17 (WT-5): ISaveable — блок "world_time" (календарь + тики).
    // RestoreOrder: восстанавливается ВТОРЫМ (после world) — все
    // остальные блоки и модули живут уже с правильными часами.
    // ══════════════════════════════════════════════════════════════

    public sealed class WorldTimeSaveState
    {
        public int Year;
        public int Month;
        public int Day;
        public int Hour;
        public int Minute;
        public int TickCount;
        public float TotalTime;
    }

    public string SaveKey => "world_time";
    public Type StateType => typeof(WorldTimeSaveState);

    public object CaptureState()
    {
        return new WorldTimeSaveState
        {
            Year = CurrentTime.Year,
            Month = CurrentTime.Month,
            Day = CurrentTime.Day,
            Hour = CurrentTime.Hour,
            Minute = CurrentTime.Minute,
            TickCount = TickCount,
            TotalTime = TotalTime,
        };
    }

    public void RestoreState(object state)
    {
        if (state is not WorldTimeSaveState data || data == null) return;

        CurrentTime = new WorldTime(data.Year, data.Month, data.Day, data.Hour, data.Minute);
        TickCount = Math.Max(0, data.TickCount);
        TotalTime = Math.Max(0f, data.TotalTime);
        DeltaTime = 1f;
        Console.WriteLine($"[TimeService] RestoreState: {CurrentTime}, tick {TickCount}");
    }

    // R17 (E-1): пересборка мира — часы на 06:00 дня 1 (канон NewGame).
    // R35 (Фаза 11 / P1-14) ФИКС: сброс и СКОРОСТИ. Прежде ResetWorld
    // оставлял _speed прежнего мира (Quick/…/Paused): тёплая NewGame
    // наследовала темп, а после паузы новая игра стартовала «замороженной»
    // при SessionState.Playing (GameBoot гейтит по IsPaused). LoadGame-путь
    // проходит ResetWorld ДО RestoreState → скорость нормализуется на
    // ОБОИХ дверях (сейв скорость не хранит; канон: LoadGame → Normal —
    // что обещает и GameSession Data.IsPaused=false; WorldConfig.
    // DefaultSpeed == Normal — единственный источник дефолта).
    public void ResetWorld()
    {
        CurrentTime = new WorldTime(GameConstants.START_YEAR, 1, 1, 6, 0);
        TickCount = 0;
        TotalTime = 0f;
        Speed = TimeSpeed.Normal;
    }

    /// <summary>
    /// R35 (Фаза 11 / P1-13): продвинуть мировые часы СКАЧКОМ на n минут БЕЗ
    /// посимвольной симуляции (hitch: реальный лаг превысил потолок честного
    /// догона — TickCatchUpClock.MaxCatchupSeconds). Календарь/счётчики
    /// остаются честными (реальное время == смоделированное + skipped);
    /// пертиковые эффекты (реген/баффы/NPC) пропущенных минут не получают.
    /// Публикует TimeHitchedEvent + пишет предупреждение в лог.
    /// Календарные Day/Month/Year события НЕ публикуются здесь — их ловит
    /// WorldModule.Tick на первом же тике после скачка (переход даты
    /// легитимен: маркеры отстают от CurrentTime).
    /// Контракт закреплён в ITimeService (Core) — адаптеру не нужен cast.
    /// </summary>
    public void BulkAdvanceTicks(int ticks)
    {
        if (ticks <= 0 || IsPaused) return;
        TickCount += ticks;
        TotalTime += ticks;
        CurrentTime = CurrentTime.AddMinutes(GameConstants.TICKS_PER_MINUTE * ticks);
        _hitchedPub?.Publish(new TimeHitchedEvent(ticks));
        Console.WriteLine($"[TimeService] HITCH: +{ticks} игровых минут скачком без симуляции " +
                          "(долг catch-up выше потолка) — календарь честен, пертиковые эффекты пропущены");
    }
}

/// <summary>
/// WorldService — registry of locations + current active location.
/// Uses LocationData (Core data model). Implements <see cref="IWorldService"/>.
///
/// R17 (аудит-0911 E-3): активная локация НЕ сохранялась (GameSession.LoadGame
/// хардкодил TestPolygon!) — сейв из large_world грузился на чужую локацию.
/// Теперь: блок "world" (CurrentLocationId; реестр локаций — процесс-scoped
/// контент, заполняется WorldModule.Start из LocationCatalog) +
/// IWorldResettable (сброс активной локации; реестр не трогаем).
/// </summary>
public sealed class WorldService : IWorldService, ISaveable, IWorldResettable
{
    [Inject] private readonly IPublisher<LocationChangedEvent> _locationChangedPub = null!;

    // Review этап 6 (P1-2): IPublisher<TravelStartedEvent> удалён — публикуется
    // только реальным travel-pipeline (будущая фаза), не заглушкой TryTravel.

    private readonly Dictionary<string, LocationData> _locations = new();
    private readonly Dictionary<string, FactionInfo> _factions = new();
    private readonly HashSet<string> _discoveredSectors = new();
    private LocationData? _current;

    /// <summary>Internal — current active location data.</summary>
    public LocationData? CurrentLocation => _current;

    public string CurrentLocationId => _current?.Id ?? string.Empty;
    public string CurrentSectorId => _current?.ParentSectorId ?? "0_0";

    // 2026-09-06 (аудит, санация мёртвого API): удалён C#-event OnLocationChanged —
    // 0 подписчиков, дублировал живой контракт шины LocationChangedEvent
    // (публикуется ниже в SetActiveLocation).

    /// <summary>Register a location in the catalogue. Not on interface.</summary>
    public void RegisterLocation(LocationData location)
    {
        if (location == null) throw new ArgumentNullException(nameof(location));
        _locations[location.Id] = location;
    }

    /// <summary>Register a faction. Not on interface.</summary>
    public void RegisterFaction(FactionInfo faction)
    {
        if (string.IsNullOrEmpty(faction.Id)) return;
        _factions[faction.Id] = faction;
    }

    /// <summary>Internal — set active location by ID.</summary>
    public void SetActiveLocation(string locationId)
    {
        if (!_locations.TryGetValue(locationId, out var loc))
        {
            Console.WriteLine($"[WorldService] SetActiveLocation('{locationId}') — NOT FOUND");
            return;
        }
        var old = _current;
        _current = loc;
        Console.WriteLine($"[WorldService] Active location: {loc.Name} (id={loc.Id})");
        _locationChangedPub.Publish(new LocationChangedEvent(old?.Id ?? string.Empty, loc.Id));
    }

    public IReadOnlyList<LocationData> GetAvailableLocations()
    {
        var list = new List<LocationData>(_locations.Values.Count);
        foreach (var v in _locations.Values) list.Add(v);
        return list;
    }

    // === IWorldService ===

    public bool TryTravel(string locationId)
    {
        if (!_locations.ContainsKey(locationId))
        {
            Console.WriteLine($"[WorldService] TryTravel('{locationId}') — unknown location");
            return false;
        }
        if (_current != null && _current.Id == locationId)
        {
            Console.WriteLine($"[WorldService] TryTravel('{locationId}') — уже находимся здесь");
            return false;
        }

        // Review этап 6 (P1-2): честный контракт. Физическое переключение мира
        // (регенерация tile grid, респавн NPC/животных/ресурсов, перемещение
        // игрока) НЕ реализовано — это travel-pipeline будущей фазы. Раньше
        // метод менял CurrentLocationId и публиковал LocationChangedEvent/
        // TravelStartedEvent, grid оставался от СТАРОЙ локации, а возвращал true —
        // API вводил в заблуждение (первый подписчик TimeChangedEvent-типа
        // получал бы мир, не соответствующий CurrentLocationId).
        Console.WriteLine($"[WorldService] TryTravel('{locationId}') — отказ: физический переход " +
                          "между локациями не реализован (travel-pipeline — будущая фаза)");
        return false;
    }

    public LocationInfo GetLocation(string locationId)
    {
        // R11 P2-World (review): раньше Biome был захардкожен Plains,
        // QiDensity передавался, DangerLevel — всегда 0. Теперь Biome
        // выводится из реального TerrainType локации, DangerLevel — из
        // LocationData.DangerLevel (масштабирование спавна NPC, «Задача 1.9»).
        if (_locations.TryGetValue(locationId, out var loc))
        {
            return new LocationInfo(loc.Id, loc.Name, loc.LocationType,
                BiomeFromTerrain(loc.TerrainType), loc.QiDensity, loc.DangerLevel, loc.ParentSectorId);
        }
        // Неизвестный id — защитный дефолт (не «подделка данных»: локации нет).
        return new LocationInfo(locationId, locationId, LocationType.Village,
            BiomeType.Grassland, 0, 0, "0_0");
    }

    /// <summary>
    /// R11 P2-World: семантический маппинг TerrainType → BiomeType (раньше —
    /// хардкод BiomeType.Plains для любой локации). Полный coverage TerrainType:
    /// Grass→Grassland, Dirt/Sand→Steppe, Stone→Highlands, Water_Shallow→Coast,
    /// Water_Deep→Ocean, Snow→Mountains, Ice/Lava→Peak, None/Void→Grassland
    /// (нет данных — биом по умолчанию, документировано).
    /// </summary>
    private static BiomeType BiomeFromTerrain(TerrainType terrain) => terrain switch
    {
        TerrainType.Grass => BiomeType.Grassland,
        TerrainType.Dirt => BiomeType.Steppe,
        TerrainType.Stone => BiomeType.Highlands,
        TerrainType.Water_Shallow => BiomeType.Coast,
        TerrainType.Water_Deep => BiomeType.Ocean,
        TerrainType.Sand => BiomeType.Steppe,
        TerrainType.Snow => BiomeType.Mountains,
        TerrainType.Ice => BiomeType.Peak,
        TerrainType.Lava => BiomeType.Peak,
        _ => BiomeType.Grassland, // None / Void — нет террейна, дефолт
    };

    public FactionInfo GetFaction(string factionId)
    {
        if (_factions.TryGetValue(factionId, out var f)) return f;
        return new FactionInfo(factionId, factionId, string.Empty, 0);
    }

    public FactionRelationType GetFactionRelation(string factionA, string factionB)
    {
        // V1 stub: neutral unless identical.
        if (factionA == factionB) return FactionRelationType.Ally;
        return FactionRelationType.Neutral;
    }

    public IReadOnlyList<string> GetDiscoveredSectors()
    {
        var list = new List<string>(_discoveredSectors.Count);
        foreach (var s in _discoveredSectors) list.Add(s);
        return list;
    }

    public bool IsSectorDiscovered(string sectorId) => _discoveredSectors.Contains(sectorId);

    /// <summary>Internal — mark a sector as discovered. Not on interface.</summary>
    public void DiscoverSector(string sectorId)
    {
        if (!string.IsNullOrEmpty(sectorId)) _discoveredSectors.Add(sectorId);
    }

    // ══════════════════════════════════════════════════════════════
    // R17 (E-3): ISaveable — блок "world" (идентичность локации).
    // RestoreOrder: ПЕРВЫЙ блок — GameSession читает CurrentLocationId
    // для Data.WorldId ПОСЛЕ Load, а TileMapGenPhase генерирует сетку
    // правильного размера из сида локации.
    // ══════════════════════════════════════════════════════════════

    public sealed class WorldSaveState
    {
        public string LocationId = "";
    }

    public string SaveKey => "world";
    public Type StateType => typeof(WorldSaveState);

    public object CaptureState()
    {
        return new WorldSaveState { LocationId = _current?.Id ?? string.Empty };
    }

    public void RestoreState(object state)
    {
        if (state is not WorldSaveState data || data == null) return;
        if (string.IsNullOrEmpty(data.LocationId)) return;

        // Неизвестный ID (реестр пуст/урезан) — оставляем текущую локацию:
        // GameSession.LoadGame сфолбэчит на TestPolygon.
        if (_locations.ContainsKey(data.LocationId))
            SetActiveLocation(data.LocationId);
        else
            Console.WriteLine($"[WorldService] RestoreState: локация '{data.LocationId}' не в реестре — оставлена текущая ({_current?.Id ?? "нет"})");
    }

    // R17 (E-1): пересборка мира — активной локации нет (WorldInitPhase
    // установит её из сессии на NewGame; LoadGame — из блока "world").
    // Реестр локаций — контент процесса, НЕ сбрасываем.
    public void ResetWorld()
    {
        _current = null;
        _discoveredSectors.Clear();
    }
}

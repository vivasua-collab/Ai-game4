# АУДИТ: World + Tile

**Дата:** 2026-09-11
**HEAD на старте:** `d0b065d` (рабочее дерево чистое)
**Скоуп:** Modules/World (WorldConfig, WorldModule, WorldService.cs = TimeService+WorldService),
Modules/Tile (TileConfig, TileModule, TileService, ResourceService) — полностью; контекст:
WorldContracts/TileContracts, WorldInitPhase/TileMapGenPhase/NpcDomainResetPhase, GameSession,
SceneOrchestrator, GameBoot (tick-loop), GameWorldController (harvest F / pickup E / рендер),
GroundItemService/InventoryModule (drop-контракт), ObjectDefaults/GameTile/WorldTime/SeededRandom/
ValueNoise/Constants; docs_v2/03_world (WORLD/TILE/TRANSITION/TIME).
**Режим:** READ-ONLY (код не изменялся).

---

## Сводка

- Файлов прочитано полностью: 8 скоуповых + ~15 контекстовых (Grep-точки GWC).
- Находок: **12** (1×P1, 5×P2, 6×P3). Главный кластер — респавн ресурсов мёртв в
  живой игре (двойная поломка проводки + маскирующий QA-сим), второй кластер —
  отсутствие world-scoped сброса World/Tile (паттерн NpcDomainResetPhase не распространён).
- Сид-детерминизм, anti-dupe harvest, drop-контракт, tick-стоимость, R11-маппинг
  biome/danger — чисто.

---

## Находки

### WT-1 [P1] Респавн ресурсов мёртв: ResourceService.Initialize() никто не вызывает

**Файл:** `Modules/Tile/ResourceService.cs:31-34`; `Modules/Tile/TileModule.cs:36-37`;
`Adapter/Scene/RespawnSimDebug.cs:141-143`.

**Сценарий:** игрок вырубает дерево/камень до истощения → `TryHarvest` →
`RegisterDepletedResource` (ResourceService.cs:89-102) кладёт запись в `_depleted` →
«7 дней»… респауна НИКОГДА не происходит: единственная подписка на `DayChangedEvent`,
через которую вызывается `RespawnCheck`, создаётся в `ResourceService.Initialize()` —
и этот метод не вызван ни в одном месте кода (grep `.Initialize()` по game/src:
UIModule, GeneratorModule, QuestModule, InventoryModule, NPCModule — и
`TileModule.cs:37` инициализирует только **TileService**, не ResourceService).

**Доказательство маскировки:** RespawnSimDebug (GODOT_RESPAWN_DEBUG) пишет в комментарии
«RespawnCheck вызывается по DayChangedEvent», но вызывает
`_resourceServiceImpl.RespawnCheck(futureDay)` **напрямую** (строка 143) — тест PASS
на сломанной проводке. Comment `TileService.cs:88` «Schedule respawn via ResourceService
(7-day timer)» — обещание, не исполняемое в рантайме.

**Последствие:** в живой сессии ресурсы необратимо исчерпываются; docs
(WORLD_SYSTEM §10.2 «ресурсы (регенерация)», TRANSITION_SYSTEM §5.2 «Ресурсы
(регенерация)») не выполняются. Смягчение: замороженное решение «восстановление при
(пере)сборке» покрывает только пересборку, не внутрисессионную регенерацию.

**Фикс (рекоменд.):** `TileModule.Start()` → рядом с `ts.Initialize()` вызвать
`(_tileService как не нужно: резолв ResourceService).Initialize()` (или перенести
подписку DayChanged в TileModule, как сделано для respawn-подписки TileService);
в RespawnSimDebug добавить ассерт «DayChangedEvent реально доходит до ResourceService»
(например, пометить `LastRespawnCheckDay`).

### WT-2 [P2] Респаун считается по дню МЕСЯЦА: wrap 30→1 — истощение ≥24-го числа не респаунится никогда

**Файл:** `Modules/Tile/ResourceService.cs:99` (`DayDepleted = _timeService.CurrentDay`),
`:112` (`currentDay - d.DayDepleted >= d.RespawnDayDelay`); `Core/Data/Structs.cs:168`
(`WorldTime.Day` = день месяца 1..30); `WorldService.cs:70-75` (AdvanceTick/AddMinutes).

**Сценарий:** ресурс истощён 25-го числа. Через 7 «дней» (2-е число следующего месяца)
`currentDay - DayDepleted = 2 - 25 = -23 < 7`; разность остаётся отрицательной до 25-го
следующего месяца, где = 0, и снова уходит в минус. Ресурс не респаунится ВООБЩЕ (даже
при исправленном WT-1). Сим не ловит: `futureDay = CurrentDay + 7` при старте (день 1)
даёт 8 ≤ 30 — wrap не тестируется.

**Фикс:** хранить абсолютный день (`TotalMinutes / 1440` или TickCount/1440) вместо
`WorldTime.Day`; сверять `absDay - d.DayDepleted >= delay`.

### WT-3 [P2] Large World (меню): SetActiveLocation("large_world") тихо проваливается — мир живёт с чужой локацией

**Файл:** `Modules/World/WorldModule.cs:41-54` (в реестр WorldService регистрируется
ТОЛЬКО test_polygon); `Entry/Phases/WorldInitPhase.cs:32-33`;
`Modules/World/WorldService.cs:137-141` (SetActiveLocation: NOT FOUND → `return` без ошибки);
`Adapter/UI/MainMenuController.cs:195-201` (кнопка Large World → `NewGame(1, "large_world")`).

**Сценарий:** игрок выбирает «Большой мир» → фазы читают данные из ДВУХ разных
каталогов: TileMapGenPhase/спавн-фазы — из `LocationCatalog.Find` (находят large_world,
grid 500×500), а WorldInitPhase — через `WorldService.SetActiveLocation` (реестр модуля
не знает large_world → CurrentLocation остаётся test_polygon 50×50).

**Последствия:** `NPCSpawnCompositionService.cs:231` строит состав населения по
`_worldService.CurrentLocation` = test_polygon (Farm → мирный состав) вместо large_world
(WildLands → бандиты/монстры); `NPCSpawnerService._currentLocationId` = test_polygon →
NPC.CurrentLocation и DangerLevel-масштабирование от чужой локации; `Data.WorldId` =
large_world — тройное рассогласование. Это остаток R11-P2 «две независимые модели
карты» (фикс закрыл только TileModule, не сборку).

**Фикс:** WorldInitPhase регистрировать локацию из LocationCatalog перед
SetActiveLocation (или WorldModule.Start регистрировать весь LocationCatalog.GetAll()).

### WT-4 [P2] Пересборка мира: у домена World/Tile нет аналога ResetWorld (двойное состояние)

**Файл:** `Entry/GameSession.cs:143` (на LoadGame сбрасывается ТОЛЬКО NPC-домен);
`Entry/Phases/TileMapGenPhase.cs` (SkipOnLoad=true унаследован → на LoadGame grid
НЕ генерируется); `Modules/Tile/ResourceService.cs:28` (`_depleted` без сброса);
`Modules/World/WorldService.cs:105-108` (реестры `_locations/_factions/_discoveredSectors`,
`_current` переживают мир).

**Сценарий (тёплая пересборка — путь открыт QA-симом REASSEMBLY и стейт-машиной
GameSession, в живом UI возврата в меню пока нет):**
- NewGame #2: grid перегенерируется (фаза 2), но `_depleted` от прошлого мира
  накапливается навсегда (утечка списка); при оживлённом респауне записи со старыми
  координатами легли бы на НОВУЮ сетку (IsInBounds пропустит координаты < 50).
- LoadGame: grid вообще не пересоздаётся и не восстанавливается из сейва (тайлов в
  сейве нет) → тёплый процесс показывает сетку прошлой сессии с её мутациями, свежий
  процесс — чистую из сида: одна и та же загрузка даёт РАЗНЫЕ миры. Плюс
  WorldService._current остаётся от прошлой сессии (LoadGame не вызывает
  SetActiveLocation вовсе), а `Data.WorldId` захардкожен в test_polygon
  (GameSession.cs:153).

**Фикс (паттерн NpcDomainResetPhase):** TileDomainResetPhase (NewGame) +
GameSession.LoadGame: `ResourceService.ResetWorld()` (clear `_depleted`) +
`TileService.Generate(...)` из сейв-сида (или честный SkipOnLoad=false с последующим
восстановлением дельты) + `WorldService.ResetWorld()`.

### WT-5 [P2] TimeService не сбрасывается и не восстанавливается: время — не в сейве, тёплая пересборка наследует прошлое

**Файл:** `Modules/World/WorldService.cs:27-30` (CurrentTime private set, старт
1864-01-01 06:00 — единственная инициализация), `:76-91` (AdvanceTick — единственный
мутатор); `Entry/GameSession.cs:85,156` (Data.WorldTime — НОВЫЙ дефолт, TimeService не
трогается); TimeService не входит в 8 ISaveable-блоков (body/inventory/techniques/
formation/npc/technique_slots/charger/save_meta).

**Сценарий:** (a) тёплый NewGame #2 — время продолжает прошлую сессию (день N вместо
дня 1); (b) LoadGame — игровое время/дата (HUD: `GameWorldController.cs:1034` читает
`Time.CurrentTime`) не соответствуют сейву: продолжает предыдущую сессию процесса.
Единственный живой путь загрузки (свежий процесс → MainMenu → Load) маскирует: время =
стартовое 06:00 день 1, что «почти» совпадает с дефолтом. R11-вопрос про round-trip
World-стейта: biome/danger честны в рантайме (GetLocation из LocationData), но
**мировое время и идентичность локации в сейве отсутствуют** — round-trip для них
не реализован.

**Фикс:** ISaveable для TimeService (TickCount/CurrentTime) + Reset-to-default в
WorldInitPhase (NewGame) / RestoreState (LoadGame).

### WT-6 [P2] Истощение тайла не возвращает TileFlags.Passable — срубленный лес навсегда «твёрдый» для NPC

**Файл:** `Modules/Tile/TileService.cs:81-91` (depleted: `Object=None`, флаги не
восстанавливаются); `Core/Data/GameTile.cs:56-60` (IsWalkable требует бит Passable),
`:124-127` (CreateWithObject сбрасывает бит для непроходимых объектов).

**Сценарий:** дерево/камень/руда созданы с сброшенным Passable → полный сбор →
Object=None, но бит не возвращён → `IsWalkable=false` навсегда (респаун, который
вернул бы объект, мёртв — WT-1). Потребители IsWalkable: AnimalService.cs:571-611
(блуждание), NPCSpawnCompositionService.cs:335, GroupSpawnPhase/HumanNPCSpawnPhase
(спавн) → животные/спавн обходят пустые клетки. Игрок не затронут (движение GWC
`:1363` IsWalkable не проверяет).

**Фикс:** в ветке Depleted — `updated.Flags |= TileFlags.Passable` (если terrain
проходим) или восстановление флагов через `GetTerrainFlags(updated.Terrain)`.

### WT-7 [P3] RespawnDayDelay=7 хардкодом — ObjectDefaults.RespawnDays игнорируется

**Файл:** `Modules/Tile/ResourceService.cs:100`; `Core/Data/ObjectDefaults.cs:84-148`
(таблица: oak/pine 7, birch 5, bush 3, rock 14, ore 30, herb 2 — «единый источник
истины»). Данные есть, не читаются. Три несогласованных источника: код 7 / таблица
2..30 / docs TRANSITION §11.1 (руда 3 дня, трава 1 день).

**Фикс:** `RespawnDayDelay = ObjectDefaults.GetRespawnDays(tile.Object)` в
RegisterDepletedResource (0 = не респаунится).

### WT-8 [P3] WorldConfig — мёртвый конфиг с неверными значениями

**Файл:** `Modules/World/WorldConfig.cs:17-60`; читается ТОЛЬКО `DefaultSpeed`
(`WorldModule.cs:57`). `StartHour=12` против фактического 06:00 (WorldService.cs:28);
`StartLocationId="start_village"` — такой локации нет в коде (реально test_polygon);
`AutoSaveIntervalTicks=60` дублируется собственным механизмом SaveModule
(`SaveModule.cs:61-65`, тики%60 + AutoSaveIntervalMinutes); FactionAttitudeWeight/
RandomEventChancePerTick/Min/MaxEventDurationTicks — не потребляются (мировых событий
нет). Регистрация: WorldConfig вообще не в DI (WorldModule создаёт `new`).

**Фикс:** вычистить мёртвые поля или перенести живые значения (StartYear →
GameConstants.START_YEAR уже совпадает 1864) и убрать дубль.

### WT-9 [P3] Фиктивные Day/Month/YearChanged на первом тике

**Файл:** `Modules/World/WorldModule.cs:30` (`_lastDay/_lastMonth/_lastYear = -1`),
`:80-88` (первый тик: Day≠-1 → публикует DayChanged(1)+MonthChanged(1,year)+YearChanged).

**Последствие:** day-subscribers (NPCQiRegenService, NPCRelationshipService,
QuestProgressTracker, ResourceService) получают лишний «наступивший день/месяц/год»
в первый тик сессии (двойная day-обработка на дне 1). Малый эффект, но событие
календарной границы лжёт.

**Фикс:** в Start() инициализировать сентинелы текущими компонентами времени.

### WT-10 [P3] IWorldService.TryTravel XML-контракт расходится с реализацией

**Файл:** `Core/Interfaces/IWorldService.cs:36-41` («Публикует TravelStartedEvent и
(позже) LocationChangedEvent») — реализация `WorldService.cs:157-180` ВСЕГДА возвращает
false и ничего не публикует (честный стаб, review этап 6 P1-2). Интерфейсный XML —
устаревший. Сам честный-false — OK-BY-DESIGN (travel-pipeline будущая фаза;
подтверждено RespawnSim/QuestSim: travel-квест не регистрируется).

**Фикс:** правка XML-комментария.

### WT-11 [P3] TileMapGeneratedEvent — мёртвый контракт

**Файл:** `Core/Messaging/Contracts/TileContracts.cs:98-108`; публикуется ТОЛЬКО
fallback-путём `TileModule.cs:80`; каноническая генерация `TileMapGenPhase` событие
НЕ публикует; подписчиков 0 (grep по game/src). Событие «карта сгенерирована» не
несёт информации о реальной генерации.

**Фикс:** публиковать из TileMapGenPhase (или удалить контракт до появления
потребителей).

### WT-12 [P3, latent] TryHarvest: пустой HarvestResult тихо обнуляет ресурс без Depleted

**Файл:** `Modules/Tile/TileService.cs:67-77` + `ResourceService.cs:73-74`.
Если `Harvest()` вернёт Empty (условие: `(int)tile.ResourceAmount == 0` при
дробном остатке < 1), TryHarvest всё равно вернёт true, запишет
`ResourceAmount = 0` (из Empty), БЕЗ Depleted/респауна/ResourceDepletedEvent →
объект висит «недособранным» навсегда. С текущими таблицами ObjectDefaults не
срабатывает (все ResourceMax делятся нацело либо остаток ≥ 1), латентно при правке
баланса на нецелые значения.

**Фикс:** в TryHarvest проверять `result.Amount <= 0 → return false` (или
трактовать как Depleted).

---

## Проверено чисто

- **Сид-детерминизм:** SeededRandom (xorshift64*, avalanche-микс сида, xor-нуль
  защита) и ValueNoise (fBm+domain warp, детерминированный hash) — чистые, без
  env/времени; `TileService.Generate` потребляет rng в фиксированном порядке
  (beaches → objects) — одинаковый (seed,w,h,terrain) → одинаковая карта.
  GODOT_MAP_SIZE override — явно задокументированный perf-путь (TileModule.cs:61-71).
- **Двойной harvest:** TryHarvest синхронный, single-threaded; каждый вызов уменьшает
  ResourceAmount; истощение атомарно чистит Object/ResourceId/IsHarvestable.
  Dupe-векторов не найдено.
- **Harvest (F) flow:** GWC `:2242-2322` — курсор→тайл, bounds-check, Чебышёв ≤ 3,
  тосты по веткам отказа, RefreshObjectLayer (viewport-culled _Draw, редро ~10 Гц —
  не O(250k) на F), RefreshExternally инвентаря.
- **Drop-контракт:** Harvest → ItemAddRequestEvent(itemId) → InventoryModule
  overflow → DropItemsNearPlayer → ItemDroppedEvent → renderer; E-pickup →
  TryPickupNearest (валидация itemId ДО удаления — P1-5 фикс) → ItemPickedUpEvent +
  ItemAddRequestEvent. Цикл замкнут, потерь/дупов нет.
- **Tick-стоимость при 500×500:** WorldModule.Tick O(1) (2 publish); TileModule.Tick —
  no-op; генерация одноразовая в фазе; SmoothBiomes — один массив counts (без
  Dictionary на тайл); Dictionary-печать распределения — только консоль. Тяжёлых O(n)
  на тик нет.
- **R11 GetLocation:** Biome из реального TerrainType (полный coverage-свич,
  документированный дефолт), DangerLevel из LocationData — честно в рантайме.
- **SmoothBiomes counts[16]:** BiomeType = 9 значений + 7 алиасов с shared values →
  Enum.GetValues = 16, индексы 0..8 — выхода за границы нет.
- **Контракты:** readonly struct, `in`-параметры, HarvestResult.Zero-alloc —
  соответствуют DI_AND_EVENTBUS-паттерну; единственный издатель
  ResourceHarvestedEvent — TileService (двойной publish исключён, ResourceService.cs:83).
- **Пляжи/объекты генерации:** сан-чеки (ore перед rocks, herbs 1% один ролл —
  B-1 аудит-2 фикс на месте).

## Соответствие docs_v2

| Док (docs_v2/03_world) | Заявлено | Код | Вердикт |
|---|---|---|---|
| WORLD_SYSTEM §10.2 | ресурсы регенерируют (semi-persistent) | респаун мёртв (WT-1/2/7) | P2 drift поведения |
| TRANSITION_SYSTEM §5.2-5.3 | таймер памяти: ресурсы/враги; NPC 1 день (заморожено) | восстановление только при пересборке (замороженное решение), внутрисессионной регенерации нет | drift заявленного vs замороженного — P2 (WT-1) |
| TRANSITION_SYSTEM §11.1 | руда 3 дня, трава 1 день, элита 7 | код 7 хардкод; ObjectDefaults 30/2/14 | P3 числа расходятся втроём |
| TILE_SYSTEM §2/§9 | 4 слоя, ~20 полей, Z −5..+5 | один struct GameTile (агрегат) | OK — сам док допускает агрегированную модель; Z/Qi/температура тайла не реализованы (концепт) |
| TILE_SYSTEM §10.2, §12 | seed+delta сохранение тайлов | тайлов в сейве нет вообще | концепт; для LoadGame даёт WT-4 |
| TILE_SYSTEM §3.1/§11 | тайл 2×2 м; 1м=32px | TILE_PIXELS=64, 50×50=100×100м | OK |
| TIME_SYSTEM §2-3 | 1 тик=1 мин; 0/1/5/15 tps | TimeSpeed enum 0/1/5/15; GameBoot accumulator | OK |
| TIME_SYSTEM §5.3 | 1864, 30/12/24 | GameConstants + WorldTime | OK (но сейв времени — WT-5) |
| TIME_SYSTEM §9.3 | автосейв каждые 60 тиков | SaveModule тик%60 | OK (WorldConfig-дубль — WT-8) |
| WORLD_SYSTEM §2-5 | мир/чанки/сектора 200000 км | V1: каталог 2 локации + world_map placeholder | OK (концепт-статус заявлен) |
| WORLD_SYSTEM §6.2 | per-location loading, одна активная | одна активная, TryTravel=false | OK-BY-DESIGN (travel — будущая фаза); интерфейсный XML лжёт — WT-10 |

*Аудит завершён. Код не изменялся (READ-ONLY). Все находки — с рекомендациями для
следующей fix-сессии; приоритет: WT-1 (+WT-2 в одном фиксе), затем WT-3, WT-4/5
(паттерн сброса World/Tile), WT-6.*

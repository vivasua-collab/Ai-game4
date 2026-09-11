# АУДИТ: Entry-слой

**Дата:** 2026-09-11, HEAD `d0b065d`, READ-ONLY.
**Скоуп:** Entry целиком — 6 корневых файлов + CoreProjectInfo + все 16 фаз
Phases/ (прочитаны полностью); проводка: GameBoot, MainMenuController,
GameWorldController (_Ready/_ExitTree/токены), SceneContracts,
ISceneAssemblyPhase, IGameSession, Core DI Container/ContainerAdapter,
NPCModule.ResetWorld-цепочка, AnimalService, WorldService/WorldModule,
TileModule, SaveModule/ISaveable-реестр, TechniqueService, ItemDatabaseService,
InventoryService.RestoreState, ReAssemblySimDebug, WeaponVisualCatalog;
docs_v2: ARCHITECTURE §6.1/§6.2, FILE_TREE §4, TESTING_RULES §0.1 (33 хука).
~28 файлов.

---

## Сводка

- **Находок:** 10 (P2×4, P3×6). P1 нет: 16-фазный конвейер как механика
  (Reset → SkipOnLoad-гейт → CanExecute → Running → Completed/Failed → rethrow)
  работает корректно (верифицирован REASSEMBLY-QA); таблица фаз в
  ARCHITECTURE §6.1 совпадает с кодом 16/16.
- **Главное (системное):** нет единого ResetWorld-контракта — сброс
  world-scoped доменов размазан и асимметричен NewGame/LoadGame (E-1);
  SkipOnLoad систематически путает «генерацию» с «восстановлением из сейва»,
  которого для Player/World/Tile/Animal нет вовсе (E-3 — корень известных
  G-2/NPC-1/QST-1); провал фазы не останавливает смену сцены (E-2);
  тик-луп не гейтится сессией — мир симулирует и автосейвит за главным
  меню (E-4).
- Тёплый рестарт достижим только QA-харнессом (ReAssemblySimDebug зовёт
  оркестратор напрямую); пути «возврат в меню» из игры нет — ветка
  GameSession.LoadGame-сброса (R14) живёт defensive-но, UI-флоу до неё не
  доходит. Код НЕ изменялся.

---

## Находки

### E-1 [P2] Системная: сброс world-scoped доменов размазан и асимметричен режимам
**Файл:** `GameSession.cs:143` (LoadGame сбрасывает ТОЛЬКО NPC);
`NpcDomainResetPhase.cs:32-36` (фаза 0: NPC + кэш оружия, NewGame-only);
`WorldInitPhase.cs:35` (TechniqueRegistry.Clear — фаза SkipOnLoad=true → на
LoadGame НЕ выполняется); `AnimalSpawnPhase.cs:40` (ClearAnimals — единственное
место, внутри генеративной фазы); `PlayerService.cs:121-127` (Spawn идемпотентен
→ при тёплой повторной NewGame игрок НЕ пересоздаётся); `InventoryService.cs:565`
(_slots.Clear только в RestoreState).
**Сценарий:** тёплая повторная сборка (меню → NewGame / QA REASSEMBLY):
NPC-домен чистится ✓ (эталон R13/R14); животные НЕ чистятся ResetWorld (NPC-1,
кросс-реф); игрок остаётся со статами/позицией прошлого мира, StartingGearPhase
ДОБАВЛЯЕТ второй набор в непочищенный инвентарь; FormationService держит
формацию прошлого мира; TechniqueRegistry на тёплом LoadGame не чистится
(WorldInitPhase skipped) — реестр накапливается (читателей почти нет:
дедуп/чит-панель — поэтому P2, не P1).
**Фикс (масштабируемый паттерн):** контракт `IWorldResettable { void
ResetWorld(); }` в Core.Interfaces; реализуют NPCModule (уже), AnimalService,
TechniqueRegistry, FormationService, PlayerService, InventoryService; фаза 0
(→ WorldDomainResetPhase) резолвит `ResolveAll<IWorldResettable>()` — новые
модули подключаются декларативно; GameSession.LoadGame зовёт ТОТ ЖЕ набор до
RestoreState (сейчас хардкод `_npcModule.ResetWorld()`).

### E-2 [P2] Провал сборки не останавливает вход в мир: сцена меняется всегда
**Файл:** `GameSession.cs:100-104,169-173` (catch → лог → SetState(MainMenu),
без rethrow/результата); `MainMenuController.cs:184-193,224-235`
(ChangeSceneToFile(GameWorld) после NewGame/LoadGame безусловно — их try/catch
мёртв: методы глотают исключения сами); `GameEntryPoint.cs:91-120` (Tick не
гейтится по Session.State → тики продолжаются).
**Сценарий:** исключение в любой фазе (например CoreValidation при сломанном
DI) → полусобранный мир + сцена GameWorld + симуляция тикает + State=MainMenu.
«Мир жив наполовину» — именно этот путь.
**Фикс:** NewGame/LoadGame возвращают bool (IGameSession сейчас void) либо
кидают; MainMenuController гейтит смену сцены по результату.

### E-3 [P2] SkipOnLoad-контракт «состояние восстанавливается из сейва» не выполняется для Player/World/Tile/Animal
**Файл:** `AbstractSceneAssemblyPhase.cs:53` (SkipOnLoad=true по умолчанию,
reason-строка «состояние из сейва»); `GameSession.cs:150-159` (после Load:
Data.WorldId=TestPolygon ХАРДКОД, WorldTime=06:00, StartVariant=1 — «свежие»
значения, сейв их не хранит); ISaveable-реестр = только Body/Inventory/
Techniques/TechniqueSlot/Formation/NPC/Charger/save_meta (`SaveModule.cs:51`)
— PlayerService/TimeService/WorldService/TileService/AnimalService/QuestService
НЕ ISaveable.
**Сценарий:** cold-load: игрок на (25,25) от PlayerModule.Start-фолбэка (не из
сейва); мировое время = 06:00 + время жизни процесса; сейв из large_world
грузится на фолбэк-грид 50×50 (TileMapGenPhase skipped, TileModule.Start
сгенерил test_polygon) — NPC из сейва с координатами до (500,500) вне карты.
Корень G-2 (каталог предметов), QST-1 (квесты), NPC-1 (звери).
**Фикс:** минимум — StartingGearPhase разделить: регистрация канонических ID в
БД (QiStoneSeeder/материалы/стрелы/слиток) — SkipOnLoad=false (контент, не
генерация), выдача набора — SkipOnLoad=true; WorldId/WorldTime/позиция игрока —
в сейв (session-блок ISaveable) и TileMapGen → «пере-генерация по WorldId из
сейва» вместо skip.

### E-4 [P2] Тик-луп не гейтится сессией: симуляция и автосейв живут в главном меню
**Файл:** `GameBoot.cs:93-124` (гейт только TimeService.IsPaused/Speed);
`GameEntryPoint.cs:93` (без проверки Session.State); `WorldModule.cs:57` +
`WorldConfig.cs:30` (Speed=Normal с бутстрапа); ResetTime/TickCount=0 не
существует (grep: `CurrentTime =` только AddMinutes — WorldService.cs:90);
`SaveModule.cs:59-69` (автосейв каждые 60 тиков).
**Сценарий:** пользователь в MainMenu → тики идут с бутстрапа: fallback-мир
(грид test_polygon, игрок (25,25), fallback-звери) симулирует; SaveModule пишет
`autosave_NNNN.json` от fallback-мира (мусорные сейвы, счётчик от бутстрапа);
NewGame НЕ сбрасывает часы — новый мир начинается с 06:00+время-в-меню;
LoadGame время не восстанавливает (часть E-3).
**Фикс:** гейт тика по `Session.State is Playing or Paused` (GameBoot или
GameEntryPoint); сброс TimeService.CurrentTime/TickCount в NewGame.

### E-5 [P3] Мёртвые события сборки: CompletedWithErrors и все Scene*-события — 0 подписчиков
**Файл:** `SceneContracts.cs:64-90` (CompletedWithErrors: «Q16-E01 FIX:
ContinueOnError=true» — ContinueOnError в оркестраторе НЕ существует, publish
нет); `SceneOrchestrator.cs:97,131,140,149,154` (5 паблишеров); grep:
`ISubscriber<Scene*>` — 0 по src. ARCHITECTURE §5 (строка 191) обещает
CompletedWithErrors в реестре контрактов — ссылка на мёртвый тип.
**Фикс:** удалить тип + строку доки, ЛИБО реализовать ContinueOnError-режим и
подписчика (loading-screen).

### E-6 [P3] Doc-drift: счётчик фаз «10/15» против фактических 16; FILE_TREE §4 устарел
**Файл:** `SceneOrchestrator.cs:16` («15-phase pipeline»);
`GameLifetimeScope.cs:93` («orchestrator + 10 phases») и `:28` («16 module» —
фактически 17 регистраций, строка 72 сама говорит «17 module services»);
`ARCHITECTURE.md:135` («(10 фаз)» при верной таблице §6.1); `FILE_TREE.md:
379-403`: «(10 фаз)», нумерация до сдвига ревью-1 (AnimalSpawn «Фаза 5»,
Finalize «14» — фактически 6/15), фантомные `SceneAssemblyConfig.cs`/
`SceneAssemblyLogger.cs`/`MessagingRegistrar.cs` и папка `Entry/UI/`, НЕ
заявлен существующий `LocationCatalog.cs` (в docs_v2 не упомянут).
**Фикс:** синхронизировать числа/список; §6.1-таблица — эталон.

### E-7 [P3] Оркестратор: нет валидации уникальности Order, нет таймаута фазы, ct не доходит до фазы
**Файл:** `SceneOrchestrator.cs:66-67` (комментарий «равных Order быть НЕ
ДОЛЖНО» — проверки нет); `:104` (ct-гейт только МЕЖДУ фазами;
`ISceneAssemblyPhase.ExecuteAsync()` — Core/Interfaces/ISceneAssemblyPhase.cs:93
— не принимает CancellationToken: реально-асинхронная фаза станет
неканселируемой); таймаутов на фазу нет (все фазы сейчас синхронные
Task.CompletedTask — риска зависания нет). RegisterPhase не дедуплицирует
явные повторные регистрации (:169 дедупит только авто-обход).
**Фикс:** assert уникальных Order в EnsurePhasesLoaded; ct в контракт фазы.

### E-8 [P3] Мёртвый код сессии: Pause/Resume/AdvanceFrame; doc-ложь про GamePausedEvent
**Файл:** `GameSession.cs:181-198` (Pause/Resume — 0 вызовов; GamePaused/
GameResumedEvent — 0 подписчиков; реальная пауза — прямой TimeService.Pause()
в GameWorldController:262/1157/1548/…); `GameSession.cs:37,201`
(AdvanceFrame/_frameCounter — 0 вызовов); ARCHITECTURE §6 GameSession-таблица:
«Pause | Подписка на GamePausedEvent» — подписки не существует.
**Фикс:** пометить как задел под escape-меню (связать Session.Pause ↔
TimeService) либо вычистить; обновить строку доки.

### E-9 [P3] Слои: Entry → Adapter-зависимость и QA-env в Entry-фазе
**Файл:** `NpcDomainResetPhase.cs:35` (`Adapter.Scene.WeaponVisualCatalog.
ResetCache()` — Entry (engine-agnostic) тянет Adapter (Godot): обратное
направление слоёв; компилируется одним csproj, потому молчит);
`TechniqueGrantPhase.cs:100` (`GODOT_FORMATION_TEST` — единственный QA-env,
прочитанный внутри Entry-фазы; остальные 32 хука — в Adapter).
**Фикс:** ResetCache — через IResettable-контракт (см. E-1) в Adapter-регистрации;
FORMATION_TEST-блок — перенести в Adapter-сим.

### E-10 [P3] GameBoot._ExitTree не диспозит контейнер — Dispose() модулей не выполняется
**Файл:** `GameBoot.cs:130-134` (только лог; комментарий «modules clean up via
their own _ExitTree» неверен — модули не Godot-ноды); `Container.cs:290-309`
(Dispose реализован: все IDisposable-синглтоны) — вызовов `Container.Dispose()`
нет (grep). Косметика при выходе из процесса; критично станет при
пересоздании контейнера в том же процессе.
**Фикс:** `Container?.Dispose()` в _ExitTree.

---

## Проверено чисто

- **Оркестратор:** Reset() всех фаз перед прогоном (:94-96); SkipOnLoad-гейт
  ДО CanExecute (:108-127); lifecycle Running→Completed/Failed + rethrow
  (:130-144); PhasesSkipped — реальное число (:154); стабильный OrderBy
  (A-1 держится); авто-дискавери дедупится (:169).
- **Порядок фаз vs зависимости:** NpcDomainReset(0) до спавнов 6/7/8 ✓;
  TileMapGen(2) до позиционных ✓; WorldInit(3) до NPC-композиции ✓;
  StartingGear(5) до HumanNPCSpawn(7) ✓; PreGen(13) до Grant(14) ✓;
  Finalize(15) последняя ✓; дубль-публикация SceneReady удалена ✓.
  Идемпотентность: GroupSpawn сбрасывает _placedGroupCentres (:65),
  HumanNPCSpawn — composition.Reset (:57), AnimalSpawn — ClearAnimals,
  ItemDatabase.Register — замена с логом (:75-84), Spawn идемпотентен.
- **GameLifetimeScope:** все регистрации ДО Build(), резолвы после
  (GameBoot:46-61); порядок 17 модулей = DI_AND_EVENTBUS §1.2;
  adapter-override после модулей до Build ✓; Build ровно 1 на процесс
  (GameBoot.cs:46) — «двойной регистрации» нет; RegisterInstance-паттерны
  корректны (EventBus concrete-key; SaveFileHandler — осознанный override).
- **GameEntryPoint:** R16-фикс на месте — стек (до 6 кадров) в логе тика
  (:105-112); идемпотентный Start; re-entrancy guard; исключения глотаются
  с логом — процесс не роняется ✓.
- **CoreValidationPhase:** SkipOnLoad=false — DI-валидация и на Load ✓;
  резолв 6 ключевых интерфейсов fail-fast.
- **LocationCatalog:** 3 записи self-consistent; Find null-safe, фазы
  fallback на TestPolygon; каталог корректен — дыра large_world в
  WorldService-реестре (WT-3, кросс-реф). DangerLevel=0 у WildLands
  (LocationData.cs:168 default; состав спасает LocationType).
- **QA-гейты (§0.1, 33 хука):** *SimDebug монтируются в GameWorldController.
  _Ready (:359-455) — гейтятся полной сборкой через GODOT_NEWGAME=1
  (MainMenuController.cs:49-53). SkipOnLoad симами не задействуется
  (SaveLoadSimDebug дергает _saveService.Load напрямую; REASSEMBLY зовёт
  оркестратор напрямую). GODOT_FORMATION_TEST — внутри фазы 14.
- **GameWorldController._ExitTree:** 16 токенов диспозятся попарно (:2454-
  2509) — утечки EventBus-подписок Godot-нод при пересборке сцены нет.
- **NpcDomainResetPhase как эталон:** полный путь DespawnNPC + Corpse/Group
  + кэш оружия; GameSession.LoadGame сбрасывает ДО _save.Load ✓ (R14).
- **Замороженные решения:** Qi=long, player/player_0 — не нарушены; G-13
  (seed=TickCount) — признан докой, не репорчу как новый.

---

## Соответствие docs_v2

Таблица фаз (ARCHITECTURE.md §6.1, строки 258-275 — сверена построчно):

| # | Фаза | Дока: SkipOnLoad | Факт | Вердикт |
|---|------|-----------------|------|---------|
| 0 | NpcDomainReset | true | = | ✓ |
| 1 | CoreValidation | false | = | ✓ |
| 2 | TileMapGen | true | = | ✓ (контракт E-3) |
| 3 | WorldInit | true | = | ✓ (сброс реестра только NewGame — E-1) |
| 4 | PlayerSpawn | true | = | ✓ (восстановления из сейва нет — E-3) |
| 5 | StartingGear | true | = | ✓ (регистрация БД тоже skip — G-2/E-3) |
| 6 | AnimalSpawn | true | = | ✓ (NPC-1 — кросс-реф) |
| 7 | HumanNPCSpawn | true | = | ✓ |
| 8 | GroupSpawn | true | = | ✓ |
| 9 | FormationInit | false | = | ✓ |
| 10 | ChargerInit | false | = | ✓ |
| 11 | QuestInit | false | = | ✓ |
| 12 | UIInit | false | = | ✓ |
| 13 | PreGenTechnique | true | = | ✓ |
| 14 | TechniqueGrant | true | = | ✓ (G-13 — признано) |
| 15 | Finalize | false | = | ✓ |

Описания фаз в §6.1 совпадают с кодом построчно (сброс реестра техник в
WorldInit, BuildSpecified в PreGen, LoadGame-примечание фазы 0).

Прочее: ARCHITECTURE §6.2 LoadGame-порядок (Load → RunAssembly(LoadGame)) = код ✓;
«Pause | Подписка на GamePausedEvent» — ложь (E-8); SceneContracts-строка §5
упоминает мёртвый CompletedWithErrors (E-5); ARCHITECTURE:135 и FILE_TREE §4 —
«10 фаз», устаревшая нумерация, фантомные файлы, нет LocationCatalog (E-6);
TESTING_RULES §0.1 — 33 хука соответствуют коду ✓.

---

*Аудит READ-ONLY. Код не менялся. Приоритет фиксов: E-1 (IWorldResettable —
закрывает NPC-1 и часть E-3-проявлений) → E-3 (session-блок сейва + StartingGear
сплит) → E-2 → E-4 → P3.*

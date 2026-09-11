# АУДИТ: Core-слой (DI / EventBus / Contracts / Data / Interfaces)

**Дата:** 2026-09-11
**HEAD на старте:** `d0b065d`
**Скоуп:** READ-ONLY. `game/src/Core` целиком (DI×2, Events/EventBus, Messaging/Contracts×24,
Data×40, Interfaces×48, Helpers/PlayerIdResolver, CoreProjectInfo) + lifecycle-контекст
(Entry/GameSession, GameEntryPoint, SceneOrchestrator, GameLifetimeScope, Phases/CoreValidationPhase,
Adapter/Scene/GameBoot) + точки pub/sub всех модулей и Adapter. Программная верификация:
все 158 контрактов × {IPublisher/ISubscriber/Publish(new/Subscribe/MessageHandler} (multiline,
namespace-aware), все 63+6 enum'ов на дубли значений, суммы морфотаблиц, DI-регистрации 17 модулей.

---

## Сводка

- **Прочитано:** ~50 файлов полнотекстово (все DI/Events/Contracts/Structs/ключевые Data),
  остальные Data/Interfaces — сигнатурно + программные проверки; pub/sub-карта по всему `game/src`.
- **Находок:** **P1×0, P2×4, P3×17**. Боевой контур (EventBus механика, DI-резолв, ключевые
  контракты) работает; главные проблемы — мёртвые проводки контрактов (sub-only) и
  неизолированные исключения подписчиков.
- **Сироты-контракты:** 21 полное событие-сирота (0 pub, 0 sub), 10 sub-only, 41 pub-only
  (из 158; +2 DTO — HarvestResult/SaveInfo — активны как возвращаемые типы, не события).

---

## Находки

### E-1 [P2] Исключение в подписчике прерывает доставку остальным + «застревает» re-entrant очередь

**Файл:** `Core/Events/EventBus.cs:112-115` (вызов `handlers[i](in message)` без try/catch), `:84-98`.
**Сценарий:** подписчик N из M бросает исключение → подписчики N+1..M не получают событие
(доставка прерывается); исключение уходит в публикующий модуль. `GameEntryPoint.Tick`
(`Entry/GameEntryPoint.cs:99-113`) ловит — тик выживает, но часть системы молча не уведомлена.
Хуже: код обработки `_pendingQueue` (`:90-98`) стоит ПОСЛЕ finally — при исключении не
выполняется; отложенные re-entrant сообщения остаются в очереди и выполнятся при СЛЕДУЮЩЕМ
`Publish` любого типа (нарушение порядка доставки, доставка «задним числом»).
**Доказательство:** трассировка Publish: try { InvokeHandlers } finally { Remove } → выход по
исключению минует `if (_publishing.Count == 0 && _pendingQueue.Count > 0)`.
**Фикс:** try/catch вокруг каждого `handlers[i]` (лог+continue); drain очереди перенести в finally.

### C-1 [P2] StatChangedEvent: подписка BodyModule мертва — StatService не публикует (пересчёт HP от VIT, П.24)

**Файл:** `Modules/Body/BodyModule.cs:50,70,147` (ISubscriber+OnStatChanged →
`RecalculateHPFromVitality`); `Modules/Player/StatService.cs` — 0 вызовов Publish вообще;
`Core/Interfaces/IStatService.cs:15` («с публикацией StatChangedEvent»).
**Сценарий:** рост Vitality (сон/развитие) не пересчитывает MaxHP частей тела: `rg
RecalculateHPFromVitality` → единственный вызов из мёртвой подписки. BodyEnhancementSystem
поднимает HP только для перков закалки (SetMaxHP, другой путь). Проводка BODY_SYSTEM П.24
мертва. [UNVERIFIED — глубина компенсации в StatService.ConsolidateSleep не трассирована,
но Publish в файле отсутствует полностью]
**Фикс:** публиковать StatChangedEvent в StatService.ModifyStat/SetStat (или прямой вызов
IBodyService.RecalculateHPFromVitality из PlayerModule).

### C-2 [P2] CultivationWindowToggleRequestedEvent: слушатель есть, публикатора нет (окно K открывается напрямую)

**Файл:** `Adapter/UI/CultivationWindow.cs:56,98,351` (подписка OnToggle);
`Adapter/Scene/GameWorldController.cs:1624-1627` (`_cultivationWindow?.Toggle()` — прямой
Godot-вызов), `:928` (комментарий «из InputAdapter (этап D)» — не реализовано).
**Сценарий:** фича работает в обход шины; подписка + контракт — мёртвый код; doc-обещание
в комментарии ложно. Двойная проводка = риск рассинхрона при будущем рефакторинге.
**Фикс:** публиковать контракт из GWController (клавиша K) и удалить прямой Toggle, либо
удалить контракт+подписку.

### DOC-1 [P2] docs_v2 DI_AND_EVENTBUS: функциональный doc-drift — обещанная проводка не существует

**Файл:** `docs/docs_v2/01_architecture/DI_AND_EVENTBUS.md`.
**Сценарий/доказательство:**
- §4 SAV-03 «SaveDataAggregator подписывается на SaveRequestedEvent/LoadRequestedEvent» —
  фактически никто не публикует оба (см. C-3); сейв идёт прямым вызовом ISaveService.
- §4 WLD-B01 «EventService подписывается на TimeChangedEvent» — IEventService не существует
  (0 реализаций/регистраций).
- §4 PLR-A01 «Устранена двойная публикация PlayerSleepEvent» — PlayerSleepEvent не
  публикуется вовсе (полный сирота).
- §2.7 упоминает несуществующие `DamageDealtEvent`, `TimeHourChanged` (фактические:
  DamageAppliedEvent, TimeChangedEvent).
- §2.3/ARCHITECTURE.md §4.3: «~130 контрактов в 20 файлах» — фактически **158 в 24**
  (Combat 5→18, Game 3→7, Player 4→8, UI 10→12, Save 4→8; не отражены Hotbar (2),
  TechniqueCharge (5), Belt (2), Trade (5), GroundItem (2)).
**Фикс:** актуализировать §2.3-реестр, вычеркнуть мёртвые уроки SAV-03/WLD-B01/PLR-A01,
исправить §2.7-имена.

### P3-находки (сводно)

| ID | Файл:строка | Суть | Фикс |
|----|-------------|------|------|
| E-2 | EventBus.cs:90-98 | Livelock-потенциал: обработка очереди вызывает InvokeHandlers напрямую (вне защиты `_publishing`); самотриггерный handler → бесконечный цикл. В проекте таких контрактов нет (проверено) | гвард-счётчик или Publish-путь для очереди |
| E-4 | EventBus.cs:139-146 | `SubscriberCount<T>` — 0 использований (мёртвый API) | удалить или использовать в QA |
| E-5 | EventBus.cs:50-56 | `static [ThreadStatic] _publishing/_pendingQueue` — глобальное состояние всех шин потока; очередь одной шины может исполниться при Publish другой (в игре шина одна — латентно) | инстанс-поля |
| D-1 | Container.cs:246-249 | XML/комментарий «Pick greediest ctor we can satisfy» — фактически строго самый жадный без fallback; GameBoot.cs:42-45 прямо обходит это через RegisterInstance (SaveFileHandler) — эвристика конфликтует с практикой | fallback на менее жадные ctor или док-фикс |
| D-3 | Container.cs:49-52 | Forwarding-ключ impl-типа перезаписывается при multi-interface регистрации (BodyService под IBodyService/IBodyDataProvider/ISaveable → ключ BodyService указывает на reg(ISaveable)). Безопасно: singleton-кэш по impl (208-234) даёт один инстанс в любом порядке | комментарий/симметрия |
| D-5 | Container.cs:275-287 | Тихий null-inject: `[Inject]`-поле с нерезолвящимся типом молча остаётся null → NRE вдалеке от причины | log-warn при resolved==null |
| D-7 | Adapter/Scene/GameBoot.cs:130-134 | Container.Dispose() не вызывается никогда («GameEntryPoint doesn't implement IDisposable in v1») — IDisposable-синглтоны (EventBus и др.) не диспозятся; безвредно при выходе из процесса, утечка в тестах/hot-reload | Dispose в _ExitTree |
| M-1 | SeededRandom.cs:57-61 | `Next()` без аргументов может вернуть ОТРИЦАТЕЛЬНОЕ (`(int)(u64>>32)` при старшем бите) — нарушение XML «non-negative». Вызовов сейчас 0 — латентно | маска 0x7FFFFFFF |
| M-2 | SeededRandom.cs:49 | `SeededRandom(long)`: XOR-фолдинг 64→32 бита — коллизии потоков для NPC-entity-сидов (детерминизм сохранён) | splitmix64-хеш |
| M-3 | ValueNoise.cs:61 | `octaves=0` → 0/0=NaN (нет валидации ctor); в проекте 4-5 октав | clamp ≥1 |
| M-6 | Structs.cs:151-163 | WorldTime год-конструктор: int-умножение, переполнение при year≳4150 (запас ~4100 игровых лет) — practically OK | long при потребности |
| EN-2 | Constants.cs:588-619 + BodyPart.cs:94 | Двойной источник шансов попадания: float-таблица `BodyPartHitChances` (legacy, читает BodyPart.BaseHitChance) и permil `BodyPartHitChancesPermil` (боевой пайплайн DamageService) — риск рассинхрона при правке | удалить float-таблицу |
| C-3 | SaveModule.cs:26-27; WorldModule.cs:26,92 | SaveRequested/LoadRequestedEvent: 3 подписчика, 0 паблишеров; WorldModule.OnSaveRequested — заглушка-лог. Командный путь сейва мёртв (прямой вызов ISaveService; F5/F9 отключены by design Q8) | публиковать из Adapter или удалить |
| C-4 | InputLogService.cs:28-29,53-54,62,75 | InputKeyEvent/InputActionEvent: подписка есть, паблишеров нет; LogKey/LogAction никем не вызываются → IInputLogService целиком мёртв (дока §2.3 «InputLogContracts для отладочного лога» promises) | публиковать из InputAdapter или удалить сервис |
| C-5 | InteractionService.cs:35,62,173; UIService.cs:38-40 | UIInteract/Pause/Resume/StateChangeRequestEvent: слушают, никто не публикует. TryInteract вызывается ТОЛЬКО из мёртвой подписки — InteractionService.TryInteract не вызывается ниоткуда (E-путь идёт через NPCService.OnNPCInteracted напрямую) | публиковать или удалить |
| I-1 | IEventService.cs; IStaminaService.cs | Мёртвые интерфейсы: IEventService (мировые события, 0 реализаций; дока §1.5/§2.3 обещает), IStaminaService (0 реализаций; обещает StaminaChangedEvent) | реализовать или удалить + дока |
| DOC-2/3 | DI_AND_EVENTBUS.md §1.2/§1.5/§5.1/§6.4; ARCHITECTURE.md | §1.2 не упоминает TradeModuleServices (есть, GameLifetimeScope.cs:87); §1.5 «30+» vs 58 фактических; ISaveable без StateType (R11 не отражён); ISceneAssemblyPhase сигнатура PhaseOrder/ExecuteAsync(ct) vs фактические Order/SkipOnLoad/CanExecute/Reset | актуализировать |

Плюс микро: `Modules/Combat/CombatService.cs:567` — комментарий ссылается на несуществующий
контракт `TechniqueCastStartedEvent` (закомментированный вызов) — мёртвый остаток.

---

## Проверено чисто

- **EventBus pub/sub механика:** подписка/отписка парны (UnsubscribeToken); snapshot
  copy-on-read — Subscribe/Unsubscribe ВО ВРЕМЯ доставки не мутируют итерацию; вызов
  handlers вне lock (нет deadlock); порядок доставки FIFO регистрации.
- **Re-entrancy защита (Q13):** Publish(A)→handler→Publish(B)→handler→Publish(A) — A
  ставится в очередь, доставляется после завершения внешнего publish. StackOverflow исключён.
- **Zero-GC:** `Publish<T>(in T)` — generic-инстанциация per тип, `MessageHandler<T>` с
  `in`-параметром, `typeof(T)` не боксит сообщение. Аллокации только при мутации подписок
  (snapshot) и re-entrant замыкании (задокументировано как редкий случай). Подтверждено.
- **DI Container:** singleton-кэш по impl-типу гарантирует ОДИН инстанс при multi-interface
  регистрации (BodyService под 3 интерфейсами — проверено логикой 208-234) и при перезаписи
  forwarding-ключа; ResolveAll instance-dedup по ссылке (R11-фикс работает: ISaveable
  находит все 8 реализаторов через impl-ключи); IResolver self-registration; циклы →
  depth>50 throw; lock reentrancy при конструкте — однопоточно корректно.
- **Lifecycle:** контейнер/EventBus/модули создаются ОДИН раз на процесс (GameBoot._Ready →
  GameLifetimeScope.Build); пересборка мира = SceneOrchestrator.Reset() фаз +
  NpcDomainResetPhase/NPCModule.ResetWorld — контейнер НЕ пересоздаётся и подписки модулей
  (в Start(), однократно) не дублируются. В фазах сборки 0 вызовов Subscribe — утечки
  подписок при пересборке НЕТ. (Известные дыры сброса в World/Tile — вне скоупа Core,
  задокументированы аудитом WT-4.)
- **Ключевые контракты живы и заполняются полностью:** AttackIntentEvent (7 полей, Potency/
  IsCharged), DamageAppliedEvent (полный 8-арг с Element+AttackSubtype — DamageService.cs:341),
  CombatStarted/Ended, NPCDeathEvent (KillerId), CorpseCreated/Removed/Looted (R13),
  DefenseIntentEvent, CombatDisengageEvent, EnemyKilledEvent, EquipmentChangedEvent (5-арг с
  OldItemId — EquipmentService.cs:134,141,155,247), QiChangedEvent (полный 5-арг с level/
  conductivity — QiService.cs:347), TechniqueUsedEvent, ItemPickedUp/Dropped, ItemAddRequest,
  QiConsume/AddRequest, TradeOpened/Closed/Completed, ToastShown, AttackRejected.
  Обратная совместимость ctor'ов не оставляет мусорных полей в активных путях.
- **Permil:** long-промежуточные во всех Apply/Multiply/Ratio; SoftCap, Clamp, FromPercent
  корректны; переполнение RatioLong недостижимо для Qi-масштабов (L10 ≈ 1e12 << 9.2e18).
- **SeededRandom:** xorshift64* корректен (стандартные константы); seed=0 → golden-ratio
  fallback; отрицательный int-seed проходит avalanche корректно; Next(min,max) — беззнаковый
  диапазон, ок; детерминизм подтверждён (в т.ч. аудитом WT).
- **ValueNoise:** fBm-нормализация, smoothstep/fade/lerp, hash — детерминированы и корректны
  для 4-5 октав (использование).
- **Enums:** 63 (Enums.cs) + 6 файлов — явных/неявных дублей значений НЕТ (программная
  проверка с учётом `1<<N` и автонумерации). BiomeType legacy-алиасы задокументированы
  (BiomeType.cs:19-26) — by design.
- **Морфотаблицы:** все 6 таблиц MorphologyHitTables = 1000‰ (Humanoid 1000, Quadruped 1000,
  Bird 1000 [P0-8.1 подтверждён], Serpentine 1000, Arthropod 1000, Amorphous 1000).
- **Data-модель:** BodyPart (dual-HP пороги, DISC-01 одновременный split, Heart=RedHP-only,
  Severed необратим), LevelBoundaries (зеркало формул генераторов, grade-mult 1.0/1.3/1.6/2.0
  = GeneratorTables/GradeProfiles), QiStoneData (канон 1024×см³), InventorySlot (SlotId
  R10), SaveSlot, Position2D/WorldTime — чисты.
- **PlayerIdResolver:** канонизация player→player_0, null-safe — чист (замороженный дизайн
  двух ID не репортится).
- **GameSession/SceneOrchestrator/GameEntryPoint:** state-machine корректна; изоляция
  исключений Start/Tick; стабильная сортировка фаз; Reset-семантика повторной сборки.
- **Interfaces vs DI:** из 58 интерфейсов незарегистрированы только маркерные
  (IModule/IStartable/ITickable/ISceneAssemblyPhase — by design, ResolveAll) и 2 мёртвых
  (I-1). Все зарегистрированные интерфейсы имеют живые реализации.

---

## Соответствие docs_v2

| Раздел | Вердикт |
|--------|---------|
| DI_AND_EVENTBUS §1.1-1.3 (ModuleServices, приоритет инъекции) | ✓ (соответствует; §1.2 — drift: нет Trade, «EventBusRegistrar/MessagingRegistrar» в реальности — RegisterInstance(eventBus) + special-case резолв) |
| §1.5 реестр интерфейсов «30+» | drift (58; IEventService обещан — не существует) |
| §1.6/§3 анти-паттерны | ✓ (нарушений в Core не найдено; прямые ссылки Adapter→Modules вне скоупа) |
| §2.2 readonly struct контракты | ✓ почти (BodyPartSeveredEvent.BlockedSlots — EquipmentSlot[], осознанное исключение с комментарием) |
| §2.3 реестр контрактов ~130/20 | drift (158/24; см. DOC-1) |
| §2.4 типы событий (state/command/lifecycle) | ✓ |
| §2.7 накладные расходы | drift (имена DamageDealtEvent/TimeHourChanged не существуют) |
| §4 уроки (SAV-03, WLD-B01, PLR-A01) | ✗ drift — проводка не существует (см. DOC-1) |
| §5 ISaveable + агрегатор | drift (нет StateType — R11 не отражён; подписка на SaveRequestedEvent мертва) |
| §6.4 ISceneAssemblyPhase | drift (PhaseOrder/ExecuteAsync vs Order/SkipOnLoad/CanExecute/Reset) |
| Q13 re-entrancy (конец дока) | ✓ реализовано как описано (с оговорками E-1/E-2) |
| ARCHITECTURE.md §1-2 (Hub-and-Spoke, 3 слоя) | ✓; §4.3 «~130/20» — drift (158/24) |

---

## Сироты-контракты (из 158 readonly struct)

**Полные сироты — 21 событие (0 pub, 0 sub):**
AutoSaveTriggeredEvent, ClickToMoveEvent, ContextMenuRequestedEvent, HotbarSlotChangedEvent,
LoadGameRequestedEvent, MouseInputEvent, NewGameRequestedEvent, PlayerSleepEvent,
QuitGameRequestedEvent, SaveDeletedEvent, SceneAssemblyCompletedWithErrorsEvent,
SceneLoadedEvent, SceneTransitionRequest, SessionStartedEvent, StaminaChangedEvent,
TechniqueSlotSelectedEvent, TrackingTargetEvent, UILoadRequestEvent, UISaveRequestEvent,
WorldEventEndedEvent, WorldEventTriggeredEvent.
*Вердикты:* NewGame/LoadGame/QuitGame-Requested, SessionStarted — «main-menu event-модель»
не реализована (GWC управляет сессией напрямую); StaminaChanged + IStaminaService —
нереализованная фича UI-2; WorldEvent* + IEventService — нереализованные мировые события;
Mouse/ClickToMove/ContextMenu/Tracking — click-to-move прототип не доведён; Hotbar/
TechniqueSlotSelected — заменены TechniqueSlotAssigned/Cleared; Scene*WithErrors/
SceneLoaded/SceneTransition — не подключены; Save* — сейв-контур Q8. Все — кандидаты на
удаление либо реализацию (в доке не числятся — реестр отстал).

**Sub-only — 10 (слушают, никто не публикует):** CultivationWindowToggleRequested,
InputAction, InputKeyEvent, LoadRequested, SaveRequested, StatChanged, UIInteractRequest,
UIPauseRequest, UIResumeRequest, UIStateChangeRequest — см. C-1..C-5. Мёртвая проводка.

**Pub-only — 41 (публикуют, никто не слушает):** AttitudeChanged, BeltSlotsChanged,
BuffExpired, Charger{Buffer,Cooled,Heat,Overheated,State}Changed (5), ConsumableUsed,
CraftFailed, EquipmentBlocked, GamePaused, GameResumed, ItemRemoved, LoadCompleted,
ModalShown, MonthChanged, NPCDamaged, NPCDespawned, NPCSpawned, PlayerMoved, PlayerRevive,
QiBuffer{Activated,Deactivated}, QiFull, Quest{Abandoned,Failed,ObjectiveUpdated,
RewardGranted,Started} (5), ResourceDepleted, Scene{AssemblyFailed,Initializing,
PhaseCompleted,PhaseStarted,Ready} (5), StatModifierChanged, TechniqueUsed (OK-BY-DESIGN —
информационный, аудит-3 C-2), TileMapGenerated, TimeSpeedChanged, TimeTick, TravelStarted
(паблишер удалён намеренно — WorldService.cs:102).
*Вердикт:* «событие в никуда» — допустимо как API будущих потребителей (UI/статистика),
но 41 шт. без единого слушателя — зона читерской тишины регрессий (поля заполняются, никто
не читает). Рекомендация: пометить в доке как «reserved» либо подключить минимальных
слушателей (лог/UI).

---

*Аудит Core-слоя завершён. Код не изменялся (READ-ONLY). Приоритет фиксов: E-1 → C-1 →
C-2 → DOC-1 → пачка P3 (мёртвые проводки C-3..C-5 при следующем касании модулей).*

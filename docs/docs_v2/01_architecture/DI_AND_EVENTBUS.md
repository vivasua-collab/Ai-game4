# DI и шина событий — engine-agnostic принципы

> **Раздел:** 01_architecture
> **Статус:** Принципиальная спецификация паттернов.
> **Связанные документы:** `ARCHITECTURE.md`, `MODULE_STRUCTURE.md`.

---

## 0. Принцип

Вся архитектура построена на двух фундаментальных паттернах:

1. **DI-контейнер (Dependency Injection)** — все зависимости разрешаются через интерфейсы Core. Прямые обращения к синглтонам, ServiceLocator, поиск узлов на сцене — запрещены.
2. **Шина событий (Event Bus / Pub-Sub)** — все межмодульные коммуникации проходят через шину. Контракты сообщений — `readonly struct` (zero GC).

**Цель:** Полная развязка модулей. Модуль A не знает о существовании модуля B, но может реагировать на его события через Core.

> В этом документе описаны **принципы**, не реализация. Конкретный DI-контейнер и реализация шины выбираются на уровне adapter-слоя (VContainer/MS DI/свой ServiceLocator — не существенно, паттерн сохраняется).

---

## 1. DI-контейнер

### 1.1. Стратегия: инъекция через интерфейсы Core

**Приоритет получения зависимостей:**

```
1. [Inject] IXxxService           ← предпочтительно (через интерфейс ядра)
2. IPublisher<T> / ISubscriber<T> ← через шину (по контракту Core.Messaging)
3. Конкретный тип ВНУТРИ модуля   ← допустимо только в пределах модуля
```

### 1.2. Корневой конфигуратор (GameLifetimeScope)

Корневой конфигуратор собирает все модули последовательно через паттерн ModuleServices:

```
public class GameLifetimeScope  // концептуальный псевдокод
{
    public void Configure(IContainerBuilder builder)
    {
        // 1. Регистрация шины событий (publisher/subscriber фабрики)
        EventBusRegistrar.Register(builder);

        // 2. Регистрация ВСЕХ контрактов сообщений (~130 readonly struct)
        MessagingRegistrar.Register(builder);

        // 3. Регистрация 16 модулей через ModuleServices pattern
        WorldModuleServices.Register(builder);      // ITimeService, IWorldService, IEventService
        TileModuleServices.Register(builder);       // ITileService, IResourceService
        BodyModuleServices.Register(builder);       // IBodyService, BodyFactory, SpeciesRegistry
        QiModuleServices.Register(builder);         // IQiService, IQiBufferService
        BuffModuleServices.Register(builder);       // IBuffService
        InventoryModuleServices.Register(builder);  // IInventoryService, IEquipmentService, ICraftingService, IStorageService
        CombatModuleServices.Register(builder);     // ICombatService, IDamageService
        FormationModuleServices.Register(builder);  // IFormationService
        NPCModuleServices.Register(builder);        // INPCService, INPCSpawnerService
        PlayerModuleServices.Register(builder);     // IPlayerService, IPlayerInputService, IStatService
        QuestModuleServices.Register(builder);      // IQuestService, IQuestRewardService
        InteractionModuleServices.Register(builder);// IInteractionService, IDialogueService
        UIModuleServices.Register(builder);         // IUIService
        ChargerModuleServices.Register(builder);    // IChargerService
        SaveModuleServices.Register(builder);       // ISaveService, агрегация ISaveable
        GeneratorModuleServices.Register(builder);  // TechniqueGeneratorService, ItemGeneratorService

        // 4. Оркестратор сборки сцены + фазы + точка входа
        SceneAssemblyRegistrar.Register(builder);
    }
}
```

### 1.3. ModuleServices Pattern

**Проблема:** Корневой конфигуратор должен видеть все сервисы модулей. Если каждый модуль имеет свой дочерний scope, дочерние scope-ы (siblings) не видят регистрации друг друга — проблема **sibling scope visibility**.

**Решение:** Каждый модуль имеет статический класс `XxxModuleServices` с методом `Register(IContainerBuilder)`, который вызывается из корневого конфигуратора.

```
public static class ChargerModuleServices
{
    public static void Register(IContainerBuilder builder)
    {
        // 1. Регистрация главного интерфейса модуля
        builder.Register<IChargerService, ChargerService>(Lifetime.Singleton);

        // 2. Регистрация точки входа модуля
        builder.Register<ChargerModule>(Lifetime.Singleton)
            .AsImplementedInterfaces();  // IStartable, ITickable

        // 3. Регистрация внутренних сервисов
        builder.Register<ChargerCalculator>(Lifetime.Singleton);
        builder.Register<ChargerBuffer>(Lifetime.Singleton);
        builder.Register<ChargerHeat>(Lifetime.Singleton);
        builder.Register<ChargerSlot>(Lifetime.Singleton);

        // 4. Конфигурация по умолчанию (через build callback)
        builder.RegisterBuildCallback(container =>
        {
            var module = container.Resolve<ChargerModule>();
            module.SetConfig(DefaultChargerConfig);
        });
    }
}
```

### 1.4. Lifetime scopes

| Scope | Содержит | Жизненный цикл |
|-------|----------|----------------|
| **GameLifetimeScope** (root) | Все 16 модулей + SceneOrchestrator + GameSession + шина | На время всего приложения |
| **ModuleLifetimeScope** (опционально, per-module) | Реализация + Module + ModuleServices (если используются дочерние scope-ы) | На время приложения |
| **Scene scope** (если требуется) | Временные данные сцены | На время жизни сцены |

> **Принцип:** ModuleServices делает LifetimeScope каждого модуля компактным (только интерфейс + Module + ModuleServices). Это упрощает тестирование и модификацию.

### 1.5. Реестр зарегистрированных интерфейсов

30+ интерфейсов в ядре (актуальный список — в `MODULE_STRUCTURE.md` §1):

| Категория | Интерфейсы |
|-----------|------------|
| Core сервисы | ITimeService, IStatService |
| Body/Buff/Qi | IBodyService, IBuffService, IQiService, IQiBufferService |
| Combat | ICombatService, IDamageService |
| Inventory | IInventoryService, IStorageService, ICraftingService, IEquipmentService |
| Tile/World | ITileService, IResourceService, IWorldService, IEventService |
| NPC/Player | INPCService, INPCSpawnerService, IPlayerService, IPlayerInputService |
| Quest/Interaction | IQuestService, IQuestRewardService, IInteractionService, IDialogueService |
| UI/Save | IUIService, ISaveService, ISaveable |
| Formation/Charger | IFormationService, IChargerService |
| Scene | ISceneAssemblyPhase |

### 1.6. Анти-паттерны (ЗАПРЕЩЕНЫ)

```
// ❌ Singleton
public static MyManager Instance { get; private set; }

// ❌ ServiceLocator
var mgr = ServiceLocator.Get<CombatManager>();

// ❌ Поиск узла на сцене (любая форма "FindObjectOfType")
var player = Scene.Find<PlayerController>();

// ❌ Прямая сериализованная ссылка между модулями
[SerializeField] private QiManager _qiManager;  // Кросс-модульная!

// ❌ Кросс-модульная прямая ссылка на конкретный класс
[Inject] ChargerService _charger;  // Нужен IChargerService!

// ❌ Прямой вызов обработчика другого модуля
bodyService.ApplyDamage(part, dmg);  // Из Combat — только через шину!
```

### 1.7. Правила DI

1. **DI через интерфейс Core** — всегда предпочтительно.
2. **DI-каст** (`_service is XxxService`) допустим только ВНУТРИ модуля (для Configure).
3. **Configure() НЕ в интерфейсе** — чтобы не создавать Core→Modules зависимость (CH-04 правило).
4. **Tick() в интерфейсе** — если модулю нужен Tick, добавлять метод в интерфейс (CH-04).
5. **Config — class, не struct** — избежание mutable struct risk (BD-48 правило).

---

## 2. Шина событий (Event Bus)

### 2.1. Принцип

Все межмодульные коммуникации проходят через шину (pub/sub). Контракты — `readonly struct` для **нулевой GC-аллокации**.

```
[Inject] IPublisher<BodyPartDamagedEvent> _damagePub;
_damagePub.Publish(new BodyPartDamagedEvent { EntityId = id, PartType = part, Damage = dmg });

[Inject] ISubscriber<BodyPartDamagedEvent> _damageSub;
_damageSub.Subscribe(e => HandleDamage(e.CurrentQi));
```

### 2.2. Контракты — `readonly struct`

**КРИТИЧНО:** Все контракты — `readonly struct`. Это гарантирует:
- Zero GC allocation при публикации/подписке.
- Value semantics — нет проблем с identity.
- Компактное представление в памяти.

**Пример канонического контракта:**
```
public readonly struct BodyPartDamagedEvent
{
    public readonly int EntityId;
    public readonly BodyPartType PartType;
    public readonly float Damage;
    public readonly DamageType DmgType;

    public BodyPartDamagedEvent(int entityId, BodyPartType part, float damage, DamageType type)
    {
        EntityId = entityId;
        PartType = part;
        Damage = damage;
        DmgType = type;
    }
}
```

**Правила:**
- Все поля `readonly`.
- Никаких reference-type полей (кроме `string` для ID, но предпочтительнее `int`).
- Никаких методов, изменяющих состояние (по определению).
- Имя оканчивается на `Event` (для событий) или `Request` (для командных событий).

### 2.3. Реестр контрактов (~130 в 20 файлах)

| Файл | Домен | Кол-во | Примеры |
|------|-------|--------|---------|
| GameContracts | Игра | 3 | GameStateChanged, GamePaused, GameResumed |
| CombatContracts | Бой | 5 | CombatStarted, CombatEnded, DamageApplied, TechniqueUsed, EnemyKilled |
| BodyContracts | Тело | 5 | BodyPartDamaged, BodyPartSevered, BodyPartHealed, BodyPartReattached, BodyCritical |
| QiContracts | Ци | 11 | QiChanged, QiDepleted, QiFull, CultivationBreakthrough, CultivationLevelChanged, QiBuffer*, QiConsume/AddRequest |
| BuffContracts | Баффы | 5 | BuffApplied, BuffRemoved, BuffExpired, BuffTicked, StatModifierChanged |
| ChargerContracts | Зарядник | 5 | ChargerStateChanged, ChargerOverheated, ChargerCooledDown, ChargerHeatChanged, ChargerBufferChanged |
| TileContracts | Тайлы | 6 | TileChanged, ResourceHarvested, ResourceDepleted, TileMapGenerated, ResourceRespawned, HarvestResult (struct) |
| InventoryContracts | Инвентарь | 5 | ItemAdded, ItemRemoved, EquipmentChanged, EquipmentBlocked, ItemAddRequest |
| PlayerContracts | Игрок | 4 | PlayerDeath, PlayerRevive, PlayerSleep, PlayerPositionChanged |
| WorldContracts | Мир | 11 | TimeChanged, DayChanged, TimeSpeedChanged, SceneTransitionRequest, SceneLoaded, MonthChanged, YearChanged, LocationChanged, TravelStarted, WorldEventTriggered, WorldEventEnded |
| NPCContracts | NPC | 7 | NPCSpawned, NPCDespawned, AttitudeChanged, NPCDeath, NPCInteracted, NPCAIStateChanged, NPCDamaged |
| FormationContracts | Формации | 5 | FormationActivated, FormationDeactivated, FormationQiPoolChanged, FormationStageChanged, FormationContributeQiRequest |
| QuestContracts | Квесты | 6 | QuestStarted, QuestObjectiveUpdated, QuestCompleted, QuestFailed, QuestAbandoned, QuestRewardGranted |
| SaveContracts | Сохранение | 4 | SaveRequested, LoadRequested, SaveCompleted, LoadCompleted |
| DialogueContracts | Диалоги | 4 | DialogueStarted, DialogueEnded, DialogueChoiceSelected, InteractionCompleted |
| StatContracts | Характеристики | 1 | StatChanged |
| CraftingContracts | Крафт | 2 | CraftCompleted, CraftFailed |
| UIContracts | UI | 10 | UIStateChangeRequest, UIInteractRequest, UIAdvanceDialogueRequest, UISelectChoiceRequest, UISaveRequest, UILoadRequest, UIPauseRequest, UIResumeRequest, ToastShown, ModalShown |
| SceneContracts | Сцена | 6 | SceneInitializing, ScenePhaseStarted, ScenePhaseCompleted, SceneReady, SceneAssemblyFailed, SceneAssemblyCompletedWithErrors |
| InputLogContracts | Ввод | 2 | InputKey, InputAction (для отладочного лога) |

### 2.4. Типы событий

#### 2.4.1. State-changed events

Уведомляют об изменении состояния. Издатель не ожидает ответа.

- `QiChangedEvent` — изменилось текущее Ци.
- `BodyPartDamagedEvent` — нанесён урон части тела.
- `LocationChangedEvent` — игрок сменил локацию.
- `DayChangedEvent` — наступил новый день.

#### 2.4.2. Command events (request→response)

Для развязки модулей: один модуль просит другой что-то сделать, не зная о нём.

- `QiConsumeRequestEvent` — попросить QiModule списать Ци.
- `QiAddRequestEvent` — попросить QiModule добавить Ци.
- `QiBufferActivateRequestEvent` / `QiBufferDeactivateRequestEvent` — управлять буфером Ци.
- `ItemAddRequestEvent` — попросить InventoryModule добавить предмет.
- `FormationContributeQiRequestEvent` — попросить участника внести Ци в формацию.

#### 2.4.3. Lifecycle events

- `GamePausedEvent` / `GameResumedEvent` — пауза/возобновление.
- `SaveRequestedEvent` / `SaveCompletedEvent` / `LoadRequestedEvent` / `LoadCompletedEvent` — сохранение.
- `SceneAssembly*Event` — фазы сборки сцены.

### 2.5. Пример: межмодульное взаимодействие

**Сценарий:** TileModule собирает ресурс — нужно добавить предмет в инвентарь.

**Без шины (запрещено):**
```
// TileModule напрямую вызывает InventoryModule — нарушение Hub-and-Spoke
[Inject] IInventoryService _inventory;
public void Harvest() {
    _inventory.TryAddItem(harvestedItem);  // ❌ Прямая зависимость!
}
```

**С шиной (правильно):**
```
// TileModule публикует событие
[Inject] IPublisher<ResourceHarvestedEvent> _harvestPub;
public void Harvest() {
    var result = ComputeHarvest();
    _harvestPub.Publish(new ResourceHarvestedEvent(result.ItemId, result.Amount));
}

// InventoryModule подписывается (в своём коде)
[Inject] ISubscriber<ResourceHarvestedEvent> _harvestSub;
public void Initialize() {
    _harvestSub.Subscribe(OnResourceHarvested);
}
private void OnResourceHarvested(in ResourceHarvestedEvent e) {
    _inventoryService.TryAddItem(e.ItemId, e.Amount);
}
```

TileModule НЕ знает об InventoryModule. InventoryModule НЕ знает об TileModule. Оба знают только о `ResourceHarvestedEvent` из Core.Messaging.

### 2.6. Циркулярные зависимости — решение через события

**Пример из Фазы 2:** ResourceService нужен ITileService для обновления тайла после респауна, а TileMapService (ITileService) зависит от IResourceService.

**Решение:** `ResourceRespawnedEvent`
1. ResourceService публикует `ResourceRespawnedEvent` при респауне.
2. TileMapService подписывается на `ResourceRespawnedEvent` и обновляет тайл.
3. Никакой циркулярной зависимости.

### 2.7. Накладные расходы

Расчёт для 100 NPC с 10 событиями/сек:

| Событие | Частота | Подписчики | Накладные расходы |
|---------|---------|------------|-------------------|
| QiChangedEvent | При каждом изменении Ци | NPCService (кэш), UI | ~0.01 ms/event |
| TimeHourChanged | Раз в игровой час | WorldService, NPCService | ~0.01 ms/event |
| NPCAIStateChanged | При смене состояния | UI | ~0.01 ms/event |
| DamageDealtEvent | При каждом ударе | UI, Achievement (TBD) | ~0.02 ms/event |

> **Вывод:** Шина с readonly struct контрактами — минимальные накладные расходы. Zero GC allocation. Для 100 NPC с 10 событиями/сек = ~0.1 ms/сек — пренебрежимо мало.

---

## 3. Перечень анти-паттернов (ЗАПРЕЩЕНЫ)

```
// ❌ Singleton
public static MyManager Instance { get; private set; }

// ❌ ServiceLocator
var mgr = ServiceLocator.Get<CombatManager>();

// ❌ Поиск узла на сцене (FindObjectOfType-стиль)
var ctrl = FindObjectOfType<PlayerController>();

// ❌ Поиск по имени
var obj = Scene.Find("Player");

// ❌ Кросс-модульная прямая ссылка на конкретный класс
[Inject] ChargerService _charger;  // Нужен IChargerService!

// ❌ Прямой вызов обработчика другого модуля
bodyService.ApplyDamage(part, dmg);  // Из Combat — только через шину!

// ❌ C# event для межмодульной коммуникации
public event Action<float> OnDamageDealt;  // Используйте шину!

// ❌ Coroutine/timer-цикл для долгих операций
// Используйте async/await!

// ❌ Mutable struct для контрактов
public struct MyEvent { public int X; }  // Должен быть readonly struct!

// ✅ DI через интерфейс ядра
[Inject] IChargerService _chargerService;

// ✅ Шина для межмодульной связи
[Inject] IPublisher<BodyPartDamagedEvent> _damagePub;

// ✅ async/await для долгих операций
public async Task TickBuffAsync() { await Task.Delay(...); }
```

---

## 4. Ключевые уроки из реализации

| Урок | Фаза | Описание |
|------|-------|----------|
| CH-32/33 | 1 | Без реализаций интерфейсов DI не создаст сервисы → нужны stub-ы на ранних фазах |
| CH-34 | 1 | Дочерние scope-ы нужно правильно привязывать к родительскому |
| CH-04 | 1 | Tick() через интерфейс, Configure() только внутри модуля (без циклической зависимости) |
| BD-42 | 3 | ITimeService.DeltaTime вместо глобального `Time.deltaTime` — для тестируемости |
| BD-48 | 3 | Config — class, не struct (mutable struct risk) |
| FIX-1 | 2 | DI-каст устранён: Harvest() добавлен в IResourceService |
| FIX-2 | 2 | ResourceRespawnedEvent решает циркулярную зависимость |
| QI-A05 | 4 | Отслеживать потреблённый Ци в буфере для корректного возврата при Deactivate |
| QI-A04 | 4 | Не перезаписывать `_coreCapacity` после прорыва |
| QI-A01 | 4 | Удалять мёртвые методы интерфейса |
| QI-C01 | 4 | Подписка на кросс-модульные события (BodyPartSevered → QiBufferService) |
| BF-A01 | 5 | Формула CalculateStatModifier должна точно совпадать с документацией |
| BF-A03 | 5 | Маппинг иммунитетов требует словарь Effect→Immunity |
| INV-01 | 6 | EquipmentService НЕ ссылается на BodySlotMapping — использует `BodyPartSeveredEvent.BlockedSlots` (Hub-and-Spoke) |
| INV-02 | 6 | SpiritStorage + StorageRing унифицированы в StorageService с параметром StorageType |
| INV-03 | 6 | EquipmentController God Object разбит на EquipmentService + EquipmentValidator + EquipmentStatAggregator |
| INV-04 | 6 | EquipmentService НЕ инжектит IBodyService — sibling scopes не видят регистрации. Использует события |
| NPC-A07 | 9 | GetAllNPCIds/SetAIState/UpdatePosition добавлены в интерфейс (баг: отсутствовали) |
| NPC-B04 | 9 | NPCCombatAdapter подписывается на CombatStarted/Ended через шину (не прямая ссылка) |
| NPC-B05 | 9 | NPCRelationshipService подписывается на DayChangedEvent для затухания отношений |
| PLR-A01 | 10 | Устранена двойная публикация PlayerSleepEvent |
| PLR-E06 | 10 | ResetFrameFlags() вызывается из PlayerModule.Tick() ПОСЛЕ всех потребителей |
| PLR-GOD | 10 | PlayerController God Object разбит на 6 сервисов + PlayerConfig + PlayerData |
| WLD-A01 | 11 | TimeService переезжает из stub в WorldModule — реальная реализация |
| WLD-A02 | 11 | WorldService не инжектит IPlayerService — использует LocationChangedEvent |
| WLD-B01 | 11 | EventService подписывается на TimeChangedEvent для триггера мировых событий |
| WLD-C01 | 11 | FactionService.FactionRelation через readonly struct, не enum — расширяемость |
| QST-A02 | 12 | QuestRewardService выделен из QuestService — SRP, отдельный интерфейс |
| QST-B01 | 12 | QuestProgressTracker подписывается на кросс-модульные события через шину |
| INT-C01 | 13 | InteractionCompletedEvent публикуется — подписчики QuestModule, NPCModule |
| UI-C01 | 14 | HUDPresenter / DialoguePresenter — презентеры, чистый C# + шина (не engine-зависимые) |
| SAV-01 | 15 | SaveService реализует ISaveService — SaveFileHandler (I/O) + SaveDataAggregator (сбор от ISaveable) |
| SAV-02 | 15 | SaveModule НЕ имеет SaveLifetimeScope — использует SaveModuleServices |
| SAV-03 | 15 | SaveDataAggregator подписывается на SaveRequestedEvent/LoadRequestedEvent — Hub-and-Spoke |
| SCN-01 | 16 | SceneOrchestrator управляет последовательной сборкой через 10 фаз |
| MIN-01/02 | 17 | ModuleServices pattern — единообразная регистрация для всех 16 модулей |
| SES-01/02 | 18 | GameSession управляет жизненным циклом, подписывается на GamePaused/Resumed |

---

## 5. ISaveable Pattern

### 5.1. Контракт

Сервисы, желающие сохранять состояние, реализуют `ISaveable`:

```
public interface ISaveable
{
    string SaveKey { get; }
    object CaptureState();
    void RestoreState(object state);
}
```

### 5.2. SaveDataAggregator

При `SaveRequestedEvent`:
1. SaveDataAggregator обходит все зарегистрированные `ISaveable`.
2. Вызывает `CaptureState()` у каждого.
3. Собирает словарь `{ SaveKey: state }`.
4. Передаёт в `SaveFileHandler` для сериализации в JSON.

При `LoadRequestedEvent`:
1. SaveFileHandler читает JSON.
2. SaveDataAggregator обходит все `ISaveable`.
3. Вызывает `RestoreState(state)` у каждого.

### 5.3. Сохраняемые сервисы

| Модуль | Сохраняет |
|--------|-----------|
| Player | Статы, уровень культивации, Ци, состояние тела, сон, стойка, позиция |
| Qi | Текущее Ци, уровень культивации, качество ядра, бонусы проводимости |
| Body | Состояние всех частей (redHP, blackHP, severed) |
| Inventory | Все предметы, экипировка в слотах |
| World | Текущее время, локация, фракции, события |
| Quest | Активные квесты, прогресс, выполненные |
| NPC | Plot и Unique NPC (не Temp) |
| Formation | Активные формации, пул Ци |
| Charger | Состояние зарядников, тепло |
| Buff | Активные баффы |
| Tile | Дельта тайлов (seed + изменения, не поштучно) |

---

## 6. Асинхронные операции

### 6.1. Принцип

Все асинхронные операции используют **async/await** (нативный C#). Запрещены Coroutines / таймер-циклы.

### 6.2. Примеры

```
// ✅ Async/await (правильно)
public class BuffTickProcessor
{
    public async Task TickBuffAsync(ActiveBuff buff, CancellationToken ct)
    {
        while (buff.RemainingTicks > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
            buff.RemainingTicks--;
        }
    }
}

// ❌ Coroutine (запрещено)
public class BuffManager
{
    IEnumerator TickBuffCoroutine(Buff buff)
    {
        while (buff.RemainingTicks > 0)
        {
            yield return new WaitForSeconds(1f);
            buff.RemainingTicks--;
        }
    }
}
```

### 6.3. CancellationToken

Все долгие async-операции принимают `CancellationToken`:
- Сцена перезагружается → отмена всех активных операций.
- Пауза → опционально отмена.
- Save/Load → отмена при ошибке.

### 6.4. ISceneAssemblyPhase

```
public interface ISceneAssemblyPhase
{
    string PhaseName { get; }
    int PhaseOrder { get; }
    Task ExecuteAsync(CancellationToken ct = default);
}
```

10 фаз сборки сцены выполняются последовательно через `await`. Если фаза падает — `SceneAssemblyFailedEvent` с указанием имени фазы и исключения.

---

## 7. Связанные документы

| Документ | Описание |
|----------|----------|
| `ARCHITECTURE.md` | Высокоуровневая архитектура |
| `MODULE_STRUCTURE.md` | Детально по модулям |
| `PERFORMANCE_STRATEGY.md` | Zero-GC, pooling, многопоточность |
| `09_workflow/ALGORITHMS.md` | Формулы (вычисления внутри сервисов) |

---

## Защита от re-entrancy (Q13)

### Проблема
Если обработчик события публикует событие того же типа → бесконечная рекурсия → `StackOverflowException` (краш приложения).

Пример:
- `BodyPartDamagedEvent` обрабатывается сервисом A.
- Внутри обработчика сервис A публикует ещё один `BodyPartDamagedEvent` (например, ricochet/chain-эффект).
- Без защиты → бесконечная рекурсия → стек переполняется.

### Решение: queue re-entrant events
Шина событий отслеживает типы, находящиеся в активной публикации:

- При публикации проверяется: тип уже в активной публикации (`_publishing HashSet<Type>`)?
- **Если да** — событие НЕ публикуется немедленно, а добавляется в очередь `_pendingQueue`.
- После завершения текущей публикации (все подписчики отработали) — очередь обрабатывается.
- События из очереди публикуются как новые (снова с проверкой re-entrancy — многоуровневая вложенность безопасна).

### Гарантии
- События **не теряются** — все queued-события дойдут до подписчиков.
- Рекурсия **исключена** — глубина стека ограничена двумя уровнями (current + drain pending).
- Порядок сохраняется — queued-события обрабатываются в порядке поступления (FIFO).

### Влияние на производительность
- В обычном (не re-entrant) случае — накладных расходов нет (просто проверка `HashSet.Contains`).
- В re-entrant случае — одно дополнительное копирование в очередь + второй проход публикации.
- На практике re-entrancy встречается редко (~0.1% событий), влияние пренебрежимо мало.

---

*Документ создан в рамках Task 2-a: engine-agnostic rewrite. Источники: `docs/ARCHITECTURE_CODE.md` §4-5, `docs/ARCHITECTURE.md`, `docs/ARCHITECTURE_IMPL.md` §ModuleServices.*

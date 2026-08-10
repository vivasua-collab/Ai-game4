# Архитектура — Cultivation World Simulator

> **Раздел:** 01_architecture
> **Статус:** Принципиальная схема (engine-agnostic).
> **Связанные документы:** `MODULE_STRUCTURE.md`, `DI_AND_EVENTBUS.md`, `PERFORMANCE_STRATEGY.md`, `00_overview/PROJECT_CONCEPT.md`.

---

## 0. Принципы дизайна

Архитектура строится на четырёх фундаментальных принципах:

1. **Engine-agnostic ядро.** Вся игровая логика — на чистом C#, без зависимостей от конкретного движка. Движок используется только для рендеринга, ввода, UI, аудио и сцены. При смене движка меняется только адаптер-слой, не ядро.
2. **Hub-and-Spoke (Звезда).** 16 модулей общаются только через центральное ядро (Core). Межмодульные зависимости запрещены.
3. **Zero GC per frame.** Hot-path аллокации исключены. Все сообщения между системами — `readonly struct`. Никаких LINQ/lambda-captures в горячих циклах.
4. **Tick-based симуляция.** Логика работает с фиксированным шагом, отвязанным от frame rate. Рендер — независимо.

---

## 1. Hub-and-Spoke (Звезда)

### 1.1. Принцип

Все модули независимы друг от друга и подключаются **ТОЛЬКО** к центральному ядру (Core). Межмодульные зависимости ЗАПРЕЩЕНЫ.

```
                    ┌──────────────────────────┐
                    │          CORE            │
                    │  Интерфейсы / Контракты  │
                    │  Данные / Константы / DI │
                    └────────────┬─────────────┘
                                 │
   ┌────────┬────────┬───────────┼───────────┬────────┬────────┐
   │        │        │           │           │        │        │
┌──▼──┐ ┌───▼──┐ ┌───▼───┐ ┌─────▼────┐ ┌───▼───┐ ┌──▼───┐ ┌──▼─────┐
│Char.│ │ Tile │ │ Body  │ │   Qi     │ │ Buff  │ │Inv.  │ │Combat  │
│ger  │ │      │ │       │ │          │ │       │ │      │ │        │
└─────┘ └──────┘ └───────┘ └──────────┘ └───────┘ └──────┘ └────────┘
   │        │        │           │           │        │        │
   └────────┴────────┴───────────┴───────────┴────────┴────────┘
                    ❌ ПРЯМЫЕ СВЯЗИ ЗАПРЕЩЕНЫ

   ┌────────┬────────┬───────────┬───────────┬────────┐
   │        │        │           │           │        │
┌──▼──┐ ┌───▼──┐ ┌───▼───┐ ┌─────▼────┐ ┌───▼───┐ ┌──▼─────┐
│ NPC │ │Player│ │ World │ │  Quest   │ │ UI    │ │ Save   │
│     │ │      │ │       │ │          │ │       │ │        │
└─────┘ └──────┘ └───────┘ └──────────┘ └───────┘ └────────┘
```

**Правило:** Если модулю A нужны данные от модуля B → A подписывается на событие из шины сообщений Core, которое публикует B.

### 1.2. Что в ядре (Core)

- **Интерфейсы сервисов** (`IChargerService`, `ITileService`, `IBodyService`, `IQiService` и т.д. — всего 30+ интерфейсов).
- **Контракты сообщений** — `readonly struct` для шины событий (~130 контрактов в 20 файлах).
- **Данные и константы** — enums, GameConstants (MAX_STAT_VALUE=1000, MAX_CULTIVATION_LEVEL=9 и т.д.), структуры данных (`BodyPartData`, `DamageRequest`, `DamageResult`, `DefenseContext`, `SaveInfo`, `ActiveBuffData`, `HarvestResult`).
- **DI-конфигурация** — корневой конфигуратор, регистрирующий все модули.

### 1.3. Что НЕ в ядре

- Конкретные реализации сервисов (`ChargerService`, `TileMapService` и т.д.) — в модулях.
- Игровые формулы (находятся в калькуляторах модулей).
- Состояние игры (хранится в сервисах модулей).

---

## 2. Слои архитектуры

Архитектура разделена на 3 чётких слоя:

```
┌─────────────────────────────────────────────────────────────┐
│                     Adapter Layer                           │
│   Рендеринг · Ввод · UI · Аудио · Сцена · Сохранение I/O   │
│   (engine-specific, изолирован от логики)                  │
└────────────────────────────┬────────────────────────────────┘
                             │ (вызовы через интерфейсы Core)
┌────────────────────────────┴────────────────────────────────┐
│                      Application Layer                      │
│   16 модулей игровой логики:                                │
│   Body · Buff · Charger · Combat · Formation · Inventory ·  │
│   NPC · Player · Qi · Tile · World · Quest · Interaction ·  │
│   UI · Save · Generator                                     │
│                                                             │
│   + SceneOrchestrator + GameSession                         │
└────────────────────────────┬────────────────────────────────┘
                             │
┌────────────────────────────┴────────────────────────────────┐
│                        Core Layer                           │
│   Интерфейсы · Контракты (readonly struct) · Данные ·       │
│   Константы · Enums · DI-конфигурация                       │
└─────────────────────────────────────────────────────────────┘
```

### 2.1. Принцип изоляции adapter-слоя

**Весь игровой логический код пишется как чистый C# без зависимостей от движка.** Движок используется только для:

- **Рендеринга** (спрайты, тайлы, эффекты).
- **Ввода** (клавиатура, мышь).
- **UI** (окна, кнопки, списки).
- **Аудио** (звуки, музыка).
- **Сцены** (загрузка/выгрузка локаций).
- **Файлового I/O** (сохранения).

Это означает: при смене движка (например, при backup-переключении на альтернативную реализацию) меняется только adapter-слой. Логика остается без изменений.

---

## 3. 16 модулей

Полный список модулей и их ответственности:

| # | Модуль | Зона ответственности |
|---|--------|----------------------|
| 1 | **Body** | Части тела, двойная HP (красная/чёрная), материалы, ампутации, регенерация |
| 2 | **Buff** | 28 типов баффов/дебаффов, мягкие капы, иммунитеты, периодические эффекты |
| 3 | **Charger** | Зарядники Ци, слоты камней, буфер, тепловой баланс |
| 4 | **Combat** | 11-слойный пайплайн урона, подавление уровнем, активная защита, AI боя |
| 5 | **Formation** | Магические массивы, контур, пул Ци, эффекты, утечка |
| 6 | **Inventory** | Инвентарь, экипировка, крафт, хранилища (Spirit/Ring), валидация слотов |
| 7 | **NPC** | Спавн, AI (Spinal/Neural/Brain), отношения, движение, категории (Temp/Plot/Unique) |
| 8 | **Player** | Игрок, ввод, сон, стойки, визуал, характеристики (StatService) |
| 9 | **Qi** | Ци, ядро, проводимость, регенерация, прорывы, буфер Ци |
| 10 | **Tile** | Тайловая карта, генерация, ресурсы, разрушаемые объекты |
| 11 | **World** | Локации, фракции, время, события, секторы |
| 12 | **Quest** | Квесты, прогресс, награды, типы целей |
| 13 | **Interaction** | Взаимодействие с объектами, диалоги, ветвление |
| 14 | **UI** | HUD, тосты, презентеры, состояние UI |
| 15 | **Save** | Сохранения (JSON), автосохранение, агрегация через `ISaveable` |
| 16 | **Generator** | Генераторы предметов/техник/NPC по принципу «Матрёшка» |

Плюс две над-модульные сущности:
- **SceneOrchestrator** — оркестратор сборки сцены (10 фаз).
- **GameSession** — жизненный цикл сессии.

> Подробно по каждому модулю — в `MODULE_STRUCTURE.md`.

---

## 4. Шина сообщений

### 4.1. Принцип

Все межмодульные коммуникации проходят через шину событий (pub/sub). Прямые C#-события и обращения между модулями запрещены.

### 4.2. Контракты — `readonly struct`

Все контракты сообщений — `readonly struct` для **нулевой GC-аллокации**.

Пример:
```
public readonly struct BodyPartDamagedEvent
{
    public readonly int EntityId;
    public readonly BodyPartType PartType;
    public readonly float Damage;

    public BodyPartDamagedEvent(int entityId, BodyPartType part, float damage)
    {
        EntityId = entityId;
        PartType = part;
        Damage = damage;
    }
}
```

### 4.3. Реестр контрактов (~130 событий в 20 файлах)

| Файл | Домен | Контракты |
|------|-------|-----------|
| GameContracts | Игра | GameStateChanged, GamePaused, GameResumed |
| CombatContracts | Бой | CombatStarted, CombatEnded, DamageApplied, TechniqueUsed, EnemyKilled |
| BodyContracts | Тело | BodyPartDamaged, BodyPartSevered, BodyPartHealed, BodyPartReattached, BodyCritical |
| QiContracts | Ци | QiChanged, QiDepleted, QiFull, CultivationBreakthrough, CultivationLevelChanged, QiBufferActivated/Deactivated/StateChanged, QiConsume/AddRequest, QiBufferActivate/DeactivateRequest |
| BuffContracts | Баффы | BuffApplied, BuffRemoved, BuffExpired, BuffTicked, StatModifierChanged |
| ChargerContracts | Зарядник | ChargerStateChanged, ChargerOverheated, ChargerCooledDown, ChargerHeatChanged, ChargerBufferChanged |
| TileContracts | Тайлы | TileChanged, ResourceHarvested, ResourceDepleted, TileMapGenerated, ResourceRespawned, HarvestResult (struct) |
| InventoryContracts | Инвентарь | ItemAdded, ItemRemoved, EquipmentChanged, EquipmentBlocked, ItemAddRequest |
| PlayerContracts | Игрок | PlayerDeath, PlayerRevive, PlayerSleep, PlayerPositionChanged |
| WorldContracts | Мир | TimeChanged, DayChanged, TimeSpeedChanged, SceneTransitionRequest, SceneLoaded, MonthChanged, YearChanged, LocationChanged, TravelStarted, WorldEventTriggered, WorldEventEnded |
| NPCContracts | NPC | NPCSpawned, NPCDespawned, AttitudeChanged, NPCDeath, NPCInteracted, NPCAIStateChanged, NPCDamaged |
| FormationContracts | Формации | FormationActivated, FormationDeactivated, FormationQiPoolChanged, FormationStageChanged, FormationContributeQiRequest |
| QuestContracts | Квесты | QuestStarted, QuestObjectiveUpdated, QuestCompleted, QuestFailed, QuestAbandoned, QuestRewardGranted |
| SaveContracts | Сохранение | SaveRequested, LoadRequested, SaveCompleted, LoadCompleted |
| DialogueContracts | Диалоги | DialogueStarted, DialogueEnded, DialogueChoiceSelected, InteractionCompleted |
| StatContracts | Характеристики | StatChanged |
| CraftingContracts | Крафт | CraftCompleted, CraftFailed |
| UIContracts | UI | UIStateChangeRequest, UIInteractRequest, UIAdvanceDialogueRequest, UISelectChoiceRequest, UISaveRequest, UILoadRequest, UIPauseRequest, UIResumeRequest, ToastShown, ModalShown |
| SceneContracts | Сцена | SceneInitializing, ScenePhaseStarted, ScenePhaseCompleted, SceneReady, SceneAssemblyFailed, SceneAssemblyCompletedWithErrors |
| InputLogContracts | Ввод | InputKey, InputAction (для отладочного лога) |

> **Command Events (request→response паттерн)** для развязки модулей: QiConsumeRequest, QiAddRequest, QiBufferActivate/DeactivateRequest, ItemAddRequest, FormationContributeQiRequest.

> Подробно о шине событий и DI — в `DI_AND_EVENTBUS.md`.

### 4.4. Пример: межмодульное взаимодействие

**Сценарий:** TileModule собирает ресурс — нужно добавить предмет в инвентарь.

```
TileModule ──publish──▶ ResourceHarvestedEvent ──subscribe──▶ InventoryModule
    │                                                              │
    └── IResourceService.Harvest()              IInventoryService.TryAddItem()
```

TileModule НЕ знает об InventoryModule. InventoryModule подпишется на `ResourceHarvestedEvent` и сам вызовет `IInventoryService.TryAddItem()`.

### 4.5. Циркулярные зависимости — решение через события

**Пример:** ResourceService нужен ITileService для обновления тайла после респауна, а TileMapService (ITileService) зависит от IResourceService.

**Решение:** `ResourceRespawnedEvent`
1. ResourceService публикует `ResourceRespawnedEvent` при респауне.
2. TileMapService подписывается на событие и обновляет тайл.
3. Никакой циркулярной зависимости.

---

## 5. ModuleServices Pattern

### 5.1. Проблема

Корневой конфигуратор (GameLifetimeScope) должен видеть все сервисы модулей. Но если каждый модуль имеет свой дочерний scope, дочерние scope-ы (siblings) не видят регистрации друг друга — это проблема **sibling scope visibility**.

### 5.2. Решение

Каждый модуль имеет статический класс `XxxModuleServices` с методом `Register(IContainerBuilder)`, который вызывается из корневого конфигуратора. Все сервисы регистрируются в корневом scope и доступны всем.

```
public static class ChargerModuleServices
{
    public static void Register(IContainerBuilder builder)
    {
        // Регистрация внутренних сервисов модуля
        builder.Register<IChargerService, ChargerService>(Lifetime.Singleton);
        builder.Register<ChargerModule>(Lifetime.Singleton)
            .AsImplementedInterfaces();  // IStartable, ITickable
        builder.Register<ChargerCalculator>(Lifetime.Singleton);
        builder.Register<ChargerHelper>(Lifetime.Singleton);
    }
}
```

Корневой конфигуратор последовательно вызывает Register всех 16 модулей. Это делает LifetimeScope каждого модуля компактным (только интерфейс + Module + ModuleServices) и упрощает тестирование.

> Подробно о регистрации и lifetime scopes — в `DI_AND_EVENTBUS.md`.

---

## 6. Сборка сцены (Scene Assembly)

### 6.1. SceneOrchestrator

Оркестратор программной сборки сцены. Выполняет 10 фаз последовательно (через async/await).

| # | Фаза | Что делает |
|---|------|------------|
| 1 | CoreValidationPhase | Проверка DI-резолва всех интерфейсов ядра |
| 2 | TileMapGenPhase | Генерация тайловой карты |
| 3 | WorldInitPhase | Инициализация мира (время, локации, фракции) |
| 4 | PlayerSpawnPhase | Спавн игрока (центр карты) |
| 5 | NPCSpawnPhase | Спавн NPC |
| 6 | FormationInitPhase | Инициализация формаций |
| 7 | ChargerInitPhase | Инициализация зарядников |
| 8 | QuestInitPhase | Инициализация квестов |
| 9 | UIInitPhase | Инициализация UI |
| 10 | FinalizePhase | Финализация (публикация SceneAssemblyCompletedEvent) |

### 6.2. Интерфейс фазы

```
public interface ISceneAssemblyPhase
{
    string PhaseName { get; }
    int PhaseOrder { get; }
    Task ExecuteAsync(CancellationToken ct = default);
}
```

Фазы регистрируются в SceneOrchestrator через `SceneAssemblyRegistrar` (открытый список — новые фазы добавляются декларативно).

### 6.3. Сборщик сцены (Scene Builder)

Программно создаёт узлы сцены из данных конфигурации:
- Камера (ортографическая, следование за игроком).
- UI-корень (overlay).
- Система ввода.
- World Root (сетка + тайловый слой + объекты).
- Player (спрайт + процедурный визуал).
- Источник 2D-освещения (глобальный).

> **Концепция:** Все узлы создаются программно из конфигурации, а не загружаются из готовых сцен. Это позволяет AI-агентам авторить сцены в виде текста и не требует ручной работы в редакторе.

### 6.4. GameSession

Управление жизненным циклом сессии:

| Действие | Что происходит |
|----------|----------------|
| NewGame | SceneOrchestrator.RunAssembly() → Playing |
| LoadGame | SaveService.Load() → SceneOrchestrator.RunAssembly() (LoadMode) → Playing |
| Pause | Подписка на GamePausedEvent |
| Resume | Подписка на GameResumedEvent |
| SaveAndQuit | SaveService.Save() → Cleanup |
| QuitWithoutSaving | Cleanup |

---

## 7. Шаблон модуля

Каждый модуль следует единому шаблону:

```
Modules/Xxx/
├── XxxModule.cs           # Точка входа (IStartable, ITickable)
├── XxxModuleServices.cs   # Регистрация внутренних сервисов
├── XxxService.cs          # Реализация IXxxService
├── XxxConfig.cs           # Конфигурация (class, не struct)
├── XxxCalculator.cs       # Чистые формулы (где применимо)
└── XxxHelper.cs           # Вспомогательные классы (где применимо)
```

### 7.1. XxxModule — точка входа

- Реализует `IStartable` (инициализация при запуске) и `ITickable` (кадровое обновление).
- Получает зависимости через `[Inject]` интерфейсы Core.
- Вызывает `Configure()` через конкретный тип — допустимо только в пределах модуля.

### 7.2. XxxService — реализация интерфейса

- Реализует `IXxxService` из Core.Interfaces.
- Хранит состояние модуля.
- Публикует/подписывается на события через шину.

### 7.3. XxxModuleServices — регистрация

- Статический класс с методом `Register(IContainerBuilder)`.
- Регистрирует интерфейс + реализацию + точку входа + внутренние сервисы.
- Вызывается из корневого конфигуратора.

> Подробно по модулям — в `MODULE_STRUCTURE.md`.

---

## 8. Время и тики

### 8.1. Принцип

**1 тик = 1 минута игрового времени.** Симуляция отвязана от frame rate.

### 8.2. Скорости времени

| Режим | Соответствие |
|-------|--------------|
| Пауза | Время остановлено |
| Нормальная | 1 секунда реального = 1 минута игрового |
| Ускоренная | 1 секунда = 5 минут |
| Быстрая | 1 секунда = 15 минут |

### 8.3. Длительности действий

| Действие | Тиков |
|----------|-------|
| Движение (1 клетка) | 1 |
| Атака | 1 |
| Ход боя | 2 |
| Медитация | 30–480 |
| Прорыв | 480 (8 игровых часов) |
| Разговор | 5 |
| Сбор ресурсов | 10 |

### 8.4. Tick-батчинг

Не все системы обновляются каждый тик:

| Система | Период |
|---------|--------|
| Qi-регенерация | Каждые 10 тиков |
| Автосохранение | Каждые 60 тиков (по триггерам — см. Save) |
| Spinal AI | Каждый тик (1–10 мс) |
| Neural Router | Каждые ~3 тика (10–50 мс) |
| Brain Controller | Каждые ~10 тиков (100–500 мс) |

### 8.5. WorldTime

| Параметр | Значение |
|----------|----------|
| Начальный год | 1864 (Э.С.М.) |
| Дней в месяце | 30 |
| Месяцев в году | 12 |
| Часов в дне | 24 |
| Сезоны | Тёплый (1–9), Холодный (10–12) |

---

## 9. Сохранение

### 9.1. Принципы

- **JSON** — human-readable, debuggable, portable. Опционально binary + GZIP для критичных данных.
- **ISaveable pattern:** сервисы реализуют `ISaveable { SaveKey, CaptureState, RestoreState }`.
- **SaveDataAggregator** собирает данные от всех `ISaveable` через шину событий при `SaveRequestedEvent`.
- **Тайловые данные регенерируются** из seed + delta, не сохраняются поштучно.
- **`long`** для всех Qi-значений (не `float`).

### 9.2. Триггеры автосохранения

- Смена локации
- Получение новой техники
- Получение важного предмета
- Прорыв уровня культивации
- Завершение боя

### 9.3. Структура сохранения

| Файл | Содержимое | Размер |
|------|------------|--------|
| `main.sav` | Состояние игрока, техники, инвентарь | 10–50 KB |
| `chunks/` | Чанки мира (chunk-based persistence) | — |
| `locations/` | Состояние локаций | — |
| `metadata.sav` | Метаданные сессии | <1 KB |

**Оценка роста:** 100h ~ 5–15 KB compressed. 1000h ~ 100 KB. Extreme 2000 locations ~ 1–2 MB.

---

## 10. Анти-паттерны (ЗАПРЕЩЕНЫ)

```
// ❌ Singleton
public static MyManager Instance { get; private set; }

// ❌ ServiceLocator
var mgr = ServiceLocator.Get<CombatManager>();

// ❌ Поиск узла на сцене (FindObjectOfType-стиль)
var ctrl = FindObjectOfType<PlayerController>();

// ❌ Прямой поиск по имени
var obj = Scene.Find("Player");

// ❌ Кросс-модульная прямая ссылка на конкретный класс
[Inject] ChargerService _charger;  // Нужен IChargerService!

// ❌ Прямой вызов обработчика другого модуля
bodyService.ApplyDamage(part, dmg);  // Из Combat — только через шину!

// ❌ C# event для межмодульной коммуникации
public event Action<float> OnDamageDealt;  // Используйте шину!

// ❌ Coroutine/timer-цикл для долгих операций
// Используйте async/await!

// ✅ DI через интерфейс ядра
[Inject] IChargerService _chargerService;

// ✅ Шина событий для межмодульной связи
[Inject] IPublisher<BodyPartDamagedEvent> _damagePub;
```

---

## 11. Иерархия источников истины

| Приоритет | Документ | Область |
|-----------|----------|---------|
| 1 | `09_workflow/ALGORITHMS.md` | Формулы, расчёты, мягкие капы |
| 2 | `04_entities/ENTITY_TYPES.md` (TODO) | Типы сущностей, морфологии, материалы |
| 3 | `06_player/EQUIPMENT_SYSTEM.md` (TODO) | Грейды, прочность, слоты экипировки |
| 4 | `02_systems/ELEMENTS_SYSTEM.md` (TODO) | Стихии, взаимодействия |
| 5 | `02_systems/BUFF_MODIFIERS_SYSTEM.md` (TODO) | Баффы/дебаффы, модификаторы |
| 6 | `01_architecture/ARCHITECTURE.md` | Общая архитектура |
| 7 | Остальные | Конкретные системы |

> **Принцип:** Документ ниже по иерархии НЕ может противоречить документу выше. Все формулы и числа — в `09_workflow/ALGORITHMS.md`.

---

## 12. Связанные документы

| Документ | Описание |
|----------|----------|
| `MODULE_STRUCTURE.md` | Детально по 16 модулям: имя, ответственность, зависимости, контракты, tick-участие |
| `DI_AND_EVENTBUS.md` | Паттерны DI + шина событий: интерфейсы, readonly struct контракты, регистрация, lifetime scopes |
| `PERFORMANCE_STRATEGY.md` | Zero-GC, pooling, tick batching, многопоточность, performance budgets, hardware tiers |
| `FILE_TREE.md` | Предлагаемая структура файлов проекта |
| `00_overview/PROJECT_CONCEPT.md` | Концепция игры |
| `00_overview/GLOSSARY.md` | Глоссарий терминов |
| `00_overview/TECHNOLOGY_DECISIONS.md` | Технологические решения |
| `09_workflow/ALGORITHMS.md` | Все формулы и расчёты |

---

*Документ создан в рамках Task 2-a: engine-agnostic rewrite. Источники: `docs/ARCHITECTURE.md` v4.0, `docs/ARCHITECTURE_CODE.md` v3.18, `docs/ARCHITECTURE_IMPL.md` v1.1, `docs_old/ARCHITECTURE.md` v21, `docs_temp/ENGINE_CHOICE_ANALYSIS.md`.*

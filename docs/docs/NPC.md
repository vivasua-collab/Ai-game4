# 🧑‍🤝‍🧑 Система NPC — Модульная Архитектура (ModuleServices)

**Версия:** 2.0
**Дата:** 2026-05-20
**Проект:** Cultivation World Simulator (Unity 6.3 URP 2D)
**Статус:** ✅ Актуально (ModuleServices архитектура, Фаза 0)

---

## ⚠️ Назначение документа

> Этот документ описывает **модульную архитектуру NPC** на основе паттерна ModuleServices:
> NPCModule → NPCModuleServices → внутренние сервисы. Для пайплайна генерации NPC
> см. [NPC_ASSEMBLY_PIPELINE.md](./NPC_ASSEMBLY_PIPELINE.md), для теоретических основ AI см.
> [NPC_AI_SYSTEM.md](./NPC_AI_SYSTEM.md), для общей архитектуры проекта см.
> [ARCHITECTURE_CODE.md](./ARCHITECTURE_CODE.md).

---

## 📋 Краткий обзор

NPC в проекте — это **чистый C# модуль** (без MonoBehaviour), управляемый через
паттерн **ModuleServices**: единственная точка входа `NPCModule` (IStartable, ITickable, IDisposable),
регистрация сервисов через `NPCModuleServices.Register()`, данные в `NPCState`.

```
┌─────────────────────────────────────────────────────────────────────┐
│  NPCModule (IStartable, ITickable, IDisposable)                    │
│     └── NPCModuleServices.Register() — DI-регистрация              │
│                                                                     │
│  Сервисы:                                                           │
│  ├── NPCService (INPCService, ISaveable) — состояние, запросы       │
│  ├── NPCSpawnerService (INPCSpawnerService) — спавн/деспавн         │
│  ├── NPCAIService — упрощённый Behaviour Tree                       │
│  ├── NPCCombatAdapter — бой через MessagePipe                       │
│  ├── NPCMovementService — движение и навигация                      │
│  └── NPCRelationshipService — управление отношениями                │
│                                                                     │
│  Данные:                                                            │
│  ├── NPCState — runtime-состояние (изменяемое)                      │
│  └── NPCSaveEntry — сериализуемый срез для сохранения               │
└─────────────────────────────────────────────────────────────────────┘
```

> **Архитектурное отличие от предыдущей версии:** MonoBehaviour-компоненты
> (NPCController, NPCAI, NPCVisual) полностью удалены. Все логические подсистемы —
> чистые C# сервисы, зарегистрированные через VContainer. Визуальное отображение
> вынесено за пределы NPC-модуля (UI-модуль / отдельный визуальный слой).

---

## 🧱 NPCModule — точка входа

`NPCModule` реализует `IStartable`, `ITickable`, `IDisposable` — стандартный шаблон модуля
(см. [ARCHITECTURE_CODE.md §7](./ARCHITECTURE_CODE.md)).

### Инициализация (IStartable.Start)

```
NPCModule.Start()
├── _npcServiceImpl.Initialize()       — инициализация NPCService
├── _relationshipService.Initialize()  — подписка на DayChangedEvent
├── _aiService.Initialize()            — подписка на кросс-модульные события
├── _combatAdapter.Initialize()        — подписка на CombatStartedEvent/CombatEndedEvent
├── _movementService.Initialize()      — инициализация движения
└── _aiStateChangedSub.Subscribe()     — NPC-E01: запуск атаки при AI→Attacking
```

### Тиковая обработка (ITickable.Tick)

```
NPCModule.Tick()
├── _aiService.Tick()                  — обновление AI-состояний
└── _movementService.ProcessMovement() — обработка перемещений
```

### Очистка (IDisposable.Dispose)

Освобождает все подписки MessagePipe у каждого сервиса + подписку на `NPCAIStateChangedEvent`.

### Подписки сервисов на кросс-модульные события

| Сервис | Событие | Действие |
|--------|---------|----------|
| NPCService | `QiChangedEvent` | Кэширование Ци игрока для AI |
| NPCRelationshipService | `DayChangedEvent` | Затухание отношений (NPC-B05) |
| NPCAIService | `DamageAppliedEvent` | Обновление угроз |
| NPCAIService | `BodyPartSeveredEvent` | Реакция на ампутацию |
| NPCAIService | `PlayerPositionChangedEvent` | Обнаружение игрока |
| NPCCombatAdapter | `CombatStartedEvent` | Вход в бой |
| NPCCombatAdapter | `CombatEndedEvent` | Выход из боя |
| NPCCombatAdapter | `DamageAppliedEvent` | Обработка урона |

> **EVT-01:** Все кросс-модульные взаимодействия — исключительно через MessagePipe.
> Прямые ссылки на сервисы других модулей ЗАПРЕЩЕНЫ.

---

## 🔄 NPCModuleServices — регистрация DI

`NPCModuleServices.Register()` — централизованная регистрация всех сервисов NPC-модуля.
Вызывается из `GameLifetimeScope.Configure()`.

```
NPCModuleServices.Register(builder, options)
├── NPCConfig (RegisterInstance)
├── NPCService → INPCService + ISaveable
├── NPCSpawnerService → INPCSpawnerService
├── NPCRelationshipService (внутренний)
├── NPCAIService (внутренний)
├── NPCCombatAdapter (внутренний)
├── NPCMovementService (внутренний)
├── NPCModule → IStartable + ITickable + IDisposable
└── RegisterBuildCallback → SetConfig(config)
```

> **MSV-01 (Фаза 17):** ModuleServices выносит регистрацию внутренних сервисов из LifetimeScope,
> делая его компактным. Новые внутренние сервисы добавляются только в NPCModuleServices.Register().

---

## 📐 NPCState — runtime-состояние

`NPCState` — изменяемый объект, хранящий текущее состояние NPC. Создаётся при спавне,
уничтожается при деспавне. Файл: `Modules/NPC/Data/NPCState.cs`.

```csharp
public class NPCState
{
    // Идентификация
    public string NpcId;           // GUID
    public string PresetId;
    public string DisplayName;

    // Классификация
    public NPCRole Role;
    public NPCCategory Category;
    public PersonalityTrait Personality;
    public SoulType SoulType;
    public Morphology Morphology;
    public BodyMaterial BodyMaterial;

    // Культивация
    public CultivationLevel CultivationLevel;
    public int SubLevel;
    public CoreQuality CoreQuality;
    public long MaxQi;
    public long CurrentQi;
    public float Conductivity;

    // Здоровье
    public int MaxHealth;
    public int CurrentHealth;

    // AI-состояние
    public NPCAIState AIState;
    public string TargetId;
    public float StateTimer;

    // Отношения
    public int AttitudeScore;       // -100..+100

    // Флаги
    public bool IsAlive;
    public bool IsInCombat;

    // Принадлежность
    public string SectId;
    public string CurrentLocation;

    // Позиция
    public Position2D Position;

    // Угрозы (sourceId → threatLevel)
    public Dictionary<string, float> Threats;

    // Кэш Ци из QiChangedEvent
    public long CachedPlayerQi;
    public int CachedPlayerLevel;
}
```

> **NPCData (read-only DTO):** Зеркалирует поля NPCState для внешнего API.
> Определён в `Core.Data` (INPCService.cs). Внешние модули получают данные
> только через NPCData, без доступа к изменяемому NPCState.

---

## 📦 Сервисы NPC

### NPCService (INPCService, ISaveable)

Единый центр управления состоянием NPC.

| Метод | Описание |
|-------|----------|
| `GetNPC(npcId)` | Получить NPCData по ID |
| `GetNearbyNPCIds(position, radius)` | NPC в радиусе |
| `GetAttitude(npcId, targetId)` | Отношение к цели |
| `ModifyAttitude(npcId, targetId, delta)` | Изменить отношение |
| `IsAlive(npcId)` | Жив ли NPC |
| `GetAIState(npcId)` | Текущее AI-состояние |
| `GetAllNPCIds()` | Все ID активных NPC |
| `SetAIState(npcId, state)` | Установить AI-состояние |
| `UpdatePosition(npcId, pos)` | Обновить позицию |
| `GetNPCState(npcId)` | Внутренний доступ к NPCState |

> **NPC-A07:** GetAllNPCIds/SetAIState/UpdatePosition добавлены в интерфейс (баг: отсутствовали).

### NPCSpawnerService (INPCSpawnerService)

| Метод | Описание |
|-------|----------|
| `SpawnNPC(presetId, position)` | Создать NPC по пресету |
| `DespawnNPC(npcId)` | Удалить NPC из мира |
| `GetSpawnedNPCIds()` | ID заспавненных NPC |
| `ActiveNPCCount` | Количество активных NPC |

> **NPC-A12:** ActiveNPCCount добавлен в INPCSpawnerService (баг: отсутствовал метод).

### NPCAIService — упрощённый Behaviour Tree

Обрабатывает AI-решения для всех NPC каждый тик:
- Система угроз с затуханием (`ThreatDecayRate` за секунду)
- Переключение AI-состояний на основе конфигурации и Personality
- Модификаторы весов от `PersonalityTrait [Flags]`
- Визуальная обратная связь через события MessagePipe

### NPCCombatAdapter — адаптер боя через MessagePipe

Связь NPC с боевой системой исключительно через MessagePipe (NPC-B04):
- Подписка на `CombatStartedEvent` / `CombatEndedEvent`
- `StartAttack(npcId, targetId)` — инициирует атаку через CombatModule

### NPCMovementService — движение и навигация

- Обработка перемещения NPC каждый тик
- Учитывает `DefaultMoveSpeed`, `FleeSpeedMultiplier`
- `ProcessMovement()` — вызывается из NPCModule.Tick()

### NPCRelationshipService — управление отношениями

- Хранение отношения к целям (AttitudeScore: -100..+100)
- Затухание: через `DayChangedEvent` (NPC-B05), `AttitudeDecayStartDays` дней до начала,
  −`AttitudeDecayPerDay`/день к нейтральному
- Флаги family/sworn/master/disciple — **без затухания**

---

## 📐 NPCConfig — конфигурация модуля

Файл: `Modules/NPC/NPCConfig.cs` (BD-48: class, не struct)

### Базовые параметры

| Поле | По умолч. | Описание |
|------|-----------|----------|
| `AggroRadius` | 5f | Радиус обнаружения врагов |
| `AttackRadius` | 1.5f | Радиус атаки |
| `FleeRadius` | 8f | Радиус бегства |
| `PatrolRadius` | 10f | Радиус патруля |
| `ThreatDecayRate` | 2f | Затухание угрозы/сек |
| `ThreatThreshold` | 50f | Порог угрозы для атаки |
| `FleeHealthRatio` | 0.2f | Порог HP для бегства |
| `AttitudeDecayPerDay` | 1 | Затухание отношения/день |
| `AttitudeDecayStartDays` | 7 | Дней до начала затухания |
| `MaxActiveNPCs` | 100 | Макс. активных NPC |
| `DefaultMoveSpeed` | 2f | Скорость движения (ед/сек) |
| `FleeSpeedMultiplier` | 1.5f | Множитель скорости бегства |

### Параметры пайплайна генерации (Шаг 1 — Soul)

| Поле | По умолч. | Описание |
|------|-----------|----------|
| `BaseCoreCapacity` | 1000f | Базовая ёмкость ядра (L1.0) |
| `CoreCapacityGrowth` | 1.1f | Множитель роста ёмкости ядра |
| `CoreQualityMultipliers` | {0.5..2.0} | 7 градаций: Fragmented..Transcendent. ЕДИНЫЕ для игрока и NPC (ПРОТИВОРЕЧИЕ #1) |
| `CoreQualityWeightsCharacter` | {5..1} | Веса качества для Character |
| `CoreQualityWeightsCreature` | {20..0} | Веса качества для Creature |
| `AwakeningTypeWeights` | {0,20,50,20,10} | 5 записей: None=0, Natural=20, Guided=50, Artifact=20, Forced=10 (ПРОТИВОРЕЧИЕ #2) |
| `LevelDeltaWeights` | {18,36,41,5} | -2, -1, 0, +1 |
| `LocationLevelCapOffset` | 0.9f | Кап уровня: npcLevel ≤ locationLevel + X |
| `AgingMultipliers` | {1.0..0.0} | По MortalStage (10 значений) |
| `ConductivityGrowthFactors` | {1.0,1.2,1.5,2.0,3.0,5.0,8.0,12.0} | levelGrowthFactor: L0..L7+ (ПРОТИВОРЕЧИЕ #4) |

### Параметры генерации техник (Шаг 6)

| Поле | По умолч. | Описание |
|------|-----------|----------|
| `TechniqueGradeWeights` | {60,30,9,1} | Common, Refined, Perfect, Transcendent |
| `TechniqueGradeMultipliers` | {1.0,1.3,1.6,2.0} | Из документации (НЕ Legacy) |

### Параметры генерации экипировки (Шаг 5)

| Поле | Описание |
|------|----------|
| `EquipmentGradeWeightsByLevel` | 5 градаций × 6 уровней (L1..L9) |

### Таймауты AI

| Поле | По умолч. | Описание |
|------|-----------|----------|
| `IdleTimeout` | 10f | Таймаут бездействия (сек) |
| `WanderTimeout` | 15f | Таймаут блуждания (сек) |
| `PatrolTimeout` | 30f | Таймаут патруля (сек) |
| `FleeTimeout` | 5f | Таймаут бегства (сек) |
| `FollowDistance` | 2f | Дистанция следования (ед) |

---

## 🧠 NPCAI — система поведения

### AI-состояния

| Состояние | Описание | Когда |
|-----------|----------|-------|
| Idle | Бездействие | По умолчанию, после отдыха |
| Wandering | Случайное блуждание | Monster, Enemy по умолчанию |
| Patrolling | Патруль по точкам | Guard по умолчанию |
| Following | Следование за целью | При приказе |
| Fleeing | Бегство | HP < FleeHealthRatio + cautiousness > aggressiveness |
| Attacking | Атака цели | Угроза > ThreatThreshold + aggressiveness > cautiousness |
| Defending | Защита | — |
| Meditating | Медитация | — |
| Cultivating | Культивация (восстановление Ци) | Cultivator, Disciple по умолчанию |
| Resting | Отдых (восст. HP) | После блуждания |
| Trading | Торговля | Merchant по умолчанию |
| Talking | Разговор | Социальный NPC |
| Working | Работа | Ambitious NPC |
| Searching | Поиск | — |
| Guarding | Охрана | — |

### PersonalityTrait [Flags] — влияние на поведение

| Флаг | Значение | Эффект на веса AI |
|------|----------|-------------------|
| Aggressive | 1 | +50% patrol, −30% rest |
| Cautious | 2 | +50% rest, −30% patrol |
| Treacherous | 4 | Если Attitude < Neutral: −50% talk, +40% patrol |
| Ambitious | 8 | +30% cultivate, +30% patrol, +20% work |
| Loyal | 16 | −50% idle, +30% work, +20% talk |
| Pacifist | 32 | −50% patrol, +30% rest, +20% cultivate |
| Curious | 64 | +40% wander, +30% talk |
| Vengeful | 128 | +30% patrol |

### Система угроз

- `AddThreat(sourceId, level)` — добавить угрозу (при получении урона: `damage × 0.5`)
- Затухание: `ThreatDecayRate`/сек
- Угроза > `ThreatThreshold` → `knownTargets.Add(sourceId)`, переход в Attacking
- Aggressiveness > Cautiousness → атака, иначе бегство

---

## 📡 События (NPCContracts.cs)

Все события — `readonly struct` (нулевая GC-аллокация).

| Событие | Поля | Статус публикации |
|---------|------|-------------------|
| `NPCSpawnedEvent` | NpcId, PresetId | ✅ Публикуется при спавне |
| `NPCDespawnedEvent` | NpcId | ✅ Публикуется при деспавне |
| `AttitudeChangedEvent` | NpcId, TargetId, OldAttitude, NewAttitude | ✅ Публикуется при изменении отношения |
| `NPCDeathEvent` | NpcId, KillerId | ✅ Публикуется при смерти |
| `NPCInteractedEvent` | NpcId, InitiatorId, InteractionType | ⚠️ Определено, но ещё НЕ публикуется |
| `NPCAIStateChangedEvent` | NpcId, OldState, NewState | ✅ Публикуется при смене AI-состояния |
| `NPCDamagedEvent` | NpcId, SourceId, Damage, HealthRatio | ✅ Публикуется при получении урона |

> **NPCInteractedEvent:** Контракт определён в NPCContracts.cs, но публикация ещё не реализована.
> Планируется к активации в одной из ближайших итераций (после реализации системы взаимодействий).

---

## 💾 Сохранение NPC

`NPCService` реализует `ISaveable` — участвует в агрегации сохранений через `SaveDataAggregator`.

```
NPCSaveEntry (сериализуемый DTO):
├── NpcId, PresetId, DisplayName
├── Role (int), Category (int), Personality (int)
├── SoulType, Morphology, BodyMaterial
├── CultivationLevel, SubLevel, CoreQuality
├── MaxQi, CurrentQi, Conductivity
├── MaxHealth, CurrentHealth
├── AIState (int), TargetId, StateTimer
├── AttitudeScore
├── IsAlive, IsInCombat
├── SectId, CurrentLocation
└── Position (x, y)
```

> **ISaveable:** NPCService.SaveKey = "npc", CaptureState() возвращает список NPCSaveEntry,
> RestoreState() восстанавливает состояние всех NPC.

---

## 📊 Отношения и Attitude

### Attitude (отношение к Player, числовое -100..+100)

| Диапазон | Attitude | Поведение |
|----------|----------|-----------|
| 80..100 | SwornAlly | Самопожертвование |
| 50..79 | Allied | Лояльность |
| 10..49 | Friendly | Помощь, торговля |
| −9..9 | Neutral | Безразличие |
| −20..−10 | Unfriendly | Избегание |
| −50..−21 | Hostile | Атака при провокации |
| −100..−51 | Hatred | Атака без предупреждения |

### NPCRelationshipService

Внутренний сервис модуля NPC (не публичный интерфейс):
- Хранит отношения к целям
- Затухание: через `DayChangedEvent`, `AttitudeDecayStartDays` дней до начала,
  −`AttitudeDecayPerDay`/день к нейтральному
- Флаги family/sworn/master/disciple — **без затухания**
- Публикует `AttitudeChangedEvent` при изменении

---

## 🚀 Пайплайн генерации NPC (Планируется)

Подробная документация пайплайна: [NPC_ASSEMBLY_PIPELINE.md](./NPC_ASSEMBLY_PIPELINE.md)

### Предстоящие сервисы (Фаза 1-2)

| Сервис | Описание | Статус |
|--------|----------|--------|
| `SoulGenerator` | Генерация души NPC (SoulType, CoreQuality, AwakenType) | 📋 Планируется (Фаза 1) |
| `NPCAssemblyService` | Сборка NPC из компонентов (Soul + Body + Qi + Techniques + Equipment) | 📋 Планируется (Фаза 1-2) |
| `NPCNameGenerator` | Генерация имён NPC по роли и культуре | 📋 Планируется (Фаза 2) |

> Текущий спавн работает через `NPCSpawnerService.SpawnNPC(presetId, position)` —
> создание NPCState по пресету. Полный пайплайн генерации будет реализован
> в Фазе 1-2 с добавлением SoulGenerator, NPCAssemblyService и NPCNameGenerator.

---

## 📂 Файловая структура NPC

### Модуль (`Modules/NPC/`)

| Файл | Назначение |
|------|------------|
| `NPCModule.cs` | Точка входа (IStartable, ITickable, IDisposable) |
| `NPCModuleServices.cs` | DI-регистрация всех сервисов |
| `NPCLifetimeScope.cs` | DI-конфигуратор (наследует ModuleLifetimeScope) |
| `NPCService.cs` | Реализация INPCService + ISaveable |
| `NPCSpawnerService.cs` | Реализация INPCSpawnerService |
| `NPCAIService.cs` | Упрощённый Behaviour Tree |
| `NPCCombatAdapter.cs` | Адаптер боя через MessagePipe |
| `NPCMovementService.cs` | Движение и навигация |
| `NPCRelationshipService.cs` | Управление отношениями |
| `NPCConfig.cs` | Конфигурация модуля (class, BD-48) |
| `Data/NPCState.cs` | Runtime-состояние NPC |

### Контракты (Core/Messaging/Contracts/)

| Файл | Назначение |
|------|------------|
| `NPCContracts.cs` | 7 событий NPC (readonly struct) |

### Интерфейсы (Core/Interfaces/)

| Файл | Назначение |
|------|------------|
| `INPCService.cs` | Публичный интерфейс + NPCData DTO |
| `INPCSpawnerService.cs` | Публичный интерфейс спавнера |

### Тесты (Tests/Modules/NPC/)

| Файл | Назначение |
|------|------------|
| `NPCServiceTests.cs` | Тесты NPCService |
| `NPCRelationshipServiceTests.cs` | Тесты NPCRelationshipService |

### Core Enums (`Core/Data/Enums.cs`)

- `CultivationLevel` (None..Ten, 1-10)
- `Attitude` (Hatred..SwornAlly)
- `PersonalityTrait [Flags]` (Aggressive=1..Vengeful=128)
- `NPCRole` (Monster, Guard, Merchant, Cultivator, Passerby, Elder, Disciple, Enemy)
- `NPCCategory` (Temp, Plot, Unique)
- `NPCAIState` (Idle, Wandering, Patrolling, Following, Fleeing, Attacking, Defending, Meditating, Cultivating, Resting, Trading, Talking, Working, Searching, Guarding)
- `SoulType`, `Morphology`, `BodyMaterial`, `CoreQuality`

---

## 🔗 Связанная документация

### Основная документация (docs/)

| Документ | Описание | Связь |
|----------|----------|-------|
| [NPC_ASSEMBLY_PIPELINE.md](./NPC_ASSEMBLY_PIPELINE.md) | Пайплайн генерации NPC (Soul→Body→Qi→Tech→Equip) | SoulGenerator, NPCAssemblyService |
| [NPC_AI_SYSTEM.md](./NPC_AI_SYSTEM.md) | AI NPC: Spinal Controller, Behaviour Tree, Neural Router | Теоретическая основа AI |
| [ARCHITECTURE_CODE.md](./ARCHITECTURE_CODE.md) | Общая архитектура проекта | ModuleServices pattern, §7 |
| [QI_SYSTEM.md](./QI_SYSTEM.md) | Ци: накопление, проводимость, формула MaxQi | Qi модуль |
| [BODY_SYSTEM.md](./BODY_SYSTEM.md) | Kenshi-style система тела | Body модуль |
| [COMBAT_SYSTEM.md](./COMBAT_SYSTEM.md) | Боевая система | NPCCombatAdapter |
| [SAVE_SYSTEM.md](./SAVE_SYSTEM.md) | Сохранение/загрузка | NPCSaveEntry, ISaveable |

---

## 📝 История изменений

| Дата | Изменение |
|------|-----------|
| 2026-03-30 | Начальная реализация NPCController, NPCAI, NPCData |
| 2026-04-11 | Fix-07: Disposition→Attitude+PersonalityTrait, SaveData, угрозы, lifespan |
| 2026-04-30 | GAP-4: авторегистрация в WorldController; NPCVisual, NPCInteractable, NPCSceneSpawner, Phase19NPCPlacement |
| 2026-05-20 | Фаза 0: полная переработка под ModuleServices архитектуру. Удалены MonoBehaviour (NPCController, NPCAI, NPCVisual). Данные в NPCState + NPCSaveEntry. NPCModuleServices регистрация. Параметры пайплайна генерации в NPCConfig |

---

*Документ создан: 2025-05-01*
*Редактировано: 2026-05-20 18:00:11 UTC — Фаза 0: П#1 CoreQualityMultipliers {0.5..2.0}, П#2 AwakeningTypeWeights 5 записей, П#4 ConductivityGrowthFactors*

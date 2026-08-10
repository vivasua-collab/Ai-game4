# NPC — сущности и конфигурация

> **Раздел:** Сущности мира
> **Статус:** Концептуальная спецификация (дизайн-документ)
> **Самостоятельный документ:** не требует иных файлов для понимания.
>
> Этот документ описывает **классификацию, личность и конфигурацию NPC**. Пайплайн генерации — см. `04_entities/NPC_ASSEMBLY_PIPELINE.md`. Теория AI — см. `04_entities/NPC_AI_SYSTEM.md`.

---

## 1. Назначение

NPC — это **подтип SoulEntity** (`soulType = character` или `creature`), управляемый AI-контроллером (в отличие от игрока, управляемого человеком). NPC являются основным населением мира: жители городов, охранники, торговцы, культиваторы, монстры, старейшины сект.

> **Архитектурный принцип:** Вся логика NPC — **чистый C# модуль** без зависимости от движкового API. Движок используется только для визуального представления (отдельный визуальный слой). Это позволяет:
> - тестировать логику NPC без движка;
> - переносить модуль между движками без переработки;
> - выполнять AI-расчёты на worker threads.

---

## 2. Категории NPC (3 категории)

| Категория | Описание | Поведение | Сохранение |
|-----------|----------|-----------|------------|
| `Temp`    | Временные NPC | Упрощённое AI | Только в памяти (без persist) |
| `Plot`    | Сюжетные NPC | Полное AI | Сохранение в файл |
| `Unique`  | Уникальные NPC | Полное AI + история | Полное сохранение + расширенные данные |

---

## 3. Роли NPC (8 ролей)

| Роль | Описание | Поведение по умолчанию |
|------|----------|------------------------|
| `Monster`    | Монстр, зверь | Wandering |
| `Guard`      | Охранник | Patrolling |
| `Merchant`   | Торговец | Trading |
| `Cultivator` | Культиватор | Cultivating |
| `Passerby`   | Прохожий | Idle |
| `Elder`      | Старейшина | Idle / Talking |
| `Disciple`   | Ученик | Cultivating |
| `Enemy`      | Враг | Wandering / Aggressive |

---

## 4. Личность NPC

### 4.1. Attitude (отношение к цели, числовое −100..+100)

| Диапазон | Attitude | Поведение |
|----------|----------|-----------|
| 80..100   | SwornAlly (Клятвенный союзник) | Самопожертвование |
| 50..79    | Allied (Союзник) | Лояльность |
| 10..49    | Friendly (Друг) | Помощь, торговля |
| −9..9     | Neutral (Нейтральный) | Безразличие |
| −20..−10  | Unfriendly (Недоброжелательный) | Избегание |
| −50..−21  | Hostile (Враждебный) | Атака при провокации |
| −100..−51 | Hatred (Ненависть) | Атака без предупреждения |

### 4.2. PersonalityTrait [Flags] — 8 черт характера

Каждая черта — битовая маска. NPC может иметь несколько черт одновременно.

| Флаг | Значение | Эффект на веса AI |
|------|----------|-------------------|
| `Aggressive`  | 1   | +50% patrol, −30% rest |
| `Cautious`    | 2   | +50% rest, −30% patrol |
| `Treacherous` | 4   | Если Attitude < Neutral: −50% talk, +40% patrol |
| `Ambitious`   | 8   | +30% cultivate, +30% patrol, +20% work |
| `Loyal`       | 16  | −50% idle, +30% work, +20% talk |
| `Pacifist`    | 32  | −50% patrol, +30% rest, +20% cultivate |
| `Curious`     | 64  | +40% wander, +30% talk |
| `Vengeful`    | 128 | +30% patrol |

> Черты характера **модифицируют веса AI** — см. `04_entities/NPC_AI_SYSTEM.md`.

---

## 5. Конфигурация NPC (NPCConfig)

### 5.1. Базовые параметры (по умолчанию)

| Поле | По умолчанию | Описание |
|------|--------------|----------|
| `AggroRadius`              | **5**    | Радиус обнаружения врагов (ед.) |
| `AttackRadius`             | **1.5**  | Радиус атаки (ед.) |
| `FleeRadius`               | 8        | Радиус бегства (ед.) |
| `PatrolRadius`             | **10**   | Радиус патруля (ед.) |
| `ThreatDecayRate`          | 2        | Затухание угрозы/сек |
| `ThreatThreshold`          | 50       | Порог угрозы для атаки |
| `FleeHealthRatio`          | 0.2      | Порог HP для бегства (20%) |
| `AttitudeDecayPerDay`      | 1        | Затухание отношения/день |
| `AttitudeDecayStartDays`   | 7        | Дней до начала затухания |
| `MaxActiveNPCs`            | **100**  | Макс. активных NPC |
| `DefaultMoveSpeed`         | **2**    | Скорость движения (ед/сек) |
| `FleeSpeedMultiplier`      | **1.5**  | Множитель скорости бегства |

> **Производительность:** `MaxActiveNPCs = 100` — стандартный потолок для одной сцены. Для мегаполисов (до 2000 NPC) требуется chunking — см. `01_architecture/PERFORMANCE_STRATEGY.md`.

### 5.2. Таймауты AI

| Поле | По умолчанию | Описание |
|------|--------------|----------|
| `IdleTimeout`    | 10 сек | Таймаут бездействия |
| `WanderTimeout`  | 15 сек | Таймаут блуждания |
| `PatrolTimeout`  | 30 сек | Таймаут патруля |
| `FleeTimeout`    | 5 сек  | Таймаут бегства |
| `FollowDistance` | 2 ед.  | Дистанция следования |

### 5.3. Параметры пайплайна генерации

Эти параметры управляют генерацией NPC — см. `04_entities/NPC_ASSEMBLY_PIPELINE.md`.

| Поле | По умолчанию | Описание |
|------|--------------|----------|
| `BaseCoreCapacity`         | 1000  | Базовая ёмкость ядра (L1.0) |
| `CoreCapacityGrowth`       | 1.1   | Множитель роста ёмкости ядра |
| `CoreQualityMultipliers`   | {0.5..2.0} | 7 градаций: Fragmented..Transcendent. **Единые для игрока и NPC.** |
| `CoreQualityWeightsCharacter` | {5..1}  | Веса качества для Character |
| `CoreQualityWeightsCreature`  | {20..0} | Веса качества для Creature |
| `AwakeningTypeWeights`     | {0, 20, 50, 20, 10} | 5 записей: None/Natural/Guided/Artifact/Forced |
| `LevelDeltaWeights`        | {18, 36, 41, 5} | −2, −1, 0, +1 |
| `LocationLevelCapOffset`   | 0.9   | Кап уровня: `npcLevel ≤ locationLevel + 0.9` |
| `AgingMultipliers`         | {1.0..0.0} | По MortalStage (10 значений) |
| `ConductivityGrowthFactors` | {1.0, 1.2, 1.5, 2.0, 3.0, 5.0, 8.0, 12.0} | levelGrowthFactor: L0..L7+ |
| `TechniqueGradeWeights`    | {60, 30, 9, 1} | Common, Refined, Perfect, Transcendent |
| `TechniqueGradeMultipliers` | {1.0, 1.3, 1.6, 2.0} | Множители грейдов техник |
| `EquipmentGradeWeightsByLevel` | 5 × 6 | Грейды экипировки по уровням (L1..L9) |

---

## 6. AI-состояния (15 состояний)

| Состояние | Описание | Когда |
|-----------|----------|-------|
| `Idle`         | Бездействие | По умолчанию, после отдыха |
| `Wandering`    | Случайное блуждание | Monster, Enemy по умолчанию |
| `Patrolling`   | Патруль по точкам | Guard по умолчанию |
| `Following`    | Следование за целью | При приказе |
| `Fleeing`      | Бегство | HP < FleeHealthRatio + cautiousness > aggressiveness |
| `Attacking`    | Атака цели | Угроза > ThreatThreshold + aggressiveness > cautiousness |
| `Defending`    | Защита | — |
| `Meditating`   | Медитация | — |
| `Cultivating`  | Культивация (восстановление Ци) | Cultivator, Disciple по умолчанию |
| `Resting`      | Отдых (восстановление HP) | После блуждания |
| `Trading`      | Торговля | Merchant по умолчанию |
| `Talking`      | Разговор | Социальный NPC |
| `Working`      | Работа | Ambitious NPC |
| `Searching`    | Поиск | — |
| `Guarding`     | Охрана | — |

> Все 15 состояний управляются FSM (машиной состояний). См. `04_entities/NPC_AI_SYSTEM.md`.

---

## 7. Система угроз

- `AddThreat(sourceId, level)` — добавить угрозу (при получении урона: `damage × 0.5`).
- Затухание: `ThreatDecayRate`/сек (по умолчанию 2/сек).
- Угроза > `ThreatThreshold` → `knownTargets.Add(sourceId)`, переход в `Attacking`.
- Aggressiveness > Cautiousness → атака, иначе бегство.

---

## 8. Структура данных (концептуальная)

`NPCState` — изменяемый объект, хранящий текущее состояние NPC. Создаётся при спавне, уничтожается при деспавне.

Ключевые поля:
- **Идентификация:** `NpcId` (GUID), `PresetId`, `DisplayName`.
- **Классификация:** `Role`, `Category`, `Personality`, `SoulType`, `Morphology`, `BodyMaterial`.
- **Культивация:** `CultivationLevel`, `SubLevel`, `CoreQuality`, `MaxQi`, `CurrentQi`, `Conductivity`.
- **Здоровье:** `MaxHealth`, `CurrentHealth`.
- **AI-состояние:** `AIState`, `TargetId`, `StateTimer`.
- **Отношения:** `AttitudeScore` (−100..+100).
- **Флаги:** `IsAlive`, `IsInCombat`.
- **Принадлежность:** `SectId`, `CurrentLocation`.
- **Позиция:** `Position2D`.
- **Угрозы:** `Dictionary<sourceId, threatLevel>`.
- **Кэш:** `CachedPlayerQi`, `CachedPlayerLevel`.

> **NPCData (read-only DTO):** Зеркалирует поля NPCState для внешнего API. Внешние модули получают данные **только** через NPCData, без доступа к изменяемому NPCState.

---

## 9. Сервисы NPC

| Сервис | Назначение |
|--------|------------|
| NPCService            | Единый центр управления состоянием NPC (GetNPC, GetAttitude, ModifyAttitude, IsAlive, GetAIState, SetAIState, UpdatePosition) |
| NPCSpawnerService     | Спавн/деспавн NPC (SpawnNPC, DespawnNPC, GetSpawnedNPCIds, ActiveNPCCount) |
| NPCAIService          | Упрощённое Behaviour Tree (см. `NPC_AI_SYSTEM.md`) |
| NPCCombatAdapter      | Связь с боевой системой через шину событий |
| NPCMovementService    | Движение и навигация (grid-based pathfinding) |
| NPCRelationshipService| Управление отношениями (Attitude, затухание) |

> **Все кросс-модульные взаимодействия — исключительно через шину событий.** Прямые ссылки на сервисы других модулей запрещены.

---

## 10. События NPC

Все события — **неизменяемые структуры** (нулевая GC-аллокация).

| Событие | Поля | Статус |
|---------|------|--------|
| `NPCSpawnedEvent`       | NpcId, PresetId | Публикуется при спавне |
| `NPCDespawnedEvent`     | NpcId | Публикуется при деспавне |
| `AttitudeChangedEvent`  | NpcId, TargetId, OldAttitude, NewAttitude | Публикуется при изменении отношения |
| `NPCDeathEvent`         | NpcId, KillerId | Публикуется при смерти |
| `NPCInteractedEvent`    | NpcId, InitiatorId, InteractionType | Определено, но ещё НЕ публикуется |
| `NPCAIStateChangedEvent`| NpcId, OldState, NewState | Публикуется при смене AI-состояния |
| `NPCDamagedEvent`       | NpcId, SourceId, Damage, HealthRatio | Публикуется при получении урона |

---

## 11. Подписки на кросс-модульные события

| Сервис | Событие | Действие |
|--------|---------|----------|
| NPCService            | `QiChangedEvent`           | Кэширование Ци игрока для AI |
| NPCRelationshipService| `DayChangedEvent`          | Затухание отношений |
| NPCAIService          | `DamageAppliedEvent`       | Обновление угроз |
| NPCAIService          | `BodyPartSeveredEvent`     | Реакция на ампутацию |
| NPCAIService          | `PlayerPositionChangedEvent` | Обнаружение игрока |
| NPCCombatAdapter      | `CombatStartedEvent`       | Вход в бой |
| NPCCombatAdapter      | `CombatEndedEvent`         | Выход из боя |
| NPCCombatAdapter      | `DamageAppliedEvent`       | Обработка урона |

---

## 12. Сохранение NPC

`NPCService` является `ISaveable` — участвует в агрегации сохранений через `SaveDataAggregator`.

`NPCSaveEntry` (сериализуемый DTO):
- NpcId, PresetId, DisplayName
- Role (int), Category (int), Personality (int)
- SoulType, Morphology, BodyMaterial
- CultivationLevel, SubLevel, CoreQuality
- MaxQi, CurrentQi, Conductivity
- MaxHealth, CurrentHealth
- AIState (int), TargetId, StateTimer
- AttitudeScore
- IsAlive, IsInCombat
- SectId, CurrentLocation
- Position (x, y)

---

## 13. Архитектурное представление

NPC-модуль следует шаблону **ModuleServices**:
- точка входа модуля (`NPCModule`) реализует интерфейсы `IStartable`, `ITickable`, `IDisposable`;
- регистрация сервисов через централизованный метод `NPCModuleServices.Register(builder, options)`;
- конфигурация передаётся через `NPCConfig` (class, не struct);
- все внутренние сервисы регистрируются в DI-контейнере;
- данные хранятся в `NPCState` (изменяемое) и `NPCSaveEntry` (сериализуемое).

> **Архитектурный принцип:** Логические подсистемы NPC — **чистые C# сервисы** без зависимости от движковых компонентов. Визуальное отображение вынесено за пределы NPC-модуля (отдельный визуальный слой / UI-модуль).

---

## 14. Производительность

- **CPU @ 100 NPC:** AI ~2 мс, Qi-regen ~0.5 мс, Buff ~1 мс, A* pathfinding ~5–50 мс.
- **Память:** ~1.9 КБ/NPC + 2–4 КБ/per-entity providers.
- **AI-каденция:** трёхуровневая (Spinal 1–10 мс / Neural 10–50 мс / Brain 100–500 мс) — см. `NPC_AI_SYSTEM.md`.
- **Tick batching:** AI-расчёты распределены по тикам, не все NPC обрабатываются каждый тик.

---

## 15. Связанные документы

- `04_entities/NPC_AI_SYSTEM.md` — Теория и архитектура AI NPC
- `04_entities/NPC_ASSEMBLY_PIPELINE.md` — Пайплайн генерации NPC (8 шагов)
- `04_entities/ENTITY_TYPES.md` — Иерархия типов сущностей
- `04_entities/FACTION_SYSTEM.md` — Секты, фракции, государства (влияют на Attitude)
- `02_systems/QI_SYSTEM.md` — Система Ци, проводимость
- `02_systems/BODY_SYSTEM.md` — Система тела (HP частей тела)
- `02_systems/COMBAT_SYSTEM.md` — Боевая система
- `05_data/SAVE_SYSTEM.md` — Сохранение/загрузка (ISaveable)
- `01_architecture/PERFORMANCE_STRATEGY.md` — Tick batching, zero-GC, multithreading

---

*Концептуальный документ. Категории, роли, личность, конфигурация и параметры — канонические и обязательны к реализации.*

# CODE_REFERENCE — Cultivation World Simulator (Новая архитектура)

// Создано: 2026-05-23 06:30:00 UTC

> **Архитектура:** VContainer (DI) + MessagePipe (шина сообщений) + UniTask (zero-alloc async)
>
> **ЗАПРЕТ 3.9:** Все игровые расчёты — ТОЛЬКО integer math (промилле арифметика).
> float/double/decimal ЗАПРЕЩЕНЫ для боёв, Ци-буфера, статов.
> Константы-промилле: 1000 = ×1.0, 2000 = ×2.0, 500 = ×0.5.
>
> **Контракты:** Все сообщения MessagePipe = `readonly struct` — нулевая GC-аллокация.
>
> **Модульность:** Модули общаются ТОЛЬКО через MessagePipe или интерфейсы Core.
> Прямые ссылки между модулями ЗАПРЕЩЕНЫ (Hub-and-Spoke).
>
> **Структура модуля:** Каждый модуль содержит `*Module.cs` (инициализация),
> `*ModuleServices.cs` (DI-регистрация), `*LifetimeScope.cs` (VContainer scope).

---

## 1. Namespace Map

| Namespace | Папка | Ключевые файлы | Ключевые типы |
|-----------|-------|----------------|---------------|
| `CultivationGame.Core` | Core/ | Constants.cs, Enums.cs, VisualProvider.cs, SpriteHelper.cs | `GameConstants`, 30+ enum, `BodyPartData` |
| `CultivationGame.Core.Data` | Core/Data/ | SpeciesData.cs, TechniqueData.cs, BodyTemplate.cs, GameTile.cs, StatType.cs, Position2D.cs, NPCData.cs, ObjectDefaults.cs, InventorySlot.cs, LootEntry.cs, StatBonus.cs | SpeciesData, TechniqueData, BodyTemplate, GameTile, StatType, Position2D |
| `CultivationGame.Core.Data` | Core/Data/ScriptableObjects/ | EquipmentData.cs, ItemData.cs | EquipmentData, ItemData |
| `CultivationGame.Core.Interfaces` | Core/Interfaces/ | 35+ файлов интерфейсов | IBodyService, ICombatService, IQiService, IDamageService, IBuffService, IFormationService, INPCService, IPlayerService, IStatProvider, IBodyDataProvider, IQiDataProvider, IEquipmentDataProvider и др. |
| `CultivationGame.Core.Messaging` | Core/Messaging/Contracts/ | 20+ файлов контрактов | CombatContracts, QiContracts, NPCContracts, BodyContracts, FormationContracts, PlayerContracts, InventoryContracts и др. |
| `CultivationGame.Core.Random` | Core/Random/ | SeededRandom.cs | SeededRandom |
| `CultivationGame.Core.DI` | Core/DI/ | ModuleLifetimeScope.cs | ModuleLifetimeScope |
| `CultivationGame.Modules.Body` | Modules/Body/ | BodyService, BodyFactory, BodyPart, BodyDamageCalculator, SpeciesRegistry, BodyEnhancementSystem, BodySlotMapping, SeveredDebuffSystem, BodyTemplateProvider, BodyModule, BodyModuleServices, BodyLifetimeScope | BodyService, BodyPart, BodyFactory |
| `CultivationGame.Modules.Charger` | Modules/Charger/ | ChargerService, ChargerData, ChargerSlot, ChargerBuffer, ChargerHeat, ChargerModule, ChargerModuleServices, ChargerLifetimeScope | ChargerService, ChargerBuffer |
| `CultivationGame.Modules.Combat` | Modules/Combat/ | CombatService, DamageService, DamageCalculator, DefenseProcessor, LevelSuppression, TechniqueCapacity, TechniqueChargeService, TechniqueService, CombatConsequencesService, CombatModule, CombatModuleServices, CombatLifetimeScope, WeaponDamageCalculator, ElementalEffectService, CombatLootService, CombatAIService, StatProviderAdapter, CombatConfig | CombatService, DamageService, DamageCalculator |
| `CultivationGame.Modules.Formation` | Modules/Formation/ | FormationService, FormationCalculator, FormationQiPool, FormationEffects, FormationConfig, FormationModule, FormationModuleServices, FormationLifetimeScope | FormationService, FormationQiPool |
| `CultivationGame.Modules.Generator` | Modules/Generator/ | TechniqueGeneratorService, ItemGeneratorService, ItemDatabaseService, TechniqueRegistry, GeneratorModule, GeneratorModuleServices, GeneratorLifetimeScope | TechniqueGeneratorService, ItemDatabaseService |
| `CultivationGame.Modules.Interaction` | Modules/Interaction/ | InteractionService, DialogueService, DialogueTypewriter, InteractionModule, InteractionModuleServices, InteractionLifetimeScope | InteractionService, DialogueService |
| `CultivationGame.Modules.Inventory` | Modules/Inventory/ | InventoryService, EquipmentService, CraftingService, BackpackService, StorageRingService, EquipmentValidator, EquipmentDataProvider, EquipmentStatAggregator, MaterialService, InventoryModule, InventoryModuleServices, InventoryLifetimeScope | InventoryService, EquipmentService |
| `CultivationGame.Modules.NPC` | Modules/NPC/ | NPCService, NPCAssemblyService, SoulGenerator, NPCNameGenerator, NPCSpawnerService, NPCAIService, NPCSpeciesSelector, NPCCombatAdapter, NPCMovementService, NPCQiRegenService, NPCRelationshipService, NPCVisualService, PerkService, NPCModule, NPCModuleServices, NPCLifetimeScope | NPCService, NPCAssemblyService |
| `CultivationGame.Modules.Player` | Modules/Player/ | PlayerService, PlayerCombatAdapter, PlayerInputService, SleepService, StatService, PlayerVisualService, PlayerModule, PlayerModuleServices, PlayerLifetimeScope | PlayerService, StatService |
| `CultivationGame.Modules.Qi` | Modules/Qi/ | QiService, QiDataProvider, QiBreakthroughCalculator, QiBufferService, QiRegenCalculator, QiModule, QiModuleServices, QiLifetimeScope | QiService, QiBufferService |
| `CultivationGame.Modules.Quest` | Modules/Quest/ | QuestService, QuestProgressTracker, QuestRewardService, QuestModule, QuestModuleServices, QuestLifetimeScope | QuestService, QuestProgressTracker |
| `CultivationGame.Modules.Save` | Modules/Save/ | SaveService, SaveFileHandler, SaveDataAggregator, SaveModule, SaveModuleServices, SaveLifetimeScope | SaveService, SaveDataAggregator |
| `CultivationGame.Modules.Tile` | Modules/Tile/ | TileMapService, TileGeneratorService, DestructibleService, ResourceService, TileModule, TileModuleServices, TileLifetimeScope | TileMapService, DestructibleService |
| `CultivationGame.Modules.UI` | Modules/UI/ | UIService, HUDPresenter, ToastService, DialoguePresenter, InputLogService, UIModule, UIModuleServices, UILifetimeScope, Inventory/ (TooltipPanel, InventoryScreen, EquipmentSlotUI, InventorySlotUI, BackpackPanel, BodyDollPanel) | UIService, HUDPresenter |
| `CultivationGame.Modules.World` | Modules/World/ | WorldService, LocationService, TimeService, FactionService, EventService, WorldModule, WorldModuleServices, WorldLifetimeScope | WorldService, TimeService |
| `CultivationGame.Modules.Buff` | Modules/Buff/ | BuffService, BuffCalculator, BuffTickProcessor, ActiveBuff, BuffModule, BuffModuleServices, BuffLifetimeScope | BuffService, ActiveBuff |
| `CultivationGame.Entry` | Entry/ | GameEntryPoint, GameLifetimeScope, GameSession, SceneOrchestrator, SceneAssemblyRegistrar, RuntimeSceneBuilder, MessagingRegistrar | GameEntryPoint, GameLifetimeScope |
| `CultivationGame.Entry.Phases` | Entry/Phases/ | CoreValidationPhase, WorldInitPhase, PlayerSpawnPhase, NPCSpawnPhase, ChargerInitPhase, TileMapGenPhase, FormationInitPhase, QuestInitPhase, UIInitPhase, FinalizePhase, AbstractSceneAssemblyPhase | 11 фаз сборки сцены |
| `CultivationGame.Entry.UI` | Entry/UI/ | HUDPanelView, DialoguePanelView, LoadingScreenView, NPCInspectorPanel, InputLogPanel, PausePanelView, GameInputAdapter | UI Views (Entry-слой) |

---

## 2. Module Reference

### 2.1 Combat (CultivationGame.Modules.Combat)

**Ключевые классы:**

| Класс | Реализует | Назначение |
|-------|-----------|------------|
| CombatService | ICombatService, IDisposable | Управление ходом боя: начало, конец, ходы, атаки, защита, каст техник |
| DamageService | IDamageService, IDisposable | Единый пайплайн урона (8 слоёв) |
| DamageCalculator | static | Базовый урон, стихийные множители, части тела, промилле-арифметика |
| DefenseProcessor | static | Броня + материал тела + пробитие (integer math) |
| LevelSuppression | static | Подавление уровнем в промилле |
| TechniqueCapacity | static | Ёмкость техник по типам и подтипам |
| TechniqueChargeService | — | Мощность техник (PotencyPermil — промилле) |
| TechniqueService | — | Хранилище изученных техник |
| WeaponDamageCalculator | static | Integer-формулы урона оружия (STR/AGI scaling) |
| CombatConsequencesService | — | Кровотечение (slashing/piercing vs blunt) |
| ElementalEffectService | — | Стихийные эффекты (Burn, Slow, Stun и др.) |
| CombatLootService | — | Лут с убитых NPC |
| CombatAIService | — | AI-поведение NPC в бою |
| StatProviderAdapter | IStatProvider | Адаптер статов: игрок из IStatService, NPC из INPCService |
| CombatModule | IStartable, ITickable, IDisposable | Инициализация, Tick-обновление, подписки |
| CombatConfig | — | Конфигурация боя (MaxCombatDuration и др.) |

**Интерфейсы, реализуемые модулем:**
- `ICombatService` (CombatService)
- `IDamageService` (DamageService)
- `IStatProvider` (StatProviderAdapter — адаптер внутри модуля)

**MessagePipe-контракты, используемые модулем:**
- Публикует: `CombatStartedEvent`, `CombatEndedEvent`, `TechniqueUsedEvent`, `EnemyKilledEvent`, `QiConsumeRequestEvent`, `QiBufferActivateRequestEvent`, `QiBufferDeactivateRequestEvent`, `DamageAppliedEvent`
- Подписывается: `QiChangedEvent` (кэш Ци/уровня), `QiBufferStateChangedEvent` (кэш буфера), `QiDepletedEvent` (прерывание техник), `DamageAppliedEvent` (прерывание каста)

**Зависимости (DI-инъекции):**
- IDamageService, TechniqueService, TechniqueChargeService, IStatProvider, IEquipmentDataProvider, IQiDataProvider
- IPublisher/ISubscriber для MessagePipe-событий

**Критические правила:**
- CombatService НЕ инжектит IBodyService, IQiService, IQiBufferService — всё через MessagePipe
- Все расчёты в integer math (промилле): PotencyPermil, DamageReductionPermil, suppressionPermil
- Пайплайн урона DamageService: 8 слоёв (базовый → подавление → стихия → баффы → часть тела → защита → Ци-буфер → броня)

---

### 2.2 Qi (CultivationGame.Modules.Qi)

**Ключевые классы:**

| Класс | Реализует | Назначение |
|-------|-----------|------------|
| QiService | IQiService, IDisposable | Накопление, расход, регенерация Ци, уровни культивации, прорывы |
| QiBufferService | IQiBufferService | Защитный Ци-буфер (RawQi/Shield режимы) |
| QiDataProvider | IQiDataProvider | Per-entity провайдер данных Ци для NPC |
| QiBreakthroughCalculator | static | Формулы прорыва (Model B), расчёт ёмкости ядра |
| QiRegenCalculator | static | Регенерация Ци (double-аккумулятор для точности) |

**Интерфейсы, реализуемые модулем:**
- `IQiService` (QiService)
- `IQiBufferService` (QiBufferService)
- `IQiDataProvider` (QiDataProvider)

**MessagePipe-контракты, используемые модулем:**
- Публикует: `QiChangedEvent`, `QiDepletedEvent`, `QiFullEvent`, `CultivationBreakthroughEvent`, `CultivationLevelChangedEvent`, `QiBufferActivatedEvent`, `QiBufferDeactivatedEvent`, `QiBufferStateChangedEvent`
- Подписывается: `QiConsumeRequestEvent` (command — расход Ци), `QiAddRequestEvent` (command — добавление Ци), `QiBufferActivateRequestEvent`, `QiBufferDeactivateRequestEvent`, `BodyPartSeveredEvent` (ампутация сердца → -50% регенерации)

**Критические правила:**
- QiService обрабатывает QiConsumeRequestEvent ТОЛЬКО для своего EntityId (фильтрация P0-X1)
- Все Qi-значения — long (Fix-01: >2.1B на L5+)
- QiBufferResult — int (AbsorbedDamage, PiercingDamage — ЗАПРЕТ 3.9)
- Проводимость: double arithmetic для точности при высоких уровнях

---

### 2.3 Body (CultivationGame.Modules.Body)

**Ключевые классы:**

| Класс | Реализует | Назначение |
|-------|-----------|------------|
| BodyService | IBodyService, IBodyDataProvider, ISaveable, IDisposable | Система тела с двойной HP, ампутацией, per-entity NPC |
| BodyPart | — | Kenshi-style двойная HP (RedHP + BlackHP) |
| BodyFactory | IBodyFactory | Data-driven создание частей тела через BodyTemplateProvider |
| BodyDamageCalculator | static | Split 70/30, проверка IsAlive, материальное снижение |
| BodySlotMapping | static | Body→Equipment маппинг (ампутация → блокировка слотов) |
| SpeciesRegistry | — | Реестр видов существ |
| BodyEnhancementSystem | — | Усиление частей тела |
| SeveredDebuffSystem | — | Дебаффы от ампутации (подписка на BodyPartReattachedEvent) |
| BodyTemplateProvider | — | Провайдер шаблонов тела по морфологии |

**Интерфейсы, реализуемые модулем:**
- `IBodyService` (BodyService)
- `IBodyDataProvider` (BodyService)
- `ISaveable` (BodyService)
- `IBodyFactory` (BodyFactory)

**MessagePipe-контракты, используемые модулем:**
- Публикует: `BodyPartDamagedEvent`, `BodyPartSeveredEvent`, `BodyPartHealedEvent`, `BodyPartReattachedEvent`, `BodyCriticalEvent`
- Подписывается: `DamageAppliedEvent` (автоматическое применение урона), `CultivationLevelChangedEvent` (кэш уровня для регенерации)

**Критические правила:**
- HP — int (ЗАПРЕТ 3.9: float/double/decimal для HP запрещены)
- Сердце (Heart) имеет ТОЛЬКО красную HP (MaxBlackHP = 0)
- Split 70/30 — единственная точка разделения урона (DISC-01)
- Per-entity BodyParts: _parts (игрок) + _entityBodyParts (NPC)

---

### 2.4 NPC (CultivationGame.Modules.NPC)

**Ключевые классы:**

| Класс | Реализует | Назначение |
|-------|-----------|------------|
| NPCService | INPCService | Управление NPC, статы, AI-состояния |
| NPCSpawnerService | INPCSpawnerService | Спавн NPC через полный пайплайн генерации |
| NPCAssemblyService | — | Сборка NPC (тело + Ци + инвентарь + техники) |
| SoulGenerator | — | Генерация души NPC |
| NPCNameGenerator | — | Генерация имён NPC |
| NPCSpeciesSelector | — | Выбор вида NPC по уровню локации |
| NPCAIService | — | AI-поведение NPC (состояния, решения) |
| NPCCombatAdapter | — | Адаптер NPC для боевой системы |
| PerkService | IPerkService | Перк-система NPC (бонусы проводимости) |

**Интерфейсы, реализуемые модулем:**
- `INPCService` (NPCService)
- `INPCSpawnerService` (NPCSpawnerService)
- `IPerkService` (PerkService)

**MessagePipe-контракты, используемые модулем:**
- Публикует: `NPCSpawnedEvent`, `NPCDespawnedEvent`, `AttitudeChangedEvent`, `NPCDeathEvent`, `NPCInteractedEvent`, `NPCAIStateChangedEvent`, `NPCDamagedEvent`
- Подписывается: `DamageAppliedEvent` (NPC получает урон), `CombatEndedEvent` (NPC после боя)

---

### 2.5 Player (CultivationGame.Modules.Player)

**Ключевые классы:**

| Класс | Реализует | Назначение |
|-------|-----------|------------|
| PlayerService | IPlayerService | Тонкий фасад управления состоянием игрока |
| PlayerCombatAdapter | — | Адаптер игрока для боевой системы |
| PlayerInputService | IPlayerInputService | Обработка ввода игрока |
| SleepService | — | Система сна (HP/Qi/статы восстановление) |
| StatService | IStatService | Характеристики игрока (STR, AGI, INT, VIT, Luck и др.) |
| PlayerVisualService | — | Визуальное представление игрока |

**Интерфейсы, реализуемые модулем:**
- `IPlayerService` (PlayerService)
- `IPlayerInputService` (PlayerInputService)
- `IStatService` (StatService)

**MessagePipe-контракты, используемые модулем:**
- Публикует: `PlayerDeathEvent`, `PlayerReviveEvent`, `PlayerSleepEvent`, `PlayerPositionChangedEvent`
- Подписывается: `DamageAppliedEvent`, `QiDepletedEvent`, `BodyPartSeveredEvent`, `CombatStartedEvent`, `CombatEndedEvent`

---

### 2.6 Formation (CultivationGame.Modules.Formation)

**Ключевые классы:**

| Класс | Реализует | Назначение |
|-------|-----------|------------|
| FormationService | IFormationService | Жизненный цикл формаций (прорисовка → наполнение → активация) |
| FormationCalculator | static | Расчёт параметров формации |
| FormationQiPool | — | Пул Ци формации (внесение, утечка, распределение) |
| FormationEffects | — | Эффекты формации (баффы/дебаффы) |
| FormationConfig | — | Конфигурация формации |

**Интерфейсы, реализуемые модулем:**
- `IFormationService` (FormationService)

**MessagePipe-контракты, используемые модулем:**
- Публикует: `FormationActivatedEvent`, `FormationDeactivatedEvent`, `FormationQiPoolChangedEvent`, `FormationStageChangedEvent`, `QiConsumeRequestEvent`
- Подписывается: `QiChangedEvent` (кэш Ци), `CombatEndedEvent` (автодеактивация)

---

### 2.7 Buff (CultivationGame.Modules.Buff)

**Ключевые классы:**

| Класс | Реализует | Назначение |
|-------|-----------|------------|
| BuffService | IBuffService | Управление баффами/дебаффами, расчёт модификаторов |
| BuffCalculator | static | Расчёт модификаторов в промилле (ЗАПРЕТ 3.9) |
| BuffTickProcessor | — | Обработка тикания DoT-баффов |
| ActiveBuff | — | Данные активного баффа |

**Интерфейсы, реализуемые модулем:**
- `IBuffService` (BuffService)

**MessagePipe-контракты, используемые модулем:**
- Публикует: `BuffAppliedEvent`, `BuffExpiredEvent`, `BuffRemovedEvent`
- Подписывается: `CombatEndedEvent` (снятие боевых дебаффов)

---

### 2.8 Inventory (CultivationGame.Modules.Inventory)

**Ключевые классы:**

| Класс | Реализует | Назначение |
|-------|-----------|------------|
| InventoryService | IInventoryService | Управление инвентарём (строчная модель: weight + volume) |
| EquipmentService | IEquipmentService | Экипировка/снятие предметов, Body→Equipment маппинг |
| EquipmentDataProvider | IEquipmentDataProvider | Per-entity провайдер данных экипировки (броня, урон) |
| CraftingService | ICraftingService | Крафт предметов |
| BackpackService | — | Рюкзак (подмножество инвентаря) |
| StorageRingService | IStorageRingService | Кольцо хранения (духовное хранилище) |
| EquipmentValidator | — | Валидация экипировки (требования, заблокированные слоты) |
| EquipmentStatAggregator | — | Агрегация статов от экипировки |
| MaterialService | — | Управление материалами |

**Интерфейсы, реализуемые модулем:**
- `IInventoryService`, `IEquipmentService`, `ICraftingService`, `IStorageRingService`, `IEquipmentDataProvider`, `IStorageService`

---

### 2.9 Charger (CultivationGame.Modules.Charger)

**Ключевые классы:**

| Класс | Реализует | Назначение |
|-------|-----------|------------|
| ChargerService | IChargerService | Управление зарядником Ци |
| ChargerSlot | — | Слот зарядника (камень Ци) |
| ChargerBuffer | — | Буфер зарядника |
| ChargerHeat | — | Система перегрева зарядника |

**Интерфейсы, реализуемые модулем:**
- `IChargerService` (ChargerService)

---

### 2.10 Quest (CultivationGame.Modules.Quest)

**Ключевые классы:**

| Класс | Реализует | Назначение |
|-------|-----------|------------|
| QuestService | IQuestService | Управление квестами |
| QuestProgressTracker | — | Отслеживание прогресса квестов |
| QuestRewardService | IQuestRewardService | Выдача наград за квесты |

**Интерфейсы, реализуемые модулем:**
- `IQuestService`, `IQuestRewardService`

---

### 2.11 Interaction (CultivationGame.Modules.Interaction)

**Ключевые классы:**

| Класс | Реализует | Назначение |
|-------|-----------|------------|
| InteractionService | IInteractionService | Взаимодействие с объектами мира |
| DialogueService | IDialogueService | Диалоговая система |
| DialogueTypewriter | — | Эффект печатной машинки для диалогов |

**Интерфейсы, реализуемые модулем:**
- `IInteractionService`, `IDialogueService`

---

### 2.12 Save (CultivationGame.Modules.Save)

**Ключевые классы:**

| Класс | Реализует | Назначение |
|-------|-----------|------------|
| SaveService | ISaveService | Сохранение/загрузка игры |
| SaveFileHandler | — | Работа с файлами сохранений |
| SaveDataAggregator | — | Агрегация данных от ISaveable-модулей |

**Интерфейсы, реализуемые модулем:**
- `ISaveService` (SaveService)

---

### 2.13 Tile (CultivationGame.Modules.Tile)

**Ключевые классы:**

| Класс | Реализует | Назначение |
|-------|-----------|------------|
| TileMapService | ITileService | Управление тайловой картой |
| TileGeneratorService | — | Генерация тайловых карт |
| DestructibleService | — | Разрушаемые объекты на карте |
| ResourceService | IResourceService | Добыча ресурсов с тайлов |

**Интерфейсы, реализуемые модулем:**
- `ITileService`, `IResourceService`

---

### 2.14 World (CultivationGame.Modules.World)

**Ключевые классы:**

| Класс | Реализует | Назначение |
|-------|-----------|------------|
| WorldService | IWorldService | Управление миром |
| LocationService | — | Локации и переходы |
| TimeService | ITimeService | Игровое время (минуты/часы/сутки) |
| FactionService | — | Фракции и отношения |
| EventService | IEventService | Мировые события |

**Интерфейсы, реализуемые модулем:**
- `IWorldService`, `ITimeService`, `IEventService`

---

### 2.15 UI (CultivationGame.Modules.UI)

**Ключевые классы:**

| Класс | Реализует | Назначение |
|-------|-----------|------------|
| UIService | IUIService | Управление UI-состояниями |
| HUDPresenter | — | Презентер HUD (HP/Qi/время) |
| ToastService | — | Уведомления (toast) |
| DialoguePresenter | — | Презентер диалогов |
| InputLogService | IInputLogService | Лог ввода (отладка) |

**Интерфейсы, реализуемые модулем:**
- `IUIService`, `IInputLogService`

---

### 2.16 Generator (CultivationGame.Modules.Generator)

**Ключевые классы:**

| Класс | Реализует | Назначение |
|-------|-----------|------------|
| TechniqueGeneratorService | ITechniqueGeneratorService | Генерация техник |
| ItemGeneratorService | IItemGeneratorService | Генерация предметов |
| ItemDatabaseService | IItemDatabaseService | База данных предметов |
| TechniqueRegistry | — | Реестр сгенерированных техник |

**Интерфейсы, реализуемые модулем:**
- `ITechniqueGeneratorService`, `IItemGeneratorService`, `IItemDatabaseService`

---

### 2.17 Entry (CultivationGame.Entry)

**Ключевые классы:**

| Класс | Реализует | Назначение |
|-------|-----------|------------|
| GameEntryPoint | IStartable, ITickable, IDisposable | Корневая точка входа, подписка на события сборки |
| GameLifetimeScope | LifetimeScope (VContainer) | Корневой DI-конфигуратор — регистрация MessagePipe + всех модулей |
| GameSession | — | Управление игровой сессией |
| SceneOrchestrator | IStartable | Оркестратор 11-фазной сборки сцены |
| SceneAssemblyRegistrar | — | Регистрация фаз и оркестратора в DI |
| RuntimeSceneBuilder | — | Runtime-построитель сцены |
| MessagingRegistrar | — | Регистрация MessagePipe-брокеров |

**11 фаз сборки сцены:**
1. CoreValidationPhase — валидация DI-конфигурации
2. WorldInitPhase — инициализация мира
3. TileMapGenPhase — генерация тайловой карты
4. PlayerSpawnPhase — спавн игрока
5. NPCSpawnPhase — спавн NPC
6. ChargerInitPhase — инициализация зарядников
7. FormationInitPhase — инициализация формаций
8. QuestInitPhase — инициализация квестов
9. UIInitPhase — инициализация UI
10. FinalizePhase — финализация сборки
11. AbstractSceneAssemblyPhase — базовый класс для всех фаз

---

## 3. Core Interfaces

| Интерфейс | Модуль-реализация | Назначение |
|-----------|-------------------|------------|
| `IBodyService` | Body.BodyService | Части тела, двойная HP, ампутация, регенерация |
| `IBodyDataProvider` | Body.BodyService | Per-entity провайдер данных тела (для NPC) |
| `ICombatService` | Combat.CombatService | Управление ходом боя |
| `IDamageService` | Combat.DamageService | Единый пайплайн урона (8 слоёв) |
| `IQiService` | Qi.QiService | Накопление/расход/регенерация Ци, прорывы |
| `IQiBufferService` | Qi.QiBufferService | Защитный Ци-буфер (RawQi/Shield) |
| `IQiDataProvider` | Qi.QiDataProvider | Per-entity провайдер данных Ци |
| `IBuffService` | Buff.BuffService | Баффы/дебаффы, модификаторы в промилле |
| `IFormationService` | Formation.FormationService | Жизненный цикл формаций |
| `INPCService` | NPC.NPCService | Управление NPC, статы, AI-состояния |
| `INPCSpawnerService` | NPC.NPCSpawnerService | Спавн NPC |
| `IPlayerService` | Player.PlayerService | Тонкий фасад состояния игрока |
| `IPlayerInputService` | Player.PlayerInputService | Обработка ввода игрока |
| `IStatService` | Player.StatService | Характеристики игрока (STR, AGI, INT, VIT) |
| `IStatProvider` | Combat.StatProviderAdapter | Единый доступ к статам (игрок + NPC) |
| `IInventoryService` | Inventory.InventoryService | Управление инвентарём |
| `IEquipmentService` | Inventory.EquipmentService | Экипировка предметов |
| `IEquipmentDataProvider` | Inventory.EquipmentDataProvider | Per-entity данные экипировки (броня, урон) |
| `ICraftingService` | Inventory.CraftingService | Крафт предметов |
| `IStorageService` | Inventory.StorageService | Хранилище предметов |
| `IStorageRingService` | Inventory.StorageRingService | Кольцо хранения |
| `IChargerService` | Charger.ChargerService | Зарядник Ци |
| `IQuestService` | Quest.QuestService | Управление квестами |
| `IQuestRewardService` | Quest.QuestRewardService | Награды за квесты |
| `IInteractionService` | Interaction.InteractionService | Взаимодействие с миром |
| `IDialogueService` | Interaction.DialogueService | Диалоговая система |
| `IUIService` | UI.UIService | Управление UI |
| `IInputLogService` | UI.InputLogService | Лог ввода |
| `ISaveService` | Save.SaveService | Сохранение/загрузка |
| `ITileService` | Tile.TileMapService | Тайловая карта |
| `IResourceService` | Tile.ResourceService | Добыча ресурсов |
| `IWorldService` | World.WorldService | Управление миром |
| `ITimeService` | World.TimeService | Игровое время |
| `IEventService` | World.EventService | Мировые события |
| `ITechniqueGeneratorService` | Generator.TechniqueGeneratorService | Генерация техник |
| `IItemGeneratorService` | Generator.ItemGeneratorService | Генерация предметов |
| `IItemDatabaseService` | Generator.ItemDatabaseService | База данных предметов |
| `IPerkService` | NPC.PerkService | Перк-система NPC |
| `ISaveable` | Body.BodyService, ... | Интерфейс для агрегации данных сохранения |
| `ISceneAssemblyPhase` | Entry.Phases.* | Интерфейс фазы сборки сцены |
| `IBodyFactory` | Body.BodyFactory | Фабрика частей тела |

---

## 4. MessagePipe Contracts

Все контракты расположены в `Core/Messaging/Contracts/`. Каждый файл — один домен.
Все сообщения — `readonly struct` (нулевая GC-аллокация).

| Файл контракта | Ключевые события/запросы | Направление |
|----------------|--------------------------|-------------|
| **CombatContracts.cs** | `CombatStartedEvent`, `CombatEndedEvent`, `DamageAppliedEvent`, `TechniqueUsedEvent`, `EnemyKilledEvent` | Combat → Все (бой начался/закончился, урон применён, враг убит) |
| **QiContracts.cs** | `QiChangedEvent`, `QiDepletedEvent`, `QiFullEvent`, `CultivationBreakthroughEvent`, `CultivationLevelChangedEvent`, `QiConsumeRequestEvent` (cmd), `QiAddRequestEvent` (cmd), `QiBufferActivateRequestEvent` (cmd), `QiBufferDeactivateRequestEvent` (cmd), `QiBufferActivatedEvent`, `QiBufferDeactivatedEvent`, `QiBufferStateChangedEvent` | Qi → Все (изменение Ци, прорыв), Все → Qi (команды расхода/добавления Ци, управление буфером) |
| **BodyContracts.cs** | `BodyPartDamagedEvent`, `BodyPartSeveredEvent`, `BodyPartHealedEvent`, `BodyPartReattachedEvent`, `BodyCriticalEvent` | Body → Все (повреждение, ампутация, исцеление, приживление, критическое состояние) |
| **NPCContracts.cs** | `NPCSpawnedEvent`, `NPCDespawnedEvent`, `AttitudeChangedEvent`, `NPCDeathEvent`, `NPCInteractedEvent`, `NPCAIStateChangedEvent`, `NPCDamagedEvent` | NPC → Все (спавн, деспавн, смерть, отношение, взаимодействие, AI-состояние) |
| **PlayerContracts.cs** | `PlayerDeathEvent`, `PlayerReviveEvent`, `PlayerSleepEvent`, `PlayerPositionChangedEvent` | Player → Все (смерть, воскрешение, сон, перемещение) |
| **FormationContracts.cs** | `FormationActivatedEvent`, `FormationDeactivatedEvent`, `FormationQiPoolChangedEvent`, `FormationStageChangedEvent`, `FormationContributeQiRequestEvent` (cmd) | Formation → Все (активация/деактивация, пул Ци), Все → Formation (внесение Ци) |
| **InventoryContracts.cs** | `ItemAddedEvent`, `ItemRemovedEvent`, `EquipmentChangedEvent`, `EquipmentBlockedEvent`, `ItemAddRequestEvent` (cmd) | Inventory → Все (предметы, экипировка), Все → Inventory (команда добавления предмета) |
| **BuffContracts.cs** | `BuffAppliedEvent`, `BuffExpiredEvent`, `BuffRemovedEvent` | Buff → Все (бафф наложен/истёк/снят) |
| **SaveContracts.cs** | `SaveCompletedEvent`, `LoadCompletedEvent` | Save → Все (сохранение/загрузка завершена) |
| **WorldContracts.cs** | `DayChangedEvent`, `MonthChangedEvent`, `YearChangedEvent`, `TimeSpeedChangedEvent` | World → Все (изменение времени) |
| **QuestContracts.cs** | `QuestStartedEvent`, `QuestObjectiveUpdatedEvent`, `QuestCompletedEvent`, `QuestFailedEvent` | Quest → Все (квест начат/цель обновлена/завершён/провален) |
| **DialogueContracts.cs** | `DialogueStartedEvent`, `DialogueEndedEvent`, `DialogueChoiceSelectedEvent` | Interaction → Все (диалог начат/закончен/выбран вариант) |
| **ChargerContracts.cs** | `ChargerStateChangedEvent`, `ChargerHeatChangedEvent` | Charger → Все (состояние зарядника, перегрев) |
| **StatContracts.cs** | `StatChangedEvent` | Player → Все (изменение характеристики) |
| **SceneContracts.cs** | `SceneReadyEvent`, `SceneAssemblyFailedEvent`, `SceneAssemblyCompletedWithErrorsEvent` | Entry → Все (сцена готова/ошибка) |
| **TileContracts.cs** | `TileChangedEvent`, `ResourceHarvestedEvent` | Tile → Все (изменение тайла, добыча ресурса) |
| **GameContracts.cs** | `GamePausedEvent`, `GameResumedEvent` | Core → Все (пауза/продолжение) |
| **UIContracts.cs** | `UIStateChangedEvent` | UI → Все (изменение UI-состояния) |
| **InputLogContracts.cs** | `InputLogEntryEvent` | UI → Все (запись лога ввода) |
| **CraftingContracts.cs** | `CraftCompletedEvent` | Inventory → Все (крафт завершён) |

### Архитектурные паттерны MessagePipe

**Command-события** (модуль-потребитель → модуль-владелец):
- `QiConsumeRequestEvent` — запрос расхода Ци (Combat/Charger → Qi)
- `QiAddRequestEvent` — запрос добавления Ци (Charger → Qi)
- `QiBufferActivateRequestEvent` — запрос активации буфера (Combat → Qi)
- `QiBufferDeactivateRequestEvent` — запрос деактивации буфера (Combat → Qi)
- `ItemAddRequestEvent` — запрос добавления предмета (Tile/Combat → Inventory)
- `FormationContributeQiRequestEvent` — запрос внесения Ци в формацию (UI/AI → Formation)

**State-события** (кэш для потребителей, без прямых вызовов):
- `QiChangedEvent` — кэш: CurrentQi, MaxQi, CultivationLevel, Conductivity
- `QiBufferStateChangedEvent` — кэш: IsActive, Mode, QiInvested
- `EquipmentChangedEvent` — кэш: TotalArmor
- `CultivationLevelChangedEvent` — кэш: OldLevel, NewLevel (только при изменении уровня)

**Per-entity фильтрация** (P0-X1 FIX):
- `QiConsumeRequestEvent.EntityId` — QiService обрабатывает только свои события
- `QiAddRequestEvent.EntityId` — аналогично

---

## 5. Assembly Definition

**Файл:** `CultivationGame.New.asmdef`

```json
{
    "name": "CultivationGame.New",
    "rootNamespace": "CultivationGame",
    "references": [
        "VContainer",
        "MessagePipe",
        "MessagePipe.VContainer",
        "UniTask",
        "Unity.InputSystem",
        "Unity.TextMeshPro",
        "Unity.RenderPipelines.Universal.Runtime",
        "Unity.RenderPipelines.Universal.2D.Runtime",
        "Unity.2D.Sprite",
        "Unity.2D.Tilemap",
        "UnityEngine.TilemapModule"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "autoReferenced": true
}
```

**Ключевые зависимости:**
- **VContainer** — DI-контейнер (замена Singleton + ServiceLocator)
- **MessagePipe** — шина сообщений (замена GameEvents + CombatEvents)
- **MessagePipe.VContainer** — интеграция MessagePipe с VContainer
- **UniTask** — zero-alloc async/await (замена корутин)

---

## 6. Ключевые архитектурные правила

### 6.1 ЗАПРЕТ 3.9 — Integer Math
Все игровые расчёты выполняются в integer math (промилле арифметика):
- `PotencyPermil` — мощность техники (1000 = ×1.0)
- `DamageReductionPermil` — снижение урона (200 = 20%)
- `GetStatModifierPermil()` — модификаторы баффов (1200 = +20%)
- `GetFormationBonusPermil()` — бонусы формаций (200 = +20%)
- `MorphologyHitTables` — шансы попадания по частям тела (промилле, сумма = 1000)
- QiBuffer — AbsorbedDamage/PiercingDamage = int

**Исключения** (float допустим):
- QiRatio, GetPartHealthRatio — отношения для UI
- Время (deltaTime, castTime, cooldown)
- Проводимость (double для точности при высоких уровнях)
- BaseHitChance — шанс попадания для Combat (legacy)

### 6.2 Модульная структура
Каждый модуль содержит три обязательных файла:
1. `*Module.cs` — реализация IStartable/ITickable/IDisposable, подписки, инициализация
2. `*ModuleServices.cs` — статический класс с `Register(IContainerBuilder, MessagePipeOptions)` — DI-регистрация
3. `*LifetimeScope.cs` — наследник ModuleLifetimeScope, VContainer scope

### 6.3 Hub-and-Spoke (EVT-01)
Модули НЕ инжектят интерфейсы друг друга напрямую.
Все кросс-модульные взаимодействия — через MessagePipe:
- CombatService НЕ инжектит IQiService, IQiBufferService, IBodyService
- DamageService НЕ инжектит IQiBufferService, IEquipmentService
- FormationService НЕ инжектит IQiService

**Исключение:** IStatProvider — адаптер ВНУТРИ Combat-модуля (делегирует в IStatService/NPCService).

### 6.4 Per-Entity Data Providers
Для поддержки NPC (множественные сущности) используются провайдеры:
- `IBodyDataProvider` — BodyParts по entityId
- `IQiDataProvider` — Qi-состояние по entityId (включая QiBuffer)
- `IEquipmentDataProvider` — броня/урон по entityId

Игрок использует кэшированные данные из MessagePipe-событий.
NPC используют данные из провайдеров напрямую.

### 6.5 VContainer DI-иерархия
```
GameLifetimeScope (корневой)
  ├── MessagingRegistrar (MessagePipe-брокеры)
  ├── GeneratorModuleServices
  ├── WorldModuleServices
  ├── TileModuleServices
  ├── BodyModuleServices
  ├── QiModuleServices
  ├── BuffModuleServices
  ├── InventoryModuleServices
  ├── CombatModuleServices
  ├── FormationModuleServices
  ├── NPCModuleServices
  ├── PlayerModuleServices
  ├── QuestModuleServices
  ├── InteractionModuleServices
  ├── UIModuleServices
  ├── ChargerModuleServices
  ├── SaveModuleServices
  └── SceneAssemblyRegistrar (11 фаз + оркестратор)
```

Каждый XxxModuleServices.Register() вызывается из GameLifetimeScope.Configure().
Модульные LifetimeScope (BodyLifetimeScope и др.) наследуют ModuleLifetimeScope
и обеспечивают изолированную регистрацию сервисов модуля.

### 6.6 Система тестирования
```
Tests/
├── Core/ (GameConstantsTests, ObjectDefaultsTests, ValidationTests)
├── Modules/ (Body, Combat, Qi, NPC, Formation, Inventory, Buff, Player, Quest, Save, Tile, World, Charger, Generator)
├── Integration/ (CombatIntegration, InventoryEquipment, ChargerFormation, QuestProgress, SaveLoad)
├── Balance/ (BalanceVerificationTests)
└── TestUtilities/ (MockNPCService, MockBodyService, MockQiService, MockBuffService, MockDataProviders, MockInventoryService, MockTimeService, TestContainerBuilder, TestMessageBus, MessagePipeTestHelper, ActionMessageHandler)
```

Моки реализуют интерфейсы из Core/Interfaces/ для изолированного тестирования модулей.
TestContainerBuilder создаёт VContainer-контейнер с MessagePipe для интеграционных тестов.

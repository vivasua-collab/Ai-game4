# Структура модулей — 16 модулей Hub-and-Spoke

> **Раздел:** 01_architecture
> **Статус:** Детальная спецификация модулей.
> **Связанные документы:** `ARCHITECTURE.md`, `DI_AND_EVENTBUS.md`, `09_workflow/ALGORITHMS.md`.

---

## 0. Принципы

Каждый модуль следует единому шаблону:

```
Modules/Xxx/
├── XxxModule.cs           # Точка входа (IStartable, ITickable)
├── XxxModuleServices.cs   # Статический класс: Register(builder) — регистрация
├── XxxService.cs          # Реализация IXxxService
├── XxxConfig.cs           # Конфигурация (class, не struct — mutable struct risk)
├── XxxCalculator.cs       # Чистые формулы (где применимо)
├── XxxHelper.cs           # Вспомогательные классы (где применимо)
└── Data/                  # Структуры данных модуля (где применимо)
```

**Ключевые правила:**
- Tick() через интерфейс — без каста.
- Configure() НЕ в интерфейсе — чтобы не создавать Core→Modules зависимость.
- DI-каст (`_service is XxxService`) допустим только ВНУТРИ модуля.
- Config — `class`, не `struct` (избежание mutable struct risk).
- Контракты — только `readonly struct`.
- Межмодульное взаимодействие — только через Core-интерфейсы или шину.

---

## 1. Сводная таблица модулей

| # | Модуль | Главный интерфейс | Сервисов | Контрактов | Tick | Описание |
|---|--------|-------------------|----------|------------|------|----------|
| 1 | Body | IBodyService | 4+ | 5 | ✓ | Части тела, двойная HP, материалы, ампутации |
| 2 | Buff | IBuffService | 3+ | 5 | ✓ | 28 типов баффов, мягкие капы, иммунитеты |
| 3 | Charger | IChargerService | 5+ | 5 | ✓ | Зарядники Ци, слоты, буфер, тепло |
| 4 | Combat | ICombatService, IDamageService | 10+ | 5 | ✓ | 11-слойный пайплайн урона |
| 5 | Formation | IFormationService | 5+ | 5 | ✓ | Магические массивы, контур, пул |
| 6 | Inventory | IInventoryService, IStorageService, ICraftingService, IEquipmentService | 7+ | 7 | — | Инвентарь, экипировка, крафт |
| 7 | NPC | INPCService, INPCSpawnerService | 6+ | 7 | ✓ | Спавн, AI, отношения, движение |
| 8 | Player | IPlayerService, IPlayerInputService | 5+ | 4 | ✓ | Игрок, ввод, сон, стойки |
| 9 | Qi | IQiService, IQiBufferService | 4+ | 11+ | ✓ | Ци, ядро, проводимость, прорывы |
| 10 | Tile | ITileService, IResourceService | 4+ | 6 | — | Тайловая карта, ресурсы |
| 11 | World | IWorldService, IEventService, ITimeService | 5+ | 11 | ✓ | Локации, фракции, время, события |
| 12 | Quest | IQuestService, IQuestRewardService | 3+ | 6 | ✓ | Квесты, прогресс, награды |
| 13 | Interaction | IInteractionService, IDialogueService | 3+ | 4 | ✓ | Взаимодействия, диалоги |
| 14 | UI | IUIService | 4+ | 10 | ✓ | HUD, тосты, презентеры |
| 15 | Save | ISaveService, ISaveable | 3+ | 4 | ✓ | Сохранения, агрегация |
| 16 | Generator | — (утилитарный) | 2+ | — | — | Генерация предметов/техник/NPC |

**Итого:** ~44 интерфейсов ядра, ~130 контрактов сообщений.

---

## 2. Детально по модулям

### 2.1. Body

| Свойство | Значение |
|----------|----------|
| Главный интерфейс | `IBodyService` (14 методов) |
| Контракты | `BodyContracts` — BodyPartDamaged/Severed/Healed/Reattached/Critical Event |
| Tick | Да — `ProcessRegeneration()` |
| Зависимости Core | ITimeService |
| Подписки на события | — |

**Ключевые методы:**
- `GetPartState`, `ApplyDamage`, `HealPart`, `IsSlotBlocked`, `GetAllParts`
- `Initialize`, `ProcessRegeneration`, `RecalculateHPFromVitality`
- `ReattachPart`, `GetMorphology`, `GetSizeClass`

**Ключевые сервисы:**
- BodyService — основной сервис
- BodyFactory — фабрика создания тел (для тестируемости)
- BodyTemplateProvider — провайдер шаблонов (6 морфологий + 4 гибрида)
- SpeciesRegistry — реестр 11 видов
- BodyDamageCalculator — расчёт урона по телу
- SeveredDebuffSystem — дебаффы от ампутации

**Особенности:**
- Система двойной HP (Kenshi-style): красная (функциональная) + чёрная (структурная).
- Распределение урона: 70% в красную, 30% в чёрную.
- 7 морфологий + 4 гибрида (Centaur, Mermaid, Harpy, Lamia).
- 7 классов размера, 6+ материалов тела.
- При потере части — временный дебафф к связанным статам.

---

### 2.2. Buff

| Свойство | Значение |
|----------|----------|
| Главный интерфейс | `IBuffService` (9 методов) |
| Контракты | `BuffContracts` — BuffApplied/Removed/Expired/Ticked, StatModifierChanged |
| Tick | Да — `TickBuffs()` |
| Зависимости Core | IStatService |
| Подписки на события | — |

**Ключевые методы:** `ApplyBuff`, `RemoveBuff`, `RemoveAllBuffs`, `HasBuff`, `GetStatModifier`, `GetElementResistance`, `HasImmunity`, `GetActiveBuffs`, `TickBuffs`.

**Ключевые сервисы:**
- BuffService — основной
- BuffCalculator — расчёт модификаторов + мягкий кап
- BuffTickProcessor — обработка тиков баффов

**Особенности:**
- 28 значений BuffType enum.
- 5 категорий: General, Combat, Cultivation, Elemental, Environment (и др.).
- Иммунитеты: маппинг Effect→Immunity (BF-A03).
- **Баффы НЕ могут модифицировать:** первичные характеристики (STR/AGI/INT/VIT), `coreCapacity`, `qiDensity`, `qiRegen` (базовый).

---

### 2.3. Charger

| Свойство | Значение |
|----------|----------|
| Главный интерфейс | `IChargerService` |
| Контракты | `ChargerContracts` — ChargerStateChanged/Overheated/CooledDown/HeatChanged/BufferChanged |
| Tick | Да — `Tick()` |
| Зависимости Core | ITimeService |
| Подписки на события | — |

**Ключевые методы:** `IsOperational`, `HeatLevel`, `UseQiForTechnique`, `EnterCombat`, `Tick`.

**Ключевые сервисы:**
- ChargerService — основной
- ChargerBuffer — Ци-буфер зарядника (50–2000)
- ChargerHeat — тепловой баланс (5 состояний: Cool→Warm→Hot→Critical→Overheated)
- ChargerSlot — слот зарядника (belt/bracelet/necklace/ring)

**Особенности:**
- Режимы: On/Off (упрощённая модель).
- Перегрев 100% → блок 30 сек.
- Пополнение через проводимость 5–50 ед/сек.

---

### 2.4. Combat

| Свойство | Значение |
|----------|----------|
| Главные интерфейсы | `ICombatService`, `IDamageService` |
| Контракты | `CombatContracts` — CombatStarted/Ended, DamageApplied, TechniqueUsed, EnemyKilled |
| Tick | Да |
| Зависимости Core | IQiService, IQiBufferService, IEquipmentService, IInventoryService |
| Подписки на события | EnemyKilled, CombatEnded, EquipmentChanged, BuffApplied, BuffRemoved |

**Ключевые методы:**
- ICombatService: `IsInCombat`, `CurrentStage`, `CurrentTargetId`, `StartCombat`, `EndCombat`, `ExecuteAttack`, `ExecuteDefense`
- IDamageService: `CalculateDamage`, `ApplyDefense`

**Ключевые сервисы:**
- CombatService — управление боем
- DamageService — пайплайн урона
- DamageCalculator — сквозной расчёт через 11 слоёв
- LevelSuppression — подавление по разнице уровней
- DefenseProcessor — обработка уклонения, парирования, блокирования
- TechniqueCapacity — расчёт ёмкости техник
- CombatAIService — AI противника
- CombatLootService — добыча после боя
- TechniqueChargeService — заряд техник
- TechniqueService — управление техниками

**Особенности:**
- Полная реализация 11-слойного пайплайна урона (см. `09_workflow/ALGORITHMS.md` §5).
- 5 подтипов атак: melee_strike, melee_weapon, ranged_projectile, ranged_beam, ranged_aoe.
- AoE до 300×300 м.
- Урон real-time-with-pause, tick-resolved (НЕ physics-driven).

---

### 2.5. Formation

| Свойство | Значение |
|----------|----------|
| Главный интерфейс | `IFormationService` (14 методов) |
| Контракты | `FormationContracts` — FormationActivated/Deactivated/QiPoolChanged/StageChanged/ContributeQiRequest |
| Tick | Да |
| Зависимости Core | ITimeService |
| Подписки на события | QiChanged, CombatEnded, FormationContributeQiRequest |

**Ключевые методы:** `IsFormationActive`, `ActiveFormationId`, `CurrentStage`, `StartDrawing`, `StartFilling`, `ContributeQi`, `ActivateFormation`, `DeactivateFormation`, `GetFormationBonus`, `QiPoolCurrent`, `QiPoolMax`, `ParticipantCount`, `CasterId`, `GetActiveEffects`.

**Ключевые сервисы:**
- FormationService — основной
- FormationCalculator — формулы contourQi, capacity, drain
- FormationQiPool — пул Ци (БЕЗ дублирования QiBuffer; у формации свой пул)
- FormationEffects — эффекты (БЕЗ статического состояния)

**Особенности:**
- Размеры: Small(3×3м) → Heavy(300×300м, L6+).
- Ёмкость пула Ци до 204.8M.
- До 50 помощников могут вносить Ци.
- Формации НЕ групповое движение — это магические массивы на земле.
- Носитель: Disk (переносной, L1–L6) или Altar (стационарный, L5–L9).

---

### 2.6. Inventory

| Свойство | Значение |
|----------|----------|
| Главные интерфейсы | `IInventoryService`, `IStorageService`, `ICraftingService`, `IEquipmentService` |
| Контракты | `InventoryContracts` (5) + `CraftingContracts` (2) |
| Tick | Нет (event-driven) |
| Зависимости Core | IBodyService (косвенно, через события) |
| Подписки на события | ResourceHarvested, BodyPartSevered |

**Ключевые методы:**
- IInventoryService: `TryAddItem`, `TryRemoveItem`, `GetItemCount`, `GetAllSlots`
- IStorageService: `TryStore`, `TryRetrieve`, `GetStoredItems`
- ICraftingService: `CanCraft`, `TryCraft`
- IEquipmentService: `GetEquipped`, `TryEquip`, `TryUnequip`, `IsSlotBlocked`, `GetTotalArmor`, `GetTotalDamage`

**Ключевые сервисы:**
- InventoryService — инвентарь
- EquipmentService — экипировка
- EquipmentValidator — проверки слотов, требований, совместимости с телом
- EquipmentStatAggregator — подсчёт бонусов
- StorageService — Spirit + Ring хранилища (унифицированы через StorageType)
- CraftingService — крафт
- MaterialService — работа с материалами

**Особенности:**
- 16 слотов экипировки (см. `GLOSSARY.md` §4).
- EquipmentService НЕ ссылается на BodySlotMapping — использует `BodyPartSeveredEvent.BlockedSlots` (Hub-and-Spoke).
- ItemAddRequestEvent — командное событие для добавления предметов из других модулей.

---

### 2.7. NPC

| Свойство | Значение |
|----------|----------|
| Главные интерфейсы | `INPCService`, `INPCSpawnerService` |
| Контракты | `NPCContracts` — NPCSpawned/Despawned/Death/Interacted/AIStateChanged/Damaged, AttitudeChanged |
| Tick | Да |
| Зависимости Core | ITimeService |
| Подписки на события | QiChanged, DamageApplied, BodyPartSevered, PlayerPositionChanged, CombatStarted, CombatEnded, DayChanged |

**Ключевые методы:**
- INPCService: `GetNPC`, `GetNearbyNPCIds`, `GetAttitude`, `ModifyAttitude`, `IsAlive`, `GetAIState`, `GetAllNPCIds`, `SetAIState`, `UpdatePosition`
- INPCSpawnerService: `SpawnNPC`, `DespawnNPC`, `GetSpawnedNPCIds`, `ActiveNPCCount`

**Ключевые сервисы:**
- NPCService — данные NPC
- NPCSpawnerService — спавн/деспавн
- NPCRelationshipService — отношения (Attitude + затухание по `DayChangedEvent`)
- NPCAIService — упрощённый Behaviour Tree
- NPCCombatAdapter — адаптер боя через шину (НЕ прямая ссылка на CombatService)
- NPCMovementService — упрощённая навигация (grid pathfinding, без NavMesh)

**Особенности:**
- Трёхуровневая нервная система: Spinal AI (1–10 мс) / Neural Router (10–50 мс) / Brain Controller (100–500 мс).
- Behavior Tree (Selector → Sequence → Condition → Action).
- 15 AI-состояний.
- 8 ролей NPC: Monster, Guard, Merchant, Cultivator, Passerby, Elder, Disciple, Enemy.
- 8 черт характера `PersonalityTrait` [Flags].
- 3 категории NPC: Temp (только память), Plot (сохранение), Unique (полное + история).
- Memory: ~1.9 KB/NPC + 2–4 KB per-entity провайдеры.

---

### 2.8. Player

| Свойство | Значение |
|----------|----------|
| Главные интерфейсы | `IPlayerService`, `IPlayerInputService` |
| Контракты | `PlayerContracts` — PlayerDeath/Revive/Sleep/PositionChanged |
| Tick | Да |
| Зависимости Core | ITimeService |
| Подписки на события | QiDepleted, CombatStarted, CombatEnded, DamageApplied, TimeChanged, PlayerPositionChanged, PlayerSleepEvent |

**Ключевые методы:**
- IPlayerService: `PlayerId`, `Position`, `IsAlive`, `IsSleeping`, `SleepState`, `Stance`, `StartSleep`, `WakeUp`, `SetPosition`, `GetAssignedTechniques`, `Tick`
- IPlayerInputService: `MoveDirection`, `RunHeld`, `IsAttackPressed`, `IsDefendPressed`, `IsInteractPressed`, `IsInventoryPressed`, `IsMeditatePressed`, `SelectedTechniqueSlot`, `InputDisabled`, `UpdateInputState`, `ResetFrameFlags`

**Ключевые сервисы:**
- PlayerService — тонкий фасад
- PlayerCombatAdapter — адаптер боя через шину
- PlayerInputService — чистый C#, обновляется через ITickable
- SleepService — логика сна (подписка на `TimeChangedEvent`)
- PlayerVisualService — визуал
- StatService — реализация IStatService (реальная, не stub)

**Особенности:**
- PlayerSleepState: Awake, FallingAsleep, Sleeping, WakingUp.
- PlayerStance: Normal, Combat, Meditating, Sleeping.
- `ResetFrameFlags()` вызывается из PlayerModule.Tick() ПОСЛЕ всех потребителей (не внутри PlayerService.Tick).
- Запрещена двойная регенерация HP во сне.

---

### 2.9. Qi

| Свойство | Значение |
|----------|----------|
| Главные интерфейсы | `IQiService` (22 метода), `IQiBufferService` (6 методов) |
| Контракты | `QiContracts` (11+ событий) — QiChanged/Depleted/Full, CultivationBreakthrough, CultivationLevelChanged, QiBuffer Activated/Deactivated/StateChanged, command events |
| Tick | Да (batched: регенерация каждые 10 тиков) |
| Зависимости Core | — |
| Подписки на события | BodyPartSevered (для деактивации буфера) |

**Ключевые методы:**
- IQiService: `EntityId`, `CurrentQi`, `MaxQi`, `QiRatio`, `IsEmpty`, `IsFull`, `TryConsumeQi`, `AddQi`, `Regenerate`, `CultivationLevel`, `SubLevel`, `CoreQuality`, `CoreCapacity`, `QiDensity`, `EffectiveQi`, `Conductivity`, `ConductivityBonus`, `SetConductivityBonus`, `CanBreakthrough`, `CalculateBreakthroughRequirement`, `TryBreakthrough`, `SetCultivationLevel`
- IQiBufferService: `IsActive`, `Mode`, `QiInvested`, `Activate`, `Deactivate`, `AbsorbDamage`

**Ключевые сервисы:**
- QiService — основной
- QiBufferService — буфер Ци
- QiRegenCalculator — расчёт регенерации
- QiBreakthroughCalculator — расчёт прорыва

**Особенности:**
- Тип данных: `long` (не `float`) — на L9 ~524M effectiveQi.
- Формула вместимости: `coreCapacity = 1000 × 1.1^totalSubLevels × qualityMultiplier`.
- Плотность: `qiDensity = 2^(level-1)`.
- Проводимость: `conductivity = coreCapacity / 360`.
- Буфер: сырая Ци (90%/3:1 для техник, 80%/5:1 для физики) и щитовая техника (100%/1:1 и 100%/2:1).
- Command events (QiConsumeRequest, QiAddRequest, QiBufferActivateRequest, QiBufferDeactivateRequest) — для развязки модулей.
- Нельзя перезаписывать `_coreCapacity` после прорыва.
- Track потреблённого Ци в буфере для корректного возврата при Deactivate.

---

### 2.10. Tile

| Свойство | Значение |
|----------|----------|
| Главные интерфейсы | `ITileService`, `IResourceService` |
| Контракты | `TileContracts` — TileChanged, ResourceHarvested/Depleted/Respawned, TileMapGenerated, HarvestResult (struct) |
| Tick | Нет (event-driven) |
| Зависимости Core | IInventoryService (через события) |
| Подписки на события | ResourceRespawned (на собственное событие, для обновления тайла) |

**Ключевые методы:**
- ITileService: `GetTile`, `SetTile`, `TryHarvest`, `IsWalkable`
- IResourceService: `TrySpawnResource`, `TryPickup`, `Harvest`, `RegisterDepletedResource`

**Ключевые сервисы:**
- TileMapService — управление тайловой картой
- ResourceService — ресурсы (опубликовывает `ResourceHarvestedEvent`)
- DestructibleService — разрушаемые объекты
- TileGeneratorService — генерация карты программно

**Особенности:**
- Циркулярная зависимость TileMapService ↔ ResourceService решена через `ResourceRespawnedEvent`.
- Размер тайла: 2×2 м (единый стандарт проекта).
- Memory: мегаполис 775 MB uncompressed → ~150 MB RLE → ~77 MB sparse.
- Чанковая загрузка для локаций >1 км.

---

### 2.11. World

| Свойство | Значение |
|----------|----------|
| Главные интерфейсы | `IWorldService`, `IEventService`, `ITimeService` |
| Контракты | `WorldContracts` (11 событий) — TimeChanged/DayChanged/MonthChanged/YearChanged, TimeSpeedChanged, LocationChanged, TravelStarted, SceneTransitionRequest, SceneLoaded, WorldEventTriggered/Ended |
| Tick | Да (TimeService тикает) |
| Зависимости Core | — |
| Подписки на события | TimeChanged (для триггера периодических мировых событий) |

**Ключевые методы:**
- IWorldService: `CurrentLocationId`, `CurrentSectorId`, `TryTravel`, `GetLocation`, `GetFaction`, `GetFactionRelation`, `GetDiscoveredSectors`, `IsSectorDiscovered`
- IEventService: `TriggerWorldEvent`, `IsEventActive`, `GetActiveEvents`, `EndWorldEvent`
- ITimeService: `DeltaTime`, `TotalTime`, `CurrentDay`/`Hour`/`Month`/`Year`, `TimeOfDay`, `Speed`, `Pause`/`Resume`

**Ключевые сервисы:**
- WorldService — мир
- TimeService — реальная реализация (заменила stub)
- LocationService — управление локациями и секторами
- FactionService — логика фракций и отношений (`FactionRelation` через readonly struct, не enum — расширяемость)
- EventService — мировые события (подписка на `TimeChangedEvent` для триггера)

**Особенности:**
- WorldService НЕ инжектит IPlayerService — использует `LocationChangedEvent` для связи с Player.
- 1 тик = 1 минута игрового времени.
- 4 скорости (Pause/Normal/Fast/VeryFast = 0/1/5/15 тиков/сек).
- Начальный год: 1864 Э.С.М.

---

### 2.12. Quest

| Свойство | Значение |
|----------|----------|
| Главные интерфейсы | `IQuestService` (9 методов), `IQuestRewardService` |
| Контракты | `QuestContracts` — QuestStarted/ObjectiveUpdated/Completed/Failed/Abandoned/RewardGranted |
| Tick | Да |
| Зависимости Core | — |
| Подписки на события | EnemyKilled, ItemAdded, InteractionCompleted, DialogueChoiceSelected |

**Ключевые методы:**
- IQuestService: `StartQuest`, `AbandonQuest`, `CompleteQuest`, `FailQuest`, `GetActiveQuestIds`, `IsQuestComplete`, `GetQuestStatus`, `QuestExists`, `GetQuestType`
- IQuestRewardService: `GrantRewards`, `AreRewardsGranted`

**Ключевые сервисы:**
- QuestService — квесты
- QuestRewardService — награды (выделен из QuestService — SRP)
- QuestProgressTracker — отслеживание прогресса через 6 подписок на события

**Особенности:**
- Квесты привязаны к NPC и локациям.
- QuestProgressTracker отслеживает: убийства, сбор ресурсов, взаимодействие с NPC, посещение локаций, получение предметов, завершение боя.
- `QuestRewardGrantedEvent` публикуется после выдачи наград — UI и SaveModule подписываются.

---

### 2.13. Interaction

| Свойство | Значение |
|----------|----------|
| Главные интерфейсы | `IInteractionService`, `IDialogueService` |
| Контракты | `DialogueContracts` — DialogueStarted/Ended/ChoiceSelected, InteractionCompleted |
| Tick | Да |
| Зависимости Core | IWorldService (через `CurrentLocationId`) |
| Подписки на события | NPCInteracted, PlayerPositionChanged |

**Ключевые методы:**
- IInteractionService: `GetNearestInteractableId`, `TryInteract`
- IDialogueService: `StartDialogue`, `AdvanceDialogue`, `SelectChoice`, `EndDialogue`, `IsInDialogue`, `CurrentDialogueId`

**Ключевые сервисы:**
- InteractionService — взаимодействия
- DialogueService — диалоги (6 методов)
- DialogueTypewriter — эффект печатающегося текста (отдельный класс — SRP)

**Особенности:**
- DialogueTypewriter с корректной отменой через `CancellationToken`.
- Диалоги с ветвлением через `DialogueNode` + `DialogueChoice`.
- `InteractionCompletedEvent` публикуется по завершении — подписчики QuestModule, NPCModule.

---

### 2.14. UI

| Свойство | Значение |
|----------|----------|
| Главный интерфейс | `IUIService` |
| Контракты | `UIContracts` (10 событий) — UIStateChangeRequest, UIInteractRequest, UIAdvanceDialogueRequest, UISelectChoiceRequest, UISaveRequest, UILoadRequest, UIPauseRequest, UIResumeRequest, ToastShown, ModalShown |
| Tick | Да |
| Зависимости Core | — |
| Подписки на события | 30+ событий: CombatStarted/Ended, QiChanged, BodyPartDamaged, BuffApplied/Removed/Expired, ItemAdded/Removed, EquipmentChanged, NPCSpawned/Despawned, PlayerDeath/Sleep, QuestStarted/Completed, DialogueStarted/Ended, SaveCompleted, LocationChanged, TimeChanged, MonthChanged, YearChanged и др. |

**Ключевые методы:** `CurrentUIState`, `SetUIState`, `ShowToast`, `ShowModal`.

**Ключевые сервисы:**
- UIService — управление UIState
- ToastService — уведомления (отдельный от UIService — SRP)
- HUDPresenter — презентер HUD (подписка на QiChanged, BodyPartDamaged и др.)
- DialoguePresenter — презентер диалогов

**Особенности:**
- UIState enum — управление стеком окон.
- Презентеры — чистый C# (через шину, не engine-зависимые).
- 22 UI View (HUDPanelView, HotbarPanelView, BuffBarView, ToastView, MiniMapView, DialoguePanelView, PausePanelView, CombatOverlayView, DeathScreenView, LoadingScreenView, CharacterPanelView, TechniqueChargeView, CombatLogView, TurnOrderView, DamageNumberView, EnemyHealthBarView, ContextMenuUI, DraggableWindow и др.).
- Тема «Древний Пергамент» на Unicode-глифах (◆ ○ ★ ✓ ◇ ▰).
- 3 sub-слоя: Hud / Window / Floating.

---

### 2.15. Save

| Свойство | Значение |
|----------|----------|
| Главные интерфейсы | `ISaveService`, `ISaveable` |
| Контракты | `SaveContracts` — SaveRequested/LoadRequested/SaveCompleted/LoadCompleted |
| Tick | Да (автосохранение) |
| Зависимости Core | — |
| Подписки на события | SaveRequestedEvent, LoadRequestedEvent, CultivationBreakthrough, PlayerDeath, NPCDeath, QuestRewardGranted |

**Ключевые методы:**
- ISaveService: `Save`, `Load`, `HasSave`, `DeleteSave`, `GetAllSaves`
- ISaveable: `SaveKey`, `CaptureState`, `RestoreState`

**Ключевые сервисы:**
- SaveService — основной
- SaveFileHandler — чтение/запись файлов (JSON)
- SaveDataAggregator — агрегация данных от всех `ISaveable`

**Особенности:**
- **ModuleServices pattern:** у Save НЕТ отдельного LifetimeScope — использует `SaveModuleServices.Register(builder)`.
- JSON (human-readable, debuggable, portable). Опционально binary + GZIP.
- Тайловые данные регенерируются из seed + delta.
- `long` для всех Qi-значений.
- Автосохранение: триггеры — смена локации, получение техники, прорыв, завершение боя.

---

### 2.16. Generator

| Свойство | Значение |
|----------|----------|
| Главные интерфейсы | — (утилитарный модуль) |
| Контракты | — (вызывается при генерации) |
| Tick | Нет |
| Зависимости Core | — |
| Подписки на события | — |

**Ключевые сервисы:**
- TechniqueGeneratorService — генерация техник
- ItemGeneratorService — генерация предметов
- (NPCGenerator — часть NPCModule, но использует Generator паттерн)

**Особенности:**
- Принцип «Матрёшка»: `Результат = База × Грейд × Специализация`.
- seededRandom для детерминированности (одинаковый seed → одинаковый результат).
- Унификация Grade: НЕ зависит от уровня. Даже на L1 есть шанс (2%) получить transcendent.
- Распределение Grade: common 60%, refined 28%, perfect 10%, transcendent 2%.
- Применяется к: экипировке, техникам, расходникам, формациям, камням Ци.

---

## 3. Над-модульные сущности

### 3.1. SceneOrchestrator

Не модуль, а оркестратор сборки сцены. 10 фаз последовательно через `ExecuteAsync`. Фазы регистрируются через `SceneAssemblyRegistrar` (открытый список). Контракты — `SceneContracts`.

Подробно — в `ARCHITECTURE.md` §6.

### 3.2. GameSession

Не модуль, а управление жизненным циклом сессии. NewGame/LoadGame/Pause/Resume/SaveAndQuit/QuitWithoutSaving. Подписывается на `GamePausedEvent`/`GameResumedEvent`/`SaveCompletedEvent`/`LoadCompletedEvent`.

---

## 4. Карта межмодульных зависимостей (через шину)

| Событие | Издатель | Подписчик |
|---------|----------|-----------|
| `ChargerStateChangedEvent` | Charger | UI |
| `ChargerOverheatedEvent` | Charger | Qi |
| `ResourceHarvestedEvent` | Tile | Inventory |
| `ResourceDepletedEvent` | Tile | UI |
| `ResourceRespawnedEvent` | ResourceService | TileMapService (внутримодульно) |
| `BodyPartDamagedEvent` | Body | Combat |
| `BodyPartSeveredEvent` | Body | Equipment (автоснятие), Qi (деактивация буфера) |
| `DayChangedEvent` | Time (World) | ResourceService (респаун), NPCRelationshipService (затухание), UI, Quest |
| `MonthChangedEvent`/`YearChangedEvent` | Time (World) | UI, Quest |
| `LocationChangedEvent` | WorldService | Player, NPC, UI |
| `QiChangedEvent` | Qi | UI, NPC (кэш для AI), Formation |
| `QiDepletedEvent` | Qi | Combat |
| `CultivationBreakthroughEvent` | Qi | UI, Save |
| `BuffApplied/Removed/Expired/Ticked` | Buff | UI |
| `ItemAdded/Removed` | Inventory/Storage | UI |
| `EquipmentChanged/Blocked` | Equipment | UI |
| `CraftCompleted/Failed` | Crafting | UI |
| `NPCSpawned/Despawned` | NPCSpawner | UI |
| `NPCDeath` | NPCCombatAdapter | UI, Save |
| `NPCAIStateChanged` | NPC | UI |
| `PlayerDeath/Revive` | Player | NPC, UI, Save |
| `PlayerSleepEvent` | Sleep/Player | Qi, Body, UI |
| `PlayerPositionChanged` | Player | NPC AI, NPC Movement, Player Visual |
| `QuestStarted/Completed/Failed` | Quest | UI |
| `QuestRewardGranted` | QuestRewardService | UI, Save |
| `DialogueStarted/Ended` | Dialogue | UI (DialoguePresenter) |
| `DialogueChoiceSelected` | Dialogue | Quest (цели квестов) |
| `InteractionCompleted` | Interaction | Quest, NPC, Save |
| `SaveRequested/LoadRequested` | GameSession/UIService | Save |
| `SaveCompleted/LoadCompleted` | Save | GameSession, UI |
| `SceneAssembly*Event` | SceneOrchestrator | UI (LoadingScreen), GameSession |

> Подробная карта зависимостей (внутримодульные связи) — в `DI_AND_EVENTBUS.md`.

---

## 5. Tick-участие модулей

| Модуль | Tick | Периодичность | Операция |
|--------|------|---------------|----------|
| Body | ✓ | Каждый тик | ProcessRegeneration |
| Buff | ✓ | Каждый тик | TickBuffs |
| Charger | ✓ | Каждый тик | Тепловой баланс, охлаждение |
| Combat | ✓ | Каждый тик (в бою) | AI, заряд техник |
| Formation | ✓ | Каждый тик | Drain Qi, effects |
| NPC | ✓ | Spinal: каждый тик / Neural: ~3 / Brain: ~10 | AI-решения |
| Player | ✓ | Каждый тик | Input, ResetFrameFlags |
| Qi | ✓ | Каждые 10 тиков | Регенерация (batch) |
| World | ✓ | Каждый тик | TimeService |
| Quest | ✓ | Каждый тик | Проверка прогресса |
| Interaction | ✓ | Каждый тик | Обновление состояния диалога |
| UI | ✓ | Каждый тик | Обновление HUD |
| Save | ✓ | Каждые 60 тиков | Автосохранение (если триггер) |
| Tile | — | Event-driven | — |
| Inventory | — | Event-driven | — |
| Generator | — | Event-driven | Вызывается при генерации |

---

## 6. История фаз реализации (справочно)

| Фаза | Модуль | Что сделано |
|------|--------|-------------|
| 0 | Core (Generator) | Интерфейсы, данные, messaging, DI, генераторы |
| 1 | Charger | Зарядник Ци |
| 2 | Tile | Тайловая карта + ресурсы |
| 3 | Body | Система тела (доработка: BodyFactory, SpeciesRegistry) |
| 4 | Qi | Система Ци (long arithmetic, batch) |
| 5 | Buff | Баффы/дебаффы (28 типов) |
| 6 | Inventory | Инвентарь/экипировка/крафт |
| 7 | Combat | Боевая система (11-слойный пайплайн) |
| 8 | Formation | Система формаций |
| 9 | NPC | Система NPC (AI 3-tier, отношения) |
| 10 | Player | Система игрока (сон, стойки, стат) |
| 11 | World | Мир, время, локации, фракции |
| 12 | Quest | Квесты, награды, прогресс |
| 13 | Interaction | Взаимодействия, диалоги |
| 14 | UI | UI, уведомления, презентеры |
| 15 | Save | Сохранения, автосохранение |
| 16-17 | SceneOrchestrator + ModuleServices | Сборка сцены + единообразная регистрация |
| 18-19 | GameSession + Entry/UI | Жизненный цикл + UI views + cleanup |

---

## 7. Связанные документы

| Документ | Описание |
|----------|----------|
| `ARCHITECTURE.md` | Высокоуровневая архитектура |
| `DI_AND_EVENTBUS.md` | Паттерны DI + шина событий |
| `PERFORMANCE_STRATEGY.md` | Performance budgets |
| `FILE_TREE.md` | Структура файлов |
| `09_workflow/ALGORITHMS.md` | Все формулы |

---

*Документ создан в рамках Task 2-a: engine-agnostic rewrite. Источники: `docs/ARCHITECTURE.md`, `docs/ARCHITECTURE_CODE.md`, `docs/ARCHITECTURE_IMPL.md`, `docs/ARCHITECTURE_FILE_TREE.md`.*

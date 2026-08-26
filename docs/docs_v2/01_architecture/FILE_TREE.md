# Структура файлов проекта

> **Раздел:** 01_architecture
> **Статус:** Предлагаемая структура (engine-agnostic, на чистом C# с текстовыми сценами).
> **Связанные документы:** `ARCHITECTURE.md`, `MODULE_STRUCTURE.md`.

---

## 0. Принципы

1. **Текстовые файлы везде.** Сцены, ресурсы, конфиги — все текстовые. AI-агенты могут авторить напрямую, без редактора.
2. **Чистый C# для всей логики.** 16 модулей не зависят от движка. Движок используется только в adapter-слое.
3. **Разделение core / adapter.** Логика отделена от рендеринга/ввода/UI/аудио.
4. **JSON для данных.** Конфиги, определения техник, предметов, NPC — в JSON (debuggable, portable).
5. **Тесты рядом с кодом.** `dotnet test` запускается headless.

---

## 1. Корневая структура

```
project-root/
├── src/                              # Исходный код
│   ├── CultivationGame.Core/         # ЯДРО (engine-agnostic, чистый C#)
│   ├── CultivationGame.Modules/      # 16 модулей (engine-agnostic)
│   ├── CultivationGame.Entry/        # Точка входа + Scene Assembly
│   └── CultivationGame.Adapter/      # Adapter-слой (engine-specific)
│
├── tests/                            # Тесты (xUnit/NUnit)
│   ├── CultivationGame.Core.Tests/
│   ├── CultivationGame.Modules.Tests/
│   └── CultivationGame.Integration.Tests/
│
├── data/                             # JSON-конфиги
│   ├── techniques/                   # Определения 34+ техник
│   ├── items/                        # Определения предметов
│   ├── equipment/                    # Экипировка
│   ├── npc_presets/                  # Пресеты NPC
│   ├── quests/                       # Квесты
│   ├── elements/                     # Стихии
│   ├── cultivation_levels/           # 9 уровней культивации
│   ├── mortal_stages/                # 6 этапов смертных
│   ├── species/                      # 11 видов
│   ├── materials/                    # Материалы
│   └── grades/                       # Грейды
│
├── scenes/                           # Текстовые сцены
│   ├── MainMenu.scene                # Главная меню
│   ├── GameWorld.scene               # Основная игровая сцена
│   └── Combat.scene                  # Боевая сцена (если выделяется)
│
├── assets/                           # Ассеты (спрайты, аудио)
│   ├── sprites/
│   │   ├── tiles/                    # Тайлы (64×64 @ PPU=32)
│   │   ├── characters/               # Персонажи (128×128 @ PPU=64)
│   │   ├── objects/                  # Объекты окружения
│   │   ├── effects/                  # Эффекты (12 effect sprites + 8 orbital-weapon)
│   │   ├── ui/                       # UI элементы (бордюры, орнаменты, слоты)
│   │   └── icons/                    # Иконки предметов
│   ├── audio/
│   │   ├── sfx/
│   │   └── music/
│   └── themes/
│       └── parchment.theme           # Тема «Древний Пергамент» (текстовый файл)
│
├── saves/                            # Локальные сохранения (runtime)
│   ├── slot1/
│   │   ├── main.sav                  # JSON
│   │   ├── chunks/
│   │   ├── locations/
│   │   └── metadata.sav
│   ├── slot2/
│   ├── slot3/
│   ├── autosave/
│   └── quicksave/
│
├── docs/                             # Документация (docs_v2)
│   └── ... (см. README.md)
│
├── .editorconfig
├── .gitignore
├── project.csproj                    # Главный .csproj
├── CultivationGame.sln               # Solution
└── README.md
```

---

## 2. Структура `src/CultivationGame.Core/` (ЯДРО)

```
CultivationGame.Core/
├── CultivationGame.Core.csproj
│
├── Data/
│   ├── Constants.cs                  # GameConstants: MAX_STAT_VALUE=1000, MAX_CULTIVATION_LEVEL=9, MAX_EQUIPMENT_GRADE=5
│   ├── Enums.cs                      # Все enums (StatType, BodyPartType, Element, EquipmentSlot, ...)
│   ├── BodyPartTemplate.cs           # Шаблон части тела
│   ├── BodyTemplate.cs               # Шаблон тела (композиция)
│   ├── SpeciesData.cs                # Данные вида (11 видов)
│   ├── StatType.cs                   # Типы характеристик
│   └── GameTile.cs                   # Структура тайла
│
├── Interfaces/                       # 30+ интерфейсов сервисов
│   ├── IChargerService.cs
│   ├── ITileService.cs
│   ├── IResourceService.cs
│   ├── IBodyService.cs
│   ├── ITimeService.cs
│   ├── IQiService.cs
│   ├── IQiBufferService.cs
│   ├── IBuffService.cs
│   ├── IStatService.cs
│   ├── IInventoryService.cs
│   ├── IStorageService.cs
│   ├── ICraftingService.cs
│   ├── IEquipmentService.cs
│   ├── ICombatService.cs
│   ├── IDamageService.cs
│   ├── IFormationService.cs
│   ├── INPCService.cs
│   ├── INPCSpawnerService.cs
│   ├── IPlayerService.cs
│   ├── IPlayerInputService.cs
│   ├── IWorldService.cs
│   ├── IEventService.cs
│   ├── IQuestService.cs
│   ├── IQuestRewardService.cs
│   ├── IInteractionService.cs
│   ├── IDialogueService.cs
│   ├── IUIService.cs
│   ├── ISaveService.cs
│   ├── ISaveable.cs
│   └── ISceneAssemblyPhase.cs
│
├── Messaging/
│   └── Contracts/                    # ~130 readonly struct контрактов в 20 файлах
│       ├── GameContracts.cs
│       ├── CombatContracts.cs
│       ├── BodyContracts.cs
│       ├── QiContracts.cs
│       ├── BuffContracts.cs
│       ├── ChargerContracts.cs
│       ├── TileContracts.cs
│       ├── InventoryContracts.cs
│       ├── PlayerContracts.cs
│       ├── WorldContracts.cs
│       ├── NPCContracts.cs
│       ├── FormationContracts.cs
│       ├── QuestContracts.cs
│       ├── SaveContracts.cs
│       ├── DialogueContracts.cs
│       ├── StatContracts.cs
│       ├── CraftingContracts.cs
│       ├── UIContracts.cs
│       ├── SceneContracts.cs
│       └── InputLogContracts.cs
│
├── DI/
│   ├── IContainerBuilder.cs          # Абстракция DI (если своя)
│   ├── Lifetime.cs                   # Enum: Singleton, Transient, Scoped
│   ├── InjectAttribute.cs            # [Inject]
│   └── ModuleLifetimeScope.cs        # Базовый класс для модульных scope-ов
│
└── Events/
    ├── IPublisher.cs                 # IPublisher<T>
    ├── ISubscriber.cs                # ISubscriber<T>
    └── EventBus.cs                   # Реализация шины (если своя)
```

> **Принцип:** Core НЕ зависит от движка. Все типы — `readonly struct`, интерфейсы — без engine-зависимостей.

---

## 3. Структура `src/CultivationGame.Modules/` (16 модулей)

```
CultivationGame.Modules/
├── CultivationGame.Modules.csproj
│
├── Body/                             # ✅ Модуль тела
│   ├── BodyModule.cs                 # Точка входа (IStartable, ITickable)
│   ├── BodyModuleServices.cs         # ModuleServices — Register(builder)
│   ├── BodyService.cs                # Реализация IBodyService
│   ├── BodyPart.cs                   # Данные части тела
│   ├── IBodyFactory.cs               # Интерфейс фабрики (для тестируемости)
│   ├── BodyFactory.cs                # Фабрика создания тел
│   ├── BodyTemplateProvider.cs       # Провайдер шаблонов (6 морфологий + 4 гибрида)
│   ├── SpeciesRegistry.cs            # Реестр 11 видов
│   ├── SeveredDebuffSystem.cs        # Дебаффы от ампутации
│   ├── BodySlotMapping.cs            # Маппинг BodyPart → EquipmentSlot
│   ├── BodyDamageCalculator.cs       # Расчёт урона по телу
│   └── Data/
│       └── BodyPartData.cs
│
├── Buff/                             # ✅ Модуль баффов
│   ├── BuffModule.cs
│   ├── BuffModuleServices.cs
│   ├── BuffService.cs
│   ├── BuffCalculator.cs             # Расчёт модификаторов + мягкий кап
│   ├── BuffTickProcessor.cs          # Обработка тиков баффов
│   ├── BuffConfig.cs
│   └── ActiveBuff.cs
│
├── Charger/                          # ✅ Модуль зарядников
│   ├── ChargerModule.cs
│   ├── ChargerModuleServices.cs
│   ├── ChargerService.cs
│   ├── ChargerBuffer.cs              # Ци-буфер зарядника
│   ├── ChargerData.cs                # Данные камня Ци
│   ├── ChargerHeat.cs                # Тепловой баланс
│   └── ChargerSlot.cs                # Слот зарядника
│
├── Inventory/                        # ✅ Модуль инвентаря
│   ├── InventoryModule.cs
│   ├── InventoryModuleServices.cs
│   ├── InventoryService.cs
│   ├── InventoryConfig.cs
│   ├── EquipmentService.cs
│   ├── EquipmentValidator.cs
│   ├── EquipmentStatAggregator.cs
│   ├── StorageService.cs             # Spirit + Ring (через StorageType)
│   ├── CraftingService.cs
│   ├── MaterialService.cs
│   └── Data/
│       └── CraftingRecipe.cs
│
├── Qi/                               # ✅ Модуль Ци
│   ├── QiModule.cs                   # + QiModuleConfig
│   ├── QiModuleServices.cs
│   ├── QiService.cs                  # Реализация IQiService (long arithmetic)
│   ├── QiBufferService.cs            # Реализация IQiBufferService
│   ├── QiConfig.cs
│   ├── QiRegenCalculator.cs          # Расчёт регенерации
│   └── QiBreakthroughCalculator.cs   # Расчёт прорыва
│
├── Tile/                             # ✅ Модуль тайлов
│   ├── TileModule.cs
│   ├── TileModuleServices.cs
│   ├── TileMapService.cs             # Реализация ITileService
│   ├── ResourceService.cs            # Реализация IResourceService
│   ├── DestructibleService.cs        # Разрушаемые объекты
│   └── TileGeneratorService.cs       # Генерация карты
│
├── Combat/                           # ✅ Модуль боя
│   ├── CombatModule.cs
│   ├── CombatModuleServices.cs
│   ├── CombatService.cs              # Реализация ICombatService
│   ├── DamageService.cs              # Реализация IDamageService
│   ├── DamageCalculator.cs           # Сквозной расчёт через 11 слоёв
│   ├── LevelSuppression.cs           # Подавление по разнице уровней
│   ├── DefenseProcessor.cs           # Уклонение, парирование, блок
│   ├── TechniqueCapacity.cs          # Ёмкость техник
│   ├── CombatAIService.cs            # AI противника
│   ├── CombatLootService.cs          # Добыча после боя
│   ├── TechniqueChargeService.cs     # Заряд техник
│   └── TechniqueService.cs           # Управление техниками
│
├── Formation/                        # ✅ Модуль формаций
│   ├── FormationModule.cs            # IStartable, ITickable, IDisposable
│   ├── FormationModuleServices.cs
│   ├── FormationService.cs           # Реализация IFormationService
│   ├── FormationCalculator.cs        # contourQi, capacity, drain
│   ├── FormationQiPool.cs            # Пул Ци (БЕЗ дублирования QiBuffer)
│   ├── FormationEffects.cs           # Эффекты (БЕЗ статического состояния)
│   ├── FormationConfig.cs
│   └── Data/
│       └── FormationData.cs
│
├── NPC/                              # ✅ Модуль NPC
│   ├── NPCModule.cs                  # IStartable, ITickable, IDisposable
│   ├── NPCModuleServices.cs
│   ├── NPCService.cs                 # Реализация INPCService
│   ├── NPCSpawnerService.cs          # Реализация INPCSpawnerService
│   ├── NPCRelationshipService.cs     # Attitude + затухание
│   ├── NPCAIService.cs               # Упрощённый Behaviour Tree
│   ├── NPCCombatAdapter.cs           # Адаптер боя через шину
│   ├── NPCMovementService.cs         # Движение и навигация
│   ├── NPCConfig.cs                  # class (BD-48)
│   └── Data/
│       └── NPCState.cs               # Runtime-состояние NPC
│
├── Player/                           # ✅ Модуль игрока
│   ├── PlayerModule.cs               # IStartable, ITickable, IDisposable
│   ├── PlayerModuleServices.cs
│   ├── PlayerService.cs              # Тонкий фасад
│   ├── PlayerCombatAdapter.cs        # Адаптер боя через шину
│   ├── PlayerInputService.cs         # Чистый C#
│   ├── SleepService.cs               # Логика сна
│   ├── PlayerVisualService.cs        # Визуал
│   ├── StatService.cs                # Реальный IStatService
│   ├── PlayerConfig.cs               # class (BD-48)
│   └── Data/
│       └── PlayerData.cs             # Runtime-состояние игрока
│
├── World/                            # ✅ Модуль мира
│   ├── WorldModule.cs                # IStartable, ITickable
│   ├── WorldModuleServices.cs
│   ├── WorldConfig.cs
│   ├── WorldService.cs               # Реализация IWorldService
│   ├── TimeService.cs                # Реализация ITimeService
│   ├── LocationService.cs            # Локации и секторы
│   ├── FactionService.cs             # Фракции и отношения
│   ├── EventService.cs               # Мировые события
│   └── Data/
│       ├── WorldState.cs
│       ├── LocationData.cs
│       ├── FactionData.cs
│       └── WorldEventData.cs
│
├── Quest/                            # ✅ Модуль квестов
│   ├── QuestModule.cs
│   ├── QuestModuleServices.cs
│   ├── QuestConfig.cs
│   ├── QuestService.cs               # Реализация IQuestService
│   ├── QuestRewardService.cs         # Реализация IQuestRewardService
│   ├── QuestProgressTracker.cs       # Подписка на 6 событий
│   └── Data/
│       ├── QuestData.cs
│       ├── QuestObjective.cs
│       └── QuestReward.cs
│
├── Interaction/                      # ✅ Модуль взаимодействий
│   ├── InteractionModule.cs
│   ├── InteractionModuleServices.cs
│   ├── InteractionConfig.cs
│   ├── InteractionService.cs         # Реализация IInteractionService
│   ├── DialogueService.cs            # Реализация IDialogueService
│   ├── DialogueTypewriter.cs         # Эффект печатающегося текста
│   └── Data/
│       ├── DialogueNode.cs
│       └── DialogueChoice.cs
│
├── UI/                               # ✅ Модуль UI
│   ├── UIModule.cs
│   ├── UIModuleServices.cs
│   ├── UIConfig.cs
│   ├── UIService.cs                  # Реализация IUIService
│   ├── ToastService.cs               # Уведомления
│   ├── HUDPresenter.cs               # Презентер HUD (чистый C#)
│   ├── DialoguePresenter.cs          # Презентер диалогов
│   └── Data/
│       └── UIState.cs
│
├── Save/                             # ✅ Модуль сохранений
│   ├── SaveModule.cs
│   ├── SaveModuleServices.cs         # Без SaveLifetimeScope
│   ├── SaveService.cs                # Реализация ISaveService
│   ├── SaveConfig.cs
│   ├── SaveFileHandler.cs            # JSON I/O
│   ├── SaveDataAggregator.cs         # Сбор от ISaveable
│   └── Data/
│       ├── SaveSlotData.cs
│       └── AutoSaveConfig.cs
│
└── Generator/                        # ✅ Модуль генерации
    ├── GeneratorModuleServices.cs    # Утилитарный, без Module.cs
    ├── TechniqueGeneratorService.cs
    ├── ItemGeneratorService.cs
    ├── MatryoshkaGenerator.cs        # Базовый класс (Base × Grade × Specialization)
    ├── GradeSelector.cs              # Выбор грейда
    ├── MaterialRegistry.cs           # Реестр материалов (T1-T5)
    └── Data/
        └── GenerationConfig.cs
```

> **Итого:** 16 модулей, каждый с единым шаблоном: Module + ModuleServices + Service + Calculator/Helper + Config + Data/.

---

## 4. Структура `src/CultivationGame.Entry/` (точка входа)

```
CultivationGame.Entry/
├── CultivationGame.Entry.csproj
│
├── GameEntryPoint.cs                 # IStartable + ITickable (главная точка входа)
├── GameSession.cs                    # Управление жизненным циклом сессии
├── SceneOrchestrator.cs              # Оркестратор сборки сцены (10 фаз)
├── GameLifetimeScope.cs              # Корневой DI-конфигуратор
├── SceneAssemblyConfig.cs            # Конфигурация сборки
├── SceneAssemblyLogger.cs            # Логирование сборки
├── SceneAssemblyRegistrar.cs         # Регистрация фаз
├── MessagingRegistrar.cs             # Регистрация контрактов шины
│
├── Phases/                           # Фазы сборки сцены (10)
│   ├── AbstractSceneAssemblyPhase.cs # Базовый класс фазы
│   ├── CoreValidationPhase.cs        # Фаза 1: Валидация DI
│   ├── TileMapGenPhase.cs            # Фаза 2: Генерация тайлов
│   ├── WorldInitPhase.cs             # Фаза 3: Инициализация мира
│   ├── PlayerSpawnPhase.cs           # Фаза 4: Спавн игрока
│   ├── NPCSpawnPhase.cs              # Фаза 5: Спавн NPC
│   ├── FormationInitPhase.cs         # Фаза 6: Формации
│   ├── ChargerInitPhase.cs           # Фаза 7: Зарядники
│   ├── QuestInitPhase.cs             # Фаза 8: Квесты
│   ├── UIInitPhase.cs                # Фаза 9: UI
│   └── FinalizePhase.cs              # Фаза 10: Финализация
│
└── UI/                               # Entry-level UI views (22 файла)
    ├── HUDPanelView.cs
    ├── HotbarPanelView.cs
    ├── BuffBarView.cs
    ├── ToastView.cs
    ├── MiniMapView.cs
    ├── DialoguePanelView.cs
    ├── PausePanelView.cs
    ├── CombatOverlayView.cs
    ├── DeathScreenView.cs
    ├── LoadingScreenView.cs
    ├── CharacterPanelView.cs
    ├── TechniqueChargeView.cs
    ├── CombatLogView.cs
    ├── TurnOrderView.cs
    ├── DamageNumberView.cs
    ├── EnemyHealthBarView.cs
    ├── InputLogPanel.cs              # #if DEBUG
    ├── NPCInspectorPanel.cs          # #if DEBUG
    ├── ContextMenuUI.cs
    ├── GameInputAdapter.cs           # Адаптер ввода (F5/F9/Esc)
    ├── UIComponentResolver.cs
    └── Common/
        └── DraggableWindow.cs
```

---

## 5. Структура `src/CultivationGame.Adapter/` (engine-specific)

> Этот слой зависит от конкретного движка. При смене движка меняется только он.

```
CultivationGame.Adapter/
├── CultivationGame.Adapter.csproj
│
├── Rendering/                        # Рендеринг
│   ├── SpriteRenderer.cs             # Адаптер спрайта
│   ├── TilemapRenderer.cs            # Адаптер тайловой карты
│   ├── Camera2DAdapter.cs            # Адаптер ортографической камеры
│   ├── Light2DAdapter.cs             # Адаптер 2D-освещения
│   ├── SortingLayerManager.cs        # Управление слоями (6: Default/Background/Terrain/Objects/Player/UI)
│   └── MultiMeshBatcher.cs           # Батчинг спрайтов (для тайлов)
│
├── Input/                            # Ввод
│   ├── InputAdapter.cs               # Клавиатура + мышь → IPlayerInputService
│   └── HotkeyManager.cs              # F5/F9/Esc/E/B/R/X/F
│
├── UI/                               # UI infrastructure
│   ├── UIFactory.cs                  # Процедурное создание UI
│   ├── UITheme.cs                    # Тема «Древний Пергамент»
│   ├── UIFontCache.cs                # Кэш шрифтов
│   ├── UISpriteCache.cs              # Кэш спрайтов темы
│   └── UIPositioning.cs              # Промилле → пиксели (1000‰ = 100%)
│
├── Scene/                            # Сцена
│   ├── SceneBuilder.cs               # Программная сборка иерархии
│   ├── CameraFollow.cs               # Следование камеры за игроком
│   └── TilemapVisualService.cs       # Визуализация тайлмапа
│
├── Audio/                            # Аудио
│   ├── AudioService.cs               # SFX + музыка
│   └── AudioManager.cs
│
├── Persistence/                      # I/O
│   ├── FileHandler.cs                # Чтение/запись файлов (JSON)
│   └── SaveSerializer.cs             # JSON сериализация (опц. binary + GZIP)
│
└── Di/                               # DI-контейнер
    └── ContainerAdapter.cs           # Адаптер движкового DI или свой ServiceLocator
```

---

## 6. Структура `tests/`

```
tests/
├── CultivationGame.Core.Tests/                 # Тесты ядра (pure C#)
│   ├── CultivationGame.Core.Tests.csproj
│   ├── Messaging/
│   │   ├── EventBusTests.cs
│   │   └── ContractsTests.cs
│   ├── Data/
│   │   ├── ConstantsTests.cs
│   │   └── EnumsTests.cs
│   └── DI/
│       └── ContainerTests.cs
│
├── CultivationGame.Modules.Tests/               # Тесты модулей (pure C#)
│   ├── CultivationGame.Modules.Tests.csproj
│   ├── Body/
│   │   ├── BodyServiceTests.cs
│   │   ├── BodyFactoryTests.cs
│   │   └── BodyDamageCalculatorTests.cs
│   ├── Qi/
│   │   ├── QiServiceTests.cs
│   │   ├── QiBufferServiceTests.cs
│   │   ├── QiRegenCalculatorTests.cs
│   │   └── QiBreakthroughCalculatorTests.cs
│   ├── Combat/
│   │   ├── DamagePipelineTests.cs
│   │   ├── LevelSuppressionTests.cs
│   │   └── DefenseProcessorTests.cs
│   ├── Buff/
│   │   ├── BuffServiceTests.cs
│   │   └── BuffCalculatorTests.cs
│   ├── Inventory/
│   │   ├── EquipmentServiceTests.cs
│   │   └── CraftingServiceTests.cs
│   ├── NPC/
│   │   ├── NPCAIServiceTests.cs
│   │   └── NPCRelationshipServiceTests.cs
│   ├── Formation/
│   │   └── FormationCalculatorTests.cs
│   ├── Generator/
│   │   ├── MatryoshkaGeneratorTests.cs
│   │   └── GradeSelectorTests.cs
│   └── ...
│
└── CultivationGame.Integration.Tests/           # Интеграционные тесты (pure C#)
    ├── CultivationGame.Integration.Tests.csproj
    ├── HubAndSpokeIntegrationTests.cs           # Проверка развязки модулей
    ├── SaveLoadIntegrationTests.cs
    ├── CombatPipelineIntegrationTests.cs
    └── SceneAssemblyIntegrationTests.cs
```

> **Принцип:** Все игровые системы — pure C#, тестируются через `dotnet test`. Adapter-слой тестируется отдельно (через движковый test framework или скриншот-тесты).

---

## 7. JSON-конфиги (`data/`)

```
data/
├── techniques/                       # 34+ техник
│   ├── combat_techniques.json
│   ├── defense_techniques.json
│   ├── healing_techniques.json
│   ├── movement_techniques.json
│   ├── curse_techniques.json
│   ├── poison_techniques.json
│   └── formation_techniques.json
│
├── items/                            # 30+ расходников
│   ├── consumables.json
│   ├── materials.json
│   └── qi_stones.json
│
├── equipment/                        # 39+ экипировки
│   ├── weapons.json
│   ├── armor.json
│   └── accessories.json
│
├── npc_presets/                      # 15+ пресетов NPC
│   └── presets.json
│
├── quests/                           # 15+ квестов
│   ├── main_quests.json
│   ├── side_quests.json
│   └── daily_quests.json
│
├── elements/                         # 8 стихий
│   └── elements.json
│
├── cultivation_levels/               # 9 уровней
│   └── cultivation_levels.json
│
├── mortal_stages/                    # 6 этапов
│   └── mortal_stages.json
│
├── species/                          # 11 видов
│   └── species.json
│
├── materials/                        # Материалы (T1-T5)
│   ├── metals.json
│   ├── leather.json
│   ├── cloth.json
│   ├── wood.json
│   ├── bone.json
│   ├── crystal.json
│   ├── gems.json
│   ├── organic.json
│   ├── spirit.json
│   └── void.json
│
└── grades/                           # Грейды
    ├── equipment_grades.json         # 5: Damaged/Common/Refined/Perfect/Transcendent
    └── technique_grades.json         # 4: Common/Refined/Perfect/Transcendent
```

---

## 8. Сцены (`scenes/`)

> Сцены — текстовые файлы. AI-агенты могут авторить напрямую. При смене движка — конвертация формата.

```
scenes/
├── MainMenu.scene                    # Главное меню
│   ├── UI-корень (overlay)
│   ├── Кнопки: New Game, Load, Settings, Quit
│   └── Список сохранений
│
├── GameWorld.scene                   # Основная игровая сцена
│   ├── Камера (ортографическая, следование)
│   ├── UI-корень (overlay)
│   ├── World Root (сетка + тайловый слой + объекты)
│   ├── Player (спрайт + процедурный визуал)
│   ├── Источник 2D-освещения (глобальный)
│   └── GameInputAdapter (F5/F9/Esc)
│
└── Combat.scene                      # (опционально, если бой отдельной сценой)
    ├── Камера
    ├── Боевая арена (тайлы)
    ├── Combat Overlay UI
    └── Damage Numbers layer
```

---

## 9. Ассеты (`assets/`)

```
assets/
├── sprites/
│   ├── tiles/                        # 64×64 @ PPU=32
│   │   ├── grass.png
│   │   ├── dirt.png
│   │   ├── stone.png
│   │   ├── water.png
│   │   ├── sand.png
│   │   ├── snow.png
│   │   └── ...
│   ├── characters/                   # 128×128 @ PPU=64
│   │   ├── player/
│   │   ├── human/
│   │   ├── elf/
│   │   ├── demon/
│   │   └── ...
│   ├── objects/
│   │   ├── trees/
│   │   ├── rocks/
│   │   ├── buildings/
│   │   └── furniture/
│   ├── effects/                      # 12 effect sprites + 8 orbital-weapon
│   │   ├── expanding/                # Расширяющиеся эффекты
│   │   ├── directional/              # Направленные эффекты
│   │   └── orbital/                  # Орбитальное оружие
│   ├── ui/
│   │   ├── borders/                  # Бордюры темы «Древний Пергамент»
│   │   ├── ornaments/                # Орнаменты
│   │   └── slots/                    # Слоты экипировки
│   └── icons/                        # Иконки предметов
│       ├── weapons/
│       ├── armor/
│       ├── consumables/
│       └── materials/
│
├── audio/
│   ├── sfx/
│   │   ├── combat/
│   │   ├── ui/
│   │   └── environment/
│   └── music/
│       ├── menu/
│       ├── world/
│       └── combat/
│
└── themes/
    └── parchment.theme               # Тема «Древний Пергамент»
        # Цвета, спрайты, промилле-размеры (1000‰ = 100%)
```

---

## 10. Сохранения (`saves/`)

```
saves/
├── slot1/                            # Ручное сохранение
│   ├── main.sav                      # JSON: персонаж, техники, инвентарь (10–50 KB)
│   ├── chunks/                       # Чанки мира (chunk-based persistence)
│   │   ├── chunk_0_0.sav
│   │   ├── chunk_0_1.sav
│   │   └── ...
│   ├── locations/                    # Состояние локаций
│   │   ├── location_town_a.sav
│   │   └── ...
│   └── metadata.sav                  # Метаданные (<1 KB)
│
├── slot2/
├── slot3/
├── autosave/                         # Автосохранение (перезаписывается)
└── quicksave/                        # Быстрое сохранение (F5)
```

**Размеры:**
- 100h gameplay: ~5–15 KB compressed.
- 1000h: ~100 KB.
- Extreme 2000 locations: ~1–2 MB.

---

## 11. Namespace правила

```
CultivationGame.Core                  — Ядро (Enums, Constants, Interfaces, Messaging)
CultivationGame.Core.Data             — Данные, таблицы
CultivationGame.Core.Messaging        — Контракты (readonly struct)
CultivationGame.Core.DI               — DI абстракции
CultivationGame.Modules.Body          — Модуль тела
CultivationGame.Modules.Buff          — Модуль баффов
CultivationGame.Modules.Charger       — Модуль зарядников
CultivationGame.Modules.Inventory     — Модуль инвентаря
CultivationGame.Modules.Qi            — Модуль Ци
CultivationGame.Modules.Tile          — Модуль тайлов
CultivationGame.Modules.Combat        — Модуль боя
CultivationGame.Modules.Formation     — Модуль формаций
CultivationGame.Modules.NPC           — Модуль NPC
CultivationGame.Modules.Player        — Модуль игрока
CultivationGame.Modules.World         — Модуль мира
CultivationGame.Modules.Quest         — Модуль квестов
CultivationGame.Modules.Interaction   — Модуль взаимодействий
CultivationGame.Modules.UI            — Модуль UI
CultivationGame.Modules.Save          — Модуль сохранений
CultivationGame.Modules.Generator     — Модуль генерации
CultivationGame.Entry                 — Точка входа + Scene Assembly
CultivationGame.Adapter               — Engine-specific adapter
CultivationGame.Adapter.Rendering     — Рендеринг
CultivationGame.Adapter.Input         — Ввод
CultivationGame.Adapter.UI            — UI infrastructure
CultivationGame.Adapter.Scene         — Сцена
CultivationGame.Adapter.Audio         — Аудио
CultivationGame.Adapter.Persistence   — I/O
```

---

## 12. .csproj конфигурация

```
<!-- CultivationGame.Core.csproj — engine-agnostic, чистый C# -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>  <!-- для hot-path оптимизаций -->
  </PropertyGroup>
</Project>

<!-- CultivationGame.Modules.csproj — engine-agnostic, зависит от Core -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\CultivationGame.Core\CultivationGame.Core.csproj" />
  </ItemGroup>
</Project>

<!-- CultivationGame.Adapter.csproj — engine-specific -->
<!-- Зависит от движковых пакетов. Меняется при смене движка. -->
```

---

## 13. Связанные документы

| Документ | Описание |
|----------|----------|
| `ARCHITECTURE.md` | Высокоуровневая архитектура |
| `MODULE_STRUCTURE.md` | 16 модулей (детально) |
| `DI_AND_EVENTBUS.md` | DI и шина событий |
| `PERFORMANCE_STRATEGY.md` | Производительность |
| `09_workflow/AI_DEVELOPMENT_WORKFLOW.md` | Headless-тестирование, цикл AI-разработки |

---

*Документ создан в рамках Task 2-a: engine-agnostic rewrite. Источник: `docs/ARCHITECTURE_FILE_TREE.md` v3.17, `docs/ARCHITECTURE_CODE.md` §2-3.*

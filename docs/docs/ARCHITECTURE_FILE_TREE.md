# Дерево файлов: Cultivation World Simulator

> Версия: 3.17  
> Дата: 2026-07-14  
> ← Назад к [ARCHITECTURE_CODE.md](ARCHITECTURE_CODE.md)  
> **Актуализировано:** 429 .cs файлов, 44 интерфейса, 19 stubs, 22 Entry/UI views, 16 модулей, 10 runtime-фаз

---

## UnityProject/Assets/Scripts/

```
├── Core/                           # ЯДРО — общие контракты и данные
│   ├── VisualProvider.cs           # Провайдер визуальных данных
│   ├── SortingLayerManager.cs      # Управление слоями сортировки
│   ├── RenderPipelineLogger.cs     # Логирование рендер-пайплайна
│   ├── SpriteHelper.cs             # Утилиты для спрайтов
│   ├── DI/                         # ModuleLifetimeScope.cs
│   ├── Data/                       # Constants.cs, Enums.cs, GameTile.cs, StatType.cs, ...
│   │   ├── BodyPartTemplate.cs     # ✅ Фаза 3 доработка — шаблон части тела
│   │   ├── BodyTemplate.cs         # ✅ Фаза 3 доработка — шаблон тела (композиция)
│   │   ├── SpeciesData.cs          # ✅ Фаза 3 доработка — данные вида
│   │   └── ScriptableObjects/      # ItemData.cs, EquipmentData.cs
│   ├── Interfaces/                 # 26 сервисных интерфейсов
│   │   ├── IChargerService.cs
│   │   ├── ITileService.cs
│   │   ├── IResourceService.cs
│   │   ├── IBodyService.cs
│   │   ├── ITimeService.cs
│   │   ├── IQiService.cs
│   │   ├── IQiBufferService.cs
│   │   ├── IBuffService.cs
│   │   ├── IStatService.cs
│   │   ├── IInventoryService.cs
│   │   ├── IStorageService.cs
│   │   ├── ICraftingService.cs
│   │   ├── IEquipmentService.cs
│   │   ├── ICombatService.cs
│   │   ├── IDamageService.cs
│   │   ├── IFormationService.cs
│   │   ├── INPCService.cs
│   │   ├── INPCSpawnerService.cs
│   │   ├── IPlayerService.cs
│   │   ├── IPlayerInputService.cs
│   │   ├── IWorldService.cs        # ✅ Фаза 11
│   │   ├── IEventService.cs        # ✅ Фаза 11
│   │   ├── IQuestService.cs        # ✅ Фаза 12
│   │   ├── IQuestRewardService.cs  # ✅ Фаза 12
│   │   ├── IInteractionService.cs  # ✅ Фаза 13
│   │   ├── IDialogueService.cs     # ✅ Фаза 13
│   │   ├── IUIService.cs           # ✅ Фаза 14
│   │   ├── ISaveService.cs         # ✅ Фаза 15
│   │   ├── ISaveable.cs            # ✅ Фаза 15
│   │   └── ISceneAssemblyPhase.cs  # ✅ Фаза 16
│   └── Messaging/
│       └── Contracts/              # 19 файлов контрактов (readonly struct)
│           ├── GameContracts.cs
│           ├── CombatContracts.cs
│           ├── BodyContracts.cs
│           ├── QiContracts.cs
│           ├── BuffContracts.cs
│           ├── ChargerContracts.cs
│           ├── TileContracts.cs
│           ├── InventoryContracts.cs
│           ├── PlayerContracts.cs
│           ├── WorldContracts.cs
│           ├── NPCContracts.cs
│           ├── FormationContracts.cs
│           ├── QuestContracts.cs
│           ├── SaveContracts.cs
│           ├── DialogueContracts.cs
│           ├── StatContracts.cs
│           ├── CraftingContracts.cs
│           ├── UIContracts.cs
│           └── SceneContracts.cs    # ✅ Фаза 16
│
├── Entry/                          # ТОЧКА ВХОДА + Scene Assembly
│   ├── GameEntryPoint.cs           # IStartable + ITickable
│   ├── SceneOrchestrator.cs        # Оркестратор сборки сцены (Фаза 16)
│   ├── GameSession.cs              # Управление игровой сессией (Фаза 18)
│   ├── RuntimeSceneBuilder.cs      # Программная сборка сцены (Фаза 16)
│   ├── GameLifetimeScope.cs        # Корневой DI-сконфигуратор
│   ├── SceneAssemblyConfig.cs      # Конфигурация сборки сцены
│   ├── SceneAssemblyLogger.cs      # Логирование сборки сцены
│   ├── SceneAssemblyRegistrar.cs   # Регистрация фаз сборки
│   ├── MessagingRegistrar.cs       # Регистрация MessagePipe брокеров
│   ├── CameraFollow.cs            # Следование камеры за игроком
│   ├── TilemapVisualService.cs    # Визуализация тайлмапа
│   │
│   ├── Phases/                    # Фазы сборки сцены (11 файлов)
│   │   ├── AbstractSceneAssemblyPhase.cs  # Базовый класс фазы
│   │   ├── CoreValidationPhase.cs        # Валидация ядра
│   │   ├── TileMapGenPhase.cs            # Генерация тайловой карты
│   │   ├── WorldInitPhase.cs             # Инициализация мира
│   │   ├── PlayerSpawnPhase.cs           # Спавн игрока
│   │   ├── NPCSpawnPhase.cs              # Спавн NPC
│   │   ├── FormationInitPhase.cs         # Инициализация формаций
│   │   ├── ChargerInitPhase.cs           # Инициализация зарядников
│   │   ├── QuestInitPhase.cs             # Инициализация квестов
│   │   ├── UIInitPhase.cs                # Инициализация UI
│   │   └── FinalizePhase.cs              # Финализация сборки
│   │
│   ├── UI/                        # Entry-level UI (5 файлов)
│   │   ├── LoadingScreenView.cs    # Экран загрузки
│   │   ├── PausePanelView.cs       # Панель паузы
│   │   ├── DialoguePanelView.cs    # Панель диалогов
│   │   ├── GameInputAdapter.cs     # Адаптер ввода
│   │   └── HUDPanelView.cs         # Панель HUD
│   │
│   └── Stubs/                     # Stub-сервисы (до реализации модулей)
│       └── StubStatService.cs      # 🔒 Fallback (StatService — основная реализация в Modules.Player)
│
└── Modules/                        # МОДУЛИ (каждый независим)
    ├── Body/                       # ✅ Фаза 3 (+ доработка)
    │   ├── BodyModule.cs           # Точка входа модуля (IStartable, ITickable)
    │   ├── BodyLifetimeScope.cs    # DI-конфигуратор
    │   ├── BodyModuleServices.cs   # ModuleServices (Фаза 17) — +BodyTemplateProvider, BodyFactory, SpeciesRegistry
    │   ├── BodyService.cs          # Реализация IBodyService (+ReattachPart, RecalculateHPFromVitality, GetMorphology, GetSizeClass)
    │   ├── BodyPart.cs             # Данные части тела (+BodyPartFunction Functions, +Reattach())
    │   ├── IBodyFactory.cs         # ✅ P1-10 FIX — интерфейс фабрики тел (для тестируемости)
    │   ├── BodyFactory.cs          # ✅ Фаза 3 доработка — фабрика создания тел (замена BodyMorphology, +IBodyFactory)
    │   ├── BodyTemplateProvider.cs # ✅ Фаза 3 доработка — провайдер шаблонов (6 морфологий + 4 гибрида)
    │   ├── SpeciesRegistry.cs      # ✅ Фаза 3 доработка — реестр 11 видов (ALGORITHMS.md П.25)
    │   ├── SeveredDebuffSystem.cs  # ✅ Фаза 3 доработка — дебаффы от ампутации (П.23)
    │   ├── BodySlotMapping.cs      # Маппинг BodyPart → EquipmentSlot (+22 не-гуманоидных)
    │   └── BodyDamageCalculator.cs # Расчёт урона по телу
    │
    ├── Buff/                       # ✅ Фаза 5
    │   ├── BuffModule.cs           # Точка входа модуля (IStartable, ITickable)
    │   ├── BuffLifetimeScope.cs    # DI-конфигуратор
    │   ├── BuffModuleServices.cs   # ModuleServices (Фаза 17)
    │   ├── BuffService.cs          # Реализация IBuffService
    │   ├── BuffCalculator.cs       # Расчёт модификаторов + мягкий кап
    │   ├── BuffTickProcessor.cs    # Обработка тиков баффов
    │   ├── BuffConfig.cs           # Конфигурация баффов
    │   └── ActiveBuff.cs           # Данные активного баффа
    │
    ├── Charger/                    # ✅ Фаза 1
    │   ├── ChargerModule.cs        # Точка входа модуля
    │   ├── ChargerLifetimeScope.cs # DI-конфигуратор
    │   ├── ChargerModuleServices.cs # ModuleServices (Фаза 17)
    │   ├── ChargerService.cs       # Реализация IChargerService
    │   ├── ChargerBuffer.cs        # Ци-буфер зарядника
    │   ├── ChargerData.cs          # Данные камня Ци
    │   ├── ChargerHeat.cs          # Тепловой баланс
    │   └── ChargerSlot.cs          # Слот зарядника
    │
    ├── Inventory/                  # ✅ Фаза 6
    │   ├── InventoryModule.cs      # Точка входа модуля (IStartable)
    │   ├── InventoryLifetimeScope.cs # DI-конфигуратор
    │   ├── InventoryModuleServices.cs # ModuleServices (Фаза 17)
    │   ├── InventoryService.cs     # Реализация IInventoryService
    │   ├── InventoryConfig.cs      # Конфигурация модуля
    │   ├── EquipmentService.cs     # Реализация IEquipmentService
    │   ├── EquipmentValidator.cs   # Проверки слотов, требования, тело
    │   ├── EquipmentStatAggregator.cs # Подсчёт бонусов экипировки
    │   ├── StorageService.cs       # Реализация IStorageService (Spirit + Ring)
    │   ├── CraftingService.cs      # Реализация ICraftingService
    │   ├── MaterialService.cs      # Работа с материалами
    │   └── Data/
    │       └── CraftingRecipe.cs   # Данные рецепта крафта
    │
    ├── Qi/                         # ✅ Фаза 4
    │   ├── QiModule.cs             # Точка входа модуля (IStartable, ITickable) + QiModuleConfig
    │   ├── QiLifetimeScope.cs      # DI-конфигуратор
    │   ├── QiModuleServices.cs     # ModuleServices (Фаза 17)
    │   ├── QiService.cs            # Реализация IQiService
    │   ├── QiBufferService.cs      # Реализация IQiBufferService
    │   ├── QiConfig.cs             # Конфигурация Ци
    │   ├── QiRegenCalculator.cs    # Расчёт регенерации Ци
    │   └── QiBreakthroughCalculator.cs # Расчёт прорыва культивации
    │
    ├── Tile/                       # ✅ Фаза 2
    │   ├── TileModule.cs           # Точка входа модуля
    │   ├── TileLifetimeScope.cs    # DI-конфигуратор
    │   ├── TileModuleServices.cs   # ModuleServices (Фаза 17)
    │   ├── TileMapService.cs       # Реализация ITileService
    │   ├── ResourceService.cs      # Реализация IResourceService
    │   ├── DestructibleService.cs  # Разрушаемые объекты
    │   └── TileGeneratorService.cs # Генерация карты
    │
    ├── Formation/                   # ✅ Фаза 8
    │   ├── FormationModule.cs       # Точка входа модуля (IStartable, ITickable, IDisposable)
    │   ├── FormationLifetimeScope.cs # DI-конфигуратор
    │   ├── FormationModuleServices.cs # ModuleServices (Фаза 17)
    │   ├── FormationService.cs     # Реализация IFormationService
    │   ├── FormationCalculator.cs  # Формулы: contourQi, capacity, drain
    │   ├── FormationQiPool.cs      # Пул Ци формации (БЕЗ дублирования QiBuffer)
    │   ├── FormationEffects.cs     # Эффекты (БЕЗ статического состояния)
    │   ├── FormationConfig.cs      # Конфигурация
    │   └── Data/
    │       └── FormationData.cs    # Данные определений формаций
    │
    ├── NPC/                        # ✅ Фаза 9
    │   ├── NPCModule.cs           # Точка входа модуля (IStartable, ITickable, IDisposable)
    │   ├── NPCLifetimeScope.cs    # DI-конфигуратор
    │   ├── NPCModuleServices.cs   # ModuleServices (Фаза 17)
    │   ├── NPCService.cs          # Реализация INPCService
    │   ├── NPCSpawnerService.cs   # Реализация INPCSpawnerService
    │   ├── NPCRelationshipService.cs # Логика отношений (Attitude + затухание)
    │   ├── NPCAIService.cs        # Упрощённый Behaviour Tree
    │   ├── NPCCombatAdapter.cs    # Адаптер боя через MessagePipe
    │   ├── NPCMovementService.cs  # Движение и навигация (упрощённая)
    │   ├── NPCConfig.cs           # Конфигурация модуля (class, BD-48)
    │   └── Data/
    │       └── NPCState.cs        # Runtime-состояние NPC
    │
    ├── Player/                      # ✅ Фаза 10
    │   ├── PlayerModule.cs          # Точка входа модуля (IStartable, ITickable, IDisposable)
    │   ├── PlayerLifetimeScope.cs   # DI-конфигуратор
    │   ├── PlayerModuleServices.cs  # ModuleServices (Фаза 17)
    │   ├── PlayerService.cs         # Реализация IPlayerService (тонкий фасад)
    │   ├── PlayerCombatAdapter.cs   # Адаптер боя через MessagePipe
    │   ├── PlayerInputService.cs    # Реализация IPlayerInputService (чистый C#)
    │   ├── SleepService.cs          # Логика сна (TimeChangedEvent → эффекты)
    │   ├── PlayerVisualService.cs   # Визуал (заглушка, Фаза 14)
    │   ├── StatService.cs           # ✅ Фаза 3 доработка — реальный IStatService (замена StubStatService)
    │   ├── PlayerConfig.cs          # Конфигурация модуля (class, BD-48)
    │   └── Data/
    │       └── PlayerData.cs        # Runtime-состояние игрока
    │
    ├── World/                       # ✅ Фаза 11
    │   ├── WorldModule.cs           # Точка входа модуля (IStartable, ITickable)
    │   ├── WorldLifetimeScope.cs    # DI-конфигуратор
    │   ├── WorldModuleServices.cs   # ModuleServices (Фаза 17)
    │   ├── WorldConfig.cs           # Конфигурация мира
    │   ├── WorldService.cs          # Реализация IWorldService
    │   ├── TimeService.cs           # Реализация ITimeService (было Stub, теперь реально)
    │   ├── LocationService.cs       # Управление локациями и секторами
    │   ├── FactionService.cs        # Логика фракций и отношений
    │   ├── EventService.cs          # Реализация IEventService (мировые события)
    │   └── Data/
    │       ├── WorldState.cs        # Runtime-состояние мира
    │       ├── LocationData.cs      # Данные локации
    │       ├── FactionData.cs       # Данные фракции
    │       └── WorldEventData.cs    # Данные мирового события
    │
    ├── Quest/                       # ✅ Фаза 12
    │   ├── QuestModule.cs           # Точка входа модуля (IStartable, ITickable)
    │   ├── QuestLifetimeScope.cs    # DI-конфигуратор
    │   ├── QuestModuleServices.cs   # ModuleServices (Фаза 17)
    │   ├── QuestConfig.cs           # Конфигурация квестов
    │   ├── QuestService.cs          # Реализация IQuestService
    │   ├── QuestRewardService.cs    # Реализация IQuestRewardService
    │   ├── QuestProgressTracker.cs  # Отслеживание прогресса целей
    │   └── Data/
    │       ├── QuestData.cs         # Данные определения квеста
    │       ├── QuestObjective.cs    # Данные цели квеста
    │       └── QuestReward.cs       # Данные награды за квест
    │
    ├── Interaction/                 # ✅ Фаза 13
    │   ├── InteractionModule.cs     # Точка входа модуля (IStartable, ITickable)
    │   ├── InteractionLifetimeScope.cs # DI-конфигуратор
    │   ├── InteractionModuleServices.cs # ModuleServices (Фаза 17)
    │   ├── InteractionConfig.cs     # Конфигурация взаимодействий
    │   ├── InteractionService.cs    # Реализация IInteractionService
    │   ├── DialogueService.cs       # Реализация IDialogueService
    │   ├── DialogueTypewriter.cs    # Эффект печатающегося текста
    │   └── Data/
    │       ├── DialogueNode.cs      # Узел диалога
    │       └── DialogueChoice.cs    # Вариант ответа в диалоге
    │
    └── UI/                          # ✅ Фаза 14
        ├── UIModule.cs              # Точка входа модуля (IStartable, ITickable)
        ├── UILifetimeScope.cs       # DI-конфигуратор
        ├── UIModuleServices.cs      # ModuleServices (Фаза 17)
        ├── UIConfig.cs              # Конфигурация UI
        ├── UIService.cs             # Реализация IUIService
        ├── ToastService.cs          # Уведомления (toast-сообщения)
        ├── HUDPresenter.cs          # Презентер HUD
        ├── DialoguePresenter.cs     # Презентер диалогов
        └── Data/
            └── UIState.cs           # Состояние UI
    │
    └── Save/                        # ✅ Фаза 15
        ├── SaveModule.cs            # Точка входа модуля (IStartable, ITickable)
        ├── SaveService.cs           # Реализация ISaveService
        ├── SaveConfig.cs            # Конфигурация сохранений
        ├── SaveModuleServices.cs    # ModuleServices (Фаза 17)
        ├── SaveFileHandler.cs       # Чтение/запись файлов сохранений
        ├── SaveDataAggregator.cs    # Агрегация данных от ISaveable
        └── Data/
            ├── SaveSlotData.cs      # Данные слота сохранения
            └── AutoSaveConfig.cs    # Конфигурация автосохранения
```

---

## Namespace правила

```
CultivationGame.Core            — Ядро (Enums, Constants, Interfaces, Messaging)
CultivationGame.Core.Data       — SO, данные, таблицы
CultivationGame.Core.Messaging  — Контракты сообщений (readonly struct)
CultivationGame.Modules.Charger — Модуль зарядников
CultivationGame.Modules.Tile    — Модуль тайлов
CultivationGame.Modules.Body    — Модуль тела
CultivationGame.Modules.Qi      — Модуль Ци
CultivationGame.Modules.Buff    — Модуль баффов/дебаффов
CultivationGame.Modules.Inventory — Модуль инвентаря/экипировки/крафта
CultivationGame.Modules.Combat   — Модуль боя ✅ реализован
CultivationGame.Modules.Formation — Модуль формаций ✅ реализован
CultivationGame.Modules.NPC — Модуль NPC ✅ реализован
CultivationGame.Modules.Player — Модуль игрока ✅ реализован
CultivationGame.Modules.World — Модуль мира ✅ реализован
CultivationGame.Modules.Quest — Модуль квестов ✅ реализован
CultivationGame.Modules.Interaction — Модуль взаимодействий ✅ реализован
CultivationGame.Modules.UI — Модуль UI ✅ реализован
CultivationGame.Modules.Save — Модуль сохранений ✅ реализован
CultivationGame.Entry            — Точка входа + стабы + Scene Assembly
```

---

*← Назад к [ARCHITECTURE_CODE.md](ARCHITECTURE_CODE.md)*  
*Создано: 2026-05-18 — извлечено из ARCHITECTURE_CODE.md v3.16*  
*Редактировано: 2026-07-14 — актуализация чисел (429 файлов, 44 интерфейса, 22 Entry/UI views, 19 stubs)*

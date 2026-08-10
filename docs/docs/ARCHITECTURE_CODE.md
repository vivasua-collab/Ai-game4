# Архитектура кодовой базы: Cultivation World Simulator

> Версия: 3.18  
> Дата: 2026-07-14  
> Статус: ✅ Актуально (модульная пересборка завершена, UI V3 Фазы 0–4 реализованы, в активной переработке, Fix Plan A завершён, B в процессе, C–D pending)
> Обновлено: 2026-07-14 — v3.18: актуализация чисел (429 файлов, 44 интерфейса, 19 stubs, 22 Entry/UI views, 16 модулей, 10 runtime-фаз). Добавлены 06_17-06_18 инфраструктурные фиксы: UIFontCache static init, WireUIViews FindObjectsInactive.Include, VContainerException фикс, MiniMapView V3, CreateText ContentSizeFitter, CreateOverlayView CanvasGroup 4.3.
> Обновлено: 2026-05-20 — v3.17: Фаза 0 док-обновление. §2: +Generator модуль (17 модулей). §5: UIContracts/SceneContracts — фикс имён событий по коду. §8: StubStatService удалён (→StatService). InputLogContracts добавлен.
> Обновлено: 2026-05-18 — v3.16: Фаза 3 Body доработка (BodyTemplate, BodyFactory, SpeciesRegistry, StatService, SeveredDebuffSystem). IStatService → реальный (9 методов). IBodyService → 14 методов. 241 файл, 25 интерфейсов, 0 stub.
> Обновлено: 2026-05-11 — v3.15: Фазы 15-19 (Save, Scene Assembly, SceneOrchestrator, GameSession). SaveModule, SaveService, SaveFileHandler, SaveDataAggregator. SceneOrchestrator, GameSession, RuntimeSceneBuilder, 11 Phases. Entry/UI (LoadingScreenView, PausePanelView, DialoguePanelView, HUDPanelView, GameInputAdapter). Core (VisualProvider, SortingLayerManager, RenderPipelineLogger, SpriteHelper). ModuleServices pattern. 16 модулей + SceneOrchestrator + GameSession, 234 файла, 26 интерфейсов.

---

## ⚠️ Важно

> **Это АКТУАЛЬНАЯ архитектура кодовой базы модульной пересборки.**  
> Старая архитектура (Singleton + ServiceLocator + C# Events) — УСТАРЕЛА.  
> Новый стек: **VContainer + MessagePipe + UniTask**.  
> Legacy код заморожен в `UnityProject/Legacy/UnityAssets/` — НЕ компилируется.

---

## 1. Общая архитектура: Hub-and-Spoke

### Принцип: Звезда (Core — центр, Модули — спицы)

```
                    ┌──────────────────────────────────┐
                    │            CORE                   │
                    │                                   │
                    │  Интерфейсы (IXxxService)         │
                    │  Контракты (readonly struct)      │
                    │  Данные (Enums, Constants, SO)    │
                    │  DI (ModuleLifetimeScope)         │
                    │  Messaging (MessagePipe brokers)  │
                    │                                   │
                    └──────────────┬────────────────────┘
                                   │
   ┌─────┬────┬─────┬────┬────┬────┼────┬──────┬─────┬─────┬──────┬──────┬──────┬──────┬──────┬─────┐
   │     │    │     │    │    │    │    │      │     │     │      │      │      │      │      │     │
┌──▼──┐┌▼──┐┌▼───┐┌▼──┐┌▼──┐┌▼──┐┌▼──┐┌▼───┐┌▼───┐┌▼──┐┌▼───┐┌▼───┐┌▼───┐┌▼───┐┌▼──┐
│Chrg ││Tl ││Bdy ││ Qi ││Bff ││Inv ││Cmb ││Frm. ││NPC ││Plr ││Wrld ││Qst  ││Intr ││ UI  ││Sav │
│ ✅  ││ ✅││ ✅ ││ ✅ ││ ✅ ││ ✅ ││ ✅ ││ ✅  ││ ✅ ││ ✅ ││ ✅ ││ ✅  ││ ✅  ││ ✅ ││ ✅│
└─────┘└───┘└────┘└───┘└───┘└───┘└───┘└────┘└────┘└───┘└────┘└────┘└────┘└────┘└───┘
   │     │    │     │    │    │    │    │      │     │     │      │      │      │      │
   └─────┴────┴─────┴────┴────┴────┴────┴──────┴─────┴─────┴──────┴──────┴──────┴──────┘
                      ❌ ПРЯМЫЕ СВЯЗИ ЗАПРЕЩЕНЫ
```

**Правило:** Модуль НЕ знает о других модулях. Общение — только через MessagePipe (публикация/подписка на контракты из Core.Messaging) или через интерфейсы Core.Interfaces.

---

## 2. Структура папок

> 📂 **Полное дерево файлов вынесено в отдельный документ:** [ARCHITECTURE_FILE_TREE.md](ARCHITECTURE_FILE_TREE.md)
>
> Это самая объёмная справочная секция (~280 строк), обновляемая при каждом изменении файлов.
> Размещение в отдельном файле ускоряет навигацию по ARCHITECTURE_CODE.md и упрощает
> синхронизацию дерева при добавлении/удалении файлов.

### Краткая структура (3 уровня)

```
UnityProject/Assets/Scripts/
├── Core/           # ЯДРО — интерфейсы, данные, контракты, DI
├── Entry/          # ТОЧКА ВХОДА + Scene Assembly + UI + Stubs
└── Modules/        # МОДУЛИ (17 независимых)
    ├── Body/       # ✅ Фаза 3 (+ доработка)
    ├── Buff/       # ✅ Фаза 5
    ├── Charger/    # ✅ Фаза 1
    ├── Inventory/  # ✅ Фаза 6
    ├── Qi/         # ✅ Фаза 4
    ├── Tile/       # ✅ Фаза 2
    ├── Combat/     # ✅ Фаза 7
    ├── Formation/  # ✅ Фаза 8
    ├── NPC/        # ✅ Фаза 9
    ├── Player/     # ✅ Фаза 10
    ├── World/      # ✅ Фаза 11
    ├── Quest/      # ✅ Фаза 12
    ├── Interaction/ # ✅ Фаза 13
    ├── UI/         # ✅ Фаза 14
    ├── Save/       # ✅ Фаза 15
    └── Generator/  # ✅ Фаза 0 (генерация предметов)
```

---

## 3. Namespace правила

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
CultivationGame.Modules.Generator — Модуль генерации предметов ✅ реализован
CultivationGame.Entry            — Точка входа + стабы + Scene Assembly
```

---

## 4. Dependency Injection: VContainer

### Стратегия: [Inject] через интерфейсы Core

**Приоритет получения зависимостей:**

```
1. [Inject] IXxxService           ← предпочтительно (через интерфейс ядра)
2. IPublisher<T> / ISubscriber<T> ← MessagePipe (через контракт ядра)
3. Конкретный тип ВНУТРИ модуля   ← допустимо только в пределах модуля
```

### Корневой сконфигуратор: GameLifetimeScope

```csharp
public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // 1. Регистрация MessagePipe
        var options = builder.RegisterMessagePipe();

        // 2. Регистрация ВСЕХ брокеров сообщений (через MessagingRegistrar)
        MessagingRegistrar.Register(builder, options);

        // 3. Регистрация модулей (через делегаты — Phase 17B)
        WorldModuleServices.Register(builder, options);      // ITimeService, IWorldService, IEventService
        TileModuleServices.Register(builder, options);        // ITileService, IResourceService
        BodyModuleServices.Register(builder, options);        // IBodyService, BodyTemplateProvider, BodyFactory, SpeciesRegistry
        QiModuleServices.Register(builder, options);          // IQiService, IQiBufferService
        BuffModuleServices.Register(builder, options);        // IBuffService
        InventoryModuleServices.Register(builder, options);   // IInventoryService, IEquipmentService, ICraftingService
        CombatModuleServices.Register(builder, options);      // ICombatService, IDamageService
        FormationModuleServices.Register(builder, options);   // IFormationService
        NPCModuleServices.Register(builder, options);         // INPCSpawnerService, INPCService
        PlayerModuleServices.Register(builder, options);      // IPlayerService, IPlayerInputService, IStatService
        QuestModuleServices.Register(builder, options);       // IQuestService, IQuestRewardService
        InteractionModuleServices.Register(builder, options); // IInteractionService, IDialogueService
        UIModuleServices.Register(builder, options);          // IUIService
        ChargerModuleServices.Register(builder, options);     // IChargerService
        SaveModuleServices.Register(builder, options);        // ISaveService, ISaveable-агрегация

        // 4. Оркестратор сборки сцены + фазы + точка входа
        SceneAssemblyRegistrar.Register(builder);
    }
}
```

### Модульный сконфигуратор: XxxLifetimeScope

Каждый модуль имеет свой `ModuleLifetimeScope`:

```csharp
public class ChargerLifetimeScope : ModuleLifetimeScope
{
    public override string ModuleName => "Charger";

    protected override void Configure(IContainerBuilder builder)
    {
        // Регистрация реализации интерфейса
        builder.Register<IChargerService, ChargerService>(Lifetime.Singleton);

        // Регистрация точки входа модуля
        builder.Register<ChargerModule>(Lifetime.Singleton)
            .AsImplementedInterfaces(); // IStartable, ITickable

        // Конфигурация по умолчанию
        builder.RegisterBuildCallback(container =>
        {
            var module = container.Resolve<ChargerModule>();
            module.SetConfig(bufferConfig, slotConfigs);
        });
    }
}
```

> **⚠️ ВНИМАНИЕ:** В Unity Editor нужно установить `parent = GameLifetimeScope` для каждого модульного LifetimeScope (урок CH-34 из Фазы 1).

### Зарегистрированные сервисы (на момент Фазы 19)

| Интерфейс | Реализация | Модуль | Статус |
|-----------|------------|--------|--------|
| `IChargerService` | `ChargerService` | Modules.Charger | ✅ Реализован |
| `ITileService` | `TileMapService` | Modules.Tile | ✅ Реализован |
| `IResourceService` | `ResourceService` | Modules.Tile | ✅ Реализован |
| `IBodyService` | `BodyService` | Modules.Body | ✅ Реализован (14 методов) |
| `ITimeService` | `TimeService` | Modules.World | ✅ Реализован |
| `IQiService` | `QiService` | Modules.Qi | ✅ Реализован |
| `IQiBufferService` | `QiBufferService` | Modules.Qi | ✅ Реализован |
| `IBuffService` | `BuffService` | Modules.Buff | ✅ Реализован |
| `IStatService` | `StatService` | Modules.Player | ✅ Реализован |
| `IInventoryService` | `InventoryService` | Modules.Inventory | ✅ Реализован |
| `IStorageService` | `StorageService` (Spirit) | Modules.Inventory | ✅ Реализован |
| `ICraftingService` | `CraftingService` | Modules.Inventory | ✅ Реализован |
| `IEquipmentService` | `EquipmentService` | Modules.Inventory | ✅ Реализован |
| `ICombatService` | `CombatService` | Modules.Combat | ✅ Реализован |
| `IDamageService` | `DamageService` | Modules.Combat | ✅ Реализован |
| `INPCService` | `NPCService` | Modules.NPC | ✅ Реализован |
| `INPCSpawnerService` | `NPCSpawnerService` | Modules.NPC | ✅ Реализован |
| `IPlayerService` | `PlayerService` | Modules.Player | ✅ Реализован |
| `IPlayerInputService` | `PlayerInputService` | Modules.Player | ✅ Реализован |
| `IWorldService` | `WorldService` | Modules.World | ✅ Реализован |
| `IEventService` | `EventService` | Modules.World | ✅ Реализован |
| `IQuestService` | `QuestService` | Modules.Quest | ✅ Реализован |
| `IQuestRewardService` | `QuestRewardService` | Modules.Quest | ✅ Реализован |
| `IInteractionService` | `InteractionService` | Modules.Interaction | ✅ Реализован |
| `IDialogueService` | `DialogueService` | Modules.Interaction | ✅ Реализован |
| `IUIService` | `UIService` | Modules.UI | ✅ Реализован |
| `ISaveService` | `SaveService` | Modules.Save | ✅ Реализован |

### Анти-паттерны (ЗАПРЕЩЕНЫ в новом коде)

```csharp
// ❌ Singleton
public static MyManager Instance { get; private set; }

// ❌ ServiceLocator
var mgr = ServiceLocator.Get<CombatManager>();

// ❌ FindFirstObjectByType / FindObjectOfType
var ctrl = FindFirstObjectByType<PlayerController>();

// ❌ GameObject.Find
var obj = GameObject.Find("Player");

// ❌ Кросс-модульная прямая ссылка
[Inject] ChargerService _charger; // Нужен IChargerService!

// ❌ Прямой вызов обработчика другого модуля
bodyService.ApplyDamage(part, dmg); // Из CombatModule — только через MessagePipe!

// ✅ DI через интерфейс ядра
[Inject] IChargerService _chargerService;

// ✅ MessagePipe для межмодульной связи
[Inject] IPublisher<BodyPartDamagedEvent> _damagePub;
```

---

## 5. Шина сообщений: MessagePipe

### Принцип: Publish/Subscribe через Core.Messaging контракты

Все контракты — `readonly struct` (нулевая GC-аллокация).

### Реестр контрактов (20 файлов)

| Файл | Контракты | Домен |
|------|-----------|-------|
| GameContracts.cs | `GameStateChangedEvent`, `GamePausedEvent`, `GameResumedEvent` | Игра |
| CombatContracts.cs | `CombatStartedEvent`, `CombatEndedEvent`, `DamageAppliedEvent`, `TechniqueUsedEvent`, `EnemyKilledEvent` | Бой |
| BodyContracts.cs | `BodyPartDamagedEvent`, `BodyPartSeveredEvent`, `BodyPartHealedEvent`, `BodyPartReattachedEvent`, `BodyCriticalEvent` | Тело |
| QiContracts.cs | `QiChangedEvent`, `QiDepletedEvent`, `QiFullEvent`, `CultivationBreakthroughEvent`, `CultivationLevelChangedEvent`, `QiBufferActivatedEvent`, `QiBufferDeactivatedEvent`, `QiConsumeRequestEvent`, `QiAddRequestEvent`, `QiBufferActivateRequestEvent`, `QiBufferDeactivateRequestEvent`, `QiBufferStateChangedEvent` | Ци |
| BuffContracts.cs | `BuffAppliedEvent`, `BuffRemovedEvent`, `BuffExpiredEvent`, `BuffTickedEvent`, `StatModifierChangedEvent` | Баффы |
| ChargerContracts.cs | `ChargerStateChangedEvent`, `ChargerOverheatedEvent`, `ChargerCooledDownEvent`, `ChargerHeatChangedEvent`, `ChargerBufferChangedEvent` | Зарядник |
| TileContracts.cs | `TileChangedEvent`, `ResourceHarvestedEvent`, `ResourceDepletedEvent`, `TileMapGeneratedEvent`, `ResourceRespawnedEvent` + `HarvestResult` | Тайлы |
| InventoryContracts.cs | `ItemAddedEvent`, `ItemRemovedEvent`, `EquipmentChangedEvent`, `EquipmentBlockedEvent`, `ItemAddRequestEvent` | Инвентарь |
| PlayerContracts.cs | `PlayerDeathEvent`, `PlayerReviveEvent`, `PlayerSleepEvent`, `PlayerPositionChangedEvent` | Игрок |
| WorldContracts.cs | `TimeChangedEvent`, `DayChangedEvent`, `TimeSpeedChangedEvent`, `SceneTransitionRequest`, `SceneLoadedEvent`, `MonthChangedEvent`, `YearChangedEvent`, `LocationChangedEvent`, `TravelStartedEvent`, `WorldEventTriggeredEvent`, `WorldEventEndedEvent` | Мир |
| NPCContracts.cs | `NPCSpawnedEvent`, `NPCDespawnedEvent`, `AttitudeChangedEvent`, `NPCDeathEvent`, `NPCInteractedEvent`, `NPCAIStateChangedEvent`, `NPCDamagedEvent` | NPC |
| FormationContracts.cs | `FormationActivatedEvent`, `FormationDeactivatedEvent`, `FormationQiPoolChangedEvent`, `FormationStageChangedEvent`, `FormationContributeQiRequestEvent` | Формации |
| QuestContracts.cs | `QuestStartedEvent`, `QuestObjectiveUpdatedEvent`, `QuestCompletedEvent`, `QuestFailedEvent`, `QuestAbandonedEvent`, `QuestRewardGrantedEvent` | Квесты |
| SaveContracts.cs | `SaveRequestedEvent`, `LoadRequestedEvent`, `SaveCompletedEvent`, `LoadCompletedEvent` | Сохранение |
| DialogueContracts.cs | `DialogueStartedEvent`, `DialogueEndedEvent`, `DialogueChoiceSelectedEvent`, `InteractionCompletedEvent` | Диалоги |
| StatContracts.cs | `StatChangedEvent` | Характеристики |
| CraftingContracts.cs | `CraftCompletedEvent`, `CraftFailedEvent` | Крафт |
| UIContracts.cs | `UIStateChangeRequestEvent`, `UIInteractRequestEvent`, `UIAdvanceDialogueRequestEvent`, `UISelectChoiceRequestEvent`, `UISaveRequestEvent`, `UILoadRequestEvent`, `UIPauseRequestEvent`, `UIResumeRequestEvent`, `ToastShownEvent`, `ModalShownEvent` | UI |
| SceneContracts.cs | `SceneInitializingEvent`, `ScenePhaseStartedEvent`, `ScenePhaseCompletedEvent`, `SceneReadyEvent`, `SceneAssemblyFailedEvent`, `SceneAssemblyCompletedWithErrorsEvent` | Сцена |
| InputLogContracts.cs | `InputKeyEvent`, `InputActionEvent` + `InputKeyEventType` enum | Ввод |

> **EVT Command Events:** QiConsumeRequestEvent, QiAddRequestEvent, QiBufferActivateRequestEvent, QiBufferDeactivateRequestEvent, QiBufferStateChangedEvent, ItemAddRequestEvent — командные события (request→response паттерн) для развязки модулей.

### Пример: Межмодульное взаимодействие

**Проблема:** TileModule публикует `ResourceHarvestedEvent`, но нужно добавить предмет в инвентарь (InventoryModule).

**Решение:** TileModule НЕ знает об InventoryModule. Вместо этого:
1. TileModule публикует `ResourceHarvestedEvent`
2. InventoryModule (когда будет реализован) подпишется на `ResourceHarvestedEvent`
3. InventoryModule сам вызовет `IInventoryService.TryAddItem()`

```
TileModule ──publish──▶ ResourceHarvestedEvent ──subscribe──▶ InventoryModule
    │                                                              │
    └── IResourceService.Harvest()              IInventoryService.TryAddItem()
```

### Циркулярные зависимости: решение через события

**Пример из Фазы 2:** ResourceService нужен ITileService для обновления тайла после респауна, а TileMapService (ITileService) зависит от IResourceService.

**Решение:** `ResourceRespawnedEvent`
- ResourceService публикует `ResourceRespawnedEvent` при респауне
- TileMapService подписывается на `ResourceRespawnedEvent` и обновляет тайл
- Никакой циркулярной зависимости!

---

## 6. Интерфейсы ядра (Core/Interfaces/)

### Реестр интерфейсов

| Интерфейс | Методы | Статус |
|-----------|--------|--------|
| `ITimeService` | DeltaTime, TotalTime, CurrentDay/Hour, CurrentMonth, CurrentYear, TimeOfDay, Speed, Pause/Resume | ✅ Реализован (WorldModule) |
| `IQiService` | EntityId, CurrentQi, MaxQi, QiRatio, IsEmpty, IsFull, TryConsumeQi, AddQi, Regenerate, CultivationLevel, SubLevel, CoreQuality, CoreCapacity, QiDensity, EffectiveQi, Conductivity, ConductivityBonus, SetConductivityBonus, CanBreakthrough, CalculateBreakthroughRequirement, TryBreakthrough, SetCultivationLevel | ✅ Реализован |
| `IQiBufferService` | IsActive, Mode, QiInvested, Activate, Deactivate, AbsorbDamage | ✅ Реализован |
| `IBuffService` | ApplyBuff, RemoveBuff, RemoveAllBuffs, HasBuff, GetStatModifier, GetElementResistance, HasImmunity, GetActiveBuffs, TickBuffs | ✅ Реализован |
| `IStatService` | GetStat, GetStatBonus, ModifyStat, SetStat, GetStatDomain, GetVirtualDelta, AddVirtualDelta, ConsolidateSleep, GetThreshold, CanAdvance | ✅ Реализован (StatService) |
| `IBodyService` | EntityId, GetPartState, IsPartSevered, IsPartDisabled, GetPartHealthRatio, ApplyDamage, HealPart, IsSlotBlocked, GetAllParts, Initialize, ProcessRegeneration, RecalculateHPFromVitality, ReattachPart, GetMorphology, GetSizeClass | ✅ Реализован |
| `IChargerService` | IsOperational, HeatLevel, UseQiForTechnique, EnterCombat, Tick | ✅ Реализован |
| `ICombatService` | IsInCombat, CurrentStage, CurrentTargetId, StartCombat, EndCombat, ExecuteAttack, ExecuteDefense | ✅ Реализован |
| `IDamageService` | CalculateDamage, ApplyDefense | ✅ Реализован |
| `IInventoryService` | TryAddItem, TryRemoveItem, GetItemCount, GetAllSlots | ✅ Реализован |
| `IStorageService` | TryStore, TryRetrieve, GetStoredItems | ✅ Реализован |
| `ICraftingService` | CanCraft, TryCraft | ✅ Реализован |
| `IEquipmentService` | GetEquipped, TryEquip, TryUnequip, IsSlotBlocked, GetTotalArmor, GetTotalDamage | ✅ Реализован |
| `ITileService` | GetTile, SetTile, TryHarvest, IsWalkable | ✅ Реализован |
| `IResourceService` | TrySpawnResource, TryPickup, Harvest, RegisterDepletedResource | ✅ Реализован |
| `INPCService` | GetNPC, GetNearbyNPCIds, GetAttitude, ModifyAttitude, IsAlive, GetAIState, GetAllNPCIds, SetAIState, UpdatePosition | ✅ Реализован |
| `INPCSpawnerService` | SpawnNPC, DespawnNPC, GetSpawnedNPCIds, ActiveNPCCount | ✅ Реализован |
| `IPlayerService` | PlayerId, Position, IsAlive, IsSleeping, SleepState, Stance, StartSleep, WakeUp, SetPosition, GetAssignedTechniques, Tick | ✅ Реализован |
| `IPlayerInputService` | MoveDirection, RunHeld, IsAttackPressed, IsDefendPressed, IsInteractPressed, IsInventoryPressed, IsMeditatePressed, SelectedTechniqueSlot, InputDisabled, UpdateInputState, ResetFrameFlags | ✅ Реализован |
| `IWorldService` | CurrentLocationId, CurrentSectorId, TryTravel, GetLocation, GetFaction, GetFactionRelation, GetDiscoveredSectors, IsSectorDiscovered | ✅ Реализован |
| `IEventService` | TriggerWorldEvent, IsEventActive, GetActiveEvents, EndWorldEvent | ✅ Реализован |
| `IQuestService` | StartQuest, AbandonQuest, CompleteQuest, FailQuest, GetActiveQuestIds, IsQuestComplete, GetQuestStatus, QuestExists, GetQuestType | ✅ Реализован |
| `IQuestRewardService` | GrantRewards, AreRewardsGranted | ✅ Реализован |
| `IInteractionService` | GetNearestInteractableId, TryInteract | ✅ Реализован |
| `IDialogueService` | StartDialogue, AdvanceDialogue, SelectChoice, EndDialogue, IsInDialogue, CurrentDialogueId | ✅ Реализован |
| `IUIService` | CurrentUIState, SetUIState, ShowToast, ShowModal | ✅ Реализован |
| `IFormationService` | IsFormationActive, ActiveFormationId, CurrentStage, StartDrawing, StartFilling, ContributeQi, ActivateFormation, DeactivateFormation, GetFormationBonus, QiPoolCurrent, QiPoolMax, ParticipantCount, CasterId, GetActiveEffects | ✅ Реализован |
| `ISaveService` | Save, Load, HasSave, DeleteSave, GetAllSaves | ✅ Реализован |
| `ISaveable` | SaveKey, CaptureState, RestoreState | ✅ Реализован |
| `ISceneAssemblyPhase` | PhaseName, PhaseOrder, ExecuteAsync | ✅ Реализован |

### Дополнительные типы в интерфейсах

| Тип | Файл | Описание |
|-----|------|----------|
| `BodyPartData` | IBodyService.cs | readonly struct — данные части тела (+BodyPartFunction Functions, float BaseHitChance) |
| `IBodyFactory` | Modules.Body/IBodyFactory.cs | interface — фабрика создания тел (для тестируемости, P1-10 FIX) |
| `DamageRequest` | ICombatService.cs | readonly struct — запрос на урон |
| `DamageResult` | ICombatService.cs | readonly struct — результат урона |
| `DefenseContext` | ICombatService.cs | readonly struct — контекст защиты |
| `QiBufferMode` | IQiService.cs | enum — режим Ци-буфера |
| `StorageType` | IInventoryService.cs | enum — тип хранилища |
| `SaveInfo` | ISaveService.cs | readonly struct — инфо о сохранении |
| `ChargerMode` | IChargerService.cs | enum — режим зарядника |
| `HarvestResult` | TileContracts.cs | readonly struct — результат сбора |
| `ActiveBuffData` | IBuffService.cs | readonly struct — данные активного баффа |
| `NPCData` | INPCService.cs (Core.Data) | class — данные NPC |
| `NPCState` | NPCState.cs (Modules.NPC.Data) | class — runtime-состояние NPC |
| `NPCAIState` | Enums.cs | enum — AI-состояние NPC |
| `NPCRole` | Enums.cs | enum — роль NPC |
| `AttitudeChangedEvent` | NPCContracts.cs | readonly struct |
| `NPCDeathEvent` | NPCContracts.cs | readonly struct |
| `NPCAIStateChangedEvent` | NPCContracts.cs | readonly struct |
| `NPCDamagedEvent` | NPCContracts.cs | readonly struct |
| `NPCInteractedEvent` | NPCContracts.cs | readonly struct |
| `PlayerSleepState` | Enums.cs | enum — состояния сна (Awake, FallingAsleep, Sleeping, WakingUp) |
| `PlayerStance` | Enums.cs | enum — боевая стойка (Normal, Combat, Meditating, Sleeping) |
| `PlayerData` | PlayerData.cs (Modules.Player.Data) | class — runtime-состояние игрока |
| `QuestType` | Enums.cs | enum — тип квеста |
| `QuestStatus` | Enums.cs | enum — статус квеста |
| `QuestObjectiveType` | Enums.cs | enum — тип цели квеста |
| `QuestRewardType` | Enums.cs | enum — тип награды за квест |
| `GameState` | Enums.cs | enum — состояние игры |
| `UIState` | Modules.UI.Data/UIState.cs | enum — состояние UI |
| `InteractionCompletedEvent` | DialogueContracts.cs | readonly struct — завершение взаимодействия |
| `SaveSlotData` | SaveSlotData.cs (Modules.Save.Data) | class — данные слота сохранения |
| `AutoSaveConfig` | AutoSaveConfig.cs (Modules.Save.Data) | class — конфигурация автосохранения |
| `StatType` | StatType.cs (Core.Data) | enum — типы характеристик |
| `SceneAssemblyConfig` | SceneAssemblyConfig.cs (Entry) | class — конфигурация сборки сцены |
| `SaveModuleServices` | SaveModuleServices.cs (Modules.Save) | class — ModuleServices для Save |
| `SceneAssemblyPhase` | ISceneAssemblyPhase.cs (Core.Interfaces) | interface — фаза сборки сцены |
| `BodyPartFunction` | Enums.cs (Core.Data) | [Flags] enum — функции части тела (Sensory, Breathing, Circulation, ...) |
| `SizeClass` | Enums.cs (Core.Data) | enum — класс размера сущности (Tiny..Colossal) |
| `VitalityScalingMode` | Enums.cs (Core.Data) | enum — режим масштабирования HP от Vitality |
| `StatDomain` | Enums.cs (Core.Data) | enum — домен характеристики (Body, Soul) |
| `BodyPartTemplate` | BodyPartTemplate.cs (Core.Data) | sealed class — шаблон части тела |
| `BodyTemplate` | BodyTemplate.cs (Core.Data) | sealed class — шаблон тела (композиция) |
| `SpeciesData` | SpeciesData.cs (Core.Data) | sealed class — данные вида (11 видов) |
| `SeveredDebuffDef` | SeveredDebuffSystem.cs (Modules.Body) | readonly struct — определение дебаффа от ампутации |
| `BodyCriticalEvent` | BodyContracts.cs (Core.Messaging) | readonly struct — критическое состояние vital-части (P2-07 FIX) |
| `CultivationLevelChangedEvent` | QiContracts.cs (Core.Messaging) | readonly struct — изменение уровня культивации (P1-14 FIX) |

---

## 7. Шаблон модуля

Каждый модуль следует единому шаблону:

### Структура модуля

```
Modules/Xxx/
├── XxxModule.cs           # Точка входа (IStartable, ITickable)
├── XxxLifetimeScope.cs    # DI-конфигуратор (наследует ModuleLifetimeScope)
├── XxxModuleServices.cs   # ModuleServices — регистрация внутренних сервисов (Фаза 17)
├── XxxService.cs          # Реализация IXxxService
├── XxxConfig.cs           # Конфигурация (class, не struct — BD-48)
└── XxxHelper.cs           # Вспомогательные классы
```

### XxxModule.cs (шаблон)

```csharp
public class XxxModule : IStartable, ITickable
{
    [Inject] private readonly IXxxService _service;
    // [Inject] дополнительные зависимости через интерфейсы Core

    private XxxConfig _config;
    private bool _isConfigured;

    public void SetConfig(XxxConfig config)
    {
        _config = config;
        _isConfigured = true;
    }

    void IStartable.Start()
    {
        // Configure через конкретный тип — допустимо в пределах модуля
        if (_isConfigured && _service is XxxService service)
            service.Configure(_config);
    }

    void ITickable.Tick()
    {
        // Кадровое обновление через интерфейс — без каста
        _service.Tick(); // Если Tick() в интерфейсе
    }
}
```

### XxxLifetimeScope.cs (шаблон)

```csharp
public class XxxLifetimeScope : ModuleLifetimeScope
{
    public override string ModuleName => "Xxx";

    protected override void Configure(IContainerBuilder builder)
    {
        // Регистрация сервиса
        builder.Register<IXxxService, XxxService>(Lifetime.Singleton);

        // Регистрация точки входа
        builder.Register<XxxModule>(Lifetime.Singleton)
            .AsImplementedInterfaces();

        // Конфигурация по умолчанию
        var defaultConfig = new XxxConfig { ... };

        builder.RegisterBuildCallback(container =>
        {
            var module = container.Resolve<XxxModule>();
            module.SetConfig(defaultConfig);
        });
    }
}
```

### XxxModuleServices.cs (ModuleServices pattern — Фаза 17)

Каждый модуль имеет `XxxModuleServices.cs` — централизованная регистрация внутренних сервисов модуля:

```csharp
public class XxxModuleServices
{
    public static void Register(IContainerBuilder builder)
    {
        // Регистрация внутренних сервисов модуля
        builder.Register<XxxCalculator>(Lifetime.Singleton);
        builder.Register<XxxHelper>(Lifetime.Singleton);
        // ... и т.д.
    }
}
```

> **MSV-01 (Фаза 17):** ModuleServices выносит регистрацию внутренних сервисов из LifetimeScope,
> делая его компактным (только интерфейс + Module + ModuleServices). Это упрощает тестирование
> и модификацию — новые внутренние сервисы добавляются только в XxxModuleServices.Register().

### Уроки из реализованных фаз

| Урок | Фаза | Описание |
|------|-------|----------|
| CH-32/33 | 1 | Без реализаций интерфейсов VContainer не создаст сервисы → нужны stub |
| CH-34 | 1 | В Unity Editor нужно установить parent = GameLifetimeScope |
| CH-04 | 1 | Tick() через интерфейс, Configure() только внутри модуля (без циклической зависимости) |
| BD-42 | 3 | ITimeService.DeltaTime вместо UnityEngine.Time.deltaTime |
| BD-48 | 3 | Config — class, не struct (mutable struct risk) |
| FIX-1 | 2 | DI-каст устранён: Harvest() добавлен в IResourceService |
| FIX-2 | 2 | ResourceRespawnedEvent решает циркулярную зависимость |
| QI-A05 | 4 | Отслеживать потреблённый Ци в буфере для корректного возврата при Deactivate |
| QI-A04 | 4 | Не перезаписывать _coreCapacity после прорыва (breakthrough) |
| QI-A01 | 4 | Удалять мёртвые методы интерфейса (unused interface methods) |
| QI-C01 | 4 | Подписка на кросс-модульные события (BodyPartSeveredEvent → QiBufferService) |
| BF-A01 | 5 | Формула CalculateStatModifier должна точно совпадать с документацией (конвенция percentSum) |
| BF-A03 | 5 | Маппинг иммунитетов требует словарь Effect→Immunity |
| INV-01 | 6 | EquipmentService НЕ ссылается на BodySlotMapping — использует BodyPartSeveredEvent.BlockedSlots (Hub-and-Spoke) |
| INV-02 | 6 | SpiritStorage + StorageRing унифицированы в StorageService с параметром StorageType |
| INV-03 | 6 | EquipmentController God Object (1418 LOC) разбит на EquipmentService + EquipmentValidator + EquipmentStatAggregator |
| INV-04 | 6 | EquipmentService НЕ инжектит IBodyService — VContainer sibling scopes не видят регистрации друг друга. Использует событийную модель (BodyPartSeveredEvent → кэш заблокированных слотов) |
| NPC-A07 | 9 | NPCService.GetAllNPCIds/SetAIState/UpdatePosition добавлены в интерфейс (баг: отсутствовали в INPCService) |
| NPC-B04 | 9 | NPCCombatAdapter подписывается на CombatStartedEvent/CombatEndedEvent — адаптер боя через MessagePipe (не прямая ссылка) |
| NPC-B05 | 9 | NPCRelationshipService подписывается на DayChangedEvent для затухания отношений (Hub-and-Spoke, не прямая зависимость) |
| NPC-A12 | 9 | NPCSpawnerService.ActiveNPCCount добавлен в INPCSpawnerService (баг: отсутствовал метод) |
| PLR-A01 | 10 | Устранена двойная публикация PlayerSleepEvent (SleepService публикует при переходах, PlayerService НЕ дублирует) |
| PLR-E02 | 10 | WakeUp() публикует start-событие при прерывании FallingAsleep (парность событий) |
| PLR-E03 | 10 | QuickSleep() добавлена проверка состояния (только из Awake) |
| PLR-E06 | 10 | ResetFrameFlags() вызывается из PlayerModule.Tick() ПОСЛЕ всех потребителей, НЕ внутри PlayerService.Tick() |
| PLR-E09 | 10 | QuickSleep() публикует оба события (start + end) для парности |
| НОВ-ИГР-01 | 10 | Устранена двойная регенерация HP во сне (ProcessFinalHPRecovery удалена) |
| БАГ-НИП-06 | 10 | FallingAsleep использует deltaTime вместо coroutine |
| НОВ-СХР-08 | 10 | InputDisabled сбрасывает состояние ввода (аналог ResetActions) |
| PLR-GOD | 10 | PlayerController God Object (1425 LOC, 14+ зависимостей) разбит на 6 сервисов + PlayerConfig + PlayerData |
| WLD-A01 | 11 | TimeService переезжает из Stub в WorldModule — ITimeService теперь имеет реальную реализацию с CurrentMonth/CurrentYear/TimeOfDay |
| WLD-A02 | 11 | WorldService не инжектит IPlayerService — использует LocationChangedEvent для связи с Player |
| WLD-B01 | 11 | EventService подписывается на TimeChangedEvent для триггера периодических мировых событий |
| WLD-C01 | 11 | FactionService.FactionRelation через readonly struct, не enum — расширяемость |
| QST-A01 | 12 | QuestService расширен: CompleteQuest, FailQuest, GetQuestStatus, QuestExists, GetQuestType добавлены в IQuestService (9 методов) |
| QST-A02 | 12 | QuestRewardService выделен из QuestService — SRP, отдельный интерфейс IQuestRewardService |
| QST-B01 | 12 | QuestProgressTracker подписывается на кросс-модульные события (EnemyKilledEvent, ItemAddedEvent) через MessagePipe |
| QST-C01 | 12 | QuestRewardGrantedEvent публикуется после выдачи наград — UI и SaveModule подписываются |
| INT-A01 | 13 | InteractionService.GetNearestInteractableId использует IWorldService.CurrentLocationId для контекста |
| INT-A02 | 13 | DialogueService расширен: IsInDialogue, CurrentDialogueId добавлены в IDialogueService |
| INT-B01 | 13 | DialogueTypewriter — отдельный класс, не встроен в DialogueService (SRP) |
| INT-C01 | 13 | InteractionCompletedEvent публикуется по завершении взаимодействия — подписчики QuestModule, NPCModule |
| UI-A01 | 14 | UIModule реализован — все «UI (будущее)» заменены на реальные подписки через UIModule ✅ |
| UI-A02 | 14 | UIService.CurrentUIState / SetUIState — центральное управление состоянием UI |
| UI-B01 | 14 | ToastService — отдельный сервис от UIService (SRP: уведомления vs. управление) |
| UI-C01 | 14 | HUDPresenter / DialoguePresenter — презентеры, не MonoBehaviours (чистый C# + MessagePipe) |
| UI-D01 | 14 | StubUIService был fallback до реализации UIModule — удалён после Phase 14 |
| SAV-01 | 15 | SaveService реализует ISaveService — SaveFileHandler (I/O) + SaveDataAggregator (сбор от ISaveable) |
| SAV-02 | 15 | SaveModule НЕ имеет SaveLifetimeScope — использует SaveModuleServices для регистрации |
| SAV-03 | 15 | SaveDataAggregator подписывается на SaveRequestedEvent/LoadRequestedEvent — Hub-and-Spoke |
| SCN-01 | 16 | SceneOrchestrator управляет последовательной сборкой через 11 фаз (ISceneAssemblyPhase) |
| SCN-02 | 16 | RuntimeSceneBuilder создаёт объекты программно (без Scene prefab) |
| SCN-03 | 16 | SceneAssemblyRegistrar регистрирует фазы в SceneOrchestrator — открытый список фаз |
| SCN-04 | 16 | SceneContracts.cs — контракты сборки сцены (SceneAssemblyStartedEvent и др.) |
| MIN-01 | 17 | ModuleServices pattern — XxxModuleServices.cs выносит регистрацию из LifetimeScope |
| MIN-02 | 17 | Все 15 модулей получили XxxModuleServices.cs — единообразная структура |
| SES-01 | 18 | GameSession управляет жизненным циклом сессии (new game / load / pause / quit) |
| SES-02 | 18 | GameSession подписывается на GamePausedEvent/GameResumedEvent — Hub-and-Spoke |
| EUI-01 | 19 | Entry/UI — 22 view (HUDPanelView, HotbarPanelView, BuffBarView, ToastView, MiniMapView, DialoguePanelView, PausePanelView, CombatOverlayView, DeathScreenView, LoadingScreenView, CharacterPanelView, TechniqueChargeView, CombatLogView, TurnOrderView, DamageNumberView, EnemyHealthBarView, InputLogPanel, NPCInspectorPanel, ContextMenuUI, GameInputAdapter, UIComponentResolver, DraggableWindow) |
| EUI-02 | 19 | GameInputAdapter преобразует Unity Input → IPlayerInputService (чистый C#) |
| CLR-01 | 19 | Все stub-сервисы удалены, кроме StubStatService — единственный оставшийся stub |
| UIA-01 | 06_17 | WireUIViews использует FindFirstObjectByType(FindObjectsInactive.Include) — находит деактивированные View |
| UIA-02 | 06_17 | CreateOverlayView + CanvasGroup blocksRaycasts=false (4.3) — stretch-fill корень не перехватывает клики |
| UIF-01 | 06_18 | UIFontCache static init + fallback на LegacyRuntime.ttf — EnsureStaticInitialized() логирует TMP статус |
| UIF-02 | 06_18 | CreateText + ContentSizeFitter + LayoutElement — текст в VLG получает preferredSize (фикс size=0x0) |
| UIF-03 | 06_18 | CreateInventoryScreen без прямого scope.Container.Inject — через UIComponentResolver в Start() |
| UIF-04 | 06_18 | MiniMapView V3 — anchor (1,1), pivot (1,1), BuildUI с try/catch, ToggleVisibility robust, N-key |

---

## 8. Стаб-сервисы (Entry/Stubs/)

Стаб-сервисы обеспечивают минимальную реализацию интерфейсов для работы DI.

> **⚠️ ВНИМАНИЕ (аудит 06_17):** В `Entry/Stubs/` физически находятся **19 stub-файлов**, но `FallbackRegistrar` не вызывается с Phase 17B (flat single-root регистрация). Stubs фактически мёртвые — `CoreValidationPhase` сравнивает типы со строками "StubXxxService", но stubs больше не регистрируются. `StrictBootMode` фактически не работает.
>
> **Рекомендация (Phase D):** Удалить 19 мёртвых stubs + `FallbackRegistrar` (637 LOC).

| Stub | Интерфейс | Поведение | Статус |
|------|-----------|-----------|--------|
| `StubStatService` | `IStatService` | Constitution = 10, остальные = 0 | ⚠️ Мёртвый (FallbackRegistrar не вызывается) |
| `StubBodyService` | `IBodyService` | — | ⚠️ Мёртвый |
| `StubBuffService` | `IBuffService` | — | ⚠️ Мёртвый |
| `StubChargerService` | `IChargerService` | — | ⚠️ Мёртвый |
| `StubDialogueService` | `IDialogueService`/`IInteractionService` | — | ⚠️ Мёртвый |
| `StubEquipmentService` | `IEquipmentService` | — | ⚠️ Мёртвый |
| `StubFormationService` | `IFormationService` | — | ⚠️ Мёртвый |
| `StubInventoryService` | `IInventoryService` | — | ⚠️ Мёртвый |
| `StubNPCService` | `INPCService` | — | ⚠️ Мёртвый |
| `StubPlayerService` | `IPlayerService` | — | ⚠️ Мёртвый |
| `StubQiService` | `IQiService` | — | ⚠️ Мёртвый |
| `StubQiBufferService` | `IQiBufferService` | — | ⚠️ Мёртвый |
| `StubQuestService` | `IQuestService` | — | ⚠️ Мёртвый |
| `StubResourceService` | `IResourceService` | — | ⚠️ Мёртвый |
| `StubStatService` | `IStatService` | — | ⚠️ Мёртвый (дубль) |
| `StubTileService` | `ITileService` | — | ⚠️ Мёртвый |
| `StubTimeService` | `ITimeService` | — | ⚠️ Мёртвый |
| `StubUIService` | `IUIService` | — | ⚠️ Мёртвый |
| `StubWorldService` | `IWorldService` | — | ⚠️ Мёртвый |

> **CLR-01 (Фаза 19, исторически):** Все stub-сервисы были удалены, кроме StubStatService.
> **Phase 17B:** Перешли на flat single-root регистрацию (ModuleServices pattern), FallbackRegistrar перестал вызываться.
> **Аудит 06_17:** 19 stub-файлов физически присутствуют, но мёртвые — не регистрируются, не инжектятся.

---

## 9. Стандарты кода

### Naming Conventions

| Элемент | Стиль | Пример |
|---------|-------|--------|
| Классы | PascalCase | `ChargerService` |
| Интерфейсы | IPascalCase | `IChargerService` |
| Методы | PascalCase | `TryHarvest()` |
| Поля (private) | _camelCase | `_activeSlots` |
| Поля (Inject) | _camelCase | `[Inject] IChargerService _chargerService;` |
| Свойства | PascalCase | `IsOperational` |
| Константы | UPPER_SNAKE | `MAX_STAT_VALUE` |
| Контракты | PascalCase + Event | `ChargerStateChangedEvent` |
| Enum | PascalCase | `HeatState.Cool` |
| Namespace | PascalCase | `CultivationGame.Modules.Charger` |

### Комментарии

```csharp
// Создано: 2026-05-09 04:27:00 UTC
// Редактировано: 2026-05-09 04:27:00 UTC — описание изменения

// Ссылки на баг-фиксы:
// Ф1-01: Empty catch → Debug.LogWarning + контекст
// БАГ-КОР-12: Overflow tracking for VFXPool
// BD-42: ITimeService.DeltaTime вместо UnityEngine.Time
// FIX-2: ResourceRespawnedEvent для респауна ресурсов
```

**⚠️ Комментарии ТОЛЬКО на русском языке** (кроме имён классов/методов/переменных).

### Критические правила

1. **НЕ создавать .meta файлы** — Unity генерирует автоматически
2. **ScriptableObject.OnEnable()** — НЕ virtual, без `override`
3. **Контракты** — ТОЛЬКО `readonly struct` (нулевая GC)
4. **Config классы** — `class`, НЕ `struct` (BD-48: mutable struct risk)
5. **Межмодульное взаимодействие** — ТОЛЬКО через Core интерфейсы или MessagePipe
6. **DI-каст** — допустим только ВНУТРИ модуля (`_service is XxxService`)
7. **Tick() в интерфейсе** — если модулю нужен Tick, добавлять в интерфейс (CH-04)
8. **Configure() НЕ в интерфейсе** — чтобы не создавать Core→Modules зависимость (CH-04)

---

## 10. Assembly Definition

**CultivationGame.New.asmdef** — единая сборка для нового кода.

References: `VContainer`, `MessagePipe`, `MessagePipe.VContainer`, `UniTask`, `Unity.InputSystem`, `Unity.TextMeshPro`

⚠️ Использовать **имена сборок** (assembly names), НЕ имена пакетов!
- ✅ `VContainer` (не `jp.hadashikick.vcontainer`)
- ✅ `MessagePipe` (не `com.cysharp.messagepipe`)
- ✅ `UniTask` (не `com.cysharp.unitask`)

---

## 11. Зависимости модулей (карта)

### Реализованные модули (Фазы 0-19)

```
                    ┌──────────────────────────────────────────┐
                    │              CORE                         │
                    │                                           │
                    │  IChargerService ←─────── ChargerModule   │
                    │  ITimeService   ←─────── ChargerModule    │
                    │                                           │
                    │  ITileService   ←─────── TileModule       │
                    │  IResourceService ←────── TileModule       │
                    │  IInventoryService ←───── ResourceService │
                    │                                           │
                    │  IBodyService   ←─────── BodyModule       │
                    │  ITimeService   ←─────── BodyModule       │
                    │                                           │
                    │  IWorldService  ←─────── WorldModule      │
                    │  ITimeService   ←─────── WorldModule      │
                    │  IEventService  ←─────── WorldModule      │
                    │                                           │
                    │  IQuestService  ←─────── QuestModule      │
                    │  IQuestRewardService ←─── QuestModule      │
                    │                                           │
                    │  IInteractionService ←─── InteractionModule│
                    │  IDialogueService ←───── InteractionModule│
                    │                                           │
                    │  IUIService     ←─────── UIModule         │
                    │                                           │
                    │  ISaveService   ←─────── SaveModule       │
                    │                                           │
                    │  ISceneAssemblyPhase ←── SceneOrchestrator │
                    │                                           │
                    └──────────────────────────────────────────┘
```

### Межмодульные связи (через MessagePipe)

| Событие | Издатель | Подписчик | Связь |
|---------|----------|-----------|-------|
| `ChargerStateChangedEvent` | ChargerModule | UIModule ✅ | Charger → UI |
| `ChargerOverheatedEvent` | ChargerModule | QiModule ✅ | Charger → Qi |
| `ResourceHarvestedEvent` | TileModule | InventoryModule | Tile → Inventory ✅ |
| `ResourceDepletedEvent` | TileModule | UIModule ✅ | Tile → UI |
| `ResourceRespawnedEvent` | ResourceService | TileMapService | Tile ↔ Tile (внутренняя) |
| `BodyPartDamagedEvent` | BodyModule | CombatModule ✅ | Body → Combat |
| `BodyPartSeveredEvent` | BodyModule | EquipmentService (автоснятие) | Body → Equipment ✅ |
| `DayChangedEvent` | TimeService (WorldModule) ✅ | ResourceService (респаун), NPCRelationshipService | World → Tile/NPC ✅ |
| `MonthChangedEvent` | TimeService (WorldModule) | UIModule ✅, QuestModule | World → UI/Quest |
| `YearChangedEvent` | TimeService (WorldModule) | UIModule ✅, QuestModule | World → UI/Quest |
| `LocationChangedEvent` | WorldService | PlayerModule, NPCModule, UIModule ✅ | World → Player/NPC/UI |
| `TravelStartedEvent` | WorldService | UIModule ✅ | World → UI |
| `WorldEventTriggeredEvent` | EventService | UIModule ✅, QuestModule | World → UI/Quest |
| `WorldEventEndedEvent` | EventService | UIModule ✅ | World → UI |
| `QiChangedEvent` | QiModule | UIModule ✅ | Qi → UI |
| `QiDepletedEvent` | QiModule | CombatModule ✅ | Qi → Combat |
| `QiFullEvent` | QiModule | UIModule ✅ | Qi → UI |
| `CultivationBreakthroughEvent` | QiModule | UIModule ✅, SaveModule ✅ | Qi → UI/Save |
| `QiBufferActivatedEvent` | QiBufferService | UIModule ✅ | Qi → UI |
| `QiBufferDeactivatedEvent` | QiBufferService | UIModule ✅ | Qi → UI |
| `BodyPartSeveredEvent` | BodyModule | QiBufferService (Deactivate) | Body → Qi |
| `BuffAppliedEvent` | BuffModule | UIModule ✅ | Buff → UI |
| `BuffRemovedEvent` | BuffModule | UIModule ✅ | Buff → UI |
| `BuffExpiredEvent` | BuffModule | UIModule ✅ | Buff → UI |
| `BuffTickedEvent` | BuffModule | UIModule ✅ | Buff → UI |
| `StatModifierChangedEvent` | BuffModule | StatModule (будущее) | Buff → Stat |
| `ItemAddedEvent` | InventoryService, StorageService | UIModule ✅ | Inventory → UI |
| `ItemRemovedEvent` | InventoryService, StorageService | UIModule ✅ | Inventory → UI |
| `EquipmentChangedEvent` | EquipmentService | UIModule ✅ | Inventory → UI |
| `EquipmentBlockedEvent` | EquipmentService | UIModule ✅ | Inventory → UI |
| `CraftCompletedEvent` | CraftingService | UIModule ✅ | Inventory → UI |
| `CraftFailedEvent` | CraftingService | UIModule ✅ | Inventory → UI |
| `NPCSpawnedEvent` | NPCSpawnerService | UIModule ✅ | NPC → UI |
| `NPCDespawnedEvent` | NPCSpawnerService | UIModule ✅ | NPC → UI |
| `AttitudeChangedEvent` | NPCService | UIModule ✅ | NPC → UI |
| `NPCDeathEvent` | NPCCombatAdapter | UIModule ✅, SaveModule ✅ | NPC → UI/Save |
| `NPCAIStateChangedEvent` | NPCService | UIModule ✅ | NPC → UI |
| `NPCDamagedEvent` | NPCCombatAdapter | UIModule ✅ | NPC → UI |
| `NPCInteractedEvent` | NPCService | InteractionModule ✅ | NPC → Interaction |
| `PlayerDeathEvent` | PlayerService | NPCModule, UIModule ✅, SaveModule ✅ | Player → NPC/UI/Save ✅ |
| `PlayerReviveEvent` | PlayerService | UIModule ✅, SaveModule ✅ | Player → UI/Save |
| `PlayerSleepEvent` | SleepService, PlayerService | QiModule, BodyModule, UIModule ✅ | Player → Qi/Body/UI ✅ |
| `PlayerPositionChangedEvent` | PlayerService | NPCAIService, NPCMovementService, PlayerVisualService | Player → NPC ✅ |
| `TechniqueUsedEvent` | PlayerCombatAdapter | CombatModule | Player → Combat ✅ |
| `QuestStartedEvent` | QuestService | UIModule ✅ | Quest → UI |
| `QuestObjectiveUpdatedEvent` | QuestProgressTracker | UIModule ✅ | Quest → UI |
| `QuestCompletedEvent` | QuestService | UIModule ✅, QuestRewardService | Quest → UI/Reward |
| `QuestFailedEvent` | QuestService | UIModule ✅ | Quest → UI |
| `QuestAbandonedEvent` | QuestService | UIModule ✅ | Quest → UI |
| `QuestRewardGrantedEvent` | QuestRewardService | UIModule ✅, SaveModule ✅ | Quest → UI/Save |
| `DialogueStartedEvent` | DialogueService | UIModule ✅ (DialoguePresenter) | Interaction → UI |
| `DialogueEndedEvent` | DialogueService | UIModule ✅ (DialoguePresenter) | Interaction → UI |
| `DialogueChoiceSelectedEvent` | DialogueService | QuestModule ✅ (цели квестов) | Interaction → Quest |
| `InteractionCompletedEvent` | InteractionService | QuestModule ✅, NPCModule, SaveModule ✅ | Interaction → Quest/NPC/Save |
| `SaveRequestedEvent` | GameSession, UIService | SaveModule ✅ | UI/Session → Save |
| `LoadRequestedEvent` | GameSession | SaveModule ✅ | Session → Save |
| `SaveCompletedEvent` | SaveService | GameSession ✅, UIModule ✅ | Save → Session/UI |
| `LoadCompletedEvent` | SaveService | GameSession ✅, UIModule ✅ | Save → Session/UI |
| `SceneAssemblyStartedEvent` | SceneOrchestrator | LoadingScreenView ✅ | Scene → UI |
| `SceneAssemblyPhaseCompletedEvent` | SceneOrchestrator | LoadingScreenView ✅ | Scene → UI |
| `SceneAssemblyCompletedEvent` | SceneOrchestrator | GameSession ✅, LoadingScreenView ✅ | Scene → Session/UI |
| `SceneAssemblyFailedEvent` | SceneOrchestrator | UIModule ✅ | Scene → UI |

### Внутримодульные зависимости

| Модуль | Зависит от Core | Внутренние связи |
|--------|----------------|------------------|
| Charger | IChargerService, ITimeService | ChargerModule → ChargerService (каст для Configure) |
| Tile | ITileService, IResourceService, IInventoryService | TileModule → TileMapService (каст для Initialize) |
| Body | IBodyService, ITimeService | BodyModule → BodyService (каст для Initialize) |
| Qi | IQiService, IQiBufferService, ISubscriber<BodyPartSeveredEvent> | QiModule → QiService (каст для Configure), QiBufferService подписывается на BodyPartSeveredEvent |
| Buff | IBuffService, IStatService | BuffModule → BuffService (каст для Configure), BuffService → BuffCalculator + BuffTickProcessor |
| Inventory | IInventoryService, IStorageService, ICraftingService, IEquipmentService, IBodyService, ISubscriber<ResourceHarvestedEvent, BodyPartSeveredEvent> | InventoryModule → InventoryService (каст для Configure), EquipmentService подписывается на BodyPartSeveredEvent, InventoryModule подписывается на ResourceHarvestedEvent |
| Combat | ICombatService, IDamageService, IQiService, IQiBufferService, IEquipmentService, IInventoryService, ISubscriber<EnemyKilledEvent, CombatEndedEvent, EquipmentChangedEvent, BuffAppliedEvent, BuffRemovedEvent> | CombatModule → CombatService (каст для Configure), DamageService → QiBufferService + EquipmentService (fallback stubs), CombatService → QiService + QiBufferService + TechniqueService. EVT-01: полная независимость через MessagePipe |
| Formation | IFormationService, ITimeService | FormationModule → FormationService (каст для Initialize), FormationService подписывается на QiChangedEvent, CombatEndedEvent, FormationContributeQiRequestEvent. FMT-A01: утечка через Tick() вместо TimeChangedEvent. EVT-01: полная независимость через MessagePipe |
| NPC | INPCService, INPCSpawnerService, ITimeService, ISubscriber<QiChangedEvent, DamageAppliedEvent, BodyPartSeveredEvent, PlayerPositionChangedEvent, CombatStartedEvent, CombatEndedEvent, DayChangedEvent> | NPCModule → NPCService (каст для Initialize), NPCSpawnerService → NPCRelationshipService (очистка при деспавне), NPCCombatAdapter подписывается на CombatStartedEvent/CombatEndedEvent/DamageAppliedEvent, NPCAIService подписывается на DamageAppliedEvent/BodyPartSeveredEvent/PlayerPositionChangedEvent, NPCMovementService подписывается на PlayerPositionChangedEvent, NPCRelationshipService подписывается на DayChangedEvent |
| Player | IPlayerService, IPlayerInputService, ITimeService, ISubscriber<QiDepletedEvent, CombatStartedEvent, CombatEndedEvent, DamageAppliedEvent, TimeChangedEvent, PlayerPositionChangedEvent, PlayerSleepEvent> | PlayerModule → PlayerService (каст для Initialize), PlayerService → SleepService + PlayerCombatAdapter + PlayerInputService, SleepService подписывается на TimeChangedEvent, PlayerCombatAdapter подписывается на CombatStartedEvent/CombatEndedEvent/DamageAppliedEvent, PlayerVisualService подписывается на PlayerPositionChangedEvent/PlayerSleepEvent. PLR-GOD: PlayerController (1425 LOC, 14+ зависимостей) → 6 сервисов. EVT-01: полная независимость через MessagePipe |
| World | IWorldService, ITimeService, IEventService, ISubscriber<TimeChangedEvent> | WorldModule → WorldService (каст для Initialize), TimeService публикует TimeChangedEvent/DayChangedEvent/MonthChangedEvent/YearChangedEvent, LocationService публикует LocationChangedEvent/TravelStartedEvent, FactionService управляет FactionRelation, EventService подписывается на TimeChangedEvent и публикует WorldEventTriggeredEvent/WorldEventEndedEvent |
| Quest | IQuestService, IQuestRewardService, ISubscriber<EnemyKilledEvent, ItemAddedEvent, InteractionCompletedEvent, DialogueChoiceSelectedEvent> | QuestModule → QuestService (каст для Initialize), QuestProgressTracker подписывается на кросс-модульные события, QuestRewardService публикует QuestRewardGrantedEvent, QuestService публикует QuestStartedEvent/QuestCompletedEvent/QuestFailedEvent/QuestAbandonedEvent |
| Interaction | IInteractionService, IDialogueService, ISubscriber<NPCInteractedEvent, PlayerPositionChangedEvent> | InteractionModule → InteractionService (каст для Initialize), InteractionService публикует InteractionCompletedEvent, DialogueService публикует DialogueStartedEvent/DialogueEndedEvent/DialogueChoiceSelectedEvent, DialogueTypewriter — отдельный класс |
| UI | IUIService, ISubscriber<30+ событий> | UIModule → UIService (каст для Initialize), UIService управляет UIState, ToastService обрабатывает ShowToast, HUDPresenter подписывается на CombatStarted/Ended, QiChanged, PlayerSleep и т.д., DialoguePresenter подписывается на DialogueStarted/Ended/ChoiceSelected |
| Save | ISaveService, ISubscriber<SaveRequestedEvent, LoadRequestedEvent, CultivationBreakthroughEvent, PlayerDeathEvent, NPCDeathEvent, QuestRewardGrantedEvent> | SaveModule → SaveService (каст для Initialize), SaveFileHandler (I/O файлов), SaveDataAggregator собирает данные от ISaveable. SAV-02: Нет SaveLifetimeScope — использует SaveModuleServices |
| SceneOrchestrator | ISceneAssemblyPhase, ISubscriber<SceneAssemblyStartedEvent> | SceneOrchestrator выполняет 11 фаз последовательно через ExecuteAsync. SceneAssemblyRegistrar регистрирует фазы, RuntimeSceneBuilder создаёт объекты |
| GameSession | ISubscriber<GamePausedEvent, GameResumedEvent, SaveCompletedEvent, LoadCompletedEvent> | GameSession управляет жизненным циклом (new/load/pause/quit). SES-01/SES-02 |

---

## 12. Scene Assembly (Фазы 16-18)

### SceneOrchestrator

`SceneOrchestrator` — оркестратор программной сборки сцены. Выполняет 11 фаз последовательно через `UniTask`:

```
SceneOrchestrator
├── Фаза 1:  CoreValidationPhase    — Валидация ядра (DI, интерфейсы)
├── Фаза 2:  TileMapGenPhase        — Генерация тайловой карты
├── Фаза 3:  WorldInitPhase         — Инициализация мира (время, локации, фракции)
├── Фаза 4:  PlayerSpawnPhase       — Спавн игрока
├── Фаза 5:  NPCSpawnPhase          — Спавн NPC
├── Фаза 6:  FormationInitPhase     — Инициализация формаций
├── Фаза 7:  ChargerInitPhase       — Инициализация зарядников
├── Фаза 8:  QuestInitPhase         — Инициализация квестов
├── Фаза 9:  UIInitPhase            — Инициализация UI
├── Фаза 10: FinalizePhase          — Финализация (публикация SceneAssemblyCompletedEvent)
└── Фаза 11: (reserved)
```

### ISceneAssemblyPhase

```csharp
public interface ISceneAssemblyPhase
{
    string PhaseName { get; }
    int PhaseOrder { get; }
    UniTask ExecuteAsync(CancellationToken ct = default);
}
```

### RuntimeSceneBuilder

`RuntimeSceneBuilder` — создаёт GameObject'ы программно (без Scene prefab):
- Tilemap, Grid, Renderer
- Player, Camera (CameraFollow)
- NPC GameObject'ы
- UI Canvas и панели

### GameSession

`GameSession` — управление жизненным циклом сессии:
- **New Game** → SceneOrchestrator.RunAssembly()
- **Load Game** → SaveService.Load() → SceneOrchestrator.RunAssembly() → RestoreState
- **Pause** → подписка на GamePausedEvent / GameResumedEvent
- **Quit** → SaveService.Save() → Cleanup

### SceneContracts

| Контракт | Описание |
|----------|----------|
| `SceneAssemblyStartedEvent` | Начало сборки сцены |
| `SceneAssemblyPhaseCompletedEvent` | Фаза завершена (PhaseName, Duration) |
| `SceneAssemblyCompletedEvent` | Сборка завершена успешно |
| `SceneAssemblyFailedEvent` | Сборка провалена (PhaseName, Exception) |

---

## 13. Метрики (актуализировано 2026-07-14)

| Метрика | Значение |
|---------|----------|
| Файлов .cs (новый код) | 429 |
| Модулей реализовано | 16 (Body, Buff, Charger, Combat, Formation, Generator, Interaction, Inventory, NPC, Player, Qi, Quest, Save, Tile, UI, World) |
| ModuleServices файлов | 16 (каждый модуль имеет XxxModuleServices.cs) |
| Scene Assembly фаз | 10 runtime (CoreValidation → Finalize) + 11 editor (Phase00-02 + Phase01B) |
| Entry/UI файлов | 22 (HUDPanelView, HotbarPanelView, BuffBarView, ToastView, MiniMapView, DialoguePanelView, PausePanelView, CombatOverlayView, DeathScreenView, LoadingScreenView, CharacterPanelView, TechniqueChargeView, CombatLogView, TurnOrderView, DamageNumberView, EnemyHealthBarView, InputLogPanel, NPCInspectorPanel, ContextMenuUI, GameInputAdapter, UIComponentResolver, DraggableWindow) |
| Stub-сервисов | 19 (все мёртвые — FallbackRegistrar не вызывается с Phase 17B) |
| Интерфейсов ядра | 44 |
| Контрактов сообщений | 23 файла (~130 контрактов) |
| Singleton-классов | 0 ✅ (все устранены) |
| ServiceLocator | 0 ✅ (устранён) |
| Debug.Log в новом коде | 1 (GameEntryPoint) |
| Stub orphan'ов | 0 ✅ (все удалены) |

---

## 14. Фазы реализации

| Фаза | Модуль | Статус | Чекпоинты |
|------|--------|--------|-----------|
| 0 | Core (интерфейсы, данные, messaging, DI) | ✅ audit_complete | plan_00, impl_00, audit_00 |
| 1 | Charger (зарядник Ци) | ✅ audit_complete | plan_01, impl_01, audit_01 |
| 2 | Tile (тайловая карта + ресурсы) | ✅ audit_complete | plan_02, impl_02, audit_02 |
| 3 | Body (система тела) | ✅ audit_complete | plan_03, impl_03, audit_03 |
| 4 | Qi (система Ци) | ✅ audit_complete | plan_04, impl_04, audit_04 |
| 5 | Buff (баффы/дебаффы) | ✅ audit_complete | plan_05, impl_05, audit_05 |
| 6 | Inventory (инвентарь/экипировка/крафт) | ✅ impl_complete | plan_06, impl_06 |
| 7 | Combat (боевая система) | ✅ audit_complete | plan_07, impl_07, audit_07 |
| 8 | Formation (система формаций) | ✅ audit_complete | plan_08, impl_08, audit_08 |
| 9 | NPC (система NPC) | ✅ audit_complete | plan_09, impl_09, audit_09 |
| 10 | Player (система игрока) | ✅ impl_complete | plan_10, impl_10 |
| 11 | World (мир, время, локации, фракции, события) | ✅ impl_complete | plan_11, impl_11 |
| 12 | Quest (квесты, награды, прогресс) | ✅ impl_complete | plan_12, impl_12 |
| 13 | Interaction (взаимодействия, диалоги) | ✅ impl_complete | plan_13, impl_13 |
| 14 | UI (интерфейс, уведомления, презентеры) | ✅ impl_complete | plan_14, impl_14 |
| 15 | Save (сохранения, автосохранение) | ✅ impl_complete | plan_15, impl_15 |
| 16 | Scene Assembly (SceneOrchestrator, Phases, RuntimeSceneBuilder) | ✅ impl_complete | plan_16, impl_16 |
| 17 | ModuleServices pattern (все модули) | ✅ impl_complete | plan_17, impl_17 |
| 18 | GameSession (жизненный цикл сессии) | ✅ impl_complete | plan_18, impl_18 |
| 19 | Entry/UI + Cleanup (UI views, stub cleanup) | ✅ impl_complete | plan_19, impl_19 |

---

*Документ создан: 2026-05-06*  
*Обновлён: 2026-05-09 — v2.0: Полная переработка под модульную архитектуру. Устаревшие паттерны (Singleton, ServiceLocator, Inspector-First) удалены. Hub-and-Spoke, VContainer, MessagePipe.*  
*Обновлён: 2026-05-09 — v3.2: Phase 7 (Combat). Combat, Damage, Technique, AI, Loot реализованы. CMB-A01..CMB-C06 исправлены. Fallback stubs для кросс-скоп DI. 7 активных модулей.*
*Обновлён: 2026-05-09 — v3.4: Phase 8 (Formation) аудит. FMT-A01: убрана двойная обработка утечки. FMT-A02: кэш Qi используется для проверок. FMT-A03: Depleted — стабильная стадия. FMT-A04: проверка уровня создателя. FMT-A05: проверка Ци для прорисовки. FMT-D01: TODO minHelperLevel. FMT-D02/D03: TODO environment/fillRate. Архитектура обновлена.*  
*Обновлён: 2026-05-09 — v3.5: Phase 9 (NPC). NPC, NPCSpawner, AI, CombatAdapter, Relationship, Movement реализованы. NPC-A07/A12/B04/B05 исправлены. 9 активных модулей.*
*Обновлён: 2026-05-09 — v3.6: Phase 10 (Player). PlayerController God Object (1425 LOC, 14+ зависимостей) разбит на 6 сервисов: PlayerService (тонкий фасад), SleepService, PlayerCombatAdapter, PlayerInputService, PlayerVisualService, PlayerConfig. PLR-A01/E02/E03/E06/E09 фиксы. StubPlayerService добавлен. 10 активных модулей.*
*Обновлён: 2026-05-09 — v3.7: Phase 11 (World). WorldModule, WorldService, TimeService (реальная реализация, заменила Stub), LocationService, FactionService, EventService. IWorldService, IEventService добавлены. WorldContracts: 10 событий. WLD-A01..WLD-C01 фиксы. 11 активных модулей.*
*Обновлён: 2026-05-09 — v3.8: Phase 12 (Quest). QuestModule, QuestService (9 методов), QuestRewardService, QuestProgressTracker. IQuestService расширен, IQuestRewardService добавлен. QuestContracts: 6 событий. QST-A01..QST-C01 фиксы. 12 активных модулей.*
*Обновлён: 2026-05-09 — v3.9: Phase 13 (Interaction). InteractionModule, InteractionService, DialogueService (6 методов), DialogueTypewriter. IInteractionService, IDialogueService реализованы. DialogueContracts: 4 события (InteractionCompletedEvent добавлен). INT-A01..INT-C01 фиксы. 13 активных модулей.*
*Обновлён: 2026-05-09 — v3.10: Phase 14 (UI). UIModule, UIService, ToastService, HUDPresenter, DialoguePresenter. IUIService реализован. UIContracts: 10 событий. Все «UI (будущее)» заменены на реальные подписки UIModule ✅. EVT command events (QiConsumeRequestEvent и др.) добавлены. Orphan stubs помечены. 15 активных модулей.*
*Обновлён: 2026-05-10 — v3.11: Phase 15 (Save). SaveModule, SaveService, SaveFileHandler, SaveDataAggregator. ISaveService реализован ✅. SaveContracts: 4 события. SaveModuleServices (без SaveLifetimeScope). SAV-01/02/03 фиксы. 16 модулей.*
*Обновлён: 2026-05-10 — v3.12: Phase 16 (Scene Assembly). SceneOrchestrator, RuntimeSceneBuilder, SceneAssemblyConfig, SceneAssemblyLogger, SceneAssemblyRegistrar, MessagingRegistrar. 11 фаз сборки (ISceneAssemblyPhase). SceneContracts: 4 события. CameraFollow, TilemapVisualService. SCN-01/02/03/04.*
*Обновлён: 2026-05-11 — v3.13: Phase 17 (ModuleServices). XxxModuleServices.cs для всех 15 модулей. Регистрация внутренних сервисов вынесена из LifetimeScope. MIN-01/02.*
*Обновлён: 2026-05-11 — v3.14: Phase 18 (GameSession). GameSession управляет жизненным циклом (new/load/pause/quit). SES-01/02. Core: VisualProvider, SortingLayerManager, RenderPipelineLogger, SpriteHelper. Core.Data: StatType.*
*Обновлён: 2026-05-11 — v3.15: Phase 19 (Entry/UI + Cleanup). LoadingScreenView, PausePanelView, DialoguePanelView, HUDPanelView, GameInputAdapter. Все stub удалены, кроме StubStatService. CLR-01. 16 модулей + SceneOrchestrator + GameSession, 234 файла, 26 интерфейсов, 1 stub.*
*Обновлён: 2026-05-18 — v3.16: Фаза 3 Body доработка. IStatService → StatService (9 методов, Modules.Player). IBodyService → 14 методов. +BodyFactory, BodyTemplateProvider, SpeciesRegistry, SeveredDebuffSystem. +BodyPartTemplate, BodyTemplate, SpeciesData (Core/Data). BodyMorphology.cs удалён. BodyPartReattachedEvent добавлен. 241 файл, 25 интерфейсов, 0 stub (StubStatService — fallback).*

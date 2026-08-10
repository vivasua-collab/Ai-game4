# 🛠️ Архитектура реализации: Cultivation World Simulator

**Версия:** 1.1
**Дата:** 2026-07-14
**Статус:** ✅ Рабочая документация (актуализировано после 06_17-06_18)

> Стабильная архитектура (финальный дизайн) — в [ARCHITECTURE.md](./ARCHITECTURE.md)
> Актуальные числа и метрики — в [ARCHITECTURE_CODE.md](./ARCHITECTURE_CODE.md) §13

---

## ⚠️ Важно

> Этот документ отслеживает **ход реализации**: статусы модулей, примеры расчётов, заметки по миграции, примеры кода.
> Для архитектурных концепций и формул — см. [ARCHITECTURE.md](./ARCHITECTURE.md) и [ALGORITHMS.md](./ALGORITHMS.md).

---

## 📊 Общий статус реализации

### История фаз

| Фаза | Название | Статус |
|------|----------|--------|
| 0 | Project Setup | ✅ |
| 1 | Charger | ✅ |
| 2 | Tile | ✅ |
| 3 | Body | ✅ |
| 4 | Qi | ✅ |
| 5 | Buff | ✅ |
| 6 | Inventory | ✅ |
| 7 | Combat | ✅ |
| 8 | Formation | ✅ |
| 9 | NPC | ✅ |
| 10 | Player | ✅ |
| 11 | World | ✅ |
| 12 | Quest | ✅ |
| 13 | Interaction | ✅ |
| 14 | UI | ✅ |
| 15 | Save | ✅ |
| 16-17 | SceneOrchestrator + ModuleServices | ✅ |
| 18-19 | GameSession + Start Scene | ✅ |

### Статус модулей

| Модуль | Фаза | Интерфейсы | Контракты | Статус |
|--------|------|------------|-----------|--------|
| Charger | 1 | IChargerService | ChargerContracts | ✅ Реализован |
| Tile | 2 | ITileService, IResourceService | TileContracts | ✅ Реализован |
| Body | 3 | IBodyService | BodyContracts | ✅ Реализован |
| Qi | 4 | IQiService, IQiBufferService | QiContracts | ✅ Реализован |
| Buff | 5 | IBuffService | BuffContracts | ✅ Реализован |
| Inventory | 6 | IInventoryService, IStorageService, ICraftingService, IEquipmentService | InventoryContracts, CraftingContracts | ✅ Реализован |
| Combat | 7 | ICombatService, IDamageService | CombatContracts | ✅ Реализован |
| Formation | 8 | IFormationService | FormationContracts | ✅ Реализован |
| NPC | 9 | INPCService, INPCSpawnerService | NPCContracts | ✅ Реализован |
| Player | 10 | IPlayerService, IPlayerInputService | PlayerContracts | ✅ Реализован |
| World | 11 | IWorldService, IEventService, ITimeService | WorldContracts | ✅ Реализован |
| Quest | 12 | IQuestService, IQuestRewardService | QuestContracts | ✅ Реализован |
| Interaction | 13 | IInteractionService, IDialogueService | DialogueContracts | ✅ Реализован |
| UI | 14 | IUIService | UIContracts | ✅ Реализован |
| Save | 15 | ISaveService, ISaveable | SaveContracts | ✅ Реализован |
| SceneOrchestrator | 16-17 | ISceneAssemblyPhase | SceneContracts | ✅ Реализован |
| GameSession | 18-19 | — | GameContracts | ✅ Реализован |

**Итого (актуализировано 2026-07-14):** 16 модулей + SceneOrchestrator + GameSession, **429 файлов, 44 интерфейса, 19 stubs (мёртвые — FallbackRegistrar не вызывается с Phase 17B)**. См. [ARCHITECTURE_CODE.md §13](./ARCHITECTURE_CODE.md#13-метрики-актуализировано-2026-07-14) для полных метрик.

---

## 📐 Миграция с Legacy

### Таблица миграций

| Было (Legacy) | Стало (Новый код) |
|---------------|-------------------|
| Singleton `Instance` | VContainer `[Inject]` |
| ServiceLocator `Get<T>()` | `IContainerBuilder` регистрация |
| `FindFirstObjectByType<T>()` | DI-инъекция через `[Inject]` |
| `[SerializeField]` кросс-модульные ссылки | `[Inject]` через интерфейс ядра |
| C# `event` / `Action` | MessagePipe `IPublisher<T>` / `ISubscriber<T>` |
| Coroutines (`IEnumerator` + `StartCoroutine`) | UniTask (`async UniTask`) |

### Примеры миграции

#### Singleton → VContainer

```csharp
// ❌ Legacy: Singleton
public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance { get; private set; }
    void Awake() { Instance = this; }
    public void Attack() { /* ... */ }
}

// Использование:
CombatManager.Instance.Attack(); // Жёсткая связь

// ✅ Новый код: VContainer DI
public class CombatService : ICombatService
{
    public void Attack() { /* ... */ }
}

// Использование:
[Inject] ICombatService _combatService;
_combatService.Attack(); // Через интерфейс, слабая связь
```

#### ServiceLocator → IContainerBuilder

```csharp
// ❌ Legacy: ServiceLocator
var mgr = ServiceLocator.Get<CombatManager>();
mgr.StartCombat(targetId);

// ✅ Новый код: DI через IContainerBuilder
public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<ICombatService, CombatService>(Lifetime.Singleton);
    }
}

// Использование:
[Inject] ICombatService _combatService;
_combatService.StartCombat(targetId);
```

#### FindFirstObjectByType → DI

```csharp
// ❌ Legacy: Поиск объекта на сцене
var player = FindFirstObjectByType<PlayerController>();
player.TakeDamage(damage);

// ✅ Новый код: DI-инъекция
[Inject] IPlayerService _playerService;
// PlayerService доступен через DI, никакого поиска
```

#### [SerializeField] кросс-модульные → [Inject] через интерфейс

```csharp
// ❌ Legacy: Прямая ссылка через Inspector
public class PlayerController : MonoBehaviour
{
    [SerializeField] private QiManager _qiManager; // Кросс-модульная ссылка!
    void Meditate() { _qiManager.AddQi(amount); }
}

// ✅ Новый код: DI через интерфейс ядра
public class PlayerService : IPlayerService
{
    [Inject] private readonly IQiService _qiService; // Через интерфейс Core
    public void Meditate() { _qiService.AddQi(amount); }
}
```

#### C# Events → MessagePipe

```csharp
// ❌ Legacy: C# event
public class QiManager : MonoBehaviour
{
    public event Action<float> OnQiChanged;
    void ChangeQi(float amount) { OnQiChanged?.Invoke(amount); }
}
// Подписчик в другом модуле:
qiManager.OnQiChanged += HandleQiChanged; // Прямая зависимость!

// ✅ Новый код: MessagePipe
// Публикация:
[Inject] IPublisher<QiChangedEvent> _qiChangedPub;
_qiChangedPub.Publish(new QiChangedEvent { CurrentQi = 500, MaxQi = 1000 });

// Подписка:
[Inject] ISubscriber<QiChangedEvent> _qiChangedSub;
_qiChangedSub.Subscribe(e => HandleQiChanged(e.CurrentQi));
// Нет прямой зависимости между модулями!
```

#### Coroutines → UniTask

```csharp
// ❌ Legacy: Coroutine
public class BuffManager : MonoBehaviour
{
    IEnumerator TickBuffCoroutine(Buff buff)
    {
        while (buff.RemainingTicks > 0)
        {
            yield return new WaitForSeconds(1f);
            buff.RemainingTicks--;
        }
    }
    void Start() { StartCoroutine(TickBuffCoroutine(buff)); }
}

// ✅ Новый код: UniTask
public class BuffTickProcessor
{
    public async UniTaskVoid TickBuffAsync(ActiveBuff buff)
    {
        while (buff.RemainingTicks > 0)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(1));
            buff.RemainingTicks--;
        }
    }
}
```

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

// ❌ Кросс-модульная прямая ссылка на конкретный класс
[Inject] ChargerService _charger; // Нужен IChargerService!

// ❌ Прямой вызов обработчика другого модуля
bodyService.ApplyDamage(part, dmg); // Из CombatModule — только через MessagePipe!

// ❌ C# event для межмодульной коммуникации
public event Action<float> OnDamageDealt; // Используйте MessagePipe!

// ❌ Coroutine для асинхронных операций
StartCoroutine(DoSomethingAsync()); // Используйте UniTask!

// ✅ DI через интерфейс ядра
[Inject] IChargerService _chargerService;

// ✅ MessagePipe для межмодульной связи
[Inject] IPublisher<BodyPartDamagedEvent> _damagePub;
```

---

## 🔧 Детали реализации по модулям

### Charger (Фаза 1)

**Интерфейс:** `IChargerService`
- `IsOperational`, `HeatLevel`, `UseQiForTechnique`, `EnterCombat`, `Tick`

**Контракты (ChargerContracts):**
- `ChargerStateChangedEvent` — изменение состояния (On/Off)
- `ChargerOverheatedEvent` — перегрев
- `ChargerCooledDownEvent` — остывание
- `ChargerHeatChangedEvent` — изменение уровня тепла
- `ChargerBufferChangedEvent` — изменение буфера Ци

**Сервисы:** ChargerService

**Файлы:** ChargerModule, ChargerLifetimeScope, ChargerModuleServices, ChargerService, ChargerBuffer, ChargerData, ChargerHeat, ChargerSlot

**Решения:**
- Режимы: On/Off (упрощённая модель)
- Тепловой баланс: 5 состояний (Cool → Warm → Hot → Critical → Overheated)
- ChargerBuffer инкапсулирует Ци-буфер зарядника
- ChargerSlot управляет слотами зарядника (belt, bracelet, necklace, ring)

> См. [ARCHITECTURE.md](./ARCHITECTURE.md) §«Система зарядников» · [CHARGER_SYSTEM.md](./CHARGER_SYSTEM.md)

---

### Tile (Фаза 2)

**Интерфейсы:** `ITileService`, `IResourceService`
- ITileService: `GetTile`, `SetTile`, `TryHarvest`, `IsWalkable`
- IResourceService: `TrySpawnResource`, `TryPickup`, `Harvest`, `RegisterDepletedResource`

**Контракты (TileContracts):**
- `TileChangedEvent` — изменение тайла
- `ResourceHarvestedEvent` — сбор ресурса
- `ResourceDepletedEvent` — истощение ресурса
- `TileMapGeneratedEvent` — генерация карты
- `ResourceRespawnedEvent` — респаун ресурса
- `HarvestResult` (readonly struct) — результат сбора

**Сервисы:** TileMapService, ResourceService, DestructibleService, TileGeneratorService

**Файлы:** TileModule, TileLifetimeScope, TileModuleServices + 4 сервиса

**Решения:**
- Циркулярная зависимость TileMapService ↔ ResourceService решена через `ResourceRespawnedEvent`
- ResourceService публикует событие, TileMapService подписывается
- TileGeneratorService генерирует карту программно

> См. [ARCHITECTURE.md](./ARCHITECTURE.md) §«Система тайлов» · [TILE_SYSTEM.md](./TILE_SYSTEM.md)

---

### Body (Фаза 3)

**Интерфейс:** `IBodyService`
- `GetPartState`, `ApplyDamage`, `HealPart`, `IsSlotBlocked`, `GetAllParts`

**Контракты (BodyContracts):**
- `BodyPartDamagedEvent` — повреждение части тела
- `BodyPartSeveredEvent` — отрубание части тела
- `BodyPartHealedEvent` — лечение части тела

**Файлы:** BodyModule, BodyLifetimeScope, BodyModuleServices, BodyService, BodyPart, BodyMorphology, BodySlotMapping, BodyDamageCalculator

**Решения:**
- Система двойной HP (Kenshi-style): красная (функциональная) + чёрная (структурная)
- Распределение урона: 70% в красную HP, 30% в чёрную HP
- BodyMorphology — раскладка частей по морфологии (Humanoid, Quadruped и т.д.)
- BodySlotMapping — маппинг BodyPart → EquipmentSlot
- BodyDamageCalculator — расчёт урона с учётом материала тела

> См. [ARCHITECTURE.md](./ARCHITECTURE.md) §«Система тела» · [BODY_SYSTEM.md](./BODY_SYSTEM.md)

---

### Qi (Фаза 4)

**Интерфейсы:** `IQiService`, `IQiBufferService`
- IQiService: 22 метода (EntityId, CurrentQi, MaxQi, QiRatio, IsEmpty, IsFull, TryConsumeQi, AddQi, Regenerate, CultivationLevel, SubLevel, CoreQuality, CoreCapacity, QiDensity, EffectiveQi, Conductivity, ConductivityBonus, SetConductivityBonus, CanBreakthrough, CalculateBreakthroughRequirement, TryBreakthrough, SetCultivationLevel)
- IQiBufferService: `IsActive`, `Mode`, `QiInvested`, `Activate`, `Deactivate`, `AbsorbDamage`

**Контракты (QiContracts):**
- `QiChangedEvent`, `QiDepletedEvent`, `QiFullEvent`, `CultivationBreakthroughEvent`
- `QiBufferActivatedEvent`, `QiBufferDeactivatedEvent`, `QiBufferStateChangedEvent`
- Командные события: `QiConsumeRequestEvent`, `QiAddRequestEvent`, `QiBufferActivateRequestEvent`, `QiBufferDeactivateRequestEvent`

**Сервисы:** QiService, QiBufferService

**Калькуляторы:** QiRegenCalculator, QiBreakthroughCalculator

**Файлы:** QiModule, QiLifetimeScope, QiModuleServices, QiService, QiBufferService, QiConfig, QiRegenCalculator, QiBreakthroughCalculator

**Решения:**
- **Fix-01:** Тип `float → long` — все значения Ци используют `long` для предотвращения потери точности на высоких уровнях (L8+ ~789,750 Ци)
- Формула вместимости: `coreCapacity = 1000 × 1.1^totalSubLevels × qualityMultiplier`
- Плотность Ци: `qiDensity = 2^(level-1)`
- Проводимость: `conductivity = coreCapacity / 360`
- QiBuffer: сырая Ци (90%/3:1 для техник, 80%/5:1 для физики) и щитовая техника (100%/1:1 и 100%/2:1)
- Command events (QiConsumeRequestEvent и др.) для развязки модулей (request→response)

> См. [ARCHITECTURE.md](./ARCHITECTURE.md) §«Система Ци» · [QI_SYSTEM.md](./QI_SYSTEM.md)

---

### Buff (Фаза 5)

**Интерфейс:** `IBuffService` — 9 методов
- `ApplyBuff`, `RemoveBuff`, `RemoveAllBuffs`, `HasBuff`, `GetStatModifier`, `GetElementResistance`, `HasImmunity`, `GetActiveBuffs`, `TickBuffs`

**Контракты (BuffContracts):**
- `BuffAppliedEvent`, `BuffRemovedEvent`, `BuffExpiredEvent`, `BuffTickedEvent`, `StatModifierChangedEvent`

**Файлы:** BuffModule, BuffLifetimeScope, BuffModuleServices, BuffService, BuffCalculator, BuffTickProcessor, BuffConfig, ActiveBuff

**Решения:**
- **Расщепление God Object:** Legacy BuffManager (1614 LOC) → BuffService + BuffCalculator + BuffTickProcessor
- BuffCalculator — расчёт модификаторов + мягкий кап (soft cap): `effectiveBonus = cap × (1 - e^(-bonus / (cap × decayRate)))`
- BuffTickProcessor — обработка тиков баффов
- 28 значений BuffType enum
- Иммунитеты: маппинг Effect → Immunity (BF-A03)
- Баффы НЕ могут модифицировать первичные характеристики, coreCapacity, qiDensity, qiRegen

> См. [ARCHITECTURE.md](./ARCHITECTURE.md) §«Система баффов/дебаффов» · [BUFF_MODIFIERS_SYSTEM.md](./BUFF_MODIFIERS_SYSTEM.md)

---

### Inventory (Фаза 6)

**Интерфейсы:** `IInventoryService`, `IStorageService`, `ICraftingService`, `IEquipmentService`
- IInventoryService: `TryAddItem`, `TryRemoveItem`, `GetItemCount`, `GetAllSlots`
- IStorageService: `TryStore`, `TryRetrieve`, `GetStoredItems`
- ICraftingService: `CanCraft`, `TryCraft`
- IEquipmentService: `GetEquipped`, `TryEquip`, `TryUnequip`, `IsSlotBlocked`, `GetTotalArmor`, `GetTotalDamage`

**Контракты:** InventoryContracts (`ItemAddedEvent`, `ItemRemovedEvent`, `EquipmentChangedEvent`, `EquipmentBlockedEvent`, `ItemAddRequestEvent`), CraftingContracts (`CraftCompletedEvent`, `CraftFailedEvent`)

**Сервисы:** InventoryService, EquipmentService, EquipmentValidator, EquipmentStatAggregator, StorageService, CraftingService, MaterialService

**Файлы:** InventoryModule, InventoryLifetimeScope, InventoryModuleServices + 7 сервисов + Data/CraftingRecipe.cs

**Решения:**
- EquipmentValidator — проверки слотов, требований, совместимости с телом
- EquipmentStatAggregator — подсчёт бонусов экипировки
- StorageService — Spirit + Ring хранилища
- ItemAddRequestEvent — командное событие для добавления предметов из других модулей

> См. [ARCHITECTURE.md](./ARCHITECTURE.md) §«Система инвентаря» · [INVENTORY_SYSTEM.md](./INVENTORY_SYSTEM.md) · [EQUIPMENT_SYSTEM.md](./EQUIPMENT_SYSTEM.md)

---

### Combat (Фаза 7)

**Интерфейсы:** `ICombatService`, `IDamageService`
- ICombatService: `IsInCombat`, `CurrentStage`, `CurrentTargetId`, `StartCombat`, `EndCombat`, `ExecuteAttack`, `ExecuteDefense`
- IDamageService: `CalculateDamage`, `ApplyDefense`

**Контракты (CombatContracts):**
- `CombatStartedEvent`, `CombatEndedEvent`, `DamageAppliedEvent`, `TechniqueUsedEvent`, `EnemyKilledEvent`

**Сервисы:** CombatService, DamageService + DamageCalculator, LevelSuppression, DefenseProcessor, TechniqueCapacity, CombatAIService, CombatLootService, TechniqueChargeService, TechniqueService

**Решения:**
- Полная реализация пайплайна урона (11 слоёв — см. [ALGORITHMS.md](./ALGORITHMS.md) §5)
- Level Suppression — множитель подавления на основе разницы уровней
- DefenseProcessor — обработка уклонения, парирования, блокирования
- TechniqueCapacity — расчёт ёмкости техник
- DamageCalculator — сквозной расчёт урона через все слои

> См. [ARCHITECTURE.md](./ARCHITECTURE.md) §«Боевая система» · [COMBAT_SYSTEM.md](./COMBAT_SYSTEM.md)

---

### Formation (Фаза 8)

**Интерфейс:** `IFormationService`
- `IsFormationActive`, `ActiveFormationId`, `CurrentStage`, `StartDrawing`, `StartFilling`, `ContributeQi`, `ActivateFormation`, `DeactivateFormation`, `GetFormationBonus`, `QiPoolCurrent`, `QiPoolMax`, `ParticipantCount`, `CasterId`, `GetActiveEffects`

**Контракты (FormationContracts):**
- `FormationActivatedEvent`, `FormationDeactivatedEvent`, `FormationQiPoolChangedEvent`, `FormationStageChangedEvent`, `FormationContributeQiRequestEvent`

**Сервисы:** FormationService

**Файлы:** FormationModule, FormationLifetimeScope, FormationModuleServices, FormationService, FormationCalculator, FormationQiPool, FormationEffects, FormationConfig, Data/FormationData

**Решения:**
- FormationCalculator: формулы contourQi, capacity, drain
- FormationQiPool — пул Ци формации (БЕЗ дублирования QiBuffer; у формации свой пул, не завязанный на QiBufferService)
- FormationEffects — эффекты (БЕЗ статического состояния; все данные через экземпляр)
- FormationContributeQiRequestEvent — командное событие для вноса Ци в формацию

> См. [ARCHITECTURE.md](./ARCHITECTURE.md) §«Система формаций» · [FORMATION_SYSTEM.md](./FORMATION_SYSTEM.md)

---

### NPC (Фаза 9)

**Интерфейсы:** `INPCService`, `INPCSpawnerService`
- INPCService: `GetNPC`, `GetNearbyNPCIds`, `GetAttitude`, `ModifyAttitude`, `IsAlive`, `GetAIState`, `GetAllNPCIds`, `SetAIState`, `UpdatePosition`
- INPCSpawnerService: `SpawnNPC`, `DespawnNPC`, `GetSpawnedNPCIds`, `ActiveNPCCount`

**Контракты (NPCContracts):**
- `NPCSpawnedEvent`, `NPCDespawnedEvent`, `AttitudeChangedEvent`, `NPCDeathEvent`, `NPCInteractedEvent`, `NPCAIStateChangedEvent`, `NPCDamagedEvent`

**Сервисы:** NPCService, NPCSpawnerService, NPCRelationshipService, NPCAIService, NPCCombatAdapter, NPCMovementService

**Файлы:** NPCModule, NPCLifetimeScope, NPCModuleServices + 6 сервисов + NPCConfig, Data/NPCState

**Решения:**
- NPCAIService — упрощённый Behaviour Tree (Selector → Sequence → Condition → Action)
- NPCRelationshipService — логика отношений (Attitude + затухание со временем)
- NPCCombatAdapter — адаптер боя через MessagePipe (НЕ прямая ссылка на CombatService)
- NPCMovementService — упрощённая навигация
- NPCConfig — `class` (BD-48: класс вместо ScriptableObject для конфигурации)
- Три категории NPC: Temp (только в памяти), Plot (сохранение в файл), Unique (полное сохранение)

> См. [ARCHITECTURE.md](./ARCHITECTURE.md) §«Система NPC AI» · [NPC.md](./NPC.md) · [NPC_AI_SYSTEM.md](./NPC_AI_SYSTEM.md)

---

### Player (Фаза 10)

**Интерфейсы:** `IPlayerService`, `IPlayerInputService`
- IPlayerService: `PlayerId`, `Position`, `IsAlive`, `IsSleeping`, `SleepState`, `Stance`, `StartSleep`, `WakeUp`, `SetPosition`, `GetAssignedTechniques`, `Tick`
- IPlayerInputService: `MoveDirection`, `RunHeld`, `IsAttackPressed`, `IsDefendPressed`, `IsInteractPressed`, `IsInventoryPressed`, `IsMeditatePressed`, `SelectedTechniqueSlot`, `InputDisabled`, `UpdateInputState`, `ResetFrameFlags`

**Контракты (PlayerContracts):**
- `PlayerDeathEvent`, `PlayerReviveEvent`, `PlayerSleepEvent`, `PlayerPositionChangedEvent`

**Сервисы:** PlayerService, PlayerCombatAdapter, PlayerInputService, SleepService, PlayerVisualService

**Файлы:** PlayerModule, PlayerLifetimeScope, PlayerModuleServices + 5 сервисов + PlayerConfig, Data/PlayerData

**Решения:**
- PlayerService — тонкий фасад (делегирует к специализированным сервисам)
- PlayerCombatAdapter — адаптер боя через MessagePipe
- PlayerInputService — чистый C# (НЕ MonoBehaviour), обновляется через ITickable
- SleepService — логика сна: подписка на `TimeChangedEvent` → расчёт эффектов сна
- PlayerVisualService — визуал (заглушка, расширение в Фазе 14+)
- PlayerSleepState: Awake, FallingAsleep, Sleeping, WakingUp
- PlayerStance: Normal, Combat, Meditating, Sleeping

> См. [ARCHITECTURE.md](./ARCHITECTURE.md) §«Система развития» · [MORTAL_DEVELOPMENT.md](./MORTAL_DEVELOPMENT.md)

---

### World (Фаза 11)

**Интерфейсы:** `IWorldService`, `IEventService`, `ITimeService`
- IWorldService: `CurrentLocationId`, `CurrentSectorId`, `TryTravel`, `GetLocation`, `GetFaction`, `GetFactionRelation`, `GetDiscoveredSectors`, `IsSectorDiscovered`
- IEventService: `TriggerWorldEvent`, `IsEventActive`, `GetActiveEvents`, `EndWorldEvent`
- ITimeService: `DeltaTime`, `TotalTime`, `CurrentDay/Hour`, `CurrentMonth`, `CurrentYear`, `TimeOfDay`, `Speed`, `Pause/Resume`

**Контракты (WorldContracts):**
- `TimeChangedEvent`, `DayChangedEvent`, `TimeSpeedChangedEvent`, `SceneTransitionRequest`, `SceneLoadedEvent`, `MonthChangedEvent`, `YearChangedEvent`, `LocationChangedEvent`, `TravelStartedEvent`, `WorldEventTriggeredEvent`, `WorldEventEndedEvent`

**Сервисы:** WorldService, TimeService, LocationService, FactionService, EventService

**Файлы:** WorldModule, WorldLifetimeScope, WorldModuleServices + 5 сервисов + WorldConfig, Data/ (WorldState, LocationData, FactionData, WorldEventData)

**Решения:**
- **TimeService** заменяет `StubTimeService` — полная реализация системы времени
- 1 тик = 1 минута игрового времени; 3 скорости (нормальная/ускоренная/быстрая) + пауза
- LocationService — управление локациями и секторами
- FactionService — логика фракций и отношений
- EventService — мировые события (динамические события в мире)
- Начальный год: 1864 (Э.С.М. — Эра Сердца Мира)

> См. [ARCHITECTURE.md](./ARCHITECTURE.md) §«Система мира» · [WORLD_SYSTEM.md](./WORLD_SYSTEM.md)

---

### Quest (Фаза 12)

**Интерфейсы:** `IQuestService`, `IQuestRewardService`
- IQuestService: `StartQuest`, `AbandonQuest`, `CompleteQuest`, `FailQuest`, `GetActiveQuestIds`, `IsQuestComplete`, `GetQuestStatus`, `QuestExists`, `GetQuestType`
- IQuestRewardService: `GrantRewards`, `AreRewardsGranted`

**Контракты (QuestContracts):**
- `QuestStartedEvent`, `QuestObjectiveUpdatedEvent`, `QuestCompletedEvent`, `QuestFailedEvent`, `QuestAbandonedEvent`, `QuestRewardGrantedEvent`

**Сервисы:** QuestService, QuestRewardService, QuestProgressTracker

**Файлы:** QuestModule, QuestLifetimeScope, QuestModuleServices + 3 сервиса + QuestConfig, Data/ (QuestData, QuestObjective, QuestReward)

**Решения:**
- QuestProgressTracker — отслеживание прогресса целей через 6 подписок на события MessagePipe:
  - `EnemyKilledEvent` → убийство врагов
  - `ResourceHarvestedEvent` → сбор ресурсов
  - `NPCInteractedEvent` → взаимодействие с NPC
  - `LocationChangedEvent` → посещение локаций
  - `ItemAddedEvent` → получение предметов
  - `CombatEndedEvent` → завершение боя
- Квесты привязаны к NPC и локациям

> См. [ARCHITECTURE.md](./ARCHITECTURE.md) §«Система квестов»

---

### Interaction (Фаза 13)

**Интерфейсы:** `IInteractionService`, `IDialogueService`
- IInteractionService: `GetNearestInteractableId`, `TryInteract`
- IDialogueService: `StartDialogue`, `AdvanceDialogue`, `SelectChoice`, `EndDialogue`, `IsInDialogue`, `CurrentDialogueId`

**Контракты (DialogueContracts):**
- `DialogueStartedEvent`, `DialogueEndedEvent`, `DialogueChoiceSelectedEvent`, `InteractionCompletedEvent`

**Сервисы:** InteractionService, DialogueService, DialogueTypewriter

**Файлы:** InteractionModule, InteractionLifetimeScope, InteractionModuleServices + 3 сервиса + InteractionConfig, Data/ (DialogueNode, DialogueChoice)

**Решения:**
- DialogueTypewriter — эффект печатающегося текста
- **Исправлен баг legacy:** таймер в старом коде не останавливался при пропуске текста → утечка корутины
- В новом коде используется UniTask с CancellationToken — корректная отмена
- Диалоги с ветвлением через DialogueNode + DialogueChoice

> См. [ARCHITECTURE.md](./ARCHITECTURE.md) §«Система взаимодействия»

---

### UI (Фаза 14)

**Интерфейс:** `IUIService`
- `CurrentUIState`, `SetUIState`, `ShowToast`, `ShowModal`

**Контракты (UIContracts):**
- `UIShowToastEvent`, `UIHideToastEvent`, `UIShowModalEvent`, `UIHideModalEvent`, `UIHUDUpdatedEvent`, `UIDialogueOpenedEvent`, `UIDialogueClosedEvent`, `UIDialogueTextUpdatedEvent`, `UIInventoryOpenedEvent`, `UIInventoryClosedEvent`

**Сервисы:** UIService, ToastService, HUDPresenter, DialoguePresenter

**Файлы:** UIModule, UILifetimeScope, UIModuleServices + 4 сервиса + UIConfig, Data/UIState

**Решения:**
- ToastService — уведомления (toast-сообщения) с автовоспроизведением и очередью
- HUDPresenter — презентер HUD: подписка на QiChangedEvent, BodyPartDamagedEvent и др.
- DialoguePresenter — презентер диалогов: связывает DialogueService с UI-элементами
- UIState enum — состояние UI (для управления стеком окон)

> См. [ARCHITECTURE.md](./ARCHITECTURE.md) §«Система UI»

---

### Save (Фаза 15)

**Интерфейсы:** `ISaveService`, `ISaveable`
- ISaveService: `Save`, `Load`, `HasSave`, `DeleteSave`, `GetAllSaves`
- ISaveable: `SaveKey`, `CaptureState`, `RestoreState`

**Контракты (SaveContracts):**
- `SaveRequestedEvent`, `LoadRequestedEvent`, `SaveCompletedEvent`, `LoadCompletedEvent`

**Сервисы:** SaveService, SaveFileHandler, SaveDataAggregator

**Файлы:** SaveModule, SaveModuleServices + 3 сервиса + SaveConfig, Data/ (SaveSlotData, AutoSaveConfig)

**Решения:**
- **ModuleServices pattern** — у Save НЕТ отдельного LifetimeScope (в отличие от других модулей)
- Регистрация через `SaveModuleServices.Register(IContainerBuilder)`
- SaveFileHandler — чтение/запись файлов сохранений (JSON)
- SaveDataAggregator — агрегация данных от всех `ISaveable` сервисов
- Автосохранение: триггеры — смена локации, получение техники, прорыв, завершение боя
- ISaveable — интерфейс для сервисов, которые хотят сохранять состояние

> См. [ARCHITECTURE.md](./ARCHITECTURE.md) §«Система сохранения» · [SAVE_SYSTEM.md](./SAVE_SYSTEM.md)

---

### SceneOrchestrator + GameSession (Фазы 16-19)

**Интерфейс:** `ISceneAssemblyPhase`
- `PhaseName`, `PhaseOrder`, `ExecuteAsync` (UniTask)

**Контракты (SceneContracts):**
- `SceneAssemblyStartedEvent`, `SceneAssemblyPhaseCompletedEvent`, `SceneAssemblyCompletedEvent`, `SceneAssemblyFailedEvent`

**Фазы сборки (11):**
1. CoreValidationPhase — проверка DI-резолва всех интерфейсов
2. TileMapGenPhase — генерация тайловой карты
3. WorldInitPhase — инициализация мира
4. PlayerSpawnPhase — спавн игрока (центр карты)
5. NPCSpawnPhase — спавн NPC
6. FormationInitPhase — инициализация формаций
7. ChargerInitPhase — инициализация зарядников
8. QuestInitPhase — инициализация квестов
9. UIInitPhase — инициализация UI
10. FinalizePhase — финализация сборки

(+ AbstractSceneAssemblyPhase — базовый класс)

**GameSession lifecycle:**
- StartNewGame → SceneOrchestrator → Playing
- LoadGame → SceneOrchestrator (LoadMode) → Playing
- Pause / Resume
- SaveAndQuit / QuitWithoutSaving

**RuntimeSceneBuilder** — программная сборка иерархии Unity-сцены:
- Camera (Orthographic, URP 2D)
- Canvas (Screen-Space Overlay)
- EventSystem
- World Root (Grid + Tilemap + Objects)
- Player (SpriteRenderer + procedural sprite)
- Light2D (Global, для Sprite-Lit-Default)
- GameInputAdapter (F5/F9/Esc)

**Entry UI (5 файлов):**
- LoadingScreenView — экран загрузки
- PausePanelView — панель паузы
- DialoguePanelView — панель диалогов
- HUDPanelView — панель HUD
- GameInputAdapter — адаптер ввода (F5=быстрое сохранение, F9=загрузка, Esc=пауза)

**Core утилиты:**
- VisualProvider — провайдер визуальных данных
- SortingLayerManager — управление слоями сортировки
- RenderPipelineLogger — логирование рендер-пайплайна
- SpriteHelper — утилиты для спрайтов

> См. [ARCHITECTURE.md](./ARCHITECTURE.md) §«Система сборки сцены» · [SCENE_BUILDER_SYSTEM.md](./SCENE_BUILDER_SYSTEM.md)

---

## 📝 Примеры расчётов

### Вместимость ядра Ци (рост по уровням)

Формула: `coreCapacity = 1000 × 1.1^totalSubLevels × qualityMultiplier`

| Уровень | totalSubLevels | coreCapacity | qiDensity | effectiveQi |
|---------|----------------|--------------|-----------|-------------|
| L1.0 | 0 | 1,000 | 1 | 1,000 |
| L2.0 | 10 | 2,594 | 2 | 5,188 |
| L3.0 | 20 | 6,727 | 4 | 26,908 |
| L4.0 | 30 | 17,450 | 8 | 139,600 |
| L5.0 | 40 | 45,260 | 16 | 724,160 |
| L6.0 | 50 | 117,390 | 32 | 3,756,480 |
| L7.0 | 60 | 304,480 | 64 | 19,486,720 |
| L8.0 | 70 | 789,750 | 128 | 101,088,000 |
| L9.0 | 80 | 2,048,400 | 256 | 524,390,400 |

### Живучесть → множитель HP

Формула: `hpMultiplier = 1.0 + (VIT - 10) × 0.05`

| VIT | Множитель HP | Пример (база 100 HP) |
|-----|-------------|---------------------|
| 10 | ×1.0 | 100 HP |
| 20 | ×1.5 | 150 HP |
| 50 | ×3.0 | 300 HP |
| 100 | ×5.5 | 550 HP |

> **Мягкий кап:** vitality_hp_mult = +500%, decay = 2.0 (см. [ALGORITHMS.md](./ALGORITHMS.md) §6.5)

### Пайплайн урона — пример с числами

**Сценарий:** Практик L3 атакует практика L5 техникой melee_strike (Common grade, mastery 50%)

```
1. Исходный урон:
   baseCapacity = 64 (melee_strike)
   levelMultiplier = 2^(3-1) = 4
   masteryBonus = 1 + (50/100) × 0.5 = 1.25
   capacity = 64 × 4 × 1.25 = 320
   gradeMultiplier = 1.0 (Common)
   ultimateMultiplier = 1.0
   rawDamage = 320 × 1.0 × 1.0 = 320

2. Level Suppression (атакующий L3, защитник L5, diff=2):
   suppression = 0.1 (technique, diff=2)
   damage = 320 × 0.1 = 32

3. Масштабирование от характеристик (STR=15):
   statBonus = (15 - 10) × 0.05 = 0.25 (+25%)
   damage = 32 × 1.25 = 40

4. Уклонение (AGI=12, броня penalty=0):
   dodgeChance = 5% + (12-10) × 0.5% = 6%
   → Не уклонился (94% шанс)

5. Qi Buffer (сырая Ци, защитник L5, Ци=724,160):
   Физический урон, сырая Ци:
   absorbable = 40 × 0.80 = 32
   requiredQi = 32 × 5.0 = 160
   piercingDamage = 40 × 0.20 = 8
   → 8 HP урона прошло

6. Броня (coverage=0.7, DR=0.3):
   → Попадание по броне (70% шанс)
   damage = 8 × (1 - 0.3) = 5.6
   → Без брони (30% шанс): 8

7. Материал тела (Organic, 0%):
   damage = 5.6 (без изменений)

8. Распределение по HP:
   redHP -= 5.6 × 0.7 = 3.92
   blackHP -= 5.6 × 0.3 = 1.68
```

### Qi Buffer — пример поглощения

**Сценарий 1:** Техника Ци наносит 100 урона практику с 500 Ци (сырая Ци)

```
absorbableDamage = 100 × 0.90 = 90
requiredQi = 90 × 3.0 = 270
piercingDamage = 100 × 0.10 = 10

Ци после: 500 - 270 = 230
В HP: 10 урона
```

**Сценарий 2:** Физический урон 100, практик с 200 Ци (сырая Ци)

```
absorbableDamage = 100 × 0.80 = 80
requiredQi = 80 × 5.0 = 400
→ Недостаточно Ци! (только 200)
absorbed = 200 / 5.0 = 40 (урона поглощено)
piercingDamage = 100 × 0.20 = 20

Ци после: 0
В HP: 100 - 40 = 60 (40 поглощено + 20 пробитие = 60 прошло)
```

**Сценарий 3:** Щитовая техника, 100 урона, 500 Ци

```
// Техника Ци
requiredQi = 100 × 1.0 = 100
piercingDamage = 0

Ци после: 500 - 100 = 400
В HP: 0 (полное поглощение!)

// Физический урон
requiredQi = 100 × 2.0 = 200
piercingDamage = 0

Ци после: 500 - 200 = 300
В HP: 0 (полное поглощение!)
```

### Масштабирование от характеристик — пример

**Техника:** melee_strike, STR = 15

```
coefficient = 0.05 (5%/ед для STR в melee_strike)
statBonus = (15 - 10) × 0.05 = 0.25
→ +25% к урону
```

**Техника:** ranged_beam, INT = 20

```
coefficient = 0.05 (5%/ед для INT в ranged_beam)
statBonus = (20 - 10) × 0.05 = 0.50
→ +50% к урону
```

---

## 📋 Реестр интерфейсов

| Интерфейс | Реализация | Модуль | Статус |
|-----------|------------|--------|--------|
| IChargerService | ChargerService | Modules.Charger | ✅ |
| ITileService | TileMapService | Modules.Tile | ✅ |
| IResourceService | ResourceService | Modules.Tile | ✅ |
| IBodyService | BodyService | Modules.Body | ✅ |
| ITimeService | TimeService | Modules.World | ✅ |
| IQiService | QiService | Modules.Qi | ✅ |
| IQiBufferService | QiBufferService | Modules.Qi | ✅ |
| IBuffService | BuffService | Modules.Buff | ✅ |
| IStatService | StubStatService | Entry.Stubs | 🔒 Stub |
| IInventoryService | InventoryService | Modules.Inventory | ✅ |
| IStorageService | StorageService | Modules.Inventory | ✅ |
| ICraftingService | CraftingService | Modules.Inventory | ✅ |
| IEquipmentService | EquipmentService | Modules.Inventory | ✅ |
| ICombatService | CombatService | Modules.Combat | ✅ |
| IDamageService | DamageService | Modules.Combat | ✅ |
| INPCService | NPCService | Modules.NPC | ✅ |
| INPCSpawnerService | NPCSpawnerService | Modules.NPC | ✅ |
| IPlayerService | PlayerService | Modules.Player | ✅ |
| IPlayerInputService | PlayerInputService | Modules.Player | ✅ |
| IWorldService | WorldService | Modules.World | ✅ |
| IEventService | EventService | Modules.World | ✅ |
| IQuestService | QuestService | Modules.Quest | ✅ |
| IQuestRewardService | QuestRewardService | Modules.Quest | ✅ |
| IInteractionService | InteractionService | Modules.Interaction | ✅ |
| IDialogueService | DialogueService | Modules.Interaction | ✅ |
| IUIService | UIService | Modules.UI | ✅ |
| IFormationService | FormationService | Modules.Formation | ✅ |
| ISaveService | SaveService | Modules.Save | ✅ |
| ISaveable | (множественная) | Разные модули | ✅ |
| ISceneAssemblyPhase | (11 фаз) | Entry.Phases | ✅ |

---

## 📋 Реестр контрактов (MessagePipe)

| Файл | События | Домен |
|------|---------|-------|
| GameContracts.cs | `GameStateChangedEvent`, `GamePausedEvent`, `GameResumedEvent` | Игра |
| CombatContracts.cs | `CombatStartedEvent`, `CombatEndedEvent`, `DamageAppliedEvent`, `TechniqueUsedEvent`, `EnemyKilledEvent` | Бой |
| BodyContracts.cs | `BodyPartDamagedEvent`, `BodyPartSeveredEvent`, `BodyPartHealedEvent` | Тело |
| QiContracts.cs | `QiChangedEvent`, `QiDepletedEvent`, `QiFullEvent`, `CultivationBreakthroughEvent`, `QiBufferActivatedEvent`, `QiBufferDeactivatedEvent`, `QiConsumeRequestEvent`, `QiAddRequestEvent`, `QiBufferActivateRequestEvent`, `QiBufferDeactivateRequestEvent`, `QiBufferStateChangedEvent` | Ци |
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
| UIContracts.cs | `UIShowToastEvent`, `UIHideToastEvent`, `UIShowModalEvent`, `UIHideModalEvent`, `UIHUDUpdatedEvent`, `UIDialogueOpenedEvent`, `UIDialogueClosedEvent`, `UIDialogueTextUpdatedEvent`, `UIInventoryOpenedEvent`, `UIInventoryClosedEvent` | UI |
| SceneContracts.cs | `SceneAssemblyStartedEvent`, `SceneAssemblyPhaseCompletedEvent`, `SceneAssemblyCompletedEvent`, `SceneAssemblyFailedEvent` | Сцена |

> **EVT Command Events:** QiConsumeRequestEvent, QiAddRequestEvent, QiBufferActivateRequestEvent, QiBufferDeactivateRequestEvent, QiBufferStateChangedEvent, ItemAddRequestEvent, FormationContributeQiRequestEvent — командные события (request→response паттерн) для развязки модулей.

---

## 🏗️ ModuleServices Pattern

### Проблема

SceneOrchestrator в корневом `GameLifetimeScope` видит только stub-сервисы. Реальные сервисы регистрируются в дочерних `LifetimeScope` (например, `ChargerLifetimeScope`). В VContainer дочерние scope-ы (siblings) не видят друг друга — это проблема **sibling scope visibility**.

### Решение

Каждый модуль имеет статический класс `XxxModuleServices` с методом `Register(IContainerBuilder)`, который вызывается из корневого `GameLifetimeScope`. Все сервисы доступны в корневом scope.

```csharp
// Пример: ChargerModuleServices.cs
public static class ChargerModuleServices
{
    public static void Register(IContainerBuilder builder)
    {
        builder.Register<IChargerService, ChargerService>(Lifetime.Singleton);
        builder.Register<ChargerModule>(Lifetime.Singleton)
            .AsImplementedInterfaces(); // IStartable, ITickable
    }
}

// GameLifetimeScope.cs
public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // 1. MessagePipe
        var options = builder.RegisterMessagePipe();
        MessagingRegistrar.RegisterAll(builder, options);

        // 2. Stub-сервисы
        builder.Register<IStatService, StubStatService>(Lifetime.Singleton);

        // 3. ModuleServices (все 16 модулей)
        ChargerModuleServices.Register(builder);
        TileModuleServices.Register(builder);
        BodyModuleServices.Register(builder);
        QiModuleServices.Register(builder);
        BuffModuleServices.Register(builder);
        InventoryModuleServices.Register(builder);
        CombatModuleServices.Register(builder);
        FormationModuleServices.Register(builder);
        NPCModuleServices.Register(builder);
        PlayerModuleServices.Register(builder);
        WorldModuleServices.Register(builder);
        QuestModuleServices.Register(builder);
        InteractionModuleServices.Register(builder);
        UIModuleServices.Register(builder);
        SaveModuleServices.Register(builder);

        // 4. SceneOrchestrator + GameSession
        builder.Register<SceneOrchestrator>(Lifetime.Singleton);
        builder.Register<GameSession>(Lifetime.Singleton);

        // 5. Entry point
        builder.RegisterEntryPoint<GameEntryPoint>();
    }
}
```

### Все модули с ModuleServices

| Модуль | Файл ModuleServices |
|--------|---------------------|
| Charger | ChargerModuleServices.cs |
| Tile | TileModuleServices.cs |
| Body | BodyModuleServices.cs |
| Qi | QiModuleServices.cs |
| Buff | BuffModuleServices.cs |
| Inventory | InventoryModuleServices.cs |
| Combat | CombatModuleServices.cs |
| Formation | FormationModuleServices.cs |
| NPC | NPCModuleServices.cs |
| Player | PlayerModuleServices.cs |
| World | WorldModuleServices.cs |
| Quest | QuestModuleServices.cs |
| Interaction | InteractionModuleServices.cs |
| UI | UIModuleServices.cs |
| Save | SaveModuleServices.cs |

> Модуль Save изначально проектировался без отдельного LifetimeScope (ModuleServices pattern).

---

## 📋 Планы и задачи

### П.23: Привязка характеристик к телу/душе — реализация

**Приоритет:** HIGH

Разделить `IStatService` на домены:
- **Body (Тело):** STR, AGI, VIT — развиваются через физические действия; при потере части тела временно снижаются
- **Soul (Душа):** INT — развивается через медитацию и техники; НЕ зависит от состояния тела

**Ключевые решения:**
- Создать `StatDomain` enum и метод `GetStatDomain(StatType)`
- При потере части тела — дебафф к привязанным статам: `statPenalty = baseStat × limbContributionPercent`
- При приживлении конечности — восстановление стата
- INT НЕ зависит от состояния тела — даже безрукый практик сохраняет интеллект

> См. [ALGORITHMS.md](./ALGORITHMS.md) §П.23 — детали и формулы дебаффов

### П.24: Vitality → HP пересчёт при изменении стата

**Приоритет:** HIGH

BodyService подписывается на `StatChangedEvent` (VIT). При изменении VIT — пересчёт HP всех частей тела:

```
newMaxHP = baseHP × (1.0 + (newVit - 10) × 0.05)
damageRatio = currentDamage / oldMaxHP
newCurrentDamage = damageRatio × newMaxHP
```

**Ключевые решения:**
- Добавить `RecalculateHPFromVitality()` в IBodyService
- Уведомление UI через `BodyPartHealedEvent` / `BodyPartDamagedEvent`

> См. [ALGORITHMS.md](./ALGORITHMS.md) §П.24 — детали формул

### П.25: Стартовые характеристики — реализация

**Приоритет:** HIGH

Создать систему стартовых характеристик для разных видов:

**Задачи:**
1. Создать `SpeciesData` с базовыми статами для каждого вида
2. Создать `SpeciesRegistry` — реестр всех видов
3. При создании персонажа / NPC — получать статы из SpeciesData
4. Учитывать возраст (ребёнок → подросток → взрослый → старец)
5. Варианты пробуждения влияют на стартовые характеристики

**Таблица стартовых характеристик по видам:**

| Species | SoulType | STR | AGI | VIT | INT | Material | Size |
|---------|----------|-----|-----|-----|-----|----------|------|
| Human | Character | 10 | 10 | 10 | 10 | Organic | Medium |
| Elf | Character | 8 | 12 | 8 | 12 | Organic | Medium |
| Demon | Character | 14 | 10 | 12 | 8 | Organic | Medium |
| Giant | Character | 18 | 6 | 16 | 4 | Organic | Huge |
| Wolf | Creature | 8 | 14 | 10 | 4 | Organic | Medium |
| Tiger | Creature | 14 | 12 | 12 | 4 | Organic | Large |
| Dragon | Creature | 20 | 10 | 18 | 10 | Scaled | Huge |
| Phoenix | Creature | 8 | 16 | 8 | 12 | Ethereal | Large |
| Ghost | Spirit | — | — | — | 12 | Ethereal | — |
| Golem | Construct | 16 | 4 | 20 | 2 | Mineral | Large |

> См. [ALGORITHMS.md](./ALGORITHMS.md) §П.25 — полная таблица и формулы

---

## 📚 Связанные документы

| Документ | Описание |
|----------|----------|
| [ARCHITECTURE.md](./ARCHITECTURE.md) | Стабильная архитектура (финальный дизайн) |
| [ARCHITECTURE_CODE.md](./ARCHITECTURE_CODE.md) | Кодовая база (namespace, DI, шаблоны) |
| [ALGORITHMS.md](./ALGORITHMS.md) | Формулы и расчёты |
| [BODY_SYSTEM.md](./BODY_SYSTEM.md) | Система тела |
| [CHARGER_SYSTEM.md](./CHARGER_SYSTEM.md) | Система зарядников |
| [TILE_SYSTEM.md](./TILE_SYSTEM.md) | Система тайлов |
| [QI_SYSTEM.md](./QI_SYSTEM.md) | Система Ци |
| [BUFF_MODIFIERS_SYSTEM.md](./BUFF_MODIFIERS_SYSTEM.md) | Баффы/дебаффы, модификаторы |
| [COMBAT_SYSTEM.md](./COMBAT_SYSTEM.md) | Боевая система |
| [FORMATION_SYSTEM.md](./FORMATION_SYSTEM.md) | Система формаций |
| [NPC.md](./NPC.md) | NPC |
| [NPC_AI_SYSTEM.md](./NPC_AI_SYSTEM.md) | NPC AI |
| [INVENTORY_SYSTEM.md](./INVENTORY_SYSTEM.md) | Инвентарь |
| [EQUIPMENT_SYSTEM.md](./EQUIPMENT_SYSTEM.md) | Экипировка, грейды, прочность |
| [SAVE_SYSTEM.md](./SAVE_SYSTEM.md) | Сохранение |
| [WORLD_SYSTEM.md](./WORLD_SYSTEM.md) | Система мира |
| [SCENE_BUILDER_SYSTEM.md](./SCENE_BUILDER_SYSTEM.md) | Сборка сцены |
| [ENTITY_TYPES.md](./ENTITY_TYPES.md) | Типы сущностей |
| [ELEMENTS_SYSTEM.md](./ELEMENTS_SYSTEM.md) | Стихии |
| [TECHNIQUE_SYSTEM.md](./TECHNIQUE_SYSTEM.md) | Система техник |
| [GLOSSARY.md](./GLOSSARY.md) | Глоссарий |
| [STAT_THRESHOLD_SYSTEM.md](./STAT_THRESHOLD_SYSTEM.md) | Пороги развития |
| [MORTAL_DEVELOPMENT.md](./MORTAL_DEVELOPMENT.md) | Развитие смертных |
| [DATA_MODELS.md](./DATA_MODELS.md) | Модели данных |
| [CONFIGURATIONS.md](./CONFIGURATIONS.md) | Конфигурации |
| [UNIT_TEST_RULES.md](./UNIT_TEST_RULES.md) | Правила юнит-тестов |
| [!Ai_Skills.md](./!Ai_Skills.md) | Навыки AI-ассистента |
| [!LISTING.md](./!LISTING.md) | Листинг файлов проекта |

---

## Changelog

| Версия | Дата | Описание |
|--------|------|----------|
| v1.0 | 2026-05-11 | Создан из ARCHITECTURE.md v3.15 (разделение на стабильную архитектуру и реализацию) |

# Чекпоинт: Transfer Core/Messaging/Contracts from Ai-game3 to Ai-game4

**Дата:** 2026-08-15 (UTC)
**Сессия:** Task 7-c — transfer-core-messaging
**Тип:** migration

---

## Контекст

Идёт миграция Core-слоя из Ai-game3 (Unity-итерация) в Ai-game4 (Godot 4.7.1).
Core-слой — engine-agnostic чистый C#, переносится напрямую с минимальной адаптацией:
- namespace: `CultivationGame.Core.Messaging` → `CultivationGame.Core.Messaging.Contracts` (file-scoped)
- `using UnityEngine` удаляется
- `using MessagePipe` удаляется (не было в источнике)
- `#nullable enable` добавляется в каждый файл
- Все контракты остаются `readonly struct` с readonly fields (zero-GC)

Ai-game3 имеет 20 contract-файлов (~104 events). Ai-game4 имел 10 stub-файлов (~50 events).
Цель: заменить stubs версиями Ai-game3 + добавить недостающие 10 файлов.

## Что сделано

### Work Log (Task ID: 7-c, Agent: transfer-core-messaging)

**Files transferred (20 source files → 20 target files):**

Replaced (9 — существовавшие в Ai-game4 stub-файлы заменены версиями Ai-game3):
- `GameContracts.cs` — 7 events (GameStateChanged, GamePaused, GameResumed, SessionStarted, NewGameRequested, LoadGameRequested, QuitGameRequested) + `GameState` enum
- `SceneContracts.cs` — 6 events (SceneInitializing, SceneReady, SceneAssemblyFailed, SceneAssemblyCompletedWithErrors, ScenePhaseStarted, ScenePhaseCompleted)
- `WorldContracts.cs` — 11 events (TimeChanged, DayChanged, MonthChanged, YearChanged, TimeSpeedChanged, LocationChanged, TravelStarted, SceneTransitionRequest, SceneLoaded, WorldEventTriggered, WorldEventEnded)
- `PlayerContracts.cs` — 6 events (PlayerDeath, PlayerRevive, PlayerSleep, PlayerPositionChanged, StaminaChanged, CurrencyChanged)
- `QiContracts.cs` — 12 events (QiChanged, QiDepleted, QiFull, CultivationBreakthrough, CultivationLevelChanged, QiBufferActivated, QiBufferDeactivated, QiConsumeRequest, QiAddRequest, QiBufferActivateRequest, QiBufferDeactivateRequest, QiBufferStateChanged) + `QiBufferMode` enum
- `CombatContracts.cs` — 6 events (CombatStarted, CombatEnded, DamageApplied, TechniqueUsed, EnemyKilled, AttackIntent) + `Element`, `CombatAttackResult`, `CombatSubtype` enums
- `SaveContracts.cs` — 7 events (SaveRequested, LoadRequested, SaveCompleted, LoadCompleted, AutoSaveTriggered, SaveDeleted) + `SaveSlot` enum + `SaveInfo` struct
- `UIContracts.cs` — 10 events (UIStateChangeRequest, UIInteractRequest, UIAdvanceDialogueRequest, UISelectChoiceRequest, UISaveRequest, UILoadRequest, UIPauseRequest, UIResumeRequest, ToastShown, ModalShown)
- `InputContracts.cs` — 6 events (InputKey, InputAction, MouseInput, ClickToMove, ContextMenuRequested, TrackingTarget) + `InputKeyEventType`, `MouseButton` enums. Слит из исходных `InputLogContracts.cs` + `MouseContracts.cs` (Ai-game3 хранил их раздельно).

Added (11 — новых файлов, не существовавших в Ai-game4):
- `BodyContracts.cs` — 5 events (BodyPartDamaged, BodyPartSevered, BodyPartHealed, BodyPartReattached, BodyCritical) + `BodyPartState` enum
- `BuffContracts.cs` — 5 events (BuffApplied, BuffRemoved, BuffExpired, BuffTicked, StatModifierChanged) + `BuffType` enum
- `ChargerContracts.cs` — 5 events (ChargerStateChanged, ChargerOverheated, ChargerCooledDown, ChargerHeatChanged, ChargerBufferChanged) + `ChargerSlotState`, `HeatState` enums
- `FormationContracts.cs` — 5 events (FormationActivated, FormationDeactivated, FormationQiPoolChanged, FormationStageChanged, FormationContributeQiRequest) + `FormationStage` enum
- `InventoryContracts.cs` — 5 events (ItemAdded, ItemRemoved, EquipmentChanged, EquipmentBlocked, ItemAddRequest)
- `NPCContracts.cs` — 7 events (NPCSpawned, NPCDespawned, AttitudeChanged, NPCDeath, NPCInteracted, NPCAIStateChanged, NPCDamaged) + `NPCRole`, `Attitude`, `NPCAIState` enums
- `QuestContracts.cs` — 6 events (QuestStarted, QuestObjectiveUpdated, QuestCompleted, QuestFailed, QuestAbandoned, QuestRewardGranted) + `QuestType`, `QuestRewardType` enums
- `DialogueContracts.cs` — 4 events (DialogueStarted, DialogueEnded, DialogueChoiceSelected, InteractionCompleted)
- `CraftingContracts.cs` — 2 events (CraftCompleted, CraftFailed)
- `StatContracts.cs` — 1 event (StatChanged)
- `HotbarContracts.cs` — 2 events (TechniqueSlotSelected, HotbarSlotChanged)

Deleted (1 — Ai-game4-only stub, content now in proper module-specific files):
- `MiscContracts.cs` — содержал дубликаты events (BodyPartDamaged, BuffApplied, ItemAdded, NPCSpawned, QuestStarted, TimeSpeedChanged, TechniqueUsed и др.) и Ai-game4-only events (FormationCreated, TimeTick, ChargerRegistered, TechniqueLearned, QuestProgress). Все эти events либо перенесены в соответствующие модульные файлы (с rich-сигнатурами Ai-game3), либо будут переданы через модульные контракты Ai-game3.

**Total events count:** 118 `readonly struct` events (vs задача оценивала ~104).
- Дополнительно: 13 enums + 1 struct (`SaveInfo`) определены локально в contract-файлах.

**Build status:**
- `dotnet build` — 0 ошибок в `Core/Messaging/Contracts/*.cs` (все 20 файлов компилируются)
- 285 ошибок в Modules/Entry/Adapter/Core.Data/Core.Interfaces — EXPECTED (другие слои ещё не мигрированы, stub-коды ссылаются на удалённые типы/события)

### Stage Summary
- Core/Messaging files: 20 (было 10)
- Compile errors in contracts: 0
- New events added: 68 (118 − 50 существовавших = 68 новых; также 50 существовавших заменены богатыми версиями Ai-game3)

## Решения

- **namespace** адаптирован: `CultivationGame.Core.Messaging` → `CultivationGame.Core.Messaging.Contracts` (file-scoped) — соответствует существующему Ai-game4 паттерну.
- **Missing enums** добавлены локально в contract-файлы (в namespace `Contracts`): `GameState`, `SaveSlot`, `SaveInfo`, `QiBufferMode`, `Element`, `CombatAttackResult`, `CombatSubtype`, `BodyPartState`, `BuffType`, `FormationStage`, `NPCRole`, `Attitude`, `NPCAIState`, `QuestType`, `QuestRewardType`. Это соответствует Ai-game3 паттерну (ChargerSlotState/HeatState/InputKeyEventType/MouseButton уже определены inline в contract-файлах источника).
- **`Element` vs `ElementType`**: Ai-game4 уже имеет `ElementType` в `Core.Data.Enums`. Добавлен отдельный `Element` enum в CombatContracts.cs (более богатый: Neutral + 5 стихий + Light/Dark/Yin/Yang), потому что Ai-game3 source использует именно `Element`. Modules при миграции будут использовать `Element`; `ElementType` остаётся для engine-internal классификации (без конфликта — разные имена/namespace).
- **InputContracts.cs merged**: Ai-game3 source имел `InputLogContracts.cs` и `MouseContracts.cs` раздельно. Задача указывает «InputContracts.cs (includes InputLogContracts + MouseContracts)» — объединено в один файл `InputContracts.cs` (существовавший stub заменён). Содержит оба enum (`InputKeyEventType`, `MouseButton`) и все 6 events.
- **MiscContracts.cs удалён**: Ai-game4-only stub с дублирующимися events. Полная замена Ai-game3 версиями подразумевает, что Ai-game4-only events (FormationCreated, TimeTick, ChargerRegistered, TechniqueLearned, QuestProgress) теряются — EXPECTED, Ai-game3 не имеет аналогов, а модули Ai-game3 используют вместо них FormationActivated, TimeChanged, ChargerStateChanged, TechniqueUsed, QuestObjectiveUpdated соответственно.
- **TileContracts.cs НЕ переносился**: не в списке 20 файлов задачи. Ai-game3 имеет TileContracts.cs (5 events: TileChanged, ResourceHarvested, ResourceDepleted, TileMapGenerated, ResourceRespawned + HarvestResult struct). Когда Tile-модуль будет переноситься, эти контракты нужно будет добавить отдельно.
- **EntityId как `string`**: Ai-game3 использует `string EntityId` (GUIDs/IDs строками), Ai-game4 stubs использовали `int EntityId`. Перенесены Ai-game3 версии со `string` — модули/Entry/Adapter при миграции получат строки. Это вызовет temporary compile errors в существующих Ai-game4 stub-модулях (expected).

## Найденные проблемы

- **Ai-game4 Enums.cs неполный**: отсутствуют `Direction`, `WaterType`, `TechniqueSubtype`, `ElementType`, `Season` references в `Core/Data/Structs.cs` и `Core/Data/DataModels.cs`. Это не относится к contracts (это Core.Data), но влияет на общий build. EXPECTED — отдельный task по переносу Core.Data.
- **`StatModifierChangedEvent` определён в BuffContracts.cs**, не в StatContracts.cs. Соответствует источнику Ai-game3 — buff-модуль публикует изменения модификаторов.
- **`EquipmentSlot[]` в `BodyPartSeveredEvent`**: массив как поле readonly struct — ссылочный тип, но аллоцируется один раз, zero-GC паттерн сохранён (как в источнике).

## Следующие шаги

- Перенос Core.Data (Enums.cs, Structs.cs, DataModels.cs, Constants.cs) из Ai-game3 — заполнит недостающие типы (`BodyPartState`, `ElementType` и др. можно будет унифицировать).
- Перенос Core.Interfaces (24+ интерфейсов) — многие Ai-game4 stub-интерфейсы ссылаются на удалённые типы.
- Перенос Modules (Calculators + Configs + Services) с адаптацией MessagePipe→EventBus и VContainer→наш DI.
- Перенос Tests.
- После миграции Core.Data — переместить локальные enums из contract-файлов в `Core.Data.Enums` (где они должны быть по Ai-game3 структуре), удалив дубликаты из Contracts.

## Файлы

Созданные/изменённые:
- `game/src/Core/Messaging/Contracts/GameContracts.cs` — replaced
- `game/src/Core/Messaging/Contracts/SceneContracts.cs` — replaced
- `game/src/Core/Messaging/Contracts/WorldContracts.cs` — replaced
- `game/src/Core/Messaging/Contracts/PlayerContracts.cs` — replaced
- `game/src/Core/Messaging/Contracts/InputContracts.cs` — replaced (merged InputLog+Mouse)
- `game/src/Core/Messaging/Contracts/QiContracts.cs` — replaced
- `game/src/Core/Messaging/Contracts/CombatContracts.cs` — replaced
- `game/src/Core/Messaging/Contracts/SaveContracts.cs` — replaced
- `game/src/Core/Messaging/Contracts/UIContracts.cs` — replaced
- `game/src/Core/Messaging/Contracts/BodyContracts.cs` — new
- `game/src/Core/Messaging/Contracts/BuffContracts.cs` — new
- `game/src/Core/Messaging/Contracts/ChargerContracts.cs` — new
- `game/src/Core/Messaging/Contracts/FormationContracts.cs` — new
- `game/src/Core/Messaging/Contracts/InventoryContracts.cs` — new
- `game/src/Core/Messaging/Contracts/NPCContracts.cs` — new
- `game/src/Core/Messaging/Contracts/QuestContracts.cs` — new
- `game/src/Core/Messaging/Contracts/DialogueContracts.cs` — new
- `game/src/Core/Messaging/Contracts/CraftingContracts.cs` — new
- `game/src/Core/Messaging/Contracts/StatContracts.cs` — new
- `game/src/Core/Messaging/Contracts/HotbarContracts.cs` — new

Удалённые:
- `game/src/Core/Messaging/Contracts/MiscContracts.cs` — deleted (Ai-game4 stub, superseded)

---

Task ID: 7-c
Agent: transfer-core-messaging
Task: Transfer Core/Messaging/Contracts from Ai-game3 to Ai-game4

Work Log:
- Files transferred: 20 (9 replaced + 11 added; 1 deleted: MiscContracts.cs)
- Files replaced: GameContracts, SceneContracts, WorldContracts, PlayerContracts, InputContracts (merged InputLog+Mouse), QiContracts, CombatContracts, SaveContracts, UIContracts
- Files added: BodyContracts, BuffContracts, ChargerContracts, FormationContracts, InventoryContracts, NPCContracts, QuestContracts, DialogueContracts, CraftingContracts, StatContracts, HotbarContracts
- Total events count: 118 readonly struct events (vs ~104 expected)
- Build status: 0 errors in Core/Messaging/Contracts/*.cs; 285 errors in Modules/Entry/Adapter/Core.Data/Core.Interfaces (EXPECTED per task)

Stage Summary:
- Core/Messaging files: 20 (was 10)
- Compile errors in contracts: 0
- New events added: 68 (118 total − 50 pre-existing = 68 net new; all 50 existing events also replaced with richer Ai-game3 versions)

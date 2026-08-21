# Аудит 1: Core Layer

**Дата:** 2026-08-21 13:15 UTC
**Сессия:** web-d86b1055
**Тип:** audit (research only)
**Task ID:** AUDIT-1

---

## Сводка

- **Файлов проверено:** 89 (24 Data + 35 Interfaces + 26 Messaging/Contracts + 2 DI + 1 Events + 1 CoreProjectInfo)
- **Проблем найдено:** 33 (critical: 3, major: 11, minor: 19)
- **Архитектурных нарушений:** 2 (Core → Modules.NPC.Data, Core → Modules.Body)
- **Godot/Unity импортов в Core:** 0 (только в комментариях)

---

## CRITICAL проблемы

### C1: Core→Module зависимость (INPCService)
- **Файл:** `Core/Interfaces/INPCService.cs:7`
- **Проблема:** `using CultivationGame.Modules.NPC.Data;` — `INPCService.GetNPCState()` возвращает `NPCState` из Modules
- **Нарушение:** ARCHITECTURE.md §2.1 "Modules depend on Core, not vice versa"
- **Решение:** Перенести `NPCState` (чистый DTO, 144 строки) в `Core/Data/NPCState.cs`

### C2: Core→Module зависимость (IBodyDataProvider)
- **Файл:** `Core/Interfaces/IBodyDataProvider.cs:8`
- **Проблема:** `using CultivationGame.Modules.Body;` — возвращает `List<BodyPart>` из Modules
- **Решение:** Перенести `BodyPart` в `Core/Data/BodyPart.cs` (272 строки, нет engine deps)

### C3: EventBus re-entrancy → StackOverflow
- **Файл:** `Core/Events/EventBus.cs:47-61`
- **Проблема:** Если handler публикует событие того же типа → бесконечная рекурсия → StackOverflowException
- **Решение:** `[ThreadStatic] HashSet<Type> _publishing` + queue re-entrant или throw

---

## MAJOR проблемы

### M1: ObjectDefaults silent failure
- **Файл:** `Core/Data/ObjectDefaults.cs:163-166`
- **Проблема:** `Get(ObjectType)` возвращает `default(ObjectInfo)` для неизвестного типа → `MoveCostModifier=0` → тайл становится непроходимым
- **Решение:** Parameterless ctor с `MoveCostModifier=1.0f` или defensive default

### M2: GameTile mutable struct (BD-48)
- **Файл:** `Core/Data/GameTile.cs:22-45`
- **Проблема:** 15 public mutable fields — нарушает собственное правило BD-48
- **Решение:** `readonly struct` с `init` accessors + `With*()` factory methods

### M3: TechniqueData UltimateQiCostMultiplier
- **Файл:** `Core/Data/TechniqueData.cs:92`
- **Проблема:** Код = 1.5f, док TECHNIQUE_SYSTEM.md §9.1 = ×2.0
- **Решение:** Подтвердить у пользователя (балансовое число)

### M4: AttackType enum vs LevelSuppressionTable
- **Файл:** `Core/Data/Enums.cs:789-797`
- **Проблема:** 6 значений enum vs 3 колонки в таблице → IndexOutOfRangeException
- **Решение:** Explicit mapping method `ToSuppressionIndex(AttackType)`

### M5: TechniqueData comments неверные
- **Файл:** `Core/Data/TechniqueData.cs:9,22,68`
- **Проблема:** Комментарии говорят `qiCost = capacity × 0.15`, док говорит `qiCost = floor(baseCapacity × 2^(level-1))`
- **Решение:** Исправить комментарии

### M6: InventorySlot два непересекающихся конструктора
- **Файл:** `Core/Data/Structs.cs:216-234`
- **Проблема:** Один ctor ставит Weight/Volume, другой — Category/Rarity; данные теряются
- **Решение:** Единый ctor с 6 параметрами

### M7: DI Container greediest-ctor non-deterministic
- **Файл:** `Core/DI/Container.cs:229-231`
- **Проблема:** При равенстве параметров `First()` выбирает недетерминированно
- **Решение:** Throw при tie или alphabetical tiebreaker

### M8: DI Register silent overwrite
- **Файл:** `Core/DI/Container.cs:43`
- **Проблема:** Дубликат регистрации молча перезаписывает
- **Решение:** Throw `InvalidOperationException` на дубликате

### M9: Constants dual _PERMIL/float tables
- **Файл:** `Core/Data/Constants.cs`
- **Проблема:** Каждая float таблица имеет _PERMIL копию — двойная поддержка
- **Решение:** Генерить _PERMIL из float при старте ИЛИ удалить float (ЗАПРЕТ 3.9)

### M10: EventBus snapshot allocation per Add/Remove
- **Файл:** `Core/Events/EventBus.cs:124-134`
- **Проблема:** `new List<>` при каждой мутации — GC pressure
- **Решение:** ImmutableArray copy-on-write

### M11: MorphologyHitTables неполные
- **Файл:** `Core/Data/Constants.cs:623`
- **Проблема:** 6 из 10 морфологий (отсутствуют 4 гибридных: Centaur, Mermaid, Harpy, Lamia)
- **Решение:** Добавить таблицы (TBD в комментарии)

---

## Концептуальные расхождения (требуют решения пользователя)

| # | Код | Документация | Вопрос |
|---|-----|--------------|--------|
| 1 | `UltimateQiCostMultiplier = 1.5f` | TECHNIQUE_SYSTEM.md §9.1 = ×2.0 | Какое значение каноничное? |
| 2 | `INPCService` возвращает `NPCState` из Modules | ARCHITECTURE.md запрещает Core→Modules | Перенести NPCState в Core ИЛИ интерфейс INPCStateView? |
| 3 | `IBodyDataProvider` возвращает `BodyPart` из Modules | То же нарушение | Перенести BodyPart в Core ИЛИ интерфейс IBodyPartView? |
| 4 | `MorphologyHitTables` 6/10 морфологий | BODY_SYSTEM.md описывает 10 | Гибриды используют Humanoid (TBD) или добавить таблицы? |

---

## MINOR проблемы (19 шт.)

- Enums: legacy aliases (BiomeType.Plains=Grassland), gap в MortalStage (6-8), TechniqueSubtype дублирует CombatSubtype
- Stale Unity references в комментариях (ISceneAssemblyPhase.cs:92, IInteractionService.cs:3)
- IPlayerInputService: 25+ boolean Is*Pressed — flag explosion
- ITimeService: Pause()/Resume() дублируют Speed=Paused
- ChargerContracts: CurrentQi float vs long (нарушение ЗАПРЕТ 2)
- PlayerContracts: PlayerPositionChangedEvent (float) vs PlayerMovedEvent (Position2D)
- ValueNoise: weak avalanche, hardcoded warp offsets
- SeededRandom: modulo bias
- ItemData/EquipmentData: public fields вместо properties
- DI: ResolveAll allocates HashSet per call, InjectProperties не документирует readonly
- DIInterfaces.cs consolidated vs FILE_TREE.md spec (4 файла)
- Permil: overflow risk
- FormationEnvironmentMultipliers: string keys вместо enum
- CombatSubtype: отсутствует DefenseReflect (по доку)
- FILE_TREE.md: spec отстал (28 интерфейсов vs 35 реальных)

---

## План исправлений (по приоритету)

### P0 — Архитектурные блокеры (до Module аудита)
1. Перенести `NPCState` → `Core/Data/NPCState.cs`
2. Перенести `BodyPart` → `Core/Data/BodyPart.cs`
3. Исправить EventBus re-entrancy

### P1 — Корректность
4. ObjectDefaults defensive default (MoveCostModifier=1.0f)
5. Подтвердить UltimateQiCostMultiplier (пользователь решает)
6. AttackType → suppression index mapping
7. Исправить комментарии TechniqueData

### P2 — Дизайн
8. GameTile → readonly struct
9. InventorySlot unified constructor
10. DI Register strict (throw on duplicate)
11. DI greediest-ctor deterministic

### P3 — Документация
12. Обновить stale Unity references
13. Обновить FILE_TREE.md

### P4 — Производительность
14. EventBus immutable array
15. ResolveAll cache

---

## Файлы

- **Аудит:** этот файл
- **Полный отчёт:** `/home/z/my-project/worklog.md` (Task AUDIT-1)
- **План исправлений:** `checkpoints/08_21_audit1_core_plan.md` (следующий файл)

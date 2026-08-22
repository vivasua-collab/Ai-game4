# Аудит 1: Core Layer — ПОДРОБНЫЙ

**Дата:** 2026-08-22 (переработан)
**Task ID:** AUDIT-1
**Scope:** Core слой (engine-agnostic) — Data, Interfaces, Messaging, DI, Events

---

## Сводка

- **Файлов проверено:** 89
- **Проблем найдено:** 33 (critical: 3, major: 11, minor: 19)
- **Архитектурных нарушений:** 2
- **Godot/Unity импортов в Core:** 0 (только в комментариях)

**ВАЖНО:** Документация НЕ редактируется. Все "не реализовано" — будет реализовано позже (начальный этап проекта, отладка ядра). Этот аудит фиксирует только реальные проблемы в коде, требующие решения.

---

## CRITICAL проблемы (3)

### C1: Core→Module зависимость — INPCService возвращает NPCState из Modules

**Файл:** `game/src/Core/Interfaces/INPCService.cs:7`

**Что происходит:**
Файл `INPCService.cs` находится в Core (движок-независимый слой). Но он содержит:
```csharp
using CultivationGame.Modules.NPC.Data;  // ← Core импортирует из Modules!
```
Метод `GetNPCState(string npcId)` возвращает тип `NPCState`, который определён в `Modules/NPC/Data/NPCState.cs`.

**Почему это проблема:**
Архитектура проекта (ARCHITECTURE.md §2.1) требует: "Modules depend on Core, not vice versa". Core — фундамент, он не должен знать о существовании модулей. Если Core ссылается на Modules, получается циклическая зависимость: Core→Modules→Core. Это ломает принцип Hub-and-Spoke (модули общаются только через EventBus).

**Варианты решения:**

**Вариант A (рекомендую): Перенести NPCState в Core/Data/NPCState.cs**
- NPCState — это чистый DTO (Data Transfer Object): 144 строки, только данные, нет логики, нет зависимостей от Godot или других модулей
- Перенос: вырезать файл из `Modules/NPC/Data/`, вставить в `Core/Data/`
- Обновить `using` во всех файлах, которые ссылаются на NPCState
- **Плюс:** полностью устраняет нарушение архитектуры, NPCState доступен всем модулям через Core
- **Минус:** нужно обновить ~10 файлов с `using CultivationGame.Modules.NPC.Data` → `using CultivationGame.Core.Data`
- **Время:** ~30 минут

**Вариант B: Создать интерфейс INPCStateView в Core**
- В Core определить интерфейс `INPCStateView` (только свойства для чтения)
- `NPCState` в Modules реализует этот интерфейс
- `INPCService.GetNPCState()` возвращает `INPCStateView`, не `NPCState`
- **Плюс:** NPCState остаётся в Modules (если там есть логика, которую нельзя переносить)
- **Минус:** усложняет код, нужно приводить типы при использовании
- **Время:** ~1 час

**Вариант C: Оставить как есть (отложить)**
- Нарушение архитектуры остаётся, но не блокирует работу
- **Риск:** при росте проекта циклические зависимости умножаются
- **Время:** 0

**Моя рекомендация:** Вариант A — NPCState это просто данные, перенос безопасен и решает проблему полностью.

---

### C2: Core→Module зависимость — IBodyDataProvider возвращает BodyPart из Modules

**Файл:** `game/src/Core/Interfaces/IBodyDataProvider.cs:8`

**Что происходит:**
```csharp
using CultivationGame.Modules.Body;  // ← Core импортирует из Modules!
```
Метод `GetBodyParts(string entityId)` возвращает `List<BodyPart>`, где `BodyPart` определён в `Modules/Body/BodyPart.cs`.

**Почему это проблема:**
То же нарушение, что и C1. Core не должен зависеть от Modules.Body.

**Варианты решения:**

**Вариант A (рекомендую): Перенести BodyPart в Core/Data/BodyPart.cs**
- BodyPart — 272 строки, содержит методы (TakeDamage, Heal, SetHP), но НЕ использует Godot или другие модули
- Перенос безопасен: все зависимости BodyPart уже в Core (Core.Data типы)
- **Плюс:** устраняет нарушение, BodyPart доступен всем через Core
- **Минус:** обновить ~8 файлов с using
- **Время:** ~30 минут

**Вариант B: Интерфейс IBodyPartView в Core**
- Аналогично C1 Вариант B
- **Время:** ~1 час

**Вариант C: Отложить**
- **Время:** 0

**Моя рекомендация:** Вариант A — BodyPart не имеет engine-зависимостей, перенос решает проблему.

---

### C3: EventBus re-entrancy — риск StackOverflow

**Файл:** `game/src/Core/Events/EventBus.cs:47-61`

**Что происходит:**
EventBus публикует событие → вызывает все подписчики. Если подписчик ВНУТРИ своего обработчика публикует событие ТОГО ЖЕ типа, EventBus вызывает подписчиков снова → включая этот же обработчик → бесконечная рекурсия → `StackOverflowException` → краш игры.

**Пример сценария:**
```csharp
// CombatService подписан на DamageAppliedEvent
// В обработчике публикует CounterAttackEvent
// CounterAttackEvent триггерит ещё один DamageAppliedEvent
// → бесконечная рекурсия → StackOverflow
```

**Почему это проблема:**
StackOverflowException невозможно перехватить (в .NET это краш процесса). Игра молча закрывается без логов.

**Варианты решения:**

**Вариант A (рекомендую): Queue re-entrant events**
- Добавить `[ThreadStatic] private static HashSet<Type>? _publishing;`
- Перед публикацией: если тип уже в `_publishing` → добавить в очередь `_pending`, НЕ вызывать подписчиков сразу
- После завершения текущей публикации: обработать очередь
- **Плюс:** события не теряются, нет рекурсии
- **Минус:** события обрабатываются чуть позже (в конце текущей публикации)
- **Время:** ~1 час

**Вариант B: Throw на re-entrancy**
- Если тип уже публикуется → `throw new InvalidOperationException("Re-entrant publish")`
- **Плюс:** явно выявляет ошибку в логике
- **Минус:** игра крашится (но с понятным сообщением)
- **Время:** ~15 минут

**Вариант C: Отложить (текущее состояние)**
- Риск краша при определённых сценариях combat/buff
- **Время:** 0

**Моя рекомендация:** Вариант A — надёжное решение, события не теряются.

---

## MAJOR проблемы (11)

### M1: ObjectDefaults — молча делает тайл непроходимым для неизвестного ObjectType

**Файл:** `game/src/Core/Data/ObjectDefaults.cs:163-166`

**Что происходит:**
`ObjectDefaults.Get(ObjectType)` ищет тип в словаре. Если не находит — возвращает `default(ObjectInfo)`. Проблема: `default(ObjectInfo)` для `readonly struct` оставляет все поля в значениях по умолчанию: `MoveCostModifier = 0f` (не 1.0f).

Если добавить новый `ObjectType` (например, `ObjectType.Stump`) и забыть добавить запись в `ObjectDefaults.Entries`, то:
1. `GetMoveCostModifier(Stump)` вернёт `0f`
2. `GameTile.EffectiveMoveCost` вычислит `terrainCost × 0 = 0`
3. `GameTile` трактует `moveCost <= 0` как "непроходимо"
4. **Тайл становится непроходимым без предупреждения**

**Варианты решения:**

**Вариант A (рекомендую): Defensive default = 1.0f**
- В `GetMoveCostModifier` возвращать `1.0f` если `TryGet` не нашёл тип
- `Get(ObjectType)` возвращать `ObjectInfo` с `MoveCostModifier=1.0f` для неизвестных
- **Плюс:** тайл остаётся проходимым, нет молчаливого блока
- **Время:** ~10 минут

**Вариант B: Throw на неизвестный тип**
- `Get(ObjectType)` → `throw new InvalidOperationException($"Unknown ObjectType: {type}")`
- **Плюс:** выявляет ошибку при разработке
- **Минус:** игра крашится если забыли добавить тип
- **Время:** ~5 минут

**Вариант C: Отложить**
- Риск молчаливых непроходимых тайлов
- **Время:** 0

**Моя рекомендация:** Вариант A — безопасный default, не крашит игру.

---

### M2: GameTile — mutable struct (нарушает BD-48)

**Файл:** `game/src/Core/Data/GameTile.cs:22-45`

**Что происходит:**
`GameTile` — это `struct` (значимый тип) с 15 public mutable fields. Struct копируется при присваивании, поэтому:
```csharp
var tile = _grid[x, y];  // копия
tile.ResourceAmount = 5;  // изменяет КОПИЮ, не оригинал!
// _grid[x, y].ResourceAmount всё ещё старое значение
```
Только `SetTile(x, y, in tile)` обновляет оригинал в `_grid`.

Проект сам задокументировал это как антипаттерн BD-48 (в DI_AND_EVENTBUS.md §4).

**Варианты решения:**

**Вариант A: readonly struct + factory methods**
- Сделать все поля `readonly` с `init` accessor
- Добавить методы `WithResource(float amount)`, `WithObject(ObjectType obj)` которые возвращают новую struct
- `SetTile` принимает `in GameTile` (zero-copy)
- **Плюс:** невозможность случайно изменить копию
- **Минус:** нужно переписать ~20 мест где tile mutates
- **Время:** ~2 часа

**Вариант B: Convert to class**
- `GameTile` становится `class` (ссылочный тип)
- `_grid[x, y]` возвращает ссылку, изменения видны сразу
- **Плюс:** проще использовать
- **Минус:** GC pressure (250k классов для 500×500 карты), потеря cache locality
- **Время:** ~1 час

**Вариант C: Отложить**
- Текущий код работает, но рискованно при модификациях
- **Время:** 0

**Моя рекомендация:** Вариант A — сохраняет performance (struct, zero-copy), устраняет баг.

---

### M3: TechniqueData UltimateQiCostMultiplier — ИСПРАВЛЕНО ✅

**Файл:** `game/src/Core/Data/TechniqueData.cs:92`

**Статус:** Уже исправлено в коммите `5d51a9f` (1.5f → 2.0f per TECHNIQUE_SYSTEM.md §9.1).

---

### M4: AttackType enum vs LevelSuppressionTable — риск IndexOutOfRange

**Файл:** `game/src/Core/Data/Enums.cs:789-797` + `Constants.cs:215`

**Что происходит:**
`AttackType` enum имеет 6 значений:
```
Normal=0, MeleeStrike=1, MeleeWeapon=2, Ranged=3, Technique=4, Ultimate=5
```

`LevelSuppressionTable` имеет 3 колонки: `[normal=0, technique=1, ultimate=2]`.

Если код делает `LevelSuppressionTable[diff][(int)attackType]` для `Technique` (4) или `Ultimate` (5) → `IndexOutOfRangeException` → краш.

**Варианты решения:**

**Вариант A (рекомендую): Helper method для маппинга**
```csharp
public static int ToSuppressionIndex(AttackType type) => type switch {
    AttackType.Technique => 1,
    AttackType.Ultimate => 2,
    _ => 0  // Normal, MeleeStrike, MeleeWeapon, Ranged
};
```
- Использовать `LevelSuppressionTable[diff][ToSuppressionIndex(attackType)]` везде
- **Плюс:** явный маппинг, нет риска краша
- **Время:** ~20 минут

**Вариант B: Разделить enum**
- `NormalAttackType { Normal, MeleeStrike, MeleeWeapon, Ranged }` → index 0
- `SpecialAttackType { Technique, Ultimate }` → index 1, 2
- **Плюс:** типобезопасно
- **Минус:** ломает существующий код
- **Время:** ~1 час

**Вариант C: Отложить**
- Риск краша если код использует прямой индекс
- **Время:** 0

**Моя рекомендация:** Вариант A — минимальные изменения, безопасно.

---

### M5: TechniqueData comments — ИСПРАВЛЕНО ✅

**Статус:** Уже исправлено в коммите `5d51a9f`.

---

### M6: InventorySlot — два конструктора теряют данные

**Файл:** `game/src/Core/Data/Structs.cs:216-234`

**Что происходит:**
`InventorySlot` имеет два конструктора:
1. `InventorySlot(itemId, count, weight, volume)` — ставит Weight/Volume, но Category=Rarity=default
2. `InventorySlot(itemId, count, category, rarity)` — ставит Category/Rarity, но Weight=Volume=0

Если вызвать конструктор 2, то вес и объём предмета теряются (0). Это приводит к неправильному подсчёту веса инвентаря.

**Варианты решения:**

**Вариант A (рекомендую): Единый конструктор с 6 параметрами**
```csharp
public InventorySlot(string itemId, int count, float weight, float volume, 
                     ItemCategory category, ItemRarity rarity)
```
- Удалить старые конструкторы или пометить `[Obsolete]`
- Обновить все вызовы (≈5 мест)
- **Плюс:** данные не теряются
- **Время:** ~30 минут

**Вариант B: Отложить**
- Текущий код может работать если все используют конструктор 1
- **Риск:** новый разработчик может использовать конструктор 2
- **Время:** 0

**Моя рекомендация:** Вариант A — устраняет риск потери данных.

---

### M7: DI Container — greediest-ctor non-deterministic

**Файл:** `game/src/Core/DI/Container.cs:229-231`

**Что происходит:**
При выборе конструктора для инъекции, DI выбирает тот, у которого больше параметров. Если два конструктора имеют одинаковое количество параметров, `OrderByDescending(...).First()` выбирает случайный (зависит от реализации .NET).

**Варианты решения:**

**Вариант A (рекомендую): Throw при неоднозначности**
- Если несколько конструкторов имеют максимальное число параметров → `throw new InvalidOperationException("Ambiguous constructors")`
- **Плюс:** выявляет ошибку при разработке
- **Время:** ~15 минут

**Вариант B: Детерминированный tiebreaker**
- Сортировать по имени типа параметров (алфавит)
- **Плюс:** предсказуемо
- **Минус:** может выбрать не тот конструктор
- **Время:** ~20 минут

**Вариант C: Отложить**
- Риск недетерминированного поведения между версиями .NET
- **Время:** 0

**Моя рекомендация:** Вариант A — явная ошибка лучше молчаливого выбора.

---

### M8: DI Register — молча перезаписывает дубликаты

**Файл:** `game/src/Core/DI/Container.cs:43`

**Что происходит:**
`Register<TInterface, TImpl>()` использует `_registrations[typeof(TInterface)] = reg;`. Если интерфейс уже зарегистрирован, новая регистрация молча заменяет старую.

**Варианты решения:**

**Вариант A (рекомендую): Throw на дубликат**
```csharp
if (_registrations.ContainsKey(typeof(TInterface)))
    throw new InvalidOperationException($"Service {typeof(TInterface)} already registered");
_registrations[typeof(TInterface)] = reg;
```
- **Плюс:** выявляет wiring баги при старте
- **Время:** ~10 минут

**Вариант B: Log warning + overwrite**
- Предупреждать в лог, но не крашить
- **Время:** ~10 минут

**Вариант C: Отложить**
- Дубликаты могут маскировать баги
- **Время:** 0

**Моя рекомендация:** Вариант A — выявляет ошибки на раннем этапе.

---

### M9: Constants — двойные таблицы (_PERMIL и float)

**Файл:** `game/src/Core/Data/Constants.cs`

**Что происходит:**
Для каждой боевой таблицы есть две версии: float и _PERMIL (integer × 1000). Например:
- `LevelSuppressionTable` (float)
- `LevelSuppressionTablePermil` (int)

Если обновить одну и забыть вторую — они расходятся.

**Варианты решения:**

**Вариант A (рекомендую): Генерировать _PERMIL из float при старте**
```csharp
public static readonly int[][] LevelSuppressionTablePermil = 
    LevelSuppressionTable.Select(row => row.Select(v => (int)(v * 1000)).ToArray()).ToArray();
```
- **Плюс:** одна таблица, автоматическая синхронизация
- **Минус:** `readonly` вместо `const` (микро-overhead)
- **Время:** ~30 минут

**Вариант B: Удалить float таблицы**
- Оставить только _PERMIL (ЗАПРЕТ 3.9 требует integer math)
- **Минус:** потеря читаемости (1500 vs 1.5f)
- **Время:** ~1 час

**Вариант C: Отложить**
- Риск расхождения таблиц
- **Время:** 0

**Моя рекомендация:** Вариант A — лучшее из обоих миров.

---

### M10: EventBus — allocation на каждый Subscribe/Unsubscribe

**Файл:** `game/src/Core/Events/EventBus.cs:124-134`

**Что происходит:**
При каждой подписке/отписке создаётся `new List<MessageHandler<T>>` для snapshot. Если подписки меняются каждый кадр (buff system), это создаёт GC pressure.

**Варианты решения:**

**Вариант A (рекомендую): ImmutableArray copy-on-write**
- Использовать `ImmutableArray<MessageHandler<T>>` вместо `List`
- При Add/Remove: создать новый ImmutableArray (copy-on-write)
- Чтение: zero-allocation
- **Плюс:** нет allocation при чтении, минимальный при записи
- **Время:** ~1 час

**Вариант B: Отложить**
- Сейчас подписки меняются редко (только при старте)
- **Время:** 0

**Моя рекомендация:** Вариант B (отложить) — на начальном этапе подписки статичны, оптимизация не нужна. Вернуться когда buff system начнёт динамически подписываться.

---

### M11: MorphologyHitTables — 6 из 10 морфологий

**Файл:** `game/src/Core/Data/Constants.cs:623`

**Что происходит:**
BODY_SYSTEM.md описывает 10 морфологий (Humanoid, Quadruped, Bird, Serpentine, Arthropod, Amorphous + 4 гибридных: Centaur, Mermaid, Harpy, Lamia). В коде только 6 таблиц. В комментарии написано "TBD".

**Варианты решения:**

**Вариант A: Реализовать позже (текущий статус)**
- Гибридные морфологии сейчас не используются (нет NPC с такими морфологиями)
- Добавить когда будут нужны
- **Время:** 0

**Вариант B: Реализовать сейчас**
- Добавить 4 таблицы на основе Ai-game3-ref
- **Время:** ~2 часа

**Моя рекомендация:** Вариант A — отложить до появления гибридных NPC. Документация НЕ редактируется, это "будет реализовано позже".

---

## Концептуальные вопросы для пользователя (2)

### Q1: NPCState — где должен жить?
- **Вариант A:** Перенести в Core/Data (рекомендую — это DTO)
- **Вариант B:** Интерфейс INPCStateView в Core
- **Вариант C:** Оставить в Modules (нарушение архитектуры)

### Q2: BodyPart — где должен жить?
- **Вариант A:** Перенести в Core/Data (рекомендую — нет engine deps)
- **Вариант B:** Интерфейс IBodyPartView в Core
- **Вариант C:** Оставить в Modules (нарушение архитектуры)

---

## MINOR проблемы (19) — можно отложить

| # | Проблема | Влияние | Приоритет |
|---|----------|---------|-----------|
| 1 | BiomeType legacy aliases (Plains=Grassland) | Запутывает, но работает | Позже |
| 2 | MortalStage gap (6-8 unused) | Итерация может попасть на undefined | Позже |
| 3 | TechniqueSubtype дублирует CombatSubtype | Двойная поддержка | Позже |
| 4 | Stale Unity references в комментариях | Косметика | Позже |
| 5 | IPlayerInputService 25+ boolean flags | Флаг-эксплозия | Позже |
| 6 | ITimeService Pause()/Resume() дублируют Speed | Косметика | Позже |
| 7 | ChargerContracts CurrentQi float | ЗАПРЕТ 2 нарушение | Позже |
| 8 | PlayerContracts двойные события | Риск double-handling | Позже |
| 9 | ValueNoise weak avalanche | Возможны артефакты | Позже |
| 10 | SeededRandom modulo bias | Микро-несправедливость | Позже |
| 11 | ItemData public fields | Косметика | Позже |
| 12 | DI ResolveAll HashSet allocation | Микро-perf | Позже |
| 13 | DIInterfaces.cs consolidated vs FILE_TREE | Косметика | Позже |
| 14 | Permil overflow risk | Теоретический | Позже |
| 15 | FormationEnvironment string keys | Косметика | Позже |
| 16 | CombatSubtype missing DefenseReflect | По доку | Позже |
| 17 | FILE_TREE.md отстал (28 vs 35) | Документация | НЕ редактируется |
| 18 | EventBus snapshot allocation | Perf | Позже |
| 19 | DI InjectProperties readonly docs | Косметика | Позже |

---

## План исправлений

### УЖЕ ИСПРАВЛЕНО ✅ (коммит 5d51a9f)
- M3: UltimateQiCostMultiplier 1.5→2.0
- M5: TechniqueData comments

### P0 — Критические (рекомендую выполнить)
1. C1: NPCState → Core (зависит от решения Q1)
2. C2: BodyPart → Core (зависит от решения Q2)
3. C3: EventBus re-entrancy protection
4. M1: ObjectDefaults defensive default
5. M4: AttackType → suppression index mapping

### P1 — Важные
6. M6: InventorySlot unified constructor
7. M7: DI greediest-ctor throw on tie
8. M8: DI Register throw on duplicate
9. M9: Constants _PERMIL generation

### P2 — Отложить
10. M2: GameTile readonly struct
11. M10: EventBus ImmutableArray
12. M11: MorphologyHitTables (позже, когда нужны гибриды)
13. Все MINOR

---

## Файлы

- **Аудит:** этот файл
- **Полный отчёт:** `/home/z/my-project/worklog.md` (Task AUDIT-1)
- **План исправлений:** `checkpoints/08_22_audit_fix_plan.md`

# Аудит 2: Modules Layer — ПОДРОБНЫЙ

**Дата:** 2026-08-22 (переработан)
**Task ID:** AUDIT-2
**Scope:** 16 игровых модулей

---

## Сводка

- **Модулей проверено:** 16 (все)
- **Проблем найдено:** 48 (critical: 4, major: 14, minor: 30)

**ВАЖНО:** Документация НЕ редактируется. "Не реализовано" (NPCSpawnPhase stub, 3-tier AI, Faction system и т.д.) — будет реализовано позже. Этот аудит фиксирует только реальные проблемы в существующем коде.

---

## CRITICAL проблемы (4)

### C1: SetConfig() никогда не вызывается — модули не инициализируются

**Файлы:** 10 модулей: Body, Qi, Combat, Inventory, Buff, Charger, Formation, NPC, Interaction, Quest

**Что происходит:**
Каждый из этих модулей имеет метод `SetConfig(XxxConfig config)` который хранит конфигурацию в приватное поле. Но НИКТО в коде не вызывает этот метод. DI регистрирует конфиг через `builder.RegisterInstance(defaultConfig)`, но модуль его не получает.

**Последствие:**
- `BodyService.Initialize()` не вызывается → игрок не имеет body parts
- `QiService.Initialize()` не вызывается → игрок не имеет Qi pool
- `CombatService` не сконфигурирован → боевые параметры по умолчанию
- `NPCService` не сконфигурирован → параметры NPC по умолчанию

**Почему это критично:**
Игра запускается (не крашится), но игровые системы работают с дефолтными/пустыми значениями. Игрок может ходить, но не имеет HP (body parts), не имеет Qi, combat не работает корректно.

**Варианты решения:**

**Вариант A (рекомендую): Inject config через [Inject]**
```csharp
public class BodyModule : IModule
{
    [Inject] private readonly BodyConfig _config = null!;  // DI внедряет
    
    public void Start()
    {
        _bodyService.Initialize(_config);  // используем
    }
}
```
- Добавить `[Inject] BodyConfig _config` в каждый из 10 модулей
- В `Start()` передать config в service
- **Плюс:** явно, типобезопасно, DI-идиоматично
- **Время:** ~1 час (10 модулей × 5 мин)

**Вариант B: Resolve config из Container в Start()**
```csharp
public void Start()
{
    var config = Container.Resolve<BodyConfig>();
    _bodyService.Initialize(config);
}
```
- **Минус:** нужно иметь доступ к Container (сейчас модули его не имеют)
- **Время:** ~1.5 часа

**Вариант C: Отложить**
- Системы работают с дефолтами, но не настроены
- **Время:** 0

**Моя рекомендация:** Вариант A — стандартный DI паттерн, минимальные изменения.

---

### C2: PlayerCombatAdapter не зарегистрирован в DI

**Файл:** `game/src/Modules/Player/PlayerCombatAdapter.cs` (75 строк)

**Что происходит:**
`PlayerCombatAdapter` — мост между input игрока и боевой системой. При нажатии J/ЛКМ он должен публиковать `AttackIntentEvent`. Но `PlayerModuleServices.Register()` не регистрирует этот класс в DI, поэтому:
- `PlayerCombatAdapter` не создаётся
- `AttackIntentEvent` никогда не публикуется игроком
- Игрок не может атаковать

**Варианты решения:**

**Вариант A (рекомендую): Register + Start в PlayerModule**
```csharp
// PlayerModuleServices.cs
builder.Register<PlayerCombatAdapter>(Lifetime.Singleton);

// PlayerModule.cs
[Inject] private readonly PlayerCombatAdapter _combatAdapter = null!;
public void Start() { _combatAdapter.Start(); }
```
- **Плюс:** минимальные изменения, следует существующему паттерну
- **Время:** ~15 минут

**Вариант B: Отложить**
- Игрок не может атаковать (combat невозможен)
- **Время:** 0

**Моя рекомендация:** Вариант A — разблокирует combat для игрока.

---

### C3: PlayerService имеет параллельную HP систему, не связан с Body

**Файл:** `game/src/Modules/Player/PlayerService.cs:27`

**Что происходит:**
`PlayerService` создаёт свой `_data = new CharacterData()` с `_data.Health = 100f`. Это отдельная HP система, которая НЕ связана с `BodyService` (который управляет body parts с dual HP).

Когда combat наносит урон:
1. `DamageService` применяет урон к `BodyPart` (через `BodyService`)
2. `PlayerService._data.Health` НЕ изменяется
3. `PlayerService.IsAlive` проверяет `_data.Health > 0` → всегда true
4. **Игрок не может умереть от combat**

**Варианты решения:**

**Вариант A (рекомендую): Делегировать IsAlive в BodyService**
- `PlayerService.IsAlive` → `_bodyProvider.IsEntityAlive("player")`
- Удалить `_data.Health` (использовать body HP)
- Подписаться на `BodyCriticalEvent` (сердце уничтожено) → умереть
- **Плюс:** единая HP система, игрок может умереть
- **Время:** ~1 час

**Вариант B: Синхронизировать HP**
- При `DamageAppliedEvent` обновлять `_data.Health` от body total HP
- **Минус:** дублирование, риск рассинхронизации
- **Время:** ~30 минут

**Вариант C: Отложить**
- Игрок бессмертный (не может умереть)
- **Время:** 0

**Моя рекомендация:** Вариант A — единая система, нет дублирования.

---

### C4: NPCVisualService — stub (NPC невидимы)

**Файл:** `game/src/Modules/NPC/NPCVisualService.cs` (32 строки)

**Что происходит:**
Все 3 метода (`Initialize`, `UpdateVisualPositions`, `Dispose`) — пустые (no-op). `NPCModule.Tick()` вызывает `UpdateVisualPositions()` который ничего не делает.

Даже если NPC заспавнятся (когда NPCSpawnPhase будет реализован), они будут невидимы — нет спрайтов.

**Варианты решения:**

**Вариант A: Реализовать позже (рекомендую)**
- NPC визуал — это Adapter-слой ответственность (Godot спрайты)
- Реализовать когда NPCSpawnPhase будет готов
- Сейчас NPC не спавнятся → визуал не нужен
- **Время:** 0 (отложить)

**Вариант B: Реализовать сейчас**
- Создать `NPCSpriteRenderer` в Adapter/Scene/
- Подписаться на `NPCSpawnedEvent` → создать Sprite2D
- **Время:** ~2 часа

**Моя рекомендация:** Вариант A — отложить до реализации NPC фазы (Phase 1 NPC_COMBAT_PREP.md). Документация НЕ редактируется.

---

## MAJOR проблемы (14)

### M1: NPCSpawnPhase — stub (не спавнит NPC)

**Статус:** БУДЕТ РЕАЛИЗОВАНО ПОЗЖЕ (Phase 1 NPC_COMBAT_PREP.md)
**Документация:** NPC_ASSEMBLY_PIPELINE.md описывает полную процедуру
**Действие:** Ничего не делать сейчас. Реализовать в следующем этапе.

---

### M2: 4 stub фазы (ChargerInit, FormationInit, QuestInit, NPCSpawn)

**Статус:** БУДЕТ РЕАЛИЗОВАНО ПОЗЖЕ
**Действие:** Ничего не делать сейчас. Документация описывает, реализуем когда дойдём до этих систем.

---

### M3: CombatService 5 TODOs — equipment data = 0

**Файл:** `game/src/Modules/Combat/CombatService.cs:300,424,428,433,444`

**Что происходит:**
5 мест в боевом конвейере используют захардкоженный 0 вместо данных экипировки:
- `techniqueCritBonus = 0` (крит техники)
- `armorDodgePenalty = 0` (штраф уклонения от брони)
- `shieldBlock = 0` (блок щитом)
- `weaponParryBonus = 0` (парирование оружием)
- `weapon.Penetration = 0` (пробитие брони)

**Почему:** `EquipmentDataProvider.GetEquipped()` всегда возвращает null (см. M4).

**Варианты решения:**

**Вариант A (рекомендую): Исправить EquipmentDataProvider**
- Реализовать `GetEquipped()` через `IItemDatabaseService.TryGetItem()` (см. M4)
- После этого CombatService сможет читать данные экипировки
- Убрать TODO (заменить 0 на реальные значения)
- **Время:** ~1 час

**Вариант B: Отложить**
- Combat работает, но без учёта экипировки
- **Время:** 0

**Моя рекомендация:** Вариант A — исправить M4, затем M3 решится автоматически.

---

### M4: EquipmentDataProvider.GetEquipped всегда возвращает null

**Файл:** `game/src/Modules/Combat/EquipmentDataProvider.cs:68-75`

**Что происходит:**
Метод должен возвращать `EquipmentData` по слоту, но возвращает null. В комментарии TODO: "нужен IItemDatabaseService для резолва ID → EquipmentData".

`IItemDatabaseService` уже реализован и зарегистрирован, но `EquipmentDataProvider` его не использует.

**Варианты решения:**

**Вариант A (рекомендую): Inject IItemDatabaseService + реализовать**
```csharp
[Inject] private readonly IItemDatabaseService _itemDb = null!;

public EquipmentData GetEquipped(string entityId, EquipmentSlot slot)
{
    var itemId = GetEquippedItemId(entityId, slot);
    if (string.IsNullOrEmpty(itemId)) return null;
    return _itemDb.TryGetItem(itemId, out var item) ? item as EquipmentData : null;
}
```
- **Плюс:** CombatService получит данные экипировки (решает M3)
- **Время:** ~30 минут

**Вариант B: Отложить**
- Equipment data не используется в combat
- **Время:** 0

**Моя рекомендация:** Вариант A — разблокирует правильную работу combat.

---

### M5: NPC↔Body cycle (BodyPart type leak)

**Статус:** Решается AUDIT-1 C2 (перенос BodyPart в Core)
**Действие:** После решения пользователя по AUDIT-1 Q2, это устраняется автоматически.

---

### M6: NPC↔Generator cycle (NPCConfig shared)

**Что происходит:**
`NPCConfig` используется и `NPCModule`, и `ItemGeneratorService`/`TechniqueGeneratorService` (для весов генерации). Это создаёт зависимость Generator→NPC.

**Варианты решения:**

**Вариант A (рекомендую): Перенести weight tables в Core/Data**
- Вынести веса генерации из `NPCConfig` в `Core/Data/GeneratorTables.cs`
- Generator читает из Core, NPC читает из Core
- **Время:** ~2 часа

**Вариант B: Отложить**
- Цикл не крашит, но нарушает архитектуру
- **Время:** 0

**Моя рекомендация:** Вариант A — но можно отложить, не блокирует работу.

---

### M7: Random.Shared в combat — non-deterministic

**Файлы:** 10+ файлов в Combat используют `Random.Shared`

**Что происходит:**
`Random.Shared` — глобальный non-deterministic RNG. Combat результаты разные при каждом запуске даже с тем же seed. Это ломает воспроизводимость (важно для save/load и тестирования).

**Варианты решения:**

**Вариант A (рекомендую): Injectable SeededRandom**
- Создать `ICombatRng` интерфейс с методами `Next()`, `NextFloat()`, `Next(min,max)`
- Реализация `CombatRng : ICombatRng` обёртывает `SeededRandom` (seed из session)
- Inject в CombatService, DamageService и т.д.
- **Плюс:** детерминированный combat
- **Время:** ~2 часа

**Вариант B: Отложить**
- Combat non-deterministic, но работает
- **Время:** 0

**Моя рекомендация:** Вариант A — важно для тестирования и save/load.

---

### M8: CombatAIService single-tier, не 3-tier

**Статус:** БУДЕТ РЕАЛИЗОВАНО ПОЗЖЕ
**Документация:** NPC_AI_SYSTEM.md описывает 3-tier (Spinal/Neural/Brain)
**Действие:** Отложить. Реализовать когда NPC система будет активна.

---

### M9: BodyModule.OnBuffTicked DoT — stub

**Файл:** `game/src/Modules/Body/BodyModule.cs`

**Что происходит:**
При тике buff (Poison, Burn, Bleed, Freeze) урон логируется в Console, но не применяется к body parts.

**Варианты решения:**

**Вариант A (рекомендую): Применять DoT damage**
```csharp
private void OnBuffTicked(in BuffTickedEvent e)
{
    if (e.Damage > 0)
    {
        _bodyService.ApplyDamage(_entityId, e.Damage, e.DamageType);
    }
}
```
- **Время:** ~15 минут

**Вариант B: Отложить**
- DoT не работает, но combat без DoT тоже функционален
- **Время:** 0

**Моя рекомендация:** Вариант A — быстрый фикс, важен для combat.

---

### M10: NPCModuleServices duplicate registrations

**Файл:** `game/src/Modules/NPC/NPCModuleServices.cs`

**Что происходит:**
`IQiDataProvider` и `IEquipmentDataProvider` регистрируются дважды: в NPCModuleServices и в основных модулях (Qi/Combat). Вторая регистрация молча перезаписывает первую.

**Варианты решения:**

**Вариант A (рекомендую): Убрать дубликаты из NPCModuleServices**
- Удалить `builder.Register<IQiDataProvider, ...>` и `builder.Register<IEquipmentDataProvider, ...>` из NPC
- Оставить регистрации в Qi/Combat модулях
- **Время:** ~5 минут

**Вариант B: Отложить**
- Работает (последняя регистрация побеждает), но хрупко
- **Время:** 0

**Моя рекомендация:** Вариант A — простой cleanup.

---

### M11: 8 missing services (Sleep, Faction, Event, HUD, ...)

**Статус:** БУДЕТ РЕАЛИЗОВАНО ПОЗЖЕ
**Документация:** MODULE_STRUCTURE.md описывает эти сервисы
**Действие:** Ничего не делать. Документация НЕ редактируется. Реализуем когда дойдём до этих систем.

---

### M12: TimeService.DeltaTime hardcoded 1/60

**Файл:** `game/src/Modules/Time/TimeService.cs`

**Что происходит:**
`DeltaTime = 1f / 60f` — зафиксировано. Pause не обнуляет, speed не масштабирует. Все регенерации/кулдауны считают что игра всегда 60 FPS на Normal speed.

**Варианты решения:**

**Вариант A (рекомендую): Учитывать pause + speed**
```csharp
public float DeltaTime => IsPaused ? 0f : (1f / 60f) * (int)Speed;
```
- **Плюс:** регенерация/кулдауны правильно масштабируются
- **Время:** ~10 минут

**Вариант B: Отложить**
- Регенерация работает на Normal, но быстра/медленна на других скоростях
- **Время:** 0

**Моя рекомендация:** Вариант A — простой фикс, важен для gameplay.

---

### M13-M14: 22 TODOs + cross-module dependencies

**Статус:** Большинство TODO — "будет реализовано позже" (NPC, Combat, Faction системы)
**Действие:** Отложить. Разбирать индивидуально когда дойдём до соответствующих систем.

---

## Концептуальные вопросы для пользователя (4)

### Q1: SetConfig wiring — как внедрять config?
- **Вариант A:** [Inject] в модуле (рекомендую — стандартный DI)
- **Вариант B:** Resolve из Container
- **Вариант C:** Оставить (системы не инициализируются)

### Q2: PlayerService HP — как связать с Body?
- **Вариант A:** Делегировать BodyService (рекомендую — единая HP)
- **Вариант B:** Синхронизировать (дублирование)
- **Вариант C:** Оставить (игрок бессмертный)

### Q3: Random.Shared в combat — детерминированность?
- **Вариант A:** Injectable SeededRandom (рекомендую — для тестов/save)
- **Вариант B:** Оставить Random.Shared (non-deterministic)

### Q4: NPC↔Generator cycle — как разорвать?
- **Вариант A:** Перенести weight tables в Core (рекомендую)
- **Вариант B:** Отложить (работает, но нарушение архитектуры)

---

## План исправлений

### УЖЕ ИСПРАВЛЕНО ✅
- (нет — модульные фиксы ещё не применялись)

### P0 — Критические (рекомендую выполнить)
1. C1: SetConfig wiring (10 модулей) — зависит от Q1
2. C2: Register PlayerCombatAdapter
3. C3: Wire PlayerService → Body — зависит от Q2
4. M4: EquipmentDataProvider → IItemDatabaseService (разблокирует M3)
5. M9: BodyModule DoT damage
6. M10: Убрать duplicate registrations
7. M12: TimeService.DeltaTime fix

### P1 — Важные
8. M7: SeededRandom в combat — зависит от Q3
9. M6: NPC↔Generator cycle — зависит от Q4

### P2 — Отложить (будет реализовано позже)
- C4: NPCVisualService (когда NPC фаза)
- M1: NPCSpawnPhase
- M2: 4 stub фазы
- M8: 3-tier AI
- M11: 8 missing services
- M13-M14: 22 TODOs

---

## Файлы

- **Аудит:** этот файл
- **Полный отчёт:** `/home/z/my-project/worklog.md` (Task AUDIT-2)

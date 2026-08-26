# Чекпоинт имплементации: 9 аудиторских исправлений

**Дата:** 2026-08-22 07:15 UTC
**Task ID:** IMPL-COMPLETE

---

## Реализованные исправления (9)

### IMPL-1: Q1+Q2 — Перенос NPCState и BodyPart в Core/Data ✅
- `NPCState.cs` → `Core/Data/NPCState.cs` (namespace → CultivationGame.Core.Data)
- `BodyPart.cs` → `Core/Data/BodyPart.cs` (namespace → CultivationGame.Core.Data)
- Убраны `using CultivationGame.Modules.NPC.Data` из INPCService + 5 файлов NPC
- Убраны `using CultivationGame.Modules.Body` из IBodyDataProvider + NPCService
- **Результат:** Core→Modules зависимость устранена

### IMPL-2: Q13 — EventBus re-entrancy queue ✅
- Добавлен `[ThreadStatic] HashSet<Type> _publishing` для отслеживания активных публикаций
- Добавлен `[ThreadStatic] List<Action> _pendingQueue` для очереди re-entrant событий
- При re-entrant publish: событие добавляется в очередь, не рекурсивно
- После завершения текущей публикации: очередь обрабатывается
- **Результат:** StackOverflow исключён, события не теряются

### IMPL-3: Q3 — SetConfig [Inject] в 10 модулях ✅
- Body, Qi, Combat, Inventory, Buff, Charger, Formation, NPC, Interaction, Quest
- Добавлен `[Inject] private readonly XxxConfig _config = null!;` в каждый модуль
- В Start() вызывается service.Initialize(_config) или Configure(_config)
- Убраны мёртвые SetConfig() методы
- **Результат:** Все сервисы корректно инициализируются

### IMPL-4: Q4 — PlayerService HP делегирует BodyService ✅
- Добавлен [Inject] IBodyService в PlayerService
- IsAlive → проверяет `!_bodyService.IsPartSevered(BodyPartType.Heart)` (fallback на _data.Health)
- Подписка на BodyCriticalEvent → Die() при остановке сердца
- _data.Health = 100f оставлен как fallback (до инициализации Body)
- **Результат:** Единая HP система, игрок может умереть от combat

### IMPL-5: Q6 — Weight tables в Core ✅
- Создан `Core/Data/GeneratorTables.cs` (static class)
- Перенесены: TechniqueGradeWeights, TechniqueGradeMultipliers, EquipmentGradeWeightsByLevel
- ItemGeneratorService и TechniqueGeneratorService обновлены — используют GeneratorTables
- Убран NPCConfig параметр из конструкторов генераторов
- **Результат:** NPC↔Generator cycle устранён

### IMPL-6: Q5 — Injectable SeededRandom в combat ✅
- Создан `Core/Interfaces/ICombatRng.cs` (Next, NextFloat, NextBool)
- Создан `Modules/Combat/CombatRng.cs` (обёртка над SeededRandom, seed=12345)
- Зарегистрирован в CombatModuleServices как Singleton
- 6 combat файлов обновлены: CombatService, DamageService, CombatAIService, ElementalEffectService, CombatConsequencesService, CombatLootService
- Все `Random.Shared` заменены на `_rng.Next(...)` / `_rng.NextFloat()`
- **Результат:** Combat детерминирован (для тестов и save/load)

### IMPL-7: Q7 — Movement real-time без Time.Speed ✅
- Убран `speedMult *= (int)Time.Speed` из HandleFreeMovement
- Movement — real-time, не зависит от game speed
- Добавлен комментарий о будущем tick-based миграции
- **Результат:** Нет экстремальной скорости на Fast/Quick

### IMPL-8: Q8 — Save/load отключён ✅
- F5/F9 stubs закомментированы
- SaveService не вызывается
- **Результат:** Нет невалидных сейвов после изменений

### IMPL-9: Q9 — Spirit + Ring storage разделены ✅
- Создан `Core/Interfaces/ISpiritStorageService.cs` (unlimited, Qi cost)
- Создан `Modules/Inventory/SpiritStorageService.cs` (реализация)
- Зарегистрирован в InventoryModuleServices
- IStorageRingService остаётся как есть (уже отдельный)
- **Результат:** Spirit и Ring storage разделены строго по доке

---

## Отложенные (по решениям пользователя)

- Q10: Element.Poison — оставить (не требует изменений)
- Q11: TileMapLayer — отложить (оставить _Draw)
- Q12: ItemCategory — оставить (не требует изменений)
- Q14: GameTile readonly — отложить

---

## Верификация

- **Build:** 0 errors, 227 warnings (pre-existing)
- **Headless:** все 16 модулей запускаются корректно
- **Только .ctex error** (biome_ocean.png import — не связано с нашими изменениями)

---

## Файлы созданные (6)
- `Core/Data/NPCState.cs` (перенесён из Modules/NPC/Data/)
- `Core/Data/BodyPart.cs` (перенесён из Modules/Body/)
- `Core/Data/GeneratorTables.cs` (weight tables)
- `Core/Interfaces/ICombatRng.cs`
- `Core/Interfaces/ISpiritStorageService.cs`
- `Modules/Inventory/SpiritStorageService.cs`
- `Modules/Combat/CombatRng.cs`

## Файлы изменённые (~25)
- Core/Events/EventBus.cs (re-entrancy queue)
- Core/Interfaces/INPCService.cs, IBodyDataProvider.cs (убраны Modules usings)
- 10 Module files (SetConfig [Inject])
- PlayerService.cs (HP delegate)
- GameWorldController.cs (movement + save/load disabled)
- 6 Combat files (SeededRandom)
- Generator files (GeneratorTables)
- InventoryModuleServices.cs (SpiritStorage registration)

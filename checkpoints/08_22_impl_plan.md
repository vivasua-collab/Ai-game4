# Чекпоинт имплементации: 14 аудиторских решений

**Дата:** 2026-08-22 06:45 UTC
**Task ID:** IMPL-1

---

## Решения пользователя

| Q | Решение | Описание |
|---|---------|----------|
| Q1 | **A** | Перенести NPCState в Core/Data |
| Q2 | **A** | Перенести BodyPart в Core/Data (NPC и игрок создаются одинаково) |
| Q3 | **A** | [Inject] в модуле для SetConfig |
| Q4 | **A** | Делегировать BodyService (поправить сейчас) |
| Q5 | **A** | Injectable SeededRandom (длительный этап тестирования) |
| Q6 | **A** | Перенести weight tables в Core |
| Q7 | **B** | Tick-based (перенести в PlayerModule.Tick) |
| Q8 | **A** | Отложить save/load, отключить stubs |
| Q9 | **B** | Разделить Spirit + Ring строго по доке |
| Q10 | **A** | Оставить Element.Poison |
| Q11 | **A** | Оставить _Draw, отложить TileMapLayer |
| Q12 | **A** | Оставить ItemCategory как есть |
| Q13 | **A** | Queue re-entrant events |
| Q14 | **A** | Отложить GameTile readonly |

---

## План реализации

### Этап P0 — Критические (блокируют корректную работу)

#### IMPL-1: Q1 + Q2 — Перенос NPCState и BodyPart в Core/Data
- Перенести `Modules/NPC/Data/NPCState.cs` → `Core/Data/NPCState.cs`
- Перенести `Modules/Body/BodyPart.cs` → `Core/Data/BodyPart.cs`
- Обновить `using` во всех файлах, которые ссылаются на эти типы
- Убрать `using CultivationGame.Modules.NPC.Data` из `INPCService.cs`
- Убрать `using CultivationGame.Modules.Body` из `IBodyDataProvider.cs`

#### IMPL-2: Q13 — EventBus re-entrancy queue
- Добавить `[ThreadStatic] HashSet<Type>? _publishing` в EventBus
- При re-entrant publish: добавить в очередь `_pending`
- После завершения текущей публикации: обработать очередь

#### IMPL-3: Q3 — SetConfig [Inject] в 10 модулях
- Body, Qi, Combat, Inventory, Buff, Charger, Formation, NPC, Interaction, Quest
- Добавить `[Inject] private readonly XxxConfig _config = null!;` в каждый модуль
- В `Start()` передать config в service: `_service.Initialize(_config)`

#### IMPL-4: Q4 — PlayerService HP делегировать BodyService
- Убрать `_data.Health = 100f` из PlayerService
- `IsAlive` → проверять через BodyService (heart not severed)
- Подписаться на `BodyCriticalEvent` → смерть игрока

### Этап P1 — Важные

#### IMPL-5: Q6 — Weight tables в Core
- Вынести веса генерации из `NPCConfig` в `Core/Data/GeneratorTables.cs`
- Generator читает из Core, NPC читает из Core

#### IMPL-6: Q5 — Injectable SeededRandom в combat
- Создать `ICombatRng` интерфейс
- `CombatRng : ICombatRng` обёртывает `SeededRandom`
- Inject в CombatService, DamageService и т.д.

#### IMPL-7: Q7 — Movement в tick-based
- Перенести HandleFreeMovement из GameWorldController._PhysicsProcess в PlayerModule.Tick
- Скорость перемещения масштабируется Time.Speed автоматически

#### IMPL-8: Q8 — Отключить save/load
- Закомментировать F5/F9 stubs в GameWorldController
- Убрать SaveAndQuit из UI

#### IMPL-9: Q9 — Разделить Spirit + Ring storage
- Создать `ISpiritStorageService` (unlimited, Qi cost)
- Оставить `IStorageRingService` (ring-specific)
- Убрать `StorageType { Spirit, Ring }` из единого StorageService

### Этап P2 — Отложено (по решениям пользователя)
- Q10: Element.Poison — оставить
- Q11: TileMapLayer — отложить
- Q12: ItemCategory — оставить
- Q14: GameTile readonly — отложить

---

## Порядок выполнения

1. **IMPL-1** (Q1+Q2): Перенос типов в Core — фундамент для остальных
2. **IMPL-2** (Q13): EventBus re-entrancy — защита от краша
3. **IMPL-3** (Q3): SetConfig wiring — инициализация модулей
4. **IMPL-4** (Q4): PlayerService HP — единая HP система
5. **IMPL-5** (Q6): Weight tables в Core
6. **IMPL-6** (Q5): SeededRandom
7. **IMPL-7** (Q7): Movement tick-based
8. **IMPL-8** (Q8): Отключить save/load
9. **IMPL-9** (Q9): Разделить storage

Каждый этап: реализация → build → verify → commit.

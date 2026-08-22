# Аудит 3: Entry + Adapter Layer — ПОДРОБНЫЙ

**Дата:** 2026-08-22 (переработан)
**Task ID:** AUDIT-3
**Scope:** Entry (GameSession, GameEntryPoint, phases) + Adapter (GameBoot, GameWorldController, renderers, UI)

---

## Сводка

- **Файлов проверено:** 34 (~4900 LOC)
- **Проблем найдено:** 47 (critical: 5, major: 14, minor: 28)

**ВАЖНО:** Документация НЕ редактируется. "Не реализовано" (save/load, NPC spawn phase и т.д.) — будет позже. Этот аудит фиксирует проблемы в существующем коде.

---

## CRITICAL проблемы (5)

### C1: SceneOrchestrator не использует CanExecute/SkipOnLoad — LoadGame сломан

**Файл:** `game/src/Entry/SceneOrchestrator.cs:68-88`

**Что происходит:**
`ISceneAssemblyPhase` интерфейс имеет методы `CanExecute()`, `SkipOnLoad`, `State`, `MarkAsSkipped()`. Но `SceneOrchestrator.RunAssembly()` вызывает `ExecuteAsync()` для ВСЕХ фаз безусловно, игнорируя эти методы.

**Последствие для LoadGame:**
Когда игрок загружает сохранение:
1. `GameSession.LoadGame()` → `_save.Load(...)` (восстанавливает state)
2. → `_orchestrator.RunAssembly(...)` (запускает ВСЕ фазы)
3. `TileMapGenPhase.ExecuteAsync()` → `_tile.Generate(...)` — **перегенерирует мир, затирая загруженный!**
4. `PlayerSpawnPhase.ExecuteAsync()` → `_player.Spawn(...)` — **перемещает игрока, затирая загруженную позицию!**

**Результат:** Save/load полностью сломан — загрузка перетирает сохранённые данные.

**Варианты решения:**

**Вариант A (рекомендую): Уважать SkipOnLoad + CanExecute**
```csharp
foreach (var phase in _phases)
{
    if (isLoadGame && phase.SkipOnLoad) { phase.MarkAsSkipped("load"); continue; }
    if (!phase.CanExecute()) { phase.MarkAsSkipped("blocked"); continue; }
    phase.State = PhaseState.Running;
    try { await phase.ExecuteAsync(); phase.State = PhaseState.Completed; }
    catch (Exception ex) { phase.State = PhaseState.Failed; throw; }
}
```
- Помечать `TileMapGenPhase.SkipOnLoad = true`, `PlayerSpawnPhase.SkipOnLoad = true`
- **Плюс:** save/load работает корректно
- **Время:** ~1 час

**Вариант B: Отложить (save/load не работает)**
- Save/load пока не используется (F5/F9 stubs)
- **Время:** 0

**Моя рекомендация:** Вариант A — но можно отложить, так как save/load ещё не активен. Реализовать когда будем подключать save/load.

---

### C2: LoadGame не восстанавливает session metadata

**Файл:** `game/src/Entry/GameSession.cs:120-129`

**Что происходит:**
После `_save.Load(...)`, создаётся НОВЫЙ `GameSessionData` с захардкоженными значениями:
```csharp
WorldId = LocationCatalog.TestPolygon.Id,  // всегда test_polygon
StartVariant = 1,                          // всегда 1
WorldTime = new WorldTime(START_YEAR, 1, 1, 6, 0),  // всегда старт
```

Сохранённые `WorldId`, `WorldName`, `StartVariant`, `WorldTime` игнорируются.

**Варианты решения:**

**Вариант A (рекомендую): Читать metadata из save**
- `ISaveService` должен предоставлять `GetSessionData()` → `GameSessionData`
- Использовать его в `LoadGame` вместо hardcoded значений
- **Время:** ~30 минут (после реализации save metadata)

**Вариант B: Отложить**
- Save/load не активен
- **Время:** 0

**Моя рекомендация:** Вариант B — отложить до реализации save/load. Документация НЕ редактируется.

---

### C3: Double-spawn игрока — PlayerModule.Start + PlayerSpawnPhase

**Файлы:** `Modules/Player/PlayerModule.cs:30-38` + `Entry/Phases/PlayerSpawnPhase.cs`

**Что происходит:**
1. `PlayerModule.Start()` (вызывается при загрузке GameBoot, ДО MainMenu) — спавнит игрока на (25,25)
2. `PlayerSpawnPhase.ExecuteAsync()` (вызывается при NewGame) — пытается спавнить на (center)
3. `PlayerService.Spawn()` — игнорирует второй вызов (`if (_spawned) return;`)

**Результат:** Игрок ВСЕГДА на (25,25). Для TestPolygon (50×50) center=(25,25) — совпадение. Для LargeWorld (500×500) center=(250,250) — игрок в углу карты вместо центра.

**Варианты решения:**

**Вариант A (рекомендую): Убрать spawn из PlayerModule.Start()**
- `PlayerModule.Start()` не должен спавнить — это ответственность фазы
- Оставить только в `PlayerSpawnPhase`
- **Плюс:** игрок спавнится в правильном месте
- **Время:** ~10 минут

**Вариант B: Отложить**
- Работает для TestPolygon, баг для LargeWorld
- **Время:** 0

**Моя рекомендация:** Вариант A — простой фикс, важен для LargeWorld.

---

### C4: Esc закрывает инвентарь но не resume Time — ИСПРАВЛЕНО ✅

**Статус:** Уже исправлено в коммите `5d51a9f`.

---

### C5: PlayerInputService meditate/minimap copy-paste bug — ИСПРАВЛЕНО ✅

**Статус:** Уже исправлено в коммите `5d51a9f`.

---

## MAJOR проблемы (14)

### M1: GroundItems ZIndex = Player ZIndex — items поверх игрока — ИСПРАВЛЕНО ✅

**Статус:** Уже исправлено в коммите `5d51a9f`.

---

### M2: HandleFreeMovement `*= (int)Time.Speed` — экстремальная скорость

**Файл:** `game/src/Adapter/Scene/GameWorldController.cs:445`

**Что происходит:**
```csharp
if (Time != null) speedMult *= (int)Time.Speed;
```
При `TimeSpeed.Quick` (15): `180 × 1.8 × 15 = 4860 px/s = ~76 tiles/s`. Камера не успевает, игрок "телепортируется".

Комментарий в коде (lines 78-81) говорит "Movement is tied to the tick system" — но реализация в `_PhysicsProcess` (real-time), противоречие.

**Варианты решения:**

**Вариант A (рекомендую): Убрать `*= (int)Time.Speed`**
- Movement — real-time, не должен зависеть от скорости симуляции
- `speedMult = run ? 1.8 : 1.0` (без умножения на Time.Speed)
- **Плюс:** скорость ходьбы стабильна на любой game speed
- **Время:** ~5 минут

**Вариант B: Перенести movement в tick system**
- Двигать игрока в `PlayerModule.Tick()` (масштабируется автоматически)
- **Минус:** большое рефакторинг, меняет ощущение управления
- **Время:** ~3 часа

**Вариант C: Отложить**
- На Normal (1×) работает нормально, проблема только на Fast/Quick
- **Время:** 0

**Моя рекомендация:** Вариант A — минимальное изменение, логически правильно.

---

### M3: HandlePickup distance — wrong constant — ИСПРАВЛЕНО ✅

**Статус:** Уже исправлено в коммите `5d51a9f`.

---

### M4: _mouseTarget не очищается при pause/inventory

**Файл:** `game/src/Adapter/Scene/GameWorldController.cs:401`

**Что происходит:**
Игрок кликает ЛКМ → `_mouseTarget` установлен. Открывает инвентарь (B) → пауза. Закрывает инвентарь → игрок ПРОДОЛЖАЕТ идти к старой цели (неожиданное поведение).

**Варианты решения:**

**Вариант A (рекомендую): Очищать _mouseTarget при pause/inventory**
- В `HandleStickyInput` при toggle inventory: `_mouseTarget = null;`
- **Время:** ~5 минут

**Вариант B: Отложить**
- Минорный UX баг
- **Время:** 0

**Моя рекомендация:** Вариант A — быстрый фикс.

---

### M5: QuickSave/QuickLoad stubs — SaveService не вызывается

**Файл:** `game/src/Adapter/Scene/GameWorldController.cs:583-593`

**Что происходит:**
F5/F9 печатают "stub" в лог, `SaveService.Save/Load` закомментированы.

**Варианты решения:**

**Вариант A: Отложить (рекомендую)**
- Save/load — отдельная фаза, требует metadata (см. C1, C2)
- Реализовать когда save system будет готов
- **Время:** 0

**Вариант B: Раскомментировать сейчас**
- `SaveService?.Save(new SaveSlot("quicksave", SaveSlotType.QuickSave))`
- **Минус:** может не работать (C1, C2 не решены)
- **Время:** ~10 минут

**Моя рекомендация:** Вариант A — отложить до реализации save/load фазы.

---

### M6: TestItemSeeder не gated DEBUG — ИСПРАВЛЕНО ✅

**Статус:** Уже исправлено в коммите `5d51a9f`.

---

### M7: CharacterDollPanel не подписан на EquipmentChangedEvent

**Файл:** `game/src/Adapter/UI/CharacterDollPanel.cs:23`

**Что происходит:**
В комментарии сказано "subscribes to EquipmentChangedEvent", но подписки нет. Doll обновляется только при открытии инвентаря (`RefreshAll`), не при изменении экипировки.

**Варианты решения:**

**Вариант A (рекомендую): Подписаться на событие**
```csharp
[Inject] private ISubscriber<EquipmentChangedEvent> _equipChangedSub = null!;
private IDisposable? _equipToken;
// в _Ready: _equipToken = _equipChangedSub.Subscribe(OnEquipChanged);
private void OnEquipChanged(in EquipmentChangedEvent e) { RefreshSlot(e.Slot); }
```
- **Плюс:** doll обновляется мгновенно при equip/unequip
- **Время:** ~20 минут

**Вариант B: Убрать вводящий в заблуждение комментарий**
- **Время:** ~2 минуты

**Моя рекомендация:** Вариант A — правильное поведение UI.

---

### M8: SurfaceTransitionRenderer ZIndex = 3 = Objects

**Файл:** `game/src/Adapter/Scene/SurfaceTransitionRenderer.cs:26`

**Что происходит:**
`ZIndex = RenderLayer.Terrain + 1 = 3` — совпадает с `RenderLayer.Objects = 3`. Переходы, объекты, тень игрока и границы — все на одном слое. Порядок определяется tree order (хрупко).

**Варианты решения:**

**Вариант A (рекомендую): Отдельный ZIndex для переходов**
- `ZIndex = 2` (между Terrain=2 и Objects=3) — переходы рисуются ПОВЕРХ biome, но ПОД объектами
- Или добавить `RenderLayer.SurfaceTransitions = 2` в enum
- **Время:** ~10 минут

**Вариант B: Отложить**
- Tree order сейчас правильный, но хрупко
- **Время:** 0

**Моя рекомендация:** Вариант A — простой фикс, делает слои явными.

---

### M9: GameLifetimeScope module order — Charger позиция — ИСПРАВЛЕНО ✅

**Статус:** Уже исправлено в коммите `5d51a9f`.

---

### M10: CoreValidationPhase проверяет только 6 из ~44 сервисов

**Файл:** `game/src/Entry/Phases/CoreValidationPhase.cs:23-28`

**Что происходит:**
Фаза валидации проверяет только 6 сервисов (ITimeService, IWorldService, IPlayerService, ITileService, ISaveService, IGameSession). Если какой-то из ~38 других сервисов не зарегистрирован — ошибка обнаружится только при первом использовании (runtime).

**Варианты решения:**

**Вариант A (рекомендую): Расширить список**
- Добавить все ключевые интерфейсы: IBodyService, IQiService, ICombatService, INPCService, IInventoryService, IEquipmentService, IGroundItemService, IItemDatabaseService, IResourceService, etc.
- **Время:** ~20 минут

**Вариант B: Reflection — проверить все интерфейсы**
- `typeof(IPlayerService).Assembly.GetTypes().Where(t => t.IsInterface && t.Name.StartsWith("I"))`
- Resolve каждый → если throws, логируем
- **Время:** ~30 минут

**Вариант C: Отложить**
- Ошибки обнаруживаются при использовании, но не при старте
- **Время:** 0

**Моя рекомендация:** Вариант A — явно, предсказуемо.

---

### M11: FinalizePhase double SceneReadyEvent

**Файл:** `game/src/Entry/Phases/FinalizePhase.cs:26`

**Что происходит:**
`FinalizePhase` публикует `SceneReadyEvent(1, 0, 0)`. `SceneOrchestrator` тоже публикует `SceneReadyEvent(10, 0, elapsed)`. Подписчики получают 2 события.

**Варианты решения:**

**Вариант A (рекомендую): Убрать publish из FinalizePhase**
- Orchestrator — единственный источник события
- **Время:** ~5 минут

**Вариант B: Отложить**
- Подписчики могут получить лишнее событие, но не крашит
- **Время:** 0

**Моя рекомендация:** Вариант A — простой cleanup.

---

### M12: GameEntryPoint _initialized=true до startables — нет retry

**Файл:** `game/src/Entry/GameEntryPoint.cs:64-78`

**Что происходит:**
`_initialized = true` устанавливается ДО запуска startables. Если startable throws, catch логирует, но `_initialized` уже true → повторный `Start()` ничего не делает.

**Варианты решения:**

**Вариант A (рекомендую): Reset на failure**
- Устанавливать `_initialized = true` только если ВСЕ startables завершились успешно
- При exception: оставить `_initialized = false` для retry
- **Время:** ~10 минут

**Вариант B: Отложить**
- Rare scenario, игра крашится при ошибке инициализации
- **Время:** 0

**Моя рекомендация:** Вариант A — надёжность запуска.

---

### M13: ContainerAdapter uncached reflection

**Файл:** `game/src/Adapter/Di/ContainerAdapter.cs:92-116`

**Что происходит:**
При каждом `InjectProperties` вызывается `typeof(IResolver).GetMethod("Resolve", ...)` — reflection lookup. Не кэшируется.

**Варианты решения:**

**Вариант A (рекомендую): Кэшировать MethodInfo**
```csharp
private static readonly MethodInfo _resolveMethod = 
    typeof(IResolver).GetMethod("Resolve", new Type[0]);
```
- **Время:** ~10 минут

**Вариант B: Добавить object Resolve(Type) в IResolver**
- Типобезопасно, без reflection
- **Время:** ~30 минут

**Вариант C: Отложить**
- Вызывается ~6 раз при старте, не hot path
- **Время:** 0

**Моя рекомендация:** Вариант A — минимальное изменение, устраняет overhead.

---

### M14: InputMapInitializer missing "meditate" + "defend" — ИСПРАВЛЕНО ✅ (meditate)

**Статус:** "meditate" добавлен в коммите `5d51a9f`. "defend" — отложить (не используется).

---

## Концептуальные вопросы для пользователя (4)

### Q1: Movement — real-time или tick-based?
- **Вариант A:** Real-time (сейчас), убрать `*= Time.Speed` (рекомендую — простой фикс)
- **Вариант B:** Tick-based (перенести в PlayerModule.Tick)
- **Вариант C:** Оставить (баг на Fast/Quick speed)

### Q2: Time.Speed влияет на скорость ходьбы?
- **Вариант A:** Нет (movement real-time, не зависит от game speed) — рекомендую
- **Вариант B:** Да (быстрая симуляция = быстрое перемещение)

### Q3: RenderLayer.GroundItems — добавить enum value?
- **Вариант A:** Да, между Objects(3) и Player(4) — рекомендую
- **Вариант B:** Использовать Objects (текущее исправление)

### Q4: Save/load — когда реализовать?
- **Вариант A:** Отложить (рекомендую — сейчас не критично)
- **Вариант B:** Реализовать сейчас (требует C1, C2, M5)

---

## План исправлений

### УЖЕ ИСПРАВЛЕНО ✅ (коммит 5d51a9f)
- C4: Esc-close inventory → Time.Resume()
- C5: PlayerInputService meditate bug
- M1: GroundItems ZIndex
- M3: HandlePickup distance
- M6: TestItemSeeder DEBUG gate
- M9: GameLifetimeScope Charger order
- M14: "meditate" action added

### P0 — Критические (рекомендую выполнить)
1. C3: Убрать PlayerModule.Start spawn (LargeWorld фикс)
2. M2: HandleFreeMovement speed scaling — зависит от Q1
3. M4: _mouseTarget clearing
4. M7: CharacterDollPanel EquipmentChangedEvent subscription
5. M8: SurfaceTransitionRenderer ZIndex

### P1 — Важные
6. M10: CoreValidationPhase expansion
7. M11: FinalizePhase double event
8. M12: GameEntryPoint retry
9. M13: ContainerAdapter reflection cache

### P2 — Отложить
- C1: SceneOrchestrator (когда save/load)
- C2: LoadGame metadata (когда save/load)
- M5: QuickSave/QuickLoad (когда save/load)

---

## Файлы

- **Аудит:** этот файл
- **Полный отчёт:** `/home/z/my-project/worklog.md` (Task AUDIT-3)

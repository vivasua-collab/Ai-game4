# Аудит 3: Entry + Adapter Layer

**Дата:** 2026-08-21 13:45 UTC
**Task ID:** AUDIT-3

---

## Сводка

- **Файлов проверено:** 34 (17 Entry + 17 Adapter, ~4900 LOC)
- **Проблем найдено:** 47 (critical: 5, major: 14, minor: 28)

---

## CRITICAL проблемы

### C1: SceneOrchestrator не использует CanExecute/SkipOnLoad/State
- **Файл:** `Entry/SceneOrchestrator.cs:68-88`
- **Проблема:** Phase interface infrastructure — dead code. `ExecuteAsync` вызывается безусловно
- **Результат:** LoadGame перегенерирует мир + re-spawn игрока → save state уничтожается
- **Решение:** Check `CanExecute` перед выполнением, skip на LoadGame path

### C2: LoadGame не восстанавливает session metadata
- **Файл:** `Entry/GameSession.cs:120-129`
- **Проблема:** Hardcoded `LocationCatalog.TestPolygon.Id`, `StartVariant=1`, `WorldTime=START_YEAR`
- **Результат:** Save data игнорируется
- **Решение:** Читать session metadata из save

### C3: Double-spawn игрока
- **Файл:** `Modules/Player/PlayerModule.cs:30-38` + `Entry/Phases/PlayerSpawnPhase.cs`
- **Проблема:** PlayerModule.Start спавнит на (25,25) до MainMenu; PlayerSpawnPhase вызывает Spawn(center) но PlayerService.Spawn игнорирует (уже spawned)
- **Результат:** На LargeWorld игрок спавнится на (25,25) вместо (250,250)
- **Решение:** Убрать spawn из PlayerModule.Start

### C4: Esc закрывает инвентарь но не resume Time
- **Файл:** `Adapter/Scene/GameWorldController.cs:537-540`
- **Проблема:** B-open → Time.Pause(); Esc-close → Toggle() без Time.Resume()
- **Результат:** Игрок застревает в паузе после Esc
- **Решение:** Добавить resume logic в Esc branch

### C5: PlayerInputService meditate/minimap copy-paste bug
- **Файл:** `Modules/Player/PlayerInputService.cs:104`
- **Проблема:** `if (data.IsSticky("minimap")) _meditate = true;` — должно быть `"meditate"`
- **Результат:** N key (minimap) триггерит медитацию; реальной медитации нет
- **Решение:** Исправить на `"meditate"`, добавить action в InputMapInitializer

---

## MAJOR проблемы (14)

| # | Проблема | Файл | Решение |
|---|----------|------|---------|
| M1 | GroundItems ZIndex=4 = Player ZIndex=4, tree order ставит items сверху | GroundItemRenderer.cs:42 | ZIndex=3 (Objects) |
| M2 | HandleFreeMovement `*= (int)Time.Speed` → 15× speed at Quick | GameWorldController.cs:445 | Убрать или сделать tick-based |
| M3 | HandlePickup distance `1.5f * 96f` но TILE_PIXELS=64 | GameWorldController.cs:721 | `1.5f * GameConstants.TILE_PIXELS` |
| M4 | _mouseTarget не очищается при pause/inventory | GameWorldController.cs:401 | Clear в HandleStickyInput |
| M5 | QuickSave/QuickLoad stubs — SaveService не вызывается | GameWorldController.cs:583-593 | Uncomment Save calls |
| M6 | TestItemSeeder не gated DEBUG — test items в release | InventoryWindow.cs:63-68 | `#if DEBUG` |
| M7 | CharacterDollPanel не подписан на EquipmentChangedEvent (вопреки docstring) | CharacterDollPanel.cs:23 | Подписаться или убрать docstring |
| M8 | SurfaceTransitionRenderer ZIndex=3 = Objects ZIndex=3 | SurfaceTransitionRenderer.cs:26 | Отдельный layer |
| M9 | GameLifetimeScope module order: Charger на позиции 6 (spec = 14) | GameLifetimeScope.cs:72-87 | Выровнять с docs или документировать |
| M10 | CoreValidationPhase проверяет только 6 из ~44 сервисов | CoreValidationPhase.cs:23-28 | Расширить список |
| M11 | FinalizePhase double SceneReadyEvent publish | FinalizePhase.cs:26 | Убрать из FinalizePhase |
| M12 | GameEntryPoint _initialized=true до startables → нет retry | GameEntryPoint.cs:64-78 | Reset на partial failure |
| M13 | ContainerAdapter uncached reflection | ContainerAdapter.cs:92-116 | Cache MethodInfo |
| M14 | InputMapInitializer missing "meditate" + "defend" actions | InputMapInitializer.cs | Добавить actions |

---

## Концептуальные расхождения (требуют решения)

| # | Код | Документация | Вопрос |
|---|-----|--------------|--------|
| 1 | Movement в _PhysicsProcess (real-time) | Comment говорит "tied to tick system" | Real-time или tick-based movement? |
| 2 | `*= (int)Time.Speed` в movement | Время влияет на скорость перемещения | Должна ли скорость игры влиять на скорость ходьбы? |
| 3 | GameLifetimeScope Charger позиция 6 | DI_AND_EVENTBUS §1.2 = позиция 14 | Перенести Charger обратно или обновить док? |
| 4 | GroundItems ZIndex = Player ZIndex | RENDER_LAYERS.md: Player выше GroundItems | Добавить RenderLayer.GroundItems между Objects и Player? |

---

## План исправлений

### P0 — Критические (до save/load)
1. SceneOrchestrator: CanExecute/SkipOnLoad/State transitions
2. GameSession.LoadGame: restore session metadata
3. Убрать PlayerModule.Start spawn
4. Esc-close inventory → Time.Resume()

### P1 — Major (до combat/NPC)
5. PlayerInputService meditate bug fix + InputMapInitializer actions
6. ZIndex ordering (GroundItems ниже Player)
7. HandlePickup distance constant fix
8. HandleFreeMovement speed scaling fix
9. GameLifetimeScope module order
10. CoreValidationPhase expansion
11. QuickSave/QuickLoad wiring
12. TestItemSeeder DEBUG gate

### P2 — Minor
13. _mouseTarget clearing
14. Theme caching
15. Dead code removal
16. Double SceneReadyEvent
17. OnLoadGame silent fallback
18. Render throttle scaling

---

## Файлы

- **Аудит:** этот файл
- **Полный отчёт:** `/home/z/my-project/worklog.md` (Task AUDIT-3)

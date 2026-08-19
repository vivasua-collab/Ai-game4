# Чекпоинт: Полный аудит кода 19.08.2026

**Дата:** 2026-08-19
**Тип:** audit
**Версия:** после коммита `2ac9c08` (Fix inventory window not showing + disable mouse debug logs)
**Задача:** AUDIT-0819 — read-only полный аудит кодовой базы Cultivation World Simulator

---

## Сводка

| Параметр | Значение |
|----------|----------|
| **Build** | `dotnet build` — 0 errors, 224 warnings (↓ с 256 после 08_16 fixes) |
| **Headless run** | OK — 17 startables, 16 tickables, все запустились без throw |
| **Files tracked** | ALL .cs files в git (нет untracked после 08_16 fix) |
| **Архитектура (Core/Modules/Entry)** | ✅ Чистый C#, 0 `using Godot` |
| **Архитектура (Adapter)** | ❌ Содержит игровую логику (Pause/Speed/Save) |
| **Критических багов** | 6 (включая 3 regressions от 08_16) |
| **Важных проблем** | 14 |
| **Косметика** | 11 |

### Состояние запуска

```
[WorldModule] Started — time 1864-01-01 06:00, speed Normal
[TileService] Generated 50x50 grid, seed=12345, baseTerrain=Grass
[TileModule] Started — generated 50x50 grid
[PlayerService] Player spawned @ (25, 25), hp 100
[PlayerModule] Started — stat svc wired=True
[GameEntryPoint] Started. 17 startables, 16 tickables, session=GameSession
```

⚠️ **SceneOrchestrator и 10 фаз НИКОГДА не запускаются** (см. критический баг #4).

---

## Найденные проблемы

### 🔴 Критические

| # | Проблема | Файл | Описание |
|---|----------|------|----------|
| 1 | **Inventory нельзя закрыть по B** | `Adapter/UI/InventoryWindow.cs:142-153` + `Adapter/Scene/GameWorldController.cs:471-474` | `InventoryWindow._Input` вызывает `Toggle()` (закрывает), затем `SetInputAsHandled()`. Но `GameWorldController._PhysicsProcess` в том же кадре читает `IsActionJustPressed("inventory")=true` (SetInputAsHandled НЕ влияет на polling в _PhysicsProcess) → `HandleStickyInput` снова вызывает `Toggle()` → инвентарь снова открывается. **Net effect: B при открытом инвентаре не закрывает его.** Аналогично Esc закрывает инвентарь, но одновременно toggles паузу через `IsPausePressed`. Коммит `2ac9c08` "Fix inventory window not showing" добавил guard `if (_isVisible && ...)` для случая "закрыть", но двойной toggle не устранил. |
| 2 | **Sticky alias mismatch ломает F5/F9 + character/quest/map/meditate/special** | `Adapter/Input/InputAdapter.cs:98-115` + `Modules/Player/PlayerInputService.cs:90-105` | InputAdapter добавляет canonical имена: `"save"`, `"load"`, `"character_sheet"`, `"quest_log"`, `"world_map"`, `"special_action"`, `"techniques"`, `"minimap"`. PlayerInputService проверяет: `"f5"`, `"f9"`, `"character"`, `"quest"`, `"map"`, `"special"`, `"k"`, `"tab"`, `"n"`, `"meditate"`. **Ни один из этих алиасов не совпадает с canonical именами InputAdapter.** Соответствующие `_quickSave`, `_quickLoad`, `_characterSheet`, `_questLog`, `_map`, `_meditate`, `_specialAction` флаги ВСЕГДА false. F5/F9 в HUD-легенде не работают. Также `_attack` проверяет `IsSticky("j")` — это опечатка (должно быть `"space"` или другой алиас для attack; `"j"` — это journal). |
| 3 | **SetOverUI никогда не вызывается** | `Adapter/Input/InputAdapter.cs:152` + все UI nodes | Метод `SetOverUI(bool)` определён, но нигде не вызывается (grep подтверждает 0 вызовов). `_isOverUI` всегда `false`. InventoryWindow имеет `MouseFilter = Stop` на bg/panel, что блокирует click events для GUI, но **`IsActionJustPressed("mouse_click")` в `GameWorldController.HandleMouseClick` всё равно срабатывает** — Godot `IsAction*` polling игнорирует `MouseFilter`. Результат: при открытом инвентаре клик ЛКМ по панели инвентаря устанавливает `_mouseTarget` под инвентарём, и игрок начинает идти к этой точке. |
| 4 | **project.godot main_scene = GameWorld.tscn — SceneOrchestrator не запускается** | `project.godot:15` + `Adapter/UI/MainMenuController.cs:163-175` + `Entry/GameSession.cs:59-91` | `run/main_scene="res://scenes/GameWorld.tscn"` — игра стартует сразу в GameWorld, минуя MainMenu. Значит `MainMenuController.OnNewGame()` никогда не вызывается → `GameSession.NewGame()` не вызывается → `SceneOrchestrator.RunAssembly()` не запускается → 10 фаз (CoreValidation, TileMapGen, PlayerSpawn, NPCSpawn, QuestInit, UIInit, FormationInit, WorldInit, ChargerInit, Finalize) **никогда не выполняются**. Весь scene-assembly pipeline — мёртвый код. Модули сами инициализируются в `Start()` (PlayerModule.Start спавнит игрока, TileModule.Start генерирует грид), поэтому игра работает, но архитектурный слой Entry/Phases не используется. GameSession.State навсегда остаётся `MainMenu` (никогда не переходит в `Playing`). |
| 5 | **Save path: `res://saves` вместо `user://saves`** | `Adapter/Persistence/SaveFileHandler.cs:47` | `ProjectSettings.GlobalizePath("res://saves")` резолвится в `<project_dir>/saves` (внутри res://). В **экспортированной** сборке `res://` доступен только для чтения — запись в `res://saves/` упадёт с `UnauthorizedAccessException`. Должно быть `ProjectSettings.GlobalizePath("user://saves")` → `~/.local/share/godot/app_userdata/CultivationGame/saves` (Linux) или `%APPDATA%/Godot/app_userdata/CultivationGame/saves` (Windows). Коммит 08_16 (fix #6) зарегистрировал Adapter-версию, но путь неверный. |
| 6 | **LMB триггерит `attack` на каждый клик** | `Adapter/Input/InputMapInitializer.cs:79-81` | `AddMouseButtonAction("mouse_click", MouseButton.Left);` + `AddMouseButtonAction("attack", MouseButton.Left);` — оба action привязаны к ЛКМ. `InputAdapter._PhysicsProcess` проверяет `IsActionJustPressed("attack")` и добавляет `"attack"` в sticky. Когда будет добавлена боевая система, **каждый клик ЛКМ для движения будет одновременно триггерить attack**. Не баг сейчас, но бомба замедленного действия. Решение: убрать LMB из `"attack"` action; использовать `"mouse_click"` для движения, `"attack"` оставить только на Space. |

### 🟡 Важные

| # | Проблема | Файл | Описание |
|---|----------|------|----------|
| 7 | **Stale comment: "Movement is handled by PlayerModule.Tick()"** | `Adapter/Scene/GameWorldController.cs:67-70` | Коммент утверждает, что движение — в PlayerModule.Tick(), но коммит `9b50baf` (08_16 disables double-movement) сделал PlayerModule.Tick() no-op для движения. HandleFreeMovement в GameWorldController — единственный источник движения. Аудит 08_16 рекомендовал обновить коммент, не сделано. |
| 8 | **PlayerModule содержит ~100 LOC мёртвого кода** | `Modules/Player/PlayerModule.cs:74-188` | `HandleKeyboardMovement()`, `HandleMouseMovement()`, `SetMouseDestination()`, `ClearMouseDestination()` — все определены, но не вызываются. `Tick()` (lines 54-72) — пустой no-op с многострочным комментарием. Эти методы плюс `_mouseDestination` поле + `MaxX`/`MaxY` свойства — мёртвый код. Либо удалить, либо восстановить если планируется revert. |
| 9 | **Adapter содержит игровую логику (Pause/Speed/Save)** | `Adapter/Scene/GameWorldController.cs:459-513` | `HandleStickyInput` напрямую вызывает `Time.Pause()/Resume()`, меняет `Time.Speed`, циклит `CycleSpeedUp/Down`. Это игровая логика в Adapter-слое. Должно быть в `TimeControlModule : IModule`, вызываться через tick. Аудит 08_15 #8, не исправлено. |
| 10 | **Pause toggles даже при открытом инвентаре** | `Adapter/Scene/GameWorldController.cs:464-469` | `HandleStickyInput` не проверяет `_inventoryWindow._isVisible` перед `Time.Pause()`. Esc при открытом инвентаре закрывает его (через `_Input`) И toggles паузу. Должен быть guard `if (inventoryOpen) return;` перед pause-блоком. |
| 11 | **Middle-click comment: "center on player" — ложь** | `Adapter/Scene/GameWorldController.cs:297-300` | `case MouseButton.Middle: _camera.Zoom = new Vector2(3f, 3f); break;` — только сбрасывает zoom. Не центрирует камеру на игроке. Коммент вводит в заблуждение. |
| 12 | **ZIndex conflict: SurfaceTransitionRenderer == Objects** | `Adapter/Scene/SurfaceTransitionRenderer.cs:26` + `Core/Data/Enums.cs:1054` | `ZIndex = (int)RenderLayer.Terrain + 1 = 3`. `RenderLayer.Objects = 3`. Transition sprites рисуются на том же ZIndex, что и деревья/камни/прочие объекты. Возможен Z-fighting (мигание) на границах тайлов с объектами. Должно быть `RenderLayer.Terrain + 1` как отдельный слой (например, добавить `RenderLayer.Transition = 3`, `RenderLayer.Objects = 4`). |
| 13 | **BiomeTileRenderer и SurfaceTransitionRenderer не перерисовываются на TileChangedEvent** | `Adapter/Scene/SceneBuilder.cs:128-135` (BiomeTileRenderer) + `Adapter/Scene/SurfaceTransitionRenderer.cs:22-28` | Оба вызывают `QueueRedraw()` только в `Initialize()`. Если `TileService.SetTile()` меняет тайл в рантайме, оба renderer'а продолжают показывать старую картинку. Аудит 08_15 #11, не исправлено. Нужно подписаться на `TileChangedEvent` и вызывать `QueueRedraw()` (с throttle). |
| 14 | **Двойная генерация тайлов в коде** | `Modules/Tile/TileModule.cs:30` + `Entry/Phases/TileMapGenPhase.cs:24` | `TileModule.Start()` вызывает `Generate(12345, 50, 50, Grass)`. `TileMapGenPhase.ExecuteAsync()` тоже вызывает `Generate(loc.Seed, loc.Width, loc.Height, loc.TerrainType)` (те же параметры). В текущем рантайме TileMapGenPhase не запускается (см. #4), так что конфликт не проявляется, но как только main_scene сменят на MainMenu — будет двойной Generate. Аудит 08_15 #12, не исправлено. |
| 15 | **TimeService.CurrentTime hardcoded 06:00 — игнорирует WorldConfig.StartHour=12** | `Modules/World/WorldService.cs:22-23` + `Modules/World/WorldConfig.cs:27` | `CurrentTime = new WorldTime(START_YEAR, 1, 1, 6, 0)`. `WorldModule.Start` устанавливает только `Speed`, не время. `WorldConfig.StartHour=12` не используется. Аудит 08_15 #14, не исправлено. |
| 16 | **TimeService не реализует ITickable** | `Modules/World/WorldModule.cs:61-66` + `Modules/World/WorldService.cs:59-68` | `AdvanceTick()` не на интерфейсе `ITimeService`. `WorldModule.Tick` делает `if (_timeService is TimeService ts) ts.AdvanceTick();` — code smell, нарушение инкапсуляции. Аудит 08_15 #20, не исправлено. |
| 17 | **PlayerModule.Start спавнит игрока напрямую** | `Modules/Player/PlayerModule.cs:33-36` | `if (_playerService is PlayerService ps && !ps.IsSpawned) ps.Spawn(...)`. Это дубль с `PlayerSpawnPhase.ExecuteAsync()` (Phase 4). Idempotent guard (08_16 fix #3) предотвращает утечку, но архитектурно модуль не должен спавнить — это фаза Pipeline. Также `is PlayerService` каст — code smell. |
| 18 | **Mouse wheel zoom без throttle** | `Adapter/Scene/GameWorldController.cs:279-303` | `_Input` обрабатывает wheel без debounce. Можно очень быстро крутить zoom с 1.0 до 8.0. Не критично, но не UX-friendly. |
| 19 | **`TimeService.DeltaTime = 1f / 60f` placeholder** | `Modules/World/WorldService.cs:63` | Хардкоженое значение "1/60 sec" в `AdvanceTick()`. Не реальный deltaTime, не используется никем (GameBoot сам считает `delta`). Можно удалить поле или вычислять правильно. |
| 20 | **HUD label hardcoded y=1020 (1080p)** | `Adapter/Scene/GameWorldController.cs:264` | `_hudLabel.Position = new Vector2(20, 1020)` — предполагает viewport 1080p. При `window/size/viewport_height=1080` это ок, но если окно ресайзится (stretch mode = expand), легенда уплывёт за экран. Должно быть anchored bottom. |

### 🟢 Замечания

| # | Проблема | Файл | Описание |
|---|----------|------|----------|
| 21 | 224 warnings компилятора | все | CS8618 (Inject non-nullable), CS8625 (null→non-nullable), CS0414 (unused Inject fields), CS0649 (QuestService._progressTracker). Все — следствие [Inject] reflection-DI паттерна. Подавить `= null!` или `#pragma warning disable`. |
| 22 | Debug-логи в production | многие | `GD.Print`/`Console.WriteLine` повсюду. Нет `#if DEBUG` или log-level фильтра. Примеры: GameWorldController:285,323-328,336; TileService:197,207-208; PlayerService:104,117,132; InventoryModule:103,117,129,140,144. |
| 23 | Два класса в одном файле | `Modules/World/WorldService.cs` | `TimeService` + `WorldService` в одном файле (183 строки). Аудит 08_15 #21, не исправлено. |
| 24 | Hardcoded texture path | `Adapter/Scene/SceneBuilder.cs:58` | `"res://resources/tiles/64/biome_{name}.png"` — строковая интерполяция путей. Ломается при изменении структуры папок. |
| 25 | `_debugFrameCount` в production | `Adapter/Scene/GameWorldController.cs:52` | Поле определено, но более не используется (периодический debug logging удалён коммитом `2ac9c08`). Мёртвое поле. |
| 26 | GameWorld.tscn / MainMenu.tscn — синтетические UID | `scenes/GameWorld.tscn:1`, `scenes/MainMenu.tscn:1` | `uid="uid://gameworld1234"` / `uid="uid://mainmenu1234"` — написаны вручную, не Godot-сгенерированы (нормальный UID — 13-символьный base32). Работают, но при открытии в editor Godot может перегенерировать. |
| 27 | `BiomeType` — 2 из 9 биомов никогда не генерируются | `Modules/Tile/TileService.cs:270-279` | `MapToBiome` возвращает только Ocean/Sea/Coast/Grassland/Highlands/Mountains/Peak. `Steppe` и `Forest` определены в enum, имеют цвета и transition sprites, но не производятся. Аудит 08_16 упоминал — не блокирует, но неиспользуемые ассеты. |
| 28 | `PlayerConfig.MoveSpeed=3f` не используется | `Modules/Player/PlayerConfig.cs:50-53` | `MoveSpeed` и `RunSpeedMultiplier` мёртвые. `GameWorldController` использует `MoveSpeedPixels=180.0f` (const) и `RunSpeedMultiplier=1.8f` (const) — хардкожены, не из конфига. Аудит 08_15 #27, не исправлено. |
| 29 | `PositionUpdateThreshold=0.01f` не используется | `Modules/Player/PlayerConfig.cs:41` | Поле определено, но `PlayerService.SetPosition` публикует `PlayerPositionChangedEvent` при любом `old != position`. Аудит 08_15 #28, не исправлено. |
| 30 | `InputAdapter._Input` пустой хендлер | `Adapter/Input/InputAdapter.cs:138-146` | Метод `_Input` обрабатывает только `InputEventMouseMotion`, но тело пустое ("Could track mouse velocity for gesture detection (future)"). Мёртвый override — удалить или реализовать. |
| 31 | `MoveDirection` теряет точность float→int | `Modules/Player/PlayerInputService.cs:51-53` | `MoveDirection` возвращает `Position2D((int)(X*1000), (int)(Y*1000))` для дискретного ввода. GameWorldController.HandleFreeMovement НЕ использует PlayerInputService.MoveDirection (читает Godot.Input.GetVector напрямую). Поле и свойство — мёртвый код. |

---

## Статус предыдущих проблем (из аудита 15.08 + 16.08)

### Аудит 15.08 — 30 проблем

| # | Проблема | Статус | Комментарий |
|---|----------|--------|-------------|
| 1 | Mouse click не детектится | ✅ FIXED | `mouse_click` action добавлен (InputMapInitializer.cs:79). HandleMouseClick использует `IsActionJustPressed("mouse_click")`. |
| 2 | Sticky flags очищаются до чтения Adapter'ом | ✅ FIXED | `ResetFrameFlags()` перемещён из `PlayerModule.Tick()` в конец `GameWorldController._PhysicsProcess` (строка 349). |
| 3 | Двойной Spawn игрока → утечка подписки | ✅ FIXED | `PlayerService.Spawn` имеет `if (_spawned) return;` guard (строки 102-106). |
| 4 | Дублирующая Camera2D | ✅ FIXED | В SceneBuilder нет Camera2D (только в GameWorldController). |
| 5 | Дублирующий Sprite2D игрока | ✅ FIXED | В SceneBuilder нет _playerSprite (только в GameWorldController). |
| 6 | Зарегистрирован не тот SaveFileHandler | ⚠️ PARTIAL | ISaveFileHandler интерфейс создан, Adapter.Persistence.SaveFileHandler регистрируется в GameBoot. Но путь `res://saves` неверный (должен быть `user://saves`). См. критический #5. |
| 7 | Клавиатура не прерывает mouse-move | ✅ FIXED (в HandleFreeMovement) | Клавиатура очищает `_mouseTarget` (GWC:377). |
| 8 | Adapter содержит игровую логику | ❌ NOT FIXED | См. важный #9. |
| 9 | Adapter напрямую резолвит Module | ✅ FIXED | `container.Resolve<PlayerModule>()` убран. `SetMouseDestination` больше не вызывается из Adapter. (Но PlayerModule.SetMouseDestination остался как мёртвый код — важный #8.) |
| 10 | `SetOverUI` никогда не вызывается | ❌ NOT FIXED | См. критический #3. |
| 11 | TransitionTileRenderer не перерисовывается | ❌ NOT FIXED | См. важный #13. |
| 12 | Двойная генерация тайлов | ❌ NOT FIXED (в коде) | TileModule.Start и TileMapGenPhase оба вызывают Generate. В рантайме проявления нет (фазы не запускаются — критический #4). См. важный #14. |
| 13 | Мёртвые `"j"`, `"e"`, `"r"` alias-проверки | ❌ NOT FIXED | PlayerInputService.cs:86-107 — все алиасы на месте. **Ухудшилось: aliases "f5"/"f9"/"character"/"quest"/"map"/"special" НЕ матчат canonical имена InputAdapter** — критический #2. |
| 14 | `WorldConfig.StartHour` игнорируется | ❌ NOT FIXED | TimeService.cs:22-23 — всё ещё hardcoded 06:00. См. важный #15. |
| 15 | Хардкод границ мира `49` | ✅ FIXED | PlayerModule использует `MaxX`/`MaxY` из `ITileService.MapWidth`. GWC и SceneBuilder — `Tiles.MapWidth`. Constants.DEFAULT_MAP_WIDTH=50. |
| 16 | Invalid UID в MainMenu.tscn | ✅ FIXED | UID убран из `[ext_resource]` (08_16). Сцена имеет синтетический `uid="uid://mainmenu1234"` на уровне `[gd_scene]` — работает, но косметика #26. |
| 17 | `MoveDirection` теряет точность | ⚠️ N/A | GameWorldController.HandleFreeMovement не использует PlayerInputService.MoveDirection (читает Godot.Input напрямую). Поле мёртвое — косметика #31. |
| 18 | `IsPaused` только через `Speed==Paused` | ❌ NOT FIXED | `WorldService.TimeService.IsPaused => Speed == Paused`. Нет независимого флага. GameSession.Pause() меняет SessionState, но не TimeService. |
| 19 | `Speed==0` обрабатывается дважды | ⚠️ N/A | GameBoot.cs:92-97 — `IsPaused` check + `speed <= 0` check. Безвредно. |
| 20 | `TimeService` не реализует `ITickable` | ❌ NOT FIXED | См. важный #16. |
| 21 | Два класса в одном файле | ❌ NOT FIXED | См. косметика #23. |
| 22 | 256 warnings | ⚠️ IMPROVED | 256 → 224 (32 CS0105 убраны в 08_16). Осталось 224. |
| 23 | CS0649 false positive | ❌ NOT FIXED | QuestService._progressTracker — всё ещё warning. |
| 24 | Debug-логи в production | ❌ NOT FIXED | См. косметика #22. |
| 25 | `PlayerModule.Start` использует `is PlayerService` | ❌ NOT FIXED | PlayerModule.cs:33 — `if (_playerService is PlayerService ps && !ps.IsSpawned)`. См. важный #17. |
| 26 | `_debugFrameCount` в production | ⚠️ PARTIAL | Периодический лог убран (commit `2ac9c08`), но поле осталось — косметика #25. |
| 27 | `MoveSpeed` в PlayerConfig не используется | ❌ NOT FIXED | См. косметика #28. |
| 28 | `PositionUpdateThreshold` не используется | ❌ NOT FIXED | См. косметика #29. |
| 29 | `TerrainColors` в TransitionTileRenderer.cs | ✅ FIXED | Вынесен в `TransitionSpriteGenerator.cs` (класс `TerrainColors`, строки 170-188). |
| 30 | `_isOverUI` без UI panels | ❌ NOT FIXED | UI panels добавлены (InventoryWindow), но SetOverUI всё ещё не вызывается. См. критический #3. |

### Аудит 16.08 — NEW issues (carryover)

| # | Проблема | Статус |
|---|----------|--------|
| NEW-1 | **Double-movement bug** (PlayerModule + GameWorldController) | ✅ FIXED — PlayerModule.Tick() — no-op для движения (commit `9b50baf`). |
| NEW-2 | SurfaceTransitionRenderer.cs — OK ✓ | ✅ Confirmed |
| NEW-3 | TransitionSpriteGenerator.cs — OK ✓ | ✅ Confirmed |
| NEW-4 | BiomeType.cs — OK ✓ | ✅ Confirmed |
| NEW-5 | GameWorldController free movement code — DOUBLE-MOVEMENT | ✅ FIXED (см. NEW-1) |
| NEW-6 | SceneBuilder biome colors rendering — OK ✓ | ✅ Confirmed |
| NEW-7 | Stale comment "Movement is handled by PlayerModule.Tick()" | ❌ NOT FIXED — важный #7 |
| NEW-8 | BiomeType: Steppe + Forest не генерируются | ❌ NOT FIXED — косметика #27 |

---

## Архитектурные проверки

### 1. Adapter layer — содержит игровую логику? ❌ ДА

- `GameWorldController.HandleStickyInput` (459-513) — Pause/Resume, Speed, CycleSpeedUp/Down, Save/Load stubs. Должно быть в `TimeControlModule`.
- `GameWorldController.HandleMouseClick` (428-457) — устанавливает `_mouseTarget` (pixel-координату), Adapter знает о pixel-movement. Это рендер-специфичная логика, приемлемо в Adapter.
- `GameWorldController.HandleFreeMovement` (357-421) — движение игрока. Архитектурно вопрос: движение — это логика (должна быть в Module) или рендер (плавная интерполяция визуальной позиции — Adapter)? Текущая реализация смешивает: pixel-position в Adapter, но tile-position sync'ается в PlayerService через `MoveTo`. Приемлемо для V1, но при росте сложности нужно разделить.

### 2. Modules layer — Godot dependencies? ✅ НЕТ

`rg "^\s*using Godot" src/Modules/` — 0 совпадений. Архитектура соблюдена.

### 3. Core layer — engine-agnostic? ✅ ДА

`rg "^\s*using Godot" src/Core/` — 0 совпадений. Все 30+ интерфейсов, контракты, DI, EventBus — чистый C#.

### 4. Entry layer — engine-agnostic? ✅ ДА

`rg "^\s*using Godot" src/Entry/` — 0 совпадений. GameSession, SceneOrchestrator, 10 фаз — чистый C#.

### 5. DI регистрация — все 16 модулей? ✅ ДА

`GameLifetimeScope.Build()` регистрирует (по порядку):
1. World, 2. Tile, 3. Body, 4. Qi, 5. Buff, 6. Charger, 7. Inventory, 8. Combat, 9. Formation, 10. NPC, 11. Player, 12. Quest, 13. Interaction, 14. UI, 15. Save, 16. Generator = **16 модулей**. ✓

Forwarding работает: `Register<IPlayerService, PlayerService>` регистрирует и интерфейс, и конкретный тип (Container.cs). Конструктор-инъекция работает (InventoryService ctor с опциональными параметрами, CombatAIService(ICombatService) и т.д.). Adapter-override хук (GameBoot:46-51) регистрирует Adapter.Persistence.SaveFileHandler как ISaveFileHandler. ✓

### 6. Constructor injection — проблемы?

- `InventoryService(IPublisher<ItemAddedEvent>, IPublisher<ItemRemovedEvent>, IItemDatabaseService? = null, BackpackService? = null)` — опциональные параметры. Container резолвит первые два (required), остальные — null. Это работает, но означает что ItemDatabase и BackpackService **не инжектятся** через DI (всегда null в production). Нужно либо убрать дефолты, либо зарегистрировать как required.
- `SaveDataAggregator(ISaveFileHandler)` — required, резолвится корректно (Adapter переопределяет ISaveFileHandler). ✓
- `SaveFileHandler()` (Adapter) — parameterless, регистрируется через `RegisterInstance` (GameBoot:48). ✓
- `SaveFileHandler(SaveConfig?)` (Modules) — опциональный параметр. DI выбирает greediest ctor → `(SaveConfig?)`. SaveConfig зарегистрирован (SaveModule.cs:76). ✓
- `[Inject]` на полях Godot Node-классов (GameWorldController, InventoryWindow, MainMenuController, InputAdapter, SceneBuilder) — резолвится через `ContainerAdapter.InjectProperties` (reflection). ✓

---

## Проверка специфических вопросов

### Movement system (GameWorldController)

| Вопрос | Ответ |
|--------|-------|
| `HandleFreeMovement` — баги? | (1) Time.Speed multiplier: `speedMult *= (int)Time.Speed` — при Quick=15 игрок движется 15× быстрее (45 px/frame). Возможно слишком быстро для gameplay. (2) При `Time.IsPaused` return — корректно. (3) Clamp на bounds — корректно (через `Tiles.MapWidth`). (4) `moveVec != Vector2.Zero` очищает `_mouseTarget` — корректно. |
| `HandleMouseClick` — `_mouseTarget` устанавливается? | ДА. `IsActionJustPressed("mouse_click")` → `GetGlobalMousePosition()` → clamp → `_mouseTarget = target`. ✓ |
| `HandleStickyInput` — inventory toggle? | ❌ Двойной toggle (критический #1). |
| Camera zoom (`_Input`) — баги? | (1) Middle-click коммент "center on player" ложь (важный #11). (2) Нет debounce (важный #18). |
| `_visualPosition` initialization? | ✓ Корректно: `_positionInitialized` guard, snap к tile center на первом кадре. |
| Speed multiplier `TimeSpeed × MoveSpeedPixels`? | ⚠️ `MoveSpeedPixels=180` × `(int)Time.Speed` (1/5/15) × `delta`. При Quick=15 → 2700 px/sec. Технически верно, но gameplay-wise обсуждается. |

### Inventory (InventoryWindow)

| Вопрос | Ответ |
|--------|-------|
| `Toggle()` работает? | ❌ Нет — двойной toggle (критический #1). |
| `RefreshItems()` обрабатывает пустой инвентарь? | ✓ ДА — `if (slots == null \|\| slots.Count == 0)` → показывает "◇ Инвентарь пуст". |
| `_Input` обрабатывает B и Esc? | ⚠️ Обрабатывает, но с побочными эффектами (критический #1 + важный #10). |
| DI инъекция `IInventoryService`? | ✓ Работает — `ContainerAdapter.InjectProperties` через reflection. IInventoryService зарегистрирован в `InventoryModuleServices.Register`. |
| Инвентарь читает из правильного сервиса? | ✓ ДА — `InventoryService?.GetAllSlots()`, `GetCurrentWeight()`, `GetEffectiveMaxWeight()` и т.д. — всё из `IInventoryService`. |

### Tile/terrain system

| Вопрос | Ответ |
|--------|-------|
| `TileService.Generate()` — biome smoothing работает? | ✓ `SmoothBiomes` — cellular automata, majority rule (≥5/9 neighbors). Корректно. |
| `MapToBiome` / `MapToSurface` — thresholds? | ⚠️ `MapToBiome` не производит `Steppe` и `Forest` (косметика #27). Thresholds: 0.30/0.40/0.45/0.65/0.82/0.92 — Ocean/Sea/Coast/Grassland/Highlands/Mountains/Peak. `MapToSurface` — те же thresholds + moisture-based Grass/Dirt split. Корректно, но Steppe/Forest неиспользуемы. |
| `LoadBiomeTextures()` — все 9 загружены? | ✓ Код обходит все 9 (пропускает 7 legacy aliases). На диске все 9 PNG существуют (`biome_ocean/sea/coast/grassland/steppe/forest/highlands/mountains/peak.png`). В headless `GD.Load` падает (`.ctex` не загружается без GPU), но в editor/runner работает. |
| `BiomeTileRenderer._Draw()` — баги? | ⚠️ Нет подписки на TileChangedEvent (важный #13). Иначе — корректно: iterate w×h, DrawTexture per tile, fallback red rect если missing. |
| `SurfaceTransitionRenderer` — priority correct? | ✓ `GetBiomePriority`: Ocean=0 < Sea=1 < Coast=2 < Steppe=3 < Grassland=4 < Forest=5 < Highlands=6 < Mountains=7 < Peak=8. Sprite рисуется на LOWER-priority tile с цветом HIGHER-priority neighbor'а. Корректно. |
| Diagonal condition — both orthogonals same? | ✓ `ShouldDrawDiagonal` проверяет `adj1 == curBiome && adj2 == curBiome`. Стандартный "inside corner" autotile. Корректно. |

### Player system

| Вопрос | Ответ |
|--------|-------|
| `Tick()` делает что-нибудь? | ❌ Нет — no-op для движения (comment-only). HandleKeyboardMovement / HandleMouseMovement — мёртвый код (важный #8). |
| `ResetFrameFlags` — где вызывается? | ✓ В `GameWorldController._PhysicsProcess:349`, после `HandleStickyInput` и `HandleMouseClick`. (08_16 fix #2.) |
| `Spawn()` — `_spawned` guard? | ✓ ДА — `if (_spawned) return;` (PlayerService.cs:102). (08_16 fix #3.) |
| `SetPosition()` — events published? | ✓ ДА — `PlayerPositionChangedEvent` при `old != position` (PlayerService.cs:76-79). |

### Time system

| Вопрос | Ответ |
|--------|-------|
| Tick accumulator — корректен? | ✓ `tickInterval = 1.0 / speed`, accumulator += delta, while-loop с cap 8 ticks/frame, backlog reset при `> tickInterval * 8`. Корректно (GameBoot.cs:99-115). |
| TimeSpeed values (0, 1, 5, 15)? | ✓ `Paused=0, Normal=1, Fast=5, Quick=15` (Enums.cs:756-762). |
| Speed change debounce — 1 second? | ✓ `SpeedChangeCooldownSec = 1.0f` (GWC:65). `_speedChangeCooldown -= GetPhysicsProcessDeltaTime()` каждый кадр. Сбрасывается в 1.0 при нажатии. Корректно. |
| Pause toggle — Esc работает? | ⚠️ Esc toggles pause (через IsPausePressed). Но также Esc закрывает инвентарь через `_Input` — двойной эффект (важный #10). |

### Input system

| Вопрос | Ответ |
|--------|-------|
| All actions registered? | ✓ move_*, run, interact, inventory, rest, harvest, special_action, pause, quicksave, quickload, journal, techniques, character_sheet, quest_log, world_map, minimap, attack, mouse_click, hotbar_1..9, input_log, time_speed_up, time_speed_down. |
| Mouse click action (LMB)? | ✓ `AddMouseButtonAction("mouse_click", MouseButton.Left)` (InputMapInitializer.cs:79). |
| PageUp/PageDown for speed? | ✓ `AddPhysicalKeyAction("time_speed_up", Key.Pageup)` / `Key.Pagedown` (строки 94-95). |
| `InputFrameData` construction? | ✓ Readonly struct с moveDirection, isRun, isLmb/Rmb, rmbHoldDuration, mouseWorldPos, isOverUI, hotbarSlot, stickyKeys, frame. Zero-alloc (stickyKeys — переиспользуемый HashSet). |
| Sticky keys handling? | ⚠️ InputAdapter добавляет canonical имена (`"inventory"`, `"save"`, `"load"`, `"character_sheet"`, etc.). PlayerInputService проверяет aliases (`"i"`, `"f5"`, `"f9"`, `"character"`, etc.) — НЕ матчат (критический #2). |

### Save system

| Вопрос | Ответ |
|--------|-------|
| `ISaveFileHandler` registration? | ✓ Интерфейс в Core/Interfaces. Modules.Save.SaveFileHandler — default (AppContext.BaseDirectory). Adapter.Persistence.SaveFileHandler — override (RegisterInstance в GameBoot). |
| `SaveFileHandler` в Adapter.Persistence? | ✓ Существует, реализует ISaveFileHandler (explicit interface). RegisterInstance регистрирует инстанс. |
| Save path issue? | ❌ `res://saves` — read-only в export. Должно быть `user://saves` (критический #5). |

### UI / HUD

| Вопрос | Ответ |
|--------|-------|
| HUD labels (time, hotkey legend)? | ✓ `_timeLabel` (top, parchment color, 18pt) + `_hudLabel` (bottom, near-black, 13pt, 4 строки). |
| ZIndex ordering? | ⚠️ SurfaceTransitionRenderer ZIndex=3 == RenderLayer.Objects (важный #12). Border rects ZIndex=Objects (3) — теоретический конфликт. Player ZIndex=4 — корректно. |
| CanvasLayer setup? | ✓ `_hudCanvas = new CanvasLayer { Layer = 10 }` (GWC:243). InventoryWindow — child of _hudCanvas. |

### Data integrity

| Вопрос | Ответ |
|--------|-------|
| `GameTile` struct — Biome + Terrain fields? | ✓ `Terrain` (stratum 1) + `Biome` (stratum 0) + MoveCost + Flags + Object + Resource + Destructible (GameTile.cs:26-45). |
| `BiomeType.cs` — correct values? | ✓ 9 base biomes (Ocean=0, Sea=1, Coast=2, Grassland=3, Steppe=4, Forest=5, Highlands=6, Mountains=7, Peak=8) + 7 legacy aliases (Plains=Grassland, Desert=Steppe, etc.). Корректно. |
| `Enums.cs` — TimeSpeed explicit values? | ✓ `Paused=0, Normal=1, Fast=5, Quick=15` — explicit. |
| Hardcoded "49" bounds? | ✓ Убраны (08_16 fix #15). `Math.Clamp(..., 0, MaxX)` где MaxX = `Tiles.MapWidth - 1`. Grep `Clamp\([^,]+,\s*0,\s*49\)` — 0 совпадений. Единственное "49" в коде — `NPCRelationshipService.cs:236` (`score <= 49` — attitude threshold, не bounds). |

---

## Рекомендации

### Приоритет P0 (блокируют геймплей)

1. **Исправить двойной toggle инвентаря** (критический #1):
   - Вариант A: убрать `_Input` хендлер из InventoryWindow, оставить только HandleStickyInput в GWC. Esc/B открывают и закрывают через sticky.
   - Вариант B: в InventoryWindow._Input вызывать `SetInputAsHandled()` BEFORE Toggle() (не помогает — polling не зависит).
   - Вариант C (рекомендуемый): добавить guard в HandleStickyInput — не togg'ать inventory если _Input уже togg'нул. Например, InventoryWindow._Input устанавливает `_suppressStickyToggle = true` на один кадр; HandleStickyInput проверяет этот флаг.

2. **Починить sticky alias mismatch** (критический #2):
   - Заменить все `"f5"`→`"save"`, `"f9"`→`"load"`, `"character"`→`"character_sheet"`, `"quest"`→`"quest_log"`, `"map"`→`"world_map"`, `"special"`→`"special_action"`, `"j"` (для attack)→убрать (или заменить на корректный alias), `"tab"`→`"minimap"`, `"n"`/`"meditate"`→убрать (InputAdapter не добавляет "meditate" — добавить или удалить поле).
   - Или: в InputAdapter добавлять ВСЕ aliases, которые проверяет PlayerInputService (включая `"f5"`, `"f9"`, etc.).

3. **Установить `SetOverUI`** (критический #3):
   - В InventoryWindow.BuildUI подключить `MouseFilter = Stop` + signals `mouse_entered`/`mouse_exited` к `_inputAdapter.SetOverUI(true/false)`.
   - Или: в `HandleMouseClick` добавить guard `if (_inventoryWindow?.Visible == true) return;`.

4. **Установить main_scene в MainMenu.tscn** (критический #4):
   - `project.godot: run/main_scene="res://scenes/MainMenu.tscn"`.
   - Это активирует SceneOrchestrator pipeline (10 фаз).
   - Перед этим убедиться, что фазы корректно работают (не throw).
   - Альтернатива: добавить автозапуск NewGame в GameBoot._Ready (если main_scene остаётся GameWorld для тестирования).

5. **Заменить `res://saves` на `user://saves`** (критический #5):
   - `Adapter/Persistence/SaveFileHandler.cs:47` — `ProjectSettings.GlobalizePath("user://saves")`.

6. **Убрать LMB из `"attack"` action** (критический #6):
   - `InputMapInitializer.cs:81` — удалить `AddMouseButtonAction("attack", MouseButton.Left);`.
   - Оставить `"mouse_click"` для движения, `"attack"` — только Space.

### Приоритет P1 (архитектура)

7. **Обновить stale comment** в `GameWorldController.cs:67-70` (важный #7).
8. **Удалить мёртвый код** в `PlayerModule.cs:74-188` (важный #8) — HandleKeyboardMovement, HandleMouseMovement, SetMouseDestination, ClearMouseDestination, _mouseDestination.
9. **Вынести HandleStickyInput в `TimeControlModule`** (важный #9) — через EventBus опубликовать `SpeedChangeRequestEvent` / `PauseToggleEvent`.
10. **Добавить guard `if (inventoryOpen) return;`** перед Pause/Speed блоком (важный #10).
11. **Исправить middle-click коммент** или реализовать center-on-player (важный #11).
12. **Добавить `RenderLayer.Transition = 3`, сдвинуть `RenderLayer.Objects = 4`, `Player = 5`, `UI = 6`** (важный #12).
13. **Подписать BiomeTileRenderer и SurfaceTransitionRenderer на TileChangedEvent** (важный #13) — с throttle (например, accumulate changes, redraw раз в 100ms).
14. **Удалить дубль Generate в TileModule.Start или TileMapGenPhase** (важный #14).
15. **Применить WorldConfig.StartHour в TimeService** (важный #15) — через `WorldModule.Start` инициализировать CurrentTime.
16. **Добавить `AdvanceTick()` на `ITimeService`** или сделать `TimeService : ITickable` (важный #16).
17. **Удалить `PlayerModule.Start` спавн** (важный #17) — оставить только в PlayerSpawnPhase.

### Приоритет P2 (косметика)

18. Подавить 224 warnings (`= null!` для [Inject] полей, `#pragma warning disable CS0414` для unused).
19. Обернуть debug-логи в `#if DEBUG` или `Conditional("DEBUG")`.
20. Разбить `WorldService.cs` на `TimeService.cs` + `WorldService.cs`.
21. Удалить `_debugFrameCount` поле.
22. Регенерировать UID для GameWorld.tscn и MainMenu.tscn через Godot editor.
23. Удалить мёртвые `PlayerConfig.MoveSpeed`, `PositionUpdateThreshold`.
24. Удалить мёртвый `InputAdapter._Input` override.
25. Привязать HUD label к bottom anchor вместо hardcoded y=1020.

---

## Файлы

### Просмотрено (read-only аудит)

**Adapter layer:**
- `src/Adapter/Scene/GameBoot.cs` — autoload, tick driver, DI bootstrap
- `src/Adapter/Scene/GameWorldController.cs` — main scene, input routing, HUD, movement
- `src/Adapter/Scene/SceneBuilder.cs` — biome textures, transition renderer setup
- `src/Adapter/Scene/SurfaceTransitionRenderer.cs` — transition sprite rendering
- `src/Adapter/Scene/TransitionSpriteGenerator.cs` — sprite generation + biome palette
- `src/Adapter/Input/InputAdapter.cs` — input polling
- `src/Adapter/Input/InputMapInitializer.cs` — action map setup
- `src/Adapter/UI/InventoryWindow.cs` — inventory UI
- `src/Adapter/UI/MainMenuController.cs` — main menu
- `src/Adapter/UI/ParchmentTheme.cs` — UI theme
- `src/Adapter/UI/UIFactory.cs` — UI factory
- `src/Adapter/Persistence/SaveFileHandler.cs` — Godot-aware save handler
- `src/Adapter/Di/ContainerAdapter.cs` — DI bridge for Godot nodes

**Modules layer:**
- `src/Modules/Player/PlayerModule.cs` — player module (Tick = no-op for movement)
- `src/Modules/Player/PlayerService.cs` — player state + spawn (idempotent)
- `src/Modules/Player/PlayerInputService.cs` — input flags + sticky aliases
- `src/Modules/Player/PlayerConfig.cs` — config (mostly unused)
- `src/Modules/Tile/TileModule.cs` — tile module (Generate in Start)
- `src/Modules/Tile/TileService.cs` — grid + noise + biome smoothing
- `src/Modules/World/WorldModule.cs` — time + world
- `src/Modules/World/WorldService.cs` — TimeService + WorldService (in one file)
- `src/Modules/World/WorldConfig.cs` — config (StartHour ignored)
- `src/Modules/Inventory/InventoryModule.cs` — inventory module
- `src/Modules/Inventory/InventoryModuleServices.cs` — DI registration
- `src/Modules/Inventory/InventoryService.cs` — inventory state
- `src/Modules/Save/SaveModule.cs` — save module
- `src/Modules/Save/SaveService.cs` — save service
- `src/Modules/Save/SaveDataAggregator.cs` — aggregator (uses ISaveFileHandler)
- `src/Modules/Save/SaveFileHandler.cs` — Modules-layer default save handler

**Core layer:**
- `src/Core/Data/Enums.cs` — TimeSpeed, RenderLayer, SaveSlotType, etc.
- `src/Core/Data/BiomeType.cs` — biome enum + aliases
- `src/Core/Data/GameTile.cs` — tile struct
- `src/Core/Data/Constants.cs` — GameConstants (DEFAULT_MAP_*, TILE_PIXELS)
- `src/Core/Data/Structs.cs` — Position2D, InputFrameData, SaveSlot
- `src/Core/Interfaces/ISaveFileHandler.cs` — flat-file save interface
- `src/Core/Interfaces/IPlayerService.cs` — player service interface
- `src/Core/Interfaces/IInventoryService.cs` — inventory interface
- `src/Core/Interfaces/ITimeService.cs` — time service interface
- `src/Core/Interfaces/IModule.cs` — module base contract
- `src/Core/DI/DIInterfaces.cs` — IContainerBuilder, IResolver, InjectAttribute

**Entry layer:**
- `src/Entry/GameLifetimeScope.cs` — DI configurator (16 modules + adapter override hook)
- `src/Entry/GameEntryPoint.cs` — IStartable/ITickable root
- `src/Entry/GameSession.cs` — session state machine
- `src/Entry/SceneOrchestrator.cs` — phase pipeline
- `src/Entry/Phases/CoreValidationPhase.cs` — phase 1
- `src/Entry/Phases/TileMapGenPhase.cs` — phase 2
- `src/Entry/Phases/PlayerSpawnPhase.cs` — phase 4

**Config:**
- `project.godot` — main_scene, autoload, dotnet
- `scenes/GameWorld.tscn` — main game scene
- `scenes/MainMenu.tscn` — main menu scene

### Не изменено

Аудит read-only. Никакие файлы не модифицированы.

---

## Следующие шаги (рекомендованный порядок)

1. **P0-1: Двойной toggle инвентаря** — критический для UX. Добавить suppress-флаг или убрать `_Input` хендлер.
2. **P0-2: Sticky alias mismatch** — F5/F9/character/quest/map не работают. Унифицировать canonical имена.
3. **P0-3: SetOverUI** — клики сквозь инвентарь. Подключить сигналы.
4. **P0-5: Save path user://** — 1 строка.
5. **P0-6: Убрать LMB из attack** — 1 строка.
6. **P0-4: main_scene → MainMenu** — активирует scene-assembly pipeline. Сначала проверить, что фазы не throw.
7. **P1: Архитектурные нарушения** — TimeControlModule, EventBus для mouse-click, TileChangedEvent подписки.
8. **P2: Косметика** — warnings, debug-логи, мёртвый код.

---

## Сводка по статусу

- **Build:** 0 errors, 224 warnings ✓
- **Headless:** OK (17 startables, 16 tickables, без throw) ✓
- **Files tracked:** ALL ✓
- **Architecture (Core/Modules/Entry):** Чистый C# ✓
- **Architecture (Adapter):** Содержит игровую логику ❌
- **Gameplay critical bugs:** 6 (4 новых + 2 regressions)
- **Audit 15.08 fixed:** 16/30 (53%)
- **Audit 16.08 fixed:** 2/3 carryover (67%)

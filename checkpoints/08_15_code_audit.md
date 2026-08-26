# Чекпоинт: Аудит кода

**Дата:** 2026-08-15
**Тип:** audit
**Задача:** AUDIT-1 — read-only аудит кодовой базы Cultivation World Simulator

## Контекст

Проект находится в стадии переноса из Ai-game3 (Unity) в Ai-game4 (Godot 4.7.1 .NET).
Согласно START_PROMPT §10, Core/Modules/Entry перенесены (stubs), Adapter работает
(GameBoot, InputAdapter, SceneBuilder, GameWorldController, MainMenuController).
Аудит должен найти проблемы в архитектуре, движении, тайлах, времени, вводе и потоке данных.

Проверено: `dotnet build` проходит (0 errors, 256 warnings). Headless запуск работает
(17 startables, 16 tickables). Визуальная верификация недоступна (нет X11/Wayland).

---

## Найденные проблемы

### 🔴 Критические (блокируют работу)

| # | Проблема | Файл | Строка | Описание |
|---|----------|------|--------|----------|
| 1 | Mouse click не детектится | `src/Adapter/Input/InputMapInitializer.cs` | 76 | Action "attack" привязан только к `Key.Space`, ЛКМ не привязан. `GameWorldController.HandleMouseClick` проверяет `IsActionJustPressed("attack")` — клик ЛКМ никогда не срабатывает. Движение по клику мыши полностью неработоспособно. |
| 2 | Sticky flags очищаются до чтения Adapter'ом | `src/Adapter/Scene/GameBoot.cs` + `src/Modules/Player/PlayerModule.cs` | GameBoot:70-101; PlayerModule:54-55 | GameBoot (autoload) вызывает `_entry.Tick()` → `PlayerModule.Tick()` → `ResetFrameFlags()` **ДО** того, как `GameWorldController._PhysicsProcess` (главная сцена) прочитает флаги. Порядок Godot: autoload → main scene. В итоге `IsPausePressed`, `IsTimeSpeedUpPressed`, `IsQuickSavePressed` и т.д. всегда `false` в HandleStickyInput. Пауза/скорость/сохранение через клавиатуру не работают. |
| 3 | Двойной Spawn игрока → утечка подписки | `src/Modules/Player/PlayerModule.cs` + `src/Entry/Phases/PlayerSpawnPhase.cs` | PlayerModule:32-35; PlayerSpawnPhase:25 | `PlayerModule.Start()` вызывает `PlayerService.Spawn(25,25)` при старте приложения. `PlayerSpawnPhase.ExecuteAsync()` снова вызывает `Spawn(center)` при NewGame. `PlayerService.Spawn` не проверяет `_spawned` и не освобождает старый `_qiChangedToken` перед повторной подпиской → QiChangedEvent срабатывает дважды, утечка памяти. |
| 4 | Дублирующая Camera2D | `src/Adapter/Scene/GameWorldController.cs` + `src/Adapter/Scene/SceneBuilder.cs` | GWC:88-101; SB:71-82 | GWC создаёт камеру Zoom=3f, SB создаёт камеру Zoom=1f. Обе вызывают `MakeCurrent()`. SB добавляется как child после GWC.SetupWorld → его `MakeCurrent()` побеждает. Активная камера имеет Zoom=1f вместо задуманных 3f. GWC._PhysicsProcess обновляет позицию неактивной камеры (no-op). |
| 5 | Дублирующий Sprite2D игрока | `src/Adapter/Scene/GameWorldController.cs` + `src/Adapter/Scene/SceneBuilder.cs` | GWC:116-123; SB:161-169 | Оба создают `_playerSprite` с процедурной текстурой и добавляют в `_worldRoot`. Два перекрывающихся спрайта на одном ZIndex — лишний draw call, риск Z-fighting. GWC также создаёт тень; SB — нет. |
| 6 | Зарегистрирован не тот SaveFileHandler | `src/Modules/Save/SaveModule.cs` + `src/Adapter/Persistence/SaveFileHandler.cs` | SaveModule:71 | `SaveModuleServices.Register` регистрирует `Modules.Save.SaveFileHandler` (использует `AppContext.BaseDirectory` + "saves"). `Adapter.Persistence.SaveFileHandler` (использует `ProjectSettings.GlobalizePath("res://saves")`) существует, но **никогда не регистрируется** — мёртвый код. Сохранения пишутся рядом с .dll, а не в Godot user data dir. Нарушение архитектуры: файловый I/O должен быть в Adapter. |

### 🟡 Важные (нужно исправить)

| # | Проблема | Файл | Строка | Описание |
|---|----------|------|--------|----------|
| 7 | Клавиатура не прерывает mouse-move | `src/Modules/Player/PlayerModule.cs` | 44-56, 63-64 | `Tick()` вызывает `HandleKeyboardMovement` только если `_mouseDestination == null`. Код очистки назначения (`_mouseDestination = null` строка 64) находится внутри `HandleKeyboardMovement` и недостижим, когда назначение мыши активно. Игрок не может прервать клик-движение клавишей WASD. |
| 8 | Adapter содержит игровую логику | `src/Adapter/Scene/GameWorldController.cs` | 344-418 | `HandleStickyInput` вызывает `Time.Pause()/Resume()`, меняет `Time.Speed`, `CycleSpeedUp/CycleSpeedDown`. Это игровая логика в Adapter-слое. Должно быть в Module (например, `TimeControlModule`) и вызываться через tick, не через `_PhysicsProcess`. |
| 9 | Adapter напрямую резолвит Module | `src/Adapter/Scene/GameWorldController.cs` | 331-336 | `container.Resolve<PlayerModule>()` + `playerModule.SetMouseDestination(...)` — прямой вызов метода модуля из Adapter'а. Нарушает hub-and-spoke (Adapter ↔ Modules должны общаться только через EventBus). `SetMouseDestination` не на интерфейсе `IModule`. |
| 10 | `SetOverUI` никогда не вызывается | `src/Adapter/Input/InputAdapter.cs` | 152 | Метод определён, но нигде не вызывается (grep подтверждает). `_isOverUI` всегда `false`. Когда UI-панели будут добавлены, клики сквозь них будут регистрироваться как игровые. Нет `mouse_entered/exited` сигналов ни в одном Control. |
| 11 | TransitionTileRenderer не перерисовывается | `src/Adapter/Scene/TransitionTileRenderer.cs` | 38 | `QueueRedraw()` вызывается только в `Initialize()`. При `TileService.SetTile` (через `TileChangedEvent`) renderer не обновляется. Переходы тайлов статичны после первой генерации. Нужно подписаться на `TileChangedEvent` и вызывать `QueueRedraw`. |
| 12 | Двойная генерация тайлов | `src/Modules/Tile/TileModule.cs` + `src/Entry/Phases/TileMapGenPhase.cs` | TileModule:30; TileMapGen:24 | `TileModule.Start()` генерирует сетку при старте приложения. `TileMapGenPhase.ExecuteAsync()` перегенерирует при NewGame. Вторая перезаписывает первую (та же seed 12345, 50×50), но это 2500 лишних вызовов `SampleWarped`. |
| 13 | Мёртвые `"j"` alias-проверки | `src/Modules/Player/PlayerInputService.cs` | 86, 90-104 | Строки вида `if (data.IsSticky("j") \|\| data.IsSticky("journal"))` проверяют каноническое имя `"j"`, но `InputAdapter` добавляет только `"journal"` (не `"j"`). Проверка `"j"` — мёртвый код. Аналогично для `"e"`, `"r"`, `"f"`, `"c"`, `"q"`, `"m"`, `"n"`, `"k"`, `"l"`, `"f5"`, `"f9"`, `"tab"`, `"escape"`, `"pause"`, `"save"`, `"load"`, `"quest"`, `"map"`, `"minimap"`, `"meditate"`, `"character"`, `"special"`, `"attack"`, `"defend"`. |
| 14 | `WorldConfig.StartHour` игнорируется | `src/Modules/World/WorldService.cs` + `src/Modules/World/WorldModule.cs` | WorldService:22-23; WorldModule:52 | `TimeService.CurrentTime` хардкожен `06:00` в инициализаторе поля. `WorldModule.Start` устанавливает только `Speed`, не время. `WorldConfig.StartYear/StartMonth/StartDay/StartHour` (1864/1/1/12) не используются. Conf mismatch. |
| 15 | Хардкод границ мира `49` | `src/Modules/Player/PlayerModule.cs` + `src/Adapter/Scene/GameWorldController.cs` | PlayerModule:90-92, 136-137, 153-154; GWC:313-314 | `Math.Clamp(..., 0, 49)` вместо `WorldService.MapWidth-1`. При смене локации (future) значения будут неверны. |
| 16 | Invalid UID в MainMenu.tscn | `scenes/MainMenu.tscn` | 3 | `uid://q7qb2mav6ygx` невалиден — Godot выводит WARNING и fallback на text path. Нужно удалить `uid=...` или перегенерировать. |
| 17 | `MoveDirection` теряет точность | `src/Modules/Player/PlayerInputService.cs` | 51-53 | `MoveDirection` возвращает `Position2D((int)(X*1000), (int)(Y*1000))`. Для диагонали (0.707, 0.707) → (707, 707). Порог 200 в PlayerModule работает, но точность float→int избыточна для дискретного ввода. Не блокирует, но усложняет отладку. |
| 18 | `IsPaused` только через `Speed == Paused` | `src/Modules/World/WorldService.cs` | 27 | `IsPaused => Speed == TimeSpeed.Paused` — нет независимого флага паузы. `GameSession.Pause()` меняет `SessionState`, но не `TimeService.Speed`. Расхождение между session-pause и time-pause. `GameWorldController.HandleStickyInput` работает с `Time.IsPaused`, а не с `Session.IsPaused`. |
| 19 | `Speed == 0` обрабатывается дважды | `src/Adapter/Scene/GameBoot.cs` | 77, 80-82 | `if (_timeService.IsPaused) return;` (IsPaused = Speed==Paused) и затем `if (speed <= 0) return;`. Избыточно, но не вредно. |
| 20 | `TimeService` не реализует `ITickable` | `src/Modules/World/WorldService.cs` + `src/Modules/World/WorldModule.cs` | WorldService:20; WorldModule:61-66 | `TimeService.AdvanceTick()` не на интерфейсе `ITimeService`. `WorldModule.Tick` делает приведение `if (_timeService is TimeService ts)` для вызова. Code smell — нарушение инкапсуляции модуля. |

### 🟢 Замечания (косметика)

| # | Проблема | Файл | Строка | Описание |
|---|----------|------|--------|----------|
| 21 | Два класса в одном файле | `src/Modules/World/WorldService.cs` | 20, 75 | `TimeService` и `WorldService` оба в WorldService.cs. Следует разбить на TimeService.cs + WorldService.cs. |
| 22 | 256 warnings компилятора | все | — | В основном CS8618 (non-nullable fields в [Inject]), CS0414 (unused fields в modules), CS8625 (null в non-nullable). Не блокируют, но засоряют вывод. |
| 23 | CS0649 false positive | `src/Modules/Quest/QuestService.cs` | 42 | `_progressTracker` помечен `[Inject]` nullable — DI присваивает через reflection, компилятор не видит. Класс зарегистрирован (QuestModule.cs:84). Можно подавить `!` или `= null!`. |
| 24 | Debug-логи в production | многие | — | `Console.WriteLine` и `GD.Print` повсюду: PlayerModule.cs:36, 78, 156; GameWorldController.cs:285, 323-328, 336; TransitionTileRenderer.cs:37, 53, 70; и т.д. Нужен log-level фильтр. |
| 25 | `PlayerModule.Start` использует `is PlayerService` | `src/Modules/Player/PlayerModule.cs` | 32 | Приведение к конкретному классу для проверки `IsSpawned`. Лучше добавить `bool IsSpawned` на `IPlayerService`. |
| 26 | `_debugFrameCount` в production | `src/Adapter/Scene/GameWorldController.cs` | 50, 279-286 | Debug-лог каждые 60 кадров. Должен быть за `#if DEBUG` или удаляться. |
| 27 | `MoveSpeed` в PlayerConfig не используется | `src/Modules/Player/PlayerConfig.cs` | 50-53 | `MoveSpeed=3f` и `RunSpeedMultiplier=1.5f` определены, но PlayerModule использует хардкод `steps = RunHeld ? 3 : 2`. Config мёртвый. |
| 28 | `PositionUpdateThreshold` не используется | `src/Modules/Player/PlayerConfig.cs` | 41 | Порог 0.01 для публикации позиции не применяется — `PlayerService.SetPosition` публикует при любом `old != position`. |
| 29 | `TerrainColors` в TransitionTileRenderer.cs | `src/Adapter/Scene/TransitionTileRenderer.cs` | 185-202 | Статический класс `TerrainColors` определён в файле TransitionTileRenderer.cs. Логичнее вынести в отдельный файл или в Core/Data. |
| 30 | `_isOverUI` без UI panels | `src/Adapter/Input/InputAdapter.cs` | 40 | Поле существует, но UI panels ещё не реализованы (22 planned). Заглушка для будущего. |

---

## Архитектурные нарушения

### 1. Adapter содержит игровую логику
- `GameWorldController.HandleStickyInput` (строки 344-394) — пауза/скорость/сохранение.
- `GameWorldController.HandleMouseClick` (строки 299-342) — резолвит `PlayerModule` напрямую.
- `GameWorldController.CycleSpeedUp/CycleSpeedDown` (строки 397-418) — игровая логика скорости.
- **Должно быть:** вся эта логика — в Module (например, `TimeControlModule`), вызывается через tick. Adapter только рендерит и пробрасывает input.

### 2. Adapter → Modules прямое обращение (мимо EventBus)
- `GameWorldController.cs:334` — `container.Resolve<PlayerModule>()` + `SetMouseDestination()`.
- **Должно быть:** Adapter публикует `MouseClickEvent(tileX, tileY)` на EventBus; `PlayerModule` подписывается и обновляет `_mouseDestination`.

### 3. Modules layer: чистый C# ✅
- Grep `^using Godot` по `src/Modules/` — **0 совпадений**. Архитектура соблюдена.

### 4. Core layer: чистый C# ✅
- Grep `^using Godot` по `src/Core/` — **0 совпадений**. Архитектура соблюдена.

### 5. Entry layer: чистый C# ✅
- Grep `^using Godot` по `src/Entry/` — **0 совпадений**. Архитектура соблюдена.

### 6. DI регистрация: 16 модулей ✅
- `GameLifetimeScope.Build()` регистрирует: World, Tile, Body, Qi, Buff, Charger, Inventory, Combat, Formation, NPC, Player, Quest, Interaction, UI, Save, Generator = **16 модулей**. ✓
- Forwarding работает: `Register<IPlayerService, PlayerService>` также регистрирует ключ `PlayerService` → тот же singleton (Container.cs:49-52, 192-197, 208-216). ✓
- Constructor injection работает: `ItemGeneratorService(IItemDatabaseService, NPCConfig)`, `CombatAIService(ICombatService)`, `StatProviderAdapter(IStatService, INPCService)`, `PerkService(IBuffService, IQiDataProvider)` — все резолвятся. ✓
- `IResolver` special-cased: `Resolve<IResolver>()` возвращает `this` (Container.cs:97, 149). ✓

### 7. Дубликат SaveFileHandler — архитектурный конфликт
- `Modules.Save.SaveFileHandler` (pure C#, `AppContext.BaseDirectory`) — **зарегистрирован** в DI.
- `Adapter.Persistence.SaveFileHandler` (Godot, `ProjectSettings.GlobalizePath`) — **не зарегистрирован**, мёртвый код.
- Файловый I/O должен быть в Adapter. Текущая регистрация нарушает изоляцию движка.

---

## Ответы на специфические вопросы

### Движение (movement system)
- **Mouse click детектится?** НЕТ. `IsActionJustPressed("attack")` в GameWorldController.cs:304 никогда не срабатывает на клик ЛКМ, потому что "attack" привязан к `Key.Space` (InputMapInitializer.cs:76), а не к Mouse Button Left.
- **`GetGlobalMousePosition()` корректен для Node2D?** Да — возвращает мировые координаты с учётом камеры. Но активная камера — SceneBuilder'а (Zoom=1f), не GameWorldController'а (Zoom=3f), из-за чего координаты мыши преобразуются с zoom=1f.
- **`PlayerModule.SetMouseDestination` вызывается?** НЕТ (потому что HandleMouseClick не проходит проверку "attack"). Если бы проходила — да, через `container.Resolve<PlayerModule>()` (нарушение архитектуры).
- **Race condition между keyboard и mouse?** ДА. Когда `_mouseDestination != null`, `HandleKeyboardMovement` не вызывается → клавиатура игнорируется. Код очистки в HandleKeyboardMovement:63-64 недостижим.

### Тайлы/террейн (tile system)
- **Почему transition tiles могут не рендериться?** `QueueRedraw()` вызывается только в `Initialize()` (TransitionTileRenderer.cs:38). Если `_tileService` ещё не сгенерирован при `_Draw()` — выйдет с ошибкой. Порядок: SceneBuilder._Ready → SetupTransitionTiles → Initialize → QueueRedraw; _Draw выполнится на следующем кадре. К тому моменту TileService уже сгенерирован (TileModule.Start). Риск: если SetTile изменяет тайл — transition не обновится.
- **TransitionTileRenderer добавлен в scene tree?** Да — `SceneBuilder.SetupTransitionTiles` (SceneBuilder.cs:64-66) вызывает `_worldRoot.AddChild(renderer)`.
- **`QueueRedraw()` вызывается?** Да, один раз в Initialize (строка 38).
- **Corner conditions слишком строгие?** Алгоритм проверяет: diagonal ≠ current И оба orthogonal == current. Это стандартный "inside corner" autotile (RPG Maker). Outside corners обрабатываются соседним тайлом. Условия корректны, но рисует только inside corners — outside corner остаётся "ступенькой".
- **ZIndex корректен?** `RenderLayer.Terrain + 1` = 3 (Enums.cs:1069: Terrain=2). Объекты на ZIndex=3 (Objects). Transition (3) перекрывает terrain (2), но совпадает с Objects (3) — возможен Z-fighting с деревьями/камнями.

### Время (time system)
- **TimeSpeed values?** `Paused=0, Normal=1, Fast=5, Quick=15` (Enums.cs). ✓ Корректно.
- **Tick accumulator?** GameBoot.cs:84-100: `tickInterval = 1.0 / speed`, accumulator += delta, while-loop с cap 8 ticks/frame, backlog reset при `> tickInterval * 8`. Корректно.
- **Speed change debounce?** GameWorldController.cs:376-393: `_speedChangeCooldown` 1 сек. Но из-за бага #2 sticky flags не читаются — debounce бесполезен.

### Ввод (input system)
- **"attack" и "time_speed_up/down" зарегистрированы?** Да (InputMapInitializer.cs:76, 89-93). ✓
- **Mouse buttons привязаны?** НЕТ. Ни одна action не привязана к MouseButton.Left/Right. LMB/RMB проверяются через `IsMouseButtonPressed` в InputAdapter:75-76, но не через action map.
- **Sticky flags reset?** Да, `ResetFrameFlags` (PlayerInputService.cs:112-121) очищает все 19 флагов. Вызывается из PlayerModule.Tick:55. Но порядок выполнения ломает чтение из Adapter (баг #2).

### Поток данных (data flow)
- **PlayerService.Position доходит до рендера?** Да — GameWorldController._PhysicsProcess:256-259 и SceneBuilder._PhysicsProcess:213-219 читают `PlayerService.Position` и обновляют sprite. Оба спрайта синхронизированы (но их два — баг #5).
- **TileService.GetTile возвращает корректные данные?** Да — возвращает `GameTile` из `_grid[x,y]` или `CreateTerrain(Void)` для OOB (TileService.cs:33-38).
- **DI container резолвит все зависимости?** Да — headless лог показывает: 17 startables, 16 tickables, все модули Started без ошибок. CoreValidationPhase проходит.

---

## Рекомендации

### Приоритет P0 (блокируют геймплей)
1. **Привязать ЛКМ к action "attack"** в InputMapInitializer.cs:76:
   ```csharp
   AddMouseButtonAction("attack", MouseButton.Left);
   ```
   Или создать отдельную action "mouse_click" и проверять её в HandleMouseClick.

2. **Исправить порядок sticky flags.** Варианты:
   - (A) Перенести `HandleStickyInput` и `HandleMouseClick` логику в Module (например, `PlayerModule.Tick` или новый `InputModule.Tick`), вызываемую из tick system ДО `ResetFrameFlags`.
   - (B) Установить `ProcessPriority` на GameWorldController так, чтобы `_PhysicsProcess` бежал ДО GameBoot (хак, не рекомендуется).
   - (C) Не вызывать `ResetFrameFlags` в `PlayerModule.Tick`, а вызывать в конце `GameWorldController._PhysicsProcess` (но тогда модули внутри одного тика могут прочитать уже сброшенные флаги — нужен другой подход).

3. **Сделать `PlayerService.Spawn` идемпотентным:**
   ```csharp
   public void Spawn(Position2D position) {
       if (_spawned) { _qiChangedToken?.Dispose(); _qiChangedToken = null; }
       // ... остальная инициализация ...
   }
   ```
   Или удалить Spawn из PlayerModule.Start (оставить только в PlayerSpawnPhase).

4. **Удалить дублирующую камеру/спрайт из SceneBuilder** (или из GameWorldController). Оставить одну точку создания. Рекомендация: GameWorldController должен делегировать рендер SceneBuilder, а сам только управлять HUD и input routing.

5. **Зарегистрировать правильный SaveFileHandler.** Варианты:
   - Удалить `Modules.Save.SaveFileHandler`, зарегистрировать `Adapter.Persistence.SaveFileHandler` (нужен Adapter-мост для DI, т.к. SaveModule в Core не может ссылаться на Adapter).
   - Или сделать `ISaveFileHandler` интерфейс в Core, реализовать в Adapter, инжектить в SaveService.

### Приоритет P1 (архитектура)
6. **Вынести `HandleStickyInput`/`CycleSpeedUp/Down` в Module.** Создать `TimeControlModule : IModule` с подпиской на input flags. Adapter только отображает `Time.Speed` в HUD.

7. **Заменить прямой вызов `PlayerModule.SetMouseDestination` на EventBus.** Опубликовать `MouseClickEvent(int tileX, int tileY)` из Adapter, PlayerModule подписывается.

8. **Подписать `TransitionTileRenderer` на `TileChangedEvent`** и вызывать `QueueRedraw()` (с throttle, чтобы не перерисовывать на каждый SetTile).

9. **Удалить мёртвые `"j"`, `"e"`, `"r"` и т.д. alias-проверки** в PlayerInputService.cs — оставить только канонические имена ("journal", "interact", "rest"...).

10. **Использовать `WorldService.MapWidth/MapHeight`** вместо хардкода `49` в PlayerModule и GameWorldController.

### Приоритет P2 (косметика)
11. Разбить `WorldService.cs` на `TimeService.cs` + `WorldService.cs`.
12. Подавить 256 warnings: `= null!` для [Inject] полей, `#pragma warning disable CS0414` для unused.
13. Удалить debug-логи или обернуть в `Conditional("DEBUG")`.
14. Применить `WorldConfig.StartHour` в `WorldService` (или удалить неиспользуемые поля).
15. Починить invalid UID в `scenes/MainMenu.tscn`.

---

## Файлы

### Просмотрено (read-only аудит)
- `START_PROMPT.md` — правила проекта
- `src/Adapter/Scene/GameBoot.cs` — autoload, tick driver
- `src/Adapter/Scene/GameWorldController.cs` — главная сцена, input routing, HUD
- `src/Adapter/Scene/SceneBuilder.ts` — тайлы, камера, спрайт игрока
- `src/Adapter/Scene/TransitionTileRenderer.cs` — transition overlays
- `src/Adapter/Input/InputAdapter.cs` — polling ввода
- `src/Adapter/Input/InputMapInitializer.cs` — action map
- `src/Adapter/Di/ContainerAdapter.cs` — DI bridge для Godot nodes
- `src/Adapter/Persistence/SaveFileHandler.cs` — мёртвый код
- `src/Adapter/UI/MainMenuController.cs` — главное меню
- `src/Adapter/UI/UIFactory.cs` — UI factory
- `src/Core/DI/Container.cs` — DI container
- `src/Core/DI/DIInterfaces.cs` — DI interfaces
- `src/Core/Events/EventBus.cs` — event bus
- `src/Core/Data/Enums.cs` — TimeSpeed, RenderLayer
- `src/Core/Data/Structs.cs` — Position2D, InputFrameData
- `src/Core/Data/ValueNoise.cs` — noise generator
- `src/Core/Data/Constants.cs` — GameConstants
- `src/Core/Interfaces/IPlayerService.cs`, `IPlayerInputService.cs`, `ITimeService.cs`, `IModule.cs`
- `src/Modules/Player/PlayerModule.cs`, `PlayerService.cs`, `PlayerInputService.cs`, `PlayerConfig.cs`
- `src/Modules/Tile/TileModule.cs`, `TileService.cs`, `TileConfig.cs`, `ResourceService.cs`
- `src/Modules/World/WorldModule.cs`, `WorldService.cs` (TimeService + WorldService), `WorldConfig.cs`
- `src/Modules/Save/SaveModule.cs`, `SaveFileHandler.cs` (мёртвый)
- `src/Modules/Quest/QuestModule.cs`, `QuestService.cs`
- `src/Entry/GameLifetimeScope.cs` — DI configurator
- `src/Entry/GameEntryPoint.cs` — IStartable/ITickable root
- `src/Entry/GameSession.cs` — session state machine
- `src/Entry/SceneOrchestrator.cs` — phase pipeline
- `src/Entry/SceneAssemblyRegistrar.cs` — phase registration
- `src/Entry/LocationCatalog.cs` — test polygon
- `src/Entry/Phases/*.cs` — 10 phases
- `scenes/GameWorld.tscn`, `scenes/MainMenu.tscn`
- `project.godot`, `CultivationGame.csproj`

### Не изменено
Аудит read-only. Никакие файлы не модифицированы.

### Следующие шаги
1. Создать тикеты на P0 баги (#1–#6).
2. Исправить в порядке: #1 (ЛКМ bind) → #2 (sticky flag order) → #4/#5 (дубликаты) → #3 (Spawn idempotent) → #6 (SaveFileHandler).
3. После P0 — заняться архитектурными нарушениями P1.
4. P2 — в свободном порядке.

---

## Статус сборки

```
dotnet build: 0 errors, 256 warnings ✓
headless --quit: корректный запуск, 17 startables, 16 tickables ✓
visual test: НЕ доступен (нет X11/Wayland в sandbox)
```

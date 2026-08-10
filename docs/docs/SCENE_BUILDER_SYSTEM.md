# Система сборки сцены — Runtime (АКТИВНАЯ)

**Версия:** 4.0
**Дата:** 2026-07-14
**Проект:** Cultivation World Simulator (Unity 6.3 URP 2D)
**Код Runtime:** `UnityProject/Assets/Scripts/Entry/RuntimeSceneBuilder.cs` (1412 LOC) + `Entry/Phases/` (10 фаз)
**Код Editor-time (заморожен):** [SCENE_BUILDER_SYSTEM_Old.md](./SCENE_BUILDER_SYSTEM_Old.md)

---

> **⚠️ ВАЖНО:** Существуют **ДВЕ** системы сборки сцены:
> 1. **Runtime RuntimeSceneBuilder** (активен) — описан в этом файле. Собирает сцену программно при Play.
> 2. **Editor-time FullSceneBuilder** (заморожен) — [SCENE_BUILDER_SYSTEM_Old.md](./SCENE_BUILDER_SYSTEM_Old.md). Историческая система для Unity Editor.

---

## Обзор

### Runtime Scene Assembly (АКТИВНАЯ)

`Entry/RuntimeSceneBuilder.cs` (1412 LOC) — программная сборка иерархии Unity-сцены при Play. Заменяет Editor-only FullSceneBuilder для runtime-инициализации.

**Создаёт:**
- Camera (Orthographic, URP 2D) + UniversalAdditionalCameraData
- Canvas (Screen-Space Overlay, 3 слоя: HudLayer, WindowLayer, FloatingLayer)
- EventSystem
- World Root (Grid + Tilemap + Objects)
- Player (SpriteRenderer + procedural sprite)
- Light2D (Global, для Sprite-Lit-Default)
- GameInputAdapter (F5/F9/Esc)
- 22 UI View + DragVisual + ItemContextMenu
- UIComponentResolver (DI-bridge для динамических View)

---

## SceneOrchestrator

Оркестратор runtime-сборки. 10 фаз `ISceneAssemblyPhase`:

| # | Фаза | Назначение |
|---|------|------------|
| 1 | CoreValidationPhase | Проверка DI-резолва всех интерфейсов |
| 2 | TileMapGenPhase | Генерация тайловой карты |
| 3 | WorldInitPhase | Инициализация мира |
| 4 | PlayerSpawnPhase | Спавн игрока (центр карты) |
| 5 | NPCSpawnPhase | Спавн NPC |
| 6 | FormationInitPhase | Инициализация формаций |
| 7 | ChargerInitPhase | Инициализация зарядников |
| 8 | QuestInitPhase | Инициализация квестов |
| 9 | UIInitPhase | Инициализация UI |
| 10 | FinalizePhase | Финализация сборки |

**Интерфейс фазы:** `ISceneAssemblyPhase` с UniTask async поддержкой.

**Порядок выполнения:** SceneOrchestrator.StartAssembly() → фазы 1→10 последовательно → SceneReadyEvent.

---

## WireUIViews — DI-инъекция в View

`WireUIViews()` — единая точка DI-инъекции в 22 UI View. Вызывается после `BuildSceneHierarchy()`.

**Ключевое решение (06_18):** Использует `FindFirstObjectByType<T>(FindObjectsInactive.Include)` для нахождения деактивированных панелей. Без `FindObjectsInactive.Include` метод не находит View, деактивированные в `CreateCanvas()` через `child.SetActive(false)`.

**Порядок wiring:**
1. `UIThemeV3` резолвится из контейнера
2. `UISpriteCache` → wiring спрайтов в тему
3. `HUDPresenter` → `HUDPanelView`, `BuffBarView`
4. `ToastService` → `ToastView`
5. `UIThemeV3` → `HotbarPanelView`, `ContextMenuUI`
6. `MapPresenter` + drag → `MiniMapView` (ранний SetTheme для N-key)
7. `DialoguePresenter` → `DialoguePanelView`
8. `SettingsService` → `PausePanelView`
9. `UIThemeV3` → `LoadingScreenView`
10. `CharacterPresenter` / `DeathPresenter` / `CombatPresenter` → overlay Views

---

## Auto-run фазы (Editor)

При первом открытии проекта Unity автоматически запускает фазы настройки (через `[InitializeOnLoadMethod]` + `EditorApplication.delayCall`):

| Фаза | Назначение | Auto-run |
|------|------------|----------|
| Phase00 URPSetup | Авто-фикс GUID-rot для URP ассетов (UniversalRP.asset + Renderer2D.asset + GraphicsSettings) | ✅ |
| Phase01 SpriteImport | Настройка 184 спрайтов (PPU=64, Point, Alpha) | ✅ |
| Phase01B TmpImport | Импорт TMP Essential Resources (опционально, ExecuteMenuItem) | ✅ |
| Phase02 TagsLayers | Настройка тегов и слоёв (Default, Background, Terrain, Objects, Player, UI) | ✅ |

> **Примечание:** Эти фазы физически находятся в `Assets/Editor/SceneBuilder/` (замороженная Editor-time система), но авто-запускаются и поддерживают актуальное состояние окружения. См. [SCENE_BUILDER_SYSTEM_Old.md](./SCENE_BUILDER_SYSTEM_Old.md) для деталей.

---

## CreateCanvas — структура Canvas

RuntimeSceneBuilder.CreateCanvas() создаёт 3-слойный Canvas:

```
Canvas (ScreenSpaceOverlay, sortingOrder=100, CanvasScaler 1920×1080)
├── HudLayer (stretch, NO Image)
│   ├── HUD Panel (stretch, NO Image) → HUDPanelView
│   ├── Buff Bar (0,1)-(0,1), 200×250 → BuffBarView
│   ├── Toast View (1,1)-(1,1), 300×300 → ToastView
│   ├── Hotbar Panel (0.5,0)-(0.5,0), 500×60 → HotbarPanelView
│   └── MiniMap (stretch) → MiniMapView
├── WindowLayer (stretch, NO Image)
│   ├── Dialogue Panel → DialoguePanelView (Phase 3)
│   ├── Pause Panel → PausePanelView (Phase 4, FIX-OVERLAY)
│   ├── Loading Panel → LoadingScreenView (UI-DISABLED, Phase 5)
│   ├── Inventory Screen → InventoryScreen (UI-DISABLED, Phase 6)
│   ├── Character Panel → CharacterPanelView (UI-DISABLED, Phase 8)
│   ├── Death Screen → DeathScreenView (UI-DISABLED, Phase 8)
│   ├── Combat Overlay → CombatOverlayView (UI-DISABLED, Phase 9)
│   └── ... (5 more combat views, UI-DISABLED)
└── FloatingLayer (stretch, NO Image)
    ├── Context Menu → ContextMenuUI (UI-DISABLED, Phase 7)
    ├── DragVisual (hidden)
    └── ItemContextMenu (hidden)
```

### CreateOverlayView + CanvasGroup (4.3)

`CreateOverlayView<T>()` создаёт stretch-fill корень с `CanvasGroup` (`blocksRaycasts=false`, `interactable=false`). Корень не перехватывает клики — только дочерние панели управляют raycast через свои Image.

---

## ModuleServices Pattern

**Проблема:** SceneOrchestrator в корневом GameLifetimeScope видит только stub-сервисы. Реальные сервисы в дочерних LifetimeScope недоступны (sibling scope visibility).

**Решение:** Каждый модуль регистрирует свои сервисы через статический метод `XxxModuleServices.Register(IContainerBuilder)`, который вызывается из корневого GameLifetimeScope. Это решает проблему sibling scope visibility — все сервисы доступны в корневом scope.

---

## UIComponentResolver — DI-bridge для динамических View

`Entry/UI/UIComponentResolver.cs` — MonoBehaviour, bridge между VContainer и динамически создаваемыми UI Views.

**Проблема:** Views создаются через `AddComponent<T>()` в `CreateCanvas()` — вне VContainer DI. Прямой `scope.Container.Inject()` в `CreateXxx()` вызывал `VContainerException` (MessagePipe брокеры ещё не зарегистрированы).

**Решение (06_18):** Views вызывают `TryInject()` в `Start()` через `FindFirstObjectByType<UIComponentResolver>()` → `resolver.Inject(this)`. `UIComponentResolver` инициализируется после построения контейнера.

---

## Ссылки

- **Замороженная Editor-time система:** [SCENE_BUILDER_SYSTEM_Old.md](./SCENE_BUILDER_SYSTEM_Old.md) — FullSceneBuilder + 20 фаз IScenePhase
- **Архитектура проекта:** [ARCHITECTURE.md](./ARCHITECTURE.md) §Scene Assembly
- **Архитектура кода:** [ARCHITECTURE_CODE.md](./ARCHITECTURE_CODE.md) — метрики, интерфейсы
- **Setup гайд:** [SETUP_GUIDE.md](./SETUP_GUIDE.md) — workflow клонирования + авто-настройка
- **Горячие клавиши:** [!hotkeys.md](./!hotkeys.md)

---

*Создано: 2026-07-14 — выделена runtime-система из SCENE_BUILDER_SYSTEM.md v3.3*
*Editor-time содержимое перенесено в SCENE_BUILDER_SYSTEM_Old.md*
*Статус: АКТИВНАЯ — runtime-сборка сцены при Play*

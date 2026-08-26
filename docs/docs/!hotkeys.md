# ⌨️ Горячие клавиши — Cultivation World Simulator

**Версия:** 4.0
**Дата:** 2026-05-24
**Архитектура:** VContainer + MessagePipe + UniTask (Runtime)

---

## Обзор

Горячие клавиши разделены на две категории:

1. **Runtime** — работают во время игры (GameInputAdapter → PlayerInputService → PlayerService)
2. **Editor-only** — работают только в Unity Editor (MenuItem, SceneBuilder)

Ввод реализован через **Input System package** (`Keyboard.current` опрос), НЕ через устаревший `UnityEngine.Input`.

**Архитектура ввода (3 слоя):**

```
[Слой 1] GameInputAdapter (MonoBehaviour, Update)
    ↓ Keyboard.current + Mouse.current опрос → InputFrameData struct
    ↓ + RMB hold timer ≥ 300мс → ContextMenuRequestedEvent
    ↓ + Digit1-9 → TechniqueSlotSelectedEvent
[Слой 2] IPlayerInputService / PlayerInputService (чистый C#, DI)
    ↓ InputFrameData → свойства (LMB/RMB/Held/WorldPos/Slot)
    ↓ Одноразовые флаги (sticky until ResetFrameFlags)
[Слой 3] PlayerModule.Tick()
    ├─ ClickIntentResolver  → LMB: move/melee/ranged
    ├─ PlayerCombatAdapter  → AttackIntentEvent → CombatModule
    ├─ TrackingService      → RMB short = трекинг
    ├─ HotbarService        → 1-9 слоты
    ├─ PlayerService.Tick() → WASD + click-to-move
    └─ ResetFrameFlags()
```

> **Важно:** VContainer НЕ инжектирует в динамически созданные GameObject.
> `GameInputAdapter` инжектируется вручную через `FindFirstObjectByType<GameLifetimeScope>()`.

---

## 🎮 Runtime — Управление игроком

### Движение

| Клавиша | Действие | Сервис | Примечание |
|---------|----------|--------|------------|
| **W / ↑** | Движение вверх | PlayerInputService | |
| **S / ↓** | Движение вниз | PlayerInputService | |
| **A / ←** | Движение влево | PlayerInputService | Спрайт зеркально отражается |
| **D / →** | Движение вправо | PlayerInputService | Спрайт зеркально отражается |
| **Left Shift** | Бег (удержание) | PlayerInputService | ×1.5 к скорости (4.5 ед/сек) |

> **Диагональная нормализация:** Вектор движения нормализуется при `SqrMagnitude > 1`,
> чтобы диагональное движение не было в √2 раз быстрее прямого.
>
> **Скорость:** Ходьба = 3 ед/сек, Бег = 4.5 ед/сек (`PlayerConfig`).

### Действия

| Клавиша | Действие | Сервис | Событие MessagePipe |
|---------|----------|--------|---------------------|
| **J** | Атака | PlayerCombatAdapter | `AttackIntentEvent("basic_attack")` |
| **K** | Защита / Парирование | PlayerInputService | ⚠️ Флаг установлен, но обработчик не подключен |
| **E** | Взаимодействие | PlayerInputService | `IsInteractPressed` (sticky flag) |
| **I** | Инвентарь (toggle) | PlayerModule | `IsInventoryPressedRaw` → toggle UI state |
| **M** | Медитация | PlayerInputService | `IsMeditatePressed` (sticky flag) |
| **N** | Мини-карта (toggle) | MiniMapView | `Keyboard.current.nKey.wasPressedThisFrame` → `ToggleVisibility()` |
| **1–9** | Слот техники / хотбар | HotbarService | `TechniqueSlotSelectedEvent` → HotbarService |

### Мышь (Фаза 9 — ✅ РЕАЛИЗОВАНО)

| Действие мыши | Намерение | Статус | Файл |
|---------------|-----------|--------|------|
| **ЛКМ (LMB)** короткий клик | Движение к точке | ✅ Реализовано | `ClickIntentResolver.cs` → `ClickToMoveEvent` |
| **ЛКМ (LMB)** на враге (ближний бой) | Атака ближнего боя | ✅ Реализовано | `ClickIntentResolver.cs` → `AttackIntentEvent(IsRanged=false)` |
| **ЛКМ (LMB)** на враге (дальний бой) | Атака дальнего боя | ✅ Реализовано | `ClickIntentResolver.cs` → `AttackIntentEvent(IsRanged=true)` |
| **ПКМ (RMB)** короткий (<0.3с) | Трекинг / слежение | ✅ Реализовано | `TrackingService.cs` → `TrackingTargetEvent` |
| **ПКМ (RMB)** длинный (≥0.3с) | Контекстное меню | ✅ Реализовано | `ContextMenuUI.cs` ← `ContextMenuRequestedEvent` |

### Хотбар (Фаза 9 — ✅ РЕАЛИЗОВАНО)

| Слот | Содержимое | Статус | Примечание |
|------|-----------|--------|------------|
| **1** | WeaponMain (основная рука) | ✅ Реализовано | Зеркалит EquipmentSlot.WeaponMain, рамка оранжевая |
| **2** | WeaponOff (вторичная рука) | ✅ Реализовано | Зеркалит EquipmentSlot.WeaponOff, рамка жёлтая |
| **3–9** | Универсальные слоты | ✅ Реализовано | Техники / расходуемые / инструменты |

> **WASD отменяет click-to-move:** При активном клик-перемещении нажатие WASD сбрасывает цель.

### Системные клавиши

| Клавиша | Действие | Событие MessagePipe |
|---------|----------|---------------------|
| **F5** | Быстрое сохранение | `SaveRequestedEvent(QuickSave)` |
| **F9** | Быстрая загрузка | `LoadRequestedEvent(QuickSave)` |
| **Escape** | Пауза / Закрыть UI | `UIPauseRequestEvent` (только если UI закрыт) / закрытие UI |
| **` / F1** | Панель лога ввода | `InputLogPanel.ToggleVisibility()` |

> ⚠️ **Автосохранение ОТКЛЮЧЕНО** (`AutoSaveConfig.Enabled = false`). На данном этапе разработки автосохранение не требуется. F5/F9 работают вручную.

---

## 🏗️ Runtime — Фазы сборки сцены (SceneOrchestrator)

Запускаются автоматически при старте игры через `SceneOrchestrator` (10 фаз).

| # | Фаза | Описание | Файл |
|---|------|----------|------|
| 1 | CoreValidationPhase | Проверка DI-резолва всех интерфейсов | `Entry/Phases/CoreValidationPhase.cs` |
| 2 | TileMapGenPhase | Генерация тайловой карты | `Entry/Phases/TileMapGenPhase.cs` |
| 3 | WorldInitPhase | Инициализация мира | `Entry/Phases/WorldInitPhase.cs` |
| 4 | PlayerSpawnPhase | Спавн игрока (центр карты) | `Entry/Phases/PlayerSpawnPhase.cs` |
| 5 | NPCSpawnPhase | Спавн NPC | `Entry/Phases/NPCSpawnPhase.cs` |
| 6 | FormationInitPhase | Инициализация формаций | `Entry/Phases/FormationInitPhase.cs` |
| 7 | ChargerInitPhase | Инициализация зарядников | `Entry/Phases/ChargerInitPhase.cs` |
| 8 | QuestInitPhase | Инициализация квестов | `Entry/Phases/QuestInitPhase.cs` |
| 9 | UIInitPhase | Инициализация UI | `Entry/Phases/UIInitPhase.cs` |
| 10 | FinalizePhase | Финализация сборки | `Entry/Phases/FinalizePhase.cs` |

> События: `SceneInitializingEvent` → `SceneReadyEvent` / `SceneAssemblyFailedEvent`

---

## 🔧 Runtime — Иерархия сцены (RuntimeSceneBuilder)

Создаётся программно в `RuntimeSceneBuilder.cs` (585 строк):

| Объект | Компоненты | Sorting Layer | Примечание |
|--------|-----------|---------------|------------|
| Main Camera | Camera + URP Camera Data | — | Orthographic, size=10 |
| Canvas | Canvas (Screen Space Overlay) | — | HUD + Dialogue + Pause + Loading + Hotbar + ContextMenu |
| EventSystem | InputSystemUIInputModule | — | Fallback: StandaloneInputModule |
| World Root → Grid | Grid | — | |
| Terrain Tilemap | Tilemap + TilemapRenderer | Terrain (2) | sortingOrder=0 |
| Objects Tilemap | Tilemap + TilemapRenderer | Objects (3) | sortingOrder=0 |
| Entities Root → Player | SpriteRenderer | Player (4) | sortingOrder=0 |
| Global Light2D | Light2D (Global) | — | Sprite-Lit-Default |
| CameraFollow | CameraFollow | — | Lerp speed=3 |
| GameInputAdapter | GameInputAdapter | — | Input bridge (клавиатура) |
| InputLogPanel | InputLogPanel | — | Input log (скрыт до ` / F1) |
| InventoryScreen | InventoryScreen | — | Inventory UI (скрыт до I-key) |

---

## 🛠️ Editor-only — SceneBuilder (заморожен)

Символы в MenuItem: `%` = Ctrl, `#` = Shift, `&` = Alt.

### Полная сборка сцены

| Путь меню | Описание |
|-----------|----------|
| `Tools → Full Scene Builder → Build All (One Click)` | Все 20 фаз (00–19) |

### Фазы Editor SceneBuilder

| # | Класс | Описание |
|---|-------|----------|
| 00 | Phase00URPSetup | URP Asset + Renderer2D + GraphicsSettings |
| 01 | Phase01Folders | Создание 37 папок |
| 02 | Phase02TagsLayers | Теги, слои, Sorting Layers |
| 03 | Phase03SceneCreation | Создание сцены |
| 04 | Phase04CameraLight | Camera + Light2D |
| 05 | Phase05GameManager | GameManager + системные компоненты |
| 06 | Phase06Player | Player (Rigidbody2D + компоненты) |
| 07 | Phase07UI | Canvas + EventSystem + HUD + Menu |
| 08 | Phase08Tilemap | Grid + Tilemap |
| 09 | Phase09GenerateAssets | JSON → ScriptableObjects |
| 10 | Phase10GenerateSprites | Процедурные спрайты тайлов |
| 11 | Phase11GenerateUIPrefabs | UI-префабы формаций |
| 12 | Phase12TMPEssentials | Импорт TMP |
| 13 | Phase13SaveScene | Сохранение сцены |
| 14 | Phase14CreateTileAssets | Tile .asset файлы |
| 15 | Phase15ConfigureTestLocation | Тестовая локация |
| 16 | Phase16InventoryData | BackpackData + Test Equipment |
| 17 | Phase17InventoryUI | InventoryScreen + панели + wiring |
| 18 | Phase18InventoryComponents | SpiritStorage + StorageRing |
| 19 | Phase19NPCPlacement | 7 NPC на тестовой поляне |

> **См. также:** [SCENE_BUILDER_SYSTEM.md](./SCENE_BUILDER_SYSTEM.md) — полное описание

---

## 🔍 Система логирования ввода (INPUT-LOG)

Нажмите **`** (Backquote/Tilde) или **F1** для открытия InputLogPanel.

Панель показывает в реальном времени:
- `[KEY]` — нажатия клавиш (WASD, I, J, K, E, M, Shift, F5, F9, Escape)
- `[ACT]` — результирующие действия (MoveStart, MoveStop, ToggleInventory, Attack, Run)

**Архитектура логирования:**
```
GameInputAdapter.Update()
  → PublishKey() → InputKeyEvent → InputLogService (кольцевой буфер 200)
  → PublishAction() → InputActionEvent → InputLogService

PlayerModule.Tick()
  → PublishAction() → InputActionEvent (InventoryOpen/InventoryClose)

InputLogPanel (UI)
  ← подписка на InputKeyEvent/InputActionEvent → RefreshLog()
```

**Формат записи:**
```
[KEY] f1234 I Pressed
[ACT] f1234 InventoryOpen Playing → Inventory, InputDisabled=true
[KEY] f1235 W Pressed dir=↑
[ACT] f1235 MoveStart (0.0,1.0)
```

---

## 🔬 Аудит Phase 9 (LMB/RMB/Hotbar) — 2026-05-24 (v2 — после отката + merge)

> ⚠️ **ВАЖНО:** Предыдущий аудит (до merge) признан недействительным — выполнялся после отката окружения.
> Данный аудит проведён на актуальном коде после `git merge origin/main` (commit bc7f428),
> включающем 24 remote-коммита: Спринты 3-8 боевой системы, 88+ фиксов компиляции, checkpoint Phase 9.

### Критический разрыв боевого пайплайна ⚠️

**PlayerCombatAdapter → TechniqueUsedEvent → CombatService НЕ СВЯЗАН!**
- `PlayerCombatAdapter` публикует `TechniqueUsedEvent("basic_attack")`, но `CombatService` на него **НЕ подписан**
- Реальный боевой пайплайн: `CombatService.ExecuteAttack()` — прямой вызов через `ICombatService`
- CombatService сам публикует `TechniqueUsedEvent` **после** расчёта урона (информационное событие)
- Phase 9 должна связать: LMB/RMB → AttackIntentEvent → CombatModule → CombatService.ExecuteAttack()

### Реализовано ✅

| Компонент | Статус | Файл | Описание |
|-----------|--------|------|----------|
| 3-слойная архитектура ввода | ✅ Реализовано | GameInputAdapter → PlayerInputService → PlayerModule | Input → Intent → Action |
| WASD движение | ✅ Реализовано | GameInputAdapter.cs | Нормализация + зеркальное отражение |
| Shift бег | ✅ Реализовано | GameInputAdapter.cs | ×1.5 к скорости |
| J / ЛКМ атака | ✅ Реализовано | PlayerCombatAdapter.cs + ClickIntentResolver.cs | `AttackIntentEvent` → CombatModule → CombatService |
| Mouse.current опрос | ✅ Реализовано | GameInputAdapter.cs | LMB/RMB + мировая позиция + IsOverUI |
| RMB hold timer | ✅ Реализовано | GameInputAdapter.cs | ≥0.3с → ContextMenuRequestedEvent |
| InputFrameData struct | ✅ Реализовано | Core/Data/InputFrameData.cs | Zero-alloc, промилле позиция |
| MouseInputEvent | ✅ Реализовано | Core/Messaging/Contracts/MouseContracts.cs | LMB/RMB + worldPos (промилле) |
| ClickToMoveEvent | ✅ Реализовано | MouseContracts.cs | Целевая позиция перемещения |
| TrackingTargetEvent | ✅ Реализовано | MouseContracts.cs | RMB короткий клик → трекинг |
| ContextMenuRequestedEvent | ✅ Реализовано | MouseContracts.cs | RMB ≥0.3с → контекстное меню |
| AttackIntentEvent | ✅ Реализовано | Core/Messaging/Contracts/CombatContracts.cs | Боевой мост: melee/ranged + target |
| TechniqueSlotSelectedEvent | ✅ Реализовано | Core/Messaging/Contracts/HotbarContracts.cs | Выбор слота 1-9 |
| HotbarSlotChangedEvent | ✅ Реализовано | HotbarContracts.cs | Изменение содержимого слота |
| ClickIntentResolver | ✅ Реализовано | Modules/Player/ClickIntentResolver.cs | LMB: move/ranged/melee |
| TrackingService | ✅ Реализовано | Modules/Player/TrackingService.cs | RMB короткий клик → трекинг |
| HotbarService | ✅ Реализовано | Modules/Player/HotbarService.cs | 9 слотов (WeaponMain/Off + универсальные) |
| HotbarPanelView | ✅ Реализовано | Entry/UI/HotbarPanelView.cs | UI: 9 слотов внизу экрана |
| ContextMenuUI | ✅ Реализовано | Entry/UI/ContextMenuUI.cs | RMB ≥0.3с контекстное меню |
| EquipmentData.attackRange | ✅ Реализовано | EquipmentData.cs | int, default 2, >2 = ranged |
| WASD → cancel click-to-move | ✅ Реализовано | PlayerService.cs | WASD сбрасывает ClickToMoveTarget |
| Click-to-move | ✅ Реализовано | PlayerService.cs | Подписка на ClickToMoveEvent, движение к точке |
| CombatModule → AttackIntentEvent | ✅ Реализовано | CombatModule.cs | Боевой мост от ЛКМ/J к CombatService |
| TechniqueUsedEvent.QiCost int | ✅ Реализовано | CombatContracts.cs | ЗАПРЕТ 3.9: float→int |
| I инвентарь (toggle) | ✅ Реализовано | PlayerModule.cs | P0-02 FIX: IsInventoryPressedRaw обходит InputDisabled |
| E взаимодействие | ✅ Реализовано | InteractionService.cs | UIInteractRequestEvent → TryInteract |
| M медитация | ✅ Реализовано | PlayerInputService.cs | Sticky flag |
| F5/F9 сохранение/загрузка | ✅ Реализовано | GameInputAdapter.cs | SaveRequestedEvent / LoadRequestedEvent |
| Escape (пауза/закрытие UI) | ✅ Реализовано | GameInputAdapter.cs | P0-INPUT-02 FIX: проверка CurrentUIState |

### НЕ реализовано ❌ (будущие фазы)

| Компонент | Статус | Описание |
|-----------|--------|----------|
| K (защита) | ⚠️ Не подключен | `IsDefendPressed` установлен, но обработчик не подключен |
| Нет геймпада | 📋 Запланировано | Только `Keyboard.current`, нет Gamepad bindings |
| Нет стамины бега | 📋 Запланировано | `PlayerData` имеет `CurrentStamina`/`MaxStamina`, но `ProcessMovement()` не тратит |
| Нет клавиши сбора | 📋 Запланировано | Legacy имел `F` для Harvest; сейчас `E` = Interact, Harvest не подключён |

### Детали взаимодействия (текущие)

```
┌──────────────────────────────────────────────────────────────┐
│ ВХОД (GameInputAdapter)                                      │
│                                                               │
│  Keyboard.current опрос:                                     │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐        │
│  │ WASD    │  │ J       │  │ I       │  │ E/M/K   │        │
│  │ MoveDir │  │ Attack  │  │ Invent. │  │ Flags   │        │
│  └────┬────┘  └────┬────┘  └────┬────┘  └────┬────┘        │
│       │            │            │            │               │
│       ▼            ▼            ▼            ▼               │
│  UpdateInputState() — все флаги в PlayerInputService         │
└──────────────────────────────────────────────────────────────┘
                        │
                        ▼
┌──────────────────────────────────────────────────────────────┐
│ ОБРАБОТКА (PlayerModule.Tick)                                │
│                                                               │
│  1. I-key toggle → IUIService.SetUIState()                  │
│  2. PlayerService.Tick() → ProcessMovement()                 │
│  3. PlayerCombatAdapter.ProcessCombatInput()                 │
│  4. ResetFrameFlags() — в конце кадра                        │
└──────────────────────────────────────────────────────────────┘
                        │
                        ▼
┌──────────────────────────────────────────────────────────────┐
│ РЕЗУЛЬТАТ (MessagePipe)                                      │
│                                                               │
│  Movement → PlayerPositionChangedEvent                       │
│  Attack   → AttackIntentEvent("basic_attack")  [Фаза 9D]       │
│  UI       → GameStateChangedEvent                            │
│  Save/Load → SaveRequestedEvent / LoadRequestedEvent         │
└──────────────────────────────────────────────────────────────┘
```

### Детали взаимодействия (Phase 9 — план)

```
┌──────────────────────────────────────────────────────────────┐
│ ВХОД (GameInputAdapter — РАСШИРЕННЫЙ)                        │
│                                                               │
│  Keyboard.current:  WASD, J, I, E, M, K, F5, F9, Esc       │
│  Mouse.current:     LMB, RMB + позиция                       │
│  Keyboard.current:  1-9 (хотбар)                             │
│                                                               │
│  ┌───────────┐  ┌───────────┐  ┌───────────┐                │
│  │ LMB click │  │ RMB short │  │ RMB long  │                │
│  │ MouseInput│  │ < 0.3с   │  │ ≥ 0.3с    │                │
│  │ Event     │  │ Tracking  │  │ Context   │                │
│  └─────┬─────┘  └─────┬─────┘  └─────┬─────┘                │
│        │               │               │                      │
│  ┌─────▼─────┐         │               │                      │
│  │ Click     │         │               │                      │
│  │ Intent    │         │               │                      │
│  │ Resolver  │         │               │                      │
│  │           │         │               │                      │
│  │ attackRange│        │               │                      │
│  │ ≤2: melee │         │               │                      │
│  │ >2: ranged│         │               │                      │
│  │ no enemy: │         │               │                      │
│  │   move    │         │               │                      │
│  └─────┬─────┘         │               │                      │
│        │               │               │                      │
│  ┌─────▼─────┐  ┌──────▼──────┐ ┌──────▼──────┐             │
│  │ Attack    │  │ Tracking    │ │ ContextMenu │             │
│  │ Intent    │  │ Service     │ │ UI          │             │
│  │ Event     │  │             │ │             │             │
│  └───────────┘  └─────────────┘ └─────────────┘             │
│                                                               │
│  ┌───────────┐                                               │
│  │ 1-9 keys  │ → HotbarService → HotbarSlotChangedEvent     │
│  │ Hotbar    │   Slot1=WeaponMain, Slot2=WeaponOff           │
│  │           │   Slots 3-9 = универсальные                   │
│  └───────────┘                                               │
│                                                               │
│  WASD → отменяет click-to-move (сброс TargetPosition)       │
└──────────────────────────────────────────────────────────────┘
```

### Ключевые файлы для Phase 9 (требуют модификации)

| Файл | Изменение |
|------|-----------|
| `Core/Data/ScriptableObjects/EquipmentData.cs` | Добавить `attackRange` (default 2, int) |
| `Core/Messaging/Contracts/MouseContracts.cs` | MouseInputEvent, ClickToMoveEvent, ContextMenuRequestedEvent, TrackingTargetEvent |
| `Core/Messaging/Contracts/HotbarContracts.cs` | TechniqueSlotSelectedEvent, HotbarSlotChangedEvent |
| `Core/Messaging/Contracts/CombatContracts.cs` | AttackIntentEvent (Фаза 9A) |
| `Core/Interfaces/IPlayerInputService.cs` | Добавить LMB/RMB флаги, TargetPosition, HotbarSlot |
| `Modules/Player/PlayerInputService.cs` | Реализовать новые свойства |
| `Entry/UI/GameInputAdapter.cs` | Добавить Mouse.current опрос, слоты 1-9 |
| `Modules/Player/PlayerModule.cs` | Интеграция ClickIntentResolver |
| `Modules/Player/PlayerCombatAdapter.cs` | Обработка AttackIntentEvent |
| `Modules/Player/PlayerModuleServices.cs` | Регистрация новых сервисов |

### Новые файлы для Phase 9

| Файл | Описание |
|------|----------|
| `Modules/Player/ClickIntentResolver.cs` | Разбор LMB: move/ranged/melee |
| `Modules/Player/TrackingService.cs` | Трекинг цели (RMB короткий клик) |
| `Modules/Player/HotbarService.cs` | Управление слотами хотбара 1-9 |
| `Modules/UI/ContextMenuUI.cs` | Контекстное меню (RMB ≥ 0.3с) |

---

## ⚠️ Известные ограничения текущего ввода

| Проблема | Статус | Описание |
|----------|--------|----------|
| K (защита) | ⚠️ Не подключен | `IsDefendPressed` установлен, но `PlayerCombatAdapter` не обрабатывает |
| Нет геймпада | 📋 Запланировано | Только `Keyboard.current`, нет Gamepad bindings |
| Нет стамины бега | 📋 Запланировано | `PlayerData` имеет `CurrentStamina`/`MaxStamina`, но `ProcessMovement()` не тратит |
| Нет клавиши сбора | 📋 Запланировано | Legacy имел `F` для Harvest; сейчас `E` = Interact, Harvest не подключён |

> **Исторический баг Escape (ИСПРАВЛЕН):** Ранее при закрытии инвентаря через Escape — InputDisabled
> оставался `true`. Фикс P0-INPUT-01: подписка на GameStateChangedEvent → сброс InputDisabled при Playing.

---

## 🎨 Цвета маркеров по редкости

| Редкость | Цвет | RGB |
|----------|------|-----|
| Common | Серый | (0.7, 0.7, 0.7) |
| Uncommon | Зелёный | (0.3, 0.9, 0.3) |
| Rare | Синий | (0.2, 0.5, 1.0) |
| Epic | Фиолетовый | (0.7, 0.2, 1.0) |
| Legendary | Оранжевый | (1.0, 0.6, 0.1) |
| Mythic | Красный | (1.0, 0.15, 0.15) |

---

## 📦 Архив: Legacy горячие клавиши (для будущего использования)

> **Заморожено** — legacy код в `UnityProject/Legacy/UnityAssets/Scripts/`, НЕ компилируется.
> Старые комбинации сохранены для справки и возможного возвращения при развитии систем.

### Legacy — Экипировка (EquipmentSceneSpawner)

| Комбинация | Действие | Файл |
|---|---|---|
| **Ctrl+G** | 3 случайных предмета (L1) рядом с Player | `EquipmentSceneSpawner.cs` |
| **Ctrl+Shift+G** | 10 случайных предметов (L1) рядом с Player | `EquipmentSceneSpawner.cs` |
| **Ctrl+Alt+G** | 5 предметов уровня 3 рядом с Player | `EquipmentSceneSpawner.cs` |
| **Ctrl+F1** | 1 оружие T1 → инвентарь Player | `EquipmentSceneSpawner.cs` |
| **Ctrl+F2** | 1 броня T1 → инвентарь Player | `EquipmentSceneSpawner.cs` |
| **Ctrl+F3** | 3 случайных предмета → инвентарь Player | `EquipmentSceneSpawner.cs` |

### Legacy — NPC (NPCSceneSpawner)

| Комбинация | Действие | Файл |
|---|---|---|
| **Ctrl+N** | 1 случайный NPC рядом с Player | `NPCSceneSpawner.cs` |
| **Ctrl+Shift+N** | 5 NPC разных ролей рядом с Player | `NPCSceneSpawner.cs` |
| **Ctrl+F5** | 1 Merchant рядом с Player | `NPCSceneSpawner.cs` |
| **Ctrl+F6** | 1 Monster/Enemy рядом с Player | `NPCSceneSpawner.cs` |

### Legacy — Игровые клавиши (PlayerController)

| Клавиша | Действие | Отличие от текущей |
|---------|----------|-------------------|
| **WASD / Стрелки** | Движение | Движение через Rigidbody2D.velocity |
| **Shift** | Бег | Бег тратил стамину |
| **Space** | Атака | Сейчас **J** |
| **F** | Сбор ресурсов | Сейчас нет отдельной клавиши |
| **F5** | Медитация | Сейчас **M** |
| **Q / E** | Цикл целей | Сейчас нет |
| **1–9** | Слоты техник | Работало через InputAction composites |

### Legacy — Генерация .asset файлов (без hotkey)

| MenuItem | Кол-во SO | Описание |
|---|---|---|
| `Tools/Equipment/Generate/Weapon Set (T1)` | 36 | 12 подтипов × 3 грейда |
| `Tools/Equipment/Generate/Weapon Set (All Tiers)` | 180 | ×5 тиров |
| `Tools/Equipment/Generate/Armor Set (T1)` | 63 | 7×3 вес.класс × 3 грейда |
| `Tools/Equipment/Generate/Armor Set (All Tiers)` | 315 | ×5 тиров |
| `Tools/Equipment/Generate/Full Set (T1)` | 99 | Оружие + броня T1 |
| `Tools/Equipment/Generate/Random Loot` | 3 | 3 случайных .asset |
| `Tools/Equipment/Clear Generated Equipment` | — | Удаление папки `Generated/` |

### Legacy — NPC спавн по ролям (без hotkey)

| MenuItem | Описание |
|---|---|
| `Tools/NPC/Spawn In Scene/Guard` | 1 Guard в сцену |
| `Tools/NPC/Spawn In Scene/Elder` | 1 Elder в сцену |
| `Tools/NPC/Spawn In Scene/Enemy` | 1 Enemy в сцену |
| `Tools/NPC/Spawn In Scene/Cultivator` | 1 Cultivator в сцену |
| `Tools/NPC/Spawn In Scene/Disciple` | 1 Disciple в сцену |
| `Tools/NPC/Clear All NPCs` | Удалить все NPC из сцены |

---

## 🔄 Типичный рабочий процесс (текущий)

### Запуск игры
1. Unity Play → `GameEntryPoint` → `GameLifetimeScope` → `SceneOrchestrator`
2. 10 фаз сборки → `SceneReadyEvent`
3. `WASD` — движение, `Shift` — бег, `J` — атака, `I` — инвентарь

### Диагностика ввода
- **InputLogPanel** — нажмите ` или F1 для реального времени
- `[GameInputAdapter]` логи в Console — инъекция VContainer
- `[PlayerModule]` логи — первый Tick, первый ввод движения
- `[PlayerService]` логи — первое реальное движение
- `PlayerInputService` — проверить `_injected` и `_inputDisabled`
- `PlayerModule.Tick()` — проверить вызов `ProcessMovement()` и `ResetFrameFlags()`

### Типичные проблемы и решения

| Симптом | Вероятная причина | Решение |
|---------|-------------------|--------|
| Не работает движение И инвентарь | GameInputAdapter не инжектирован | Проверить Console: `[GameInputAdapter] VContainer инъекция выполнена` |
| Не работает движение И инвентарь | Keyboard.current == null | Проверить Player Settings → Active Input Handling = 'Both' |
| Инвентарь открывается, но не закрывается | IsInventoryPressedRaw не работает | Проверить PlayerModule.Tick() логику toggle |
| После Escape в инвентаре — нельзя двигаться | GameStateChangedEvent не обработан | Проверить PlayerModule.OnGameStateChanged() |

---

## 📎 Связанные документы

| Документ | Описание |
|----------|----------|
| [ARCHITECTURE.md](./ARCHITECTURE.md) | Общая архитектура (VContainer + MessagePipe) |
| [SCENE_BUILDER_SYSTEM.md](./SCENE_BUILDER_SYSTEM.md) | Editor SceneBuilder (фазы 00–19) |
| [SORTING_LAYERS.md](./SORTING_LAYERS.md) | Порядок рендеринга 2D |
| [CONFIGURATIONS.md](./CONFIGURATIONS.md) | Конфигурации (уровни, техники, предметы) |

---

*Документ создано: 2026-04-30 08:09:40 UTC*
*Редактировано: 2026-05-24 — v4.0: Фаза 9 реализована! LMB/RMB/Hotbar/ContextMenu/ClickToMove все ✅. Обновлена архитектура 3 слоёв (InputFrameData), добавлена секция мыши, хотбар, click-to-move, WASD cancel, AttackIntentEvent боевой мост. Обновлены таблицы "Реализовано/Не реализовано".*
*Редактировано: 2026-05-24 — v3.0: полный аудит Phase 9 (LMB/RMB/Hotbar). Добавлены: секция мыши, хотбар, текущая и планируемая архитектура взаимодействия, детали Phase 9 (реализовано/не реализовано), списки файлов для модификации и создания. Исправлен баг Escape (P0-INPUT-01 FIX подтверждён).*
*Редактировано: 2026-05-18 — v2.1: добавлена секция InputLogPanel, баг Escape+Inventory (P0), диагностика, таблица проблем. Обновлена иерархия сцены (InputLogPanel, InventoryScreen).*
*Редактировано: 2026-05-15 — v2.0: полная переработка под новую архитектуру (VContainer + MessagePipe + UniTask). Runtime ввод через GameInputAdapter + PlayerInputService. Legacy горячие клавиши перемещены в архив.*
*Проект: Cultivation World Simulator (Unity 6.3 URP 2D)*

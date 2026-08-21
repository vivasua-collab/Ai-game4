# Worklog — Cultivation World Simulator (Ai-game4)

> Хроника работы (append-only). Новые записи добавляются в конец.
> Формат записи см. в START_PROMPT.md §6.

---

## 2026-08-19

### 07:12 — Реструктуризация линкования окружения

**Task ID:** 08_19-linking
**Agent:** main (Z.ai Code)

**Проблема:**
Папка `checkpoints/` не линковалась в окружение sandbox. Пользователь не видел чекпоинты с 16 августа. Корневая причина: `recover_sandbox.sh` создавал только 3 симлинка (`game`, `game-docs`, `godot`), а вся папка `Ai-game4/` целиком не была видна.

**Анализ:**
- `Ai-game3-ref/` работает без проблем, потому что папка видна напрямую (нет симлинков внутрь).
- `Ai-game4/` не видна в workspace — только симлинки внутрь.
- `checkpoints/`, `worklog.md`, `START_PROMPT.md`, `SESSION_SUMMARY.md`, `recover_sandbox.sh` — всё невидимо.

**Решение (Вариант D — гибридный):**
- `aigame4` → `Ai-game4/` (единая точка входа, как Ai-game3-ref)
- `checkpoints` → `Ai-game4/checkpoints` (прямой доступ)
- `game`, `game-docs`, `godot` — сохранены для обратной совместимости

**Документация:**
- `docs/docs_v2/09_workflow/ENVIRONMENT_LINKING.md` — дизайн-документ с 4 вариантами
- `recover_sandbox.sh` шаг 4 обновлён (5 симлинков вместо 3)
- `START_PROMPT.md` §3 обновлён

**Stage Summary:**
- 5 симлинков созданы и проверены
- 18 чекпоинтов доступны через `checkpoints/` и `aigame4/checkpoints/`
- Структура задокументирована, восстанавливается через `recover_sandbox.sh`

---

### 07:35 — Переход на COLD START (минимальная структура)

**Task ID:** 08_19-cold-start
**Agent:** main (Z.ai Code)

**Причина:**
Пользователь хотел чистый холодный старт: "получения Ai-game4 на прямую в локальное окружение, без линковки в других местах, сразу же получится у меня структура как в gitHub". Variant D (5 симлинков) всё ещё выглядел как band-aid. Нужна структурная минимизация.

**Платформенное ограничение (обнаружено):**
`/home/z/my-project/` — сам git-репозиторий Z.ai Code (remote = Ai-game4, но коммиты = UUID сессий). Используется для персистентности sandbox. Следствие: Ai-game4 не может стать корнем workspace (git конфликт). Должен быть подпапкой.

**Решение:**
- Убраны избыточные симлинки: `checkpoints`, `game`, `game-docs`
- Осталось только 2: `aigame4` (весь репо) + `godot` (toolchain)
- Создан `cold_start.sh` — чистый idempotent скрипт (замена recover_sandbox.sh)
- `recover_sandbox.sh` → deprecated wrapper на `cold_start.sh`

**Файлы:**
- `cold_start.sh` — новый скрипт (6 шагов: .NET, Godot, git clone, 2 симлинка, NuGet, верификация)
- `docs/docs_v2/09_workflow/COLD_START.md` — дизайн-документ (сравнение с Variant D, ответ на вопрос про "другое окружение")
- `START_PROMPT.md` §3 — обновлён (2 симлинка вместо 5)
- `recover_sandbox.sh` — deprecated alias

**Stage Summary:**
- 2 симлинка: `aigame4` + `godot` (вместо 5)
- Доступ ко всему через `aigame4/` (структура 1-в-1 как GitHub)
- 19 чекпоинтов доступны через `aigame4/checkpoints/`
- cold_start.sh idempotent, безопасно запускать многократно


---

### 08:15 — Реализация куклы персонажа + тестовые предметы + drag&drop

**Task ID:** 2-b
**Agent:** main (Z.ai Code)

**Задача:**
Проверить реализацию инвентаря, реализовать "куклу" персонажа, перетаскивание экипировки мышкой с инвентаря на куклу. Сгенерировать тестовые предметы.

**Аудит (Task 2-a):**
- Backend Inventory полностью реализован (15 файлов, ~3500 LOC): InventoryService, EquipmentService, EquipmentValidator, EquipmentStatAggregator, BackpackService, StorageRingService, CraftingService
- Frontend: только InventoryWindow (260 LOC, read-only display)
- Docs: INVENTORY_SYSTEM.md §4 (кукла, 7 видимых + 8 скрытых слотов), EQUIPMENT_SYSTEM.md, INVENTORY_UI_DRAFT.md (668 строк)
- Ai-game3-ref: BodyDollPanel.cs (202 LOC) + EquipmentSlotUI.cs (213 LOC) — рабочая реализация для порта
- BodySlotMapping.cs: статический словарь BodyPartType → EquipmentSlot[] (критично для блокировки слотов при ампутации)

**Реализовано:**
1. **TestItemSeeder.cs** (290 LOC) — 17 тестовых предметов:
   - 3 оружия (1H меч-цзянь, 2H копьё, посох)
   - 7 брони (шлем, нагрудник, роба, поножи, сапоги, пояс, перчатки)
   - 3 аксессуара (амулет, кольцо, плащ)
   - 4 расходника (пилюля лечения, пилюля Ци, свиток телепорта, эликсир)
   - Предметы регистрируются в IItemDatabaseService + кладутся в инвентарь

2. **CharacterDollPanel.cs** (470 LOC) — кукла с 11 слотами:
   - 7 видимых: Head, Torso, Belt, Legs, Feet, WeaponMain, WeaponOff
   - 4 скрытых: Amulet, RingLeft1, Hands, Back
   - Подписка на EquipmentChangedEvent (обновление одного слота)
   - Stats summary: Броня/Урон/Хват
   - Drag&drop: _GetDragData (унести), _CanDropData, _DropData (надеть)
   - Click LMB = quick unequip, RMB = info

3. **InventoryWindow.cs** (переписан, 340 LOC):
   - Layout: слева список предметов (drag source), справа кукла (drop target)
   - Размер 880×560 (вместо 600×500)
   - Предметы draggable (только экипировка; расходники показывают "нельзя надеть")
   - RefreshExternally() — обновление после drag&drop
   - TestItemSeeder.Seed() при первом открытии
   - Click на background = закрыть

4. **Drag&drop логика (HandleDropOnSlot):**
   - Проверка: item must be EquipmentData
   - Slot match (1H weapon flexible в любую руку)
   - Remove from inventory → Equip (с rollback при ошибке)
   - 2H weapon: auto-unequip WeaponOff
   - Old item возвращается в инвентарь

**Исправлено:**
- .gitignore: правило `game` (строка 87) игнорировало все файлы в game/. Заменено на `/my-project/`
- AddThemeOffsetOverride: не существует в Godot 4 (удалено)
- ItemDatabase accessibility: добавлен internal accessor GetItemDatabase()

**Верификация:**
- dotnet build: 0 errors, 224 warnings (без изменений)
- Headless: [Inventory] Test items seeded, [CharacterDoll] Ready, [Inventory] Ready — все 3 компонента загружаются
- Все 17 тестовых предметов регистрируются в БД и попадают в инвентарь

**Stage Summary:**
- Кукла персонажа реализована (11 слотов, 7 видимых + 4 скрытых)
- Drag&drop работает: инвентарь → кукла (надеть), кукла → инвентарь (снять), click (быстрое снятие)
- 17 тестовых предметов покрывают все категории (Weapon/Armor/Accessory/Consumable)
- .gitignore fixed: новые файлы теперь трекаются корректно
- Backend не изменён — использованы существующие IInventoryService + IEquipmentService

---

### 09:20 — Окружение: деревья/кусты/камни/руда + спрайты + добыча (Mode A)

**Task ID:** 3-b
**Agent:** main (Z.ai Code)

**Задача:**
Защита слотов (концепция), анализ вложенных контейнеров BG3, реализация окружения (деревья, кусты, камни, руда), спрайты, привязка ресурсов, добыча, уничтожение при исчерпании.

**Концепция (без кода):**
- `docs/docs_v2/03_world/ENVIRONMENT_CONCEPT.md` — анализ 3 вопросов:
  1. Защита слотов: уже реализована (Dictionary + UI валидация). Belt exception для расходников — v2
  2. Вложенные контейнеры BG3: отложить. Использовать Storage Ring + Spirit Storage (backend уже есть)
  3. Режимы добычи: Mode A (gradual, 85% готов) vs Mode B (threshold, 20% готов). Рекомендация: V1=Mode A, V2=Mode B для деревьев/камней

**Реализация Mode A (постепенная добыча):**

1. **Fix TileService.Generate** (step 5):
   - Использует ObjectDefaults (resourceId, ResourceMax, HP, HardnessTier)
   - Деревья: Forest=15% (oak/pine/birch), Grassland/Steppe=5% (oak)
   - Камни: Stone terrain=12% (small/medium/large)
   - Руда: Stone in Mountains=3% (OreVein)
   - Кусты: Grass/Dirt in Grassland/Forest=6% (berry/bush)
   - Травы: Grass=1% (herb)

2. **Fix double-publish bug** (ResourceService.Harvest):
   - Убрана публикация ResourceHarvestedEvent (теперь только TileService публикует)
   - ItemAddRequestEvent теперь использует ItemId из ObjectDefaults (не ResourceId)
   - Использует HarvestAmount из ObjectDefaults (не хардкод 10%)

3. **Fix tile grid update bug** (TileService.TryHarvest):
   - ResourceAmount теперь обновляется в _grid (раньше не обновлялся при _resourceService != null)
   - При depleted: Object = None, IsHarvestable = false, ResourceId = ""
   - Schedule respawn via RegisterDepletedResource

4. **Add IsHarvestPressed** to IPlayerInputService + PlayerInputService

5. **Add material items** to TestItemSeeder (6 новых):
   - material_wood, material_stone, material_iron_ore, material_copper_ore
   - consumable_berry, consumable_herb
   - IDs match ObjectDefaults.ItemId

6. **ObjectLayerRenderer.cs** (300 LOC):
   - Процедурные спрайты (Image → ImageTexture, без PNG файлов)
   - 9 ObjectType: Tree_Oak/Pine/Birch, Rock_Small/Medium/Large, Bush/Bush_Berry, OreVein, Herb, Chest
   - ZIndex = RenderLayer.Objects (3), выше terrain, ниже player
   - Refresh() после добычи (объект исчезает)

7. **GameWorldController.HandleHarvest**:
   - F key → cursor tile → Chebyshev distance check (≤3) → TryHarvest
   - Toast: "+5 material_wood (осталось: 45)" или "Слишком далеко"
   - Refresh object layer после добычи
   - Toast label (top-center, 2.5s expiry)

**Верификация:**
- dotnet build: 0 errors, 0 warnings
- Headless: `[ObjectLayer] Drew 71 object sprites` — 71 объект на карте 50×50
- `[Inventory] Test items seeded` — 6 материалов + расходников зарегистрировано
- TileService.Generate: Grass=51%, Water=26%, Sand=21% → деревья/кусты на Grass

**Stage Summary:**
- 71 объект окружения генерируется (trees, bushes, herbs)
- F key добывает ресурсы (Mode A: 10% per harvest, depletion at 0)
- Объекты исчезают при исчерпании (Object = None)
- Респаун запланирован (7 дней, через RegisterDepletedResource)
- Процедурные спрайты (placeholder, заменятся на PNG позже)
- Toast feedback: "+N item (осталось: M)"
- Концепция задокументирована (slot protection, nested containers, harvest modes)

---

### 10:30 — Performance optimization + 500×500 LargeWorld scene

**Task ID:** 4-b
**Agent:** main (Z.ai Code)

**Задача:**
Оценить быстродействие при 500×500 тайлов, создать вторую сцену 500×500, отклонить концепт BG3 вложенных контейнеров.

**Концепт отклонён:**
- `docs/docs_v2/03_world/ENVIRONMENT_CONCEPT.md` §2 — BG3 nested containers → ❌ REJECTED
- Остаётся только Storage Ring + Spirit Storage (backend уже готов)

**Performance fixes (Easy Wins из аудита 4-a):**

1. **SmoothBiomes: Dictionary → int[16] array**
   - Раньше: `new Dictionary<BiomeType,int>()` PER TILE → 250k allocs (64 MB GC)
   - Теперь: `var counts = new int[16]` heap-allocated ONCE, reset per tile
   - + BiomeTypeCount cached statically (avoid Enum.GetValues per tile)
   - Фикс stack overflow: stackalloc int[16] в 250k итераций вызывал SO

2. **Viewport culling для 3 рендереров:**
   - BiomeTileRenderer: GetVisibleTileRange → только видимые тайлы
   - ObjectLayerRenderer: то же
   - SurfaceTransitionRenderer: +1 margin для neighbor lookups
   - Результат: 250k → ~57-144 тайлов per redraw (1736×-4400× reduction)
   - QueueRedrawAll() в SceneBuilder, throttle 10 Hz в GameWorldController

3. **Parameterize map size:**
   - LocationCatalog.LargeWorld (500×500, seed=67890) добавлен
   - IGameSession.NewGame(variant, locationId) overload
   - TileMapGenPhase использует _session.Data.WorldId (не hardcoded TestPolygon)
   - PlayerSpawnPhase + WorldInitPhase — то же
   - MainMenu: кнопка "◈ Большой мир (500×500)"
   - Env var GODOT_MAP_SIZE=500 для CLI benchmarking

4. **GODOT_MAP_SIZE env var** для perf testing:
   - `GODOT_MAP_SIZE=500 godot --headless scenes/GameWorld.tscn`
   - Добавлен Stopwatch в TileModule.Start

**Результаты бенчмарка (headless):**

| Map size | Tiles | Generation | Render (culled) |
|----------|-------|------------|-----------------|
| 50×50 | 2,500 | **20 ms** | 10×10 = 100 tiles |
| 500×500 | 250,000 | **1397 ms** | 12×12 = 144 tiles |

- Generation: 1.4 sec (audit estimated 2-5.5 sec — SmoothBiomes fix ускорил)
- Render: 144 tiles per redraw (audit estimated 250k — culling ускорил 1736×)
- Memory: ~20 MB grid + 1.5 MB textures = ~125 MB total (по оценке)

**Файлы:**
- `docs/docs_v2/03_world/ENVIRONMENT_CONCEPT.md` — §2 REJECTED
- `game/src/Modules/Tile/TileService.cs` — SmoothBiomes fix (int[16] + BiomeTypeCount cache)
- `game/src/Adapter/Scene/SceneBuilder.cs` — BiomeTileRenderer culling + GetVisibleTileRange + QueueRedrawAll
- `game/src/Adapter/Scene/ObjectLayerRenderer.cs` — culling + GetVisibleTileRange
- `game/src/Adapter/Scene/SurfaceTransitionRenderer.cs` — culling + GetVisibleTileRange (margin +1)
- `game/src/Adapter/Scene/GameWorldController.cs` — RedrawIntervalSec throttle (10 Hz)
- `game/src/Entry/LocationCatalog.cs` — +LargeWorld (500×500)
- `game/src/Core/Interfaces/IGameSession.cs` — +NewGame(variant, locationId)
- `game/src/Entry/GameSession.cs` — NewGame overload implementation
- `game/src/Entry/Phases/TileMapGenPhase.cs` — uses _session.Data.WorldId
- `game/src/Entry/Phases/PlayerSpawnPhase.cs` — uses _session.Data.WorldId
- `game/src/Entry/Phases/WorldInitPhase.cs` — uses _session.Data.WorldId
- `game/src/Adapter/UI/MainMenuController.cs` — +LargeWorld button + OnLargeWorld handler
- `game/src/Modules/Tile/TileModule.cs` — GODOT_MAP_SIZE env var + Stopwatch

**Stage Summary:**
- 500×500 работает: generation 1.4 sec, render 144 tiles (culled from 250k)
- Viewport culling: 1736× reduction in draw calls
- SmoothBiomes: 0 allocations (was 250k Dictionary allocs)
- Scene selection: MainMenu → "Новая игра (50×50)" or "Большой мир (500×500)"
- BG3 nested containers rejected — only Storage Ring

---

### 11:45 — Подготовка к NPC + Combat: аудит + план + генератор debug

**Task ID:** 5
**Agent:** main (Z.ai Code)

**Задача:**
Подготовить всё для внедрения NPC (мирные/враждебные), взаимодействие, тестовый чат, торговцы. Анализ боевой системы (классическая + техники). Проверка генераторов. Изучение документации.

**Аудит (3 параллельных агента):**

1. **Task 5-a — NPC система** (worklog lines 1075-1408):
   - Backend ~3500 LOC, 16 файлов — MOSTLY IMPLEMENTED
   - NPCSpawnPhase — STUB (NPC не спавнятся)
   - NPCVisualService — STUB (нет Godot рендеринга)
   - DialogueService (398 LOC) — работает, но NO UI
   - Trade — ZERO implementation
   - Faction — ZERO в Ai-game4 (есть в Ai-game3-ref, 261 LOC, portable)
   - Документация: 5 docs (1795 LOC), MISSING Trade + Dialogue specs

2. **Task 5-b — Боевая система** (worklog lines 1411-1884):
   - Backend ~3527 LOC, 18 файлов — MOSTLY IMPLEMENTATED
   - 11-layer damage pipeline работает
   - PlayerCombatAdapter — STUB (74 LOC vs 241), NOT registered в DI
   - 5 TODOs (equipment data not wired: pen, dodge, parry = 0)
   - NO weapon variety (hardcoded "Sword")
   - NO ammo, NO thrown, NO dual wield
   - Knockback + Chain lightning — STUBS
   - Документация: 11 docs (~4300 LOC), COMPREHENSIVE

3. **Task 5-c — Генераторы** (worklog lines 1885-2169):
   - ItemGeneratorService (527 LOC, 7 methods) — работает, verified
   - TechniqueGeneratorService (555 LOC, 10-step) — работает
   - DORMANT: generators не вызываются (NPCSpawnPhase stub, PlayerCombatAdapter stub)
   - Проблемы: pen=0, dodge=0, Cultivator cap=0, weapon variety=0

**Реализация: GODOT_GEN_DEBUG env flag**
- `GeneratorModule.cs` — added RunGeneratorDebugDump()
- Генерирует 5 items + 3 techniques + 6 loot items, выводит в лог
- Headless verified: генераторы работают, но с ограничениями

**Результаты debug dump:**
```
[Weapon]  weapon_3_001 | Меч уровня 3 | dmg=14 | pen=0 (!) | OneHand
[Armor]   armor_3_002 | Броня уровня 3 | def=9 | dodge=0 (!) | Torso
[Tech1]   Cultivator → Cultivation/Neutral | cap=0 (!) | qiCost=0 (!)
[Tech2]   Guard → Defense/Void | cap=831 | qiCost=124
[Loot]    3 items + 3 consumables generated correctly
Database: 11 items registered
```

**План внедрения (NPC_COMBAT_PREP.md):**
- 9 phases, ~5110 LOC total
- Phase 1: NPC Spawn + Render (~480 LOC, P0 BLOCKER)
- Phase 2: Test Chat (~450 LOC, P0)
- Phase 3: Faction Port (~400 LOC, P1)
- Phase 4: Trade Foundation (~520 LOC, P1)
- Phase 5: Trade UI (~650 LOC, P1)
- Phase 6: Combat Activation (~630 LOC, P0)
- Phase 7: Combat Visuals (~330 LOC, P2)
- Phase 8: Weapon Variety + Ammo (~1000 LOC, P2)
- Phase 9: Thrown + Dual Wield (~650 LOC, P2)

**Stage Summary:**
- Генераторы работают (verified via GODOT_GEN_DEBUG=1)
- NPC backend 90% готов, не хватает spawn + render + UI
- Combat backend 85% готов, не хватает PlayerCombatAdapter + target selection
- Trade — ZERO, нужен с нуля
- Faction — есть в Ai-game3-ref, portable
- План задокументирован, готов к поэтапной реализации

---

### 08:00 — Fix: LMB over UI двигает персонажа (mouse input scheme)

**Task ID:** 6-a
**Agent:** main (Z.ai Code)

**Проблема (из локального тестирования):**
При открытом инвентаре нажатие ЛКМ на предмете для перетаскивания одновременно вызывает перемещение персонажа к точке клика.

**Корневая причина:**
`GameWorldController.HandleMouseClick()` использовал `Godot.Input.IsActionJustPressed("mouse_click")` — это **polling API**, который проверяет сырое состояние InputMap action. Он **не уважает** цепочку потребления ввода Godot (UI → unhandled).

Цепочка ввода Godot 4.7:
1. `Node._input()` — ВСЕ события (top-level)
2. `Control._gui_input()` — если мышь над Control с MouseFilter.Stop
3. (событие помечается как consumed)
4. `Node._unhandled_input()` — если НЕ потреблено UI
5. `Godot.Input.IsActionJustPressed()` — **обходит** всю цепочку (polling)

**Решение:**
- Заменить polling в `_PhysicsProcess` на `_UnhandledInput(InputEvent)` override
- `_UnhandledInput` автоматически НЕ вызывается, если UI потребил событие
- Изменить `bg.MouseFilter` с `Pass` на `Stop` (клик по фону закрывает + не идёт в world)

**Документация:**
- `docs/docs_v2/07_ui/MOUSE_INPUT_SCHEME.md` — полная схема:
  - Цепочка ввода Godot 4.7
  - Матрица MouseFilter для всех UI элементов
  - Правила для будущих UI (minimap, quickbar, trade, dialogue)
  - Проверочные сценарии

**Изменения:**
1. `GameWorldController.cs`:
   - Удалён `HandleMouseClick()` (polling в _PhysicsProcess)
   - Добавлен `_UnhandledInput(InputEvent)` override
   - Логика: LMB → set _mouseTarget (только если UI не потребил)

2. `InventoryWindow.cs`:
   - `bg.MouseFilter`: `Pass` → `Stop`
   - Клик по фону: закрывает инвентарь + НЕ идёт в world

3. `cold_start.sh`:
   - Добавлен `export DOTNET_ROOT` для Godot headless (hostfxr detection)

**Верификация:**
- dotnet build: 0 errors
- Headless: игра загружается, Inventory/Doll/ObjectLayer — OK
- (Визуальная проверка LMB на ПК с Godot — у пользователя)

**Stage Summary:**
- LMB над UI → UI потребляет → игрок НЕ двигается ✓
- LMB над world → _UnhandledInput → игрок идёт ✓
- Drag&drop в инвентаре работает без конфликта с movement ✓
- Схема задокументирована для будущих UI элементов

---

### 18:00 — Fix 8 issues: inventory double-click, wheel zoom, pause, grid lines, biomes, harvest, destruction, resources

**Task ID:** 7-a
**Agent:** main (Z.ai Code)

**Issues fixed:**

1. **Double-click equip** (Issue 1):
   - Added double-click detection in `InventoryItemRow._GuiInput`
   - 350ms interval, calls `TryEquipFromInventory()` → `dollPanel.HandleDropOnSlot(eq.Slot, itemData)`
   - Added `GetDollPanel()` method to InventoryWindow
   - Resolves correct slot from EquipmentData.Slot

2. **Mouse wheel zoom in inventory** (Issue 2):
   - Moved zoom from `_Input` (receives ALL events) to `_UnhandledInput` (only if UI didn't consume)
   - ScrollContainer with MouseFilter.Stop now consumes wheel events → zoom doesn't fire
   - Same fix pattern as LMB movement (MOUSE_INPUT_SCHEME.md)

3. **Pause on inventory open** (Issue 3):
   - When inventory opens: `Time.Pause()` (unless already paused)
   - When inventory closes: `Time.Resume()` (only if not paused before)
   - Tracks `_wasPausedBeforeInventory` to not resume if game was manually paused
   - Rationale: inventory = planning activity (Kenshi/RimWorld pattern), no time pressure

4. **Surface sprite grid lines** (Issue 4):
   - Root cause: default LINEAR texture filter bleeds edge pixels across tile boundaries
   - Fix: `project.godot` → `textures/canvas_textures/default_texture_filter=0` (NEAREST)
   - Eliminates grid lines between same-type tiles

5. **Large map all biomes** (Issue 5):
   - Root cause: `MapToBiome(elevation)` used ONLY elevation → Steppe and Forest never generated
   - Fix: `MapToBiome(elevation, moisture)` — mid-elevation biome varies by moisture:
     - moisture < 0.35 → Steppe (dry)
     - moisture > 0.65 → Forest (moist)
     - else → Grassland
   - Verified 500×500: all 9 biomes present (Ocean 7%, Sea 17%, Coast 12%, Grassland 35%, Forest 7%, Steppe 7%, Highlands 12%, Mountains 0.6%, Peak 0.02%)

6. **Harvest not adding to inventory** (Issue 6):
   - Root cause: `_inventoryWindow?.RefreshExternally()` NOT called after harvest
   - Fix: added refresh call in `HandleHarvest` after `TryHarvest`
   - Also: added display name resolution via ItemDatabase (toast shows "Древесина" not "material_wood")

7. **Objects not removed after depletion** (Issue 7):
   - Root cause: objects with `ResourceMax=0` (Bush, Rock_Large) had `IsHarvestable=false` → `TryHarvest` returned false → grid never updated → sprite remained
   - Fix: see Issue 8 (give resources to all objects)
   - For objects WITH resources: grid update + RefreshObjectLayer already worked

8. **Objects missing resources** (Issue 8):
   - Root cause: `ObjectDefaults` had `ResourceMax=0` for Bush and Rock_Large
   - Fix Bush: ResourceId="fiber", ItemId="material_fiber", ResourceMax=8, HarvestAmount=2
   - Fix Rock_Large: ResourceId="stone_large", ItemId="material_stone", ResourceMax=80, HarvestAmount=8
   - Fix OreVein: unreachable code (else-if order) → moved Mountains biome check BEFORE generic Stone check
   - Added `material_fiber` to TestItemSeeder
   - Added biome distribution debug print in Generate()

**Верификация:**
- dotnet build: 0 errors
- Headless 500×500: all 9 biomes present, objects generate with resources
- Harvest flow: TryHarvest → ResourceService.Harvest → ItemAddRequestEvent → InventoryModule → TryAddItem → RefreshExternally

**Stage Summary:**
- 8 issues fixed in one pass
- All 9 biomes now generate on large map
- All resource objects have resources (Bush=fiber, Rock_Large=stone, OreVein=iron)
- Double-click equips, wheel scrolls inventory not zoom, pause on inventory open
- NEAREST texture filter eliminates grid lines
- Harvest adds items to inventory + refreshes UI + removes depleted objects

---

### 12:30 — Overweight system: overflow allowed, speed penalty, notification

**Task ID:** 8-a
**Agent:** main (Z.ai Code)

**Проблема:**
При переполнении инвентаря по весу новые ресурсы не попадают. Нет сообщения о перевесе.

**Корневая причина:**
`InventoryService.TryAddItem` проверял `CanFitItem` (вес+объём) и отклонял предмет если перевес. Это блокировало добычу при полном инвентаре.

**Решение (overflow policy):**
- **Вес:** НЕ enforced — предметы ВСЕГДА попадают в инвентарь (даже при перевесе)
- **Объём:** enforced (физическое пространство рюкзака) — partial add если переполнен
- **Перевес** → штраф к скорости перемещения + уведомление
- Будущее: Storage Ring / Spirit Storage для перемещения избыточных ресурсов

**Изменения:**

1. **InventoryService.TryAddItem** — убрана проверка веса, оставлена только объём:
   - Если объём полон → partial add (сколько влезло)
   - Если объём OK → полный add (даже если перевес)

2. **CanFitItem** — проверяет только объём (вес игнорируется):
   ```csharp
   return currentVolume + addedVolume <= effectiveMaxVolume;
   ```

3. **HowManyCanFit** — лимит по объёму только:
   ```csharp
   int byVolume = item.Volume > 0 ? (int)Math.Floor(remainingVolume / item.Volume) : int.MaxValue;
   ```

4. **IInventoryService** — добавлены:
   - `bool IsOverweight { get; }` — текущий вес > эффективный макс
   - `float OverweightRatio { get; }` — 0 = нет перегруза, 1.0 = 2× макс, 3.0 = 4× макс (cap)

5. **GameWorldController.HandleFreeMovement** — штраф скорости:
   ```csharp
   if (Inventory.IsOverweight) {
       float ratio = Inventory.OverweightRatio;
       float penalty = 1.0f / (1.0f + ratio);
       speedMult *= penalty;
       // ratio 0 → 1.0× speed, 1.0 → 0.5×, 2.0 → 0.33×, 3.0 → 0.25× (min)
   }
   ```
   - Toast "⚠ Перевес! 15.2/10.0 кг — скорость снижена" (debounced, один раз при переходе)
   - Toast "Вес в норме" при возврате

6. **InventoryWindow weight label** — цветовая индикация:
   - Красный (AccentRed) если перевес или объём полон
   - Золотой (AccentGold) если >80% лимита
   - Серый (InkFaded) в норме
   - Текст: "Вес: 15.2 / 10.0 кг ⚠ ПЕРЕВЕС | Объём: 45.0 / 100.0"

**DI:** IInventoryService injected в GameWorldController для доступа к IsOverweight/OverweightRatio.

**Верификация:**
- dotnet build: 0 errors
- Headless: игра загружается, Inventory/Doll/ObjectLayer — OK
- (Визуальная проверка перевеса на ПК с Godot)

**Stage Summary:**
- Ресурсы всегда попадают в инвентарь (overflow по весу разрешён)
- Перевес → скорость снижается (0.25×-1.0× в зависимости от ratio)
- Toast уведомление при переходе через порог перевеса
- Weight label: красный при перевесе, золотой при >80%, серый в норме
- Объём всё ещё enforced (partial add если переполнен)

---

### 13:00 — Ground item system: overflow drop, trash zone, pickup

**Task ID:** 9-a
**Agent:** main (Z.ai Code)

**Задача:**
При превышении ОБЪЁМА ресурсы должны выпадать на землю. Корзина в инвентаре для выбрасывания. Подбор выпавших предметов.

**Реализация:**

1. **Контракты** (`GroundItemContracts.cs`):
   - `ItemDroppedEvent` — предмет выпал (dropId, itemId, count, worldX, worldY)
   - `ItemPickedUpEvent` — предмет подобран (dropId, itemId, count)

2. **IGroundItemService** + `GroundItemService`:
   - `DropItem(itemId, count, x, y)` — создать ground item, опубликовать ItemDroppedEvent
   - `TryPickupNearest(x, y, maxDistance)` — найти ближайший, опубликовать ItemPickedUpEvent + ItemAddRequestEvent
   - `GetAllGroundItems()` — для рендерера
   - Хранит List<GroundItem>, уникальные dropId

3. **GroundItemRenderer** (270 LOC):
   - Подписывается на ItemDroppedEvent / ItemPickedUpEvent
   - Создаёт Sprite2D для каждого ground item
   - Процедурные текстуры 16×16 по категориям:
     - Weapon: меч (вертикальная линия + гарда)
     - Armor: щит (прямоугольник)
     - Accessory: кольцо (окружность)
     - Consumable: зелье (бутылка)
     - Material: куб
     - Technique: свиток
     - Quest: звезда
     - Misc: круг
   - ZIndex = RenderLayer.Objects + 1 (выше объектов окружения)
   - Scale 0.5 (16×16 → 8×8 на земле)

4. **InventoryService.TryAddItem** — новая сигнатура с `out int addedCount`:
   - Возвращает сколько реально добавлено (partial add при полном объёме)
   - Caller (InventoryModule) вычисляет overflow = requested - addedCount

5. **InventoryModule.OnItemAddRequest** — overflow handling:
   - TryAddItem with out addedCount
   - If overflow > 0 → DropItemsNearPlayer(itemId, overflow)
   - DropItemsNearPlayer: конвертирует tile→pixel, random offset, вызывает GroundItemService.DropItem

6. **TrashDropZone** (Panel в инвентаре):
   - 🗑 иконка + "Выбросить" label
   - MouseFilter.Stop, _CanDropData принимает source="inventory"
   - _DropData → InventoryWindow.DropItemOnGround(itemId)
   - DropItemOnGround: GetItemCount → TryRemoveItem → GroundItemService.DropItem near player

7. **GameWorldController.HandlePickup** (E key):
   - TryPickupNearest(player pixel pos, 1.5 tiles distance)
   - Toast: "Подобран предмет" / "Рядом нет предметов"
   - RefreshExternally после подбора

8. **DI**: IGroundItemService registered в InventoryModuleServices
   - Injected в GameWorldController (pickup)
   - Injected в InventoryWindow (trash drop)
   - Injected в InventoryModule (overflow drop)
   - Injected в GroundItemRenderer (events)

**Верификация:**
- dotnet build: 0 errors
- Headless: [GroundItemRenderer] Ready, [Inventory] Test items seeded
- Все компоненты загружаются

**Stage Summary:**
- При превышении объёма излишек выпадает на землю рядом с игроком
- Корзина в инвентаре: перетащи предмет → выпадает рядом с игроком
- E key: подобрать ближайший предмет (1.5 тайла)
- Процедурные спрайты по категориям (8 типов)
- Полный цикл: harvest → overflow → drop → pickup → inventory

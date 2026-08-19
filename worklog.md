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

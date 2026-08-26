# Чекпоинт: Fix 8 issues — inventory, harvest, biomes, rendering

**Дата:** 2026-08-20 18:00 UTC
**Сессия:** web-d86b1055
**Тип:** fix (8 issues)

---

## Контекст

Из локального тестирования пользователь выявил 8 проблем. Все исправлены в одном проходе.

## Issues fixed

### Issue 1: Double-click equip
**Проблема:** Двойной клик на предмете в инвентаре не экипирует его.
**Фикс:** Добавлен double-click detection в `InventoryItemRow._GuiInput` (350ms interval). При двойном ЛКМ вызывается `TryEquipFromInventory()` → `dollPanel.HandleDropOnSlot(eq.Slot, itemData)`. Добавлен `GetDollPanel()` метод в InventoryWindow.

### Issue 2: Mouse wheel zoom in inventory
**Проблема:** Колесо мыши в инвентаре одновременно прокручивает список и меняет масштаб карты.
**Фикс:** Zoom перенесён из `_Input` (получает ВСЕ события) в `_UnhandledInput` (только если UI не потребил). ScrollContainer с MouseFilter.Stop потребляет wheel events → zoom не срабатывает.

### Issue 3: Pause on inventory open
**Проблема:** При открытом инвентаре игра продолжается, создавая давление времени.
**Фикс:** При открытии инвентаря → `Time.Pause()`. При закрытии → `Time.Resume()` (только если не был на паузе до). Отслеживается `_wasPausedBeforeInventory`.
**Обоснование:** Inventory management = planning activity (Kenshi/RimWorld pattern). Игрок должен свободно изучать предметы, экипировать, drag&drop без спешки. Не влияет на real-time input (мышь, клавиатура) — только на tick-based simulation.

### Issue 4: Surface sprite grid lines
**Проблема:** Между спрайтами одного типа поверхности просвечивает сетка (особенно в лесу).
**Корневая причина:** Default LINEAR texture filter bleeding edge pixels across tile boundaries (bilinear sampling).
**Фикс:** `project.godot` → `textures/canvas_textures/default_texture_filter=0` (NEAREST). Eliminates grid lines.

### Issue 5: Large map all biomes
**Проблема:** На большой карте (500×500) не все биомы присутствуют.
**Корневая причина:** `MapToBiome(elevation)` использовал ТОЛЬКО elevation → Steppe и Forest никогда не генерировались (им нужны moisture thresholds).
**Фикс:** `MapToBiome(elevation, moisture)` — mid-elevation (0.45-0.65) biome varies by moisture:
- moisture < 0.35 → Steppe (dry grassland)
- moisture > 0.65 → Forest (moist grassland)
- else → Grassland
**Результат 500×500:** все 9 биомов: Ocean 7%, Sea 17%, Coast 12%, Grassland 35%, Forest 7%, Steppe 7%, Highlands 12%, Mountains 0.6%, Peak 0.02%.

### Issue 6: Harvest not adding to inventory
**Проблема:** Toast показывает добычу, но количество в инвентаре не меняется.
**Корневая причина:** `_inventoryWindow?.RefreshExternally()` НЕ вызывался после harvest. Backend добавлял предмет, но UI не обновлялся.
**Фикс:** Добавлен `_inventoryWindow?.RefreshExternally()` после `TryHarvest`. Также: toast теперь показывает display name ("Древесина") вместо itemId ("material_wood") через `ItemDatabase.TryGetItem`.

### Issue 7: Objects not removed after depletion
**Проблема:** Деревья и камни после добычи не пропадают, спрайт остаётся.
**Корневая причина:** Объекты с `ResourceMax=0` (Bush, Rock_Large) имели `IsHarvestable=false` → `TryHarvest` возвращал false → grid никогда не обновлялся → sprite оставался.
**Фикс:** См. Issue 8 — дать ресурсы всем объектам. Для объектов С ресурсами: grid update + RefreshObjectLayer уже работали.

### Issue 8: Objects missing resources
**Проблема:** У большинства сгенерированных объектов нет ресурсов, при добыче пишет "нет объекта".
**Корневая причина:** `ObjectDefaults` имел `ResourceMax=0` для:
- `Bush` (plain) — ResourceId="", ResourceMax=0
- `Rock_Large` — ResourceId="", ResourceMax=0
- `OreVein` — unreachable code (else-if order)

**Фиксы:**
1. `Bush`: ResourceId="fiber", ItemId="material_fiber", ResourceMax=8, HarvestAmount=2
2. `Rock_Large`: ResourceId="stone_large", ItemId="material_stone", ResourceMax=80, HarvestAmount=8
3. `OreVein`: moved Mountains biome check BEFORE generic Stone check (was unreachable)
4. Added `material_fiber` to TestItemSeeder
5. Added biome distribution debug print in Generate()

## Файлы

**Изменённые:**
- `game/src/Core/Data/ObjectDefaults.cs` — Bush + Rock_Large resources
- `game/src/Modules/Tile/TileService.cs` — MapToBiome(elev, moisture) + OreVein fix + biome debug print
- `game/src/Adapter/Scene/GameWorldController.cs` — zoom to _UnhandledInput + pause on inventory + harvest refresh + IItemDatabase inject
- `game/src/Adapter/UI/InventoryWindow.cs` — double-click equip + GetDollPanel() + bg MouseFilter
- `game/src/Adapter/UI/TestItemSeeder.cs` — +material_fiber
- `game/project.godot` — NEAREST texture filter
- `worklog.md` — запись 18:00

**Верификация:**
- `dotnet build`: 0 errors
- Headless 500×500: all 9 biomes present (Ocean, Sea, Coast, Grassland, Forest, Steppe, Highlands, Mountains, Peak)
- Harvest flow: TryHarvest → ResourceService → ItemAddRequestEvent → Inventory → RefreshExternally
- Object destruction: grid updated + RefreshObjectLayer on depletion

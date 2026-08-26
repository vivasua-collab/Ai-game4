# Чекпоинт: Окружение (деревья/кусты/камни/руда) + добыча Mode A + концепции

**Дата:** 2026-08-19 09:20 UTC
**Сессия:** web-d86b1055
**Тип:** implementation + decision

---

## Контекст

Пользователь запросил:
1. Защита слотов куклы от множественной экипировки (концепция, без кода)
2. Анализ вложенных контейнеров BG3-style в Godot
3. Реализация окружения: деревья, кусты, камни, руда + простые спрайты
4. Привязка ресурсов, количество, добыча
5. Уничтожение при исчерпании (2 режима: gradual vs threshold)
6. Анализ сложности обоих режимов

## Что сделано

### 1. Концепция (без кода)
**Файл:** `docs/docs_v2/03_world/ENVIRONMENT_CONCEPT.md`

- **Защита слотов:** уже реализована (Dictionary<EquipmentSlot, EquipmentData> + UI валидация). 1 слот = 1 предмет. Belt exception для расходников — v2 (quick-access slots)
- **Вложенные контейнеры BG3:** отложить. Сложность 🟡 Medium-hard (800-1200 LOC). Использовать существующие Storage Ring (1 уровень вложенности) + Spirit Storage (параллельный контейнер). Backend уже готов.
- **Режимы добычи:**
  - Mode A (gradual depletion): 85% готов, ~100 LOC до работающей версии
  - Mode B (harvest-damage threshold): 20% готов, ~350 LOC, нужен tool data model
  - Рекомендация: V1=Mode A для всех, V2=Mode B для деревьев/камней/руды

### 2. Backend фиксы

**Fix 1: TileService.Generate** — теперь использует `ObjectDefaults` вместо хардкода:
- Раньше: `resourceMax: 3f, resourceId: "wood", hp: 0f` (неправильно)
- Теперь: `ObjectDefaults.TryGet(objType, out var oi)` → корректные ResourceMax (50 для oak), ResourceId ("wood_oak"), HP (100)
- Добавлены: Tree_Pine, Tree_Birch, Rock_Small, Rock_Large, OreVein, Bush_Berry, Herb
- Биом-зависимая плотность: Forest=15% trees, Grassland=5%, Stone=12% rocks, Mountains=3% ore

**Fix 2: Double-publish bug** в ResourceService.Harvest:
- Раньше: ResourceHarvestedEvent публиковался и в ResourceService.Harvest И в TileService.TryHarvest
- Теперь: только TileService.TryHarvest публикует (sole publisher)
- ItemAddRequestEvent теперь использует ItemId из ObjectDefaults (не ResourceId) — фикс "предмет не найден в ItemDatabase"

**Fix 3: Tile grid update bug** в TileService.TryHarvest:
- Раньше: при `_resourceService != null` tile grid НЕ обновлялся → бесконечная добыча
- Теперь: ResourceAmount обновляется, при depleted → Object=None, IsHarvestable=false, schedule respawn

**Fix 4: IsHarvestPressed** добавлен в IPlayerInputService + PlayerInputService (раньше поле `_harvest` существовало, но не экспортировалось)

### 3. Материалы в TestItemSeeder (+6 предметов)
- `material_wood` (древесина, weight=0.5, maxStack=100)
- `material_stone` (камень, weight=1.0, maxStack=100)
- `material_iron_ore` (железная руда, weight=1.5, maxStack=50)
- `material_copper_ore` (медная руда, weight=1.3, maxStack=50)
- `consumable_berry` (ягоды, heal=5, maxStack=50)
- `consumable_herb` (лекарственная трава, maxStack=50)

IDs совпадают с `ObjectDefaults.ItemId` — критично для резолва `ItemAddRequestEvent → TryGetItem`.

### 4. ObjectLayerRenderer (300 LOC, новый файл)
`game/src/Adapter/Scene/ObjectLayerRenderer.cs`

- **Процедурные спрайты** (Image → ImageTexture, без PNG файлов)
- 9 ObjectType рендерятся:
  - Tree_Oak/Pine/Birch: ствол + крона (круг для oak/birch, треугольник для pine)
  - Rock_Small/Medium/Large: серый круг с highlight/shadow
  - Bush/Bush_Berry: кластер зелёных кругов + красные ягоды
  - OreVein: серый камень + цветные вкрапления
  - Herb: стебель + листья + цветок
  - Chest: коричневый ящик + золото
- ZIndex = RenderLayer.Objects (3) — выше terrain (2), ниже player (4)
- `Refresh()` после добычи — объект исчезает

### 5. GameWorldController.HandleHarvest
- F key → cursor tile (GetGlobalMousePosition → tile coords)
- Chebyshev distance check (≤3 тайлов от игрока)
- Bounds check
- TryHarvest → toast: "+5 material_wood (осталось: 45)"
- Refresh object layer после добычи
- Toast label (top-center, gold color, 2.5s expiry)

## Решения

- **Mode A для V1** — backend 85% готов, ~100 LOC wiring. Mode B отложен до V2 (нужен tool data model, combat system)
- **Процедурные спрайты вместо PNG** — placeholder качество, заменятся на AI-generated PNG позже (per SPRITE_PROMPTS_OBJECTS.md). Нет зависимости от asset pipeline.
- **Cursor-based target selection** (Strategy A из аудита) — игрок наводит мышь на объект, нажимает F. Max distance 3 тайла. Intuitive, поддерживает будущий ranged harvest.
- **ObjectDefaults как единый источник истины** — ResourceId, ItemId, ResourceMax, HarvestAmount, HP, HardnessTier берутся из таблицы, не хардкодятся
- **TileService как sole publisher** ResourceHarvestedEvent — устраняет double-publish bug

## Найденные проблемы

- **Biome sprites missing** (`biome_ocean.png`, `biome_sea.png`, etc.) — существующая проблема, не связана с текущей задачей. Godot не может загрузить .ctex файлы (нужен Editor для импорта)
- **Test polygon без Stone terrain** — 51% Grass, 26% Water, 21% Sand, 0% Stone. Камни/руда не спавнятся в этом сиде. Для теста камней нужен другой сид или карта с горами
- **ResourceService.TrySpawnResource / TryPickup** — stubs (не критично для V1)
- **ResourceRespawnedEvent** — публикуется, но TileService не подписан (объект не восстанавливается после 7 дней). Нужна подписка для V2

## Следующие шаги

- [ ] Визуальная проверка на ПК с Godot (у пользователя нет доступа сегодня)
- [ ] Подписать TileService на ResourceRespawnedEvent (восстановление объектов через 7 дней)
- [ ] Mode B (V2): реализовать DamageObject + tool data model + ObjectDestroyedEvent
- [ ] Заменить процедурные спрайты на PNG (по SPRITE_PROMPTS_OBJECTS.md)
- [ ] Добавить визуальный фидбек: HP bar над объектом, стадии повреждения
- [ ] Belt quick-access slots для расходников

## Файлы

**Созданные:**
- `docs/docs_v2/03_world/ENVIRONMENT_CONCEPT.md` — концепция (slot protection + nested containers + harvest modes)
- `game/src/Adapter/Scene/ObjectLayerRenderer.cs` (300 LOC) — процедурные спрайты окружения
- `checkpoints/08_19_environment.md` — этот чекпоинт

**Изменённые:**
- `game/src/Modules/Tile/TileService.cs` — Generate (ObjectDefaults), TryHarvest (tile grid update + sole publish)
- `game/src/Modules/Tile/ResourceService.cs` — Harvest (ItemId из ObjectDefaults, no double-publish)
- `game/src/Core/Interfaces/IPlayerInputService.cs` — +IsHarvestPressed
- `game/src/Modules/Player/PlayerInputService.cs` — +IsHarvestPressed impl
- `game/src/Adapter/UI/TestItemSeeder.cs` — +6 материалов/расходников
- `game/src/Adapter/Scene/SceneBuilder.cs` — +SetupObjectLayer + RefreshObjectLayer
- `game/src/Adapter/Scene/GameWorldController.cs` — +HandleHarvest + ShowToast + toast label
- `worklog.md` — запись 09:20

**Верификация:**
- `dotnet build`: 0 errors, 0 warnings
- Headless: `[ObjectLayer] Drew 71 object sprites` — 71 объект генерируется
- 6 материалов регистрируются в ItemDatabase
- F key wired (cursor tile → distance check → TryHarvest → toast)

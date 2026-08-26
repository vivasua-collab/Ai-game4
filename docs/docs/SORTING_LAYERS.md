# 🎨 Sorting Layers — Порядок рендеринга 2D

**Версия:** 3.0  
**Дата:** 2026-04-20  
**Проект:** Cultivation World Simulator (Unity 6.3 URP 2D)

---

## 📐 Принцип рендеринга

Unity URP 2D рендерит спрайты **снизу вверх** по Sorting Layers.  
Слой с **меньшим индексом** рендерится **позади**.  
Внутри одного слоя порядок определяется `sortingOrder` (меньше = позади).

```
Камера (Z = -10, смотрит в +Z)
  │
  ▼ Порядок рендеринга (снизу → вверх, позади → впереди):

  ┌─────────────────────────────────────────────────────┐
  │  [0] Default     — ❌ ЗАПРЕЩЁН! Fallback            │
  │  [1] Background  — Фоновые декорации                │
  │  [2] Terrain     — Поверхность земли/воды/лавы      │
  │  [3] Objects     — Деревья, камни, ресурсы, дроп    │
  │  [4] Player      — Игрок и тень                     │
  │  [5] UI          — UI элементы в мировом простр.     │
  └─────────────────────────────────────────────────────┘
   
  uniqueID = индекс слоя (0,1,2,3,4,5) — ДЕТЕРМИНИРОВАННО!
```

> **⚠️ ВАЖНО:** uniqueID каждого слоя назначается **детерминированно** (= индексу).  
> Это гарантирует одинаковый порядок слоёв на любом ПК.  
> См. раздел «Баг: Разный порядок слоёв на разных ПК».

---

## 📋 Полная таблица: Слой → Объекты → sortingOrder

### Слой [0] Default — ❌ ЗАПРЕЩЁН для использования

| Объект | sortingOrder | Примечание |
|--------|-------------|------------|
| — | — | Если SpriteRenderer/TilemapRenderer не назначил sortingLayerName, Unity ставит "Default" → рендер поверх всех остальных! Это **баг** |

> **⚠️ КРИТИЧЕСКИ:** Любой рендерер на "Default" слое рендерится **ПОСЛЕ** всех остальных слоёв (поверх Player, UI). Если в логах `[RPL]-L9` видны объекты на Default — это баг, который нужно немедленно исправить.

### Слой [1] Background — Фоновые декорации

| Объект | Компонент | sortingOrder | Источник |
|--------|-----------|-------------|----------|
| (пока не используется) | — | — | — |

> Зарезервирован для фоновых параллакс-слоёв, неба, далёких гор.

### Слой [2] Terrain — Поверхность

| Объект | Компонент | sortingOrder | Спрайт | Источник |
|--------|-----------|-------------|--------|----------|
| TerrainTilemap | TilemapRenderer | 0 | Процедурный (Sprite.Create) | TileMapController.cs |
| Grass | GameTile | — | 64×64 PPU=32 Point | ForceProceduralTerrainTile |
| Dirt | GameTile | — | 64×64 PPU=32 Point | ForceProceduralTerrainTile |
| Stone | GameTile | — | 64×64 PPU=32 Point | ForceProceduralTerrainTile |
| Water_Shallow | GameTile | — | 64×64 PPU=32 Point (α=0.8) | ForceProceduralTerrainTile |
| Water_Deep | GameTile | — | 64×64 PPU=32 Point (α=0.9) | ForceProceduralTerrainTile |
| Sand | GameTile | — | 64×64 PPU=32 Point | ForceProceduralTerrainTile |
| Void | GameTile | — | 64×64 PPU=32 Point | ForceProceduralTerrainTile |
| Snow | GameTile | — | 64×64 PPU=32 Point | ForceProceduralTerrainTile |
| Ice | GameTile | — | 64×64 PPU=32 Point | ForceProceduralTerrainTile |
| Lava | GameTile | — | 64×64 PPU=32 Point | ForceProceduralTerrainTile |

> **ВСЕ terrain-спрайты — процедурные** (Sprite.Create в рантайме). PNG через Import Pipeline вызывают белую сетку между тайлами. См. TileMapController.EnsureTileAssets().

### Слой [3] Objects — Объекты окружения, ресурсы, дроп

| Объект | Компонент | sortingOrder | Спрайт | Источник |
|--------|-----------|-------------|--------|----------|
| ObjectTilemap | TilemapRenderer | 0 | PNG/процедурный | TileMapController.cs |
| OverlayTilemap | TilemapRenderer | 10 | — | TileMapController.cs |
| Herb (harvestable) | SpriteRenderer | 1 | PNG | HarvestableSpawner.cs |
| Bush (harvestable) | SpriteRenderer | 2 | PNG | HarvestableSpawner.cs |
| Bush_Berry (harvestable) | SpriteRenderer | 2 | PNG | HarvestableSpawner.cs |
| Rock_Small (harvestable) | SpriteRenderer | 3 | PNG | HarvestableSpawner.cs |
| Rock_Medium (harvestable) | SpriteRenderer | 3 | PNG | HarvestableSpawner.cs |
| OreVein (harvestable) | SpriteRenderer | 4 | PNG | HarvestableSpawner.cs |
| Tree_Oak (harvestable) | SpriteRenderer | 5 | PNG | HarvestableSpawner.cs |
| Tree_Pine (harvestable) | SpriteRenderer | 5 | PNG | HarvestableSpawner.cs |
| Tree_Birch (harvestable) | SpriteRenderer | 5 | PNG | HarvestableSpawner.cs |
| Resource pickups | SpriteRenderer | 5 | PNG | ResourceSpawner.cs |
| Drop items | SpriteRenderer | 5 | PNG | DestructibleObjectController.cs |

> **⚠️ ИЗВЕСТНАЯ ПРОБЛЕМА:** HarvestableSpawner назначает sortingOrder по **типу объекта** (Herb=1, Tree=5), а не по Y-позиции. Это означает, что ВСЕ деревья рендерятся поверх ВСЕХ камней, независимо от их позиции на экране. Правильное решение — Y-сортировка: `order = -Y * 10`. Это задача на следующий этап.

### Слой [4] Player — Игрок

| Объект | Компонент | sortingOrder | Спрайт | Источник |
|--------|-----------|-------------|--------|----------|
| Player Visual | SpriteRenderer | 0 | AI/PNG (128×128 PPU=64) | PlayerVisual.cs |
| Player Shadow | SpriteRenderer | -1 | Программный круг (64×64 PPU=64) | PlayerVisual.cs |

> **Fallback:** Если слой "Player" не существует или его индекс меньше Terrain/Objects, PlayerVisual переводит спрайт на слой "Objects" с sortingOrder=100. Это гарантирует, что игрок виден поверх terrain и objects.

### Слой [5] UI — UI элементы в мировом пространстве

| Объект | Компонент | sortingOrder | Источник | Примечание |
|--------|-----------|-------------|----------|------------|
| Formation UI preview | SpriteRenderer | 1000 | FormationUI.cs | ⚠️ Не назначает sortingLayerName! |
| Formation UI prefabs | SpriteRenderer | 1000 | FormationUIPrefabsGenerator.cs | ⚠️ Не назначает sortingLayerName! |
| Harvest Feedback | Canvas (WorldSpace) | 100 | HarvestFeedbackUI.cs | Canvas.sortingOrder, не SpriteRenderer |

> **⚠️ ПРОБЛЕМА:** FormationUI и FormationUIPrefabsGenerator НЕ назначают `sortingLayerName = "UI"`. SpriteRenderer без sortingLayerName попадает на Default → рендерится ПОСЛЕ всех слоёв. Это работает только потому, что Default рендерится поверх UI, но это совпадение, а не правильное поведение. **Нужно исправить: добавить `sr.sortingLayerName = "UI"` в оба файла.**

---

## 🔧 Где настраиваются Sorting Layers

### Источник истины: КОД (не TagManager.asset!)

| Файл | Назначение |
|------|-----------|
| `SceneBuilderConstants.cs` | **Единственный источник истины.** `REQUIRED_SORTING_LAYERS` определяет имена и порядок слоёв |
| `SceneBuilderUtils.cs` | `EnsureSortingLayers()` — создаёт слои + назначает детерминированные uniqueID |
| `TileMapController.cs` | `EnsureSortingLayersExist()` + `EnsureSortingLayerOrder()` — рантайм-проверка и исправление |

> **⚠️ ВАЖНО:** `ProjectSettings/` **удалён из проекта** — вызывает падение Unity Editor. Unity пересоздаёт TagManager.asset автоматически при открытии проекта. Слои создаются и проверяются **кодом** при каждом запуске, не вручную.
>
> Код гарантированно создаёт правильные слои с детерминированными uniqueID = индекс (0,1,2,3,4,5) на любом ПК.

### Создание и проверка слоёв (Editor-only)

| Место | Файл | Метод | Что делает |
|-------|------|-------|-----------|
| Phase02TagsLayers | Phase02TagsLayers.cs | `IsNeeded()` | Проверяет: существование, порядок И uniqueID слоёв |
| Phase02TagsLayers | Phase02TagsLayers.cs | `Execute()` → `SceneBuilderUtils.EnsureSortingLayers()` | Создаёт недостающие + переназначает uniqueID |
| SceneBuilderUtils | SceneBuilderUtils.cs | `EnsureSortingLayers()` | Создаёт слои + переставляет в правильный порядок + uniqueID = индекс |
| TileMapController | TileMapController.cs | `EnsureSortingLayersExist()` | Создаёт недостающие (рантайм) |
| TileMapController | TileMapController.cs | `EnsureSortingLayerOrder()` | Проверяет и исправляет порядок + uniqueID |

### Константы слоёв

| Файл | Переменная | Значение |
|------|-----------|----------|
| SceneBuilderConstants.cs | `REQUIRED_SORTING_LAYERS` | `["Default", "Background", "Terrain", "Objects", "Player", "UI"]` |

### Назначение слоёв рендерерам

| Компонент | Файл | Метод | Слой | sortingOrder |
|-----------|------|-------|------|-------------|
| Terrain TilemapRenderer | TileMapController.cs | `FixTilemapSortingLayers()` | "Terrain" | 0 |
| Objects TilemapRenderer | TileMapController.cs | `FixTilemapSortingLayers()` | "Objects" | 0 |
| Overlay TilemapRenderer | TileMapController.cs | `FixTilemapSortingLayers()` | "Objects" | 10 |
| **ЛЮБОЙ** TilemapRenderer | TileMapController.cs | `FixAllTilemapRenderers()` | по имени GO | — |
| Player SpriteRenderer | PlayerVisual.cs | `CreateVisual()` + `EnsureCorrectSortingLayer()` | "Player" | 0 |
| Player Shadow SpriteRenderer | PlayerVisual.cs | `CreateVisual()` | "Player" | -1 |
| Harvestable SpriteRenderer | HarvestableSpawner.cs | `CreateHarvestableSprite()` | "Objects" | по типу (1-5) |
| Resource SpriteRenderer | ResourceSpawner.cs | `CreateResourceSprite()` | "Objects" | 5 |
| Drop SpriteRenderer | DestructibleObjectController.cs | `CreateDropSprite()` | "Objects" | 5 |
| Formation UI SpriteRenderer | FormationUI.cs | `CreateFormationPreview()` | ⚠️ Default (нет назначения) | 1000 |
| Formation prefab SpriteRenderer | FormationUIPrefabsGenerator.cs | генерация | ⚠️ Default (нет назначения) | 1000 |
| Harvest Feedback | HarvestFeedbackUI.cs | `EnsureCanvasExists()` | Canvas (Default) | 100 |

---

## 🐛 Диагностика: RenderPipelineLogger

| Уровень | Метод | Что проверяет |
|---------|-------|---------------|
| L1 | `LogSortingLayers()` | Существование слоёв |
| L2 | `LogTilemapState()` | TilemapRenderer: слой, order, кол-во тайлов |
| L3 | `LogSpriteImportState()` | PPU, alphaIsTransparency |
| L4 | `LogTileRendered()` | Кол-во terrain/object тайлов |
| L5 | `LogPlayerVisualState()` | Player SpriteRenderer: слой, шейдер |
| L6 | `LogHarvestableState()` | Harvestable SpriteRenderer: слой, order |
| L7 | `LogCameraState()` | Камера: ortho, position, size |
| L8 | `LogLightState()` | GlobalLight2D: тип, интенсивность |
| **L9** | **`LogAllRendererState()`** | **Все рендереры: порядок слоёв, Default-баг, Player видимость** |

---

## 🚨 Типичные баги и решения

### Баг: Разный порядок слоёв на разных ПК 🔴

**Симптом:** На одном ПК Player рендерится поверх Terrain, на другом — позади. Разный порядок Sorting Layers.  
**Причина (историческая):**
1. `ProjectSettings/TagManager.asset` **отсутствовал в Git** — каждый ПК получал дефолтные слои Unity
2. `uniqueID` генерировались **случайно** (`Guid.NewGuid().GetHashCode()`) — на разных ПК разные ID → разный порядок

**Решение (текущее):**
1. `ProjectSettings/` удалён из проекта — вызывает падение Unity Editor
2. Unity пересоздаёт TagManager.asset автоматически при открытии проекта
3. Код (`EnsureSortingLayers()` / `EnsureSortingLayerOrder()`) создаёт слои с `uniqueID = индекс` (0,1,2,3,4,5) — **детерминированно**
4. `Phase02TagsLayers.IsNeeded()` проверяет, что uniqueID совпадают с индексами
5. `TileMapController.EnsureSortingLayerOrder()` переназначает uniqueID детерминированно в рантайме

**Источник истины:** `SceneBuilderConstants.REQUIRED_SORTING_LAYERS` (код, не файл)
**Файлы исправления:** SceneBuilderUtils.cs, Phase02TagsLayers.cs, TileMapController.cs

### Баг: Terrain рендерится поверх Player

**Причина:** Sorting Layer "Player" имеет индекс меньше, чем "Terrain"  
**Решение:** `TileMapController.EnsureSortingLayerOrder()` переставляет слои  
**Fallback:** `PlayerVisual.EnsureCorrectSortingLayer()` переводит Player на "Objects" с order=100

### Баг: Объекты на "Default" слое

**Причина:** SpriteRenderer/TilemapRenderer не назначил sortingLayerName  
**Решение:** `TileMapController.FixAllTilemapRenderers()` ищет ВСЕ TilemapRenderer по типу  
**Диагностика:** `RenderPipelineLogger.LogAllRendererState()` показывает объекты на Default

### Баг: Спрайт поверхности (terrain) поверх спрайтов объектов

**Причина:** TilemapRenderer terrain на "Default" (id=0), а не на "Terrain" (id=2)  
**Решение:** `FixTilemapSortingLayers()` + `FixAllTilemapRenderers()` принудительно назначают "Terrain"

### Баг: Белая сетка между terrain-тайлами

**Причина:** PNG-спрайты через Unity Import Pipeline имеют субпиксельные зазоры  
**Решение:** Все terrain-спрайты — процедурные (Sprite.Create), PPU=32, FilterMode.Point

### Баг: Formation UI рендерится на Default слое

**Причина:** `FormationUI.cs` и `FormationUIPrefabsGenerator.cs` не назначают `sortingLayerName`  
**Решение:** Добавить `sr.sortingLayerName = "UI"` в оба файла (пока не исправлено, работает за счёт Default поверх UI)

---

## 📐 Формат спрайтов

| Тип | Размер | PPU | Filter | Alpha | Источник |
|------|--------|-----|--------|-------|----------|
| Terrain (процедурный) | 64×64 | 32 | Point | Сплошная заливка | Sprite.Create |
| Objects (PNG) | 64×64 | 32 | Point | Прозрачный фон | AssetDatabase |
| Player (PNG) | 128×128 | 64 | Bilinear | RGBA с прозрачностью | AssetDatabase |
| Player Fallback | 64×64 | 64 | Bilinear | RGBA | Sprite.Create |

---

## 🔢 Детерминированные uniqueID

С версии 2.0 все Sorting Layers имеют **детерминированные uniqueID**, равные индексу слоя:

| Индекс | Имя слоя | uniqueID | Примечание |
|--------|----------|----------|-----------|
| 0 | Default | 0 | Всегда существует, не удаляется |
| 1 | Background | 1 | |
| 2 | Terrain | 2 | |
| 3 | Objects | 3 | Критический — PlayerVisual, HarvestableSpawner |
| 4 | Player | 4 | PlayerVisual |
| 5 | UI | 5 | FormationUI, HarvestFeedbackUI |

**Почему это важно:** Unity использует `uniqueID` для определения порядка рендеринга. Если на разных ПК слои имеют разные `uniqueID`, порядок рендеринга будет отличаться даже при одинаковых именах слоёв. Детерминированное назначение `uniqueID = индекс` гарантирует воспроизводимость.

**Где проверяется:**
- `Phase02TagsLayers.IsNeeded()` — проверяет `sortingLayers[i].id == i`
- `SceneBuilderUtils.EnsureSortingLayers()` — переназначает при расхождении
- `TileMapController.EnsureSortingLayerOrder()` — переназначает при расхождении

---

## 📎 Связанные документы

- [ARCHITECTURE.md](./ARCHITECTURE.md) — Общая архитектура
- [TILE_SYSTEM.md](./TILE_SYSTEM.md) — Тайловая система
- [UNITY_DOCS_LINKS.md](./UNITY_DOCS_LINKS.md) — Документация Unity 6.3

---

*Создано: 2026-04-17 12:50 UTC*  
*Редактировано: 2026-04-20 09:19 UTC — v3.0: ProjectSettings/ удалён (краш Unity), источник истины → код (SceneBuilderConstants), TagManager.asset создаётся Unity автоматически, актуализация по коду*  
*Проект: Cultivation World Simulator*

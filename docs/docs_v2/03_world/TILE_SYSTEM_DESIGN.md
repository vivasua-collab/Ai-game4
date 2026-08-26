# Дизайн: система биомов, слоёв и тайлов

> **Статус:** Концепция (не реализована)
> **Дата:** 2026-08-15
> **Связанные документы:** `docs_v2/03_world/TILE_SYSTEM.md`, `docs_v2/07_ui/RENDER_LAYERS.md`

---

## 1. Проблема

Текущая система тайлов:
- **Генерация on-the-fly** — ValueNoise генерирует terrain в рантайме
- **Transition tiles** — quarter-circle overlays через `_Draw()` (не работают корректно)
- **Один слой** — все тайлы в одном MultiMesh, нет разделения
- **Нет масок** — нет предопределённых тайлов с переходами

**Результат:** резкие квадратные переходы, нет плавности, нет структуры слоёв.

---

## 2. Многообразие биомов и поверхностей

### 2.1. Биомы (определяются по elevation + moisture)

| Биом | Elevation | Moisture | Surface | Цвет | Описание |
|------|-----------|----------|---------|------|----------|
| **Ocean** | 0.00-0.30 | any | Water_Deep | Тёмно-синий | Глубокий океан |
| **Sea** | 0.30-0.40 | any | Water_Shallow | Голубой | Мелководье |
| **Beach** | 0.40-0.45 | any | Sand | Жёлтый | Пляжи |
| **Grassland** | 0.45-0.65 | >0.35 | Grass | Зелёный | Луга |
| **Steppe** | 0.45-0.65 | <0.35 | Dirt | Коричневый | Степь |
| **Forest** | 0.45-0.65 | >0.60 | Grass+Trees | Тёмно-зелёный | Лес |
| **Highlands** | 0.65-0.82 | any | Stone | Серый | Нагорье |
| **Mountains** | 0.82-0.92 | any | Snow | Белый | Снежные пики |
| **Peak** | 0.92-1.00 | any | Ice | Голубовато-белый | Ледники |

### 2.2. Поверхности (terrain types для тайлов)

| ID | Surface | Категория | Проходим? | MoveCost | Цвет |
|----|---------|-----------|-----------|----------|------|
| 0 | Void | empty | ❌ | ∞ | Чёрный |
| 1 | Water_Deep | water | ❌ (без навыка) | ∞ | #1a3a6b |
| 2 | Water_Shallow | water | ⚠ (wading) | 2.0 | #4a7ab8 |
| 3 | Sand | ground | ✅ | 1.2 | #d9c489 |
| 4 | Dirt | ground | ✅ | 1.0 | #735a33 |
| 5 | Grass | ground | ✅ | 1.0 | #477a38 |
| 6 | Road | ground | ✅ | 0.7 | #8a7050 |
| 7 | Stone | ground | ✅ | 1.5 | #808080 |
| 8 | Snow | ground | ✅ | 1.8 | #ebeff5 |
| 9 | Ice | ground | ⚠ (sliding) | 1.5 | #b3d9f2 |
| 10 | Lava | hazard | ❌ | ∞ | #d9401a |
| 11 | Bush | vegetation | ✅ (slow) | 1.5 | #3d6629 |
| 12 | TallGrass | vegetation | ✅ (slow) | 1.2 | #5a8a45 |

### 2.3. Объекты на поверхности (layer 1+)

| Категория | Примеры | Слой |
|-----------|---------|------|
| **Vegetation** | Tree_Oak, Tree_Pine, Bush, Bush_Berry | 1 |
| **Rocks** | Rock_Small, Rock_Medium, Rock_Large | 1 |
| **Structures** | Building, Wall, Fence | 2 |
| **Items** | Drop, Loot, Resource | 3 |
| **Effects** | Formation, Fire, Ice | 4 |

---

## 3. Система слоёв

### 3.1. Рендер-слои (ZIndex)

| ZIndex | Имя | Назначение | Godot Node |
|--------|-----|------------|------------|
| 0 | Background | Параллакс, небо | CanvasLayer |
| 1 | Terrain | Поверхность земли | TileMapLayer (ground) |
| 2 | TerrainTransitions | Сглаживание переходов | TileMapLayer (transitions) |
| 3 | Objects | Деревья, камни, кусты | TileMapLayer (objects) + YSort |
| 4 | Player | Игрок + NPC | Sprite2D + YSort |
| 5 | Effects | Формации, эффекты | Node2D |
| 10 | HUD | Интерфейс | CanvasLayer |

### 3.2. TileMapLayer структура

```
GameWorld (Node2D)
├── TileMapLayer "Ground"          (ZIndex=1) — поверхность
├── TileMapLayer "Transitions"     (ZIndex=2) — сглаживание
├── TileMapLayer "Objects"         (ZIndex=3, YSort=true) — деревья/камни
├── Node2D "Entities"              (ZIndex=4, YSort=true) — игрок/NPC
├── Node2D "Effects"               (ZIndex=5) — формации
└── CanvasLayer "HUD"              (ZIndex=10) — UI
```

### 3.3. Логические слои (в TileData)

Каждый тайл имеет 4 логических слоя (как в Ai-game3):
1. **Base** — elevation, Qi density, temperature
2. **Surface** — terrain type (grass, water, stone)
3. **Objects** — деревья, камни, ресурсы
4. **Subjects** — сущности (игрок, NPC)

---

## 4. Маски тайлов (TileSet)

### 4.1. Принцип

Вместо генерации on-the-fly, используем **предопределённый TileSet** с масками переходов:
- Каждый terrain тип имеет набор тайлов для каждой комбинации соседей
- 47 уникальных тайлов на terrain тип (как RPG Maker autotiles)
- Переходы "запечены" в текстуры — не нужно рисовать в рантайме

### 4.2. TileSet terrain (Godot 4.7 native)

Godot 4.7 имеет встроенную систему **TileSet Terrains**:
- **Connect mode** — для переходов между terrain типами
- **Path mode** — для дорог/рек
- Автозаполнение через `TileMapLayer.set_cell()`

### 4.3. Структура TileSet

```
TileSet "CultivationTerrain"
├── TerrainSet "Ground"
│   ├── Terrain "Water_Deep" (color: #1a3a6b)
│   ├── Terrain "Water_Shallow" (color: #4a7ab8)
│   ├── Terrain "Sand" (color: #d9c489)
│   ├── Terrain "Grass" (color: #477a38)
│   ├── Terrain "Dirt" (color: #735a33)
│   ├── Terrain "Stone" (color: #808080)
│   ├── Terrain "Snow" (color: #ebeff5)
│   └── Terrain "Ice" (color: #b3d9f2)
├── TerrainSet "Objects"
│   ├── Terrain "Tree_Oak"
│   ├── Terrain "Rock_Medium"
│   └── ...
├── Physics layers (collision)
├── Navigation layers
└── Custom data layers (moveCost, harvestable, etc.)
```

### 4.4. Процесс создания тайлов

**Сейчас (placeholder):**
- Процедурные цвета (TerrainColors.cs)
- MultiMesh для base terrain
- _Draw() для transitions (не работает)

**Целевое (с качественными тайлами):**
1. Создать TileSet ресурс в Godot Editor
2. Импортировать текстуры тайлов (PNG, 64×64px @ PPU=32)
3. Настроить TerrainSet для каждого биома
4. Использовать `TileMapLayer.set_cell()` вместо MultiMesh
5. Transition tiles — через Godot's native autotiling

**Промежуточное (без текстур, с масками):**
- Создать TileSet с цветными ячейками
- Настроить Terrain peering masks
- Использовать set_cell() вместо MultiMesh
- Transition tiles — через Godot's terrain system

---

## 5. План реализации

### Фаза 1: Очистка (сейчас)
- Убрать MultiMesh + TransitionTileRenderer (временный код)
- Убрать ValueNoise из Adapter (оставить в Core для генерации)
- Очистить SceneBuilder

### Фаза 2: TileSet + TileMapLayer (следующий шаг)
- Создать TileSet ресурс с TerrainSet
- Настроить peering masks для переходов
- Заменить MultiMesh на TileMapLayer.set_cell()
- Transition tiles — через Godot's native terrain autotiling

### Фаза 3: Качественные тайлы (когда будут текстуры)
- Создать/найти текстуры тайлов (PNG)
- Импортировать в TileSet
- Настроить collision, navigation, custom data
- Добавить объекты (деревья, камни) как TileSet scenes

### Фаза 4: Слои объектов
- TileMapLayer "Objects" с YSort
- Деревья/камни как TileSet cells или Sprite2D nodes
- Player/NPC на отдельном слое с YSort

---

## 6. Преимущества подхода

| Аспект | Сейчас (MultiMesh) | Целевое (TileSet) |
|--------|-------------------|-------------------|
| Transition tiles | _Draw() (не работает) | Godot native autotiling |
| Слои | Один MultiMesh | Раздельные TileMapLayer |
| Collision | Нет | TileSet physics layers |
| Navigation | Нет | TileSet navigation layers |
| Custom data | Нет | TileSet custom data (moveCost и т.д.) |
| Текстуры | Цвета | PNG тайлы |
| Performance | 1 draw call | Godot optimizes internally |
| Editor support | Нет (код только) | Визуальный редактор TileSet |

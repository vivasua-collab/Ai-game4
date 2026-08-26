# 🗺️ Тайловая система - Реализация

**Создано:** 2026-04-07 14:24:05 UTC
**Версия:** 1.0
**Статус:** Базовая реализация

---

## ⚠️ Быстрый старт

### 1. Генерация спрайтов
В Unity Editor: `Tools > Generate Tile Sprites`

### 2. Настройка сцены
В Unity Editor: `Tools > Setup Test Location Scene`

### 3. Назначение тайлов
1. Откройте сцену `Assets/Scenes/TestLocation.unity`
2. Выберите объект `TileMapController`
3. В Inspector назначьте спрайты из `Assets/Sprites/Tiles/`:
   - Terrain Tiles: terrain_grass, terrain_dirt, terrain_stone, terrain_water_*
   - Object Tiles: obj_tree, obj_rock_*, obj_bush, obj_chest

### 4. Запуск
Нажмите Play - карта сгенерируется автоматически.

---

## 📁 Структура файлов

```
Assets/Scripts/Tile/
├── TileEnums.cs           # Перечисления (TerrainType, TileObjectType, TileFlags)
├── TileData.cs            # Данные тайла и объектов
├── TileMapData.cs         # Данные карты
├── TileMapController.cs   # Контроллер карты (MonoBehaviour)
├── GameTile.cs            # Пользовательские TileBase
├── CultivationGame.TileSystem.asmdef
└── Editor/
    ├── TileSpriteGenerator.cs    # Генератор спрайтов
    └── TestLocationSetup.cs      # Настройка сцены
```

---

## 📐 Размерности

| Параметр | Значение |
|----------|----------|
| Размер тайла | **2×2 м** |
| Размер тестовой карты | 30×20 тайлов = 60×40 м |
| Tilemap cellSize | (2, 2, 1) |

---

## 🏗️ Архитектура

### Слои тайла

```
┌─────────────────────────────────────────┐
│   СЛОЙ 4: СУБЪЕКТЫ (динамический)       │
│   ├── Игрок                             │
│   └── NPC                               │
├─────────────────────────────────────────┤
│   СЛОЙ 3: ОБЪЕКТЫ (TileObjectData)      │
│   ├── Деревья (tree)                    │
│   ├── Камни (rock)                      │
│   └── Интерактивные (chest, shrine)     │
├─────────────────────────────────────────┤
│   СЛОЙ 2: ПОВЕРХНОСТЬ (TerrainType)     │
│   ├── Трава (grass)                     │
│   ├── Вода (water_shallow/deep)         │
│   └── Камень (stone)                    │
├─────────────────────────────────────────┤
│   СЛОЙ 1: БАЗОВЫЕ ПАРАМЕТРЫ             │
│   ├── Ци (qiDensity)                    │
│   └── Температура (temperature)         │
└─────────────────────────────────────────┘
```

### Основные классы

| Класс | Описание |
|-------|----------|
| `TileData` | Данные одного тайла |
| `TileObjectData` | Данные объекта на тайле |
| `TileMapData` | Данные всей карты |
| `TileMapController` | Управление отображением и взаимодействием |
| `GameTile` | TileBase для Unity Tilemap |

---

## 🎮 API

### TileMapController

```csharp
// Генерация карты
controller.GenerateMap(width, height, "Location Name");

// Доступ к тайлам
TileData tile = controller.GetTile(x, y);
TileData tile = controller.GetTileAtWorld(worldPos);

// Изменение тайлов
controller.SetTerrain(x, y, TerrainType.Water_Deep);
controller.AddObject(x, y, TileObjectType.Tree_Oak);

// События
controller.OnTileChanged += (tile) => { };
controller.OnMapGenerated += (mapData) => { };
```

### TileData

```csharp
// Проверки
bool passable = tile.IsPassable();
bool passable = tile.IsPassable(canSwim: true);
bool blocks = tile.BlocksVision();

// Преобразование координат
Vector2 worldPos = tile.GetWorldPosition();
Vector2Int tilePos = TileData.WorldToTile(worldPos);
```

---

## 📋 Типы поверхностей

| TerrainType | Move Cost | Проходимость |
|-------------|-----------|--------------|
| Grass | 1.0 | ✅ |
| Dirt | 1.0 | ✅ |
| Stone | 1.0 | ✅ |
| Sand | 1.2 | ✅ |
| Water_Shallow | 2.0 | ✅ (медленно) |
| Water_Deep | 0.0 | 🏊 (только плавание) |
| Snow | 1.5 | ✅ |
| Ice | 1.5 | ✅ (скольжение) |
| Lava | 0.0 | ❌ (урон) |
| Void | 0.0 | ❌ |

---

## 📋 Типы объектов

### Растительность
- `Tree_Oak`, `Tree_Pine`, `Tree_Birch` - деревья (непроходимые, укрытие)
- `Bush`, `Bush_Berry` - кусты (можно собирать ягоды)
- `Grass_Tall`, `Flower`, `Herb` - трава (проходимые)

### Камни
- `Rock_Small` - маленький камень
- `Rock_Medium` - средний камень
- `Rock_Large`, `Boulder` - большие камни (2×2 тайла)

### Здания
- `Wall_Wood`, `Wall_Stone` - стены
- `Door`, `Window` - двери и окна

### Интерактивные
- `Chest` - сундук
- `Shrine`, `Altar` - святилища
- `OreVein` - рудная жила
- `Herb` - целебная трава

---

## 🔗 Связанные документы

- **docs/TILE_SYSTEM.md** - Теоретическое описание
- **docs/WORLD_MAP_SYSTEM.md** - Размерности мира
- **docs/LOCATION_MAP_SYSTEM.md** - Генерация локаций

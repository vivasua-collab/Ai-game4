# Просмотр спрайтов и слоёв в Godot Editor

> **Путь:** `docs/docs_v2/07_ui/GODOT_SPRITE_VIEWER.md`
> **Назначение:** Инструкция для самостоятельной проверки слоёв и спрайтов в Godot Editor

---

## 1. Открытие FileSystem

В Godot Editor, слева внизу — панель **FileSystem**.

Путь к спрайтам:
```
res://resources/tiles/64/         ← рабочие тайлы (64×64)
res://resources/tiles/originals/  ← оригиналы (928×928)
```

**Проверка:** раскройте `res://resources/tiles/64/` — должны быть 9 файлов:
- `biome_ocean.png`
- `biome_sea.png`
- `biome_coast.png`
- `biome_grassland.png`
- `biome_steppe.png`
- `biome_forest.png`
- `biome_highlands.png`
- `biome_mountains.png`
- `biome_peak.png`

Если файлов нет — выполните `git pull` и переоткройте проект.

---

## 2. Предпросмотр спрайта

**Двойной клик** по PNG файлу в FileSystem → откроется превью в центре.

Для проверки tileable:
1. Откройте спрайт двойным кликом
2. Визуально проверьте, совпадают ли края (левый=правый, верх=низ)

---

## 3. Проверка слоёв в GameWorld сцене

### Шаг 1: Открыть сцену
- В FileSystem: `res://scenes/GameWorld.tscn`
- Двойной клик → откроется в viewport

### Шаг 2: Запустить игру (F5)
- Нажмите **Play** (▶) или **F5**
- Игра запустится с GameWorld сценой

### Шаг 3: Проверить слои через Scene Tree

Во время игры откройте **Remote** вкладку в панели Scene Tree (слева):
- `GameWorld` (Node2D)
  - `WorldRoot` (Node2D)
    - `AmbientLight` (CanvasModulate) — ZIndex: не задан (глобальный)
    - `BiomeTileRenderer` (Node2D) — **ZIndex: 2** (страта 0, биомы)
    - `SurfaceTransitionRenderer` (Node2D) — **ZIndex: 3** (страта 1, переходы)
    - `PlayerSprite` (Sprite2D) — **ZIndex: 4** (игрок)
    - `PlayerShadow` (Sprite2D) — **ZIndex: 3** (тень игрока)
    - `SceneBuilder` (Node) — контейнер рендереров
  - `HUDCanvas` (CanvasLayer) — **Layer: 10** (HUD)

### Порядок слоёв (ZIndex)

| ZIndex | Узел | Что рисует |
|--------|------|------------|
| 2 | BiomeTileRenderer | Спрайты биомов (страта 0) |
| 3 | SurfaceTransitionRenderer | Переходы между биомами (страта 1) |
| 3 | PlayerShadow | Тень игрока |
| 4 | PlayerSprite | Спрайт игрока |
| 10 | HUDCanvas | Текст, время, легенда |

**Если спрайты не видны:**
1. Проверьте ZIndex BiomeTileRenderer = 2 (должен быть ниже всех)
2. Проверьте, что BiomeTileRenderer добавлен в WorldRoot
3. Проверьте Output на сообщения `[SceneBuilder] Loaded biome texture:`

---

## 4. Включение/отключение слоёв

### Через код (SceneBuilder.cs)

```csharp
// Страта 0 (биомы) — включена:
SetupTerrainSprites();

// Страта 1 (переходы) — включена:
SetupSurfaceTransitions();
```

Для отключения — закомментировать строку:
```csharp
// SetupSurfaceTransitions();  // отключить страту 1
```

### Через Godot Editor (временно)

В **Scene Tree** (Remote вкладка во время игры):
1. Найдите узел `BiomeTileRenderer` или `SurfaceTransitionRenderer`
2. В **Inspector** (справа) снимите галочку **Visible**

---

## 5. Проверка текстур в коде

В **Output** панель (внизу) при запуске:

```
[SceneBuilder] Loaded biome texture: res://resources/tiles/64/biome_ocean.png
[SceneBuilder] Loaded biome texture: res://resources/tiles/64/biome_sea.png
...
[SceneBuilder] Missing biome texture: res://resources/tiles/64/biome_xxx.png  ← ERROR
```

Если `Missing` — файл не найден или не импортирован Godot.

### Импорт вручную:
1. Убедитесь, что PNG файл в `res://resources/tiles/64/`
2. В Godot Editor: меню **Project → Reload Current Project**
3. Godot автоматически импортирует новые PNG

---

## 6. Замена спрайта

### Выбор другого варианта (V2, V3, V4):

1. В FileSystem найдите нужный вариант: `res://resources/tiles/64/biome_grassland_v2.png`
2. В коде `SceneBuilder.cs`, метод `LoadBiomeTextures()`:
   ```csharp
   // Заменить:
   string path = $"{basePath}{name}.png";
   // На:
   string path = $"{basePath}{name}_v2.png";  // конкретный вариант
   ```
3. Или: переименуйте файл в FileSystem (правый клик → Rename)

---

## 7. Структура папок спрайтов

```
res://resources/tiles/
├── 64/                    ← Рабочие тайлы (64×64, NEAREST)
│   ├── biome_ocean.png        ← выбранный (default = V1)
│   ├── biome_ocean_v1.png     ← вариант 1
│   ├── biome_ocean_v2.png     ← вариант 2
│   ├── biome_ocean_v3-1.png   ← вариант 3 (подвариант 1)
│   ├── biome_ocean_v3-2.png   ← вариант 3 (подвариант 2)
│   ├── biome_ocean_v4.png     ← вариант 4
│   ├── biome_sea.png           ← выбранный
│   ├── biome_sea_v1.png
│   └── ...
├── originals/             ← Оригиналы (928×928, полноразмерные)
│   ├── biome_ocean_v1.png
│   └── ...
└── transitions/           ← Transition спрайты (будущее)
```

---

## 8. Диагностика проблем

| Проблема | Решение |
|----------|---------|
| Спрайты не видны | Проверить ZIndex (BiomeTileRenderer=2) |
| Красные квадраты вместо спрайтов | Текстуры не загружены — проверить Output на `Missing` |
| Файлы есть, но не загружаются | Переоткрыть проект (Project → Reload) |
| Слои в неправильном порядке | Проверить ZIndex в Inspector |
| Transition спрайты поверх биомов | SurfaceTransitionRenderer ZIndex должен быть > BiomeTileRenderer |

---

## 9. Связанные файлы

| Файл | Полный путь | Назначение |
|------|-------------|------------|
| SceneBuilder | `game/src/Adapter/Scene/SceneBuilder.cs` | Загрузка текстур + рендеринг страты 0 |
| BiomeTileRenderer | `game/src/Adapter/Scene/SceneBuilder.cs` | _Draw() отрисовка спрайтов биомов |
| SurfaceTransitionRenderer | `game/src/Adapter/Scene/SurfaceTransitionRenderer.cs` | _Draw() переходы между биомами |
| TransitionSpriteGenerator | `game/src/Adapter/Scene/TransitionSpriteGenerator.cs` | Генерация transition спрайтов |
| BiomeType enum | `game/src/Core/Data/BiomeType.cs` | Определение биомов |
| GameTile | `game/src/Core/Data/GameTile.cs` | Структура тайла (Biome + Terrain) |
| RenderLayer enum | `game/src/Core/Data/Enums.cs` | ZIndex константы |

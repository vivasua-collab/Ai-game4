# Документация: спрайты тайлов — добавление, нарезка, промпты

> **Статус:** Актуально
> **Дата:** 2026-08-16
> **Связанные документы:** [`docs/docs_v2/03_world/TERRAIN_TRANSITIONS_ANALYSIS.md`](../03_world/TERRAIN_TRANSITIONS_ANALYSIS.md), [`docs/docs_v2/03_world/WORLD_STRATA_DESIGN.md`](../03_world/WORLD_STRATA_DESIGN.md)

---

## 1. Структура папок

Все пути указаны относительно корня репозитория `Ai-game4/`:

```
Ai-game4/game/resources/tiles/
├── originals/                                        ← Оригинальные несжатые PNG (1024×1024)
│   ├── biome_ocean_v1.png                            → https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/originals/biome_ocean_v1.png
│   ├── biome_sea_v1.png                              → https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/originals/biome_sea_v1.png
│   ├── biome_coast_v1.png                            → https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/originals/biome_coast_v1.png
│   ├── biome_grassland_v1.png                        → https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/originals/biome_grassland_v1.png
│   ├── biome_steppe_v1.png                           → https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/originals/biome_steppe_v1.png
│   ├── biome_forest_v1.png                           → https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/originals/biome_forest_v1.png
│   ├── biome_highlands_v1.png                        → https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/originals/biome_highlands_v1.png
│   ├── biome_mountains_v1.png                        → https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/originals/biome_mountains_v1.png
│   └── biome_peak_v1.png                             → https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/originals/biome_peak_v1.png
├── 64/                                               ← Даунскейл до 64×64 (рабочий размер)
│   ├── biome_ocean.png                               → https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/64/biome_ocean.png
│   ├── biome_sea.png                                 → https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/64/biome_sea.png
│   ├── biome_coast.png                               → https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/64/biome_coast.png
│   ├── biome_grassland.png                           → https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/64/biome_grassland.png
│   ├── biome_steppe.png                              → https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/64/biome_steppe.png
│   ├── biome_forest.png                              → https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/64/biome_forest.png
│   ├── biome_highlands.png                           → https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/64/biome_highlands.png
│   ├── biome_mountains.png                           → https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/64/biome_mountains.png
│   └── biome_peak.png                                → https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/64/biome_peak.png
└── transitions/                                      ← Transition спрайты (8 направлений × пары) — пока не созданы
    ├── grassland_to_ocean_N.png                      (будущее)
    ├── grassland_to_ocean_S.png                      (будущее)
    ├── grassland_to_ocean_E.png                      (будущее)
    ├── grassland_to_ocean_W.png                      (будущее)
    ├── grassland_to_ocean_NW.png                     (будущее)
    ├── grassland_to_ocean_NE.png                     (будущее)
    ├── grassland_to_ocean_SW.png                     (будущее)
    └── grassland_to_ocean_SE.png                     (будущее)
```

### Полные пути на GitHub

| Файл | GitHub ссылка |
|------|---------------|
| Оригинал Ocean | https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/originals/biome_ocean_v1.png |
| Оригинал Sea | https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/originals/biome_sea_v1.png |
| Оригинал Coast | https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/originals/biome_coast_v1.png |
| Оригинал Grassland | https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/originals/biome_grassland_v1.png |
| Оригинал Steppe | https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/originals/biome_steppe_v1.png |
| Оригинал Forest | https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/originals/biome_forest_v1.png |
| Оригинал Highlands | https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/originals/biome_highlands_v1.png |
| Оригинал Mountains | https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/originals/biome_mountains_v1.png |
| Оригинал Peak | https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/originals/biome_peak_v1.png |

### Скачивание оригиналов (raw)

Для скачивания несжатых файлов добавьте `?raw=true` к ссылке:

```
https://github.com/vivasua-collab/Ai-game4/blob/main/game/resources/tiles/originals/biome_grassland_v1.png?raw=true
```

Или через `raw.githubusercontent.com`:

```
https://raw.githubusercontent.com/vivasua-collab/Ai-game4/main/game/resources/tiles/originals/biome_grassland_v1.png
```

---

## 2. Спецификация тайла

| Параметр | Значение |
|----------|----------|
| **Размер оригинала** | 1024×1024 px |
| **Рабочий размер** | 64×64 px (`GameConstants.TILE_PIXELS`) |
| **Формат** | PNG с alpha-каналом |
| **PPU** | 32 (Pixels Per Unit) |
| **Фильтр** | Nearest (Point) — pixel-perfect |
| **Стиль** | Top-down 2D, pixel art, fantasy RPG |

---

## 3. Страты и что рисовать на каждом тайле

| Страта | Что рисовать | Что НЕ рисовать |
|--------|-------------|-----------------|
| **0 (биом)** | Только текстура поверхности (земля, вода, камень) | Объекты, флору, камни |
| **1 (поверхность)** | Transition спрайты (края, переходы) | Объекты |
| **2+ (объекты)** | Деревья, кусты, камни, здания | Поверхность |

### Важно для промптов

Биом-тайл (страта 0) — это **только поверхность без объектов**:
- ✅ Трава, земля, песок, вода, камень
- ❌ Деревья, кусты, цветы (это страта 2+)
- ❌ Камни-валуны (это страта 3+)
- ❌ Тропинки, дороги (это страта 1)

См. подробнее: [`docs/docs_v2/03_world/WORLD_STRATA_DESIGN.md`](../../03_world/WORLD_STRATA_DESIGN.md)

---

## 4. Процесс добавления своих спрайтов

### Шаг 1: Генерация оригинала

Сгенерируйте 4 варианта тайла (1024×1024) через внешнюю нейросеть.
Используйте промпты из: [`docs/docs_v2/07_ui/SPRITE_PROMPTS.md`](SPRITE_PROMPTS.md)

Сохраните в:
```
Ai-game4/game/resources/tiles/originals/biome_{name}_v{1-4}.png
```

### Шаг 2: Выбор лучшего

Откройте все 4 варианта, выберите лучший.
Переименуйте в `biome_{name}.png` (без `_v{n}`).

### Шаг 3: Даунскейл

Из папки `Ai-game4/game/resources/tiles/`:

```bash
# Python (Pillow):
python3 -c "
from PIL import Image
img = Image.open('originals/biome_grassland.png')
img = img.convert('RGBA')
img = img.resize((64, 64), Image.NEAREST)  # pixel-perfect
img.save('64/biome_grassland.png')
print('Done')
"

# Или ImageMagick:
convert originals/biome_grassland.png -resize 64x64! -filter Point 64/biome_grassland.png
```

### Шаг 4: Импорт в Godot

Godot автоматически импортирует PNG из `res://` папок.
Файл появится в FileSystem → `res://resources/tiles/64/`

### Шаг 5: Использование в коде

```csharp
var texture = GD.Load<Texture2D>("res://resources/tiles/64/biome_grassland.png");
```

---

## 5. Даунскейл: автоматизация (массовый)

Скрипт для даунскейла всех тайлов сразу:

```python
# Файл: Ai-game4/game/resources/tiles/downscale_all.py
from PIL import Image
import os

src_dir = "originals"
dst_dir = "64"

for fname in sorted(os.listdir(src_dir)):
    if not fname.endswith(".png"):
        continue
    name = fname.replace("_v1.png", ".png").replace("_v2.png", ".png") \
                .replace("_v3.png", ".png").replace("_v4.png", ".png")
    src = os.path.join(src_dir, fname)
    dst = os.path.join(dst_dir, name)
    img = Image.open(src)
    img = img.convert("RGBA")
    img = img.resize((64, 64), Image.NEAREST)
    img.save(dst)
    print(f"  {fname} → {name}")
```

Запуск из `Ai-game4/game/resources/tiles/`:
```bash
python3 downscale_all.py
```

---

## 6. Проверка tileability

```python
# Файл: Ai-game4/game/resources/tiles/check_tileable.py
from PIL import Image

def check_tileable(path):
    """Check if left/right and top/bottom edges match."""
    img = Image.open(path)
    w, h = img.size
    left = img.crop((0, 0, 1, h))
    right = img.crop((w-1, 0, w, h))
    top = img.crop((0, 0, w, 1))
    bottom = img.crop((0, h-1, w, h))
    lr_match = list(left.getdata()) == list(right.getdata())
    tb_match = list(top.getdata()) == list(bottom.getdata())
    print(f"  {path}: L/R={lr_match}, T/B={tb_match}")
    return lr_match and tb_match

import sys
for path in sys.argv[1:]:
    check_tileable(path)
```

Запуск:
```bash
python3 check_tileable.py 64/biome_grassland.png 64/biome_ocean.png
```

---

## 7. Переход от процедурных цветов к спрайтам

Сейчас `Ai-game4/game/src/Adapter/Scene/SceneBuilder.cs` использует MultiMesh с цветами.
Для перехода на спрайты:

1. Загрузить текстуры при инициализации
2. Заменить `SetInstanceColor` на отрисовку текстур через `TileMapLayer` или `DrawTexture`
3. Для переходов: `SurfaceTransitionRenderer` с `DrawTexture`

```csharp
// Будущий подход — загрузить 9 текстур биомов:
var biomeTextures = new Dictionary<BiomeType, Texture2D>
{
    { BiomeType.Ocean,      GD.Load<Texture2D>("res://resources/tiles/64/biome_ocean.png") },
    { BiomeType.Sea,        GD.Load<Texture2D>("res://resources/tiles/64/biome_sea.png") },
    { BiomeType.Coast,      GD.Load<Texture2D>("res://resources/tiles/64/biome_coast.png") },
    { BiomeType.Grassland,  GD.Load<Texture2D>("res://resources/tiles/64/biome_grassland.png") },
    { BiomeType.Steppe,     GD.Load<Texture2D>("res://resources/tiles/64/biome_steppe.png") },
    { BiomeType.Forest,     GD.Load<Texture2D>("res://resources/tiles/64/biome_forest.png") },
    { BiomeType.Highlands,  GD.Load<Texture2D>("res://resources/tiles/64/biome_highlands.png") },
    { BiomeType.Mountains,  GD.Load<Texture2D>("res://resources/tiles/64/biome_mountains.png") },
    { BiomeType.Peak,       GD.Load<Texture2D>("res://resources/tiles/64/biome_peak.png") },
};
```

См. код: `Ai-game4/game/src/Adapter/Scene/SceneBuilder.cs`

---

## 8. Конвертация процедурных transition спрайтов в PNG

Текущие процедурные transition спрайты (в `TransitionSpriteGenerator.cs`) можно сохранить как PNG:

```csharp
// В Ai-game4/game/src/Adapter/Scene/TransitionSpriteGenerator.cs:
var img = CreateTransitionImage(color, dir, 64);
img.SavePng($"res://resources/tiles/transitions/{pair}_{dir}.png");
```

Это позволит редактировать их в графическом редакторе и заменять на качественные.

---

## 9. Формат именования

| Тип | Формат | Пример | Путь |
|------|--------|--------|------|
| Биом | `biome_{name}.png` | `biome_grassland.png` | `game/resources/tiles/64/` |
| Биом (вариант) | `biome_{name}_v{n}.png` | `biome_grassland_v1.png` | `game/resources/tiles/originals/` |
| Transition | `{biomeA}_to_{biomeB}_{dir}.png` | `grassland_to_ocean_NW.png` | `game/resources/tiles/transitions/` |
| Объект | `obj_{name}.png` | `obj_tree_oak.png` | `game/resources/tiles/objects/` |

---

## 10. Чеклист качества спрайта

- [ ] Размер 1024×1024 (оригинал)
- [ ] Даунскейл до 64×64 через NEAREST
- [ ] Tileable (края совпадают)
- [ ] Нет объектов (только поверхность)
- [ ] Alpha-канал (для transition спрайтов)
- [ ] Стиль consistent с другими тайлами
- [ ] Имя файла по формату
- [ ] Файл закоммичен в git

---

## 11. Связанные файлы

| Файл | Путь | Назначение |
|------|------|------------|
| Промпты | `docs/docs_v2/07_ui/SPRITE_PROMPTS.md` | Промпты для внешней нейросети |
| Анализ переходов | `docs/docs_v2/03_world/TERRAIN_TRANSITIONS_ANALYSIS.md` | Анализ подходов к переходам |
| Страты | `docs/docs_v2/03_world/WORLD_STRATA_DESIGN.md` | Описание страт мира |
| Пары биомов | `docs/docs_v2/03_world/BIOME_TRANSITION_PAIRS.md` | Список пар для transition |
| SceneBuilder | `game/src/Adapter/Scene/SceneBuilder.cs` | Рендеринг страты 0 |
| TransitionGenerator | `game/src/Adapter/Scene/TransitionSpriteGenerator.cs` | Генерация transition спрайтов |
| SurfaceRenderer | `game/src/Adapter/Scene/SurfaceTransitionRenderer.cs` | Рендеринг страты 1 |
| BiomeType | `game/src/Core/Data/BiomeType.cs` | Enum биомов |
| GameTile | `game/src/Core/Data/GameTile.cs` | Структура тайла (Biome + Terrain) |
| TileService | `game/src/Modules/Tile/TileService.cs` | Генерация карты |

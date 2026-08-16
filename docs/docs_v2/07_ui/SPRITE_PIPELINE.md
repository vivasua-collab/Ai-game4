# Документация: спрайты тайлов — добавление, нарезка, промпты

> **Статус:** Актуально
> **Дата:** 2026-08-16
> **Связанные документы:** `TERRAIN_TRANSITIONS_ANALYSIS.md`, `WORLD_STRATA_DESIGN.md`

---

## 1. Структура папок

```
game/resources/tiles/
├── originals/                    ← Оригинальные несжатые PNG (1024×1024)
│   ├── biome_grassland_v1.png
│   ├── biome_grassland_v2.png
│   ├── biome_grassland_v3.png
│   ├── biome_grassland_v4.png
│   ├── biome_ocean_v1.png
│   └── ...
├── 64/                           ← Даунскейл до 64×64 (рабочий размер)
│   ├── biome_grassland.png       ← выбранный лучший вариант
│   ├── biome_ocean.png
│   └── ...
├── transitions/                  ← Transition спрайты (8 направлений × пары)
│   ├── grassland_to_ocean_N.png
│   ├── grassland_to_ocean_S.png
│   ├── grassland_to_ocean_E.png
│   ├── grassland_to_ocean_W.png
│   ├── grassland_to_ocean_NW.png
│   ├── grassland_to_ocean_NE.png
│   ├── grassland_to_ocean_SW.png
│   └── grassland_to_ocean_SE.png
└── README.md                     ← Этот файл
```

## 2. Спецификация тайла

| Параметр | Значение |
|----------|----------|
| **Размер оригинала** | 1024×1024 px |
| **Рабочий размер** | 64×64 px (TILE_PIXELS) |
| **Формат** | PNG с alpha-каналом |
| **PPU** | 32 (Pixels Per Unit) |
| **Фильтр** | Nearest (Point) — pixel-perfect |
| **Стиль** | Top-down 2D, pixel art, fantasy RPG |

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

## 4. Процесс добавления своих спрайтов

### Шаг 1: Генерация оригинала

Сгенерируйте 4 варианта тайла (1024×1024) через внешнюю нейросеть.
Сохраните в `game/resources/tiles/originals/biome_{name}_v{1-4}.png`

### Шаг 2: Выбор лучшего

Откройте все 4 варианта, выберите лучший.
Переименуйте в `biome_{name}.png`.

### Шаг 3: Даунскейл

```bash
# С помощью Python (Pillow):
python3 -c "
from PIL import Image
img = Image.open('originals/biome_grassland.png')
img = img.resize((64, 64), Image.NEAREST)  # pixel-perfect
img.save('64/biome_grassland.png')
"

# Или через ImageMagick:
convert originals/biome_grassland.png -resize 64x64! -filter Point 64/biome_grassland.png
```

### Шаг 4: Импорт в Godot

Godot автоматически импортирует PNG из `res://` папок.
Файл появится в FileSystem → `res://resources/tiles/64/`

### Шаг 5: Использование в коде

```csharp
var texture = GD.Load<Texture2D>("res://resources/tiles/64/biome_grassland.png");
```

## 5. Даунскейл: автоматизация

Я выполняю даунскейл средствами Python (Pillow):

```python
from PIL import Image
import os

def downscale_tile(src_path, dst_path, size=64):
    """Downscale with NEAREST filter for pixel-perfect look."""
    img = Image.open(src_path)
    img = img.convert("RGBA")
    img = img.resize((size, size), Image.NEAREST)
    img.save(dst_path)
    print(f"Saved {dst_path} ({size}×{size})")
```

## 6. Проверка tileability

После даунскейла проверьте, что тайл бесшовный:

```python
from PIL import Image

def check_tileable(path):
    """Check if left/right and top/bottom edges match."""
    img = Image.open(path)
    w, h = img.size
    # Compare left and right columns
    left = img.crop((0, 0, 1, h))
    right = img.crop((w-1, 0, w, h))
    # Compare top and bottom rows
    top = img.crop((0, 0, w, 1))
    bottom = img.crop((0, h-1, w, h))
    
    lr_match = list(left.getdata()) == list(right.getdata())
    tb_match = list(top.getdata()) == list(bottom.getdata())
    
    print(f"Left/Right seamless: {lr_match}")
    print(f"Top/Bottom seamless: {tb_match}")
    return lr_match and tb_match
```

## 7. Переход от процедурных цветов к спрайтам

Сейчас SceneBuilder использует MultiMesh с цветами.
Для перехода на спрайты:

1. Загрузить текстуры при инициализации
2. Заменить `SetInstanceColor` на `SetInstanceTexture` (или использовать TileMapLayer)
3. Для перехода: использовать SurfaceTransitionRenderer с DrawTexture

```csharp
// Текущий подход (цвета):
multimesh.SetInstanceColor(idx, color);

// Будущий подход (текстуры):
// Загрузить 9 текстур биомов
var biomeTextures = new Dictionary<BiomeType, Texture2D>
{
    { BiomeType.Grassland, GD.Load<Texture2D>("res://resources/tiles/64/biome_grassland.png") },
    { BiomeType.Ocean, GD.Load<Texture2D>("res://resources/tiles/64/biome_ocean.png") },
    // ...
};
```

## 8. Конвертация процедурных спрайтов в PNG

Текущие процедурные transition спрайты можно сохранить как PNG:

```csharp
var img = CreateTransitionImage(color, dir, 64);
img.SavePng($"res://resources/tiles/transitions/{pair}_{dir}.png");
```

Это позволит:
- Редактировать их в графическом редакторе
- Использовать как референс для внешней нейросети
- Заменять на качественные версии

## 9. Формат именования

| Тип | Формат | Пример |
|------|--------|--------|
| Биом | `biome_{name}.png` | `biome_grassland.png` |
| Биом (вариант) | `biome_{name}_v{n}.png` | `biome_grassland_v1.png` |
| Transition | `{biomeA}_to_{biomeB}_{dir}.png` | `grassland_to_ocean_NW.png` |
| Объект | `obj_{name}.png` | `obj_tree_oak.png` |

## 10. Чеклист качества спрайта

- [ ] Размер 1024×1024 (оригинал)
- [ ] Даунскейл до 64×64 через NEAREST
- [ ] Tileable (края совпадают)
- [ ] Нет объектов (только поверхность)
- [ ] Alpha-канал (для transition спрайтов)
- [ ] Стиль consistent с другими тайлами
- [ ] Имя файла по формату

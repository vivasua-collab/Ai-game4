# Промпты для генерации спрайтов объектов карты локации

> **Назначение:** Промпты для внешней нейросети (Midjourney, DALL-E, Stable Diffusion, ComfyUI и т.д.)
> **Дата:** 2026-08-17
> **Связанные документы:** `SPRITE_PROMPTS.md` (биомы), `SPRITE_PIPELINE.md` (пайплайн), `WORLD_STRATA_DESIGN.md` (страты)

---

## 1. Общие правила

### 1.1. Отличие от тайлов биомов

Объекты — это спрайты **на поверхности**, а не сама поверхность:
- Имеют **alpha-канал** (прозрачный фон)
- Стоят **поверх** тайла биома (страта 0)
- Размещаются на стратах 3-9 (см. `WORLD_STRATA_DESIGN.md`)
- Не tileable (уникальный объект, не повторяющийся паттерн)

### 1.2. Обязательные элементы промпта

Каждый промпт должен содержать:
- **Тип:** "top-down 2D game sprite" (не tile!)
- **Стиль:** "pixel art" (тот же, что у биомов)
- **Размер:** "1024×1024"
- **Ориентация:** "top-down view, looking straight down at 45-degree angle" (изометричный вид сверху)
- **Фон:** "transparent background" или "isolated on solid black background" (для последующего вырезания)
- **Ограничения:** "no shadow on ground, no text, no borders, no frame"
- **Качество:** "high detail, fantasy RPG game asset, consistent pixel art style"

### 1.3. Ключевые отличия от статьи на Habr

Из статьи "Генерируем ассеты для игры" (https://habr.com/ru/companies/studyai/articles/1069832/) вынесены уроки:

| Урок | Применение |
|------|-----------|
| Чёрный фон лучше прозрачного для генерации | Используем "isolated on solid black background" |
| Указывать размер сетки: "64x64 pixel grid" | Добавляем в каждый промпт |
| Указывать "consistent style" между объектами | Добавляем "same style as other game sprites" |
| 4 кадра анимации для живых объектов | Для NPC/монстров (будущий файл) |
| Статичные объекты — 1 кадр | Для деревьев, камней, зданий |
| ComfyUI не делает спрайты — делает иллюстрации | Для спрайтов используем Nano Banana Pro / DALL-E / Midjourney |

### 1.4. Что НЕ рисовать

- ❌ Тень на земле (рендерится отдельно в Godot)
- ❌ Тайл поверхности под объектом (это страта 0)
- ❌ Другие объекты рядом (1 объект = 1 спрайт)
- ❌ Текст, надписи, интерфейс
- ❌ Рамки, границы

---

## 2. Категории объектов

Из `game/src/Core/Data/TileEnums.cs`:

| Категория | ObjectType | Страта | Описание |
|-----------|-----------|--------|----------|
| **Vegetation** | Tree_Oak, Tree_Pine, Tree_Birch | 5 | Крупные деревья (непроходимы) |
| **Vegetation** | Bush, Bush_Berry | 4 | Кусты (замедление, собираемые) |
| **Rock** | Rock_Small, Rock_Medium, Rock_Large | 5 | Камни (непроходимы, ломаются) |
| **Interactive** | Chest | 3 | Сундук (открыть/взломать) |
| **Interactive** | OreVein | 3 | Рудная жила (добывать) |
| **Interactive** | Herb | 4 | Трава (собирать) |
| **Building** | (будущие) | 6 | Здания, стены, заборы |
| **Decoration** | (будущие) | 3 | Декорации (цветы, пни, поваленные деревья) |

---

## 3. Промпты для растительности (страты 4-5)

### 3.1. Tree_Oak — Дуб

```
pixel art top-down 2D game sprite of a large oak tree, seen from above at 45-degree angle,
thick brown trunk, spreading green canopy with individual leaves visible,
fantasy RPG game asset, 1024x1024, isolated on solid black background,
sharp pixel edges, 64x64 pixel grid equivalent, no ground shadow,
no text, no borders, consistent with fantasy pixel art style
```

**Варианты:**
- v1: Летний дуб, густая крона
- v2: Осенний дуб, жёлто-оранжевая листва
- v3: Молодой дуб, меньше крона
- v4: Старый дуб, дупло, сухие ветки

### 3.2. Tree_Pine — Сосна

```
pixel art top-down 2D game sprite of a pine tree, seen from above at 45-degree angle,
dark green needle canopy, brown trunk visible at base,
conical shape, fantasy RPG game asset, 1024x1024, isolated on solid black background,
sharp pixel edges, 64x64 pixel grid equivalent, no ground shadow,
no text, no borders, consistent with fantasy pixel art style
```

**Варианты:**
- v1: Взрослая сосна, тёмно-зелёная
- v2: Молодая сосна, светло-зелёная
- v3: Засохшая сосна, коричневая
- v4: Заснеженная сосна, снег на ветках

### 3.3. Tree_Birch — Берёза

```
pixel art top-down 2D game sprite of a birch tree, seen from above at 45-degree angle,
white trunk with black marks, light green oval canopy,
fantasy RPG game asset, 1024x1024, isolated on solid black background,
sharp pixel edges, 64x64 pixel grid equivalent, no ground shadow,
no text, no borders, consistent with fantasy pixel art style
```

**Варианты:**
- v1: Летняя берёза, светло-зелёная
- v2: Осенняя берёза, золотая
- v3: Молодая берёза, тонкий ствол
- v4: Зимняя берёза, без листьев

### 3.4. Bush — Куст

```
pixel art top-down 2D game sprite of a wild bush, seen from above,
round green shrub with small leaves, medium density,
fantasy RPG game asset, 1024x1024, isolated on solid black background,
sharp pixel edges, 64x64 pixel grid equivalent, no ground shadow,
no text, no borders, consistent with fantasy pixel art style
```

**Варианты:**
- v1: Зелёный куст, круглый
- v2: Цветущий куст, белые цветы
- v3: Сухой куст, коричневый
- v4: Густой куст, тёмно-зелёный

### 3.5. Bush_Berry — Ягодный куст

```
pixel art top-down 2D game sprite of a berry bush, seen from above,
green shrub with red berries visible, round shape,
fantasy RPG game asset, 1024x1024, isolated on solid black background,
sharp pixel edges, 64x64 pixel grid equivalent, no ground shadow,
no text, no borders, consistent with fantasy pixel art style
```

**Варианты:**
- v1: Красные ягоды
- v2: Синие ягоды
- v3: Жёлтые ягоды
- v4: Незрелые ягоды (зелёные)

---

## 4. Промпты для камней (страта 5)

### 4.1. Rock_Small — Малый камень

```
pixel art top-down 2D game sprite of a small rock, seen from above,
gray stone with moss patches, rough texture, fist-sized,
fantasy RPG game asset, 1024x1024, isolated on solid black background,
sharp pixel edges, 64x64 pixel grid equivalent, no ground shadow,
no text, no borders, consistent with fantasy pixel art style
```

**Варианты:**
- v1: Серый гранит
- v2: Замшелый камень
- v3: Песчаник, рыжеватый
- v4: Базальт, тёмный

### 4.2. Rock_Medium — Средний камень

```
pixel art top-down 2D game sprite of a medium boulder, seen from above at 45-degree angle,
gray stone with cracks and lichen, roughly human-sized,
fantasy RPG game asset, 1024x1024, isolated on solid black background,
sharp pixel edges, 64x64 pixel grid equivalent, no ground shadow,
no text, no borders, consistent with fantasy pixel art style
```

**Варианты:**
- v1: Серый валун с трещинами
- v2: Округлый камень, гладкий
- v3: Острый камень, угловатый
- v4: Замшелый валун

### 4.3. Rock_Large — Большой камень

```
pixel art top-down 2D game sprite of a large boulder formation, seen from above at 45-degree angle,
massive gray stone with mineral veins, multi-toned,
fantasy RPG game asset, 1024x1024, isolated on solid black background,
sharp pixel edges, 64x64 pixel grid equivalent, no ground shadow,
no text, no borders, consistent with fantasy pixel art style
```

**Варианты:**
- v1: Огромный валун
- v2: Группа камней
- v3: Скальный выступ
- v4: Камень с кристаллами

---

## 5. Промпты для интерактивных объектов (страта 3)

### 5.1. Chest — Сундук

```
pixel art top-down 2D game sprite of a treasure chest, seen from above at 45-degree angle,
wooden chest with iron bands and lock, closed, ornate,
fantasy RPG game asset, 1024x1024, isolated on solid black background,
sharp pixel edges, 64x64 pixel grid equivalent, no ground shadow,
no text, no borders, consistent with fantasy pixel art style
```

**Варианты:**
- v1: Закрытый деревянный сундук
- v2: Открытый сундук с золотом
- v3: Каменный саркофаг
- v4: Железный сундук, окованный

### 5.2. OreVein — Рудная жила

```
pixel art top-down 2D game sprite of an ore vein in rock, seen from above,
rocky outcrop with shiny metal ore deposits, glimmering crystals in stone,
fantasy RPG game asset, 1024x1024, isolated on solid black background,
sharp pixel edges, 64x64 pixel grid equivalent, no ground shadow,
no text, no borders, consistent with fantasy pixel art style
```

**Варианты:**
- v1: Железная руда (оранжевые вкрапления)
- v2: Медная руда (зелёные вкрапления)
- v3: Серебряная руда (белые блестящие)
- v4: Духовные кристаллы (фиолетовые)

### 5.3. Herb — Лекарственная трава

```
pixel art top-down 2D game sprite of a medicinal herb plant, seen from above,
small green plant with glowing leaves, distinct from grass,
fantasy RPG game asset, 1024x1024, isolated on solid black background,
sharp pixel edges, 64x64 pixel grid equivalent, no ground shadow,
no text, no borders, consistent with fantasy pixel art style
```

**Варианты:**
- v1: Зелёная трава с цветком
- v2: Светящаяся синяя трава
- v3: Красный гриб
- v4: Золотой корень

---

## 6. Промпты для декораций (страта 3, будущие)

### 6.1. Пень

```
pixel art top-down 2D game sprite of a tree stump, seen from above,
cut wooden stump with growth rings, small moss,
fantasy RPG game asset, 1024x1024, isolated on solid black background,
sharp pixel edges, 64x64, no ground shadow, no text, no borders
```

### 6.2. Поваленное дерево

```
pixel art top-down 2D game sprite of a fallen log, seen from above,
horizontal tree trunk on ground, bark texture, moss and mushrooms,
fantasy RPG game asset, 1024x1024, isolated on solid black background,
sharp pixel edges, 64x64, no ground shadow, no text, no borders
```

### 6.3. Цветы (декоративные)

```
pixel art top-down 2D game sprite of wildflowers, seen from above,
small cluster of colorful flowers, red yellow and white,
fantasy RPG game asset, 1024x1024, isolated on solid black background,
sharp pixel edges, 64x64, no ground shadow, no text, no borders
```

---

## 7. Промпты для зданий (страта 6, будущие)

### 7.1. Деревянная хижина

```
pixel art top-down 2D game sprite of a wooden hut, seen from above at 45-degree angle,
small cabin with thatched roof, wooden walls, simple door,
fantasy RPG game asset, 1024x1024, isolated on solid black background,
sharp pixel edges, 64x64 pixel grid equivalent, no ground shadow,
no text, no borders, consistent with fantasy pixel art style
```

### 7.2. Каменная стена

```
pixel art top-down 2D game sprite of a stone wall segment, seen from above,
gray stone bricks, mossy, weathered, horizontal segment,
fantasy RPG game asset, 1024x1024, isolated on solid black background,
sharp pixel edges, 64x64, no ground shadow, no text, no borders
```

### 7.3. Деревянный забор

```
pixel art top-down 2D game sprite of a wooden fence segment, seen from above,
simple wooden posts and rails, weathered gray-brown,
fantasy RPG game asset, 1024x1024, isolated on solid black background,
sharp pixel edges, 64x64, no ground shadow, no text, no borders
```

---

## 8. Сводная таблица

| # | Объект | Категория | Страта | Вариантов | Промптов |
|---|--------|-----------|--------|-----------|----------|
| 1 | Tree_Oak | Vegetation | 5 | 4 | 4 |
| 2 | Tree_Pine | Vegetation | 5 | 4 | 4 |
| 3 | Tree_Birch | Vegetation | 5 | 4 | 4 |
| 4 | Bush | Vegetation | 4 | 4 | 4 |
| 5 | Bush_Berry | Vegetation | 4 | 4 | 4 |
| 6 | Rock_Small | Rock | 5 | 4 | 4 |
| 7 | Rock_Medium | Rock | 5 | 4 | 4 |
| 8 | Rock_Large | Rock | 5 | 4 | 4 |
| 9 | Chest | Interactive | 3 | 4 | 4 |
| 10 | OreVein | Interactive | 3 | 4 | 4 |
| 11 | Herb | Interactive | 4 | 4 | 4 |
| 12 | Пень | Decoration | 3 | 1 | 1 |
| 13 | Поваленное дерево | Decoration | 3 | 1 | 1 |
| 14 | Цветы | Decoration | 3 | 1 | 1 |
| 15 | Деревянная хижина | Building | 6 | 1 | 1 |
| 16 | Каменная стена | Building | 6 | 1 | 1 |
| 17 | Деревянный забор | Building | 6 | 1 | 1 |
| **Итого** | | | | **49** | **49** |

---

## 9. После генерации

1. Сохранить оригиналы в `game/resources/tiles/objects/originals/obj_{name}_v{n}.png`
2. Выбрать лучший → переименовать в `obj_{name}.png`
3. Даунскейл до 64×64 через NEAREST → `game/resources/tiles/objects/64/obj_{name}.png`
4. Вырезать из чёрного фона (если генерировали на чёрном):
   ```python
   from PIL import Image
   img = Image.open("obj_tree_oak.png")
   img = img.convert("RGBA")
   datas = img.getdata()
   newData = []
   for item in datas:
       if item[0] < 10 and item[1] < 10 and item[2] < 10:
           newData.append((0, 0, 0, 0))  # transparent
       else:
           newData.append(item)
   img.putdata(newData)
   img.save("obj_tree_oak.png")
   ```
5. Проверить, что спрайт отображается поверх тайла биома
6. Закоммитить в git

---

## 10. Структура папок

```
game/resources/tiles/objects/
├── originals/                    ← Оригиналы 1024×1024
│   ├── obj_tree_oak_v1.png
│   ├── obj_tree_oak_v2.png
│   └── ...
└── 64/                           ← Рабочие 64×64 с alpha
    ├── obj_tree_oak.png
    ├── obj_tree_pine.png
    ├── obj_rock_medium.png
    └── ...
```

---

## 11. Использование в коде

```csharp
var objectTextures = new Dictionary<ObjectType, Texture2D>
{
    { ObjectType.Tree_Oak,    GD.Load<Texture2D>("res://resources/tiles/objects/64/obj_tree_oak.png") },
    { ObjectType.Tree_Pine,   GD.Load<Texture2D>("res://resources/tiles/objects/64/obj_tree_pine.png") },
    { ObjectType.Rock_Medium, GD.Load<Texture2D>("res://resources/tiles/objects/64/obj_rock_medium.png") },
    { ObjectType.Chest,       GD.Load<Texture2D>("res://resources/tiles/objects/64/obj_chest.png") },
    // ...
};
```

---

## 12. Связанные файлы

| Файл | Полный путь | Назначение |
|------|-------------|------------|
| Промпты биомов | `docs/docs_v2/07_ui/SPRITE_PROMPTS.md` | Промпты для тайлов биомов (страта 0) |
| Пайплайн спрайтов | `docs/docs_v2/07_ui/SPRITE_PIPELINE.md` | Инструкция по добавлению и нарезке |
| Страты мира | `docs/docs_v2/03_world/WORLD_STRATA_DESIGN.md` | Описание страт 0-9 |
| Анализ переходов | `docs/docs_v2/03_world/TERRAIN_TRANSITIONS_ANALYSIS.md` | Анализ подходов к transition |
| ObjectType enum | `game/src/Core/Data/TileEnums.cs` | Все типы объектов |
| ObjectDefaults | `game/src/Core/Data/ObjectDefaults.cs` | Параметры объектов (HP, ресурсы) |
| TileService | `game/src/Modules/Tile/TileService.cs` | Генерация объектов на тайлах |

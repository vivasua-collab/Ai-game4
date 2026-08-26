# Промпты для генерации спрайтов тайлов

> **Назначение:** Промпты для внешней нейросети (Midjourney, DALL-E, Stable Diffusion и т.д.)
> **Дата:** 2026-08-16
> **Связанные документы:** `SPRITE_PIPELINE.md`, `TERRAIN_TRANSITIONS_ANALYSIS.md`

---

## 1. Общие правила для всех промптов

### 1.1. Обязательные элементы промпта

Каждый промпт должен содержать:
- **Тип:** "top-down 2D game tile"
- **Стиль:** "pixel art" или "digital painting" (выберите один для всех)
- **Размер:** "square, 1024×1024"
- **Ориентация:** "top-down view, looking straight down"
- **Ограничения:** "no objects, no trees, no rocks, no buildings, no characters, no text, no borders, no frame"
- **Качество:** "seamless tileable texture, high detail, fantasy RPG game asset"

### 1.2. Что НЕ рисовать (страты 2+)

Спрайт биома (страта 0) — **только поверхность**:
- ❌ Деревья, кусты, цветы — страта 2+
- ❌ Камни-валуны — страта 3+
- ❌ Тропинки, дороги — страта 1
- ❌ Здания, стены — страта 6+
- ❌ Персонажи, NPC — страта 4
- ❌ Эффекты (огонь, лёд) — страта 7

### 1.3. Что рисовать

Только **текстуру поверхности**:
- ✅ Цвет и фактура материала (трава, песок, вода, камень)
- ✅ Микрорельеф (неровности, трещины, волны)
- ✅ Цветовые вариации в пределах биома
- ✅ Естественные переходы цвета

### 1.4. Параметры генерации

| Параметр | Значение |
|----------|----------|
| Размер | 1024×1024 (квадрат) |
| Стиль | Pixel art (предпочтительно) или digital painting |
| Tileable | Да (обязательно) |
| Количество вариантов | 4 на каждый биом |
| Референсное изображение | Скриншот Heroes 3 (прилагается) |

---

## 2. Промпты для 9 биомов

### 2.1. Ocean (Океан)

```
Top-down 2D game tile, deep ocean water surface, pixel art style, 
dark blue water with subtle wave patterns and depth variations, 
small foam spots, fantasy RPG game asset, seamless tileable texture, 
high detail, 1024×1024, no objects, no land, no boats, no creatures, 
no text, no borders
```

**Варианты (v1-v4):**
- v1: Тёмно-синий, спокойная вода
- v2: Сине-зелёный, лёгкие волны
- v3: Глубокий синий, подводные течения
- v4: Тёмный, мистический, духовная энергия

### 2.2. Sea (Мелководье)

```
Top-down 2D game tile, shallow sea water surface, pixel art style,
medium blue water with visible sandy bottom, gentle ripples,
lighter blue-green tones, fantasy RPG game asset, seamless tileable texture,
high detail, 1024×1024, no objects, no land, no boats, no fish,
no text, no borders
```

**Варианты:**
- v1: Голубой, мелководье с песком
- v2: Бирюзовый, коралловый отсвет
- v3: Светло-синий, ряби
- v4: Зелёно-голубой, водоросли

### 2.3. Coast / Beach (Побережье)

```
Top-down 2D game tile, sandy beach surface, pixel art style,
golden sand with small pebbles and shell fragments, wind ripples,
warm yellow-tan color, fantasy RPG game asset, seamless tileable texture,
high detail, 1024×1024, no objects, no water, no plants, no rocks,
no text, no borders
```

**Варианты:**
- v1: Золотистый песок, гладкий
- v2: Песок с галькой
- v3: Раковины и обломки
- v4: Влажный песок (тёмный)

### 2.4. Grassland (Луга)

```
Top-down 2D game tile, grassland terrain surface, pixel art style,
lush green grass with natural color variations, small patches of 
darker and lighter green, subtle ground texture, fantasy RPG game asset,
seamless tileable texture, high detail, 1024×1024, no trees, no flowers,
no bushes, no rocks, no buildings, no text, no borders
```

**Варианты:**
- v1: Ярко-зелёная трава, ровная
- v2: Тёмно-зелёная, густая
- v3: Желтовато-зелёная, сухая
- v4: Изумрудная, сочная

### 2.5. Steppe (Степь)

```
Top-down 2D game tile, dry steppe terrain surface, pixel art style,
brown-yellow dry earth with cracked soil patches, sparse dry grass blades,
arid landscape texture, fantasy RPG game asset, seamless tileable texture,
high detail, 1024×1024, no trees, no bushes, no rocks, no buildings,
no text, no borders
```

**Варианты:**
- v1: Сухая жёлто-коричневая земля
- v2: Треснувшая земля
- v3: Рыжая, глинистая
- v4: Серая, пыльная

### 2.6. Forest (Лес — поверхность)

```
Top-down 2D game tile, forest floor terrain surface, pixel art style,
dark green mossy ground with leaf litter, small twigs and fallen leaves,
dappled light pattern, fantasy RPG game asset, seamless tileable texture,
high detail, 1024×1024, no trees, no bushes, no mushrooms, no rocks,
no buildings, no text, no borders
```

**Варианты:**
- v1: Тёмный мох, опавшие листья
- v2: Земля с корнями
- v3: Хвойная подстилка
- v4: Трава с тенью от деревьев

### 2.7. Highlands (Нагорье)

```
Top-down 2D game tile, highland terrain surface, pixel art style,
rocky gray-brown ground with sparse grass patches, gravel and small stones,
rugged terrain texture, fantasy RPG game asset, seamless tileable texture,
high detail, 1024×1024, no large boulders, no trees, no buildings,
no text, no borders
```

**Варианты:**
- v1: Серо-коричневая каменистая земля
- v2: Щебень с редкой травой
- v3: Скалистая поверхность
- v4: Замшелые камни

### 2.8. Mountains (Горы)

```
Top-down 2D game tile, mountain rock surface, pixel art style,
gray stone with cracks and mineral veins, rocky texture with snow dust,
cold color palette, fantasy RPG game asset, seamless tileable texture,
high detail, 1024×1024, no boulders, no trees, no buildings,
no text, no borders
```

**Варианты:**
- v1: Серый гранит с трещинами
- v2: Тёмный базальт
- v3: Светлый песчаник
- v4: Снежная пыль на камне

### 2.9. Peak (Вершины)

```
Top-down 2D game tile, mountain peak snow surface, pixel art style,
white snow with ice crystals, blue-white shadows, frozen texture,
sparkling ice details, fantasy RPG game asset, seamless tileable texture,
high detail, 1024×1024, no objects, no rocks, no buildings,
no text, no borders
```

**Варианты:**
- v1: Белый снег, искрящийся
- v2: Лёд с трещинами
- v3: Снег с метелью
- v4: Глубокий снег, голубоватый

---

## 3. Референсное изображение

Прилагается скриншот Heroes of Might and Magic 3 как референс:
- `docs/screenshots/heroes3_reference.png` (или из upload)
- Стиль: pre-rendered 2D, top-down
- Переходы: edge sprites между биомами

Используйте референс для:
- Общей цветовой палитры
- Уровня детализации
- Стиля переходов

---

## 4. Промпты для transition спрайтов (будущее)

После утверждения базовых биомов:

### 4.1. Прямой переход (N/S/E/W)

```
Top-down 2D game tile, transition between {biomeA} and {biomeB},
pixel art style, {biomeB} covers the top half, {biomeA} covers the bottom,
natural blending edge, fantasy RPG game asset, seamless horizontally,
1024×1024, transparent background where no terrain, no objects, no text
```

### 4.2. Диагональный переход (NW/NE/SW/SE)

```
Top-down 2D game tile, diagonal corner transition, pixel art style,
{biomeB} in the top-left quarter circle, {biomeA} fills the rest,
smooth curved edge, fantasy RPG game asset, 1024×1024,
transparent background where no terrain, no objects, no text
```

---

## 5. Чеклист для каждого сгенерированного спрайта

- [ ] Размер 1024×1024
- [ ] Top-down view
- [ ] Нет объектов (только поверхность)
- [ ] Tileable (края совпадают)
- [ ] Стиль consistent с другими тайлами
- [ ] Нет текста, рамок, водяных знаков
- [ ] 4 варианта сохранены в `originals/`

---

## 6. После генерации

1. Сохранить 4 варианта в `game/resources/tiles/originals/`
2. Выбрать лучший → переименовать в `biome_{name}.png`
3. Даунскейл до 64×64 через NEAREST → `64/biome_{name}.png`
4. Проверить tileability
5. Загрузить в Godot (автоматический импорт)

Всего: 9 биомов × 4 варианта = **36 изображений** для генерации.

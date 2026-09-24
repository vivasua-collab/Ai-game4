# Промпты для генерации спрайтов персонажей, NPC и экипировки

> **Назначение:** Готовые к копированию промпты для нейросетей (z-ai image /
> z-ai image-edit CLI; внешний генератор — любой, поддерживающий промпты) по
> генерации анимированных спрайтов: игрок, NPC, звери, оружие/броня, иконки.
> Дополняет `SPRITE_PROMPTS_OBJECTS.md` (объекты карты) и реализует план
> `SPRITE_ANIMATION_PLAN.md`.
>
> **Статус:** Актуально. Дата: 2026-09-24.
>
> **Связанные документы:** `SPRITE_ANIMATION_PLAN.md` (план/фазы G1–G5/условия),
> `SPRITE_PIPELINE.md` (пайплайн 1024→64), `SPRITE_PROMPTS_OBJECTS.md` (правила
> и уроки генерации), `SPRITE_CATALOG.md` (каталог имён).

---

## 1. Общие правила промптов персонажей

### 1.1. Обязательные элементы (наследуют §1.2 SPRITE_PROMPTS_OBJECTS)

- **Тип:** `pixel art 2D game sprite sheet` (для листов) / `pixel art 2D game sprite` (для одиночных);
- **Ракурс:** `45-degree top-down oblique view (3/4 perspective)` — как объекты карты;
- **Стиль:** `xianxia / wuxia fantasy RPG, sharp pixel edges`;
- **Размер:** `1024x1024` одиночный; **`1344x768`** лист-полоса (до 6 кадров);
  **`1024x1024`** сетка направлений 5×N;
- **Фон:** `isolated on solid black background` (чёрный фон лучше прозрачного — урок Habr);
- **Ограничения:** `no ground shadow, no text, no borders, no frame numbers, no UI`;
- **Пиксельная сетка:** `64x64 pixel grid equivalent`.

### 1.2. Отличия персонажей от объектов (критично)

| Правило | Формулировка в промпте | Зачем |
|---|---|---|
| **Identity-якорь** | неизменный текстовый блок описания персонажа (§2) в КАЖДОМ промпте | один и тот же герой во всех листах |
| **Структура листа** | `N frames arranged in a single horizontal row, same character in every frame, consistent size and proportions` | ровная нарезка, внутри-листовая консистентность |
| **Направление** | `facing right, full body` (этап D1); для D2 — `5 rows of directions` (§4) | FlipH-модель кода (SPRITE_CATALOG §16) |
| **Поза-контроль** | анимация описывается глаголами поз (`left foot forward, arms swinging`) | меньше свободы генератору — стабильнее цикл |
| **Базовая линия** | `feet aligned to the same baseline in every frame` | стопы не «прыгают» (§10.1 плана) |

### 1.3. Рабочий процесс (консистентность)

1. Сгенерировать `player_idle` (2–4 варианта) → выбрать лучший → **одобрить как эталон**.
2. ВСЕ последующие листы игрока — через **image-edit от эталона**
   (`z-ai image-edit -i approved_idle.png -p "same character, walk cycle…"`),
   не «с нуля». Это главный митигатор риска рассинхрона персонажа (§10.3 плана).
3. Перекраски (роли NPC, материалы) — только edit-каналом.
4. Генерация «с нуля» — только для новых архетипов (звери, спец-NPC, предметы).

---

## 2. Identity-якорь игрока (канонический блок)

Единый блок во все промпты игрока (EN, между кавычек — не менять):

```
young male cultivator, slim build, dark purple robe with wide sleeves,
black hair tied in a topknot, light skin, simple dark cloth boots, bare hands
```

Соответствие процедурному рецепту (ProceduralSpriteGenerator.CreatePlayerSprite):
роба фиолет `#4D3380`, кожа `#E6BF99`, волосы `#261A0D`, сапоги `#1A140D`.
Расхождения стиля с процедурным fallback допустимы (PNG — целевой уровень),
но палитра-якорь — §4.5 плана.

**Промпт-префикс игрока (общая шапка):**

```
pixel art 2D game sprite, 45-degree top-down oblique view (3/4 perspective),
character: young male cultivator, slim build, dark purple robe with wide sleeves,
black hair tied in a topknot, light skin, simple dark cloth boots, bare hands,
xianxia fantasy RPG style, sharp pixel edges, 64x64 pixel grid equivalent,
isolated on solid black background, no ground shadow, no text, no borders
```

---

## 3. Промпты: листы игрока (фазы G1–G5 плана §5.1)

> Кадровость/fps — по спецификации §5.1 плана. Все промпты: направление
> «вправо», полоса кадров. Для генерации «с нуля» — шапку §2 + блок анимации.

### 3.1. player_idle — G1 (4 кадра, loop)

```
[шапка §2] + animation: idle breathing cycle, 4 frames in a single horizontal row,
same character standing still, subtle chest breathing and slight arm sway,
facing right, full body, feet aligned to the same baseline in every frame,
consistent size and proportions
```

Варианты: v1 спокойный; v2 лёгкий ветер в рукавах; v3 рука на поясе; v4 взгляд
вдаль (голова чуть повёрнута). Выбрать 1 → эталон.

### 3.2. player_walk — G2 (6 кадров, loop)

Генерация с нуля:

```
[шапка §2] + animation: walk cycle, 6 frames in a single horizontal row,
full stride cycle (contact, down, passing, up, contact positions),
same character, facing right, full body, feet aligned to the same baseline
```

Edit-вариант (рекомендуется):

```
same character, walking animation sprite sheet, 6 frames in a single
horizontal row, keep face, robe, colors and proportions identical
```

### 3.3. player_run — G4 (6 кадров, loop)

```
same character, running animation sprite sheet, 6 frames in a single horizontal
row, dynamic leaning forward, robe fluttering, arms pumping, keep identity
```

### 3.4. player_melee — G2 (6 кадров, one-shot 0.4 с ≈ замах кода 0.42 с)

```
[шапка §2] + animation: horizontal sword slash with empty hands (weapon is drawn
separately by the engine), 6 frames in a single horizontal row:
wind-up stance, swing back, mid-slash with torso rotation, follow-through,
recovery, return to stance, facing right, feet on same baseline
```

> **Важно:** руки в позе «держит оружие» (кулаки вдоль диагонали удара), сам
> меч НЕ рисуется — оружие рисует MainHand-композит поверх. Кадр 3 — размах
> совпадает с сектором замаха (выпад 12 px к цели).

### 3.5. player_bow — G4 (3 кадра)

```
same character, bow shooting animation, 3 frames: drawing an invisible bow
(arms in draw pose), full draw hold, release follow-through, facing right
```

### 3.6. player_cast — G3 (4 кадра, loop при зарядке)

```
same character, spell charging animation, 4 frames: hands raised with palms up,
subtle rising energy stance, robe sleeves flaring, calm focused face
```

### 3.7. player_meditate — G3 (4–6 кадров, loop) — закрывает «медитация стоя»

```
same character, sitting in lotus position meditation, 4 frames of subtle
breathing cycle, hands resting on knees, eyes closed, perfectly still posture,
slight qi shimmer silhouette allowed, facing right
```

### 3.8. player_guard — G4 (2 кадра, loop)

```
same character, defensive stance animation, 2 frames: braced guard posture with
arms crossed in front, and a slight tense variation
```

### 3.9. player_hit — G3 (2 кадра, once)

```
same character, taking damage reaction, 2 frames: flinch backward with head
snapped back, and recovery hunch
```

### 3.10. player_death — G3 (4 кадра, once + hold) — закрывает «смерть без позы»

```
same character, death animation, 4 frames: knees buckling, falling sideways,
body halfway down, lying flat on the ground facing right, final frame is a
still lying pose
```

### 3.11. player_dash — G5 (3 кадра)

```
same character, quick dash lunge animation, 3 frames: crouch ready, explosive
forward lunge with motion streaks behind, landing recovery
```

### 3.12. player_sleep — G5 (2 кадра, loop)

```
same character, sleeping lying on the ground curled in robe, 2 frames of slow
breathing, eyes closed
```

### 3.13. player_breakthrough — G5 (4 кадра, once)

```
same character, cultivation breakthrough animation, 4 frames: straining upward
posture, aura flare silhouette, arcs of energy bursting outward, final radiant
standing pose
```

### 3.14. Сетка направлений (этап D2, G5)

```
[шапка §2] + walk cycle sprite sheet arranged as a grid: 5 rows of directions
(row 1 facing camera, row 2 three-quarter front, row 3 pure side profile,
row 4 three-quarter back, row 5 facing away), 6 columns of walk frames,
30 cells total, evenly spaced, same character in every cell
```

> Ряд 3 (side) = одобренный лист D1 (переиспользовать!). Кадровая формула кода:
> `Frame = row × 6 + frame`, FlipH для левой стороны — как в демо.

---

## 4. Промпты: NPC (фаза G3 плана §6)

### 4.1. Базовое гуманоидное тело (одобряемый эталон)

```
pixel art 2D game sprite, 45-degree top-down oblique view (3/4 perspective),
character: middle-aged villager man, plain grey-brown hanfu robe, straw sandals,
short black hair, light skin, xianxia fantasy RPG style, sharp pixel edges,
64x64 pixel grid equivalent, isolated on solid black background,
no ground shadow, no text, no borders
```

+ листы (те же блоки анимации, что §3): `npc_idle` (2–4 кадра), `npc_walk`
(4–6), `npc_melee` (4), `npc_cast` (3), `npc_hit` (2), `npc_death` (3).
Женская база (G5): `young villager woman, plain hanfu dress, hair bun`.

### 4.2. Перекраски по ролям (edit-канал, 8 ролей)

База → роль (палитра NPCSpriteRenderer :71-78):

| Роль | Edit-промпт | Палитра кода |
|---|---|---|
| Guard | `recolor the robe to royal blue guard uniform with leather bracers, keep pose, shading and proportions identical` | #334D80 |
| Merchant | `recolor the robe to wealthy gold-trimmed merchant robe, add a small trade bundle on the back` | #806626 |
| Elder | `recolor the robe to humble brown elder robe, change hair to grey beard` | #735933 |
| Cultivator | `recolor the robe to green sect cultivator robe with sash` | зелёный |
| Disciple | `recolor the robe to blue-grey disciple robe` | сине-серый |
| Enemy | `recolor the robe to ragged dark red bandit clothes` | #802626 |
| Monster | `recolor to wild grey furs outfit, more hunched pose` | серый |
| Passerby | `recolor the robe to plain traveller grey clothes` | серый |

Формат команды (пример Guard):

```bash
z-ai image-edit -p "recolor the robe to royal blue guard uniform with leather
bracers, keep pose, shading, face and proportions identical, keep solid black
background" -i npc_walk_base.png -o npc_guard_walk.png -s 1344x768
```

### 4.3. Спец-архетипы (G5, генерация с нуля)

```
… character: demonic enemy cultivator, black-red tattered robes, pale skin,
faint dark aura, sinister grin …
… character: rival young cultivator, white and silver robe, arrogant smirk …
… character: fairy maiden, flowing pale-teal robes, long hair, delicate …
… character: beast cultivator, fur-trimmed outfit, clawed hands, wild hair …
```

### 4.4. Звери (фаза G2/G3)

Общая шапка зверей:

```
pixel art 2D game sprite sheet, 45-degree top-down oblique view,
xianxia fantasy RPG, sharp pixel edges, 64x64 pixel grid equivalent,
isolated on solid black background, no ground shadow, no text, full body
side view facing right, feet aligned to the same baseline
```

| Вид | Промпт (добавить к шапке) | Листы |
|---|---|---|
| Волк | `grey wolf, lean body, dark grey back fur, alert ears, bushy tail; animation: walk cycle, 4 frames in a single horizontal row` (+ run 4, attack lunge 2, idle 2, lying dead 1) | 5 листов |
| Олень | `brown deer, slim legs, small antlers, light spots on back; walk/run/idle/death` | 4 листа |
| Кролик | `small white-grey rabbit, long ears, hopping gait; walk (hop) 4, run 4, idle 2, death 1` | 4 листа |

### 4.5. Трупы (G5)

```
pixel art 2D game sprite, 45-degree oblique view, human body wrapped in dark
brown burial shroud lying on the ground, small golden loot pouch beside it,
single static frame …
beast corpse: grey wolf lying on its side, motionless …
```

(до G5 остаётся текущий маркер «саван+крест+бейдж» — уже работает)

---

## 5. Промпты: экипировка (фазы G1/G4 плана §7–§8)

### 5.1. Оружие — Icon 32×32 (предметный вид, вертикально)

Шапка:

```
pixel art 2D game asset icon, single weapon item, vertical orientation as held
upright, fantasy xianxia RPG style, sharp pixel edges, 32x32 pixel grid
equivalent, isolated on solid black background, no text, no borders,
no hand, no character
```

Классы (7): `iron dagger, short straight sword, single-handed axe, long spear
with leaf blade, massive greatsword, wooden recurve bow with string, wooden
staff with jade orb top`.

### 5.2. Оружие — Hand 48×48 (в руке, диагональ 45°)

```
pixel art 2D game sprite, single melee weapon held diagonally at 45 degrees
pointing up-right, grip at the lower-left corner of the image, blade pointing
to the upper-right, iron sword, xianxia fantasy RPG, sharp pixel edges,
48x48 pixel grid equivalent, solid black background, no hand, no hilt furniture
beyond the grip
```

> Геометрия ДОЛЖНА совпадать с процедурным эталоном: рукоять 1H в текстуре
> G=(16,36), диагональ через кадр; 2H (spear/greatsword/staff) — длинная
> диагональ; bow — вертикально у левой кисти (WeaponVisualCatalog §7).

### 5.3. Материалы-перекраски (edit-канал, 5 тиров)

От базового iron-варианта каждого класса:

```
recolor the metal to polished steel, keep shape and outline identical
recolor to green-tinged spirit iron with faint jade glow
recolor to pale blue-white star metal with subtle sparkle
recolor to dark violet void metal with purple glow
```

( tirы: iron→steel→spirit→star→void; канон палитр §7 SPRITE_CATALOG)

### 5.4. Броня — Torso-варианты тела (полно-кадровые, §8.3 плана)

Edit от одобренного `player_walk`:

```
same character now wearing a leather chest vest over the purple robe,
keep pose, face, proportions and frame layout identical, robe sleeves visible
```

Материалы: `cloth robe variant / leather vest / iron chainmail / plate iron
chest / spirit jade scale armor` → 5 тиров × (idle+walk+melee) = 15 листов.

### 5.5. Броня — стикеры Head/Feet/Belt

```
pixel art 2D game sprite, iron helmet only, 45-degree oblique view, no face,
no character, designed to overlay a 48px tall character head, sharp pixel
edges, solid black background
… leather boots pair, walking pose … small leather belt with buckle …
```

(Feet — 3 позы: idle-stance, walk-step, walk-roll; стикеры 24×24/24×16/32×12)

### 5.6. Иконки предметов

Шапка иконок:

```
pixel art 2D game item icon, single object centered, fantasy xianxia RPG,
sharp pixel edges, 32x32 pixel grid equivalent, solid black background,
no text, no borders
```

| Группа | Промпты |
|---|---|
| Камни Ци (5 размеров) | `small glowing qi crystal, {tiny dust mote / small pebble / broken shard / fist-sized stone / large boulder}, pale blue-white inner glow` (QiStoneSize: Dust/Pebble/Shard/Stone/Boulder, 1–125 см³) |
| Пилюли | `red healing pill on tiny cloth, green qi restoration pill, golden breakthrough pill, white antidote vial` |
| Материалы | `iron ore chunk, iron ingot bar, leather hide roll, cloth bolt, jade fragment, glowing spirit stone shard` |
| Еда | `wheat bread loaf, roasted meat on bone` |
| Зарядник | `jade belt-mounted qi charger artifact with three crystal sockets` |

---

## 6. Пайплайн обработки после генерации

### 6.1. CLI-команды (z-ai)

```bash
# Генерация листа (1344×768 — полоса из ≤6 кадров)
z-ai image -p "<промпт из §3-§5>" -o ./out/player_walk_v1.png -s 1344x768

# Генерация одиночного спрайта/иконки
z-ai image -p "<промпт>" -o ./out/player_idle_v1.png -s 1024x1024

# Перекраска/вариант от эталона (консистентность!)
z-ai image-edit -p "<edit-промпт>" -i ./approved/npc_walk_base.png \
  -o ./out/npc_guard_walk.png -s 1344x768
```

### 6.2. Нарезка листа (PIL-скелет)

```python
from PIL import Image

def slice_sheet(src, cols, rows, frame=64, baseline_align=True):
    img = Image.open(src).convert("RGBA")
    # 1) фон: цвет угла -> прозрачность
    corner = img.getpixel((2, 2))
    px = img.load()
    for y in range(img.height):
        for x in range(img.width):
            p = px[x, y]
            if abs(p[0]-corner[0])<24 and abs(p[1]-corner[1])<24 \
               and abs(p[2]-corner[2])<24:
                px[x, y] = (0, 0, 0, 0)
    # 2) сетка: равные ячейки (cols x rows); при неровной раскладке —
    #    детект фоновых колонок/строк (разделители) и ручная сетка
    cw, ch = img.width // cols, img.height // rows
    out = []
    for r in range(rows):
        for c in range(cols):
            cell = img.crop((c*cw, r*ch, (c+1)*cw, (r+1)*ch))
            bbox = cell.getbbox()          # кроп по содержимому
            if bbox: cell = cell.crop(bbox)
            # 3) нормализация в квадрат 64x64 с выравниванием низа (стопы)
            canvas = Image.new("RGBA", (frame, frame), (0, 0, 0, 0))
            w, h = cell.size
            scale = min(frame // 1 // max(1, w), 1) if w > frame else 1
            #    (даунскейл — только NEAREST, кратный)
            cell = cell.resize((w, h), Image.NEAREST)
            canvas.paste(cell, ((frame - w)//2, frame - h), cell)
            out.append(canvas)
    return out   # сохранить как один лист-полосу 64*N x 64
```

> Даунскейл 1024→64 — только `Image.NEAREST` (SPRITE_PIPELINE §4). Итоговый
> лист: `player_walk.png` = полоса 384×64 (6 кадров), либо сетка 5×6 = 384×320
> (D2, как `run.png` демо). Отдельные кадры в файлы не раскладывать — код
> индексирует `Hframes/Vframes/Frame`.

### 6.3. Чек качества (автоматический + ревью)

- счётчик кадров == спецификации (§5.1 плана);
- низ bbox каждого кадра ± 2 px (базовая линия);
- ширина тела в кадре ≤ 48 px + поля;
- палитра: доля якорных цветов (§4.5 плана) — визуальное ревью пары
  «персонаж поверх биом-тайла» (превью zoom 3);
- альфа-края без ореолов (порог вырезания 24/255 — §6.2).

### 6.4. Каталог файлов

```
game/resources/sprites/
├── characters/
│   ├── player/            player_{anim}.png           (§3, 13 листов D1)
│   ├── player_directions/ player_{anim}_grid.png      (D2, 5×N сетки)
│   └── npc/               npc_{role}_{anim}.png       (§4)
├── animals/               animal_{species}_{anim}.png (§4.4)
├── corpses/               corpse_{human,beast}.png    (§4.5)
├── equipment/
│   ├── icons/             weapon_{class}_{tier}.png / armor_{slot}_{tier}.png
│   └── equipped/          weapon_hand_{class}_{tier}.png / armor overlay (§5)
└── items/                 qistone_{size}.png, pill_*.png, material_*.png
```

---

## 7. Сводная таблица объёмов (соответствие фазам плана)

| Категория | Листов (D1) | Кадров | Генераций | Эдитов | Фаза |
|---|---|---|---|---|---|
| Игрок (13 листов §3) | 13 | 52 | ~15 | ~10 | G1–G5 |
| Игрок D2 (5×N сетки) | 13 | ~260 | ~13 | — | G5 |
| NPC база+роли | 6 + 16 | ~90 | ~10 | ~20 | G3 |
| NPC спец (4 архетипа) | 8 | ~32 | ~8 | — | G5 |
| Звери (3 вида) | 13 | ~35 | ~10 | — | G2 |
| Трупы | 2 | 2 | 2 | — | G5 |
| Оружие PNG (7×5, 2 вида) | 70 файлов | — | 14 | 56 | G1/G4 |
| Броня torso (5×3) + стикеры | ~45 файлов | — | ~15 | ~35 | G4 |
| Иконки предметов | ~20 | — | ~20 | — | G1 |
| **Итого (D1, без D2)** | | | **~70** | **~120** | |

---

## 8. Связанные файлы

| Файл | Назначение |
|---|---|
| `SPRITE_ANIMATION_PLAN.md` | мастер-план: состояния, фазы, условия, интеграция |
| `SPRITE_PIPELINE.md` | даунскейл 1024→64, NEAREST, импорт |
| `SPRITE_PROMPTS_OBJECTS.md` | правила промптов + уроки генерации (чёрный фон) |
| `SPRITE_CATALOG.md` | каталог имён/размеров/зеркалирования (§16/§17) |
| `PROCEDURAL_SPRITES.md` §5 | контракт PNG-миграции по ключу кэша |
| `RENDER_LAYERS.md` §3.5 | порядок слоёв игрока (Shadow→Visual→Equipped→Effects) |
| `2.5d-project-with-c#-demo/assets/player/` | эталон листов (run.png 5×6, 15 fps) |

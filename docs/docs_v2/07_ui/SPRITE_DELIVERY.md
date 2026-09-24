# Доставка спрайтов: операционный регламент (G0)

> **Назначение:** как доставить сгенерированные PNG в игру, проверить их и
> принять. Регламент реализует подготовительную фазу **G0** (2026-09-25):
> вся инфраструктура уже в коде, игра до доставки выглядит идентично
> процедурной (fallback), после доставки PNG «зажигаются» без правок кода.
>
> **Статус:** G0 внедрена и верифицирована (ANIMQA PASS, регрессия 21/21).
> **Связанные:** `SPRITE_ANIMATION_PLAN.md` (мастер-план),
> `SPRITE_PROMPTS_CHARACTERS.md` (промпты), `SPRITE_PIPELINE.md`
> (пайплайн 1024→64), `sprites_manifest.json` (контракт доставки).

---

## 1. Что уже готово (G0 — не требует действий при доставке)

| Компонент | Файл | Что делает |
|---|---|---|
| **I-2 Лист-загрузчик** | `game/src/Adapter/Scene/SpriteSheetCache.cs` | PNG → `ImageTexture` **без editor-импорта** (`Image.LoadFromFile` — файл кладётся в каталог и работает сразу, в т.ч. headless). Реестр fps/loop (24 анимации: игрок 13 / NPC 6 / звери 5). Кадры = размеры PNG (W/64). Негативный кэш. |
| **I-1 Аниматор игрока** | `game/src/Adapter/Scene/PlayerAnimator.cs` | Sprite2D Hframes/Vframes/Frame по состояниям: death > melee > bow > hit > meditate > run/walk > idle. One-shot-длительности синхронны кодовым таймерам (melee 0.42с = MainHandSwingSec). walk/run fps × множитель скорости движения. Пауза — стоп-кадр. |
| **I-11 PNG оружия** | `WeaponVisualCatalog.GetOrCreate` | PNG-первый по тем же ключам `class|tier` (rarity НЕ в имени — золотая обводка остаётся кодом). Нет PNG → процедурный рецепт R15. Механика крепления/офсетов/замаха не менялась. |
| **I-5 NPC** | `NPCSpriteRenderer` | `npc_{role}_{anim}` → `npc_base_{anim}` → процедурный. Кадры — `DrawTextureRectRegion` в `_Draw`; walk-детект по дельте позиции; melee — по замахам R16; рассинхрон толпы по хэшу id. |
| **I-7 Звери** | `AnimalSpriteRenderer` | `animal_{species}_{anim}` → процедурный. Та же региональная отрисовка. |
| **QA-сим** | `SpriteAnimSimDebug.cs` | `GODOT_ANIMQA_DEBUG=1` — верифицирует весь контур (fallback/загрузчик/аниматор/one-shot/death-hold/оружие/NPC/звери). В регрессии №21. **Dual-режим (G0.2):** шаг 0 зондирует диск — сим валиден и ДО, и ПОСЛЕ доставки (файл есть → ожидаем PNG-текстуру; нет → fallback). |
| **Приёмщик батчей** | `tools/sprites/intake_batch.py` | Пакетная доставка zip: распаковка (zip-slip защита) → инвентаризация → маппинг имён на манифест (два пространства: README file-stem + id; версии/тиры/префиксы; `--map` для ручных) → выбор лучшего варианта (геометрия>альфа>версия) → маршруты DIRECT/CONFORM/SLICE/FIT/REJECT → валидация чеками → установка (`--force` для перезаписи, `--dry-run` без касания game/). |
| **Манифест** | `game/resources/sprites/sprites_manifest.json` | 221 позиция: файл, кадры, fps, loop, фаза G1–G5. Генератор `tools/sprites/make_manifest.py`. |
| **Валидатор** | `tools/sprites/validate_sprites.py` | Приёмка: размеры/кадры/прозрачность/базовая линия/ширина тела. |
| **Слайсер** | `tools/sprites/slice_sheet.py` | Исходник генерации (1344×768 и т.п.) → игровой лист: вырез фона, сетка (авто-детект разделителей), bbox-кроп, NEAREST-даунскейл, нормализация 64×64 с выравниванием низа к y=58. |

## 2. Процедура доставки (по одной позиции или пачкой)

**Пачкой (рекомендуется):** положить zip в `tools/` (репо-относительные пути
или плоские имена) и запустить приёмщик — он делает шаги 3–5 автоматически:

```bash
python3 tools/sprites/intake_batch.py tools/<batch>.zip --dry-run   # отчёт без установки
python3 tools/sprites/intake_batch.py tools/<batch>.zip             # установка
python3 tools/sprites/intake_batch.py tools/<batch>.zip --force     # замена итерации
```

Вариант файла `player_walk_v2.png` / `wolf_walk.png` / `weapon_dagger_1.png`
сопоставляется с манифестом автоматически; нестандартные имена — через
`--map имя=позиция`. Итог: маршруты DIRECT (готовая геометрия)/CONFORM
(доводка кадров)/SLICE (сырой лист → слайсер)/FIT (иконки)/REJECT,
дефекты валидатора — в отчёте.

**По одной позиции:**

1. **Генерация** — промпт из `SPRITE_PROMPTS_CHARACTERS.md` (§3 игрок /
   §4 NPC и звери / §5 экипировка), выход 1024×1024 (одиночный) или
   1344×768 / 1440×720 (полоса ≤6 кадров).
2. **Отбор** — 2–4 варианта, выбрать лучший (консистентность §10.3 плана).
3. **Нарезка:**
   ```bash
   python3 tools/sprites/slice_sheet.py out/player_walk_v1.png \
       --frames 6 --out game/resources/sprites/characters/player/player_walk.png
   ```
   D2-сетка: `--grid 5`. Уже прозрачный исходник: `--no-bg-cut`.
4. **Валидация:**
   ```bash
   python3 tools/sprites/validate_sprites.py player   # или без фильтра — всё
   ```
   Дефектный файл → исправить и перегенерировать (не коммитить с FAIL).
5. **Рантайм-QA:**
   ```bash
   env GODOT_NEWGAME=1 GODOT_ANIMQA_DEBUG=1 \
     tools/run_godot.sh --headless --path . scenes/MainMenu.tscn
   ```
   Шаги 1/4/5/6 покажут `png`-режим вместо fallback, счётчики > 0.
6. **Визуальный ревью** (Xvfb-скрин, zoom 3) — «персонаж поверх биом-тайла»,
   силуэт читается, стиль совпадает (чек-лист §10 плана).
7. **Коммит** PNG + обновлённый манифест (если менялись кадры/fps).

## 3. Семантика fallback (важно)

- **Отсутствие файла ≠ ошибка.** `TryGetSheet` → null → рендер остаётся на
  процедурном спрайте. Поэтому можно доставлять поэтапно (фазами G1–G5),
  мир никогда не «пустеет».
- **Тот же ключ — тот же слот.** Замена файла = замена текстуры после
  перезапуска (кэш сессии).
- **Битая геометрия игнорируется.** Лист не кратен 64 → `GD.PrintErr` и
  fallback (не краш) — см. `SpriteSheetCache.LoadSheet`.
- **Рантайм не читает манифест.** fps/loop — реестр `SpriteSheetCache.Specs`;
  кадры — размеры PNG. Изменить fps можно в реестре (код) — манифест
  зеркало для людей/валидатора.

## 4. Формат листов (контракт)

| Параметр | Значение |
|---|---|
| Кадр | 64×64 RGBA, прозрачный фон, NEAREST |
| D1-полоса | `64×N` — 1 анимация × 1 направление («вправо») × N кадров |
| D2-сетка | `64×N × 320` — 5 рядов (front / front-¾ / side / back-¾ / back); бинарный facing берёт ряд 2 (side); октантный facing — задача I-3 |
| Тело | в 48×48-боксе по X; правая кисть (+6,+6) от центра; стопы — низ bbox к y=58, разброс ±2px |
| Оружие Icon | 32×32, вертикально |
| Оружие Hand | 48×48, диагональ 45°, рукоять G=(16,36) |
| Стикеры брони | head 24×24, feet 24×16, belt 32×12 |

## 5. Именование (ключи резолва рантайма)

```
player_{anim}                  → characters/player/
npc_base_{anim}                → characters/npc/     (базовое тело)
npc_{role}_{anim}              → characters/npc/      (8 ролей: guard/merchant/
                                  elder/cultivator/enemy/passerby/disciple/monster)
animal_{species}_{anim}        → animals/             (wolf/deer/rabbit)
corpse_{human,beast}           → corpses/
weapon_{class}_{tier}          → equipment/icons/     (Icon 32×32)
weapon_hand_{class}_{tier}     → equipment/equipped/  (Hand 48×48)
armor_*, player_{anim}_armor_{tier} → equipment/{icons,equipped}/, characters/player/
qistone_{size}, pill_*, material_* → items/
```

`{anim}` игрока: idle/walk/run/melee/bow/cast/meditate/guard/hit/death/
dash/sleep/breakthrough. NPC: idle/walk/melee/cast/hit/death. Звери:
idle/walk/run/attack/death.

Полный перечень с кадрами/fps/фазами — `sprites_manifest.json`
(`python3 tools/sprites/validate_sprites.py --list`).

## 6. Что НЕ входит в G0 (остальные задачи плана §11)

| ID | Задача | Когда |
|---|---|---|
| I-3 | Октантный facing (D2 8 направлений) | G5, код-раунд |
| I-4 | Визуализация дэша/сна (триггеры состояний) | G5 |
| I-6 | Вариативность NPC по seed | G5 |
| I-8 | Иконки брони/предметов в UI (CreateEquipmentIcon) | G1/G4 |
| I-9 | EquipmentVisualCatalog (slot-ключи брони) | G4 |
| I-10 | Слои брони игрока (Sprite2D + z-сдвиг MainHand 5→6) | G4 |
| — | Триггер player_cast (зарядка техники) | G3 |
| — | Трупы-позы (сейчас канон-маркер «саван») | G5 |

## 7. QA-доступ (headless-симы)

- `GODOT_ANIMQA_DEBUG=1` — сим №21 в **dual-режиме**: `step0 delivery:`
  печатает состояние поставки (player/npc/animal/weapon PNG); ожидания всех
  шагов вычисляются от диска — PASS и на пустом каталоге, и с доставкой
  (G0.2: пруфы обеих сторон, лог-прогон 09-24).
- `GWC.PlayerAnimId / PlayerAnimFrame / PlayerAnimIsPng /
  PlayerAnimFrameCount / PlayerAnimatorForQA` — аниматор игрока.
- `NPCSpriteRenderer.ResolveNpcAnim(id, role, species, moving)` — прямой
  резолв (без отрисовки) + `GetNpcAnimInfo(id)` из последнего кадра.
- `AnimalSpriteRenderer.ResolveAnimalAnim(id, species, size, moving)`.
- `WeaponVisualCatalog.PngHandCount / PngIconCount` — счётчики PNG.
- `SpriteSheetCache.LoadAttemptCount / ResetCache()`.
- QA-плейсхолдеры `_qa/qa_walk_strip.png` (полоса) и `_qa/qa_grid_5x6.png`
  (сетка) — генерируются `tools/sprites/make_placeholders.py`; боевые
  ассеты туда НЕ класть.

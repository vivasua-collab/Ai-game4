# Каталог доставки спрайтов (G0, 2026-09-25)

**Контракт:** PNG кладётся в свой каталог под каноническим именем →
подхватывается игрой **без правок кода и без editor-импорта**
(`Image.LoadFromFile`). PNG нет → рендер остаётся на процедурном
спрайте (fallback, PROCEDURAL_SPRITES §5). 

Манифест доставки (машиночитаемый контракт): `sprites_manifest.json`
(генератор: `tools/sprites/make_manifest.py`, валидатор:
`tools/sprites/validate_sprites.py`).

## Структура и имена файлов

| Каталог | Файлы | Формат |
|---|---|---|
| `characters/player/` | `player_{anim}.png` — idle/walk/run/melee/bow/cast/meditate/guard/hit/death/dash/sleep/breakthrough | полоса 64×N×64 (D1); D2-сетки — `player_{anim}.png` 64×N×320 (5 рядов) |
| `characters/npc/` | `npc_base_{anim}.png` (база) + `npc_{role}_{anim}.png` (guard/merchant/elder/cultivator/enemy/passerby/disciple/monster) — idle/walk/melee/cast/hit/death | 64×N×64 |
| `animals/` | `animal_{species}_{anim}.png` — wolf/deer/rabbit × idle/walk/run/attack/death | 64×N×64 |
| `corpses/` | `corpse_human.png`, `corpse_beast.png` | 64×32 |
| `equipment/icons/` | `weapon_{class}_{tier}.png` (32×32), `armor_{slot}_{tier}.png` (32×32) | иконки предметов |
| `equipment/equipped/` | `weapon_hand_{class}_{tier}.png` (48×48, диагональ 45°, рукоять G=(16,36)) | вид «в руке» |
| `items/` | `qistone_{size}.png`, `pill_*.png`, `material_*.png` | 32×32 иконки |
| `_qa/` | QA-плейсхолдеры пайплайна (`qa_walk_strip.png`, `qa_grid_5x6.png`) | **НЕ поставлять сюда боевые ассеты** |

## Обязательные условия приёмки (сокращённо; полный чек-лист —
SPRITE_ANIMATION_PLAN §10 / SPRITE_DELIVERY.md)

1. Кадр ровно 64×64 (стикеры/иконки — 32/24), прозрачный фон, RGBA.
2. NEAREST-даунскейл (без сглаживания), базовая линия стоп ±2px.
3. Якорь правой кисти (+6,+6) от центра — совместимость MainHand.
4. fps/кадры — по манифесту (`frames` = ширина/64).
5. Один лист = одна генерация; перекраски — только image-edit.

## Проверка доставки

```bash
python3 tools/sprites/validate_sprites.py          # весь каталог
python3 tools/sprites/validate_sprites.py player   # только игрок
```

QA рантайма: `GODOT_NEWGAME=1 GODOT_ANIMQA_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn`

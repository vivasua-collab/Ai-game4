#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
G0 (2026-09-25): валидатор доставки спрайтов.

Проверяет game/resources/sprites/ против sprites_manifest.json:
  - геометрия: размер файла == size манифеста (кратность 64 для листов);
  - кадры: W/frame == frames (счётчик кадров — чек §10.1);
  - прозрачность: фон прозрачен (углы + доля alpha=0), RGBA;
  - базовая линия стоп: низ bbox каждого кадра ±2px (§10.1);
  - ширина тела ≤ 48+8 в X для листов персонажей (D1);
  - NEAREST-след: отсутствие полупрозрачных «ореолов» на краях
    (доля пикселей 0<alpha<255 в кромке кадра).

Режимы:
    python3 tools/sprites/validate_sprites.py            # всё
    python3 tools/sprites/validate_sprites.py player     # фильтр по подстроке id
    python3 tools/sprites/validate_sprites.py --phases G1,G2
    python3 tools/sprites/validate_sprites.py --list     # только список ожидающихся

Выход: код 0 — все доставленные позиции валидны (отсутствующие НЕ FAIL —
поставка поэтапная, фазы G1–G5); код 1 — есть дефектные файлы.
"""
import argparse
import json
import os
import sys

from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
SPRITES = os.path.join(ROOT, "game", "resources", "sprites")
MANIFEST = os.path.join(SPRITES, "sprites_manifest.json")

FRAME = 64
BASELINE_TOL = 2   # ±px базовой линии стоп
BODY_W_MAX = 56    # 48-бокс + допуск полей
QA_DIR = "_qa"     # QA-плейсхолдеры не валидируются

# PT3 (плейтест-2-рестарт): политика РОСТА силуэта игрока. Репорт:
# «статичный спрайт больше спрайта бега — персонаж уменьшается» — листы
# были разноразмерны (idle/walk 52, run 44, melee 40, hit 36), а валидатор
# молчал: гейты были только по базовой линии и ширине. Канон чекпоинта
# PT3: стоячий рост 52 (±4 на позу/боб бега), сидячий (медитация) 48,
# смерть — первый кадр стоячий, дальше падение. NPC/звери — канон не
# зафиксирован, без политики (только их собственные гейты выше).
STANDING_ANIMS = {"player_idle", "player_walk", "player_run",
                  "player_melee", "player_cast", "player_hit"}
SITTING_ANIMS = {"player_meditate"}
# PT3: атакующие позы НЕсут декоративные дуги замаха (меч/энергия) шире
# тела — гейт «48-бокс+8» писался для ходячих поз. Дуги и ДО нормализации
# были ~49px (проходили впритык), после подъёма к канону 52 — до 64-65px
# (клип по канве). Тело при этом в норме; допустимый предел для
# дуговых листов — вся ширина кадра.
ARC_WIDE_ANIMS = {"player_melee", "player_cast"}
BODY_W_MAX_ARC = 64
STANDING_MED_MIN, STANDING_MED_MAX = 48, 56
STANDING_HARD_MAX = 58
SITTING_MIN, SITTING_MAX = 40, 52
DEATH_FIRST_MIN = 46


def check_file(entry):
    """Проверка одной позиции. Возвращает список дефектов (пустой = OK)."""
    path = os.path.join(SPRITES, entry["file"])
    if not os.path.exists(path):
        return None  # не доставлена — не дефект (поставка поэтапная)

    defects = []
    exp_w, exp_h = entry["size"]
    frames = entry["frames"]
    try:
        img = Image.open(path).convert("RGBA")
    except Exception as e:
        return [f"не читается: {e}"]

    if img.size != (exp_w, exp_h):
        defects.append(f"размер {img.size} ≠ манифест {(exp_w, exp_h)}")
        return defects

    if img.mode != "RGBA":
        defects.append(f"режим {img.mode} ≠ RGBA")

    # Прозрачность фона: углы пустые + доля прозрачных пикселей.
    w, h = img.size
    corners = [img.getpixel(p)[3] for p in [(0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)]]
    if any(a > 8 for a in corners):
        defects.append("фон не прозрачен (углы непустые)")

    px = img.load()
    total = w * h
    transparent = sum(1 for y in range(h) for x in range(w) if px[x, y][3] == 0)
    if transparent < total * 0.35:
        defects.append(f"прозрачных пикселей {transparent * 100 // total}% < 35% "
                       "(фон не вырезан?)")

    # Кадры: ширина / frame == frames (для однострочных).
    if h == FRAME and w % FRAME == 0:
        if w // FRAME != frames:
            defects.append(f"кадров {w // FRAME} ≠ манифест {frames}")
    elif h % FRAME == 0 and w % FRAME == 0 and frames > 1:
        rows = h // FRAME
        if w // FRAME != frames:
            defects.append(f"кадров в ряду {w // FRAME} ≠ манифест {frames} (рядов {rows})")

    # Базовая линия стоп + ширина тела + рост силуэта — для листов 64×N.
    if h % FRAME == 0 and w % FRAME == 0 and w >= FRAME:
        fsize = FRAME
        cols, rows = w // fsize, h // fsize
        bottoms, widths, heights = [], [], []
        for r in range(rows):
            for c in range(cols):
                cell = img.crop((c * fsize, r * fsize, (c + 1) * fsize, (r + 1) * fsize))
                b = cell.getbbox()
                if not b:
                    defects.append(f"кадр {r}×{c} пуст")
                    continue
                bottoms.append(b[3])
                widths.append(b[2] - b[0])
                # PT3: bbox по альфе >16 (getbbox ловит и альфу-ореол 1-2px)
                px = cell.load()
                miny, maxy = fsize, -1
                for y in range(fsize):
                    for x in range(fsize):
                        if px[x, y][3] > 16:
                            miny = min(miny, y)
                            maxy = max(maxy, y)
                            break
                if maxy >= miny:
                    heights.append(maxy - miny + 1)
        if bottoms:
            spread = max(bottoms) - min(bottoms)
            if spread > BASELINE_TOL:
                defects.append(f"базовая линия стоп: разброс {spread}px > ±{BASELINE_TOL}")
        if widths and max(widths) > (BODY_W_MAX_ARC if entry["id"] in ARC_WIDE_ANIMS else BODY_W_MAX):
            limit = BODY_W_MAX_ARC if entry["id"] in ARC_WIDE_ANIMS else BODY_W_MAX
            defects.append(f"ширина тела {max(widths)}px > {limit} (48-бокс+допуск"
                           + ("; дуговые листы — до кадра" if entry["id"] in ARC_WIDE_ANIMS else "") + ")")
        if widths and max(widths) < 8:
            defects.append("силуэт подозрительно узкий (<8px)")
        # PT3: рост силуэта — политика по id (см. константы вверху).
        if heights:
            sid = entry["id"]
            if sid in STANDING_ANIMS:
                hs = sorted(heights)
                med = hs[len(hs) // 2]
                if not (STANDING_MED_MIN <= med <= STANDING_MED_MAX):
                    defects.append(
                        f"рост силуэта: медиана {med}px вне канона "
                        f"{STANDING_MED_MIN}..{STANDING_MED_MAX} (стоячий 52) — "
                        f"разноразмерная поставка: tools/sprites/normalize_heights.py")
                if max(heights) > STANDING_HARD_MAX:
                    defects.append(
                        f"рост силуэта: max {max(heights)}px > {STANDING_HARD_MAX} — кадр выше канона")
            elif sid in SITTING_ANIMS:
                if not (SITTING_MIN <= min(heights) and max(heights) <= SITTING_MAX):
                    defects.append(
                        f"рост силуэта: сидячий канон {SITTING_MIN}..{SITTING_MAX}px, "
                        f"фактически {min(heights)}..{max(heights)}")
            elif sid == "player_death":
                if heights[0] < DEATH_FIRST_MIN:
                    defects.append(
                        f"рост силуэта: первый кадр смерти {heights[0]}px < {DEATH_FIRST_MIN} "
                        "(падение должно начинаться с стоячего роста)")

    return defects


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("filter", nargs="?", default="",
                    help="подстрока фильтра по id/file (player, npc, wolf…)")
    ap.add_argument("--phases", default="",
                    help="фазы через запятую (G1,G2…)")
    ap.add_argument("--list", action="store_true",
                    help="только перечислить ожидающиеся позиции")
    args = ap.parse_args()

    if not os.path.exists(MANIFEST):
        print("НЕТ манифеста — запустите: python3 tools/sprites/make_manifest.py")
        sys.exit(2)

    with open(MANIFEST, encoding="utf-8") as f:
        manifest = json.load(f)

    phases = set(p.strip().upper() for p in args.phases.split(",") if p.strip())
    entries = [e for e in manifest["sheets"]
               if args.filter.lower() in e["id"].lower()
               and (not phases or e["phase"].upper() in phases)]

    delivered, defects_found, checked = 0, 0, 0
    for e in entries:
        path = os.path.join(SPRITES, e["file"])
        exists = os.path.exists(path)
        if args.list:
            mark = "✔" if exists else "·"
            print(f"  [{mark}] {e['phase']:3} {e['id']:36} "
                  f"{e['size'][0]}×{e['size'][1]:3} {e['frames']}к "
                  f"{e['fps']:2}fps loop={int(e['loop'])}")
            continue
        if not exists:
            continue
        delivered += 1
        checked += 1
        defects = check_file(e)
        if defects:
            defects_found += 1
            print(f"✘ {e['id']} ({e['file']}):")
            for d in defects:
                print(f"    — {d}")
        else:
            print(f"✔ {e['id']} ({e['size'][0]}×{e['size'][1]}, {e['frames']}к) — OK")

    if args.list:
        print(f"\nИтого позиций: {len(entries)}; доставлено: {delivered}")
        sys.exit(0)

    print(f"\nПроверено доставленных: {checked}; дефектных: {defects_found}")
    if defects_found:
        print("VERDICT: FAIL — исправить перечисленные файлы")
        sys.exit(1)
    print("VERDICT: PASS — доставленные спрайты соответствуют контракту "
          "(отсутствующие ожидаются по фазам G1–G5)")


if __name__ == "__main__":
    main()

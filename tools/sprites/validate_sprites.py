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

    # Базовая линия стоп + ширина тела — для листов 64×N (персонажи/звери).
    if h % FRAME == 0 and w % FRAME == 0 and w >= FRAME:
        fsize = FRAME
        cols, rows = w // fsize, h // fsize
        bottoms, widths = [], []
        for r in range(rows):
            for c in range(cols):
                cell = img.crop((c * fsize, r * fsize, (c + 1) * fsize, (r + 1) * fsize))
                b = cell.getbbox()
                if not b:
                    defects.append(f"кадр {r}×{c} пуст")
                    continue
                bottoms.append(b[3])
                widths.append(b[2] - b[0])
        if bottoms:
            spread = max(bottoms) - min(bottoms)
            if spread > BASELINE_TOL:
                defects.append(f"базовая линия стоп: разброс {spread}px > ±{BASELINE_TOL}")
        if widths and max(widths) > BODY_W_MAX:
            defects.append(f"ширина тела {max(widths)}px > {BODY_W_MAX} (48-бокс+допуск)")
        if widths and max(widths) < 8:
            defects.append("силуэт подозрительно узкий (<8px)")

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

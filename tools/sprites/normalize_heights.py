#!/usr/bin/env python3
# -*- coding: utf-8 -*-
r"""PT3 (25.10, плейтест-2-рестарт): нормализация роста силуэта листов игрока.

РЕПОРТ ВЛАДЕЛЬЦА: «статичный спрайт больше, чем спрайт бега — персонаж
как будто уменьшается. Спрайт медитации идеально лёг. Спрайт удара,
персонаж уменьшается».

ДИАГНОЗ (замер PIL по установленным листам): код рендерит ВСЕ анимации
в одном масштабе (Sprite2D Scale=1, кадр 64×64), но PNG разноразмерны:
idle/walk 52px (канон «стоячий рост 52»), meditate 48px (канон «сидячий»,
потому «идеально»), а листы из превью 7.png отрисованы мельче:
run 41-45, melee 37-41, cast 41-43, hit 36 — «проседание» на 15-31%.

ЛЕЧЕНИЕ: поднять силуэты run/melee/cast/hit к стоячему канону 52px.
Один масштаб на анимацию (factor = 52 / медиана высот кадров) — разброс
кадров (амплитуда бега) сохраняется; NEAREST (пиксель-арт); низ силуэта
прижат к базовой линии y=57 (канон чистых листов, валидатор ±2);
центр X кадра сохранён. idle/walk/meditate/death НЕ трогаем (канон).

    python3 tools/sprites/normalize_heights.py            # run/melee/cast/hit
    python3 tools/sprites/normalize_heights.py --dry-run  # только замер
    python3 tools/sprites/normalize_heights.py --target 52 --anim run

После: python3 tools/sprites/validate_sprites.py player
(чек высоты добавлен PT3 — регресс разноразмерности больше не проходит).
"""
import argparse
import os
import sys

from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
SHEETS = os.path.join(ROOT, "game", "resources", "sprites", "characters", "player")

FRAME = 64
BASELINE = 57          # низ силуэта (канон чистых листов владельца)
ALPHA_MIN = 16         # порог непрозрачности силуэта
DEFAULT_TARGET = 52    # стоячий рост (idle/walk — эталон)
DEFAULT_ANIMS = ("run", "melee", "cast", "hit")


def silhouette_bbox(cell):
    """bbox силуэта кадра по альфе > ALPHA_MIN (None — пустой кадр)."""
    px = cell.load()
    w, h = cell.size
    minx, miny, maxx, maxy = w, h, -1, -1
    for y in range(h):
        for x in range(w):
            if px[x, y][3] > ALPHA_MIN:
                if x < minx: minx = x
                if x > maxx: maxx = x
                if y < miny: miny = y
                if y > maxy: maxy = y
    if maxy < 0:
        return None
    return (minx, miny, maxx + 1, maxy + 1)


def median(vals):
    s = sorted(vals)
    n = len(s)
    return s[n // 2] if n % 2 == 1 else (s[n // 2 - 1] + s[n // 2]) / 2.0


def normalize_sheet(path, target, dry_run):
    img = Image.open(path).convert("RGBA")
    w, h = img.size
    frames = w // FRAME
    cells = [img.crop((i * FRAME, 0, (i + 1) * FRAME, FRAME)) for i in range(frames)]
    boxes = [silhouette_bbox(c) for c in cells]
    if any(b is None for b in boxes):
        print(f"  ✘ {os.path.basename(path)}: пустой кадр — СТОП (поставка битая)")
        return False
    heights = [b[3] - b[1] for b in boxes]
    med = median(heights)
    factor = target / med
    print(f"  {os.path.basename(path)}: кадров={frames}, высоты={heights}, "
          f"медиана={med:g} → фактор ×{factor:.4f} (цель {target}px)")

    if abs(factor - 1.0) < 1e-3:
        print("    уже в каноне — пропускаю")
        return True
    if dry_run:
        return True

    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    new_heights = []
    for i, (cell, b) in enumerate(zip(cells, boxes)):
        spr = cell.crop(b)
        nw = max(1, round(spr.size[0] * factor))
        nh = max(1, round(spr.size[1] * factor))
        spr = spr.resize((nw, nh), Image.NEAREST)
        # нецелый NEAREST-масштаб даёт пустые кромки — чистим до силуэта
        b2 = silhouette_bbox(spr)
        if b2 is not None:
            spr = spr.crop(b2)
            nw, nh = spr.size
        # позиция: центр X прежнего bbox, низ → BASELINE (ноги на земле).
        # Широкие позы (замах с дугой, размах бега) упираются в край канвы
        # УЖЕ в исходнике — X клэмпим в кадр (PIL клипит выступ; рост и
        # аспект не искажаем — теряются 1-3px декоративной дуги, а не тело).
        cx = (b[0] + b[2]) / 2.0
        x0 = int(round(cx - nw / 2.0))
        if nw >= FRAME:
            x0 = (FRAME - nw) // 2   # клип с обоих краёв поровну
        elif x0 < 0:
            x0 = 0
        elif x0 + nw > FRAME:
            x0 = FRAME - nw
        y1 = BASELINE
        y0 = y1 - nh
        if y0 < 0:
            print(f"    ✘ кадр {i}: высота {nh} > базовой линии {BASELINE} — СТОП")
            return False
        clipped = nw > FRAME or x0 != int(round(cx - nw / 2.0))
        out.paste(spr, (i * FRAME + x0, y0), spr)
        new_heights.append(nh)
        if clipped:
            print(f"    кадр {i}: X-клэмп (широкая поза {nw}px, сдвиг "
                  f"{x0 - int(round(cx - nw / 2.0)):+d}px, клип краёв дуги)")
    out.save(path)
    print(f"    OK: новые высоты {new_heights} (разброс бега сохранён), "
          f"низ y={BASELINE}")
    return True


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--anim", action="append", default=[],
                    help="только эти анимации (по умолчанию: run,melee,cast,hit)")
    ap.add_argument("--target", type=int, default=DEFAULT_TARGET,
                    help=f"целевой стоячий рост в px (default {DEFAULT_TARGET})")
    ap.add_argument("--dry-run", action="store_true", help="только замер, без записи")
    args = ap.parse_args()

    anims = tuple(args.anim) if args.anim else DEFAULT_ANIMS
    print(f"Нормализация роста: {', '.join(anims)} → {args.target}px "
          f"(базовая линия y={BASELINE}){' [DRY-RUN]' if args.dry_run else ''}")
    ok = True
    for anim in anims:
        path = os.path.join(SHEETS, f"player_{anim}.png")
        if not os.path.exists(path):
            print(f"  ✘ player_{anim}.png не найден")
            ok = False
            continue
        ok &= normalize_sheet(path, args.target, args.dry_run)
    if not ok:
        sys.exit(1)
    print("Готово. Проверка: python3 tools/sprites/validate_sprites.py player")


if __name__ == "__main__":
    main()

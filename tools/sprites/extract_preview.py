#!/usr/bin/env python3
# -*- coding: utf-8 -*-
r"""PT3 (25.10): экстрактор игровых полос из ПРЕВЬЮ-листов генератора.

Превью-шаблон владельца (1536×1024, «PLAYER_* | N FRAMES»): кадры лежат в
ЧЕКЕРБОРД-ячейках переменной ширины (bbox спрайта + margin), спрайты
сглажены (НЕ пиксель-апскейл), под ногами drop-shadow (затемнённый
чекерборд), по краям ячеек антиалиас-кайма 1-2px. Чистых листов для
части анимаций нет — извлекаем из превью:

  1) авто-детект y-зон чекерборда (полосы >50px) → ряды;
  2) x-ячейки ряда по чекерборд-покрытию (зазоры ≥4 колонок);
  3) чекерборд-вырез: 2 цвета ±TOL → alpha=0;
     тень-вырез: цвет = t×A или t×B, t∈[0.22..0.82] → alpha=0
     (drop-shadow затемняет чекерборд, сохраняя его пропорции RGB);
  4) inset INSET px от краёв ячейки (антиалиас-кайма);
  5) NEAREST-даунскейл ×SCALE (масштаб превью ≈3 при кадре 64);
  6) кадр 64×64: центр по медиане X спрайта, низ bbox → BASELINE=57
     (канон чистых листов владельца; при разбросе низов >2px ряд
     прижимается — амплитуда полёта ног сглажена, top-down канон);
  7) полоса 64N×64 в out-dir.

Пример (превью 7.png дозагрузки PT2 — run/melee/cast/hit):
    python3 tools/sprites/extract_preview.py ../upload/7.png \
        --anims run:6,melee:6,cast:4,hit:2 --out /tmp/batch64

Проверено на превью 7.png (PT2-дозагрузка: run/melee/cast/hit —
батч 6/6, валидатор PASS, ANIMQA PASS). Смежное превью 8.png имеет
другой лейаут панелей (run-зона не делится на 6 ячеек порогом зазоров)
— не шаблон; 6.png — вотермарка. Превью с другим чекербордом —
флаги --checker-a/--checker-b.
Дальше — по регламенту §2 SPRITE_DELIVERY.md: intake_batch --dir →
валидатор → ANIMQA. Цвета чекерборда (94..106, 106..120, 121..134)
покрывают превью 6/7/8 (дозагрузка PT2, «вариации разного качества»).
"""
import argparse
import os
import sys

from PIL import Image

FRAME = 64
BASELINE = 57   # низ bbox чистых листов владельца (валидатор ±2)
INSET = 2      # антиалиас-кайма ячейки
TOL = 10

DEFAULT_A = (105, 117, 132)
DEFAULT_B = (94, 106, 121)


def near(c, t, tol):
    return abs(c[0] - t[0]) <= tol and abs(c[1] - t[1]) <= tol \
        and abs(c[2] - t[2]) <= tol


def cut_shadow_color(r, g, b, A, B):
    """Тень = затемнённый чекерборд: пропорции A/B, яркость t∈[0.22..0.82]."""
    for t in (A, B):
        s, ts = r + g + b, t[0] + t[1] + t[2]
        if s == 0:
            return True
        f = s / ts
        if f < 0.22 or f > 0.82:
            continue
        if near((r / f, g / f, b / f), t, tol=14):
            return True
    return False


def is_checker_px(c, A, B):
    return near(c, A, TOL) or near(c, B, TOL)


def detect_rows(im, A, B, min_h=50):
    """y-полосы с чекерборд-покрытием → [(y0, y1)] рядов кадров."""
    w, h = im.size
    px = im.load()
    cov = []
    for y in range(0, h, 2):
        n = 0
        for x in range(0, w, 4):
            if is_checker_px(px[x, y][:3], A, B):
                n += 1
        cov.append((y, n))
    rows, y0 = [], None
    for (y, n) in cov + [(h, 0)]:
        if n >= 12 and y0 is None:
            y0 = y
        elif n < 12 and y0 is not None:
            if y - y0 >= min_h:
                rows.append((y0, y - 2))
            y0 = None
    return rows


def checker_xcells(im, y0, y1, A, B):
    """x-ячейки ряда: чекерборд-профиль → зазоры → интервалы."""
    w = im.size[0]
    px = im.load()
    prof = [0] * w
    for x in range(w):
        n = 0
        for y in range(y0, y1 + 1):
            if is_checker_px(px[x, y][:3], A, B):
                n += 1
        prof[x] = n
    gaps, x = [], 0
    while x < w:
        if prof[x] < 5:
            x0 = x
            while x < w and prof[x] < 5:
                x += 1
            if x - x0 >= 4:
                gaps.append((x0, x - 1))
        else:
            x += 1
    cells, prev = [], 0
    for (g0, g1) in gaps + [(w, w)]:
        if g0 - prev > 40:
            cells.append((prev, g0 - 1))
        prev = g1 + 1
    return cells


def cell_y_bounds(im, x0, x1, y_lo, y_hi, A, B):
    px = im.load()
    ys = []
    for y in range(y_lo, y_hi + 1):
        n = 0
        for x in range(x0, min(x1 + 1, x0 + 60)):
            if is_checker_px(px[x, y][:3], A, B):
                n += 1
        if n >= 20:
            ys.append(y)
    if not ys:
        return None
    return min(ys), max(ys)


def extract_cell(im, x0, x1, cy0, cy1, A, B):
    """Ячейка → RGBA-спрайт (чекерборд и тень вырезаны)."""
    cell = im.crop((x0 + INSET, cy0 + INSET, x1 + 1 - INSET, cy1 + 1 - INSET)) \
        .convert("RGBA")
    px = cell.load()
    w, h = cell.size
    for y in range(h):
        for x in range(w):
            r, g, b, _ = px[x, y]
            if is_checker_px((r, g, b), A, B) or cut_shadow_color(r, g, b, A, B):
                px[x, y] = (0, 0, 0, 0)
    return cell


def median_x(px, w, h):
    xs = [x for y in range(0, h, 2) for x in range(w) if px[x, y][3] > 40]
    if not xs:
        return w // 2
    xs.sort()
    return xs[len(xs) // 2]


def to_frame(spr, baseline=BASELINE):
    """Спрайт ≤64 → кадр 64×64 (центр по медиане X, низ к baseline)."""
    w, h = spr.size
    if w > FRAME:
        spr = spr.crop(((w - FRAME) // 2, 0, (w - FRAME) // 2 + FRAME, h))
        w = FRAME
    if h > FRAME:
        spr = spr.crop((0, 0, w, FRAME))
        h = FRAME
    px = spr.load()
    x = max(0, min(FRAME // 2 - median_x(px, w, h), FRAME - w))
    y = max(0, baseline - h)
    canvas = Image.new("RGBA", (FRAME, FRAME), (0, 0, 0, 0))
    canvas.paste(spr, (x, y), spr)
    return canvas


def extract_preview(src, out_dir, anims, scale, A, B):
    im = Image.open(src).convert("RGB")
    rows = detect_rows(im, A, B)
    print(f"превью: {src} {im.size}; рядов-зон: {len(rows)}: {rows}")
    if len(rows) != len(anims):
        print(f"ОШИБКА: зон {len(rows)} ≠ анимаций {len(anims)} {anims}; "
              f"неиспользуемые ряды помечайте '-:N' (например '-:4' для death)")
        return 1
    os.makedirs(out_dir, exist_ok=True)
    rc = 0
    for (y0, y1), spec in zip(rows, anims):
        anim, nframes = spec
        if anim == "-":
            print(f"\n[-] зона y[{y0}..{y1}] пропущена (чистый лист уже есть)")
            continue
        cells = checker_xcells(im, y0, y1, A, B)
        print(f"\n[{anim}] зона y[{y0}..{y1}]: {len(cells)} ячеек (ожидалось {nframes})")
        if len(cells) != nframes:
            print("  ЧИСЛО ЯЧЕЕК ≠ манифесту — СТОП")
            rc = 1
            continue
        placed = []
        for i, (x0, x1) in enumerate(cells):
            yb = cell_y_bounds(im, x0, x1, y0 - 12, y1 + 12, A, B)
            if not yb:
                print(f"  {anim}[{i}]: ячейка без y-границ — СТОП")
                rc = 1
                continue
            cell = extract_cell(im, x0, x1, yb[0], yb[1], A, B)
            b = cell.getbbox()
            if not b:
                print(f"  {anim}[{i}]: спрайт пуст — СТОП")
                rc = 1
                continue
            spr = cell.crop(b)
            spr = spr.resize((max(1, round(spr.size[0] / scale)),
                              max(1, round(spr.size[1] / scale))), Image.NEAREST)
            b2 = spr.getbbox()               # нецелый масштаб даёт пустые края —
            if b2:                           # чистим до фактического силуэта
                spr = spr.crop(b2)
            placed.append((spr, b))
            print(f"  {anim}[{i}] cell {x1 - x0 + 1}x{yb[1] - yb[0] + 1} "
                  f"sprite {b[2] - b[0]}x{b[3] - b[1]} → {spr.size[0]}x{spr.size[1]}")
        if not placed:
            continue
        bottoms = [to_frame(s).getbbox()[3] for s, _ in placed]
        spread = max(bottoms) - min(bottoms)
        if spread > 2:
            print(f"  разброс низов {spread}px > 2 — прижимаю ряд к y={BASELINE}")
        frames = [to_frame(s) for s, _ in placed]
        sheet = Image.new("RGBA", (FRAME * len(frames), FRAME), (0, 0, 0, 0))
        for i, f in enumerate(frames):
            sheet.paste(f, (i * FRAME, 0))
        out = os.path.join(out_dir, f"player_{anim}.png")
        sheet.save(out)
        print(f"  OK: {out} {sheet.size}")
    return rc


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("src", help="превью-PNG генератора")
    ap.add_argument("--anims", required=True,
                    help="ряды сверху вниз: run:6,melee:6,cast:4,hit:2")
    ap.add_argument("--out", required=True, help="каталог выходных полос 64×N")
    ap.add_argument("--scale", type=float, default=3.0,
                    help="масштаб превью (пикселей на кадр 64; default 3)")
    ap.add_argument("--checker-a", default="",
                    help="R,G,B первого цвета чекерборда (default 105,117,132)")
    ap.add_argument("--checker-b", default="",
                    help="R,G,B второго цвета чекерборда (default 94,106,121)")
    args = ap.parse_args()

    A = tuple(int(v) for v in args.checker_a.split(",")) if args.checker_a \
        else DEFAULT_A
    B = tuple(int(v) for v in args.checker_b.split(",")) if args.checker_b \
        else DEFAULT_B
    anims = []
    for part in args.anims.split(","):
        name, n = part.strip().split(":")
        anims.append((name.strip(), int(n)))
    sys.exit(extract_preview(args.src, args.out, anims, args.scale, A, B))


if __name__ == "__main__":
    main()

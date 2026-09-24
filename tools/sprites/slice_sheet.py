#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
G0 (2026-09-25): слайсер исходников генерации в игровые листы.

Реализация §6.2 SPRITE_PROMPTS_CHARACTERS (PIL-скелет плана):
  1) вырезание фона (цвет угла → прозрачность, порог 24/255);
  2) сетка cols×rows (равные ячейки; при неровной раскладке — авто-детект
     пустых колонок/строк-разделителей);
  3) кроп по содержимому (bbox) каждого кадра;
  4) NEAREST-даунскейл (целочисленный, без сглаживания);
  5) нормализация в 64×64: по центру X, выравнивание низа (базовая
     линия стоп — низ bbox к y=58 канона);
  6) сборка выходного листа: полоса 64N×64 (--grid 1) или D2-сетка
     64N×320 (--grid 5 — 5 рядов-направлений).

Пример (полоса player_walk из 1344×768):
    python3 tools/sprites/slice_sheet.py out/player_walk_v1.png \
        --frames 6 --out game/resources/sprites/characters/player/player_walk.png

D2-сетка (5 рядов × 6 кадров, 1024×1024 исходник):
    python3 tools/sprites/slice_sheet.py src/run_grid_v1.png \
        --frames 6 --grid 5 --out game/resources/sprites/characters/player/player_run.png

Проверка результата: tools/sprites/validate_sprites.py <animId>.
"""
import argparse
import sys

from PIL import Image, ImageDraw

FRAME = 64
BASELINE_Y = 58  # стопы (канон §4.1: голова y≈14, стопы y≈58)
BODY_MAX_W = 48  # тело в 48-боксе по X (поля — под замах/эффекты)
BG_THRESH = 24   # порог вырезания фона (§6.2)


def cut_background(img):
    """Цвет угла → прозрачность (генерация на чёрном/сером фоне)."""
    corner = img.getpixel((2, 2))
    px = img.load()
    w, h = img.size
    for y in range(h):
        for x in range(w):
            p = px[x, y]
            if (abs(p[0] - corner[0]) < BG_THRESH
                    and abs(p[1] - corner[1]) < BG_THRESH
                    and abs(p[2] - corner[2]) < BG_THRESH):
                px[x, y] = (0, 0, 0, 0)
    return img


def detect_grid(img, cols, rows):
    """Авто-детект пустых разделителей (генератор кладёт кадры неровно):
    ищем полностью прозрачные колонки/строки и подбираем ячейки.
    Fallback — равная сетка cols×rows."""
    if cols == 1 and rows == 1:
        return [img]
    w, h = img.size
    px = img.load()

    empty_cols = [all(px[x, y][3] == 0 for y in range(h)) for x in range(w)]
    empty_rows = [all(px[x, y][3] == 0 for x in range(w)) for y in range(h)]

    def segments(flags, count):
        """Разрезы по пустым линиям; если нашли count полос — ок, иначе None."""
        segs, start = [], None
        for i, e in enumerate(flags):
            if not e and start is None:
                start = i
            elif e and start is not None:
                segs.append((start, i))
                start = None
        if start is not None:
            segs.append((start, len(flags)))
        # сливаем микрополосы (<25% средней) с соседями
        if segs and len(segs) > count:
            avg = sum(b - a for a, b in segs) / len(segs)
            merged = [segs[0]]
            for a, b in segs[1:]:
                pa, pb = merged[-1]
                if (b - a) < avg * 0.25:
                    merged[-1] = (pa, b)
                else:
                    merged.append((a, b))
            segs = merged
        return segs if len(segs) == count else None

    col_seg = segments(empty_cols, cols) if cols > 1 else [(0, w)]
    row_seg = segments(empty_rows, rows) if rows > 1 else [(0, h)]
    if col_seg is None or row_seg is None:
        # Равная сетка (fallback — документированный риск §12-2).
        print(f"  [grid] равная сетка {cols}×{rows} (разделители не найдены)")
        cw, ch = w // cols, h // rows
        return [img.crop((c * cw, r * ch, (c + 1) * cw, (r + 1) * ch))
                for r in range(rows) for c in range(cols)]

    print(f"  [grid] авто-детект: колонки {col_seg}, ряды {row_seg}")
    return [img.crop((c0, r0, c1, r1))
            for (r0, r1) in row_seg for (c0, c1) in col_seg]


def normalize_cell(cell):
    """Кроп bbox → NEAREST-даунскейл до ≤64×64 → в canvas 64×64,
    центр X, низ bbox к BASELINE_Y."""
    bbox = cell.getbbox()
    if bbox:
        cell = cell.crop(bbox)
    w, h = cell.size
    if w > FRAME or h > FRAME:
        # Целочисленный NEAREST-даунскейл (§4 SPRITE_PIPELINE).
        k = max(1, (max(w, h) + FRAME - 1) // FRAME)
        # кратный даунскейл для чистоты пикселей:
        k = 2 ** (k - 1).bit_length() if k > 1 else 1
        k = max(1, min(k, w, h))
        cell = cell.resize((max(1, w // k), max(1, h // k)), Image.NEAREST)
        w, h = cell.size
    if w > FRAME:
        cell = cell.crop(((w - FRAME) // 2, 0, (w - FRAME) // 2 + FRAME, h))
        w = FRAME
    if h > FRAME:
        cell = cell.crop((0, 0, w, FRAME))  # верх обрезаем (ноги важнее)
        h = FRAME

    canvas = Image.new("RGBA", (FRAME, FRAME), (0, 0, 0, 0))
    x = (FRAME - w) // 2
    y = min(BASELINE_Y - h, FRAME - h)  # низ к BASELINE_Y (не ниже кадра)
    y = max(y, 0)
    canvas.paste(cell, (x, y), cell)
    return canvas


def slice_sheet(src, frames, grid_rows, out_path, no_bg_cut):
    img = Image.open(src).convert("RGBA")
    print(f"  источник: {src} {img.size}, кадров={frames}, сетка={grid_rows}")

    if not no_bg_cut:
        img = cut_background(img)

    cells = detect_grid(img, frames, grid_rows)
    if len(cells) != frames * grid_rows:
        print(f"  ОШИБКА: нарезано {len(cells)} ячеек, ожидалось "
              f"{frames * grid_rows} ({frames}×{grid_rows})")
        return 1

    frames_norm = [normalize_cell(c) for c in cells]

    # Сборка листа: grid_rows рядов × frames кадров.
    sheet = Image.new("RGBA", (FRAME * frames, FRAME * grid_rows), (0, 0, 0, 0))
    for i, f in enumerate(frames_norm):
        r, c = divmod(i, frames)
        sheet.paste(f, (c * FRAME, r * FRAME))

    sheet.save(out_path)
    print(f"  OK: {out_path} {sheet.size} "
          f"(полоса {frames}×64" + (f", {grid_rows} рядов" if grid_rows > 1 else "") + ")")

    # Диагностика базовой линии (чек-лист §10.1): низ bbox каждого кадра.
    bottoms = []
    for i, f in enumerate(frames_norm):
        b = f.getbbox()
        bottoms.append(b[3] if b else 0)
    spread = max(bottoms) - min(bottoms)
    print(f"  базовая линия стоп: min={min(bottoms)}, max={max(bottoms)}, "
          f"разброс={spread}px ({'OK ±2' if spread <= 2 else 'ВНИМАНИЕ: >2 — '
          'выровнять вручную'})")
    if spread > 2:
        print("  РЕКОМЕНДАЦИЯ: перегенерировать или поправить кадры "
              "(вал. чек validate_sprites.py)")

    # Диагностика ширины тела (§10.1: ≤48 в X).
    widths = []
    for f in frames_norm:
        b = f.getbbox()
        widths.append(b[2] - b[0] if b else 0)
    if max(widths) > FRAME:
        print(f"  ВНИМАНИЕ: ширина кадра > {FRAME}: {max(widths)}px")
    return 0


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("src", help="исходный PNG генерации (1344×768 / 1024×1024)")
    ap.add_argument("--frames", type=int, required=True,
                    help="кадров в ряду (N)")
    ap.add_argument("--grid", type=int, default=1, choices=[1, 5],
                    help="рядов-направлений: 1 = D1-полоса, 5 = D2-сетка")
    ap.add_argument("--out", required=True, help="выходной лист PNG")
    ap.add_argument("--no-bg-cut", action="store_true",
                    help="не вырезать фон (уже прозрачный)")
    args = ap.parse_args()
    sys.exit(slice_sheet(args.src, args.frames, args.grid, args.out,
                         args.no_bg_cut))


if __name__ == "__main__":
    main()

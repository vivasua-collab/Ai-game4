#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
G0 (2026-09-25): генератор QA-плейсхолдеров пайплайна спрайтов.

Создаёт в game/resources/sprites/_qa/ ДВА тестовых листа (НЕ боевые
ассеты — только для GODOT_ANIMQA_DEBUG и ручной проверки нарезки):
  - qa_walk_strip.png  — полоса 6 кадров 384×64 (D1-формат): силуэт
    гуманоида со сдвигом ног по кадрам + номер кадра (проверка
    Frame-индексации рантайма);
  - qa_grid_5x6.png    — D2-сетка 384×320 (5 рядов × 6 кадров): ряды
    различаются цветом пояса (front/¾/side/¾/back) — проверка
    рядовой адресации FrameAt(progress, row).

Пиксель-арт, NEAREST-чистые края (без полупрозрачности).
"""
import os

from PIL import Image, ImageDraw

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
QA = os.path.join(ROOT, "game", "resources", "sprites", "_qa")

F = 64
FRAMES = 6
ROWS = 5

# Палитра-якоря игрока (§4.5 плана): роба фиолет, кожа, волосы, сапоги.
ROBE = (77, 51, 128, 255)
SKIN = (230, 191, 153, 255)
HAIR = (38, 26, 13, 255)
BOOT = (26, 20, 13, 255)
BELT = (102, 77, 26, 255)
ROW_TINTS = [(255, 90, 31, 255), (242, 225, 64, 255),
             (80, 200, 120, 255), (80, 160, 255, 255), (200, 120, 255, 255)]


def draw_humanoid(d, cx, legs_phase, belt):
    """Силуэт игрока (анатомия процедурного рецепта 48×48 в 64-кадре)."""
    # голова + волосы
    d.ellipse((cx - 6, 14, cx + 6, 26), fill=SKIN)
    d.ellipse((cx - 5, 12, cx + 5, 22), fill=HAIR)
    # торс-роба (трапеция)
    d.rectangle((cx - 5, 26, cx + 5, 40), fill=ROBE)
    d.rectangle((cx - 7, 36, cx + 7, 44), fill=ROBE)
    # рукава + кисти (якорь правой кисти +6 от центра)
    d.rectangle((cx - 8, 27, cx - 5, 37), fill=ROBE)
    d.rectangle((cx + 5, 27, cx + 8, 37), fill=ROBE)
    d.ellipse((cx - 7, 34, cx - 4, 37), fill=SKIN)
    d.ellipse((cx + 4, 34, cx + 7, 37), fill=SKIN)
    # пояс (дифференциатор ряда в сетке)
    d.rectangle((cx - 6, 34, cx + 6, 36), fill=belt)
    # ноги — фаза шага
    swing = [0, 2, 3, 0, -2, -3][legs_phase % FRAMES]
    d.rectangle((cx - 4 + swing, 44, cx - 1 + swing, 52), fill=(51, 38, 26, 255))
    d.rectangle((cx + 1 - swing, 44, cx + 4 - swing, 52), fill=(51, 38, 26, 255))
    # сапоги (базовая линия стоп: низ = 58 — канон §4.1)
    d.rectangle((cx - 5 + swing, 52, cx - 1 + swing, 58), fill=BOOT)
    d.rectangle((cx + 1 - swing, 52, cx + 5 - swing, 58), fill=BOOT)


def draw_badge(d, x0, y0, num, tint):
    """Номер кадра (2×4 px цифры) — визуальная проверка индексации."""
    digits = {
        0: ["111", "101", "101", "111"],
        1: ["010", "010", "010", "010"],
        2: ["111", "001", "111", "100"],
        3: ["111", "001", "111", "001"],
        4: ["101", "101", "111", "001"],
        5: ["111", "100", "111", "001"],
        6: ["111", "100", "111", "101"],
        7: ["111", "001", "001", "001"],
        8: ["111", "101", "111", "101"],
        9: ["111", "101", "111", "001"],
    }
    s = num % 10
    for ry, rowpat in enumerate(digits[s]):
        for rx, ch in enumerate(rowpat):
            if ch == "1":
                d.point((x0 + rx, y0 + ry), fill=tint)


def main():
    os.makedirs(QA, exist_ok=True)

    # === 1. D1-полоса: 6 кадров 384×64 ================================
    strip = Image.new("RGBA", (F * FRAMES, F), (0, 0, 0, 0))
    d = ImageDraw.Draw(strip)
    for i in range(FRAMES):
        cx = i * F + F // 2
        draw_humanoid(d, cx, i, BELT)
        draw_badge(d, i * F + 4, 4, i + 1, (255, 255, 255, 255))
    strip.save(os.path.join(QA, "qa_walk_strip.png"))
    print(f"OK: qa_walk_strip.png {strip.size} (6 кадров D1)")

    # === 2. D2-сетка: 5 рядов × 6 кадров 384×320 ======================
    grid = Image.new("RGBA", (F * FRAMES, F * ROWS), (0, 0, 0, 0))
    d = ImageDraw.Draw(grid)
    for r in range(ROWS):
        tint = ROW_TINTS[r]
        for i in range(FRAMES):
            cx = i * F + F // 2
            cy_off = r * F
            # сдвиг по вертикали ряда — различимость рядов помимо пояса
            d.rectangle((i * F + 2, cy_off + 2, i * F + 6, cy_off + 6), fill=tint)
            draw_humanoid(d, cx, i, tint)
            draw_badge(d, i * F + 4, cy_off + 4, i + 1, tint)
    grid.save(os.path.join(QA, "qa_grid_5x6.png"))
    print(f"OK: qa_grid_5x6.png {grid.size} (5×6, D2-ряды)")

    # Самопроверка базовой линии: низ сапог == 58 в каждом кадре.
    for name, img in [("strip", strip), ("grid", grid)]:
        w, h = img.size
        for r in range(h // F):
            bottoms = []
            for c in range(w // F):
                cell = img.crop((c * F, r * F, (c + 1) * F, (r + 1) * F))
                b = cell.getbbox()
                bottoms.append(b[3] if b else 0)
            spread = max(bottoms) - min(bottoms)
            status = "OK" if spread <= 2 else f"FAIL (spread={spread})"
            print(f"  baseline {name} row{r}: {status}")


if __name__ == "__main__":
    main()

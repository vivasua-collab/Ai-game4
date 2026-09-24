#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
G0 (2026-09-25): генератор манифеста доставки спрайтов.

Единый машиночитаемый контракт: какие PNG ожидаются, где лежат, сколько
кадров, fps, loop. Читают:
  - tools/sprites/validate_sprites.py (валидатор доставки);
  - человек (сверка объёмов/фаз);
  - рантайм НЕ читает (fps/loop — в реестре SpriteSheetCache.cs,
    кадры — из размеров PNG; манифест — контракт ДОСТАВКИ, не рантайма).

Запуск (из корня репо):
    python3 tools/sprites/make_manifest.py
Выход: game/resources/sprites/sprites_manifest.json

Таблицы соответствуют плану:
  SPRITE_ANIMATION_PLAN.md §5.1 (игрок 13 листов), §6 (NPC/звери),
  §7/§8 (оружие/броня), фазы G1–G5.
"""
import json
import os

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
OUT = os.path.join(ROOT, "game", "resources", "sprites", "sprites_manifest.json")

FRAME = 64  # рабочий кадр (SPRITE_ANIMATION_PLAN §4.1)


def player_sheets():
    """§5.1: 13 листов / 52–56 кадров, этап D1 (направление «вправо»)."""
    rows = [
        # (anim, frames, fps, loop, phase)
        ("idle",         4,  7, True,  "G1"),
        ("walk",         6, 11, True,  "G2"),
        ("melee",        6, 15, False, "G2"),  # 6к@15 = 0.4с ≈ замах 0.42
        ("cast",         4, 10, True,  "G3"),
        ("meditate",     6,  5, True,  "G3"),  # поза лотоса
        ("hit",          2, 12, False, "G3"),
        ("death",        4, 12, False, "G3"),  # hold последнего кадра
        ("run",          6, 14, True,  "G4"),
        ("bow",          3, 12, False, "G4"),
        ("guard",        2,  8, True,  "G4"),
        ("dash",         3, 15, False, "G5"),
        ("sleep",        2,  2, True,  "G5"),
        ("breakthrough", 4, 15, False, "G5"),
    ]
    return [
        {
            "id": f"player_{a}", "file": f"characters/player/player_{a}.png",
            "frames": n, "fps": fps, "loop": loop, "phase": phase,
            "size": [FRAME * n, FRAME],
        }
        for (a, n, fps, loop, phase) in rows
    ]


def npc_sheets():
    """§6.1: базовое тело + 8 перекрасок ролей (idle/walk; melee/cast/hit/
    death — в базовом цвете). Ключи резолва рантайма:
    npc_{role}_{anim} → npc_base_{anim} → процедурный fallback."""
    base_anims = [
        ("idle",   4,  6, True,  "G3"),
        ("walk",   6, 10, True,  "G3"),
        ("melee",  4, 15, False, "G3"),
        ("cast",   3, 10, True,  "G3"),
        ("hit",    2, 12, False, "G3"),
        ("death",  3, 12, False, "G3"),
    ]
    roles = ["guard", "merchant", "elder", "cultivator",
             "enemy", "passerby", "disciple", "monster"]
    sheets = []
    for (a, n, fps, loop, phase) in base_anims:
        sheets.append({
            "id": f"npc_base_{a}", "file": f"characters/npc/npc_base_{a}.png",
            "frames": n, "fps": fps, "loop": loop, "phase": phase,
            "size": [FRAME * n, FRAME],
        })
    # Роли: idle+walk (перекраски edit-каналом — §6.1 «16 эдитов»).
    for role in roles:
        for (a, n, fps, loop, phase) in [("idle", 4, 6, True, "G3"),
                                         ("walk", 6, 10, True, "G3")]:
            sheets.append({
                "id": f"npc_{role}_{a}", "file": f"characters/npc/npc_{role}_{a}.png",
                "frames": n, "fps": fps, "loop": loop, "phase": phase,
                "size": [FRAME * n, FRAME],
            })
    return sheets


def animal_sheets():
    """§6.2: 3 спавнящихся вида. Волк — враг №1 (полный боевой комплект);
    deer/rabbit — мирные (без attack)."""
    wolf = [("idle", 2), ("walk", 4), ("run", 4), ("attack", 2), ("death", 1)]
    deer = [("idle", 2), ("walk", 4), ("run", 4), ("death", 1)]
    rabbit = [("idle", 2), ("walk", 4), ("run", 4), ("death", 1)]
    fps = {"idle": 6, "walk": 10, "run": 14, "attack": 15, "death": 12}
    loop = {"idle": True, "walk": True, "run": True,
            "attack": False, "death": False}
    phase_map = {"idle": "G2", "walk": "G2", "run": "G3",
                 "attack": "G3", "death": "G3"}
    out = []
    for species, anims in [("wolf", wolf), ("deer", deer), ("rabbit", rabbit)]:
        for (a, n) in anims:
            out.append({
                "id": f"animal_{species}_{a}",
                "file": f"animals/animal_{species}_{a}.png",
                "frames": n, "fps": fps[a], "loop": loop[a],
                "phase": phase_map[a], "size": [FRAME * n, FRAME],
            })
    return out


def corpse_sheets():
    """§6.3: лежачие позы, 1 кадр 64×32 (G5; до того — канон-маркер)."""
    return [
        {"id": "corpse_human", "file": "corpses/corpse_human.png",
         "frames": 1, "fps": 1, "loop": False, "phase": "G5",
         "size": [64, 32]},
        {"id": "corpse_beast", "file": "corpses/corpse_beast.png",
         "frames": 1, "fps": 1, "loop": False, "phase": "G5",
         "size": [64, 32]},
    ]


def weapon_pngs():
    """§7: PNG-миграция по тем же ключам class|tier (rarity НЕ в имени —
    золотая обводка Legendary/Mythic остаётся кодом). 7 классов × 5 тиров.
    Icon 32×32 (предметный вид, вертикально) + Hand 48×48 (диагональ 45°,
    рукоять G=(16,36))."""
    classes = ["dagger", "sword", "axe", "spear", "greatsword", "bow", "staff"]
    out = []
    for c in classes:
        for tier in range(1, 6):
            out.append({
                "id": f"weapon_icon_{c}_t{tier}",
                "file": f"equipment/icons/weapon_{c}_{tier}.png",
                "frames": 1, "fps": 1, "loop": False,
                "phase": "G1" if tier == 1 else "G4", "size": [32, 32],
            })
            out.append({
                "id": f"weapon_hand_{c}_t{tier}",
                "file": f"equipment/equipped/weapon_hand_{c}_{tier}.png",
                "frames": 1, "fps": 1, "loop": False,
                "phase": "G4", "size": [48, 48],
            })
    return out


def armor_pngs():
    """§8.3 (гибрид): torso — полно-кадровые armored-варианты листов
    (idle/walk/melee × 5 тиров); head/feet/belt — стикеры; legs/hands —
    тинт кодом (0 PNG). G4."""
    out = []
    for tier in range(1, 6):
        for anim in ["idle", "walk", "melee"]:
            n = {"idle": 4, "walk": 6, "melee": 6}[anim]
            fps = {"idle": 7, "walk": 11, "melee": 15}[anim]
            loop = anim != "melee"
            out.append({
                "id": f"player_{anim}_armor_t{tier}",
                "file": f"characters/player/player_{anim}_armor_{tier}.png",
                "frames": n, "fps": fps, "loop": loop, "phase": "G4",
                "size": [FRAME * n, FRAME],
            })
    # Стикеры: head 24×24 (3 формы × 5 тиров), feet 24×16 (3 позы × 5),
    # belt 32×12 (5).
    for tier in range(1, 6):
        for form in ["helm", "hood", "circlet"]:
            out.append({
                "id": f"armor_head_{form}_t{tier}",
                "file": f"equipment/equipped/armor_head_{form}_{tier}.png",
                "frames": 1, "fps": 1, "loop": False, "phase": "G4",
                "size": [24, 24],
            })
        for pose in ["step", "roll", "stand"]:
            out.append({
                "id": f"armor_feet_{pose}_t{tier}",
                "file": f"equipment/equipped/armor_feet_{pose}_{tier}.png",
                "frames": 1, "fps": 1, "loop": False, "phase": "G4",
                "size": [24, 16],
            })
        out.append({
            "id": f"armor_belt_t{tier}",
            "file": f"equipment/equipped/armor_belt_{tier}.png",
            "frames": 1, "fps": 1, "loop": False, "phase": "G4",
            "size": [32, 12],
        })
        # Иконки брони (UI — оживление CreateEquipmentIcon, задача I-8).
        for slot in ["head", "torso", "arms", "legs", "feet", "belt"]:
            out.append({
                "id": f"armor_icon_{slot}_t{tier}",
                "file": f"equipment/icons/armor_{slot}_{tier}.png",
                "frames": 1, "fps": 1, "loop": False, "phase": "G4",
                "size": [32, 32],
            })
    return out


def item_icons():
    """G1: иконки предметов (камни Ци 5 размеров, пилюли, материалы)."""
    out = []
    for size in ["xs", "s", "m", "l", "xl"]:
        out.append({
            "id": f"qistone_{size}",
            "file": f"items/qistone_{size}.png",
            "frames": 1, "fps": 1, "loop": False, "phase": "G1",
            "size": [32, 32],
        })
    for pill in ["heal_s", "heal_m", "heal_l", "qi_s", "qi_m", "qi_l"]:
        out.append({
            "id": f"pill_{pill}",
            "file": f"items/pill_{pill}.png",
            "frames": 1, "fps": 1, "loop": False, "phase": "G1",
            "size": [32, 32],
        })
    for mat in ["iron", "leather", "cloth", "wood", "bone",
                "steel", "spirit_stone", "star_metal", "dragon_bone", "void_matter"]:
        out.append({
            "id": f"material_{mat}",
            "file": f"items/material_{mat}.png",
            "frames": 1, "fps": 1, "loop": False, "phase": "G1",
            "size": [32, 32],
        })
    return out


def main():
    sheets = (player_sheets() + npc_sheets() + animal_sheets() + corpse_sheets()
              + weapon_pngs() + armor_pngs() + item_icons())
    manifest = {
        "version": 1,
        "generated": "2026-09-25",
        "frame": FRAME,
        "source": "SPRITE_ANIMATION_PLAN.md §5–§9; SPRITE_PROMPTS_CHARACTERS.md §3–§5",
        "conventions": {
            "sheet": "полоса 1 анимация × 1 направление × N кадров (64N×64); "
                     "D2-сетки 5 рядов-направлений (64N×320, ряд: front/¾/side/¾/back)",
            "directions": "D1: лист «вправо», зеркало — FlipH кодом; "
                          "D2: 5 рядов × FlipH = 8 направлений",
            "anchors": "тело в 48×48-боксе по X; правая кисть (+6,+6) от центра; "
                       "базовая линия стоп ±2px; рукоять weapon_hand G=(16,36)",
            "filter": "NEAREST (никакого сглаживания)",
            "runtime": "рентайм не читает манифест: кадры = W/64, fps/loop = "
                       "реестр SpriteSheetCache.cs; отсутствие файла = "
                       "процедурный fallback",
            "phases": "G1 статика → G2 игрок/звери → G3 NPC → G4 экипировка → G5 полировка",
        },
        "total_files": len(sheets),
        "sheets": sheets,
    }
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"OK: {OUT}")
    print(f"     {len(sheets)} позиций")
    by_phase = {}
    for s in sheets:
        by_phase[s["phase"]] = by_phase.get(s["phase"], 0) + 1
    for ph in sorted(by_phase):
        print(f"     {ph}: {by_phase[ph]}")


if __name__ == "__main__":
    main()

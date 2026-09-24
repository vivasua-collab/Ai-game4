#!/usr/bin/env python3
# -*- coding: utf-8 -*-
r"""
G0.2 (приёмка батчей): пакетный приёмник zip-батча генераций спрайтов.

Автоматизирует §2 SPRITE_DELIVERY.md для пачки файлов:
  1) распаковка zip → staging (защита от zip-slip);
  2) инвентаризация PNG (размер/альфа/кадры);
  3) маппинг имён на манифест по ДВУМ пространствам имён:
     - file-stem (README §5 — как называются генерации: weapon_dagger_1.png),
     - id манифеста (weapon_icon_dagger_t1 — если генератор звал по id);
     поддерживаются суффиксы версий (_v2/_final/alt…), числовые хвосты,
     t-формы тиров (weapon_dagger_t1 ↔ weapon_dagger_1), префиксы
     категорий (wolf_walk → animal_wolf_walk), ручной override --map;
     дубли stem (armor_belt_1: иконка 32×32 + стикер 32×12) —
     дизамбигуация по геометрии картинки;
  4) выбор лучшего варианта при нескольких файлах на позицию
     (скоринг: точная геометрия > игровой формат > альфа > версия);
  5) продакшн по маршруту:
     - DIRECT   — геометрия == манифест → копия как есть;
     - CONFORM  — игровой формат 64×N, но кадров ≠ манифест → доводка
                  (обрезка лишних / дубль последнего) → копия;
     - SLICE    — сырой лист генерации (1344×768 и т.п.) → slice_sheet.py
                  (вырез фона, сетка, bbox, NEAREST, базовая линия y=58);
     - FIT      — иконки/предметы/трупы (items/, corpses/, equipment/icons/)
                  → bbox-кроп + NEAREST-вписать в размер манифеста;
     - REJECT   — equipment/equipped/ с чужой геометрией (якорь рукояти
                  G=(16,36) — только ручная подгонка) и прочие не-форматы;
  6) валидация продуктов чеками validate_sprites.check_file
     (в --dry-run — на зеркале staging/out, без касания game/);
  7) установка в game/resources/sprites/ (перезапись только с --force);
  8) отчёт: инвентаризация / маппинг / варианты / маршруты / дефекты /
     покрытие манифеста по фазам G1–G5.

Примеры:
    python3 tools/sprites/intake_batch.py ../upload/AiGame4_sprite_batch1_iter2.zip --dry-run
    python3 tools/sprites/intake_batch.py ../upload/AiGame4_sprite_batch1_iter2.zip
    python3 tools/sprites/intake_batch.py ../upload/batch.zip --map hero_a=player_idle
    python3 tools/sprites/intake_batch.py --dir out/          # уже распаковано

Выход: 0 — доставленное валидно; 1 — есть дефектные продукты;
2 — ошибка входа (нет zip/манифеста).
"""
import argparse
import json
import os
import re
import shutil
import sys
import time
import zipfile

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import validate_sprites as vs           # чеки приёмки (check_file)
from slice_sheet import slice_sheet     # продакшн сырых листов

ROOT = os.path.join(HERE, "..", "..")
SPRITES = os.path.join(ROOT, "game", "resources", "sprites")
MANIFEST = os.path.join(SPRITES, "sprites_manifest.json")
FRAME = 64

# Префиксы категорий: файл "wolf_walk" → манифест "animal_wolf_walk".
CATEGORY_PREFIXES = ["animal_", "npc_", "weapon_hand_", "weapon_", "armor_"]

# Каталоги, где допустим авто-FIT (нет якорных точек).
FIT_DIRS = ("items/", "corpses/", "equipment/icons/")


# ── Индексы манифеста ─────────────────────────────────────────────────

def build_indexes(manifest):
    """by_id: id → [entry]; by_stem: file-stem (README-имя) → [entry]."""
    by_id, by_stem = {}, {}
    for e in manifest["sheets"]:
        by_id.setdefault(e["id"], []).append(e)
        stem = os.path.splitext(os.path.basename(e["file"]))[0].lower()
        by_stem.setdefault(stem, []).append(e)
    return by_id, by_stem


def norm_stem(path):
    r"""player_Walk v2.png → 'player_walk_v2' (lower, [-\s]→_, без повторов)."""
    stem = os.path.splitext(os.path.basename(path))[0]
    s = re.sub(r"[\s\-]+", "_", stem.strip())
    s = re.sub(r"_+", "_", s).strip("_").lower()
    return s


def tier_forms(stem):
    """weapon_dagger_t1 ↔ weapon_dagger_1 (id- vs README-тир)."""
    out = [stem]
    m = re.match(r"^(.*?)_?t(\d+)$", stem)
    if m and m.group(1):
        out.append(f"{m.group(1)}_{m.group(2)}")
    m = re.match(r"^(.*?)_(\d+)$", stem)
    if m and m.group(1):
        out.append(f"{m.group(1)}_t{m.group(2)}")
    return out


def match_candidates(stem):
    """Цепочка кандидатов: точный → срез хвостовых токенов → t-формы →
    числовой хвост (player_walk2)."""
    toks = stem.split("_")
    cands = []
    for cut in range(len(toks), 0, -1):
        cands.extend(tier_forms("_".join(toks[:cut])))
    m = re.match(r"^(.*?)_?(\d+)$", stem)
    if m and m.group(1):
        cands.extend(tier_forms(m.group(1)))
    return cands


def resolve_entries(stem, by_id, by_stem):
    """stem → дедуплицированный список entries (0/1/несколько)."""
    hits, seen = [], set()

    def try_(key):
        for e in by_stem.get(key, []) + by_id.get(key, []):
            if e["file"] not in seen:
                seen.add(e["file"])
                hits.append(e)

    for cand in match_candidates(stem):
        try_(cand)
    if not hits:                                   # префиксы категорий
        for p in CATEGORY_PREFIXES:
            for cand in match_candidates(stem):
                try_(p + cand)
            if hits:
                break
    return hits


def disambiguate(entries, img_size):
    """Дубли stem (armor_belt_1: иконка+стикер) → выбор по геометрии."""
    if len(entries) == 1:
        return entries[0], None
    if img_size is None:
        return None, "нет геометрии для дизамбигуации"
    w, h = img_size
    best, note = None, None
    for e in entries:
        ew, eh = e["size"]
        if (w, h) == (ew, eh):
            return e, f"точная геометрия {ew}×{eh}"
    best_a, best_e = None, None
    for e in entries:
        ew, eh = e["size"]
        a = abs((w / h) - (ew / eh))
        if best_a is None or a < best_a:
            best_a, best_e = a, e
    return best_e, f"аспект ближе к {best_e['size'][0]}×{best_e['size'][1]}"


# ── Геометрия/скоринг ─────────────────────────────────────────────────

def alpha_stats(img):
    """(min_alpha, доля прозрачных) по альфа-каналу."""
    hist = img.getchannel("A").histogram()
    total = sum(hist)
    trans = sum(hist[:8])
    mn = min((a for a, n in enumerate(hist) if n), default=255)
    return mn, (trans / total if total else 0.0)


def score_variant(path, entry):
    """Приоритет варианта: точная геометрия > игровой формат > альфа > версия."""
    try:
        img = Image.open(path)
        w, h = img.size
    except Exception:
        return -100
    score = 0
    if (w, h) == tuple(entry["size"]):
        score += 10                       # game-ready как есть
    elif h == FRAME and w % FRAME == 0 and w // FRAME == entry["frames"]:
        score += 6                        # игровая полоса, кадров в манифест
    if img.mode == "RGBA":
        mn, frac = alpha_stats(img.convert("RGBA"))
        if mn == 0 and frac > 0.05:
            score += 3                    # фон уже вырезан
    m = re.search(r"[_\s\-\.\(]?v(\d+)", os.path.basename(path), re.I)
    if m:
        score += min(int(m.group(1)), 9)  # поздние итерации чуть приоритетнее
    return score


def cut_background_dynamic(img):
    """Вырез фона по цвету угла (для FIT-иконок из непрозрачной генерации)."""
    corner = img.getpixel((2, 2))
    px = img.load()
    w, h = img.size
    for y in range(h):
        for x in range(w):
            p = px[x, y]
            if (abs(p[0] - corner[0]) < 24 and abs(p[1] - corner[1]) < 24
                    and abs(p[2] - corner[2]) < 24):
                px[x, y] = (0, 0, 0, 0)
    return img


def fit_icon(img, target):
    """bbox-кроп → NEAREST-вписать → canvas target (центр)."""
    img = img.convert("RGBA")
    if alpha_stats(img)[0] != 0:
        img = cut_background_dynamic(img)
    bbox = img.getbbox()
    if bbox:
        img = img.crop(bbox)
    w, h = img.size
    tw, th = target
    k = max(1, (max(w, h) + max(tw, th) - 1) // max(tw, th))
    k = max(1, min(k, w, h))
    img = img.resize((max(1, w // k), max(1, h // k)), Image.NEAREST)
    w, h = img.size
    if w > tw:
        img = img.crop(((w - tw) // 2, 0, (w - tw) // 2 + tw, h))
    if h > th:
        img = img.crop((0, 0, w, th))
    canvas = Image.new("RGBA", (tw, th), (0, 0, 0, 0))
    canvas.paste(img, ((tw - img.size[0]) // 2, (th - img.size[1]) // 2), img)
    return canvas


def conform_frames(img, frames):
    """Игровая полоса 64×N с неверным числом кадров → доводка до N."""
    w, h = img.size
    actual = w // FRAME
    if actual == frames:
        return img
    if actual > frames:                      # лишние справа — срезать
        img = img.crop((0, 0, frames * FRAME, h))
    else:                                    # не хватает — дублировать последний
        canvas = Image.new("RGBA", (frames * FRAME, h), (0, 0, 0, 0))
        canvas.paste(img, (0, 0))
        last = img.crop(((actual - 1) * FRAME, 0, actual * FRAME, h))
        for i in range(actual, frames):
            canvas.paste(last, (i * FRAME, 0))
        img = canvas
    return img


# ── Маршрутизация и продакшн ──────────────────────────────────────────

def route(entry, src):
    """Маршрутизация: (kind, arg); kind ∈ DIRECT/CONFORM/SLICE/FIT/REJECT."""
    ew, eh = entry["size"]
    rel = entry["file"]
    img = Image.open(src)
    w, h = img.size

    if (w, h) == (ew, eh):
        return "DIRECT", None

    if rel.startswith(("characters/", "animals/")):
        if eh % FRAME == 0 and h == FRAME and w % FRAME == 0:
            return "CONFORM", None                       # кадров ≠ манифест
        rows = eh // FRAME
        no_bg = False
        if img.mode == "RGBA":
            mn, frac = alpha_stats(img.convert("RGBA"))
            no_bg = (mn == 0 and frac > 0.05)
        return "SLICE", (entry["frames"], rows, no_bg)

    if rel.startswith(FIT_DIRS):
        return "FIT", (ew, eh)

    return "REJECT", (f"геометрия {w}×{h} ≠ манифест {ew}×{eh}; каталог требует "
                      f"ручной подгонки (якорь/канон)")


def produce(kind, arg, src, entry, out_path):
    """Готовит итоговый PNG по маршруту. True = успех."""
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    if kind in ("DIRECT", "CONFORM"):
        img = Image.open(src).convert("RGBA")
        if kind == "CONFORM":
            img = conform_frames(img, entry["frames"])
        img.save(out_path)
        return True
    if kind == "SLICE":
        frames, rows, no_bg = arg
        return slice_sheet(src, frames, rows, out_path, no_bg) == 0
    if kind == "FIT":
        tw, th = arg
        fit_icon(Image.open(src), (tw, th)).save(out_path)
        return True
    return False


def extract_zip(zip_path, staging):
    """Распаковка с защитой от zip-slip; возвращает список PNG."""
    out = []
    with zipfile.ZipFile(zip_path) as z:
        for info in z.infolist():
            name = info.filename
            if name.startswith("__MACOSX") or name.endswith("/"):
                continue
            safe = os.path.normpath(name).lstrip("/\\")
            if ".." in safe.split(os.sep):
                print(f"  [zip] ПРОПУСК (path traversal): {name}")
                continue
            dest = os.path.join(staging, safe)
            os.makedirs(os.path.dirname(dest), exist_ok=True)
            with z.open(info) as fsrc, open(dest, "wb") as fdst:
                shutil.copyfileobj(fsrc, fdst)
            if safe.lower().endswith(".png"):
                out.append(dest)
    return out


# ── Основной сценарий ─────────────────────────────────────────────────

def main():
    ap = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("src", nargs="?", help="zip-батч или каталог с PNG")
    ap.add_argument("--dir", help="каталог уже распакованных генераций")
    ap.add_argument("--dry-run", action="store_true",
                    help="без установки в game/: продукты в staging/out, "
                         "валидация на зеркале")
    ap.add_argument("--force", action="store_true",
                    help="перезаписывать существующие позиции в game/")
    ap.add_argument("--map", action="append", default=[], metavar="STEM=ID",
                    help="ручной маппинг имени файла на позицию манифеста "
                         "(id или file-stem; повторяемо)")
    ap.add_argument("--filter", default="",
                    help="обрабатывать только позиции, содержащие подстроку")
    ap.add_argument("--staging", default="",
                    help="корень staging (по умолч. /tmp/sprite_intake/<имя>)")
    args = ap.parse_args()

    src = args.dir or args.src
    if not src or not os.path.exists(src):
        print("ОШИБКА: не указан источник (zip или каталог).")
        return 2
    if not os.path.exists(MANIFEST):
        print("НЕТ манифеста — запустите: python3 tools/sprites/make_manifest.py")
        return 2

    with open(MANIFEST, encoding="utf-8") as f:
        manifest = json.load(f)
    by_id, by_stem = build_indexes(manifest)
    manual = {}
    for m in args.map:
        if "=" not in m:
            print(f"ОШИБКА --map: ожидалось STEM=ID, получено {m!r}")
            return 2
        k, v = m.split("=", 1)
        manual[k.strip().lower()] = v.strip()

    stem = os.path.basename(src)
    staging = args.staging or os.path.join(
        "/tmp/sprite_intake", f"{os.path.splitext(stem)[0]}_{int(time.time())}")
    extract_dir = os.path.join(staging, "extract")
    out_root = os.path.join(staging, "out")
    os.makedirs(extract_dir, exist_ok=True)

    # ── 1. Инвентаризация ────────────────────────────────────────────
    print(f"════ ПРИЁМКА БАТЧА: {src}")
    if os.path.isdir(src):
        pngs = []
        for root, _dirs, files in os.walk(src):
            if "__MACOSX" in root:
                continue
            for fn in sorted(files):
                if fn.lower().endswith(".png"):
                    pngs.append(os.path.join(root, fn))
                else:
                    print(f"  [игнор] {fn} (не PNG)")
    else:
        pngs = extract_zip(src, extract_dir)
    if not pngs:
        print("ОШИБКА: PNG не найдены.")
        return 2
    print(f"PNG в батче: {len(pngs)}")

    # ── 2. Маппинг ───────────────────────────────────────────────────
    mapped = {}       # entry id -> [(path, score)]
    unmapped = []
    for p in sorted(pngs):
        stem_n = norm_stem(p)
        if stem_n in manual:
            hits, seen = [], set()
            for e in by_stem.get(manual[stem_n], []) + \
                    by_id.get(manual[stem_n], []):
                if e["file"] not in seen:
                    seen.add(e["file"])
                    hits.append(e)
            if not hits:
                print(f"  [map] {stem_n} → {manual[stem_n]}: НЕТ в манифесте")
        else:
            hits = resolve_entries(stem_n, by_id, by_stem)
        if not hits:
            unmapped.append((p, stem_n))
            continue
        try:
            img_size = Image.open(p).size
        except Exception:
            img_size = None
        entry, note = disambiguate(hits, img_size)
        if entry is None:
            unmapped.append((p, f"{stem_n} ({note})"))
            continue
        if args.filter and args.filter.lower() not in entry["id"]:
            continue
        if note:
            print(f"  [map] {stem_n} → {entry['id']} ({note})")
        mapped.setdefault(entry["id"], []).append((p, score_variant(p, entry)))

    # ── 3. Продакшн + валидация + установка ──────────────────────────
    vs_dir_real = vs.SPRITES
    produced, installed, skipped_exist, rejected, defects = [], [], [], [], []
    for ident, variants in sorted(mapped.items()):
        entry = by_id[ident][0]
        variants.sort(key=lambda pv: (pv[1], os.path.getsize(pv[0])))
        best, score = variants[-1]
        others = [os.path.basename(p) for p, _ in variants[:-1]]
        kind, arg = route(entry, best)
        if kind == "REJECT":
            rejected.append(ident)
            print(f"✘ {ident}: {os.path.basename(best)} — {arg}")
            continue
        out_path = os.path.join(out_root, entry["file"])
        if not produce(kind, arg, best, entry, out_path):
            rejected.append(ident)
            print(f"✘ {ident}: продакшн {kind} не удался ({os.path.basename(best)})")
            continue
        produced.append(ident)
        extra = f" (варианты отброшены: {', '.join(others)})" if others else ""
        print(f"✔ {ident}: {kind} ← {os.path.basename(best)}{extra}")

        vs.SPRITES = out_root                       # валидация продукта
        d = vs.check_file(entry)
        if d:
            defects.append((ident, d))
            for x in d:
                print(f"    — ДЕФЕКТ: {x}")
            continue
        if args.dry_run:
            continue
        target = os.path.join(vs_dir_real, entry["file"])
        os.makedirs(os.path.dirname(target), exist_ok=True)
        if os.path.exists(target) and not args.force:
            skipped_exist.append(ident)
            print(f"  ⚠ {ident}: уже доставлен, пропуск (перезапись: --force)")
            continue
        shutil.copyfile(out_path, target)
        installed.append(ident)
        vs.SPRITES = vs_dir_real                    # валидация установленного
        d2 = vs.check_file(entry)
        if d2:
            defects.append((ident, d2))
            for x in d2:
                print(f"    — ДЕФЕКТ (после установки): {x}")

    # ── 4. Отчёт ─────────────────────────────────────────────────────
    print("\n──── ИТОГО ────")
    print(f"продуктов: {len(produced)}   установлено: {len(installed)}"
          f"   (dry-run={int(bool(args.dry_run))}, force={int(bool(args.force))})")
    print(f"занятых позиций (без --force): {len(skipped_exist)}   "
          f"отклонено: {len(rejected)}   дефектных: {len(defects)}")
    if unmapped:
        print(f"\nНЕ СОПОСТАВЛЕНЫ с манифестом ({len(unmapped)}) — "
              f"проверьте имена или добавьте --map stem=<позиция>:")
        for p, s in unmapped:
            print(f"  · {os.path.basename(p)}  (норм. имя: {s})")
    cover_root = out_root if args.dry_run else vs_dir_real
    cover = {}
    for e in manifest["sheets"]:
        if os.path.exists(os.path.join(cover_root, e["file"])):
            cover[e["phase"]] = cover.get(e["phase"], 0) + 1
    total_cover = sum(cover.values())
    phases = " ".join(f"{k}:{v}" for k, v in sorted(cover.items()))
    print(f"\nпокрытие манифеста: {total_cover}/{len(manifest['sheets'])} "
          f"({phases})")
    print(f"staging: {staging}")

    if defects:
        print("\nVERDICT: FAIL — дефектные позиции требуют правки/перегенерации")
        return 1
    print("\nVERDICT: PASS — все продукты батча соответствуют контракту")
    return 0


if __name__ == "__main__":
    sys.exit(main())

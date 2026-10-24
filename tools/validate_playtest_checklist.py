#!/usr/bin/env python3
"""Валидатор чек-листа плейтеста (pre-playtest gate, 24.10).

Проверяет portal/data/playtest.json против portal/data/bugs.json:
  1. JSON парсится; обязательные поля пунктов на месте; статусы/приоритеты
     из допустимого множества.
  2. ID пунктов уникальны по всем категориям (критерий допуска к раунду 2).
  3. Рефы классифицируются: точное совпадение с реестром / контекстный тег
     (не реестр: коммиты, доки, волны) / неразрешимый реестро-подобный реф
     (предупреждение, не ошибка — часть находок этапов 13/14/16/17 живёт
     только в checkpoint-доках и в bugs.json не регистрировалась).
  4. Счётчики статусов для сверки со связанными отчётами.

Exit code: 0 = JSON валиден и дубликатов ID нет (неразрешимые рефы —
предупреждения); 1 = JSON битый / дубликаты ID / битые поля.

Использование: python3 tools/validate_playtest_checklist.py \
                    [portal/data/playtest.json] [portal/data/bugs.json]
"""
from __future__ import annotations

import json
import re
import sys
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PLAYTEST = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "portal/data/playtest.json"
BUGS = Path(sys.argv[2]) if len(sys.argv) > 2 else ROOT / "portal/data/bugs.json"

ALLOWED_STATUS = {"planned", "passed", "failed", "skipped"}
ALLOWED_PRIORITY = {"high", "normal", "optional"}
# Реестро-подобный реф: BUG-012 / GAP-025 / STAGE11-004 / ST11-001 / ST13-001a
REGISTRY_STYLE = re.compile(r"^(?:BUG|VER|GAP|STAGE\d{1,2}|ST\d{2})-\d{2,3}[a-z]?$")


def main() -> int:
    problems: list[str] = []

    try:
        data = json.loads(PLAYTEST.read_text(encoding="utf-8"))
    except Exception as exc:  # noqa: BLE001 — любая ошибка разбора фатальна
        print(f"FATAL: playtest.json не парсится: {exc}")
        return 1
    try:
        bugs = json.loads(BUGS.read_text(encoding="utf-8"))
    except Exception as exc:  # noqa: BLE001
        print(f"FATAL: bugs.json не парсится: {exc}")
        return 1

    registry_ids = {b["id"] for b in bugs.get("items", [])}
    items: list[tuple[str, dict]] = []
    for cat in data.get("categories", []):
        for it in cat.get("items", []):
            items.append((cat.get("id", "?"), it))

    # ── 1. Структурная валидность ───────────────────────────────────────────
    for cat, it in items:
        missing = [f for f in ("id", "title", "priority", "status", "steps", "expected")
                   if f not in it]
        if missing:
            problems.append(f"{it.get('id', '?')}: нет полей {missing}")
        if it.get("status") not in ALLOWED_STATUS:
            problems.append(f"{it.get('id', '?')}: неизвестный статус {it.get('status')!r}")
        if it.get("priority") not in ALLOWED_PRIORITY:
            problems.append(f"{it.get('id', '?')}: неизвестный приоритет {it.get('priority')!r}")

    # ── 2. Уникальность ID ──────────────────────────────────────────────────
    ids = [it["id"] for _, it in items]
    dupes = {i: n for i, n in Counter(ids).items() if n > 1}
    if dupes:
        for dup_id, n in sorted(dupes.items()):
            where = [f"{cat}/{it['id']}" for cat, it in items if it["id"] == dup_id]
            problems.append(f"ДУБЛИКАТ ID {dup_id} ×{n}: {where}")

    # ── 3. Классификация рефов ──────────────────────────────────────────────
    exact, ctx_tags, unresolved = [], [], []
    for cat, it in items:
        for ref in it.get("refs", []):
            if ref in registry_ids:
                exact.append((it["id"], ref))
            elif REGISTRY_STYLE.match(ref):
                unresolved.append((it["id"], ref))
            else:
                ctx_tags.append((it["id"], ref))

    # ── 4. Счётчики ─────────────────────────────────────────────────────────
    statuses = Counter(it["status"] for _, it in items)
    print(f"Файл: {PLAYTEST}")
    print(f"Пунктов: {len(items)}; категорий: {len(data.get('categories', []))}")
    print(f"Статусы: {dict(statuses)} "
          f"(passed={statuses.get('passed', 0)}, failed={statuses.get('failed', 0)}, "
          f"planned={statuses.get('planned', 0)}, skipped={statuses.get('skipped', 0)})")
    print(f"Реестр bugs.json: {len(registry_ids)} записей")
    print()
    print(f"Рефы: точных совпадений с реестром {len(exact)}; "
          f"контекстных тегов {len(ctx_tags)}; "
          f"неразрешимых реестро-подобных {len(unresolved)}")
    if unresolved:
        print("  Неразрешимые (предупреждение — нет такой записи в bugs.json):")
        for item_id, ref in unresolved:
            print(f"    {item_id} -> {ref}")
    if dupes:
        print("  ДУБЛИКАТЫ ID (блокер): см. выше")
    print()
    if problems:
        print("ПРОБЛЕМЫ:")
        for p in problems:
            print(f"  - {p}")
        return 1
    print("VERDICT: OK — JSON валиден, дубликатов ID нет")
    return 0


if __name__ == "__main__":
    sys.exit(main())

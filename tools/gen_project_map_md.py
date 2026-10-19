#!/usr/bin/env python3
"""Генератор PROJECT_MAP.md из project_map.json (единственный источник правды).

JSON — канон; этот MD — сгенерированный дашборд для человека и git-обзора.
Править данные ТОЛЬКО в project_map.json, затем перегенерировать MD:
    python3 tools/gen_project_map_md.py           # записать docs/.../PROJECT_MAP.md
    python3 tools/gen_project_map_md.py --check   # только сверка (exit 1 при дрейфе)

Конвенции (сверено с дашбордом итерации 3, 19.10.2026):
- «Финал трека» в §2 обрезается до 110 символов + «…»;
- счётчики этапов в §2: ✅done 🔧in_progress ◻️прочие (planned+deferred+not_started);
- прогресс-формула — см. format.progress_rule в JSON.
"""
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MAP_PATH = os.path.join(ROOT, "docs", "docs_v2", "00_overview", "project_map.json")
OUT_PATH = os.path.join(ROOT, "docs", "docs_v2", "00_overview", "PROJECT_MAP.md")

STAGE_ICON = {"done": "✅", "in_progress": "🔧", "planned": "◻️",
              "deferred": "⏸️", "not_started": "⬜"}
TRACK_ICON = {"complete": "✅", "in_progress": "🔧",
              "foundation": "🧱", "not_started": "⬜"}
MS_STATUS = {"done": "✅ done", "in_progress": "🔧 в работе",
             "not_started": "⬜ не начато"}
FINAL_CUT = 110

RULES_SECTION = """## 4. Правила ведения карты

1. **Единственный источник правды — `project_map.json`.** MD-дашборд генерируется из JSON и не редактируется руками.
2. **Статусы этапов:** `done` (есть пруф: чекпоинт/коммит/QA-вердикт) · `in_progress` · `planned` · `deferred` (причина в note) · `not_started`.
3. **Прогресс трека** = доля `done`-этапов (авто-пересчёт при изменениях).
4. **Обновление:** в конце каждой рабочей итерации (R-раунда) — сдвиг статусов этапов, добавление новых этапов под `final` трека; коммит вместе с чекпоинтом.
5. **Веб-портал (план):** читает JSON; каждый трек — «звуковая дорожка»: маркеры этапов на шкале от `start.date` к `final`; цвет маркера = статус; подписи из `title`/`note`; группировка по `group`; вехи M0–M7 — верхняя общая дорожка."""


def esc(s: str) -> str:
    return s.replace("|", "\\|")


def swindow(w) -> str:
    """Окно этапа: [d1, d2] → 'd1', 'd1→d2', '' (список может быть короче 2)"""
    if not w:
        return ""
    a = w[0] or ""
    b = (w[1] if len(w) > 1 else None) or ""
    if a and b:
        return f"{a}→{b}"
    return a


def mwindow(w) -> str:
    """Окно вехи: 'd1 → d2' / 'd1 → …' / '— → …'"""
    a = (w[0] if w and w[0] else "—")
    b = (w[1] if w and len(w) > 1 and w[1] else "…")
    return f"{a} → {b}"


def final_cell(t: dict) -> str:
    fin = t["final"]
    if len(fin) > FINAL_CUT:
        fin = fin[:FINAL_CUT] + "…"
    return esc(fin)


def proof_cell(s: dict) -> str:
    proofs = " · ".join(f"`{p}`" for p in s.get("proofs", []))
    note = s.get("note", "")
    if proofs and note:
        return f"{proofs} — {note}"
    return proofs or note


def gen(m: dict) -> str:
    out = []
    ap = out.append

    # ── Шапка ──────────────────────────────────────────────────────────
    ap("# Карта проекта — Cultivation World Simulator (Ai-game4)")
    ap("")
    ap("> **Раздел:** 00_overview · **Статус:** активный документ-роадмап")
    ap(f"> **Обновлено:** {m['updated']} ({m['version']}) · **Ядро данных:** "
       "[project_map.json](project_map.json) (машиночитаемое — единственный источник правды)")
    ap(">")
    ap("> **Назначение:** детальный роадмап для веб-портала «звуковые дорожки»: каждый трек = шкала")
    ap("> последовательных этапов (начало → выполненные → текущая позиция → запланированные → финал модуля).")
    ap("> Этот файл — сгенерированный дашборд JSON; править данные только в JSON, затем перегенерировать MD.")
    ap("")
    ap("---")
    ap("")

    # ── §1 Вехи ────────────────────────────────────────────────────────
    ap("## 1. Вехи проекта (M0–M7)")
    ap("")
    ap("| Веха | Название | Статус | Окно | Суть |")
    ap("|---|---|---|---|---|")
    for ms in m["milestones"]:
        ap(f"| **{ms['id']}** | {esc(ms['title'])} | {MS_STATUS[ms['status']]} | "
           f"{mwindow(ms.get('window'))} | {esc(ms['summary'])} |")
    ap("")
    ap("---")
    ap("")

    # ── §2 Сводка треков ───────────────────────────────────────────────
    ap("## 2. Треки (дорожки) — сводка")
    ap("")
    ap("Прогресс = доля выполненных этапов от общего числа (формула в JSON `format.progress_rule`).")
    ap("")
    ap("| # | Трек | Группа | Прогресс | Этапы | Финал трека (definition of done) |")
    ap("|---|---|---|---|---|---|")
    for t in sorted(m["tracks"], key=lambda x: x["number"]):
        d = sum(1 for s in t["stages"] if s["status"] == "done")
        i = sum(1 for s in t["stages"] if s["status"] == "in_progress")
        o = len(t["stages"]) - d - i
        grp = re.sub(r"^[A-D]\. ", "", t["group"])
        ap(f"| {t['number']} | **{esc(t['title'])}** `{t['id']}` | {grp} | "
           f"{TRACK_ICON[t['status']]} {t['progress_pct']}% | ✅{d} 🔧{i} ◻️{o} | {final_cell(t)} |")
    ap("")
    ap("---")
    ap("")

    # ── §3 Детальные дорожки ───────────────────────────────────────────
    ap("## 3. Детальные дорожки по группам")
    ap("")
    groups = []
    for t in sorted(m["tracks"], key=lambda x: x["number"]):
        if t["group"] not in groups:
            groups.append(t["group"])
    for grp in groups:
        ap(f"### {grp}")
        ap("")
        for t in [x for x in sorted(m["tracks"], key=lambda x: x["number"])
                  if x["group"] == grp]:
            ap(f"#### {t['number']}. {esc(t['title'])} — "
               f"{t['progress_pct']}% ({TRACK_ICON[t['status']]})")
            ap("")
            ap(f"**Цель:** {esc(t['goal'])}")
            ap("")
            ap(f"**Финал трека:** {esc(t['final'])}")
            ap("")
            st = t.get("start") or {}
            if st.get("date"):
                ap(f"**Начало:** {st['date']} — {esc(st.get('milestone', ''))}")
            else:
                ap(f"**Начало:** {esc(st.get('milestone', 'Не начат'))}")
            ap("")
            ap("| Этап | Статус | Окно | Пруф / примечание |")
            ap("|---|---|---|---|")
            for s in t["stages"]:
                ap(f"| {esc(s['title'])} | {STAGE_ICON[s['status']]} {s['status']} | "
                   f"{swindow(s.get('window'))} | {esc(proof_cell(s))} |")
            ap("")
        if grp != groups[-1]:
            ap("")
    ap("---")
    ap("")

    # ── §4 Правила ─────────────────────────────────────────────────────
    ap(RULES_SECTION)
    ap("")
    ap("---")
    ap("")

    # ── §5 Открытые верификации ────────────────────────────────────────
    ap("## 5. Открытые верификации (перенос в следующие итерации)")
    ap("")
    for v in m.get("open_verifications", []):
        ap(f"- {v}")
    ap("")
    return "\n".join(out)


def main() -> int:
    with open(MAP_PATH, encoding="utf-8") as f:
        m = json.load(f)
    text = gen(m)
    if "--check" in sys.argv:
        with open(OUT_PATH, encoding="utf-8") as f:
            cur = f.read()
        if cur == text:
            print("OK: PROJECT_MAP.md синхронен с project_map.json")
            return 0
        print("DRIFT: PROJECT_MAP.md отличается от JSON — перегенерируй:")
        print("    python3 tools/gen_project_map_md.py")
        return 1
    with open(OUT_PATH, "w", encoding="utf-8") as f:
        f.write(text)
    print(f"OK: сгенерирован {os.path.relpath(OUT_PATH, ROOT)} "
          f"({len(text.splitlines())} строк) из project_map.json v{m['version']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

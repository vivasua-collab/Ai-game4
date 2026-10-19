#!/usr/bin/env bash
# =============================================================================
# RESTORE_PORTAL — восстановление каркаса веб-портала «Карта проекта Ai-game4»
# =============================================================================
# Назначение: после сброса/отката песочницы возвращает тонкий каркас портала
# (страница /, API /api/portal, компоненты, типы) из git-репозитория aigame4
# в живой Next.js-проект my-project. Данные НЕ восстанавливаются — их нечего
# восстанавливать: портал читает JSON из репозитория в рантайме.
#
# Запуск (из любого каталога):
#   bash /home/z/my-project/aigame4/portal/restore_portal.sh
#
# Idempotent: безопасно запускать многократно; существующие файлы портала
# перезаписываются — это и есть restore.
# =============================================================================
set -euo pipefail

MY_PROJECT="/home/z/my-project"

# ─── Шаг 0: проверки окружения ──────────────────────────────────────────────
echo "═══════════════════════════════════════════════════════════════"
echo "  RESTORE PORTAL — Карта проекта Ai-game4"
echo "  Время: $(date)"
echo "═══════════════════════════════════════════════════════════════"

if [ ! -d "$MY_PROJECT" ]; then
    echo "❌ Нет каталога $MY_PROJECT — песочница не восстановлена."
    echo "   Проверь окружение Next.js-проекта и повтори запуск."
    exit 1
fi

# Репозиторий может лежать как aigame4 (симлинк от cold_start), так и Ai-game4.
PORTAL_DIR="$MY_PROJECT/aigame4/portal"
if [ ! -d "$PORTAL_DIR" ]; then
    PORTAL_DIR="$MY_PROJECT/Ai-game4/portal"
fi

if [ ! -d "$PORTAL_DIR/skeleton/src" ]; then
    echo "❌ Репозиторий Ai-game4 (каталог portal/skeleton) не найден в $MY_PROJECT."
    echo "   Склонируй репозиторий и повтори:"
    echo "     cd $MY_PROJECT"
    echo "     git clone https://github.com/vivasua-collab/Ai-game4"
    echo "   (затем: bash aigame4/cold_start.sh — создаст симлинк aigame4)"
    exit 1
fi

# ─── Шаг 1: копирование каркаса skeleton → my-project/src ────────────────────
# mkdir -p + cp: перезапись существующих файлов портала допустима — это restore.
echo "── Шаг 1/2: копирование каркаса портала ──"
mkdir -p "$MY_PROJECT/src"
cp -a "$PORTAL_DIR/skeleton/src/." "$MY_PROJECT/src/"

COPIED=$(find "$PORTAL_DIR/skeleton/src" -type f | wc -l)
echo "  ✅ Скопировано файлов: $COPIED"
echo "     src/app/page.tsx                       (единственная страница /)"
echo "     src/app/api/portal/route.ts             (API /api/portal, no-store)"
echo "     src/components/portal/*.tsx             (компоненты каркаса)"
echo "     src/lib/portal/*.ts                     (типы и helpers)"

# ─── Шаг 2: проверка dev-сервера (НЕ запускаем сами — только подсказка) ─────
echo "── Шаг 2/2: проверка dev-сервера (порт 3000) ──"
HTTP_CODE="$(curl -s -o /dev/null -w '%{http_code}' --max-time 3 http://localhost:3000/ || echo 000)"
if [ "$HTTP_CODE" = "200" ]; then
    echo "  ✅ Dev-сервер отвечает (HTTP 200): http://localhost:3000/"
    echo "     Открыть страницу: /  ·  API данных: /api/portal"
else
    echo "  ⚠ Dev-сервер не отвечает (HTTP $HTTP_CODE)."
    echo "     Запусти вручную:  cd $MY_PROJECT && bun run dev"
    echo "     (скрипт сервер сам не запускает — только подсказывает)"
fi

# ─── Саммари ────────────────────────────────────────────────────────────────
echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "  RESTORE PORTAL ЗАВЕРШЁН"
echo "═══════════════════════════════════════════════════════════════"
echo "Источники данных (читаются порталом в рантайме, правка = правка JSON):"
echo "  aigame4/docs/docs_v2/00_overview/project_map.json   (карта: треки/этапы/вехи)"
echo "  aigame4/portal/data/bugs.json                       (реестр находок аудита)"
echo "  aigame4/portal/data/status.json                     (снимок состояния)"
echo "Что проверять после запуска сервера:"
echo "  curl -s http://localhost:3000/api/portal | head -c 300   → ok:true"
echo "  браузер: http://localhost:3000/                          → страница портала"
echo "Обновление каркаса после правок портала: bash aigame4/portal/publish_skeleton.sh"

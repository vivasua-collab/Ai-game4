#!/usr/bin/env bash
# =============================================================================
# PUBLISH_SKELETON — актуализация зеркала каркаса портала в git-репозитории
# =============================================================================
# Обратная операция к restore_portal.sh: копирует ЖИВЫЕ исходники портала из
# my-project в aigame4/portal/skeleton/ (то, что переживает откат песочницы
# и попадает в git). Старое содержимое skeleton/src стирается полностью —
# зеркалу незачем копить удалённые файлы.
#
# Запуск (из любого каталога):
#   bash /home/z/my-project/aigame4/portal/publish_skeleton.sh
#
# После публикации закоммить aigame4 (коммит делает main-агент).
# Idempotent: безопасно запускать многократно.
# =============================================================================
set -euo pipefail

MY_PROJECT="/home/z/my-project"
LIVE_APP="$MY_PROJECT/src/app"
LIVE_COMPONENTS="$MY_PROJECT/src/components/portal"
LIVE_LIB="$MY_PROJECT/src/lib/portal"

# Репозиторий может лежать как aigame4 (симлинк от cold_start), так и Ai-game4.
PORTAL_DIR="$MY_PROJECT/aigame4/portal"
if [ ! -d "$PORTAL_DIR" ]; then
    PORTAL_DIR="$MY_PROJECT/Ai-game4/portal"
fi
SKELETON="$PORTAL_DIR/skeleton"

echo "═══════════════════════════════════════════════════════════════"
echo "  PUBLISH SKELETON — зеркалирование каркаса портала"
echo "  Время: $(date)"
echo "═══════════════════════════════════════════════════════════════"

# ─── Шаг 0: проверка живых исходников ───────────────────────────────────────
if [ ! -f "$LIVE_APP/page.tsx" ] || [ ! -f "$LIVE_APP/api/portal/route.ts" ] \
   || [ ! -d "$LIVE_COMPONENTS" ] || [ ! -d "$LIVE_LIB" ]; then
    echo "❌ Живые исходники портала не найдены в $MY_PROJECT/src."
    echo "   Ожидаются: src/app/page.tsx, src/app/api/portal/route.ts,"
    echo "              src/components/portal/, src/lib/portal/."
    exit 1
fi
if [ ! -d "$SKELETON" ]; then
    echo "❌ Нет каталога $SKELETON — репозиторий aigame4 не восстановлен?"
    echo "   Склонируй: git clone https://github.com/vivasua-collab/Ai-game4"
    exit 1
fi

# ─── Шаг 1: чистка старого зеркала ───────────────────────────────────────────
echo "── Шаг 1/3: очистка прежнего зеркала skeleton/src ──"
rm -rf "$SKELETON/src"
mkdir -p "$SKELETON/src/app/api/portal" "$SKELETON/src/components/portal" "$SKELETON/src/lib/portal"

# ─── Шаг 2: копирование свежих исходников ────────────────────────────────────
echo "── Шаг 2/3: копирование живых исходников в skeleton ──"
cp "$LIVE_APP/page.tsx"                     "$SKELETON/src/app/page.tsx"
cp "$LIVE_APP/api/portal/route.ts"          "$SKELETON/src/app/api/portal/route.ts"
cp "$LIVE_COMPONENTS"/*.tsx                 "$SKELETON/src/components/portal/"
cp "$LIVE_LIB"/*.ts                         "$SKELETON/src/lib/portal/"

# ─── Шаг 3: саммари ────────────────────────────────────────────────────────
COPIED=$(find "$SKELETON/src" -type f | wc -l)
echo "── Шаг 3/3: итог ──"
echo "  ✅ Зеркало обновлено: $SKELETON/src ($COPIED файлов)"
echo "     src/app/page.tsx + src/app/api/portal/route.ts"
echo "     src/components/portal/ ($(ls "$LIVE_COMPONENTS"/*.tsx | wc -l) компонентов)"
echo "     src/lib/portal/ ($(ls "$LIVE_LIB"/*.ts | wc -l) модулей)"
echo ""
echo "Дальше: закоммить репозиторий aigame4 (коммит и push — main-агент)."
echo "Проверка восстановления: bash $PORTAL_DIR/restore_portal.sh"

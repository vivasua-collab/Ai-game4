#!/bin/bash
# =============================================================================
# run_godot.sh — обёртка запуска Godot (обход overlayfs ENOENT-флапа)
# =============================================================================
# ДИАГНОЗ (сессия 2026-09-03): корень ФС — overlayfs (kata-containers,
# volatile, index=off). Lookup по НОВОМУ пути в НОВОМ процессе периодически
# отдаёт ENOENT при живом файле (stat/прямой exec из прогретой сессии — OK).
# Повторный lookup того же пути «прогревает» dentry и проходит. Это и есть
# «аномалия ФС» из worklog 08-26 (unzip-файлы «мерцают», python-zipfile
# «стабильны» — на деле оба стабильны, флапает resolution).
#
# Обход: серия lookup-прогревов каждого компонента пути + ретраи запуска.
#
# Использование:
#   env GODOT_NEWGAME=1 GODOT_COMBAT_SIM=1 GODOT_TIMEOUT=40 \
#     tools/run_godot.sh --headless --path game scenes/MainMenu.tscn
#
# Переменные:
#   GODOT_TIMEOUT — сек watchdog (default 40). Exit-код всегда 0:
#   headless-прогоны НЕ само-завершаются (мир живёт до kill) — критерий
#   PASS = ключевые строки лога (SESSION_CONTEXT.md §6).
# =============================================================================
set -u
export DOTNET_ROOT="${DOTNET_ROOT:-/home/z/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"

GODOT_BIN="/home/z/my-project/godot/Godot_v4.7.1-stable_mono_linux.x86_64/Godot_v4.7.1-stable_mono_linux.x86_64"
GODOT_DIR="/home/z/my-project/godot/Godot_v4.7.1-stable_mono_linux.x86_64"
GAME_DIR="/home/z/my-project/aigame4/game"
TIMEOUT_SECS="${GODOT_TIMEOUT:-40}"

# ── Прогрев overlayfs-lookup: цепочка компонентов пути ──
warmup() {
    local p="" part
    IFS='/' read -ra parts <<< "$1"
    for part in "${parts[@]}"; do
        [ -z "$part" ] && continue
        p="$p/$part"
        # Повторяем lookup, пока не пройдёт (максимум 5).
        for _ in 1 2 3 4 5; do
            [ -d "$p" ] || [ -f "$p" ] && break
            sleep 0.1
        done
    done
}

warmup "$GODOT_DIR"
warmup "$GAME_DIR"

cd "$GAME_DIR" || { echo "run_godot: game dir not found" >&2; exit 1; }

# ── Запуск с ретраями (ENOENT-флап на exec) ──
for attempt in 1 2 3 4 5; do
    "$GODOT_BIN" "$@" &
    PID=$!
    ( sleep "$TIMEOUT_SECS"; kill "$PID" 2>/dev/null ) &
    WATCHDOG=$!
    # Если процесс умер в первые 2 сек И код 127 — флап, ретрай.
    sleep 2
    if ! kill -0 "$PID" 2>/dev/null; then
        wait "$PID" 2>/dev/null
        rc=$?
        kill "$WATCHDOG" 2>/dev/null; wait "$WATCHDOG" 2>/dev/null
        if [ "$rc" -eq 127 ]; then
            echo "run_godot: ENOENT flap (attempt $attempt), retrying..." >&2
            warmup "$GODOT_BIN"
            sleep 1
            continue
        fi
        exit 0
    fi
    wait "$PID" 2>/dev/null
    kill "$WATCHDOG" 2>/dev/null; wait "$WATCHDOG" 2>/dev/null
    exit 0
done
echo "run_godot: all attempts failed" >&2
exit 1

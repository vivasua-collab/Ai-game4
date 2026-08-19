#!/usr/bin/env bash
# =============================================================================
# COLD START — Чистая процедура холодного старта sandbox
# =============================================================================
# Заменяет recover_sandbox.sh (минимальная структура, 2 симлинка)
# Дизайн: docs/docs_v2/09_workflow/COLD_START.md
#
# Запуск:
#   bash /home/z/my-project/aigame4/cold_start.sh
#   bash /home/z/my-project/Ai-game4/cold_start.sh  (если симлинка ещё нет)
#
# Idempotent: безопасно запускать многократно.
# =============================================================================

set -e

TOKEN="${GITHUB_TOKEN:-[REDACTED:github_token]}"
REPO_URL_PUBLIC="https://github.com/vivasua-collab/Ai-game4.git"
REPO_URL_AUTH="https://x-access-token:${TOKEN}@github.com/vivasua-collab/Ai-game4.git"
GODOT_URL="https://github.com/godotengine/godot/releases/download/4.7.1-stable/Godot_v4.7.1-stable_mono_linux_x86_64.zip"
DOTNET_INSTALL="/tmp/dotnet-install.sh"

SANDBOX="/home/z/my-project"
REPO_DIR="$SANDBOX/Ai-game4"
GODOT_DIR="/home/z/godot"
DOTNET_DIR="/home/z/.dotnet"

echo "═══════════════════════════════════════════════════════════════"
echo "  COLD START — Cultivation World Simulator (Ai-game4)"
echo "  Время: $(date)"
echo "═══════════════════════════════════════════════════════════════"
echo ""

# ─── Шаг 1: .NET SDK ──────────────────────────────────────────────
echo "── Шаг 1/6: .NET SDK ──"
if [ -x "$DOTNET_DIR/dotnet" ]; then
    echo "  ✅ Уже установлен: $($DOTNET_DIR/dotnet --version)"
else
    echo "  Устанавливаю .NET SDK 8.0 + 9.0..."
    curl -sSL https://dot.net/v1/dotnet-install.sh -o "$DOTNET_INSTALL"
    chmod +x "$DOTNET_INSTALL"
    "$DOTNET_INSTALL" --channel 8.0 --install-dir "$DOTNET_DIR" --no-path 2>&1 | tail -1
    "$DOTNET_INSTALL" --channel 9.0 --install-dir "$DOTNET_DIR" --no-path 2>&1 | tail -1
fi
export DOTNET_ROOT="$DOTNET_DIR"
export PATH="$DOTNET_ROOT:$PATH"
echo "  SDK: $(dotnet --list-sdks | tr '\n' ' ')"
echo ""

# ─── Шаг 2: Godot 4.7.1 .NET ─────────────────────────────────────
echo "── Шаг 2/6: Godot 4.7.1 ──"
GODOT_BIN="$GODOT_DIR/Godot_v4.7.1-stable_mono_linux_x86_64/Godot_v4.7.1-stable_mono_linux.x86_64"
if [ -x "$GODOT_BIN" ]; then
    echo "  ✅ Уже установлен: $(basename "$GODOT_DIR")"
else
    echo "  Скачиваю Godot 4.7.1 .NET..."
    curl -sSL "$GODOT_URL" -o /tmp/godot471.zip
    mkdir -p "$GODOT_DIR"
    cd "$GODOT_DIR"
    unzip -o /tmp/godot471.zip > /dev/null 2>&1
    chmod +x "$GODOT_BIN"
    rm -f /tmp/godot471.zip
fi
echo ""

# ─── Шаг 3: Ai-game4 репозиторий ─────────────────────────────────
echo "── Шаг 3/6: Ai-game4 (git clone/pull) ──"
if [ -d "$REPO_DIR/.git" ]; then
    echo "  Репо существует, обновляю..."
    cd "$REPO_DIR"
    git remote set-url origin "$REPO_URL_AUTH"
    git pull --ff-only 2>&1 | tail -2
    git remote set-url origin "$REPO_URL_PUBLIC"
else
    echo "  Клонирую с GitHub..."
    cd "$SANDBOX"
    git clone "$REPO_URL_AUTH" 2>&1 | tail -2
    cd "$REPO_DIR"
    git remote set-url origin "$REPO_URL_PUBLIC"
fi
echo "  HEAD: $(git -C "$REPO_DIR" rev-parse --short HEAD)"
echo ""

# ─── Шаг 4: Симлинки (минимум — только 2) ────────────────────────
echo "── Шаг 4/6: Симлинки (aigame4 + godot) ──"
# Удаляем ВСЕ старые симлинки (включая устаревшие из Variant D)
for link in aigame4 checkpoints game game-docs godot; do
    rm -rf "$SANDBOX/$link" 2>/dev/null || true
done

# Создаём только 2 симлинка (минимальная структура)
ln -sf "$REPO_DIR"       "$SANDBOX/aigame4"
ln -sf "$GODOT_DIR"      "$SANDBOX/godot"

echo "  aigame4 → $(readlink "$SANDBOX/aigame4")"
echo "  godot   → $(readlink "$SANDBOX/godot")"
echo "  (устаревшие checkpoints, game, game-docs — удалены)"
echo ""

# ─── Шаг 5: NuGet.config (локальный, gitignored) ─────────────────
echo "── Шаг 5/6: NuGet.config ──"
cat > "$REPO_DIR/game/NuGet.config" << 'NUGET'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
NUGET
echo "  ✅ NuGet.config создан"
echo ""

# ─── Шаг 6: Верификация ──────────────────────────────────────────
echo "── Шаг 6/6: Верификация ──"

# Сборка
echo "  dotnet build:"
cd "$REPO_DIR/game"
BUILD_OUTPUT=$(dotnet build 2>&1)
echo "$BUILD_OUTPUT" | tail -3

# Headless проверка
echo ""
echo "  Godot headless:"
GODOT_BIN="$GODOT_DIR/Godot_v4.7.1-stable_mono_linux_x86_64/Godot_v4.7.1-stable_mono_linux.x86_64"
timeout 15 "$GODOT_BIN" --headless --path . --quit 2>&1 | grep -E "(GameBoot|MainMenu|Started|Error)" | head -3

echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "  COLD START ЗАВЕРШЁН"
echo "  Время: $(date)"
echo "═══════════════════════════════════════════════════════════════"
echo ""
echo "Симлинки (2):"
for link in aigame4 godot; do
    if test -e "$SANDBOX/$link"; then
        echo "  ✅ $link → $(readlink "$SANDBOX/$link")"
    else
        echo "  ❌ $link (BROKEN)"
    fi
done
echo ""
echo "Ключевые файлы (через aigame4/):"
for f in START_PROMPT.md SESSION_SUMMARY.md worklog.md cold_start.sh checkpoints/08_19_full_audit.md; do
    if test -e "$SANDBOX/aigame4/$f"; then
        echo "  ✅ aigame4/$f"
    else
        echo "  ❌ aigame4/$f (MISSING)"
    fi
done
echo ""
echo "Доступ к проекту:"
echo "  cd /home/z/my-project/aigame4          # весь репозиторий"
echo "  cd /home/z/my-project/aigame4/game      # Godot проект"
echo "  cd /home/z/my-project/aigame4/checkpoints  # чекпоинты"
echo "  cd /home/z/my-project/aigame4/docs      # документация"

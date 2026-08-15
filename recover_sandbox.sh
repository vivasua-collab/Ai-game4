#!/usr/bin/env bash
# =============================================================================
# Восстановление песочницы после пересоздания контейнера
# =============================================================================
# Запуск: bash /home/z/my-project/Ai-game4/recover_sandbox.sh
#
# Что делает:
#   1. Устанавливает .NET SDK 8.0 + 9.0
#   2. Скачивает Godot 4.7.1 .NET
#   3. Клонирует Ai-game4 с GitHub
#   4. Чинит симлинки (game, game-docs, godot)
#   5. Верифицирует сборку
# =============================================================================

set -e

TOKEN="${GITHUB_TOKEN:-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}"
REPO_URL="https://github.com/vivasua-collab/Ai-game4.git"
GODOT_URL="https://github.com/godotengine/godot/releases/download/4.7.1-stable/Godot_v4.7.1-stable_mono_linux_x86_64.zip"
DOTNET_INSTALL="/tmp/dotnet-install.sh"

echo "=== Восстановление песочницы ==="
echo "Время: $(date)"
echo ""

# ── Шаг 1: .NET SDK ──────────────────────────────────────────────────────────
echo "── Шаг 1: .NET SDK ──"
if [ -f /home/z/.dotnet/dotnet ]; then
    echo "  .NET уже установлен: $(/home/z/.dotnet/dotnet --version)"
else
    echo "  Устанавливаю .NET SDK 8.0 и 9.0..."
    curl -sSL https://dot.net/v1/dotnet-install.sh -o "$DOTNET_INSTALL"
    chmod +x "$DOTNET_INSTALL"
    "$DOTNET_INSTALL" --channel 8.0 --install-dir /home/z/.dotnet --no-path 2>&1 | tail -2
    "$DOTNET_INSTALL" --channel 9.0 --install-dir /home/z/.dotnet --no-path 2>&1 | tail -2
fi
export DOTNET_ROOT=/home/z/.dotnet
export PATH="$DOTNET_ROOT:$PATH"
echo "  SDK версии: $(dotnet --list-sdks | tr '\n' ' ')"
echo ""

# ── Шаг 2: Godot 4.7.1 ──────────────────────────────────────────────────────
echo "── Шаг 2: Godot 4.7.1 ──"
if [ -x /home/z/godot/Godot_v4.7.1-stable_mono_linux_x86_64/Godot_v4.7.1-stable_mono_linux.x86_64 ]; then
    echo "  Godot уже установлен"
else
    echo "  Скачиваю Godot 4.7.1 .NET..."
    curl -sSL "$GODOT_URL" -o /tmp/godot471.zip
    mkdir -p /home/z/godot
    cd /home/z/godot
    unzip -o /tmp/godot471.zip > /dev/null 2>&1
    chmod +x Godot_v4.7.1-stable_mono_linux_x86_64/Godot_v4.7.1-stable_mono_linux.x86_64
    echo "  Godot установлен: $(/home/z/godot/Godot_v4.7.1-stable_mono_linux_x86_64/Godot_v4.7.1-stable_mono_linux.x86_64 --version)"
fi
echo ""

# ── Шаг 3: Клонирование Ai-game4 ────────────────────────────────────────────
echo "── Шаг 3: Ai-game4 репозиторий ──"
if [ -d /home/z/my-project/Ai-game4/.git ]; then
    echo "  Репозиторий существует, обновляю..."
    cd /home/z/my-project/Ai-game4
    git remote set-url origin "https://x-access-token:${TOKEN}@github.com/vivasua-collab/Ai-game4.git"
    git pull --ff-only 2>&1 | tail -3
    git remote set-url origin "$REPO_URL"
else
    echo "  Клонирую репозиторий..."
    cd /home/z/my-project
    git clone "https://x-access-token:${TOKEN}@github.com/vivasua-collab/Ai-game4.git" 2>&1 | tail -3
    cd Ai-game4
    git remote set-url origin "$REPO_URL"
fi
echo ""

# ── Шаг 4: Симлинки ─────────────────────────────────────────────────────────
echo "── Шаг 4: Симлинки ──"
# Удаляем старые симлинки (могут быть битыми или директориями)
rm -rf /home/z/my-project/game /home/z/my-project/game-docs /home/z/my-project/godot 2>/dev/null
ln -sf /home/z/my-project/Ai-game4/game /home/z/my-project/game
ln -sf /home/z/my-project/Ai-game4/docs /home/z/my-project/game-docs
ln -sf /home/z/godot /home/z/my-project/godot
echo "  game      → $(readlink /home/z/my-project/game)"
echo "  game-docs → $(readlink /home/z/my-project/game-docs)"
echo "  godot     → $(readlink /home/z/my-project/godot)"
echo ""

# ── Шаг 5: NuGet.config (локальный, не в git) ───────────────────────────────
echo "── Шаг 5: NuGet.config ──"
cat > /home/z/my-project/Ai-game4/game/NuGet.config << 'NUGET'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
NUGET
echo "  NuGet.config создан"
echo ""

# ── Шаг 6: Верификация ──────────────────────────────────────────────────────
echo "── Шаг 6: Верификация сборки ──"
cd /home/z/my-project/Ai-game4/game
dotnet build 2>&1 | tail -5
echo ""

echo "── Шаг 7: Headless проверка ──"
GODOT=/home/z/godot/Godot_v4.7.1-stable_mono_linux_x86_64/Godot_v4.7.1-stable_mono_linux.x86_64
timeout 15 "$GODOT" --headless --path . --quit 2>&1 | grep -E "(GameBoot|MainMenu|Started)" | head -5
echo ""

echo "=== Восстановление завершено ==="
echo "Время: $(date)"
echo ""
echo "Симлинки:"
for link in /home/z/my-project/game /home/z/my-project/game-docs /home/z/my-project/godot; do
    if test -e "$link"; then echo "  ✅ $link"; else echo "  ❌ $link (BROKEN)"; fi
done

#!/usr/bin/env bash
# =============================================================================
# COLD START — Чистая процедура холодного старта sandbox
# =============================================================================
# Дизайн: docs/docs_v2/09_workflow/COLD_START.md
#
# ОБНОВЛЕНО 2026-08-26 (аудит среды):
#   - unzip в песочнице НЕНАДЁЖЕН (созданные им файлы "мерцают"/исчезают):
#     извлечение Godot — ТОЛЬКО python-zipfile.
#   - В официальном zip имена с UNDERSCORE (linux_x86_64), все скрипты
#     проекта используют DOT (linux.x86_64) → нормализация при извлечении.
#   - Godot ставится в ПЕРСИСТЕНТНЫЙ my-project/godot (реальная директория,
#     переживает сбои песочницы); /home/z/godot — симлинк (легаси-пути).
#
# Запуск:
#   bash /home/z/my-project/Ai-game4/cold_start.sh
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
GODOT_DIR="$SANDBOX/godot"
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

# ─── Шаг 2: Godot 4.7.1 .NET (python-извлечение!) ────────────────
echo "── Шаг 2/6: Godot 4.7.1 ──"
GODOT_BIN="$GODOT_DIR/Godot_v4.7.1-stable_mono_linux.x86_64/Godot_v4.7.1-stable_mono_linux.x86_64"
if python3 -c "import os,sys; sys.exit(0 if os.path.exists('$GODOT_BIN') and os.path.getsize('$GODOT_BIN')==145073296 else 1)" 2>/dev/null; then
    echo "  ✅ Уже установлен: $GODOT_DIR"
else
    ZIP=/tmp/godot471.zip
    if ! python3 -c "import zipfile; zipfile.ZipFile('$ZIP')" 2>/dev/null; then
        echo "  Скачиваю Godot 4.7.1 .NET..."
        curl -sSL "$GODOT_URL" -o "$ZIP"
    fi
    echo "  Извлекаю python-zipfile (dot-нормализация имён)..."
    python3 - "$ZIP" <<'PYEXTRACT'
import os, sys, zipfile, shutil
zp = sys.argv[1]
base = '/home/z/my-project/godot'
bin_size = 145073296
dotdir = os.path.join(base, 'Godot_v4.7.1-stable_mono_linux.x86_64')
binpath = os.path.join(dotdir, 'Godot_v4.7.1-stable_mono_linux.x86_64')
if os.path.exists(binpath) and os.path.getsize(binpath) == bin_size:
    raise SystemExit(0)
if os.path.exists(base):
    shutil.rmtree(base)
os.makedirs(dotdir, exist_ok=True)
with zipfile.ZipFile(zp) as zf:
    for info in zf.infolist():
        if info.filename.endswith('/'):
            continue
        norm = info.filename.replace('linux_x86_64', 'linux.x86_64')
        tgt = os.path.join(base, norm)
        os.makedirs(os.path.dirname(tgt), exist_ok=True)
        with zf.open(info) as f, open(tgt, 'wb') as o:
            while True:
                c = f.read(4 << 20)
                if not c:
                    break
                o.write(c)
        perm = info.external_attr >> 16 & 0o777
        os.chmod(tgt, perm if perm & 0o400 else 0o644)
sz = os.path.getsize(binpath)
assert sz == bin_size, f'binary {sz} != {bin_size}'
print(f'  OK: binary {sz} + GodotSharp извлечены')
PYEXTRACT
fi
echo "  Версия: $(timeout 20 "$GODOT_BIN" --version 2>/dev/null || echo 'check failed')"
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

# ─── Шаг 4: Симлинки ─────────────────────────────────────────────
echo "── Шаг 4/6: Симлинки (aigame4 + legacy /home/z/godot) ──"
# my-project/godot — РЕАЛЬНАЯ директория (не симлинк, переживает сбои).
ln -sf "$REPO_DIR"   "$SANDBOX/aigame4"
ln -sfn "$GODOT_DIR" "/home/z/godot"

echo "  aigame4       → $(readlink "$SANDBOX/aigame4")"
echo "  /home/z/godot → $(readlink /home/z/godot) (legacy)"
echo "  my-project/godot — реальная директория"
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
# Godot .NET needs DOTNET_ROOT to find hostfxr (set above in step 1).
export DOTNET_ROOT="$DOTNET_DIR"
export PATH="$DOTNET_ROOT:$PATH"
timeout 15 "$GODOT_BIN" --headless --path . --quit 2>&1 | grep -E "(GameBoot|MainMenu|Started|Error|hostfxr)" | head -3

echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "  COLD START ЗАВЕРШЁН"
echo "  Время: $(date)"
echo "═══════════════════════════════════════════════════════════════"
echo ""
echo "Симлинки:"
for link in "$SANDBOX/aigame4" "/home/z/godot"; do
    if test -e "$link"; then
        echo "  ✅ $link → $(readlink "$link")"
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
echo "  cd /home/z/my-project/godot             # движок Godot (реальная директория)"

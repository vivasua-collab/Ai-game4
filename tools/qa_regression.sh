#!/usr/bin/env bash
# R17 QA-регрессия: полный прогон headless-симов (после GODOT_NEWGAME=1).
# Использование: bash tools/qa_regression.sh [sim1 sim2 ...] (по умолчанию — все)
set -u
export PATH="/home/z/.dotnet:$PATH"
export DOTNET_ROOT="/home/z/.dotnet"
GODOT="${GODOT_BIN:-/home/z/my-project/godot/Godot_v4.7.2-stable_mono_linux.x86_64/Godot_v4.7.2-stable_mono_linux.x86_64}"
# R23-1: my-project/godot периодически «гасится» платформой (dentry-ghost:
# записи недоступны, exec ENOENT при живом ls). Fallback — стабильная копия
# в /home/z/godot_bin (вне зоны снапшота). Ручной оверрайд: GODOT_BIN=... .
if [ ! -x "$GODOT" ]; then
  ALT="/home/z/godot_bin/Godot_v4.7.2-stable_mono_linux.x86_64/Godot_v4.7.2-stable_mono_linux.x86_64"
  if [ -x "$ALT" ]; then
    GODOT="$ALT"
    echo "[qa_regression] WARN — canonical godot недоступен (dentry-ghost), использую $ALT"
  fi
fi
cd /home/z/my-project/Ai-game4/game || exit 1

# Порядок = исторический реестр TESTING_RULES §0.1 (R17: SAVELOAD+REASSEMBLY расширены)
ALL_SIMS=(
  "COMBAT_SIM:GODOT_COMBAT_SIM"
  "COMBATAI:GODOT_COMBATAI_DEBUG"
  "ANIMALQA:GODOT_ANIMALQA_DEBUG"
  "LOOT:GODOT_LOOT_DEBUG"
  "DOT:GODOT_DOT_DEBUG"
  "SAVELOAD:GODOT_SAVELOAD_DEBUG"
  "KILLFEED:GODOT_KILLFEED_DEBUG"
  "QUEST:GODOT_QUEST_DEBUG"
  "STORAGE:GODOT_STORAGE_DEBUG"
  "HOTBAR:GODOT_HOTBAR_DEBUG"
  "TRASHDROP:GODOT_TRASHDROP_DEBUG"
  "CONTEXT:GODOT_CONTEXT_DEBUG"
  "MODALQA:GODOT_MODALQA_DEBUG"
  "MODAL2:GODOT_MODAL2_DEBUG"
  "L500:GODOT_L500_DEBUG"
  "WEAPONVIS:GODOT_WEAPONVIS_DEBUG"
  "REASSEMBLY:GODOT_REASSEMBLY_DEBUG"
  "CHARGE:GODOT_CHARGE_SIM"
  "AUDIT0922:GODOT_AUDIT0922_DEBUG"
)

if [ "$#" -gt 0 ]; then
  # Аргумент — имя сима (COMBAT_SIM) или пара NAME:VAR. Имя резолвится
  # в env-переменную из ALL_SIMS (иначе сим молча не активируется).
  declare -A VAR_BY_NAME
  for e in "${ALL_SIMS[@]}"; do VAR_BY_NAME["${e%%:*}"]="${e##*:}"; done
  SIMS=()
  for a in "$@"; do
    if [[ "$a" == *:* ]]; then
      SIMS+=("$a")
    elif [ -n "${VAR_BY_NAME[$a]:-}" ]; then
      SIMS+=("$a:${VAR_BY_NAME[$a]}")
    else
      echo "UNKNOWN SIM: $a (доступны: ${!VAR_BY_NAME[*]})" >&2; exit 2
    fi
  done
else
  SIMS=("${ALL_SIMS[@]}")
fi

declare -A RESULTS
FAILED=()
LOG="/tmp/qa_sim_$$.log"
# Аудит-0915 P3: trap — прерывание (Ctrl-C / таймаут сессии) не должно
# оставлять сироту-godot и хвост в /tmp.
SIM_PID=""
cleanup() {
  [ -n "$SIM_PID" ] && kill -9 "$SIM_PID" 2>/dev/null
  rm -f "$LOG"
}
trap cleanup INT TERM EXIT
# Потолок на один сим (сек): VERDICT обычно приходит за 30-120с; если за
# CEILING секунд вердикта нет — сим завис/сломан, kill + FAIL.
CEILING="${QA_CEILING:-240}"
for entry in "${SIMS[@]}"; do
  name="${entry%%:*}"
  var="${entry##*:}"
  echo "─── QA: $name ($var) ─────────────────────────────"
  # NOTE: симы НЕ завершают процесс после VERDICT (мир продолжает
  # тикать) — потому «сторож»: kill через 3с ПОСЛЕ появления вердикта в
  # логе; жёсткий потолок CEILING сек без вердикта. Вердикт ищем во всём
  # выводе (exit 137 от kill — норма, если вердикт уже напечатан).
  : > "$LOG"
  # L500: сим большого мира требует выбора large_world в меню-харнессе
  # (GODOT_NEWGAME_WORLD); остальные — дефолтный QA-мир test_polygon.
  if [ "$name" = "L500" ]; then
    export GODOT_NEWGAME_WORLD=large_world
  else
    unset GODOT_NEWGAME_WORLD
  fi
  env GODOT_NEWGAME=1 "$var=1" "$GODOT" --headless --path . scenes/MainMenu.tscn > "$LOG" 2>&1 &
  PID=$!
  SIM_PID="$PID"
  (
    i=0
    while kill -0 "$PID" 2>/dev/null; do
      if grep -q "VERDICT" "$LOG" 2>/dev/null; then
        sleep 3
        kill -9 "$PID" 2>/dev/null
        break
      fi
      i=$((i+1))
      if [ "$i" -ge "$CEILING" ]; then
        kill -9 "$PID" 2>/dev/null
        break
      fi
      sleep 1
    done
  ) &
  WATCHDOG=$!
  wait "$PID" 2>/dev/null || true
  SIM_PID=""
  kill "$WATCHDOG" 2>/dev/null; wait "$WATCHDOG" 2>/dev/null || true
  VERDICT=$(grep -E "VERDICT: (PASS|FAIL)" "$LOG" | tail -1)
  if echo "$VERDICT" | grep -q "VERDICT: PASS"; then
    RESULTS[$name]="PASS"
    echo "  ✅ $VERDICT"
  else
    RESULTS[$name]="FAIL"
    FAILED+=("$name")
    echo "  ❌ ${VERDICT:-NO VERDICT LINE}"
    grep -E "FAIL|Error|error|Exception" "$LOG" | grep -vE "^ERROR: (BUG|Pages|[0-9]+ RID)" | head -8
  fi
done

echo ""
echo "══════════ QA-РЕГРЕССИЯ: ИТОГ ══════════"
TOTAL=0; OK=0
for entry in "${SIMS[@]}"; do
  name="${entry%%:*}"
  TOTAL=$((TOTAL+1))
  echo "  ${RESULTS[$name]}  $name"
  [ "${RESULTS[$name]}" = "PASS" ] && OK=$((OK+1))
done
echo "────────────────────────────────────────"
echo "  $OK/$TOTAL PASS"
if [ ${#FAILED[@]} -gt 0 ]; then
  echo "  FAILED: ${FAILED[*]}"
  exit 1
fi
echo "  ВСЕ СИМЫ ЗЕЛЁНЫЕ"

#!/usr/bin/env bash
# R17 QA-регрессия: полный прогон headless-симов (после GODOT_NEWGAME=1).
# Использование: bash tools/qa_regression.sh [sim1 sim2 ...] (по умолчанию — все)
set -u
export PATH="/home/z/.dotnet:$PATH"
export DOTNET_ROOT="/home/z/.dotnet"
GODOT="/home/z/my-project/godot/Godot_v4.7.1-stable_mono_linux.x86_64/Godot_v4.7.1-stable_mono_linux.x86_64"
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
  "WEAPONVIS:GODOT_WEAPONVIS_DEBUG"
  "REASSEMBLY:GODOT_REASSEMBLY_DEBUG"
  "CHARGE:GODOT_CHARGE_SIM"
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
  env GODOT_NEWGAME=1 "$var=1" "$GODOT" --headless --path . scenes/MainMenu.tscn > "$LOG" 2>&1 &
  PID=$!
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
  kill "$WATCHDOG" 2>/dev/null; wait "$WATCHDOG" 2>/dev/null || true
  VERDICT=$(grep -E "VERDICT" "$LOG" | head -1)
  if echo "$VERDICT" | grep -q "PASS"; then
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
rm -f "$LOG"
if [ ${#FAILED[@]} -gt 0 ]; then
  echo "  FAILED: ${FAILED[*]}"
  exit 1
fi
echo "  ВСЕ СИМЫ ЗЕЛЁНЫЕ"

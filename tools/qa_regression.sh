#!/usr/bin/env bash
# R17 QA-регрессия: полный прогон headless-симов (после GODOT_NEWGAME=1).
# Использование: bash tools/qa_regression.sh [sim1 sim2 ...] (по умолчанию — все)
set -u
export PATH="/home/z/.dotnet:$PATH"
export DOTNET_ROOT="/home/z/.dotnet"
# ST13-001a-изоляция: детерминизм QA (техники/снаряжение; сид мира уже
# фиксирован) — устраняет флаки харнессов от случайного оружия NPC.
export GODOT_QA_SEED="${GODOT_QA_SEED:-20261023}"
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
  # Плейтест-раунд 1 (23.10, PT-001): экран смерти + сброс агро NPC/зверей
  # на смерть игрока (шаг 5) — прежде в раннере отсутствовал.
  "RESPAWN:GODOT_RESPAWN_DEBUG"
  "ANIMALQA:GODOT_ANIMALQA_DEBUG"
  "LOOT:GODOT_LOOT_DEBUG"
  "DOT:GODOT_DOT_DEBUG"
  "SAVELOAD:GODOT_SAVELOAD_DEBUG"
  # Этап-4 аудита (20.10): полный GameSession.LoadGame-путь — pre-flight,
  # транзакционный откат, полнота v2, BUG-013 спавнер, GAP-014 группы.
  "STAGE4:GODOT_STAGE4_DEBUG"
  # Этап-5 аудита (22.10): тело=истина смерти (BUG-015), сплит сердца
  # (BUG-016), DoT-зачёт квестов + атрибуция (BUG-017).
  "STAGE5:GODOT_STAGE5_DEBUG"
  # Этап-6 аудита (22.10): инвентарь/торговля/экономика (BUG-018..021).
  "STAGE6:GODOT_STAGE6_DEBUG"
  # Этап-7 аудита (22.10): квесты/диалоги/NPC (BUG-022..024, GAP-025).
  "STAGE7:GODOT_STAGE7_DEBUG"
  # Этап-8 аудита (22.10): техники/Ци/эффекты (ST8-01..06).
  "STAGE8:GODOT_STAGE8_DEBUG"
  # Этап-9 аудита (23.10): инвентарь/экипировка/переносы (ST9-01..03,
  # прочность = GAP).
  "STAGE9:GODOT_STAGE9_DEBUG"
  # Этап-10 аудита (23.10): жизненный цикл мира (ST10-01..04).
  "STAGE10:GODOT_STAGE10_DEBUG"
  # Этап-11 аудита (23.10): тело/травмы/морфологии (ST11-001..007,
  # HoT-распределение и Amorphous Core = GAP).
  "STAGE11:GODOT_STAGE11_DEBUG"
  # Этап-12 аудита (23.10): баффы/статы/Ци/культивация (ST12-001..006
  # + прогрессия: дельты/сон/медитация/прорывы).
  "STAGE12:GODOT_STAGE12_DEBUG"
  # Этап-13 аудита (23.10): ИИ NPC/навигация/группы/животные (ST13-001..006;
  # детект по радиусу — норма §9.1, перки/лестница Attitude — GAP).
  "STAGE13:GODOT_STAGE13_DEBUG"
  # Этап-14 аудита (23.10): ввод/фокус/модальные окна (ST14-001..006;
  # OS-путь потери фокуса — физический PT-U06).
  "STAGE14:GODOT_STAGE14_DEBUG"
  # Этап-15 аудита (23.10): генераторы/валидация/дедуп/ID/реестры
  # (ST15-001..008; cold-load — парой процессов GODOT_STAGE15_COLD).
  "STAGE15:GODOT_STAGE15_DEBUG"
  # Этап-16 аудита (23.10): рендеринг/анимация/синхронизация визуального
  # состояния (ST16-001..009; фактический walk-рейшо зверей — Xvfb-прогон).
  "STAGE16:GODOT_STAGE16_DEBUG"
  # Этап-17 аудита (24.10): инициализация/конфигурация/настройки/выносливость
  # (ST17-001..010; cold-пара — GODOT_STAGE17_COLD=save|load).
  "STAGE17:GODOT_STAGE17_DEBUG"
  # Этап-18 аудита (24.10): интеграция подсистем/длительная сессия
  # (ST18-001..011; cold-пара — GODOT_STAGE18_COLD=save|load).
  "STAGE18:GODOT_STAGE18_DEBUG"
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
  # R37-c (баг-репорт 23.09): зарядник Ци — окно H/мост/анти-дюп/пояс-гейт.
  "CHARGERQA:GODOT_CHARGERQA_DEBUG"
  # G0 (25.09): подготовка спрайтов — кэш/загрузчик/аниматоры/fallback.
  "ANIMQA:GODOT_ANIMQA_DEBUG"
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
  # STAGE15 cold-load (ST15-008): НАСТОЯЩАЯ пара процессов — SAVE-фаза
  # (маркер в каталоге → слот) и LOAD-фаза (новый процесс → LoadGame →
  # маркер резолвится, новые ID уникальны). Подмена внутрипроцессной
  # проверкой запрещена каноном этапа (audit §4 ST15-008).
  if [ "$name" = "STAGE15" ]; then
    echo "─── QA: STAGE15-COLD-SAVE ─────────────────────"
    : > "$LOG"
    env GODOT_NEWGAME=1 GODOT_STAGE15_DEBUG=1 GODOT_STAGE15_COLD=save "$GODOT" --headless --path . scenes/MainMenu.tscn > "$LOG" 2>&1 &
    PID=$!; SIM_PID="$PID"
    ( i=0; while kill -0 "$PID" 2>/dev/null; do
        if grep -q "VERDICT" "$LOG" 2>/dev/null; then sleep 3; kill -9 "$PID" 2>/dev/null; break; fi
        i=$((i+1)); if [ "$i" -ge "$CEILING" ]; then kill -9 "$PID" 2>/dev/null; break; fi
        sleep 1
      done ) &
    WATCHDOG=$!; wait "$PID" 2>/dev/null || true; SIM_PID=""
    kill "$WATCHDOG" 2>/dev/null; wait "$WATCHDOG" 2>/dev/null || true
    COLD_SAVE=$(grep -E "COLD-SAVE" "$LOG" | tail -1)
    echo "$COLD_SAVE"
    if echo "$COLD_SAVE" | grep -q " PASS "; then :; else
      RESULTS[$name]="FAIL"; FAILED+=("STAGE15-COLD-SAVE")
      echo "  ❌ cold-save провален"; fi
    echo "─── QA: STAGE15-COLD-LOAD (НОВЫЙ процесс) ─────"
    # NOTE: GODOT_NEWGAME=1 нужен, чтобы поднялась world-сцена с хуком сима
    # (сим — узел GameWorldController). Каталог при буте НОВОГО процесса пуст;
    # LoadGame накладывает сейв поверх — холодная семантика сохранена.
    : > "$LOG"
    env GODOT_NEWGAME=1 GODOT_STAGE15_DEBUG=1 GODOT_STAGE15_COLD=load "$GODOT" --headless --path . scenes/MainMenu.tscn > "$LOG" 2>&1 &
    PID=$!; SIM_PID="$PID"
    ( i=0; while kill -0 "$PID" 2>/dev/null; do
        if grep -q "VERDICT" "$LOG" 2>/dev/null; then sleep 3; kill -9 "$PID" 2>/dev/null; break; fi
        i=$((i+1)); if [ "$i" -ge "$CEILING" ]; then kill -9 "$PID" 2>/dev/null; break; fi
        sleep 1
      done ) &
    WATCHDOG=$!; wait "$PID" 2>/dev/null || true; SIM_PID=""
    kill "$WATCHDOG" 2>/dev/null; wait "$WATCHDOG" 2>/dev/null || true
    COLD_LOAD=$(grep -E "COLD-LOAD" "$LOG" | tail -1)
    echo "$COLD_LOAD"
    if echo "$COLD_LOAD" | grep -q " PASS "; then :; else
      RESULTS[$name]="FAIL"; FAILED+=("STAGE15-COLD-LOAD")
      echo "  ❌ cold-load провален (холодная загрузка каталога)"; fi
  fi
  # STAGE17 cold-пара (ST17-003/F): пара НОВЫХ процессов — SAVE-фаза (частичная
  # выносливость → слот) и LOAD-фаза (новый процесс → LoadGame → значение
  # восстановлено из файла, не из памяти). Канон этапа-17 §3 F.
  if [ "$name" = "STAGE17" ]; then
    echo "─── QA: STAGE17-COLD-SAVE ─────────────────────"
    : > "$LOG"
    env GODOT_NEWGAME=1 GODOT_STAGE17_DEBUG=1 GODOT_STAGE17_COLD=save "$GODOT" --headless --path . scenes/MainMenu.tscn > "$LOG" 2>&1 &
    PID=$!; SIM_PID="$PID"
    ( i=0; while kill -0 "$PID" 2>/dev/null; do
        if grep -q "VERDICT" "$LOG" 2>/dev/null; then sleep 3; kill -9 "$PID" 2>/dev/null; break; fi
        i=$((i+1)); if [ "$i" -ge "$CEILING" ]; then kill -9 "$PID" 2>/dev/null; break; fi
        sleep 1
      done ) &
    WATCHDOG=$!; wait "$PID" 2>/dev/null || true; SIM_PID=""
    kill "$WATCHDOG" 2>/dev/null; wait "$WATCHDOG" 2>/dev/null || true
    COLD_SAVE=$(grep -E "COLD-SAVE" "$LOG" | tail -1)
    echo "$COLD_SAVE"
    if echo "$COLD_SAVE" | grep -q " PASS "; then :; else
      RESULTS[$name]="FAIL"; FAILED+=("STAGE17-COLD-SAVE")
      echo "  ❌ cold-save провален"; fi
    echo "─── QA: STAGE17-COLD-LOAD (НОВЫЙ процесс) ─────"
    : > "$LOG"
    env GODOT_NEWGAME=1 GODOT_STAGE17_DEBUG=1 GODOT_STAGE17_COLD=load "$GODOT" --headless --path . scenes/MainMenu.tscn > "$LOG" 2>&1 &
    PID=$!; SIM_PID="$PID"
    ( i=0; while kill -0 "$PID" 2>/dev/null; do
        if grep -q "VERDICT" "$LOG" 2>/dev/null; then sleep 3; kill -9 "$PID" 2>/dev/null; break; fi
        i=$((i+1)); if [ "$i" -ge "$CEILING" ]; then kill -9 "$PID" 2>/dev/null; break; fi
        sleep 1
      done ) &
    WATCHDOG=$!; wait "$PID" 2>/dev/null || true; SIM_PID=""
    kill "$WATCHDOG" 2>/dev/null; wait "$WATCHDOG" 2>/dev/null || true
    COLD_LOAD=$(grep -E "COLD-LOAD" "$LOG" | tail -1)
    echo "$COLD_LOAD"
    if echo "$COLD_LOAD" | grep -q " PASS "; then :; else
      RESULTS[$name]="FAIL"; FAILED+=("STAGE17-COLD-LOAD")
      echo "  ❌ cold-load провален (холодная загрузка выносливости)"; fi
  fi
  # STAGE18 cold-пара (ST18-006/F): НОВЫЙ процесс на фазу — SAVE (интеграционная
  # метка: стамина+позиция → слот) и LOAD (новый процесс → LoadGame → метка
  # восстановлена из файла). Канон этапа-18 §3 F (как STAGE15/17).
  if [ "$name" = "STAGE18" ]; then
    echo "─── QA: STAGE18-COLD-SAVE ─────────────────────"
    : > "$LOG"
    env GODOT_NEWGAME=1 GODOT_STAGE18_DEBUG=1 GODOT_STAGE18_COLD=save "$GODOT" --headless --path . scenes/MainMenu.tscn > "$LOG" 2>&1 &
    PID=$!; SIM_PID="$PID"
    ( i=0; while kill -0 "$PID" 2>/dev/null; do
        if grep -q "VERDICT" "$LOG" 2>/dev/null; then sleep 3; kill -9 "$PID" 2>/dev/null; break; fi
        i=$((i+1)); if [ "$i" -ge "$CEILING" ]; then kill -9 "$PID" 2>/dev/null; break; fi
        sleep 1
      done ) &
    WATCHDOG=$!; wait "$PID" 2>/dev/null || true; SIM_PID=""
    kill "$WATCHDOG" 2>/dev/null; wait "$WATCHDOG" 2>/dev/null || true
    COLD_SAVE=$(grep -E "COLD-SAVE" "$LOG" | tail -1)
    echo "$COLD_SAVE"
    if echo "$COLD_SAVE" | grep -q " PASS "; then :; else
      RESULTS[$name]="FAIL"; FAILED+=("STAGE18-COLD-SAVE")
      echo "  ❌ cold-save провален"; fi
    echo "─── QA: STAGE18-COLD-LOAD (НОВЫЙ процесс) ─────"
    : > "$LOG"
    env GODOT_NEWGAME=1 GODOT_STAGE18_DEBUG=1 GODOT_STAGE18_COLD=load "$GODOT" --headless --path . scenes/MainMenu.tscn > "$LOG" 2>&1 &
    PID=$!; SIM_PID="$PID"
    ( i=0; while kill -0 "$PID" 2>/dev/null; do
        if grep -q "VERDICT" "$LOG" 2>/dev/null; then sleep 3; kill -9 "$PID" 2>/dev/null; break; fi
        i=$((i+1)); if [ "$i" -ge "$CEILING" ]; then kill -9 "$PID" 2>/dev/null; break; fi
        sleep 1
      done ) &
    WATCHDOG=$!; wait "$PID" 2>/dev/null || true; SIM_PID=""
    kill "$WATCHDOG" 2>/dev/null; wait "$WATCHDOG" 2>/dev/null || true
    COLD_LOAD=$(grep -E "COLD-LOAD" "$LOG" | tail -1)
    echo "$COLD_LOAD"
    if echo "$COLD_LOAD" | grep -q " PASS "; then :; else
      RESULTS[$name]="FAIL"; FAILED+=("STAGE18-COLD-LOAD")
      echo "  ❌ cold-load провален (холодная загрузка интеграционной метки)"; fi
    # ST18-004/D4-COLD: план частичного сна переживает cold-load (иначе —
    # вечный сон в новом процессе; in-process тест слеп из-за протухших полей).
    D4COLD=$(grep -E "D4-COLD" "$LOG" | tail -1)
    echo "$D4COLD"
    if echo "$D4COLD" | grep -q " PASS "; then :; else
      RESULTS[$name]="FAIL"; FAILED+=("STAGE18-D4-COLD")
      echo "  ❌ D4-COLD провален (план сна теряется в новом процессе)"; fi
    # FA-1 (final-audit follow-up): crash-целостность — намеренное исключение
    # сценария H (GODOT_STAGE18_FAULT_INJECT=1) обязано давать VERDICT: FAIL.
    # Прежде CRASH писался как GAP и вердикт оставался PASS — ложная зелёность
    # (доказано финальным аудитом). Ожидаемый исход инъекции: FAIL; PASS или
    # отсутствие вердикта = провал QA-агрегации, а не «пройденный» тест.
    echo "─── QA: STAGE18-FAULT-INJECT (ожидание: VERDICT: FAIL) ──"
    : > "$LOG"
    env GODOT_NEWGAME=1 GODOT_STAGE18_DEBUG=1 GODOT_STAGE18_FAULT_INJECT=1 "$GODOT" --headless --path . scenes/MainMenu.tscn > "$LOG" 2>&1 &
    PID=$!; SIM_PID="$PID"
    ( i=0; while kill -0 "$PID" 2>/dev/null; do
        if grep -q "VERDICT" "$LOG" 2>/dev/null; then sleep 3; kill -9 "$PID" 2>/dev/null; break; fi
        i=$((i+1)); if [ "$i" -ge "$CEILING" ]; then kill -9 "$PID" 2>/dev/null; break; fi
        sleep 1
      done ) &
    WATCHDOG=$!; wait "$PID" 2>/dev/null || true; SIM_PID=""
    kill "$WATCHDOG" 2>/dev/null; wait "$WATCHDOG" 2>/dev/null || true
    FA_VERDICT=$(grep -E "VERDICT: (PASS|FAIL)" "$LOG" | tail -1)
    echo "$FA_VERDICT"
    if echo "$FA_VERDICT" | grep -q "VERDICT: FAIL"; then :; else
      RESULTS[$name]="FAIL"; FAILED+=("STAGE18-FAULT-INJECT")
      echo "  ❌ fault-inject не покраснел (исключение сценария не даёт FAIL — агрегация врёт)"; fi
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

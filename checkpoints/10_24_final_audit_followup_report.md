# [FINAL AUDIT FOLLOW-UP] QA Crash Integrity — закрытие ST18-011

Дата: 2026-10-24. Задание: итог финального аудита (ревьювер) — «QA Crash
Integrity и закрытие Stage18-011».

## 0. База и коммиты

- База аудита: `b04ef544b1af09225110f00ef567e774b7d26bac` (публичный main,
  состояние на момент финального аудита).
- Фикс: **`f129408`** — `fix(qa): FA-1 crash-целостность Stage18Sim`:
  - `game/src/Adapter/Scene/Stage18SubsystemIntegrationTruthSimDebug.cs`:
    `RunScenario` — CRASH = FAIL + fatal-счётчик (`_fatalCrashes`), не GAP;
    `PrintSummary` — PASS запрещён при любом FAIL ИЛИ CRASH, счётчик CRASH
    в сводке; cold-фаза и ScenarioI переведены под crash-защиту `RunScenario`;
    fault-inject-хук `GODOT_STAGE18_FAULT_INJECT=1` (намеренное исключение
    в сценарии H).
  - `tools/qa_regression.sh`: шаг `STAGE18-FAULT-INJECT` — ожидание
    `VERDICT: FAIL`; зелёная инъекция или отсутствие вердикта =
    `FAILED+=(STAGE18-FAULT-INJECT)`.
  - `docs/docs_v2/09_workflow/TESTING_RULES.md` §0.1: crash-политика + инъекция.
- Отчёт/логи/ворклог: финальный docs-коммит (см. `git log`).

## 1. Подтверждённый дефект (префикс-пруф на базе b04ef54)

Дефект из аудита: `RunScenario()` перехватывал исключение сценария и писал
`CRASH/<имя>` как **GAP**; `PrintSummary()` ставил `VERDICT: PASS` при
`FAIL == 0 && PASS > 0`. Падение одного сценария могло прятаться за зелёным
итогом.

Воспроизведение (контролируемая инъекция того же исключения в сценарий H,
добавленная ТОЛЬКО к старому коду):

```
команда: env GODOT_QA_SEED=20261023 GODOT_NEWGAME=1 GODOT_STAGE18_DEBUG=1 \
  GODOT_STAGE18_FAULT_INJECT=1 <godot> --headless --path . scenes/MainMenu.tscn
результат (старая агрегация):
  GAP        CRASH/H — необработанное исключение: InvalidOperationException: ...
  PASS=30  FAIL=0  GAP=2  NOT TESTED=0
  [Stage18Sim] VERDICT: PASS        ← ЛОЖНАЯ ЗЕЛЁНОСТЬ (доказано)
```

Лог: `checkpoints/10_24_final_audit_followup_prefix_faultinject_base_b04ef54.log`.

## 2. Минимальное исправление и постфикс-пруф

Изменения агрегации (весь дифф — `git show f129408`):

1. `RunScenario`: исключение сценария → `Record("CRASH/<имя>", false, …)`
   (FAIL) **плюс** инкремент `_fatalCrashes` (защита в глубину: даже если
   счётчик FAIL когда-нибудь сломают, вердикт всё равно красный).
2. `RecordGap()` сохранён только для явно задокументированных ограничений
   окружения (вызовы по месту в сценариях A/C/E/G — не тронуты).
3. `PrintSummary`: `verdictPass = fail == 0 && _fatalCrashes == 0 && pass > 0`;
   сводка печатает `PASS/FAIL/CRASH/GAP/NT`; GAP/NT перечисляются и в
   отчёт передаются отдельно (не маскируются).
4. Cold-фаза (`RunColdPhase`) и `ScenarioI` — под тем же `RunScenario`:
   прежде исключение в cold-фазе гасило fire-and-forget `RunAsync` ВООБЩЕ
   без вердикта (раннер застрахован `NO VERDICT LINE`→FAIL, но прямые
   запуски теряли итог; ScenarioI был синхронным без try/catch).
5. Совместимость с раннером: формат `[Stage18Sim] VERDICT: PASS|FAIL`
   не изменён; раннер по-прежнему ищет `VERDICT: (PASS|FAIL)` (проверено
   прогоном ниже).

Постфикс (тот же сбой на исправленном коде):

```
результат (новая агрегация):
  FAIL       CRASH/H — необработанное исключение: InvalidOperationException: ...
  PASS=30  FAIL=1  CRASH=1  GAP=1  NOT TESTED=0
  [Stage18Sim] VERDICT: FAIL        ← КРАСНЫЙ при 30 зелёных сценариях
```

Лог: `checkpoints/10_24_final_audit_followup_postfix_faultinject.log`.

Нормальный постфикс (без инъекции) — регресс проверок не ослаблен:

```
  PASS=31  FAIL=0  CRASH=0  GAP=1  NOT TESTED=0
  [Stage18Sim] VERDICT: PASS
```

Лог: `checkpoints/10_24_final_audit_followup_postfix_normal.log`
(31 PASS — та же сумма, что в постфиксе Этапа-18 `b031887`; пофазовые
результаты A–I идентичны — сверка diff построчно).

## 3. Раннер распознаёт и красный, и зелёный

Таблица решений шага `STAGE18-FAULT-INJECT` (и всего блока STAGE18):

| Ситуация | Сим печатает | Шаг раннера | Итог раннера |
|---|---|---|---|
| Старая агрегация + инъекция (база) | `VERDICT: PASS` (ложный) | ❌ `FAILED+=(STAGE18-FAULT-INJECT)` | exit 1 |
| Новая агрегация + инъекция | `VERDICT: FAIL` (ожидаемый) | ✅ шаг зелёный | STAGE18 PASS |
| Реальный сбой любого теста STAGE18 | `VERDICT: FAIL` | основной чек ❌ | exit 1 |
| Нет вердикта (завис/тихая смерть) | — | ❌ `NO VERDICT LINE` | exit 1 |

Доказательство строки 2 — прогон раннера (`bash tools/qa_regression.sh STAGE18`,
41 c): основной PASS + cold-save PASS + cold-load PASS + D4-COLD PASS +
FAULT-INJECT `VERDICT: FAIL` → **1/1 PASS, ВСЕ СИМЫ ЗЕЛЁНЫЕ**.
Лог: `checkpoints/10_24_final_audit_followup_runner_stage18.log`.
Строка 1 следует из префикс-лога §1 (тот же grep, что в раннере: вердикт
PASS → шаг красный) + кода шага в `tools/qa_regression.sh`.

## 4. Сборка и cold-пара

- `dotnet build --no-incremental` (game/): **0 Error**, 735 предупреждений
  (всеpre-existing, не из зоны фикса).
  Лог: `checkpoints/10_24_final_audit_followup_build.log`.
- Cold-пара (НОВЫЙ процесс на фазу, канон §3 F):
  - SAVE: `PASS ST18-006/COLD-SAVE` (стамина 61 + позиция + Ци + частичный
    сон 31/120 мин) → `checkpoints/10_24_final_audit_followup_cold_save.log`
  - LOAD: `PASS ST18-006/COLD-LOAD` + `PASS ST18-004/D4-COLD`
    (пробуждение на 89-м тике, окно 60–95) →
    `checkpoints/10_24_final_audit_followup_cold_load.log`

## 5. Три последовательных полных регрессии

Метод: foreground-чанки (5 чанков на прогон; фоновый прогон целиком убивается
платформой между вызовами — задокументировано в Этапе-16/18). Каждый чанк —
полный вызов `bash tools/qa_regression.sh <симы…>`, stdout сохранён целиком.
37 симов на прогон; STAGE18 в каждом прогоне — с cold-парой и шагом
FAULT-INJECT.

| Прогон | Прямой итог | Флаки (предсуществующие) | Изолированные повторы | Итог |
|---|---|---|---|---|
| №1 | 35/37 | COMBAT_SIM bug012; STAGE14 C3 Esc-стек | оба зелёные | 37/37 |
| №2 | 35/37 | COMBAT_SIM bug012; STAGE8 D6a зарядка world-reset | COMBAT_SIM зелёный; STAGE8 1-й повтор FAIL (флака ~50%), 2-й зелёный | 37/37 |
| №3 | 37/37 | — | — | 37/37 без повторов |

Каждый прогон: STAGE18 основной PASS + COLD-SAVE/LOAD/D4-COLD PASS +
FAULT-INJECT ожидаемо `VERDICT: FAIL` (шаг зелёный) — т.е. инъекция
сработала 4/4 прогонов раннера (1 изолированный + 3 полных).

Логи (полные, по чанкам, включая неудачные попытки — ничего не
переименовано и не удалено):
- `checkpoints/10_24_final_audit_followup_regression_run1.log`
- `checkpoints/10_24_final_audit_followup_regression_run2.log`
- `checkpoints/10_24_final_audit_followup_regression_run3.log`

### Флаки: статус и причины (вне зоны FA-1)

- **COMBAT_SIM bug012** («ранний гейт Support не сработал», посл. отказ пуст):
  известная флака, отмечена в заголовке регрессии Этапа-18 (b031887–b04ef54);
  в изоляции зелёный в обоих прогонах. Код CombatSim не менялся.
- **STAGE8 D6a** («зарядка для world-reset не стартовала» → NOT TESTED;
  вердикт сима строго краснеет при NT): недетерминизм генерации NPC/техник
  (ID NPC различаются между прогонами при том же QA-seed — сид покрывает не
  весь пайплайн). Та же флака была в сессии Этапа-17 (файл
  `checkpoints/10_24_stage17_regression_stage8_rerun.log` существует именно
  поэтому). Частота ~50% в этой сессии; зелёный на повторе.
- **STAGE14 C3** (Esc-стек B→T: порядок закрытия): одиночное проявление в
  прогоне №1, в изоляции и в прогонах №2/№3 зелёный.

Ни одна из флак не связана с FA-1: дифф фикса не пересекается с
CombatSim/Stage8/Stage14 (только Stage18-сим + блок STAGE18 раннера + docs).
Все зафиксированы честно, «неудачные» запуски сохранены в логах как есть.

## 6. Статус ST18-011

**CONFIRMED_FIXED — ST18-011 закрывается.** Условие аудита выполнено:
- намеренно сломанный сценарий (инъекция в H) даёт `VERDICT: FAIL`
  (постфикс-пруф §2), на старом коде тот же сбой давал ложный PASS
  (префикс-пруф §1);
- регрессионный раннер распознаёт оба исхода (таблица §3, прогоны §5);
- успешно пройденные сценарии выдают прежние результаты (постфикс 31/0/0/1,
  пофазовые идентичны базе; проверки не ослаблены, исключения не подменены
  тестовыми GAP — RecordGap остался только для документированных
  ограничений).

## 7. Остаётся открытым (владелец решает; FA-1 их не закрывает)

1. Несейв-домены: активные баффы и наземные предметы не входят в сейв
   (задокументированное поведение, GAP-заметки сценариев A/B) — семантику
   утверждает владелец.
2. Визуальные проверки PT-V01–V08 (анимации, Y-sort, оружие, VFX) —
   headless-сим их не заменяет.
3. Физическая потеря фокуса ОС (PT-U06-смежное) — вне headless.
4. Расход выносливости действиями (бег/атаки) и HUD-полоса стамины —
   сервис готов, подключение не выполнено (дорожная карта).
5. Флаки COMBAT_SIM bug012 / STAGE8 D6a / STAGE14 C3 — предсуществующая
   недетерминированность QA-мира (сид покрывает не всю генерацию);
   кандидаты на отдельный тикет стабилизации сид-покрытия.
6. Контентная программа M6/M7 — не начата (статус дорожной карты).

## 8. Команды для воспроизведения

```bash
export PATH="/home/z/.dotnet:$PATH"; export DOTNET_ROOT="/home/z/.dotnet"
GODOT=/home/z/my-project/godot/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
cd game
dotnet build --no-incremental                                   # сборка
env GODOT_QA_SEED=20261023 GODOT_NEWGAME=1 GODOT_STAGE18_DEBUG=1 \
  $GODOT --headless --path . scenes/MainMenu.tscn                # норма: PASS
env GODOT_QA_SEED=20261023 GODOT_NEWGAME=1 GODOT_STAGE18_DEBUG=1 \
  GODOT_STAGE18_FAULT_INJECT=1 $GODOT --headless --path . scenes/MainMenu.tscn   # инъекция: FAIL
env GODOT_QA_SEED=20261023 GODOT_NEWGAME=1 GODOT_STAGE18_DEBUG=1 \
  GODOT_STAGE18_COLD=save $GODOT --headless --path . scenes/MainMenu.tscn        # cold-save
env GODOT_QA_SEED=20261023 GODOT_NEWGAME=1 GODOT_STAGE18_DEBUG=1 \
  GODOT_STAGE18_COLD=load $GODOT --headless --path . scenes/MainMenu.tscn        # cold-load
cd .. && bash tools/qa_regression.sh STAGE18                     # контур раннера
bash tools/qa_regression.sh <чанки по §0.1>                      # полная регрессия
```

## 9. Артефакты сессии

- `checkpoints/10_24_final_audit_followup_build.log` — полная сборка (0 ошибок).
- `checkpoints/10_24_final_audit_followup_prefix_faultinject_base_b04ef54.log` — префикс: ложный PASS на базе.
- `checkpoints/10_24_final_audit_followup_postfix_normal.log` — норма 31/0/0/1.
- `checkpoints/10_24_final_audit_followup_postfix_faultinject.log` — инъекция: FAIL.
- `checkpoints/10_24_final_audit_followup_cold_save.log` / `_cold_load.log` — cold-пара.
- `checkpoints/10_24_final_audit_followup_runner_stage18.log` — раннер: 1/1 PASS с шагом FAULT-INJECT.
- `checkpoints/10_24_final_audit_followup_regression_run{1,2,3}.log` — три полных прогона (37/37 каждый, с честной фиксацией флак).
- `checkpoints/10_24_final_audit_followup.log` — сводный лог сессии.
- `docs/docs_v2/09_workflow/AGENT_FILE_EDITING_NOTES.md` — побочная задача владельца (механика правок/размер файлов/комментарии).

# Чекпоинт: Этап-17 аудита — Инициализация, конфигурация, настройки и полная выносливость

**Дата:** 2026-10-24 (сессия coder-1024)
**Канон:** [STAGE 17 AUDIT] от ревьювера (отчёт верификации этапов 15–16, наблюдавшийся HEAD `c05bbb9152505c39b81d7dd1daa8f7127094f5b6`)
**Тип:** postfix-отчёт (префикс → воспроизведение → минимальные исправления → postfix → полная регрессия)

---

## 1. Окружение и SHA-цепочка

- **Godot:** v4.7.2.stable.mono.official.ed1daf0bf (`/home/z/my-project/godot/…`)
- **.NET:** SDK 9.0.319 (`/home/z/.dotnet`; hostfxr виден Godot только при
  `PATH`/`DOTNET_ROOT` в окружении запуска — qa_regression.sh выставляет сам,
  прямые запуски обязаны экспортировать, иначе mono-модуль падает по hostfxr).
- **Наблюдаемый HEAD аудита:** `c05bbb9` — подтверждён как старт этапа
  (working tree clean, `main` не менялся между аудитом и началом работы).
- **SHA-цепочка этапа (коммиты — маленькие логические блоки):**
  - `223d998` — test(stage17): Stage17InitConfigStaminaTruthSimDebug + QA-хуки
    GameSettings (без изменения поведения) + регистрация qa_regression/TESTING_RULES;
    **префикс: 8 PASS / 13 FAIL / 0 GAP / 1 NOT TESTED — VERDICT: FAIL**,
    лог `checkpoints/10_24_stage17_init_config_stamina_prefix.log`.
  - `c0ff043` — fix(stamina): ST17-001..003 (StaminaService + DI + инварианты +
    аккумулятор + сон + ISaveable/IWorldResettable + OptionalBlocksV1).
  - `4ba6d5d` — fix(settings): ST17-004 (JSON-разбор, порядок `_loaded`,
    EnsureLoaded в Set*, своевременное применение в GameWorld._Ready).
  - `e00094f` — fix(init-config): ST17-005/006 (WorldConfig DI-синглтон +
    WorldInitPhase из конфига, InputMap флаг после успеха, legacy-пометки).
  - `c06d152` — fix(residuals): ST17-010 (ST11-006res size-enum валидация +
    ST12-003res дробный остаток) — отдельный логический блок, как требовал канон.
  - (docs-коммит) — этот отчёт + портал + worklog.

## 2. Команды

```bash
export PATH="/home/z/.dotnet:$PATH"; export DOTNET_ROOT="/home/z/.dotnet"
cd game && dotnet build                        # 0 errors; 719 warnings (фонд ДО и ПОСЛЕ этапа не изменился)
# префикс/постфикс (одна и та же команда):
GODOT_NEWGAME=1 GODOT_STAGE17_DEBUG=1 <godot> --headless --path . scenes/MainMenu.tscn
# cold-пара (пара НОВЫХ процессов, ST17-003/F):
GODOT_NEWGAME=1 GODOT_STAGE17_DEBUG=1 GODOT_STAGE17_COLD=save <godot> …
GODOT_NEWGAME=1 GODOT_STAGE17_DEBUG=1 GODOT_STAGE17_COLD=load <godot> …
# регрессия:
bash tools/qa_regression.sh          # 36 симов (35 прежних + STAGE17)
bash tools/qa_regression.sh STAGE8   # изолированный повтор нестабильности
```

Сборка: **0 errors / 719 warnings** — и на префиксной базе, и после всех
фиксов (фонд предупреждений этапом не тронут; расхождение с отчётом этапа-16
«711» объясняется сменой окружения SDK: 8.0.426+9.0.319 → 9.0.319-only).

## 3. Симулятор этапа

`game/src/Adapter/Scene/Stage17InitConfigStaminaTruthSimDebug.cs`, хук
`GODOT_STAGE17_DEBUG=1` (+ `GODOT_STAGE17_COLD=save|load`), зарегистрирован в
`tools/qa_regression.sh` (STAGE17 + cold-пара по образцу STAGE15) и
`docs/docs_v2/09_workflow/TESTING_RULES.md` §0.1. Сценарии A–K по канону §3.
Переиспользует контракты проекта: полный `GameSession.LoadGame`-путь (как
STAGE4), `WorldDomainResetCoordinator` (как этап-11), `ISaveable`-агрегатор,
`BuffTickProcessor`/`ActiveBuff` напрямую. Сим компилируется на префиксном
коде (зависит только от интерфейса `IStaminaService` + QA-хуков GameSettings)
— префикс честно FAIL-ит отсутствием реализации, а не ошибками компиляции.

## 4. Результаты ST17-001..010 (префикс → постфикс)

| ID | Суть | Префикс | Постфикс | Статус |
|----|------|---------|----------|--------|
| ST17-001 | Реализация/регистрация IStaminaService | A1 FAIL: TryResolve=NULL — интерфейс (2026-05-28) без реализации/DI/потребителей; B–G: 8 FAIL «подсистема не существует» | A1 PASS (Modules/Player/StaminaService), A2 PASS (живой тик-луп растит ресурс с нуля), A3 PASS (Max=100 из PlayerConfig) | **CONFIRMED_FIXED** |
| ST17-002 | Инварианты полной выносливости | (в составе ST17-001-FAIL) | B1/B2 PASS (новая игра 1000‰, Current==Max); C1–C4 PASS (расход −200‰ ровно/1 событие; граница до 0 без минуса; недостаток = отказ без частичного списания и без события; 0/−50/1001 = отказ без изменений); D1–D4 PASS (60×1000мс == 1×60000мс; сонная разбивка эквивалентна; большая дельта клампится; ≤0 = no-op) | **CONFIRMED_FIXED** |
| ST17-003 | Жизненный цикл | (в составе ST17-001-FAIL) | E1 PASS (30 мин сна = 500‰ строго по SleepStaminaRecoveryPercentPerHour=100%/ч), E2 PASS (25‰/мин бодрствования по конфигу), F1 PASS (Save@37 → LoadGame → 37/370‰ точно, НЕ максимум), COLD-SAVE/COLD-LOAD PASS (пара НОВЫХ процессов: частичное значение из файла), G1 PASS (ResetAll → полный), G2 PASS (Die→Revive: без авто-восстановления — задокументированное поведение), G3 PASS (легаси-сейв без stamina-блока грузится, пост-ресет дефолт) | **CONFIRMED_FIXED** |
| ST17-004 | GameSettings: Contains-шаблоны, порядок загрузки | H1 FAIL (compact `"cheatsEnabled":false` не парсится — валидный JSON!), H6 FAIL (Set до загрузки затёр сохранённые настройки дефолтами), H7 FAIL (`_loaded=true` до Load блокирует повторную попытку: валидный файл после повреждённого игнорируется) | H1–H7 все PASS (+ H2–H5 PASS и на префиксе: прежний парсер частично работал) | **CONFIRMED_FIXED** |
| ST17-005 | Конфигурации: фактически читаемые поля | DefaultSpeed затирался хардкодом Normal фазой 3 (код-реф); StartLocationId/StartSectorId — 0 читателей | WorldInitPhase читает WorldConfig.DefaultSpeed (DI-синглтон, общий инстанс с WorldModule); мёртвые поля задокументированы (см. §5) | **CONFIRMED_FIXED** (DefaultSpeed); мёртвые поля — **задокументированы, удаление за владельцем** |
| ST17-006 | Последовательность старта и fail-closed | I1–I3 PASS уже на префиксе (P2-13/P1-10 заделы живы: повторный Start без дублей подписок, тики гейтен `!_initialized`, InputMap идемпотентен) | без регрессий; InputMapInitializer `_initialized` перенесён ПОСЛЕ регистраций (fail-retriable контракт) — харденинг | **OK (заделы подтверждены) + харденинг флага**; mid-failure рантайм-воспроизведение невозможно (Godot InputMap API не бросает) — NOT TESTED для самого обрыва |
| ST17-007 | Spawn/NewGame/LoadGame/RestoreState | Сим-рутайм: игрок спавнится ровно один раз (идемпотентный гард PlayerService.Spawn — код-реф + F1/G3 рутайм: LoadGame → Playing без двойных событий) | F1/G3 PASS; NewGame-путь: ResetAll до RestoreState (координатор), LoadGame-путь: pre-flight → backup → reset → restore → post-sync (без изменений — этап-3/4 контракты живы) | **OK (подтверждено)** — отдельных дефектов не найдено |
| ST17-008 | Стражи входных данных | StaminaMax<1 → fallback 100; >2M → кламп; скорости <0 → 0, >10000 → кламп; deltaTime≤0 → no-op; SetCurrent кламп [0..Max]; Spend диапазон [1..1000]; повреждённый settings.json → дефолты+диагностика без крэша (H3); несуществующий SizeClass → диагностика+текущий (J1) | всё в постфиксе PASS | **CONFIRMED_FIXED (в составе подсистем)** |
| ST17-009 | Ошибки завершения процесса из отчёта этапа-16 | K: NOT TESTED рантаймно (сообщения возникают при exit процесса — сим неприменим) | Классификация по лог-доказательству: `string_name.cpp`/`thread.cpp wait_to_finish`/`paged_allocator`/RID `23NavMeshGeometryParser2D/3D` — присутствуют во ВСЕХ headless-логах с 22.09 (r34/r35/r36a/b/r37c, stage13/15) ДО этапа-16 и после — движковый шум выхода Godot 4.7.2, НЕ дефект приложения/этапа-16 | **DISPROVED (как дефект этапа-16/приложения)** — движок/окружение |
| ST17-010 | Остатки этапов 11/12 | J1 FAIL: `_size = (SizeClass)999` слепо кастуется (ST11-006res OPEN); J2 FAIL: интервал 1.0с — 5×0.6с → 2 тика вместо 3 (дрейф +0.6с на 2 интервала, ST12-003res OPEN — рутайм-пруф) | J1 PASS (валидация enum + диагностика + текущий размер сохранён), J2 PASS (остаток сохраняется: 5×0.6с → 3 тика == 3×1.0с → 3 тика), J3 PASS (потолок MaxCatchUpTicks=5 — документированный анти-всплеск не тронут) | **CONFIRMED_FIXED** (оба, отдельным коммитом `c06d152` с отдельными тестами J1–J3, как требовал канон) |

**Постфикс: 32 PASS / 0 FAIL / 0 GAP / 1 NOT TESTED — VERDICT: PASS**
(лог `checkpoints/10_24_stage17_init_config_stamina_report.log`).

### Решения, принятые по выносливости (неоговорённые в аудите — фиксирую явно)

1. **Единицы времени `Regenerate(deltaTimeMs)`** — игровое время: 1 игровая
   минута = 60 000, 1 час = 3 600 000 (канон TIME_SYSTEM §5: 1 тик = 1 игровая
   минута; PlayerModule передаёт `DeltaTime × 60 000`). Доч-несоответствие
   `deltaTimeSec`→`deltaTimeMs` в док-комментарии интерфейса исправлено.
2. **Целочисленная модель** (ЗАПРЕТ 3.9): внутри — milli-единицы (текущая ×1000);
   аккумулятор «scale-остаток» `a += num'·ms; gain = a/den'; a %= den'` с
   НОД-редукцией и чанкованием против переполнения — сумма floor(частей)
   тождественна floor(целого), разбивки delta эквивалентны (D1/D2).
3. **Скорости**: сон — существовавший `SleepStaminaRecoveryPercentPerHour=100`
   (первый читатель появился); бодрствование — новое поле
   `StaminaAwakeRegenPromillePerMinute=25` (полное восстановление за 40 минут).
4. **Death/respawn**: авто-восстановления НЕТ — канон такого требования не
   содержит; ресурс переживает смерть/возрождение неизменным (G2-пруф).
   Реген во время смерти продолжается (время идёт) — документируется здесь.
5. **Легаси-сейвы без блока** — `OptionalBlocksV1` дополнен `"stamina"`
   (семантика «отсутствует = домен в дефолте после сброса»), G3-пруф.
6. **Аккумулятор**: переключение режима сна/бодрствования и достижение полного
   ресурса сбрасывают суб-единичный остаток (потеря ≤ 0.001 единицы) —
   задокументировано в классе; observable-состояние (CurrentMilli) всегда точно.

## 5. ST17-005 — таблица конфигураций

Метод: скриптованный `rg`-обход всех 14 `*Config.cs` (поле → поиск имени вне
файла конфига) + ручная верификация приоритетного списка аудита. Оговорка
метода: подсчёт по ИМЕНИ поля (возможны ложные «живые» при коллизиях имён;
для приоритетных полей — ручная проверка).

- **14 конфигов, 94 публичных поля: 64 живых, 30 мёртвых (0 читателей).**
- Приоритетный список аудита:
  - `WorldConfig.DefaultSpeed` — читался WorldModule.Start, но ЗАТИРАЛСЯ
    хардкодом `Normal` в WorldInitPhase (фаза 3) → **исправлено**: DI-синглтон
    WorldConfig, общий инстанс у WorldModule и WorldInitPhase; поведение по
    умолчанию НЕ изменилось (DefaultSpeed=Normal).
  - `WorldConfig.StartLocationId`/`StartSectorId` — **0 читателей**: выбор
    мира реально идёт через `GameSessionData.WorldId` ← `LocationCatalog`
    (PlayerSpawnPhase/TileMapGenPhase/WorldInitPhase/…, fallback TestPolygon).
    Поля помечены LEGACY-комментариями; **не удалены** (исторический
    док-контракт; удаление — решение владельца).
  - `PlayerConfig.SleepStaminaRecoveryPercentPerHour` — был мёртвым (0
    читателей) → **ожил**: читается StaminaService (E1-пруф 100%/ч).
  - `SaveConfig.AutoSaveIntervalMinutes` — жив (SaveModule `[Inject]`, гейт
    по SessionState.Playing; SaveFileHandler читает SaveDirectory) — подтверждено.
- Полный список 30 мёртвых полей (все — «задокументированы, не удалены»;
  массовое удаление — за владельцем, поле может быть док-контрактом):
  FormationConfig.DefaultFormationId; NPCConfig.BaseCoreCapacity/CoreCapacityGrowth/
  IdleTimeout/WanderTimeout/PatrolTimeout/FleeTimeout/FollowDistance;
  PlayerConfig.DefaultPlayerId/DefaultPlayerName/FallAsleepTime/WakeUpTime/
  MinSleepHoursForConsolidation/MaxConsolidationPerSleep/SleepHpRecoveryPercentPerHour/
  MaxTechniqueSlots/StatDeltaPerAction/MoveSpeed; SaveConfig.MaxSaveSlots/
  EnableCompression/SaveVersion/QuickSaveEnabled; UIConfig.ToastQueueDelay;
  WorldConfig.StartLocationId/StartSectorId/FactionAttitudeWeight/NationAttitudeWeight/
  RandomEventChancePerTick/MinEventDurationTicks/MaxEventDurationTicks.
- Примечание: `PlayerConfig.RunSpeedMultiplier` и ряд NPC-полей живы
  косвенно (коллизии имён не критичны для вывода — приоритетные поля
  проверены вручную).

## 6. Регрессия

`bash tools/qa_regression.sh` — **36 симов (35 прежних + STAGE17): 35/36 PASS**
(лог `checkpoints/10_24_stage17_regression_full.log`):

- **STAGE15 PASS** (+ cold-пара PASS) и **STAGE16 PASS** — регрессий нет.
- **STAGE17 PASS** + cold-пара SAVE/LOAD PASS.
- **STAGE8 FAIL в полном прогоне** — известная нестабильность (задокументирована
  в отчётах этапов 15–16): внутренний итог 29 PASS / 0 FAIL / 1 NOT TESTED
  (условный skip — недоступность NPC/техники в окружении полного прогона),
  вердикт-формула сима требует 0 NOT TESTED. **Изолированный повтор
  `bash tools/qa_regression.sh STAGE8` — PASS (29 проверок, лог в выводе
  сессии)**. FAIL не маскируется: первичный результат и повторный прогон
  зафиксированы раздельно, дефектом этапа-17 не является (изменения этапа
  касаются стамины/настроек/конфига скорости — не техник; STAGE8 зелёный в
  изоляции на том же коде).
- COMBAT_SIM (bug012): в финальном полном прогоне PASS; в первом (прерванном
  платформой) запуске регрессии успел показать FAIL «ранний гейт Support» —
  та же задокументированная нестабильность, упомянута для полноты.

## 7. Открытые GAP / follow-up (не маскируются общим PASS)

1. **GAP: расход выносливости действиями** — игрок ещё НЕ тратит стамину:
   бег по Shift запланирован (HOTKEYS.md:303 «Нет стамины бега — 📋
   Запланировано»), вводить gameplay-расход без решения владельца этап не
   стал. Сервис готов (`Spend` с полным контрактом), потребитель — будущий.
2. **GAP: бафф-модификаторы** — `StatType.StaminaCost`/`StaminaRegen`
   (BuffCalculator, SoftCaps-константы готовы) НЕ подключены к Spend/Regenerate
   (бафф «Второе дыхание» и т.п. пока не влияют на выносливость) — follow-up.
3. **GAP: HUD-полоса стамины** — UI_DESIGN HudLayer описывает полосу
   HP/Ци/Stamina; события `StaminaChangedEvent` публикуются, UI-читатель не
   подключен (визуализация — вне этапа, скриншоты в среде заблокированы
   со времён этапа-16).
4. **NOT TESTED: InputMap mid-failure** — обрыв регистраций посередине не
   воспроизводим рантаймно (Godot InputMap API не бросает); фикс — харденинг
   порядка флага по контракту P1-10, проверено идемпотентностью (I3).
5. **NOT TESTED (K): сообщения выхода процесса** — классифицированы по
   лог-доказательству (см. ST17-009); рантайм-проверка в процессе сима
   неприменима по природе сообщений.
6. **30 мёртвых полей конфигов** — задокументированы (§5), удаление за владельцем.
7. **Физические плейтесты** — PT-статусы НЕ менялись (канон §0); визуальные
   проверки этапа-16 остаются незакрытыми, PT-V01..V08 — planned.

## 8. Критерии закрытия этапа-17

- [x] Реализация и DI-путь выносливости доказаны (A1–A3 + CoreValidationPhase
      резолв на каждой сборке + cold-пара); новая игра — полный ресурс;
      расход/регенерация и пределы корректны (B–D).
- [x] Save/Load, cold-load и ResetWorld не теряют и не выдумывают состояние (F/G).
- [x] Настройки читаются из валидного JSON (любой формат), сохраняются и
      восстанавливаются; Set-до-загрузки не затирает; ретраи работают (H).
- [x] Инициализация и повторная попытка запуска безопасны (I); источники
      истины конфигов документированы (§5).
- [x] Сборка 0 ошибок; Stage17 (32/0/1NT) и полная регрессия (35/36 +
      изолированный STAGE8 PASS) записаны фактическими логами.
- [x] Незакрытые визуальные проверки Stage16 и физические плейтесты НЕ
      отмечены выполненными.
- [x] Неустранённые дефекты, два follow-up этапов 11/12 (исправлены отдельно)
      и ошибки завершения процесса классифицированы раздельно.

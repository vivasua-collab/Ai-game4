# Чекпоинт: Этап-16 аудита — Рендеринг, анимация и синхронизация визуального состояния

**Дата:** 2026-10-24 (сессия coder-1024, продолжение ночной после сбоя среды)
**Канон:** `upload/audit_10_20_e16.txt` ([STAGE 16 AUDIT])
**Тип:** postfix-отчёт (префикс → фиксы → постфикс → регрессия)

---

## 1. Окружение и SHA-цепочка

- **Godot:** v4.7.2.stable.mono.official.ed1daf0bf (`/home/z/my-project/godot/…`)
- **.NET:** SDK 8.0.426 + 9.0.319 (hostfxr 9.0.21; переустановка после сброса
  песочницы ночью 24.10 — `/home/z/.dotnet` был утерян, восстановлено
  dotnet-install; сборка и headless-симы верифицированы после восстановления).
- **SHA-цепочка этапа:**
  - `c3a652d` — статическая карта ST16-001..009 (5 CONFIRMED / 2 PART / 2 DISPROVED-статика) + восстановление песочницы (ff 75b8634→9cb19a0).
  - `3b73482` — Stage16RenderAnimationSyncTruthSimDebug + регистрация в qa_regression/TESTING_RULES; **префикс: 16 PASS / 13 FAIL**, лог `checkpoints/10_23_stage16_render_animation_prefix.log`.
  - `65ee7b8` — фиксы ST16-001..007 + уточнение сценария/метрики D2b.
  - (docs-коммит) — этот отчёт + TESTING_RULES-правка + портал (playtest render_animation PT-V01..V08 planned, bugs.json STAGE16-*, status.json).
- **Ночная сессия (обрыв):** правки ST16-001/003/004/005/007 + ST16-006a
  выжили в рабочем каталоге (uncommitted), обрыв произошёл на ST16-006b/c
  (TechniqueEffectRenderer) — фиксы доведены и закоммичены как `65ee7b8`.
  Смешения с Этапом-15 нет: Этап-15 закрыт ранее (`9cb19a0`, 27/27),
  рендер-файлы этапом-15 не трогались.

## 2. Команды

```bash
export PATH="/home/z/.dotnet:$PATH"; export DOTNET_ROOT="/home/z/.dotnet"
cd game && dotnet build                        # 0 errors (711 warnings — прежний фонд)
# префикс/постфикс (одна и та же команда):
GODOT_NEWGAME=1 GODOT_STAGE16_DEBUG=1 <godot> --headless --path . scenes/MainMenu.tscn
# регрессия:
bash tools/qa_regression.sh
```

## 3. Симулятор этапа

`game/src/Adapter/Scene/Stage16RenderAnimationSyncTruthSimDebug.cs`,
хук `GODOT_STAGE16_DEBUG=1`, зарегистрирован `tools/qa_regression.sh`
(STAGE16) и `docs/docs_v2/09_workflow/TESTING_RULES.md` §0.1. Сценарии
A–I по ST16-001..009, переиспользует ANIMQA/WEAPONVIS/COMBAT_SIM-контракты
(не дублирует); итог `VERDICT: PASS/FAIL`.

**Факт, уточнённый по ходу (важно для будущих сессий):** в Godot 4.7
`--headless` **ВЫПОЛНЯЕТ** `_Draw` canvas-узлов (пруф: G2 — кролики у
камеры заполняют `_lastAnimInfo` без opengl3; D2b читает живые резолвы).
Формулировка «headless _Draw не выполняется» в ранних комментариях была
неверна — TESTING_RULES-строка скорректирована docs-коммитом.

## 4. Результаты ST16-001..009 (префикс → постфикс)

| ID | Суть | Префикс | Постфикс | Статус |
|----|------|---------|----------|--------|
| ST16-001 | one-shot-таймеры переживают death/respawn | A1–A5: 5 FAIL (melee/ranged/hit/pause → 'player_melee'/'player_hit' после респавна) | A1–A5: 5 PASS ('player_idle') | **CONFIRMED_FIXED** |
| ST16-002 | полнота подключённых состояний | B1–B4 PASS | без изменений | **OK / 5 GAP (G3–G5)** |
| ST16-003 | звериный melee-ключ `animal_{species}_melee` vs канон `attack` | C1 FAIL ('animal_wolf_melee') | C1 PASS ('animal_wolf_attack'); C2 PASS | **CONFIRMED_FIXED**; C3–C5 GAP (G3: hit/cast/death не подключены — по плану; смерть→труп сразу — живой жизненный цикл, задержку НЕ вводить) |
| ST16-004 | walk-мерцание зверей (кадровая дельта) | D2a PASS-пруф (дельта 7.6% vs домен 99.0%), D2b FAIL (0.0%) | D2b PASS (walk-сигнал 68.9%) | **CONFIRMED_FIXED**; D4–D7 GAP (run/attack/death/facing — фаза G3/D1) |
| ST16-005 | ZIndex формации 5 > Player 4 | E1 FAIL (ZIndex=5), E2 FAIL (сиблинг #17 после тел) | E1 PASS (ZIndex=3), E2 PASS (formation#13 < ground#14/npc#17/animal#15) | **CONFIRMED_FIXED**; E3 GAP (Y-sort — план §3.2, вне объёма) |
| ST16-006 | Shield: one-shot ≠ persistent-буфер | F1 FAIL (активация=0 — события несли EntityId=''), F2 FAIL (стаки) | F1–F4 PASS (1/2; 2→2→2; деакт→1; 2.5с→0) | **CONFIRMED_FIXED** (3 суб-дефекта: a-EntityId, b-стаки, c-преждевременное убийство one-shot) |
| ST16-007 | нечистимые per-entity кэши | G1 FAIL (12→20→20), G2 FAIL (stale 5/5) | G1 PASS (12→20→12), G2 PASS (stale 0/5) | **CONFIRMED_FIXED**; G3 GAP (culling-1frame — документированная самокоррекция BUG-007) |
| ST16-008 | PNG/fallback/реестр | H1–H3 PASS, H4 | без изменений | **DISPROVED** (геометрия→диагностика+fallback; негативный кэш — контракт сессии; ANIMQA-звери честно WARN) |
| ST16-009 | синхрон тело/MainHand | I1/I2/I4 PASS; I3 NOT TESTED | без изменений | **OK (логика)**; I3 NOT TESTED — фактический FlipH/offset требует скриншотов (см. §6) |

**Итог сима (постфикс): `29 PASS / 0 FAIL / 14 GAP / 1 NOT TESTED / 1 DISPROVED — VERDICT: PASS`**
(префикс: `16 PASS / 13 FAIL — VERDICT: FAIL`). Лог:
`checkpoints/10_23_stage16_render_animation_report.log`.

## 5. Исправления (все — в `65ee7b8`)

1. **PlayerAnimator (ST16-001):** `NotifyDeath()`/`NotifyRespawn()` обнуляют
   `_meleeAge/_rangedAge/_hitAge` — смерть прерывает любой one-shot, новая
   жизнь начинается с чистого листа. Приоритеты состояний не менялись
   (директива аудита).
2. **NPCSpriteRenderer (ST16-003):** квадрупед-melee резолвил
   `animal_{species}_melee`; канон манифеста/SPRITE_DELIVERY §5 —
   `animal_{species}_attack`. Замах зверя-NPC теперь резолвит поставимый
   ключ. `npc_hit/cast/death` — фаза G3 (GAP по плану §6.1); смерть →
   CorpseSpriteRenderer сразу (задержку не вводить — директива).
3. **AnimalSpriteRenderer (ST16-004):** движение = доменный сигнал
   `AnimalEntity.Target != null` (живёт весь путь блуждания/погони;
   null = прибыл/отказ/смерть). Кадровая дельта видела дискретный прыжок
   1–2 тайла ровно на одном кадре 15-Гц редрава → walk мерцал один кадр.
   `_animalLastPos` удалён; `QaFrameDeltaMoving` сохранён как QA-референс
   (D2a сравнивает сигналы). Culling больше не влияет на признак walk.
4. **FormationVisualRenderer + SceneBuilder (ST16-005):** `ZIndex =
   RenderLayer.Objects` (было Objects+2=5 > Player=4) + перенос
   `SetupFormationVisuals()` сразу после `SetupObjectLayer()` — канон
   проекта «равный ZIndex + порядок сиблингов» (паттерн «трупы до NPC»):
   контур НАД деревьями/камнями, ПОД ground items/зверями/трупами/NPC/
   игроком. Между Objects=3 и Player=4 целого Z нет — решение по канону.
5. **QiBufferService (ST16-006a):** `EntityId` читается живьём из
   `_qiService.EntityId`. Снапшот конструктора был пуст навсегда
   (DI-конструкция синглтона происходит ДО `QiModule.Start →
   QiService.Initialize("player")`) → ВСЕ события буфера несли
   `EntityId=""` → фильтры потребителей (`TechniqueEffectRenderer`,
   UI) их гасили: **persistent-щит не появлялся в игре вообще**.
   Найдено при внедрении F-сценария (префикс: событие `[on:]`, счётчик 0).
6. **TechniqueEffectRenderer (ST16-006b/c):** `ActiveVisual.IsPersistent`
   разделяет жизненные циклы одного вида отрисовки Shield:
   - b) повторный `IsActive=true` (AbsorbDamage репаблишит на каждый
     поглощённый удар) → идемпотентная замена persistent-визуала, не стаки;
   - c) деактивация буфера гасит ТОЛЬКО persistent — one-shot 2.5с
     успешного каста Defense доигрывает (аудит: «деактивация не удаляет
     несвязанный одноразовый эффект»).
   Дубли между TechniqueEffect/AoeFx/Projectile/Homing-рендерерами —
   DISPROVED (R29-гварьи PublishSuccess подавляют generic для
   AoE/homing); координаты миллипиксели/1000 = пиксели рендера —
   DISPROVED (контракт CombatContracts), рантайм-ассерт в F1.
7. **NPCSpriteRenderer + AnimalSpriteRenderer (ST16-007):** чистка
   per-entity записей мёртвых/деспавненных (NPC: lastPos/lastX/facing/
   animInfo/phaseOffset; звери: animInfo/phaseOffset) на редком проходе
   `_PhysicsProcess` (0.5с), ВНЕ `_Draw` (записи текущего прохода не
   трогаются). ID уникальны на спавн (`npc_{Guid}`,
   `animal_{species}_{nextId++}`) — переиспользования нет, удаление
   безопасно. Общие texture-кэши (`_weaponTexCache`, каталоги) не тронуты.
8. **Stage16-сим (D2b):** постфикс-ревизия сценария по результатам
   первого постфикса: (1) D2a уводил волка за видимую область камеры
   (x≈38 при виде x∈[16..34]) → culling BUG-007 не обновлял кэш,
   45 семплов читали stale-idle (0% в ОБЕИХ фазах — артефакт сценария);
   теперь волк телепортируется в кадр и цель короткая; (2) метрика —
   `AnimalAnimInfo.Moving` (сигнал реального резолва), а не
   `AnimId.Contains("walk")`: walk-лист волка — недоставленный (G3) →
   moving=true легитимно резолвится в процедурный 'animal_wolf_static'
   (контракт G1 fallback); PNG-доставка — отдельный GAP D-серии.

## 6. Визуальные артефакты — ЗАБЛОКИРОВАНО СРЕДОЙ (честная фиксация)

Аудит §2.6 требует скриншоты (GODOT_SCREENSHOT/GODOT_VFX_SHOT/Xvfb +
`--rendering-driver opengl3`). **После ночного сброса песочницы Godot в
GUI-режиме не поднимает игру**: движок стартует, GL-контекст жив
(llvmpipe, OpenGL 4.5), .NET-модуль инициализируется (hostfxr 9.0.21,
GodotPlugins OK), главный цикл итерирует (`--quit-after` работает), но
**C#-сцены не инстанцируются** (0 строк `[GameBoot]`/`[MainMenu]`,
GODOT_SCREENSHOT-хук C# не срабатывает), экран чёрный (дамп фреймбуфера
Xvfb `-fbdir` + XWD→PNG конвертация + VLM: «полностью чёрный
прямоугольник, ни панелей редактора, ни UI»). Headless той же сборки —
полностью функционален (все симы, вкл. постфикс 29/0).

Испробовано: позиционная сцена и `--scene res://…`; без сцены
(main_scene); `-screen 0 1280x720x24` и `1920x1080x24`;
`+extension GLX +render` (ошибка новичка `-extension` = DISABLE —
исправлена); `LIBGL_ALWAYS_SOFTWARE=1`; `--render-thread unsafe`;
`--log-file`; `--verbose` (показал: D-Bus-портал читается, EditorTheme
генерируется — editor-шаги); `--headless --import` (успешно, exit 0).
Техника Xvfb в точности по протоколу R37c/R39 («ручной Xvfb :99
+extension GLX +render в одном bash-вызове»), который давал рабочие
скриншоты до сброса (screenshots/r37_*.png, r39_*.png в репо).

**Следствие:** I3 (фактический FlipH/offset оружия) остаётся
`NOT TESTED`; визуальное подтверждение порядка формации (PT-V06) и
остальных визуальных сценариев — физический плейтест владельца
(PT-V01..V08, категория `render_animation`, все `planned`). Кодовые
пруфы порядка: E1 (ZIndex=3=Objects) + E2 (сиблинг-порядок:
formation#13 < ground#14 < animal#15 < npc#17) — логическая истина
рендер-дерева; визуальная верификация владельцем/скриншотами — после
восстановления GUI-режима среды (кандидат на следующий эпизод:
диагностика editor-boot против game-boot в opengl3, дамп экрана
работает — xwd2png-метод сохранён в отчёте).

## 7. Полная регрессия

`bash tools/qa_regression.sh` — **35/35 PASS / 0 FAIL** (10 foreground-чанков,
24.10; лог `checkpoints/10_23_stage16_regression.log`). Включая ранее
флакавшие зоны: COMBAT_SIM (bug012-контур) и STAGE8 (D6a) — оба зелёные
в этом прогоне; STAGE13/14/15/16 — PASS (этап-16 подтверждён вторично
независимым прогоном раннера). 35 симов реестра покрыты полностью
(COMBAT_SIM…ANIMQA, порядок §0.1 TESTING_RULES).

## 8. GAP / NOT TESTED — реестр

| ID | Причина | Владелец решения |
|----|---------|------------------|
| ST16-002 B5–B9 | cast(G3)/guard(G4)/dash,sleep,breakthrough(G5) — листы в манифесте, механика будущих фаз | план SPRITE_ANIMATION_PLAN §5.1 |
| ST16-003 C3–C5 | npc_hit/cast/death — фаза G3; смерть→труп сразу (presentation-only hold — при доставке G3) | аудит-директива |
| ST16-004 D4–D7 | animal_run/attack/death — G3; facing D1-силоэт — контракт доставки | манифест |
| ST16-005 E3 | Y-sort не реализован (план §3.2); batch-рендеры требуют индивидуальной сортировки в _Draw | будущий этап (канон §3.4 RENDER_LAYERS) |
| ST16-007 G3 | culling: ≤1 кадр walk при возврате, самокоррекция | документировано (BUG-007) |
| ST16-009 I3 | фактический FlipH/offset — скриншоты; **заблокировано средой** (§6) | след. эпизод / физплейтест |

## 9. Физический плейтест

В `portal/data/playtest.json` добавлена категория **render_animation**
(PT-V01..V08 по аудиту §6, все `planned` — владелец проходит лично):
PT-V01 player idle/walk, PT-V02 one-shot×death/respawn, PT-V03 NPC/звери
атака/ранение/смерть→труп, PT-V04 экипировка/зеркало, PT-V05 Y-перекрытие,
PT-V06 формация под персонажем, PT-V07 VFX-синхрон (shield/hold/aoe/
homing/directional), PT-V08 длительная сессия (кэши). Существующие
статусы PT не менялись.

## 10. Реестр аудита (bugs.json)

Добавлены `STAGE16-001..007` (verified): 001 one-shot после респавна,
003 ключ звериного melee, 004 walk-мерцание, 005 формация поверх
игрока, 006 (a: persistent-щит не появлялся вовсе — EntityId-снапшот;
b: стаки; c: убийство one-shot деактивацией), 007 рост кэшей.
ST16-008/009 — без записи (DISPROVED/OK; I3 — NOT TESTED, не дефект).

## 11. Критерии закрытия (аудит §7) — самооценка

- Визуальные состояния согласованы с доменными событиями: **да** (B/D/F-серии PASS).
- one-shot не застревают после death/respawn: **да** (A1–A5 PASS).
- Порядок слоёв соответствует спецификации: **да логически** (E1/E2),
  **визуально — владелец PT-V05/V06** (среда, §6).
- VFX не дублируются и не исчезают преждевременно: **да** (F1–F4 PASS).
- Кэши проходят длительный цикл spawn/despawn: **да** (G1/G2 PASS).
- fallback и загрузчик PNG не деградируют: **да** (B3, H1–H3 PASS — без правок).
- Сборка и регрессия честно задокументированы: **да** (§2, §7).
- Скриншоты: **заблокировано средой** — задокументировано с полным
  протоколом попыток (§6); NOT TESTED не маскируется под PASS.

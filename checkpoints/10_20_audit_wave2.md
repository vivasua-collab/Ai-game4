# Чекпоинт: Волна 2 — багфикс внешнего аудита adit_10_20 (BUG-003…012 + DOC-001)

**Дата:** 2026-10-20
**Сессия:** main Z.ai Code; субагенты 10-20-fix-c/d/e/f/g/h (6 параллельных, непересекающиеся зоны)
**Тип:** implementation (bug-fix wave, внешний аудит)
**Коммит:** см. git log (волна 2)

---

## Контекст

Директива владельца (20.10): «Тесты вчера не получились. Лови для анализа
внешний аудит, пока что 2 этапа. Необходимо провести валидацию проблем. При
подтверждении создать план исправления и выполнить исправления. файл
upload/adit_10_20».

**Валидация (main-агент, по коду): 10/10 находок + 3 док-расхождения —
ВСЕ ПОДТВЕРЖДЕНЫ.** Этап 1: безусловный свитч сцены меню, потеря re-entrancy
EventBus при дренировании (ранее R34 P2-22), DI-снапшот, выбор ctor,
culling рендереров, доки. Этап 2 (по коду волны 1): глобальные бонусы
формаций, неблокированное управление при смерти, гонка кэша убийцы,
безусловное скрытие экрана при провале респавна, трата Ци на невозможное
лечение. Реестр: `portal/data/bugs.json` 12 → 23 записи (волна 2 = 11,
субагенты 10-20-fix-c…h). Малые фичи перенесены: волна 2 → волна 4
(GAP-002/003/004).

## Приоритеты исправления (по аудиту) и результаты

### P1

**BUG-003 — провал NewGame/LoadGame всё равно ведёт в GameWorld (fix-e)**
Корень: `GameSession.NewGame/LoadGame` глотают исключения (SetState(MainMenu)),
а `MainMenuController` безусловно делал `ChangeSceneToFile` в 4 точках
(OnNewGame/OnTestPolygon/OnLoadGame/QA OnNewGameInWorld). Фикс: helper
`EnterWorldIfSessionReady(op)` — свитч сцены только при
`SessionState.Playing`; иначе `GD.PrintErr` + видимый статус-лейбл в меню
(кнопки не блокируются). QA-путь автостарта не задет (CHARGE PASS).

**BUG-008 — бонусы формаций глобальны (fix-c)**
Корень: `FormationEffects` агрегировал статы без TargetTag;
`GetFormationBonusPermil(stat)` не знал позицию/сторону; DamageService слои
3b/6-8 тянул бонус для ЛЮБОГО атакующего/защитника — активный барьер игрока
защищал врагов по всей карте, Amplification усиливал чужой урон. (Регрессия
волны 1: Shield-агрегация VER-001 сделала глобальность боеспособной.)
Фикс: entity-aware overload
`GetFormationBonusPermil(stat, entityId, tileX, tileY)` в
IFormationService/FormationService: радиус Чебышёва int-математикой
(`dist*TILE_SIZE_M > EffectRadiusMeters`, ЗАПРЕТ 3.9) + сторона по
TargetTag (v1-правило: ally = кастер или игрок-алиас — NPC-союзников как
системы нет; остальное enemy); FormationEffects — агрегация по
`(StatType, Tag)` + legacy-сумма для старых QA; DamageService — резолв
тайлов атакующего/цели по паттерну CombatRangeGateService (опциональные
параметры ctor: IPlayerService/INPCService/IAnimalService), неизвестная
позиция → 0. PlayerTechniqueCaster.GetAmplificationBonusPermil делегирует
сервису. QA: FormationShotSimDebug 4a (ally в зоне 250‰) / 4a2 (вне
радиуса → 0, возврат → 250‰) / 4a3 (враг в зоне → 0) / 4c (Amplification:
игрок 220‰, враг 0). Контур в симе смещён на (10,10) — за EffectRadius
Small (25 тайлов) в карте 50×50 иначе не уйти.

**BUG-009 — управление не блокируется при экране смерти (fix-d + fix-c)**
Корень: вуаль блокирует только мышь; HandleFreeMovement двигал по WASD,
HandleStickyInput обрабатывал B/C/K/F1/J/Q/H/Esc/F/3-9; V/T/X/Z/Space вне
StickyInput; `_deathOverlay` не в AnyModalWindowOpen.
Дизайн-решение (зафиксировано): смерть блокирует УПРАВЛЕНИЕ ИГРОКА, мировое
время НЕ паузится (экран смерти — не окно планирования, осознанно вне
INP-1 стека: Esc-семантика и pause-снапшоты целы).
Фикс (fix-d): предикат `DeathScreenShown` + 4 гейта: стоп-кадр в
HandleFreeMovement, полный early-return в HandleStickyInput, обёртка
`if (!DeathScreenShown)` вокруг V/T/X/Z/Space/CombatAdapter.Tick
(ResetFrameFlags — вне), `modalOpen += DeathScreenShown` в _UnhandledInput.
Фикс (fix-c, модульная защита): PlayerTechniqueCaster — «Вы мертвы» в
начале OnCastRequested (до held-обработки) и в OnChargeCompleted.

### P2

**BUG-010 — убийца в событии смерти (fix-d)**
Корень: кэш оверлея обновляется подписчиком DamageAppliedEvent, идущим в
fan-out ПОСЛЕ BodyService — смерть происходила синхронно внутри хендлера
BodyService → BuildKillerLine читал ПРОТУХШИЙ кэш (предыдущий атакующий).
Фикс: перенос источника по цепочке — `IBodyService.ApplyDamage(+sourceId=null)`
+ `LastDamageSourceId` (кэш ДО применения урона), `PlayerService.Die(cause,
killerId)`, `PlayerDeathEvent{Cause, KillerId}` (back-compat ctor сохранён),
GWC → `overlay.Show(e.Cause, e.KillerId)` (killerId приоритетнее кэша),
кэш чистится при любом оживлении (OnPlayerRevived) и респавне.
QA: RespawnSimDebug 4a — несмертельный удар с source "player_0" (протухший
кэш показал бы «собственное рассеяние»), смертельный с "npc_qa_killer"
→ строка «неведомая сила», НЕ «собственное рассеяние» — гонка ловится
детерминированно через публикацию синтет-события по шине (прямой
ApplyDamage событий не публикует).

**BUG-011 — провал респавна скрывает кнопку (fix-d)**
Корень: RequestPlayerRespawn void + try/catch внутри; OnRespawnPressed
безусловно HideScreen (даже при null-делегате).
Фикс: скрытие только по подтверждению — PlayerReviveEvent (синхронный) или
`_player.IsAlive` после Invoke; null-делегат → PrintErr, экран остаётся.
RequestPlayerRespawn → bool; обёртка `RequestPlayerRespawnForOverlay`
(метод-группа bool→Action даёт CS0407 — компилятор). QA: RespawnSimDebug 4d
(отвинченный делегат → экран жив + мёртв; перепривязка → жив + скрыт).

**BUG-012 — Support/Healing тратят Ци на невозможное лечение (fix-c)**
Корень: гейт «Тело не ранено» только в FireTechnique (после зарядки);
Combat/Poison/Curse имели ранние гейты до StartCharge, Healing/Support — нет.
Фикс: ранний гейт в Stage 0 до StartCharge; повторная проверка в
FireTechnique сохранена (тело могло зажить за зарядку). Правило
зафиксировано: Ци, вложенная в НАЧАТУЮ зарядку, не возвращается (консистентно
с Combat «Цель исчезла»). QA: CombatSimDebug 3m2 — полный HP → мгновенный
fail «Тело не ранено», Ци 444→444 не списана, аура пуста.

**BUG-004 — EventBus: потеря re-entrancy при дренировании (fix-f)**
Корень: дрен очереди в finally ПОСЛЕ `_publishing.Remove(typeof(T))` —
хендлер дрена, публикующий тот же тип (или A↔B), уходил в normal-path →
вложенный finally-дрен → безграничный рост стека → StackOverflowException.
Фикс: сентинел `[ThreadStatic] _draining` — в дрине ЛЮБАЯ публикация
становится в очередь; дрен — итеративный while на ОДНОМ уровне стека.
Семантика: normal-path публикует в очередь только тот же тип (прейдыне);
в дрине — любые типы, FIFO по батчам. QA: Audit0922 U1/U2 (self-publish 250
→ ровно 250 доставлено, depth 1), U4/U5 (mutual A↔B 100/100, depth 2).

**BUG-005 — DI Build() без снапшота (fix-f)**
Корень: `Build()` передавал _registrations/_orderedRegistrations по ссылке.
Фикс: копии обеих коллекций при Build (Registration-объекты общие —
допустимо, не мутируются). QA: Audit0922 V1–V9 (слепота контейнера к
пост-Build мутациям builder через Resolve/ResolveAll, same-instance,
второй Build — свой кэш, Dispose изоляция).

**BUG-006 — выбор ctor без проверки разрешимости (fix-f)**
Корень: комментарий «greediest ctor we can satisfy», реализация
`OrderByDescending(...).First()` — длинный ctor с неразрешимой зависимостью
ронял Resolve при пригодном коротком.
Фикс: перебор кандидатов от длинного к короткому; первый, чьи обязательные
параметры резолвятся или имеют DefaultValue; полный провал →
InvalidOperationException с диагностикой. QA: Audit0922 W1 (короткий ctor
выбран: N=4242), W2 (одиночный неразрешимый → исключение), W3 (жадный при
разрешимости).

**BUG-007 — рендереры без viewport culling (fix-g)**
Корень: NPC/Animal _Draw обходили ВСЕ сущности — резолв анимации/HP/рамок
для невидимых; троттл 15 Гц (R41-2) не уменьшал стоимость ОБХОДА.
Фикс: паттерн ObjectLayerRenderer.GetVisibleTileRange —
`GetGlobalTransformWithCanvas().AffineInverse() × GetViewportRect()` (зум
учтён); отсечение после IsAlive-гейта по позиции, ДО резолва анимации;
margin 80px (спрайт 48 + HP-бар 24 + рамка 3 + буфер); общие хелперы
`ComputeVisibleWorldRect`/`IsEntityOnScreen` в NPCSpriteRenderer — Animal
переиспользует. Отсечённая Tab-цель не рисуется (вне экрана — ОК); кэш
позиций протухает → один кадр walk при возврате (безопасно).
QA: L500SimDebug bug007-culling 4/4 (математика хелпера: центр/вне/
граница+margin/margin+10). Эффект: L500 в кадре ~30×17 тайлов из 500×500
→ отсекается ~97% сущностей (48 NPC + 109 зверей). Находка fix-g: headless
_Draw ВЫПОЛНЯЕТСЯ (WEAPONVIS подтверждает) — culling-ветка реально
прогнана в QA.

### Доки

**DOC-001 (fix-h):** README 16→17 (строки 13/96, +Trade); START_PROMPT —
дата 20.10, §3/§4/§11 17 модулей, §10 статус переписан (21 блок сейва,
23 QA, волна 2), §12 приоритеты: волны аудита → плейтесты → малые фечи →
дизайн-этапы + «Статус актуален на 20.10.2026»; SESSION_SUMMARY — шапка
20.10 (HEAD 56a8573), +5 записей (20.10 волна 2; 19.10 карта+волна 1+
портал; 18.10 R41; 02.10 R38+R39; мост R36-R37), статусные секции
(Miss/овекилл/каст-эффекты, смерть/диагностика, портал, 23 QA, реестр как
ведущий «не работает»). Остаток вне зоны: ~15 «16 модулей» в docs_v2 —
следующий проход плана аудита доков R31.

## Регрессия

- Сборка: 0 Error(s).
- Полная регрессия: **23/23 PASS** (21 дефолт + FORM + RESPAWN), чанками
  3–4 сима: COMBAT_SIM/COMBATAI/ANIMALQA, DOT/SAVELOAD/KILLFEED/QUEST,
  STORAGE/HOTBAR/TRASHDROP/CONTEXT, MODALQA/MODAL2/L500,
  WEAPONVIS/REASSEMBLY/CHARGE, AUDIT0922/CHARGERQA/ANIMQA, FORM/RESPAWN.
- Все 11 пунктов волны 2 → verified в `portal/data/bugs.json` (23 записи,
  v1.1). Карта проекта → v1.4-wave2 (form-s7/tech-s7/player-s5/core/perf/docs
  аннотации, open_verifications player-s5 обновлён; PROJECT_MAP.md
  перегенерирован, --check без дрейфа).

## Находки волны 2, НЕ вошедшие в фиксы (кандидаты реестра)

1. FormationDescriptions.cs:81 — «радиус 25 **м**» для Small: делит
   EffectRadiusMeters на TILE_SIZE_M → показывает ТАЙЛЫ под меткой «м».
2. Gathering (Buff/Conductivity/ally) не потребляется DamageService —
   заявлен для медитации, потребитель не проверен.
3. Положительный end-to-end «Amplification активна + игрок бьёт NPC в
   зоне» в симах не покрыт (FORM проверяет сервис напрямую) — кандидат
   интеграционного шага.
4. docs_v2: ~15 упоминаний «16 модулей» (ARCHITECTURE, DI_AND_EVENTBUS,
   PERFORMANCE_STRATEGY, FILE_TREE, GLOSSARY, TECHNOLOGY_DECISIONS) —
   следующий проход R31.
5. Прямые ApplyDamage без sourceId (ItemUseService и симы) → KillerId=null
   → честный fallback «неведомая сила» (осознанно, не баг).

## NEXT

1. Плейтест владельца: новые UX смерти (гейты/убийца/подтверждённый
   респавн), баланс формаций в бою (entity-aware бонусы), скорость на
   large_world после culling (PerfOverlay F6 — до/после).
2. Волна 4 (малые фичи): GAP-002 крафт-UI, GAP-003 форм-факторы зарядников,
   GAP-004 NPC-категории.
3. Волна 3 (дизайн с владельцем): GAP-007 события мира, GAP-008 AI-слои,
   BUG-002 mastery-рост, GAP-009 Trap-контроль.
4. Итерация 4 карты — верификации §5; план аудита доков R31 (docs_v2 16→17).

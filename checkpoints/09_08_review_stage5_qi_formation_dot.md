# Этап 5 — Ци, формации, тело, периодические эффекты

> Статус: ✅ **ВЫПОЛНЕН** (валидация: 5/5 подтверждены; фиксы + QA + регрессия PASS).

## Вердикты и фиксы

| # | Находка | Вердикт | Фикс |
|---|---|---|---|
| P0-1 | NPC-вклад Ци в формацию списывает Ци игрока | ✅ Подтверждено (ContributeQi: contributorId → RequesterId, EntityId пуст) | Игрок — QiConsumeRequestEvent с ЯВНЫМ EntityId + подтверждение списания по кэшу QiChangedEvent (EventBus диспатчит синхронно); NPC — прямое синхронное списание IQiDataProvider.TryConsumeQi. QiService: алиас-безопасное сравнение EntityId (AreSameEntity — "player"/"player_0") |
| P0-2 | Создание формации всегда оплачивает игрок | ✅ Подтверждено (StartDrawing: пустой EntityId) | Контур оплачивает ИМЕННО кастер (игрок: событие с EntityId + подтверждение; NPC: IQiDataProvider.TryConsumeQi) |
| P1-3 | Проверка/списание Ци не атомарны | ✅ Подтверждено (fire-and-forget + пул «в кредит») | Пул пополняется ТОЛЬКО по подтверждённому списанию (charged-блок); при неподтверждении — return 0 |
| P1-4 | DoT только логируется | ✅ Подтверждено (Console.WriteLine, HP не менялся) | BodyModule публикует DamageAppliedEvent(source="dot:{buffId}", target=e.EntityId, Torso, Qi/Physical) — ЕДИНЫЙ пайплайн: BodyService применяет по частям, NPCCombatAdapter → смерть, kill-feed. Броня/Ци-буфер не применяются (DoT-значение финальное) |
| P2-5 | Кэш уровня культивации тела не синхронизируется при инициализации | ✅ Подтверждено (QiService.Initialize публикует только QiChangedEvent) | BodyService подписан на QiChangedEvent (OnQiChangedForLevel, фильтр по сущности с алиасами) — начальный уровень актуален |

## QA

- **НОВЫЙ хук `GODOT_DOT_DEBUG=1`** (DotSimDebug, №22): Poison/Burn/Bleed/Freeze → DamageAppliedEvent → HP падает (NPC −4/7 и игрок −3/5 — 70/30-сплит как у обычных ударов), 4 события, устойчивость после QiChanged(level=3). VERDICT: PASS.
- FORMATION_TEST: StartDrawing → Filling → Active 800/800 (контур+вклад оплачены через новый подтверждённый путь).
- Регрессия: COMBAT, CHARGE, TOAST, LOWHP, KILLFEED, STORAGE, DIALOGUE, REASSEMBLY, GEN — все PASS. Build 0 errors.

## Находки ревью → план валидации и фикса

### P0-1. NPC-вклад Ци в формацию списывает Ци игрока
**Клейм:** `QiConsumeRequestEvent(amount, requesterId, entityId)`; FormationService.ContributeQi публикует
`new QiConsumeRequestEvent(effectiveAmount, contributorId)` → contributorId попадает в RequesterId, EntityId пустой; QiService пустой EntityId трактует как игрока → списание у игрока.
**Валидация:** прочитать QiContracts (порядок параметров), FormationService.ContributeQi, QiService-обработчик QiConsumeRequestEvent.
**Фикс:**
- Публиковать с явным EntityId: `new QiConsumeRequestEvent(effectiveAmount, "Formation", contributorId)`.
- Проверить, что для NPC есть реальный обработчик списания (QiService обслуживает только свою сущность — нужен обработчик NPC-Ци через IQiDataProvider.TryConsumeQi либо единый сервис).
- Если NPC-Ци нельзя списать синхронно — как минимум не списывать у игрока (валидировать возможность списания contributorId).

### P0-2. Создание формации всегда оплачивает игрок
**Клейм:** StartDrawing сохраняет _casterId, но contour-Ци публикуется без EntityId → платит игрок даже если создатель NPC.
**Валидация:** прочитать StartDrawing и публикацию contour-Ци.
**Фикс:** EntityId = casterId во всех публикациях расхода; проверка баланса именно casterId до начала рисования.

### P1-3. Проверка и списание Ци не атомарны
**Клейм:** FormationService проверяет баланс (кэш/IQiDataProvider), публикует fire-and-forget QiConsumeRequestEvent и сразу зачисляет в пул; TryConsumeQi может вернуть false, но результат теряется.
**Валидация:** прочитать последовательность check→publish→pool в ContributeQi/StartDrawing; комментарий про fire-and-forget.
**Фикс:**
- Синхронный authoritative API: `TryConsumeQi(entityId, amount): bool` (через QiService для игрока, IQiDataProvider для NPC).
- Пул пополнять ТОЛЬКО по успешному списанию.
- Если синхронное списание невозможно для какого-то пути — событие QiConsumedEvent/QiConsumeRejectedEvent и зачисление по подтверждению (но приоритет — синхронный API).

### P1-4. DoT не реализован: Poison/Burn/Bleed/Freeze только логируются
**Клейм:** BodyModule.OnBuffTicked лечит только HealthRegen; DoT не наносят урон и не публикуют DamageAppliedEvent.
**Валидация:** прочитать OnBuffTicked.
**Фикс:**
- Publisher DamageAppliedEvent (или PeriodicDamageRequestEvent) для Poison/Burn/Bleed/Freeze через единый damage pipeline (броня/Ци-буфер/смерть/визуал).
- Передавать e.EntityId (не всегда player torso).
- DoT против брони: определить DamageType (Poison/Pure — решить по балансу; зафиксировать в доке).
- QA: тик яда → HP снижается, смерть обрабатывается.

### P2-5. BodyService кэш уровня культивации не синхронизируется при инициализации
**Клейм:** _cachedCultivationLevel=1, обновляется только по CultivationLevelChangedEvent; QiService.Initialize публикует только QiChangedEvent → при старте с уровнем >1 регенерация считается как L1.
**Валидация:** прочитать BodyService кэш и его подписки, QiService.Initialize.
**Фикс:**
- BodyService подписать на QiChangedEvent (обновлять кэш уровня, фильтруя entity ID), ЛИБО публиковать CultivationLevelChangedEvent в QiService.Initialize.
- QA: старт с CultivationLevel=N → регенерация как N без прорыва.

## QA-план

- Новые хуки: formation-Ци (вклад NPC списывает Ци NPC, НЕ игрока; отказ при нехватке), DoT-тик (HP меняется), regen-уровень.
- Регрессия CHARGE/COMBAT + полная.

## Коммит
`fix(review-5): qi transactions + DoT pipeline + cultivation sync` + push.

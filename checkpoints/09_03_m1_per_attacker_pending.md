# Чекпоинт: M1 — per-attacker pending technique (npc self-hit fix)

**Дата:** 2026-09-03 01:35 UTC
**Сессия:** web-6be67126 (основная сессия, цикл webDevReview)
**Тип:** fix

---

## Контекст

Латентный баг «per-attacker pending technique» задокументирован в
SESSION_CONTEXT §2 как P2-долг: «глобальный pending: NPC может ударить себя
при смене цели в полёте атаки». Подтверждён в COMBAT_SIM логе сессии
2026-09-02: `npc_XXX → npc_XXX: 21` (self-hit). Включён в MVP-план этапом M1
(стабилизация). Пользователь подтвердил приоритет: основная боевка сначала.

## Что сделано

- **Корневая причина:** `BuildAndExecuteDamageRequest` резолвил цель в момент
  СРАБАТЫВАНИЯ каста: `defenderId = attackerId == _instigatorId ? _currentTargetId
  : _instigatorId`. Сценарий: NPC-инстагатор начинает каст → игрок атакует NPC
  (A3-3: `ExecuteAttack` переключает `_currentTargetId` на NPC) → каст
  срабатывает → `attackerId(NPC) == _instigatorId(NPC)` → defender =
  `_currentTargetId` = сам NPC.
- **Второй дефект (обнаружен при фиксе):** `_lastAttackPotencyPermil` —
  глобальное поле: заряженная атака игрока во время чужого pending-каста
  подменяла potency чужой отложенной техники (усиление NPC мощностью игрока).
- **Фикс (CombatService.cs):**
  - `PendingTechnique` + `TargetId` (defender на момент старта каста) +
    `PotencyPermil` (potency кастера на момент старта).
  - `ExecuteAttack` (pending-ветка): резолвит `castTargetId` немедленно и
    сохраняет в pending вместе с potency.
  - `UpdateTimer` передаёт запомненные значения в `ApplyTechniqueImmediately`.
  - `ApplyTechniqueImmediately` / `BuildAndExecuteDamageRequest` принимают
    optional `explicitDefenderId` / `explicitPotencyPermil`; мгновенный путь
    (null) использует прежний резолв — поведение без каста не изменилось.
- **Тест обновлён (CombatSimDebug.cs):** старый PASS armed-фазы держался на
  self-hit баге: урон NPC летел в самого NPC → `OnDamageAppliedForCastInterrupt`
  (механика C11: урон по кастеру прерывает его каст) не срабатывал для игрока →
  его pending доживал до armed swing. После фикса NPC честно бьёт игрока →
  каст игрока прерывается (задуманная механика!) → armed swing давал 0.
  Решение: фаза 3b ждёт 1.4с «settle» до armed-интента (все pending раундов
  догорают) → чистое окно для weapon wiring.

## Решения

- Цель и potency фиксируются в pending на момент СТАРТА каста — семантика
  «выстрел уже в полёте» (нельзя перенаправить/перепитать чужой каст) —
  соответствует духу A3-3 (переключение цели действует на НОВЫЕ атаки).
- Тест обновлён, а не отключён: механика прерывания C11 остаётся честной,
  armed-фаза проверяет weapon wiring в окне без параллельных кастов.

## Найденные проблемы

- Аномалия ФС песочницы жива: `timeout` execve бинаря Godot периодически
  ENOENT при живом файле (stat OK, прямой exec OK) — обход: повтор/`env`
  literal path. Памятка в worklog 08-26 актуальна.
- Headless-прогоны НЕ завершаются сами (exit 124 по timeout — норма,
  успешность определяется по ключевым строкам лога, не по exit code).

## Следующие шаги

- Новые указания пользователя (2026-09-03): сейвы отложить; приоритет —
  физическая боевка; затем техники; чит-меню расширить + выключатель в
  настройках; стартовая генерация предметов/техник вместо сейвов.
- Изучить CHEAT_PANEL.md + CheatPanel.cs → план расширения.
- Физбойка: TODO isRanged → CombatSubtype (CombatService:338), аудит полноты
  11-слойного пайплайна против COMBAT_SYSTEM.md.

## Файлы

- `game/src/Modules/Combat/CombatService.cs` — фикс (5 правок)
- `game/src/Adapter/Scene/CombatSimDebug.cs` — settle 1.4с в фазе 3b
- `checkpoints/09_03_m1_per_attacker_pending.md` — этот чекпоинт

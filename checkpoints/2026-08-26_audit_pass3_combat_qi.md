# АУДИТ-3: Архитектура + Мир + Боевой контур (Combat / Qi / Formation / Body / Trade)

**Дата:** 2026-08-26, 15:50–16:30 MSK
**Проход:** 3 из 3 (минимум), основной поток ИИ
**HEAD на старте:** `e7f2008`
**Scope:** аудита-1+2 (инварианты удержаны) + новые модули: CombatService
(полностью, 805 строк), TechniqueChargeService/QiService/FormationService —
целевые проверки (qi-потребление, подписки, lifecycle), PlayerIdResolver
(миграция Combat).

---

## Сводка

- **Файлов прочитано:** CombatService полностью; PlayerIdResolver, CombatModule
  (мост OnAttackIntent), DamageService (ветка IsPlayerTarget), CombatConfig,
  PlayerCombatAdapter/NPCCombatAdapter (подписчики), QiService — точки входа
  событий; структура Combat/Qi/Formation/Body/Trade (16 файлов, ~10k строк).
- **Находок:** 6 (1 critical-баг с инверсией, 2 подтверждённых-чисто, 3 minor).
- **Исправлено:** 1 (C-1 — пять связанных мест в CombatService).

---

## Находки

### C-1 [CRITICAL-BUG] Инверсия ролей игрока при инициировании боя NPC

**Файл:** `Modules/Combat/CombatService.cs` (+ связка `CombatModule.OnAttackIntent`).

**Сценарий:** NPC атакует первым (волк/бандит: AttackIntentEvent →
`StartCombat(npcId, playerId)` → instigator = NPC). Все проверки
«это игрок?» строились на ложной посылке «instigator = игрок»:

| Место | Было | Последствие при NPC-инстагаторе |
|-------|------|--------------------------------|
| `isPlayerTarget = defenderId == _instigatorId` | false для игрока | Qi-щит игрока читался из per-entity провайдера (IQiDataProvider) вместо кэша событий → механика щита ломалась |
| `isPlayerAttacker = attackerId == _instigatorId` | true для NPC | Защита игрока (`_lastPlayerDefense`) игнорировалась в пайплайне урона |
| Fatal: `if (attackerId == _instigatorId)` → EnemyKilledEvent(_currentTargetId) | публикует EnemyKilledEvent(ИГРОКА) | Лут (CombatLootService) и квесты (QuestProgressTracker) дропались НА игрока при ЕГО гибели; ставилась Victory |
| EndCombat: Victory → winner = instigator | winner = NPC, победивший игрока, даже когда игрок выиграл бой | NPCCombatAdapter обновлял отношения по ложному победителю |
| ExecuteDefense: `defenderId == _config?.PlayerEntityId` («player») | не покрывает «player_0» | Выбор защиты игроком не запоминался (NPC AI атакует canonical «player_0») |

**Почему не ловилось раньше:** GODOT_COMBAT_SIM инициирует бой игроком
(instigator=player) — инвертированная ветка не выполнялась. Плюс двойной
ID игрока («player» vs «player_0» — P0-DUAL-PLAYER-ID, для которого уже
создан PlayerIdResolver, но Combat не был мигрирован).

**Фикс (реализован, 5 мест):**
- `isPlayerAttacker`/`isPlayerTarget` → `PlayerIdResolver.IsPlayer(...)`;
- Fatal-ветка → виктим-центричная (жертва-игрок → Defeat БЕЗ EnemyKilledEvent;
  жертва-NPC + атакующий-игрок → Victory + EnemyKilledEvent(victim));
- EndCombat → игроко-центричные winner/loser (NPC-vs-NPC — прежняя
  инстагаторская схема как fallback);
- ExecuteDefense → PlayerIdResolver.

**Верификация:** build 0 errors; GODOT_COMBAT_SIM=1 —
**VERDICT: PASS** («обе стороны боя получают урон»).

### C-2 [OK-BY-DESIGN] TechniqueUsedEvent.QiCost не списывает Ци

QiService не подписан на TechniqueUsedEvent: расход Ци происходит на этапе
зарядки техники (aura-hold модель, 08-25: TechniqueChargeService инвестирует
Ци до атаки). TechniqueUsedEvent — информационный (UI/статистика). Проверено —
не утечка.

### C-3 [OK] Подписки QiDepleted в StartCombat/EndCombat

Subscribe в StartCombat, Dispose в EndCombat — парность соблюдена; финальный
Dispose() подчищает всё. Утечки нет.

### C-4 [MINOR] CombatService._cachedCurrentQi — только игрок

Кэш QiChangedEvent — Ци игрока; ExecuteDefense (щит = cached/4) корректен
только для защиты игрока (защиту выбирает только игрок). ОК сейчас, заметка
при появлении NPC-защит.

### C-5 [MINOR] ExecuteAttack тихо игнорирует при _isCasting

`if (_isCasting) return;` без события/лога — игрок может не понять, почему
атака не прошла. Рекомендация: публикация события отклонения (для UI-тоста).

### C-6 [MINOR] CombatConfig.PlayerEntityId = «player» (алиас)

Поле больше НЕ используется после миграции на PlayerIdResolver (единственный
читатель заменён). Рекомендация: удалить поле или задать канонический
«player_0» при следующем касании CombatConfig.

---

## Что проверено и чисто (выборочно по контуру)

- **CombatModule:** мост AttackIntentEvent → StartCombat+ExecuteAttack;
  Dispose подписок парный.
- **DamageService:** ветка IsPlayerTarget → кэш игрока vs per-entity NPC —
  с фиксом C-1 флаг приходит корректным.
- **PlayerCombatAdapter/NPCCombatAdapter:** подписчики null-safe
  (GetNPCState(null-для-игрока) → null-чек есть).
- **QiService:** command-события QiConsume/QiAdd — единая точка списания.
- **FormationService:** CombatEndedEvent → автодеактивация (подписка
  парная); lifecycle Drawing→Filling→Active→Depleted известен из прошлой
  сессии (headless-проверен).
- **TechniqueChargeService:** уже мигрирован на PlayerIdResolver
  (TryGetQiCache + IsPlayerId — B1 прошлой сессии).

---

## Журнал фиксов

| Файл | Изменение |
|------|-----------|
| Modules/Combat/CombatService.cs | +using Core.Helpers; isPlayerAttacker/isPlayerTarget → PlayerIdResolver; fatal-ветка виктим-центрична; EndCombat игроко-центричен (fallback NPC-vs-NPC); ExecuteDefense → PlayerIdResolver |

**Верификация:** dotnet build 0 errors; GODOT_COMBAT_SIM=1 VERDICT PASS.

---

## Итог трёх проходов аудита (2026-08-26)

| Проход | Scope | Находок | Фиксов | Коммит |
|--------|-------|---------|--------|--------|
| 1 | Архитектура | 6 | 4 | b8ddda1 |
| 2 | + Мир (World/Tile/NPC-spawn) | 7 | 4 | e7f2008 |
| 3 | + Боевой контур | 6 | 1 (5 мест) | (этот) |

Итого: 19 находок, 9 исправлений (включая 3 критических бага: порядок
фаз, травы 0.01%, инверсия ролей игрока в бою).

---

*Аудит-3 завершён. Минимальный план пользователя (3 последовательных
прохода) выполнен. Следующие кандидаты для аудита-4+: Inventory/UI/Save,
Interaction/Trade, Body/Enhancement, NPC AI/Movement.*

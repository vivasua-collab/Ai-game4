# Чекпоинт: Этап-5 аудита — боевой истины смерти (BUG-015/016/017)

**Дата:** 2026-10-22
**Сессия:** main Z.ai Code (приём дефектов от аудитора → фиксы → QA → доки)
**Тип:** bugfix (аудит волна 3, Этап 5: расчёт урона / применение повреждений /
смерть / согласованность боевых событий)
**Источник:** аудит владельца «Этап 5 — аудит боевой системы и состояний
сущностей»: 1×P1 + 2×P2, все подтверждены статическим анализом; регрессионные
сценарии заданы аудитором дословно и реализованы в новом симе STAGE5.

---

## Дефекты и фиксы

### BUG-015 (P1) — бой объявлял цель убитой до фактической смерти тела

Причинная цепочка аудита: `DamageCalculator.IsFatalHit` (голова/сердце +
урон ≥ FATAL_DAMAGE_THRESHOLD=50) объявлял удар смертельным, но
`BodyDamageCalculator.SplitDamage` направлял в функциональную HP только 70%:
голова 50 HP при ударе 50 → −35 красной → жива (15/50), а
`CombatService` (`defenderDead = result.IsFatal || …`) уже считал цель
поверженной — ложные Victory/Defeat/EnemyKilledEvent без NPCDeathEvent и трупа
(домены смерти проверяют реальное тело → домены расходились).

Фикс — единый источник истины смерти = фактическое состояние тела:
- `DamageService.CalculateDamage`: `isFatal` вычисляется ПОСЛЕ синхронного
  `DamageAppliedEvent.Publish` (fan-out уже прогнал `BodyService.ApplyDamage`
  c овекилл-переносом R41-1) = `HasEntity && !IsEntityAlive`;
  `DamageResult.IsFatal` и `DamageTraceEvent.Fatal` теперь одно значение
  (ранее трейс расходился с результатом при незарегистрированной цели).
- `DamageCalculator.IsFatalHit` + `GameConstants.FATAL_DAMAGE_THRESHOLD`
  УДАЛЕНЫ (тело-правило: смерть = Head|Heart RedHP ≤ 0).
- `CombatService.BuildAndExecuteDamageRequest`: комментарий-правило
  обновлено; дублирующая проверка провайдера оставлена как гейт-страховка.

### BUG-016 (P2) — 30% урона по сердцу терялось

Сердце (CORE-C01) не имеет структурной HP (MaxBlackHP=0), а сплит 70/30 был
безусловен: 30% уходило в несуществующий слой, `BodyPart.TakeDamage` их
отбрасывал — удар 10 снимал 7 красной (50 → 35 вместо 50), порог фатальности
сравнивался с уроном, которого тело не получало.

Фикс — part-aware сплит (зеркало правила лечения BD-24):
- `BodyDamageCalculator.SplitDamage(totalDamage, targetPart = null)`:
  MaxBlackHP ≤ 0 → весь урон в красную; обычные части — сплит 70/30.
- Оба вызова в `BodyService` (игрок + per-entity NPC) передают часть.

### BUG-017 (P2) — DoT-убийство не засчитывалось в квестах

`EnemyKilledEvent` (единственный источник прогресса KillEnemy-квестов)
публиковал только `CombatService` в ветке обычного удара → смерть от
Poison/Burn/Bleed/Freeze (BodyModule.OnBuffTicked → DamageAppliedEvent) квесты
не двигала; `NPCDeathEvent.KillerId` = фантом `dot:{buffId}` (авторство не
сохранялось нигде в цепочке).

Фикс — единый доменный путь смерти + атрибуция:
- `EnemyKilledEvent` публикуют домены смерти `NPCCombatAdapter.OnDamageApplied`
  и `AnimalService.OnDamageApplied` при подтверждении гибели по телу
  (killer-игрок): ровно один зачёт и для прямого удара, и для DoT-тика;
  `CombatService` зачёт больше не публикует (паблишер/поле/ctor-параметр
  удалены — DI-поручения не нужны: `IPublisher<T>` авто-врапится EventBus).
- Цепочка атрибуции: `IBuffService.ApplyBuff(+sourceId)` →
  `ActiveBuff.SourceId` (Refresh обновляет владельца) → `BuffTickedEvent.
  SourceId` → `DamageAppliedEvent.OriginalAttackerId` (новое поле; SourceId
  остаётся фантомом `dot:…` — threat-таблицы NPC от яда не мстят,
  AUDIT-0911 NPC-7 сохранён) → `NPCDeathEvent.KillerId` (реальный убийца)
  + `BodyService.LastDamageSourceId` (экран смерти игрока).
- Продюсеры передают автора: PlayerTechniqueCaster (яд = игрок),
  ElementalEffectService (elemental_burn/elemental_poison = атакующий удара),
  CombatConsequencesService (combat_bleed = атакующий удара).
- Легаси-конструкторы `DamageAppliedEvent` совместимы (OriginalAttackerId
  = SourceId для прямых ударов).

## Регрессионный QA — новый сим STAGE5 (GODOT_STAGE5_DEBUG)

`Stage5CombatTruthSimDebug.cs` (№23 в qa_regression.sh) — 22 ассерта по
сценариям аудитора, ПОЛНЫЕ пути (реальный fan-out EventBus → BodyService →
домены смерти → CorpseService/QuestService):

1. **BUG-016**: сердце NPC −10 красной (полный), black=0; торс −7/−3 (70/30).
2. **BUG-015**: голова-50 → red 50→15, жив, kill/death-событий 0; повторный
   50 → смерть: EnemyKilled ×1, NPCDeath(killer=player_0) ×1, труп ×1.
3. **Сердце**: 50×2 (80 HP) убивают; первый удар (≥50!) на живом теле —
   никаких событий (старый IsFatalHit дал бы ложную смерть).
4. **Консистентность DamageService**: `IsFatal == !IsEntityAlive` после
   каждого удара (5×20 через броню → final=1 + овекилл-каскад 3000 → смерть).
5. **BUG-017**: реальная DoT-цепочка ApplyBuff(elemental_poison,
   sourceId=player_0) → тик (TickInterval 1с) → смерть волка:
   EnemyKilledEvent ×1 (квест «Охота на волков» 1/3),
   NPCDeathEvent.KillerId = «player_0» (атрибуция, не «dot:…»), труп ×1;
   добивание двух волков ударом → квест Completed 3/3, зачёт ровно по
   одному на волка (двойного счёта нет).
6. **Реальный бой** CombatService.ExecuteAttack (charged ×300): смерть NPC
   завершает бой (IsInCombat=false), зачёт ровно один.
7. **Игрок**: голова-50 → жив (red 50→15, black −15), PlayerDeathEvent=0;
   сердце-10 → −10 полный (BUG-016); торс-10 → −7/−3.

Вердикт: `[Stage5Sim] VERDICT: PASS — BUG-015/016/017: тело=истина смерти,
сплит сердца 100%, DoT-зачёт квесту + атрибуция` (лог
`qa_artifacts/10_22_stage5_sim.log`).

## Документация (углублённый аудит по директиве)

- `docs_v2/02_systems/COMBAT_SYSTEM.md` §1.4: правило смерти переписано
  (IsFatalHit удалён; IsFatal = !IsEntityAlive post-damage); НОВЫЙ пункт
  «Зачёт убийств — ЕДИНАЯ точка» (цепочка атрибуции; тихая смерть не-
  участника + DoT).
- `docs_v2/01_architecture/MODULE_STRUCTURE.md` §2.4: то же правило в
  исторической сводке Combat-модуля.
- `docs_v2/02_systems/BODY_SYSTEM.md` §5.3: исключение сплита для частей без
  структурной HP (part-aware SplitDamage) + НОВЫЙ §5.3.1 «Смерть организма
  (единый источник истины)».
- `docs_v2/09_workflow/TESTING_RULES.md` §0.1: реестр 37→38 хуков, строка
  GODOT_STAGE5_DEBUG (сим №23).
- `portal/data/bus_map.json`: регенерирован (EnemyKilledEvent: Combat →
  NPCCombatAdapter/AnimalService; QA-узел 31→32 файла; source_head af56edc);
  `--check` = OK.
- `portal/data/bugs.json`: реестр 31→34 (STAGE5-001 P1 / STAGE5-002 P2 /
  STAGE5-003 P2, все status=verified, волна 3).
- `portal/data/status.json`: headline/build/qa/focus — Этап-5 (после
  полного прогона регрессии).

## Затронутые файлы (код)

| Файл | Изменение |
|---|---|
| `Modules/Body/BodyDamageCalculator.cs` | SplitDamage part-aware (BUG-016) |
| `Modules/Body/BodyService.cs` | 2× SplitDamage(total, part); LastDamageSourceId ← OriginalAttackerId ?? SourceId |
| `Modules/Body/BodyModule.cs` | DoT-тик публикует OriginalAttackerId=e.SourceId |
| `Modules/Combat/DamageService.cs` | isFatal = тело post-damage (BUG-015) |
| `Modules/Combat/DamageCalculator.cs` | IsFatalHit удалён (заменит-комментарий) |
| `Core/Data/Constants.cs` | FATAL_DAMAGE_THRESHOLD удалена |
| `Modules/Combat/CombatService.cs` | EnemyKilledEvent-публикация/паблишер удалены; комментарии |
| `Modules/NPC/NPCCombatAdapter.cs` | +EnemyKilledEvent при смерти (killer-игрок), KillerId-атрибуция |
| `Modules/NPC/AnimalService.cs` | то же для зверей |
| `Core/Messaging/Contracts/CombatContracts.cs` | +DamageAppliedEvent.OriginalAttackerId (+полный ctor, легаси-ctrs сохранены) |
| `Core/Messaging/Contracts/BuffContracts.cs` | +BuffTickedEvent.SourceId |
| `Core/Interfaces/IBuffService.cs` | ApplyBuff(+sourceId=null) |
| `Modules/Buff/ActiveBuff.cs` | +SourceId |
| `Modules/Buff/BuffService.cs` | ApplyBuff/Refresh/CreateBuffFromId несут sourceId |
| `Modules/Buff/BuffTickProcessor.cs` | BuffTickedEvent(+buff.SourceId) |
| `Modules/Player/PlayerTechniqueCaster.cs` | яд: sourceId=игрок |
| `Modules/Combat/ElementalEffectService.cs` | burn/poison: sourceId=атакующий |
| `Modules/Combat/CombatConsequencesService.cs` | bleed: sourceId=атакующий |
| `Adapter/Scene/Stage5CombatTruthSimDebug.cs` | НОВЫЙ QA-сим (22 ассерта) |
| `Adapter/Scene/GameWorldController.cs` | подключение GODOT_STAGE5_DEBUG |
| `tools/qa_regression.sh` | +STAGE5 (реестр 23) |

## Открытые хвосты (НЕ баги, для будущих волн)

- `_enemyKilledPub`-хвосты в QA-симах не тронуты (QuestSimDebug публикует
  EnemyKilledEvent напрямую — валидный тестовый вход).
- Killfeed-дедуп EventLogWindow (<2с «повержен») покрывает пару
  EnemyKilled+NPCDeath — поведение сохранено для обоих путей.
- Аудиторский триаж 68 событий каталога шины + 5 мёртвых DI-регистров
  (итерация 2 вкладки «Схема шины») — отдельная задача владельца.

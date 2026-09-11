# Чекпоинт: Аудит и фикс боя с животными (волк) — ЧАСТЬ 1

**Дата:** 2026-09-11 ~04:20–06:30 UTC (сессия №8, агент Z.ai Code)
**Жалоба пользователя:** «Бегу с посохом за волком, пытаюсь его бить, а
результата 0. Как будто не проходит регистрация урона.»
**Вторая задача:** легенда клавиш на главном окне → перенести в F1 (просил
много итераций назад).

**ПРОДОЛЖЕНО ПОСЛЕ ЗАВИСАНИЯ СЕРВИСА — состояние на момент обрыва описано
в «Статус» внизу. Этот чекпоинт написан по требованию пользователя (вести
детальные чекпоинты РЕГУЛЯРНО).**

---

## 1. Диагноз боя с животными (7+2 дефектов)

### D1 (P0 — жалоба): таргетинг не видел животных
- `PlayerCombatAdapter.FindNearestTarget` искал цели ТОЛЬКО в
  `INPCService.GetNearbyNPCIds`. Волки НЕ регистрируются в NPCService —
  живут в `AnimalService._animals` (+тела в IBodyDataProvider).
- Space-атака по волку → target==null → тихий return. «Результата 0».
- **ФИКС:** инъекция `IAnimalService` (новый Core-интерфейс) в адаптер;
  кандидаты = NPC ∪ животные (`GetAliveAnimalsInRange`), общий чебышёв +
  LOS-фильтр. QA-метод `DebugFindNearestTarget`.

### D2 (P0): смерть животных не детектировалась НИКЕМ
- AnimalService.OnDamageApplied только помечал hostile; HP волка мог быть
  0 — «жив». Трупа/killfeed не было.
- **ФИКС (частично, см. D8):** HP ≤ 0 или vital-разрушение →
  IsAlive=false + NPCDeathEvent.

### D3 (P0): CorpseService не создавал труп животного
- OnNPCDeath требует NPCState → return. DEATH_AND_LOOT §5 требует труп.
- **ФИКС:** ветка TryCreateAnimalCorpse (имя «Волк»/«Олень»/«Кролик»,
  уровень 1, лут = 1–3 осколка дух. камня, дедуп, CorpseCreatedEvent).

### D4 (P1): месть волка была «фантомной»
- Интент публиковался с ЛЮБОЙ дистанции (бил через всю карту), волк НЕ
  преследовал (стоял на месте), ID цели — литерал "player", de-aggro нет.
- **ФИКС:** честный чейз (Target=позиция игрока, StepTowardsTarget в
  кулдаун), атака только при Чебышёв ≤ 2, ID = PlayerService.PlayerId,
  de-aggro > 5 тайлов → CombatDisengageEvent (AbandonCombat — бой не
  «висит», симметрия с leash NPC R16) + 5 тиков «успокаивается».
- Кулдаун укуса: 3 → 2 тика (окно EnemyTurnTimeout 2.5с; 3-й тик всегда
  опаздывал в чужой ход → укус отклонялся вечно).
- Кролик — мирный (ANIMALS §5.5): не становится hostile.

### D5 (P1): цифры урона над животными
- DamageNumberRenderer.ResolveTargetPixelPos → null для животных (комментарий
  автора прямо признавал дыру).
- **ФИКС:** ветка IAnimalService.TryGetAnimal → позиция зверя.

### D6 (P1): killfeed «??? повержен»
- EventLogWindow.Name резолвит только NPCState.
- **ФИКС:** fallback на IAnimalService.GetDisplayName («Волк»).

### D7 (P2): статы животного в бою = статы игрока
- StatProviderAdapter: неизвестная сущность (животное) → ветка игрока:
  AGI волка = AGI игрока, морфология Humanoid вместо Quadruped (таблица
  попаданий по гуманоиду).
- **ФИКС:** ветка животных: статы вида (SpeciesRegistry: волк STR 8/AGI
  14/VIT 10/INT 4), материал, Quadruped-морфология.

### D8 (P0, найден QA-прогоном): «сущности неубиваемы» (животные И NPC!)
- Все домены смерти ждали «суммарный HP ≤ 0». Но урон в УЖЕ мёртвую часть
  тела теряется (per-part floor) → сумма практически не обнуляется
  (волк 460 HP: 8 ударов × 1000 урона → HP застрял на 230, Torso/ноги
  мертвы, Head жив).
- IsFatalHit (Head/Heart ≥ 50 урона) публиковал isFatal, но НИКТО по нему
  смерть не ставил — CombatService только ставил Stage=Victory.
- `IBodyDataProvider.IsEntityAlive` (vital RedHP ≤ 0) существовал С
- 2026-05 и НЕ использовался никем!
- **ФИКС (3 точки):**
  1. CombatService: defenderDead = IsFatal || (не-игрок && тело мертво по
     IsEntityAlive) → Victory/EndCombat (+инъекция IBodyDataProvider).
  2. NPCCombatAdapter.OnDamageApplied: смерть = !IsEntityAlive ||
     HP ≤ 0 → NPCDeathEvent (труп).
  3. AnimalService: то же правило.
- ЭТО ЖЕ правило — будущий кандидат для PlayerService (смерть игрока).

### D9 (найден QA, инфраструктурный): QA-телепорт игрока «не прилипал»
- Прямой PlayerService.SetPosition откатывался: GWC._PhysicsProcess каждый
  кадр синхронизирует логику к _visualPosition (визуал — источник истины
  движения) → позиция откатывалась к визуальной (25,25).
- **ФИКС:** GWC.DEBUG_TeleportPlayer(tileX, tileY) — ставит _visualPosition
  + _mouseTarget=null + SetPosition. Только для QA.

### Попутные системные находки
- Двойное имя фазы у животных: старый AnimalSpawn fallback (сцена напрямую)
  + AnimalSpawnPhase → 8 зверей на TestPolygon (5+3 доспавн QA-волка:
  состав не критичен, но композиция = 5 базовых + QA-доспавны).
- Враждебный человек-NPC может начать бой с игроком после QA-телепорта в
  угол карты — это ЛЕГИТИМНОЕ поведение (не баг): QA-ассерты делают
  устойчивыми к посторонним боям (счётчик CombatEndedEvent, не глобальный
  IsInCombat).

## 2. Новые файлы/интерфейсы

- `game/src/Core/Data/AnimalInfo.cs` — readonly struct: боевой профиль
  (ID/вид/позиция/морфология/материал/статы вида/IsAlive).
- `game/src/Core/Interfaces/IAnimalService.cs` — GetAliveAnimalsInRange /
  TryGetAnimal / GetDisplayName.
- AnimalService реализует IAnimalService (+ IsHostile QA-геттер).
- `builder.Register<IAnimalService, AnimalService>` в NPCModuleServices.
- CorpseService: +IAnimalService (TryCreateAnimalCorpse,
  BuildAnimalCorpseItems).
- StatProviderAdapter: +IAnimalService (ветки в 4 геттерах).
- `game/src/Adapter/Scene/AnimalCombatSimDebug.cs` (НОВЫЙ QA):
  GODOT_ANIMALQA_DEBUG=1, 7 тестов → VERDICT PASS/FAIL.
- GWC: хук ANIMALQA + DEBUG_TeleportPlayer.

## 3. HUD-легенда (жалоба №2) — СДЕЛАНО

- `_hudLabel` (HudHint, низ экрана, WASD/Space/E/B/...) УДАЛЁН из
  GameWorldController (+поле). Комментарий-могила оставлен.
- Канон — HotkeysWindow (F1, решение пользователя 2026-08-28): полный
  перечень, группы, «В разработке/отключено».
- Легенда к тому же врала: рекламировала нереализованные M/N-карты,
  называла F1 чит-меню (чит — F2).

## 4. QA AnimalCombatSimDebug — 7 тестов, все PASS (лог /tmp/animalqa11.log)

1. Таргетинг: DebugFindNearestTarget → волк OK
2. Урон: интент игрока → HP волка 460→453 (пайплайн через тело) OK
3. Месть: hostile=True, укус игрока за 1.0с (HP 500→491) OK
4. Смерть: подранок (Head=0) + заряженный удар → IsAlive=False,
   труп «Волк» 1 запись лута, бой завершён (Victory) OK
5. Killfeed-имя: «Волк» OK
6. De-aggro: телепорт игрока в даль → месть гаснет, CombatEnded(+1),
   AbandonCombat Flee OK
7. Кролик после урона НЕ hostile OK

**VERDICT: PASS.**

## 5. Регрессия — ПОЛНЫЙ ПРОГОН, все VERDICT: PASS (14/14 + ANIMALQA)

- build: **0 errors**.
- ANIMALQA (новый): PASS 7/7.
- COMBAT_SIM, COMBATAI, LOOT, SAVELOAD, KILLFEED, CHARGE — PASS (батч 1).
- QUEST, STORAGE, HOTBAR, DOT, TRASHDROP, CONTEXT, WEAPONVIS, REASSEMBLY —
  PASS (батчи 2–4, догнаны после зависания сервиса).
- Критичные для изменений: LOOT (труп-пайплайн + новый конструктор
  CorpseService — опциональный параметр, обратная совместимость), COMBATAI
  (правило смерти NPC), REASSEMBLY (фазы 16/16, NPC-домен). Все PASS.

## 6. Статус (обновлено после регрессии)

- Все правки кода применены и собираются (0 errors).
- AnimalQA PASS (7/7); регрессия 14/14 PASS.
- docs_v2 СИНХРОНИЗИРОВАНЫ: ANIMALS §5.2/5.3/5.4/5.5 (чейз/2 тика/de-aggro
  + CombatDisengageEvent/боевой профиль), DEATH_AND_LOOT §5 (реализация
  трупа животного), MODULE_STRUCTURE §2.7 (AnimalService-блок) + §2.4
  (единое правило смерти), COMBAT_SYSTEM §1.4.1 (смерть защитника +
  животные-участники), TESTING_RULES §0.1 (+GODOT_ANIMALQA_DEBUG).
- Инструментация в CombatService.AbandonCombat (диагностические логи) и
  AnimalQA (combat-started/dmg-события) — оставлена: полезные диагностические
  принты, не шумные в обычном бою.
- Осталось: worklog, SESSION_SUMMARY/CONTEXT, финальный build, коммит+push.

## 7. Дальнейшие шаги (порядок)

1. ~~Догнать регрессию~~ — ГОТОВО (14/14 PASS).
2. ~~docs_v2 sync~~ — ГОТОВО.
3. worklog + SESSION_SUMMARY/CONTEXT (сессия №8) + этот чекпоинт.
4. Финальный build + коммит + push.
5. Пользователю: живой QA на ПК — посох→волк (урон/цифры/смерть/труп/обыск),
  месть волка (чейз/укусы), уход от погони (de-aggro), F1-справка (легенды
  на HUD больше нет).

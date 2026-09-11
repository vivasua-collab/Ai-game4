# АУДИТ: Player

**Дата:** 2026-09-11 (аудит-сессия 09-11, субагент playerquest-audit)
**HEAD на старте:** `d0b065d`
**Скоуп (READ-ONLY, код НЕ изменялся):** Modules/Player — все 9 файлов
полностью (~1.5k строк) + стыки: PlayerContracts/StatContracts,
BodyService (BodyCriticalEvent) + BodyModule (StatChangedEvent→RecalculateHP),
CombatService (fatal-ветка 850-853, ExecuteDefense 897-906, статы
атакёра 676-690), StatProviderAdapter, TechniqueChargeService (drain),
QiBufferService (Activate), QiService (consume/add), InputAdapter +
InputMapInitializer (клавиши), GameWorldController (смерть/респавн
1921-1970, HandleStickyInput 1135-1209, модальность 1228-1238, слоты
3-9 1655-1674), NPCService (GetNearbyNPCIds); docs_v2: DEATH_AND_LOOT,
TECHNIQUE_SYSTEM §5.4, STAT_THRESHOLD_SYSTEM, 06_player/ (ls).

## Сводка

- Прочитано 9/9 файлов; rg-трассировка вызовов SetBaseStat/ModifyStat/
  SetStat/AddBonus/AddVirtualDelta/ConsolidateSleep (0 вызовов),
  StartSleep/WakeUp (0 внешних), Dissipate (2), StatChangedEvent
  (0 публикаторов), InputDisabled (0 сеттеров), QiChangedEvent-публикаторы
  (только QiService), HeldTechniqueChangedEvent (только QA).
- **Находок: 15 (P1×2, P2×7, P3×6).** Главные: смерть игрока не покрывает
  Head-путь (BOD-2-пересечение подтверждено); статы игрока никогда не
  инициализируются → боевые статы = 0.
- Замороженное НЕ репортилось: Qi=long; пермил; «player»/«player_0»;
  per-attacker pending technique.

## Находки

### PLR-1 [P1] Смерть игрока: Head-путь не реализован — OnBodyCritical только Heart+Disabled

**Файл:** `Modules/Player/PlayerService.cs:184`; пересечение
`Modules/Combat/CombatService.cs:850-853`.
**Сценарий:** Head RedHP→0 → BodyCriticalEvent(Head, Disabled) публикуется
честно (BodyService.cs:267-278, любая vital-часть, Disabled|Severed), но
PlayerService матчит только `Heart && Disabled` → Die() не вызывается,
IsAlive=true. Одновременно CombatService:851 `!isPlayerTarget` исключает
игрока из D8-правила IsEntityAlive → единое правило тел НЕ покрывает
игрока: разрушенная голова не убивает, игрок «жив» с 0-HP головой
(BOD-2 подтверждён). Дополнительно Die()-гейт `if (!IsAlive) return`
(:151) при Heart-Severed молча не публикует PlayerDeathEvent.
**Доказательство:** PlayerService.cs:183-187; CombatService.cs:850-853;
DEATH_AND_LOOT.md §3.1 (стр.75-79) обещает «Head Disabled/Severed = смерть».
**Фикс:** OnBodyCritical → смерть при (Heart|Head) и (Disabled|Severed) +
снять `!isPlayerTarget`-фильтр в defenderDead (или единый владелец смерти
— BodyCritical-цепочка); убрать/инвертировать гейт в Die().

### PLR-2 [P1] Статы игрока никогда не инициализируются: StatService._base пуст всю игру

**Файл:** `StatService.cs:24,27,53,59,73,79` — 0 вызовов writers в
game/src; `EquipmentStatAggregator.cs:65` GetStatBonuses — 0 вызовов;
**Сценарий:** GetStat → `_base` (:47-48) → 0 всегда. StatProviderAdapter:88
→ CombatService attackerSTR/AGI/INT/Luck = 0 (:676-690): урон без
стат-бонусов §4.2, защита/додж игрока по AGI=0. AttackCooldownSeconds
(PlayerCombatAdapter.cs:281-285): `?? 10` не работает (GetStat возвращает
0, не null) → кулдаун всегда 1.0с, AGI-ускорение §8.2 мёртво. Весь
прогресс-контур (экипировка/культивация → статы) не работает.
**Фикс:** spawn-фаза SetBaseStat; проводка EquipmentStatAggregator →
AddBonus/RemoveBonus по EquipmentChangedEvent; публикация StatChangedEvent
(см. PLR-9).

### PLR-3 [P2] Стойка G (Shield): двойное списание Ци 25%+25% — подтверждение QI-1

**Файл:** `CombatService.cs:897-906` (QiConsumeRequestEvent :903 +
QiBufferActivateRequestEvent :905 → QiBufferService.cs:91 TryConsumeQi
повторно). Триггер — PlayerCombatAdapter.cs:246-273 (DefenseIntentEvent,
проводка Player→Combat без искажений). Репортит qi-аудит (QI-1);
подтверждено. **Фикс:** убрать явный QiConsumeRequestEvent.

### PLR-4 [P2] Defense-техника: двойное списание Ци (зарядка QiCost + активация буфера)

**Файл:** `PlayerTechniqueCaster.cs:230-237` + TechniqueChargeService.cs:
270-281 (drain QiCost тиками) + QiBufferService.cs:86-99 (Activate →
TryConsumeQi(invest)).
**Сценарий:** зарядка уже списала QiCost; FireTechnique(Defense) публикует
QiBufferActivateRequestEvent(invest=max(50,QiCost)×potency) → Activate
списывает ЕЩЁ раз. Итого ≈2×QiCost. Qi-аудит QI-1 считал Defense-технику
«однократной» — не учтён платёж зарядки. (Barrier: contourQi+shield —
платежи семантически разные, не репортится.)
**Фикс:** Defense-ветка без повторного списания (режим «предоплачено»).

### PLR-5 [P2] Сон/отдых — мёртвая механика: R не экспонирована, StartSleep недостижим

**Файл:** `PlayerInputService.cs:25,125` (_rest приватно; IsRestPressed в
IPlayerInputService НЕТ); PlayerService.cs:77-83 (StartSleep — 0 вызовов);
PlayerContracts.cs:18 (PlayerSleepEvent — 0/0).
**Сценарий:** R регистрируется (InputMapInitializer.cs:42), sticky «rest»
пишется, потребителей нет → R ничего не делает; SleepState всегда Awake;
ConsolidateSleep/AddVirtualDelta (рост статов во сне, STAT_THRESHOLD §5)
не вызываются. PlayerConfig: 8/12 полей мертвы (ConsolidateSleep
хардкодит 4f/0.20f — StatService.cs:83,86).
**Фикс:** IsRestPressed + связка R→StartSleep→WakeUp→ConsolidateSleep
(или удалить фичу и конфиг-поля).

### PLR-6 [P2] Техники (Z/слоты 3-9) не видят животных — подтверждение CMB-5

**Файл:** `PlayerTechniqueCaster.cs:301-318` (FindTargetInRange — только
_npcs; IAnimalService не инжектируется). Combat-аудит CMB-5 уже репортит;
подтверждено в Player-скоупе. **Фикс:** D1-паттерн
(IAnimalService + GetAliveAnimalsInRange).

### PLR-7 [P2] Модальность неполная: Space/Z/G/X работают при открытых окнах

**Файл:** `InputAdapter.cs:116-122` (attack/defend/cast_technique —
фильтр ТОЛЬКО _isOverUI «курсор над UI»); GWC:1164-1183 (X и Z — без
фильтров вовсе), :1192 (CombatAdapter.Tick — Space/G), :1150-1151 (T —
гейт только lootWindow). PlayerInputService.cs:115 InputDisabled —
0 сеттеров: гварды `!InputDisabled` (:67-96) — ложная защита.
**Сценарий:** инвентарь/диалог/книга техник открыты, курсор ВНЕ панели →
Space инициирует бой (EventBus синхронный, паузу не уважает), Z кастует,
X циклит технику. Движение закрыто честно (GWC:1293 IsPaused).
R13-фикс покрыл только lootWindow. **Фикс:** modalOpen-гейт (паттерн
_UnhandledInput:1228-1238) для боевого ввода; InputDisabled — проводка
или удаление.

### PLR-8 [P2] Movement-техника (рывок): нет клампов координат и проходимости

**Файл:** `PlayerTechniqueCaster.cs:240-248`; PlayerService.SetPosition
(:92-101) без клампов/TileService.
**Сценарий:** SetPosition(player±3) у края карты → отрицательные/внекартные
координаты (PlayerModule.MaxX/MaxY живут только в мёртвом коде);
PlayerPositionChangedEvent с мусорными координатами уходит в NPC AI;
рывок сквозь камни/воду без ограничений.
**Фикс:** Math.Clamp(0..MaxX/MaxY) + проходимость тайла (дизайн: сквозь
препятствия по канону? — решить).

### PLR-9 [P2] StatChangedEvent мёртв → RecalculateHPFromVitality мёртв

**Файл:** IStatService.cs:15 (обещание публикации); StatService.cs:53-59 —
публикаций нет; BodyModule.cs:50,70,147-155 — подписка не срабатывает
никогда; BodyService.cs:705 — 0 вызовов вне мёртвого обработчика.
**Сценарий:** рост VIT не пересчитывает HP частей (BODY_SYSTEM §7.3 —
дока не работает; STAT_THRESHOLD_SYSTEM.md:240 «публикуются через шину» —
drift). Пересечение с Core-агентом — подтверждено.
**Фикс:** IPublisher<StatChangedEvent> в ModifyStat/SetStat/AddBonus.

### PLR-10 [P3] «В ауре» → ложный тост «Техника применено»; HeldTechniqueChangedEvent без UI

**Файл:** `PlayerTechniqueCaster.cs:161-164` (success «В ауре»);
GWC:536-555 — success-ветка тостит «✴ Техника применено» (Type=Combat →
default). Подписчики HeldTechniqueChangedEvent — только ChargeSimDebug.
**Сценарий:** первая Z паркует технику; игрок не знает про второе Z,
индикатора ауры нет. **Фикс:** отдельный тост + UI-индикатор
(событие уже несёт данные).

### PLR-11 [P3] AuraHold: Dissipate не вызывается при смерти/стуне/медитации

**Файл:** `AuraHoldService.cs:55,107` — вызовы: SaveStartedEvent +
technique_forgotten (PlayerTechniqueCaster.cs:102). TECHNIQUE_SYSTEM.md
§5.4 (стр.274) обещает «стюн/смерть/медитация».
**Сценарий:** смерть → респавн → техника всё ещё в ауре (декей тикает —
PlayerModule.Tick не гейтился по IsAlive) — можно выпустить «посмертно».
**Фикс:** PlayerDeathEvent-подписка → Dissipate("death").

### PLR-12 [P3] FireTechnique: кулдаун+мастерство тратятся при «Цель исчезла»

**Файл:** `PlayerTechniqueCaster.cs:186-204` — CompleteUse (:186) ДО
FindTargetInRange (:200). **Сценарий:** цель умерла/ушла за время
зарядки → PublishFail, но кулдаун пошёл, мастерство выросло, Qi потрачена
зарядкой. **Фикс:** валидация цели до CompleteUse.

### PLR-13 [P3] Метрики дистанции расходятся: евклид (NPCService) vs Chebyshev

**Файл:** `NPCService.cs:109-124` (SqrMagnitude ≤ range²) vs
`PlayerCombatAdapter.cs:316-318` (Math.Max; комментарий :289-294 обещает
Chebyshev). **Сценарий:** melee 2.5: диагональ (2,2) — евклид 2.83 > 2.5
→ НЕ кандидат, хотя Chebyshev=2 «в радиусе»: диагональный ближний бой
обрезан; ранжирование — гибридное. **Фикс:** единая метрика.

### PLR-14 [P3] Мёртвый код PlayerModule + мёртвые контракты

**Файл:** `PlayerModule.cs:85-199` — HandleKeyboardMovement/
HandleMouseMovement/SetMouseDestination не вызываются (движение — GWC,
комментарий :69-72 сам признаёт); PlayerMovedEvent публикуется только из
мёртвого кода → контракт мёртв. PlayerService.GetCurrentQi (:73) — 0
вызовов; PlayerSleepEvent — 0/0; StaminaChangedEvent — StaminaService не
существует. **Фикс:** удалить или пометить `// DEAD: future`.

### PLR-15 [P3] TechniqueSlotService: дубликат техники в нескольких слотах

**Файл:** `TechniqueSlotService.cs:76-89` (AssignSlot не проверяет
FindSlotForTechnique — одна техника в слотах 3 и 5); RestoreState
(:151-162) без валидации изученности (каст graceful-fail — ок).
**Фикс:** авто-очистка старого слота при повторном биндинге.

## Проверено чисто

- **Space-таргетинг (D1):** NPC ∪ животные, строго ближайший, LOS для
  ranged + тост-анти-спам 0.4с; кулдаун только на успешный интент
  (207-233); бэкофф на AttackRejected (173-180).
- **Спам-гейты:** §8.1-кулдаун атаки, G-анти-спам 0.3с, сброс стойки на
  CombatEnded (364-372, R16 на месте); режимы оружия 1/2 с экип-гейтом и
  авто-fallback в melee (199-203).
- **Аура-декей:** B3 deltaTime, минимум 1 Ци/тик, рассеивание при
  ≤QiCost/2 с возвратом 50% (131-159); SaveStarted → Dissipate (B5);
  Release без refund — верно.
- **Каст-контур:** Qi-списание только зарядкой для Combat-типов;
  ранняя валидация цели до StartCharge (118-126); Healing по худшим
  частям с клампом; Formation-гейт занятости; слоты 3-9 →
  TechniqueCastRequestedEvent (GWC:1655-1674), TechniqueForgotten →
  авто-очистка, save round-trip с клампом слотов.
- **InputMap:** полный реестр без дублей клавиш; hotbar_i = Key.Key0+i —
  корректно Key1..Key9.
- **Смерть по СЕРДЦУ:** Heart→Disabled → Die → PlayerDeathEvent →
  респавн 3с: полное лечение, Revive, телепорт, try/catch (GWC:1921-1970)
  — единственный живой путь смерти, работает.
- **Qi-кэш PlayerService:** без EntityId-фильтра, но QiChangedEvent
  публикует только QiService (игрок) — безопасно (латентно при NPC-Ци).

## OK-BY-DESIGN (с доказательством)

- Двойной ID игрока — PlayerIdResolver; Spawn «player_0» =
  PlayerConfig.DefaultPlayerId (заморожено).
- IsAlive fallback на _data.Health до Spawn (:65-66) — ок.
- Qi=long; пермил; per-attacker pending technique — заморожено.
- TechniqueUsedEvent.QiCost — информационный (аудит-3 C-2).

## Соответствие docs_v2

| Дока | Статус |
|------|--------|
| DEATH_AND_LOOT §3.1 (Head = смерть) | ✖ PLR-1 |
| DEATH_AND_LOOT §3.2 (экран смерти + выбор) | ✖ авто-респавн 3с без экрана |
| DEATH_AND_LOOT §4.3 (респавн на месте смерти) | ✖ телепорт в центр (GWC:1954-1960) |
| DEATH_AND_LOOT §4.2 (Ци→макс, баффы снимаются) | ✖ Revive пустой, PlayerReviveEvent без слушателей |
| TECHNIQUE_SYSTEM §5.4 (аура/декей) | ✔ кроме принудительного рассеивания (PLR-11) |
| STAT_THRESHOLD_SYSTEM (события/consolidation) | ✖ мёртв целиком (PLR-2/5/9) |
| BODY_SYSTEM §7.3 (VIT→HP пересчёт) | ✖ мёртв (PLR-9) |
| 06_player/ (8 файлов; доки сна/статов нет) | ✔ стыки живы |

*Аудит Player завершён. Код не изменялся (READ-ONLY). Приоритет: PLR-1 →
PLR-2(+9) → PLR-7 → PLR-4/3 (в связке с QI-1) → PLR-5/6/8 → P3.*

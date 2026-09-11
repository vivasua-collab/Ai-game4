# АУДИТ: Combat

**Дата:** 2026-09-11 (аудит-сессия 09-11, субагент combat-audit)
**HEAD на старте:** `d0b065d`
**Скоуп:** модуль Combat целиком (19 файлов ~4.6k строк; CombatService 1145 —
полностью в 3 прохода) + стыки: CombatContracts, PlayerCombatAdapter,
NPCCombatAdapter, AnimalService (боевые методы), NPCModule.ProcessNpcAttacks,
StrikeFxRenderer, EquipmentDataProvider/QiDataProvider/BodyService (проводки),
docs_v2/COMBAT_SYSTEM.md (594 строки). READ-ONLY: код НЕ изменялся.

## Сводка

- Прочитано полностью: CombatService (1145, в 3 прохода), DamageService,
  DamageCalculator, DefenseProcessor, NPCDefenseSelector, LevelSuppression,
  WeaponDamageCalculator, CombatRng, CombatLos, CombatRangeGateService,
  CombatConsequencesService, ElementalEffectService, StatProviderAdapter,
  CombatModule/ModuleServices/Config/TechniqueCapacity; ским — TechniqueService/
  TechniqueChargeService. Верификация: git-grep вызовов SetArmorCoverage/
  SetTotalArmor/SetEquipmentData/SetQiState; трассировка 5 публикаторов
  AttackIntentEvent, 13 подписчиков DamageAppliedEvent, потребителей
  EnemyKilled/NPCDeath.
- Находок: 15 (P1×1, P2×5, P3×9, вкл. doc-drift-пачку). Главное: слой
  брони (6-7) МЁРТВ для всех сущностей (нулевая проводка coverage);
  Qi-техники Ranged* gated на стрелы; Dodge не публикует событие.
  Фиксы R16 / аудита 09-10 / animal-фикса 09-11 — на месте, регрессов НЕТ.

## Находки

### CMB-1 [P1] Слой брони мёртв для ВСЕХ сущностей (coverage не установлен никем)

**Файл:** `Modules/Combat/DamageService.cs:291-302`; `Modules/Inventory/EquipmentDataProvider.cs:228-244`.
**Сценарий:** любой удар по любой цели в броне. `SetArmorCoverage()` не
вызывается НИКЕМ в game/src (rg: только определение + интерфейс; комментарий
«из NPCAssemblyService» ложен — там нет упоминаний Coverage). После P2-6.2
(default 100→0) `GetArmorCoverage` всегда 0 → `armorCoversHit=false` → ВСЕГДА
ветка «броня не покрыла» (noArmorContext, armorValue=0). DefenseProcessor с
бронёй (СЛОЙ 6-7 §2) не выполняется ни разу; penetration (C6) и GetTotalArmor
считаются впустую. Экипировка игрока синкается корректно (EquipmentService.cs:212-213
→ SetEquipmentData пишет _cachedTotalArmor), но значение не используется.
**Доказательство:** DamageService.cs:298-300; 0 вызовов SetArmorCoverage по
репо; QA COMBAT_SIM броню не покрывает — регрессия P2-6.2 (2026-05-22) незамечена.
**Фикс:** (а) SetArmorCoverage при сборке NPC + в SetEquipmentData игрока; ИЛИ
(б) считать coverage в DamageService из EnumerateEquipment (паттерн
GetDodgeBonusPermil); ИЛИ (в) убрать coverage-гейт как дублирующий (агрегатор
уже взвешивает Defense по Coverage, EquipmentStatAggregator.cs:36). + QA-кейс.

### CMB-2 [P2] Ranged-техники Ци требуют и списывают стрелы

**Файл:** `Modules/Combat/CombatModule.cs:180-212`; `Modules/Player/PlayerTechniqueCaster.cs:206-210`.
**Сценарий:** каст Combat-техники подтипа Ranged* (Ци-снаряд, DamageType.Qi —
CombatService.cs:1030-1034). CombatModule гейтит ЛЮБОЙ e.IsRanged как выстрел
из лука: нет стрел → отклонение «нет стрел — колчан пуст» (190-196); есть →
списание 1 ammo_arrow за каст (209-211). Комментарий CombatService.cs:627
прямо говорит «стрела — материя, не Ци-снаряд» — гейт не различает источник.
**Фикс:** флаг isWeaponShot (только лук-атаки PlayerCombatAdapter) либо
проверка AttackRange оружия для ammo-гейта.

### CMB-3 [P2] Dodge не публикует DamageAppliedEvent — «уклонение» невидим

**Файл:** `Modules/Combat/DamageService.cs:199-203`.
**Сценарий:** успешное уклонение (стойка G-Dodge игрока; NPC-защита Dodge —
все NPC без щита/оружия). Ранний return БЕЗ Publish. Все потребители теряют
исход: «уклонение»-текст не всплывает (DamageNumberRenderer.cs:132-135 —
МЁРТВАЯ ветка, рендерер её ждёт; StrikeFxRenderer.cs:8-9 обещает «уклонение
пишет текстом DamageNumberRenderer»), EventLogWindow молчит, QA-счётчики
промахов не видны. Публикация Damage=0 безопасна: Consequences (Damage<=0 →
return), BodyService (no-op), StrikeFx (фильтр Result), NPCAdapter (0-урон ок).
**Фикс:** публиковать DamageAppliedEvent(…, 0, Result=Dodge) перед return.

### CMB-4 [P2] Броня незарегистрированных целей = кэш ИГРОКА

**Файл:** `Modules/Combat/DamageService.cs:266-274`.
**Сценарий:** цель вне IEquipmentDataProvider (животные — AnimalService ничего
не регистрирует) → `armorValue = _cachedTotalArmor` — кэш
EquipmentChangedEvent ИГРОКА (заполняется ТОЛЬКО событием игрока, 127-129).
Волк получил бы броню игрока. Сейчас маскировано CMB-1, но любой фикс CMB-1
вскроет. **Фикс:** для незарегистрированной цели armorValue=0 (+ видовая
«шкура» через SetTotalArmor при спавне животных).

### CMB-5 [P2] Техники (Z/панель) не видят животных — D1 покрыл только Space

**Файл:** `Modules/Player/PlayerTechniqueCaster.cs:301-318`.
**Сценарий:** каст Combat-техники рядом с волком → FindTargetInRange перебирает
ТОЛЬКО `_npcs.GetNearbyNPCIds` → «Цель исчезла». IAnimalService не инжектируется
в кастер вовсе. Жалоба «посох→волк=0» возвращается через второй вход.
**Фикс:** дублировать D1-паттерн (IAnimalService + GetAliveAnimalsInRange).

### CMB-6 [P2] NPC-vs-NPC: инверсия победителя при смерти инстагатора

**Файл:** `Modules/Combat/CombatService.cs:303-317`.
**Сценарий:** NPC A инициировал бой с NPC B (достижимо: ProcessNpcAttacks
поддерживает NPC-цели, NPCCombatAdapter.cs:120-125 помечает обоих). B
контратакует и убивает A → defender=A умирает → Victory (870) → EndCombat
фолбэк «winner=_instigatorId» = A (ТРУП), loser=B (убийца). NPCCombatAdapter
(166-191) инвертирует отношения: убийца получает −20 к трупу. Для боёв с
игроком корректно (C-1 игроко-центричные ветки).
**Фикс:** запоминать killerId при смерти защитника; в EndCombat NPC-vs-NPC
брать сохранённого победителя, а не инстагатора.

### CMB-7 [P3] Резолв защитника по строгому `==` вместо AreSameEntity

**Файл:** `Modules/Combat/CombatService.cs:552, 640`. Латентно: при смешении
алиасов «player»/«player_0» в одном бою → defender = сам атакующий
(self-hit). Сегодня все 5 публикаторов шлют канонический «player_0» —
безопасно; рядом гейты уже используют AreSameEntity (471, 485-488).
**Фикс:** AreSameEntity в обоих местах.

### CMB-8 [P3] DamageService: литерал `"player"` вместо PlayerIdResolver

**Файл:** `Modules/Combat/DamageService.cs:390`. `targetEntityId != "player"`
— для «player_0» истинно → TryConsumeQi(player_0). Двойного списания НЕТ:
QiDataProvider хранит только NPC (SetQiState — NPCSpawner/NPCService/
PerkService/NPCQiRegen), QiService обрабатывает событие по AreSameEntity
(QiService.cs:316-323). Латентно при будущей регистрации игрока в провайдере.
**Фикс:** `!PlayerIdResolver.IsPlayer(...)`.

### CMB-9 [P3] StatProviderAdapter: неизвестная сущность получает статы ИГРОКА

**Файл:** `Modules/Combat/StatProviderAdapter.cs:87-89`. Класс-док обещает
«Иначе → 0», но фолбэк — статы игрока без проверки ID. Животные закрыты D7
(74-85); прочий неизвестный ID (typo/будущая сущность) получит AGI/STR
игрока. **Фикс:** IsPlayer-гейт перед фолбэком.

### CMB-10 [P3] ExecuteDefense: Qi-щит читает кэш Ци игрока для любого defenderId

**Файл:** `Modules/Combat/CombatService.cs:896-907`. Shield-ветка списывает
_cachedCurrentQi (кэш ИГРОКА) без проверки defenderId. Сегодня
DefenseIntentEvent публикует только PlayerCombatAdapter — безопасно.
**Фикс:** IsPlayer-гейт перед Shield-веткой.

### CMB-11 [P3] «Единое правило смерти» разошлось в трёх реализациях

**Файл:** `CombatService.cs:850-853` vs `NPCCombatAdapter.cs:247-249`,
`AnimalService.cs:522-524`. CombatService: `IsFatal || !IsEntityAlive` (БЕЗ
дренажа); адаптеры: `!IsEntityAlive || GetCurrentHealth<=0`. Практически
эквивалентно: RedHP клампится `Math.Max(0,…)` (BodyPart.cs:118) ⇒ дренаж ⇒
все части 0 ⇒ витальные мертвы. Но формулировка D8 «единое правило» не
выдержана — три копии по-разному. **Фикс:** хелпер BodyDeathRules.IsDead.

### CMB-12 [P3] LOS-гейт модуля не резолвит животных; дистанция не гейтится вовсе

**Файл:** `Modules/Combat/CombatRangeGateService.cs:89-109`. TryResolveTile:
животные → false → HasLineOfSight=true (стрелы по зверям сквозь камни на
уровне авторитетного гейта). Для игрока компенсировано адаптерным LOS-фильтром
(PlayerCombatAdapter.cs:321-327 — позиция AnimalInfo есть). ExecuteAttack
дистанцию не проверляет (на совести источников) — имя «RangeGate» обещает
больше, чем делает. **Фикс:** инжект IAnimalService в резолвер.

### CMB-13 [P3] Стрела списывается на принятом интенте, каст может прерваться

**Файл:** `Modules/Combat/CombatModule.cs:207-212`. Списание ammo при
Accepted — старт pending-каста (~0.5с), а не в резолве. Прерывание каста
уроном (C11, CombatService.cs:241-250) теряет стрелу без выстрела.
**Фикс:** списывать в резолве (pending-флаг) / возврат при прерывании.

### CMB-14 [P3] StrikeFxRenderer: без FX для животных + IsPlayer-литерал

**Файл:** `Adapter/Scene/StrikeFxRenderer.cs:217, 233-234`. «Животные/
неизвестные — без FX пока» — удары по волку без дуг (цифры есть);
`id == "player" || id == "player_0"` дублирует нормализацию (безопасно).
**Фикс:** позиция животного через IAnimalService; PlayerIdResolver.

### CMB-15 [P3] Doc-drift COMBAT_SYSTEM.md (пачка)

- §1.2 «superSuperSlow при начале боя» — в коде НЕТ (0 вхождений; подписчики
  CombatStartedEvent: Qi/NPCAI/NPCCombatAdapter — никто не меняет скорость).
- §10.1 Water slow «−20% / 2 тика» vs код −30% / 3 сек (ElementalEffectService.cs:138-142);
  Light purify «1 дебафф» vs ВСЕ дебаффы (195-203); Earth stun «1 тик» vs 2.0 сек (149-156).
- §11.1/§12.1 Bleed «1–10 HP/тик по степеням» vs код 5% maxHP × 3 тика
  (CombatConsequencesService.cs:120-124); Poison «стадии» vs плоские 3% × 3 тика
  (ElementalEffectService.cs:209-220).
- §2 Слой 7 «плоское вычитание effectiveArmor×0.5» — в коде только процентная
  формула armor×1000/(armor+100) (DefenseProcessor.cs:75-79); §9.2
  critDamageBonus — не реализован (крит фикс. ×1.5, DamageService.cs:207-213).
**Фикс:** синхронизировать таблицы §10.1/§11.1 под реализацию (или метки
«future»), убрать superSuperSlow-утверждение §1.2.

## Проверено чисто

- **Turn-gate (цель 1):** единственный владелец хода (CombatService.cs:98-102);
  инициатива инстагатора (280); переход хода только в резолве (880);
  EnemyTurnTimeout 2.5с — только не-игрок, отключён на время каста (957-968);
  гейт участника P1-4 (456-465); цель боя — только игрок (478-492). NPC-темп
  1.6с+0.5с < 2.5с; волчий кулдаун 2 тика < 2.5с — окна честные.
- **Гашение каста (R16 P1-1):** все пути выхода сходятся в EndCombat
  (Victory/Defeat 872; Flee — AbandonCombat 172, MaxDuration 953) →
  _isCasting=false, pending=default (359-372). _isCasting живёт только внутри
  боя → перманентный лок невозможен.
- **Инстагатор-семантика (C-1, регресса НЕТ):** isPlayerAttacker/isPlayerTarget
  по PlayerIdResolver (672, 770); fatal-ветка виктим-центрична (860-871);
  ExecuteDefense → IsPlayer (889). **_lastPlayerDefense vs NPC-защита (R16 D4):**
  игрок — своя стойка (780-782); NPC — NPCDefenseSelector на каждой атаке
  (786-797) + QA-геттер LastNpcDefenseSelected (797); селектор pure, без RNG.
- **Pending-техника M1/M2:** цель/potency/ranged переживают каст (552-563,
  970-984) — self-hit и «чужая зарядка» закрыты.
- **Victory при смерти по телу (D8):** `IsFatal || !IsEntityAlive` (850-853);
  двойного EndCombat нет (гейт 287). NPCDeathEvent при смерти защитника:
  NPCCombatAdapter (247-256), AnimalService (522-531) — труп/killfeed;
  killfeed-дедуп < 2с (EventLogWindow.cs:141-149).
- **Пермил-математика (цель 2):** урон long×промилле без float во всём
  пайплайне; капы 800/600/700/500/500/800‰; таблицы подавления/попаданий = доке
  (Constants.cs:230-238, 588-601; морфотаблицы суммой 1000 — аудит Core); крит
  ×1500‰; Dodge=0, Parry/Block=×500‰; floors per-part — OK by design; Element-таблица
  (DamageCalculator.cs:87-111). **QiBuffer (СЛОЙ 5):** inline-формулы =
  константам; per-entity NPC + кэш игрока (P2-4.1); P0-X1 EntityId; Pure-урон
  обходит буфер и броню (256-260).
- **CombatRng (цель 6):** SeededRandom seed=12345, singleton
  (CombatModuleServices.cs:24), внедрён в 3 сервиса; Random.Shared в модуле нет.
  **CombatLos:** Bresenham integer, крайние тайлы исключены (55), блок — только
  непроходимые объекты (73-74), null-tiles → fallback.
- **Ammo (цель 5, оружие):** наличие ДО боя (190), списание только после
  Accepted (209-211), отклонённый каст стрелу не тратит (180). NPC — безлимит
  by design. (Отступления — CMB-2/CMB-13.)
- **Стыки:** Space-таргетинг NPC ∪ животные (PlayerCombatAdapter 332-350);
  G-цикл с экип-гейтами/анти-спамом (246-273), сброс на CombatEnded (364-372);
  месть волка: чейз+укус ≤2, кулдаун 2 тика, de-aggro >5 → Disengage →
  AbandonCombat (AnimalService 405-465); NPC-атаки: дистанция оружия + 1.6с
  (NPCModule 151-197); тосты отклонений только для игрока (GWC:1910-1915).
- **Consequences/Elemental (цель 3):** подписки в конструкторе, Dispose
  парный; контракты читаются полностью; blunt ×200‰ bleed; stun ×100 (P0-7.1);
  shock 30%; poison/burn DoT через IBuffService (эвристика buffId — известный
  баг Buff-агента; проводка контрактов чистая).
- **DamageAppliedEvent (цель 7):** поля полные (8); потребители: BodyService
  (HP), DamageNumberRenderer (цифры), EventLogWindow (лог), StrikeFxRenderer
  (анимация), NPCAIService (угроза), адаптеры (HP/смерть), CombatService
  (прерывание каста). CombatStarted — только StartCombat; Ended — только
  EndCombat (идемпотентен); Disengage → Flee-EndCombat парный.

## OK-BY-DESIGN (с доказательством)

- Qi = long / QiCost int-каст (CombatService.cs:1106) — замороженное решение.
- Двойной ID игрока — PlayerIdResolver везде в Combat (кроме латентных
  CMB-7/8/14); EquipmentService синкает оба алиаса (212-213).
- Урон в мёртвую часть теряется (floors): RedHP клампится в 0 (BodyPart.cs:118).
- per-attacker pending technique — единственный PendingTechnique на бой
  (известный TODO; корректен в 1v1 MVP). Element.Poison остаётся (Poison-DoT
  проводка в ElementalEffectService).

## Соответствие docs_v2 (COMBAT_SYSTEM.md)

| Раздел | Статус |
|--------|--------|
| §1.4.1 ИИ NPC (месть/бегство/leash/селектор/kiting/гашение каста/смерть по телу/животные) | ✔ |
| §1.4.2 стойка G | ✔ |
| §1.4 turn-gate/инициатива/тайм-аут 2.5с | ✔ |
| §7 попадание (формулы 7.1-7.3) + ссылка на StrikeFxRenderer | ✔ |
| §4.1-4.3 бонусы статов/оружия (WeaponDamageCalculator) | ✔ |
| §6 подавление уровней (таблица = код) | ✔ |
| §2 слои 6-7 (броня) | ✖ код ≠ доке И код мёртв (CMB-1) |
| §10.1/§11.1 стихии/DoT-таблицы | ✖ drift (CMB-15) |
| §1.2 superSuperSlow | ✖ не реализован (CMB-15) |
| Q5 детерминизм (seed 12345, 3 потребителя) | ✔ |

*Аудит Combat завершён. Код не изменялся (READ-ONLY). Приоритет фиксов:
CMB-1 (броня) → CMB-2 (стрелы за техники) → CMB-3 (Dodge-событие) → CMB-4
(в связке с CMB-1) → CMB-5/CMB-6 → P3-пачка.*

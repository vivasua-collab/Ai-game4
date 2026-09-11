# Чекпоинт: Полный аудит проекта 2026-09-11 + P1-пакет фиксов

**Дата:** 2026-09-11 08:00–11:30 UTC
**Сессия:** №9 (полный аудит по запросу пользователя: «полный аудит проекта. Последовательно каждый модуль. Для каждого модуля свой файл чекпоинт»)
**Тип:** audit + fix
**HEAD на старте:** d0b065d → аудит закоммичен (2072fde) → фиксы (этот коммит)

---

## Контекст

Пользователь запросил полный аудит всех модулей с индивидуальным
чекпоинт-файлом на каждый. Выполнено 4 волны из 9 параллельных
аудита-агентов (glm-5.3, READ-ONLY): каждый модуль прочитан целиком,
находки верифицированы в коде главным агентом перед фиксом.

## Что сделано

### Этап 1 — Аудит (18 чекпоинт-файлов, коммит 2072fde)

| Чекпоинт | Модуль | P1 | P2 | P3 |
|----------|--------|----|----|----|
| 09_11_audit_core_layer.md | Core (DI/EventBus/Contracts) | 0 | 4 | 17 |
| 09_11_audit_world_tile.md | World + Tile | 1 | 6 | 5 |
| 09_11_audit_body.md | Body | 2 | 5 | 4 |
| 09_11_audit_buff.md | Buff | 2 | 6 | 5 |
| 09_11_audit_qi.md | Qi | 2 | 7 | 5 |
| 09_11_audit_charger.md | Charger | 0 | 4 | 4 |
| 09_11_audit_inventory.md | Inventory | 6 | 6 | 9 |
| 09_11_audit_combat.md | Combat | 1 | 6 | 9 |
| 09_11_audit_npc.md | NPC | 0 | 7 | 9 |
| 09_11_audit_formation.md | Formation | 0 | 5 | 7 |
| 09_11_audit_generator.md | Generator | 2 | 3 | 9 |
| 09_11_audit_player.md | Player | 2 | 9 | 7 |
| 09_11_audit_quest.md | Quest | 1 | 4 | 5 |
| 09_11_audit_interaction.md | Interaction | 0 | 2 | 6 |
| 09_11_audit_trade.md | Trade | 1 (латентно) | 2 | 6 |
| 09_11_audit_save.md | Save | 0 | 2 | 6 |
| 09_11_audit_entry.md | Entry (GameSession/16 фаз) | 0 | 4 | 6 |
| 09_11_audit_adapter.md | Adapter (Scene/UI/Input) | 0 | 2 | 8 |

**Итого: P1×19, P2×85+, P3×137+** (~200 файлов прочитано).

### Этап 2 — P1-пакет фиксов (14 дефектов + 2 попутных, этот коммит)

Верифицированы в коде лично (правило R13: claims сверять с кодом):

1. **PLR-2/C-1/PLR-9/BOD-8 — статы игрока никогда не инициализировались**
   (`StatService.cs`, `PlayerModule.cs`, `IStatService.cs`, `BodyModule.cs`):
   SetBaseStat звал никто → GetStat=0 всегда → бой без STR/AGI/INT/Luck.
   Фикс: InitializeDefaults("player") — канон SoulGenerator Character
   (10/10/10/10, Luck 5, Perception 10), идемпотентно из PlayerModule.Start;
   ModifyStat/SetStat/ConsolidateSleep публикуют StatChangedEvent (контракт
   интерфейса «с публикацией» не выполнялся!) → BodyModule. OnStatChanged →
   RecalculateHPFromVitality (VIT→HP, П.24) оживает; сравнение алиас-безопасно.

2. **BOD-2/PLR-1 — единое правило смерти не покрывало игрока**
   (`PlayerService.cs`, `CombatService.cs`, `IBodyService.cs`, `BodyService.cs`):
   Head RedHP→0 не убивал (OnBodyCritical ждал только Heart+Disabled);
   CombatService defenderDead исключал игрока (!isPlayerTarget); Die()-гейт
   `!IsAlive` глотал PlayerDeathEvent при vital-смерти; IsAlive=«сердце не
   ампутировано» = всегда true. Фикс: OnBodyCritical (Head|Heart)×(Disabled|
   Severed) → Die; IsAlive = IsEntityAlive (vital-правило, экспортировано в
   IBodyService); Die с флагом _deathAnnounced (сброс в Revive); defenderDead
   без !isPlayerTarget. **Попутно BOD-10**: BodyService HasEntity/
   IsEntityAlive/GetMaxHealth/GetCurrentHealth/OnCultivationLevelChanged —
   PlayerIdResolver.AreSameEntity вместо строгого == (без этого «player_0»
   падал в NPC-ветку = «мёртв» — фиксы смерти без алиас-безопасности
   убивали бы игрока в первом же бою).

3. **BOD-1 — таблицы попаданий оперировали несуществующими частями тел**
   (`Constants.cs` MorphologyHitTables): Quadruped-таблица юзала гуманоидные
   LeftArm/RightArm/LeftHand/RightHand/LeftLeg/RightLeg (46% попаданий мимо
   → фолбэк Torso: ноги зверя недостижимы, 81% в торс); Bird — крылья как
   LeftArm/RightArm, хвост как Tail (43% мимо); Serpentine — Tail 500‰ не
   существует; **Arthropod — 500‰ мимо + нет Torso → фолбэк в Heart (24 HP)
   → паук умирал от пары ударов**; Amorphous — Torso 500‰ не существует →
   null → 50% ударов по призраку терялись. Фикс: все 5 таблиц ретаргетнуты
   на фактические части тел шаблонов (FrontLeftLeg/BackLeftLeg/Tail,
   LeftWing/RightWing/BirdTail, BodySegment1/2/SerpentineTail,
   Cephalothorax/Abdomen/Leg1-8/Pedipalps/Chelicerae, Core/Essence), суммы
   = 1000‰.

4. **BUF-1/2/3/4/5 + BOD-4 — корневые дефекты системы баффов**
   (`BuffService.cs`, `BuffCalculator.cs`):
   - BUF-1: GetStatModifierPermil конвертировал CalculateStatModifier, где
     при baseValue=0/flatSum=0 (чисто процентные баффы — все!) модификатор = 0
     → слои 3a/3b урона не работали ВООБЩЕ. Фикс: CalculatePercentSum
     (аддитивный процент) + кламп [−90%…+300%].
   - BUF-2: «combat_bleed» не распознан эвристикой → AttackBoost +10%
     (кровотечение не наносило урона). Фикс: ветка Bleed DoT, тик из potency
     (5% maxHP).
   - BUF-3: «combat_shock»/«elemental_void_pierce» → бафф +урона раненому.
     Фикс: shock = AttackReduction −20% (урон), void_pierce = DefenseReduction
     −30% (броня).
   - BUF-4: DoT-тики хардкодом (poison 10/burn 15). Фикс: тик из producer-
     potency (3% maxHP / 5% урона).
   - BUF-5: duration<0 → «30 сек» вместо Permanent (ампутационные дебаффы
     истекали!). Фикс: duration<0 = Permanent (контракт ApplyBuff default −1).
   - BOD-4: «severed_*» → AttackBoost+10% без статов. Фикс: суффикс-
     маппинг (_str→AttackReduction/Strength, _vit→DefenseReduction/Vitality,
     _agi→SpeedReduction/Agility), величина из producer-potency (−0.15 и т.п.).
   - Нормализация potency-семантики: маппинг вбирает producer-potency в
     Value/TickDamage, buff.Potency=1 (раньше elemental_slow: Value=−0.3 ×
     Potency=300 → TotalValue=−90 = −9000% при починке BUF-1 — бомба
     обезврежена). Перки (perk_*) → Conductivity (не урон).

5. **CMB-1/CMB-4/INV-5/NPC-3 — слой брони 6-7 был мёртв для всех**
   (`EquipmentDataProvider.cs`, `NPCSpawnerService.cs`, `NPCService.cs`,
   `DamageService.cs`): SetArmorCoverage НИКТО не вызывал (после P2-6.2
   default coverage 0 → armorCoversHit всегда false); игрок — raw Defense
   без грейд-множителей; NPC — «+5 за предмет»; RestoreState затирал урон
   оружия/броню NPC при каждой загрузке; незарегистрированные цели (звери)
   получали броню ИГРОКА из кэша. Фикс: RecomputeAggregatesFromData (общий
   для игрока и NPC: грейд-множители, coverage = средний по броневым
   предметам); SetEquipment (NPC) резолвит ID→EquipmentData и пересчитывает
   агрегаты сам; NPC-спавн добавляет BaseDefense (natural) поверх + coverage
   (100 при natural без носимой); RestoreState — честные агрегаты поверх
   SetEquipment; DamageService-фолбэк для незарегистрированных = 0 (кэш
   брони игрока удалён вместе с подпиской).

6. **QI-1/PLR-3 — двойное списание Ци за стойку щита (G)**
   (`CombatService.cs`): QiConsumeRequestEvent (QiService→TryConsumeQi) ПОВЕРХ
   QiBufferActivateRequestEvent (QiBufferService.Activate→TryConsumeQi) = 50%
   Ци за одно действие. Фикс: явный publish удалён (изъяты поле+параметр
   конструктора) — активация буфера сама инвестирует 25% и возвращает
   остаток (QI-A05).

7. **INV-1 — кэш itemCount присваивался счётом одной кучки** при мульти-кучках
   (сплит R10) → крафт/продажа/перенос видели не весь запас. Фикс: `+count`
   зеркально ветке заполнения стака.

8. **INV-2/INV-3/INV-17 — три точки тихой потери предметов при volume-full**
   (`InventoryModule.cs`, `BeltService.cs`): unequip/замена экипировки (2-arg
   TryAddItem→false→предмет исчезал); возврат из пояса при частичном добавлении
   (слот очищался целиком); harvest (тайл уже истощён до публикации — ресурс
   исчезал бесследно). Фикс: единый overflow-паттерн (3-arg TryAddItem +
   DropItemsNearPlayer / остаток в слоте пояса); в IInventoryService добавлена
   транзакционная 3-arg перегрузка; OnEquipmentChanged — PlayerIdResolver
   вместо == "player".

9. **CMB-2 — Ци-техники Ranged* требовали стрелы** (`CombatModule.cs`):
   ammo-гейт не различал лук и Ци-снаряд. Фикс: isWeaponShot (TechniqueId
   "basic_attack"/пусто) — стрелы только оружейным выстрелам (гейт наличия +
   списание после Accepted).

10. **CMB-3 — уклонение не публиковало DamageAppliedEvent** (мёртвая ветка
    «уклонение» в DamageNumberRenderer, молчаливый лог). Фикс: честное
    событие (Damage=0, Result=Dodge); StrikeFxRenderer фильтрует Dodge
    by-design; BodyService применяет 0 (безопасно).

11. **WT-1/WT-2/WT-7 — респавн ресурсов был мёртв в живой игре**
    (`ResourceService.cs`, `TileModule.cs`): Initialize() (единственная
    DayChanged-подписка) никто не вызывал; сравнение по дню МЕСЯЦА (1-30,
    оборот) → ресурс с ≥24-го числа не респаунился никогда; задержка хардкод
    7 дней. Фикс: Initialize из TileModule.Start (идемпотентно, инъекция
    IResourceService); абсолютный день (TotalMinutes/1440); RespawnDays из
    ObjectDefaults (берёза 5/куст 3/камень 14/руда 30/трава 2).

12. **CMB-5/PLR-6 — техники (Z/слоты 3-9) не видели животных** (D1-фикс
    закрыл только Space-атаку): FindTargetInRange только NPC → «Цель
    исчезла» на волке. Фикс: IAnimalService-инъекция + кандидаты NPC ∪ звери
    (D1-паттерн PlayerCombatAdapter).

13. **NPC-7 — фантомная угроза «sever_unknown»** (`NPCAIService.cs`):
    ампутация → Threats["sever_unknown"]=100 → после таймаута Fleeing →
    Attacking на фантом → фолбэк движения «не-NPC = игрок» → NPC бил игрока
    без агро (то же с «dot:{buffId}» от DoT-тиков). Фикс: фантом удалён
    (бегство сохранено); GetTopThreatId валидирует цель (игрок или живой
    NPC).

14. **ADP-2 — цифры 1-9 в диалоге — двойное действие** (выбор реплики +
    смена режима/каст техники): hotbar-цикл без !_isOverUI-гейта. Фикс:
    гейт симметрично attack/defend/cast_technique.

### QA-верификация

Build: **0 errors** (384 warnings — базовый уровень). Полная регрессия —
**15/15 VERDICT: PASS**: COMBAT_SIM, COMBATAI, ANIMALQA, LOOT, DOT (bleed
ветка подтверждена), SAVELOAD, KILLFEED, QUEST, STORAGE, HOTBAR, TRASHDROP,
CONTEXT, WEAPONVIS, REASSEMBLY, CHARGE.

## Решения

- **Двухэтапная дисциплина**: аудит закоммичен ОТДЕЛЬНО (2072fde) до фиксов
  — история находок не смешана с правками; каждый чекпоинт модуля — сам-
  достаточный артефакт с file:line.
- **P1-пакет сейчас, «полнота сейва» — отдельным эпизодом R17**: 8 из 19 P1
  (QI-2 Qi-стейт вне сейва, QST-1 квесты, TRD-1 валюта, INV-4 кукла/пояс,
  G-1 легаси-ID коллизии, G-2 фантомные ID при cold-load, NPC-1 звери
  переживают LoadGame, E-3 контракт SkipOnLoad) — одна когерентная
  архитектурная семья «state не в сейве/не сбрасывается»; чинить их россыпью
  = повторить R14-патчинг вслепую. Отдельная сессия с расширением
  SaveLoadSimDebug.
- **G-1+NPC-6 отложены парой**: легаси-экипировка NPCAssemblyService.
  EquipHumanoid затирается EquipFromGenerator («Матрёшка») — рефакторинг
  «один генератор экипировки NPC» меняет сборочный пайплайн, требует своего
  QA-прогона.
- **BOD-10 обезврежен ДО BOD-2**: алиас-безопасность BodyService —
  предусловие фикса смерти игрока (иначе «player_0» строгое == → «не найден
  = мёртв» → мгновенная смерть в первом бою).

## Найденные проблемы (не исправлено — очередь)

Полные списки — в 18 чекпоинтах аудита. Топ очереди:

- **R17 «Полнота сейва»** (P1-семья): QI-2, QST-1, TRD-1, INV-4, G-2,
  NPC-1, E-1/E-3, WT-5 (время), SAV-1 (атомарная запись tmp+rename), SAV-2
  (автосейв из меню), CH-2 (warm-load зарядника), F-2/F-4 (формации),
  NPC-3-остаток (сейв сам дописывает агрегаты — половина закрыта тут).
- **R18 кандидаты**: G-1+NPC-6 (один генератор экипировки NPC), NPC-2
  (NPC ходят сквозь стены — greedy-подшаги с IsWalkable), NPC-5 (смерть
  лидера группы), NPC-4 (отношения в сейве), WT-3 (large_world не в
  реестре WorldService), E-2 (провал фазы не останавливает вход в мир),
  ADP-1 (вложенные модальные окна — залипание паузы), BOD-3 (усиления
  негуманоидов), BOD-6 (ампутации не переживают load), PLR-5 (сон мёртв),
  QI-3/QI-4 (реген-модели дрейф + L10 long.MinValue), F-3 (бонусы формаций
  глобальны), DOC-синк пакет (CMB-15, WT-доки, DI_AND_EVENTBUS-реестр).

## Следующие шаги

1. Живой QA на ПК (вечером у пользователя): бой с учётом статов (теперь
   STR/AGI влияют), броня работает (игрок/NPC), кровотечение/шок ранят,
   ампутации дают честные дебаффы, смерть игрока по голове, «уклонение» в
   цифрах урона, волк достижим по ногам, паук не умирает с одного удара,
   респавн берёз/камней, стойка щита ест 25% Ци (не 50%), техники по зверям.
2. R17 «Полнота сейва» — отдельная сессия (P1-семья из 8+ дефектов).
3. R18 — очередь из чекпоинтов аудита.

## Файлы

- Аудит: checkpoints/09_11_audit_*.md (18 файлов, коммит 2072fde)
- Фиксы: StatService.cs, IStatService.cs, PlayerModule.cs, PlayerService.cs,
  IBodyService.cs, BodyService.cs, BodyModule.cs, Constants.cs,
  BuffService.cs, BuffCalculator.cs, EquipmentDataProvider.cs,
  NPCSpawnerService.cs, NPCService.cs, DamageService.cs, CombatService.cs,
  CombatModule.cs, InventoryService.cs, IInventoryService.cs,
  InventoryModule.cs, BeltService.cs, ResourceService.cs, TileModule.cs,
  PlayerTechniqueCaster.cs, NPCAIService.cs, InputAdapter.cs

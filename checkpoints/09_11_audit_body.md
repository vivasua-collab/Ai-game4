# АУДИТ: Body

**Дата:** 2026-09-11 (bodybuff-audit-agent). **HEAD:** `d0b065d`
**Скоуп (READ-ONLY):** Modules/Body — 11 файлов (BodyService 969,
BodyDamageCalculator 105, BodyEnhancementSystem 535, BodyFactory 80, BodyModule
157, BodyModuleServices 61, BodySlotMapping 164, BodyTemplateProvider 309,
IBodyFactory 24, SeveredDebuffSystem 279, SpeciesRegistry 152). Проводка:
Core/Data/BodyPart.cs, BodyTemplate/BodyPartTemplate/SpeciesData, BodyContracts,
Core/DI/Container.cs, Constants.cs (hit-таблицы/реген/HP); потребители —
Combat/DamageService+DamageCalculator+CombatService, NPC/NPCCombatAdapter,
NPC/AnimalService, NPC/NPCAssemblyService, Player/PlayerService,
Combat/CombatConsequencesService, Combat/ElementalEffectService.
Замороженные решения не репортились (кроме мест без нормализации — BOD-10).

---

## Сводка

- Находок: 11 (2×P1, 5×P2, 4×P3) + OK-BY-DESIGN с доказательствами.
- **D8-фикс работает для NPC/животных** (CombatService/NPCCombatAdapter/
  AnimalService — IsEntityAlive + дренаж), но игрок из единого правила выпал (BOD-2).
- Терминология: «GreenHP» в кодовой базе не существует (grep: 0); структурная
  HP = BlackHP, сплит одновременный (DISC-01) — «GreenHP→RedHP конверсии» нет by design.

---

## Находки

### BOD-1 [P1] Hit-таблицы морфологий рассинхронизированы с шаблонами тел

**Файлы:** Core/Data/Constants.cs:633-689 vs BodyTemplateProvider.cs:91-208;
потребление — BodyService.cs:545-563 (ResolveEntityTarget), 425-445 (ResolveTarget).
**Сценарий:** DamageService.cs:181 выбирает часть по
GameConstants.MorphologyHitTables[morphology], но ключи таблиц — гуманоидные
типы, отсутствующие в телах не-гуманоидов; BodyService фолбэк: цель→Torso→vital(Head→Heart)→null.
**Доказательство:**
- Quadruped-таблица (Constants.cs:633-646) содержит LeftArm/RightArm/
  LeftHand/RightHand/LeftLeg/RightLeg = 460/1000 весов — а Quadruped-тело
  (BodyTemplateProvider.cs:93-103) = FrontLeftLeg…BackRightLeg/Tail. Итог:
  46% попаданий по волку фолбэком в Torso (с родными 35% — ~81% в торс);
  ноги зверя невозможно повредить прямым попаданием (нет ампутаций ног/дебаффов).
- Serpentine (Constants.cs:661-667): Tail 500/1000, в теле только
  SerpentineTail (BodyTemplateProvider.cs:150) → 50% попаданий в торс.
- Arthropod (Constants.cs:670-680): Torso 400/1000 + Head 80/1000, в теле
  Cephalothorax/Abdomen → 40% попаданий фолбэчатся до vital-цепочки и бьют
  СЕРДЦЕ (24 RedHP, vital) — паук умирает аномально быстро.
- Amorphous (Constants.cs:683-688): Torso 500/1000, в теле Core/Essence →
  vital-цепочка пуста → ResolveEntityTarget=null → урон молча теряется (BodyService.cs:506).
**Фикс:** привести ключи таблиц к фактическим BodyPartType морфологий (или
alias-трансляция перед публикацией DamageAppliedEvent); суммы промилле = 1000.

### BOD-2 [P1] Единое правило смерти не покрывает игрока: Head не убивает, IsAlive всегда true

**Файлы:** Player/PlayerService.cs:56-67, 176-188; Combat/CombatService.cs:850-853.
**Сценарий:** постепенное разрушение головы игрока (RedHP→0 / Severed) не
убивает. CombatService для игрока проверяет только result.IsFatal (одиночный
удар ≥50 в Head/Heart), тело не смотрит (`!isPlayerTarget`). BodyService
публикует BodyCriticalEvent для обеих vital-частей (BodyService.cs:269-278),
но PlayerService.OnBodyCritical фильтрует только `Heart && Disabled`.
**Доказательство:** PlayerService.IsAlive (стр. 63) = `!IsPartSevered(Heart)` —
сердце никогда не Severed (BodyPart.cs:80-84, MaxBlackHP=0) → телесная ветка
IsAlive ВСЕГДА true: после Die() игрок остаётся «жив» (Tick продолжает идти).
**Фикс:** OnBodyCritical — Head|Heart × Disabled|Severed; IsAlive — по
BodyDamageCalculator.IsAlive(GetAllParts()).

### BOD-3 [P2] BodyEnhancementSystem: усиления негуманоидных видов не работают

**Файлы:** BodyEnhancementSystem.cs:277-279 (фильтр), 392-487 (таблица);
NPCAssemblyService.cs:135.
**Доказательство:** GetEnhancementBonuses(enhancements, BodyPartType.All) —
фильтр `TargetPart==All || TargetPart==partType` при partType=All пропускает
только All-таргетированные усиления → волк/тигр/паук/призрак не получают ни
одного бонуса (NaturalWeapon/NaturalArmor/QiInfusion). Дополнительно части-цели
(LeftArm/Torso/Head) отсутствуют в морфологиях этих видов → ApplyBodyHardening
(355-377) тоже пуст. Работают только All-усиления: dragon/demon/golem.
**Фикс:** ретаргетировать таблицу на реальные части + агрегация без части-фильтра.

### BOD-4 [P2] SeveredDebuffSystem: параметры дебаффа теряются при передаче в BuffService

**Файлы:** SeveredDebuffSystem.cs:225; Buff/BuffService.cs:519-527, 420-429.
**Доказательство:** DebuffTable задаёт (AttackReduction, Strength, −0.15), но
ApplyBuff принимает только (buffId, duration, potency). ID «severed_*» не
распознан эвристикой → default: AttackBoost, Damage, +10%, IsDebuff=false →
итог «Damage −1.5% на 30 тиков» вместо «STR −15% постоянно» (и это обнуляется
BUF-1 из аудита Buff). Двойного применения нет (Severed-событие only-on-transition,
BodyService.cs:258-265, 522-527). **Фикс:** BuffData-описатель или «severed_»-ветка маппинга.

### BOD-5 [P2] Пассивная регенерация не восстанавливает BlackHP; NPC не регенерируют

**Файлы:** BodyService.cs:405-415 (`Heal(healAmount, 0)` — только RedHP), 359-416.
**Доказательство:** BODY_SYSTEM.md §9 требует structural-first; код лечит только
функциональную (BD-05). BlackHP восстанавливается лишь направленным лечением —
риск ампутации не «зарастает» отдыхом. ProcessRegeneration итерирует только
_parts игрока — _entityBodyParts (NPC/звери) регенерации не имеют: раны NPC
необратимы. **Фикс:** синхронизировать доку или добавить black-реген; NPC-реген — отдельным решением.

### BOD-6 [P2] Ампутации не переживают Save/Load; приживление недоступно

**Файлы:** BodyService.cs:629-681 (RestoreState), 744-765 (ReattachPart).
**Доказательство:** RestoreState восстанавливает Severed корректно (SetHP→UpdateState),
но НЕ публикует BodyPartSeveredEvent → SeveredDebuffSystem._activeDebuffs после
load пуст: дебаффы ампутации не восстанавливаются; ReattachPart после load не
найдёт ключ. Сам ReattachPart вызовов не имеет (grep) — приживление (дока §10) мертво.
**Фикс:** републикация Severed-событий при RestoreState (или сериализация трекинга).

### BOD-7 [P2] CultivationBonus HP не реализован (doc-drift, влияет на поведение)

**Файлы:** BODY_SYSTEM.md §7.1 vs BodyFactory.cs:33-62, NPCAssemblyService.cs:94-95.
**Доказательство:** док: effectiveHP = baseHP × hpMult × sizeMult ×
cultivationBonus (1+(L−1)×0.1); код: vitality×size только — L5-волк имеет те
же HP, что L1 (компенсация усилениями тоже не работает — BOD-3). **Фикс:** реализовать или убрать из доки.

### BOD-8 [P3] RecalculateHPFromVitality — мёртвый код

BodyModule.cs:147-156; `new StatChangedEvent` не публикуется никем (только
контракт StatContracts.cs:21) — П.24-пересчёт недостижим. Фильтр EntityId строгий.

### BOD-9 [P3] HealPart: малое лечение целиком уходит в BlackHP

BodyService.cs:296-306: redHeal=(int)(amount×0.7) — amount=1 → 0 red/1 black.
Асимметрия с P1-03 (урон гарантирует red≥1, лечение — нет). Сердце — исключение (BD-24).

### BOD-10 [P3] IBodyDataProvider: player-ветка строгим == вместо PlayerIdResolver

BodyService.cs:821/842/874/907. Вызов с «player_0» → «сущность не найдена»
(IsEntityAlive=false). Сейчас безопасно: CombatService проверяет `!isPlayerTarget`
до HasEntity; NPCCombatAdapter/AnimalService вызывают только для NPC. Латентная мина. [UNVERIFIED-impact: живых вызовов с player-алиасом нет — grep]

### BOD-11 [P3] SpeciesRegistry vs докa §2.4

SpeciesRegistry.cs:43-99 — 13 видов; дока — «11» + зверолюд/медведь/скорпион/
многоножка/элементаль/кентавр (нет в коде). Wolf STR 8/AGI 14/VIT 10 — совпадает (60-62).

---

## Проверено чисто

- **D8 для NPC/зверей:** CombatService.cs:850-853, NPCCombatAdapter.cs:247-249,
  AnimalService.cs:522-524 — IsEntityAlive + дренаж; «суммарного HP≤0»-домена не осталось.
- **Урон в мёртвую часть теряется — OK-BY-DESIGN:** BodyPart.cs:108-128
  (Math.Max(0,…) на обоих HP; Severed → TakeDamage=false) — на этом floor держится D8.
- **SplitDamage 70/30, redDmg≥1** (BodyDamageCalculator.cs:31-41); Green→Red
  конверсии нет и не нужно (DISC-01, BodyPart.cs:114-124).
- **DI-одиночность BodyService** под 3 интерфейсами (BodyModuleServices.cs:31-33)
  — Container.cs:208-234 кэширует по ImplementationType → один инстанс.
- **Регенерация:** дробный аккумулятор (BodyService.cs:394-403), L10 instant
  (379-392), IsAlive-guard не лечит труп (373), split dirty-флагов P0-01(V3).
- **Состояния:** UpdateState (BodyPart.cs:242-271) — Severed при BlackHP≤0 И
  MaxBlackHP>0 (сердце не ампутируется), Disabled при RedHP≤0; события
  only-on-transition; BodyCriticalEvent на обе vital-части.
- **BD-23:** отсутствующие части = severed (BodyService.cs:149-155, 669-680).
- **Save/Load тела игрока:** BodySaveData + консистентность морфологии, SetHP-клампы,
  Severed восстанавливается через UpdateState (BD-22).
- **SetMaxHP** сохраняет damage_ratio (BodyPart.cs:180-202).
- **BodySlotMapping:** прямой/обратный маппинг, ленивый кэш (BD-02/BD-40/BD-25/P2-03).
- **Seed-детерминизм:** NPCAssemblyService.Assemble(seed)→SeededRandom;
  AnimalService.SpawnForLocation(seed+offset) — воспроизводимо.
- **ResolveTarget:** детерминированный fallback (BD-30, static VitalPriority).
- **DoT-проводка Body↔Combat:** BodyModule.cs:87-130 — единый DamageAppliedEvent,
  per-part урон, смерть NPC от DoT детектируется NPCCombatAdapter.

---

## Соответствие docs_v2 (BODY_SYSTEM.md)

| Пункт | Код | Статус |
|---|---|---|
| §5.2-5.3 Red/Black=×2, split 70/30 одновременно | BodyPart/BodyDamageCalculator | ✅ |
| §5.4 состояния Damaged/Crippled | Bruised/Wounded/Disabled | ⚠ терминология |
| §6 шаблоны HP | BodyTemplateProvider | ✅ цифры совпадают |
| §7.1 cultivationBonus | отсутствует | ❌ BOD-7 (P2) |
| §8 кровотечение 5 уровней | bleed-бафф сломан (BUF-2, чекпоинт Buff) | ❌ |
| §9 реген structural→functional | только RedHP | ❌ BOD-5 (P2) |
| §10 приживление | API есть, вызовов нет, load не восстанавливает | ❌ BOD-6 (P2) |
| §2.4 реестр видов | 13 видов, состав отличается | ⚠ BOD-11 (P3) |
| §11 INT не зависит от тела | guard в SeveredDebuffSystem | ✅ (механика — BOD-4) |
| §14 BodyPartData.bleeding | не реализовано | ⚠ P3 |

**Следующий шаг:** BOD-1 (таблицы попаданий) + BOD-2 (Head-смерть игрока);
BOD-3/BOD-4 — одним пакетом с BUFF-фиксами (BUF-1/2/5).

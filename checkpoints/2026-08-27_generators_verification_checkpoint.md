# ЧЕКПОИНТ сессии 2026-08-27 — Верификация генераторов + читы + пред-генерация

**Дата:** 2026-08-27 (MSK, UTC+3)
**Сессия:** основная, режим без субагентов
**HEAD на старте:** `41201b7`
**План-компаньон:** `checkpoints/plans/2026-08-27_generators_verification_plan.md`

---

## Контекст (состояние на старте — собрано в Phase A)

- TechniqueGeneratorService.cs (641 строка) — детерминированный генератор
  по (cultivationLevel, role, seed). Параметры:
  - capacity = baseCapacity(type) × 2^(level-1) × (1 + mastery×0.005)
  - qiCost  = floor(baseCapacity × 2^(level-1)) — ВСЕГДА ×1.0 по Grade
  - baseDamage = capacity × gradeMultiplier
  - cooldown/range/castTime — статические таблицы по type/subtype
  - ultimate (5% для Transcendent): damage×2.0, qiCost×2.0
- EquipmentGenerator.cs (340 строк) — «Матрёшка»:
  базовый класс × материал × грейд × зачарование (опц.)
  - Damage = (base + perLevel×(L-1)) × speedScale × gradeEff × (1+mat/100)
  - Defense = (base + perLevel×(L-1)) × gradeEff × (1+mat/100)
  - Durability = MaterialDurabilityByTier[tier] × GradeDurabilityMult
  - StatBonuses: 0..6 по грейду, значения 2-5 × BonusPowerMult
- FormationGeneratorService.cs (251 строка) — детерминированный,
  type×size×shape×element×level. Heavy только с L6+.
- TechniqueRegistry / FormationRegistry — словари id→data, перезапись по id.
- CheatPanel.cs (313 строк, #if DEBUG) — F1, кнопки: L1-L9, Ци, прорыв,
  техники (3 рандом), камни Ци, формация Сбора, утечка ×10.
- FormationService.cs (685 строк) — lifecycle Drawing→Filling→Active→Depleted.
  Auto-deactivate при истощении + вариация A (одноразовая, без ядра).
- Constants.cs: MAX_CULTIVATION_LEVEL=10, FORMATION_BASE_CONTOUR_QI=80,
  HEAVY_FORMATION_MIN_LEVEL=6, BaseCapacityByType, GradeMultipliers.
- GeneratorTables: TechniqueGradeWeights {60,30,9,1},
  EquipmentGradeWeightsByLevel[6][5] (L1..L9+).
- EquipmentGenerationTables: Weapons(7), Armors(6), Materials(T1-T5,13 шт),
  Enchants(5), GradeProfiles (efficiencyMult, durabilityMult, bonusCount).

---

## Phase A — Аудит (только чтение)

- [x] A1. Прочитать TechniqueGeneratorService.cs (642 строки)
- [x] A2. Прочитать EquipmentGenerator.cs (340 строк)
- [x] A3. Прочитать FormationGeneratorService.cs (251 строка)
- [x] A4. Прочитать CheatPanel.cs (313 строк)
- [x] A5. Прочитать FormationService.cs (685 строк, lifecycle Drawing→Active→Depleted)
- [x] A6. Прочитать TechniqueRegistry, FormationRegistry (перерегистрация=замена по id)
- [x] A7. Прочитать GeneratorTables + EquipmentGenerationTables + Constants (key sections)
- [x] A8. Прочитать TechniqueGrantPhase, GeneratorModule, GeneratorModuleServices

### Найденные проблемы (фиксируем для Phase B-H)

**P1-NO-BOUNDS:** Ни генератор техник, ни генератор экипировки НЕ проверяют,
  что итоговый стат попал в границы уровня. capacity/damage могут
  скакать из-за mastery (0..100 → 1.0×..1.5×). Нужно ввести min/max таблицы.

**P2-NO-DEDUP:** TechniqueRegistry.Register ПЕРЕЗАПИСЫВАЕТ по id (т.е. дубль
  по id с другим контентом тихо заменяет). НО: одинаковые по статам, но
  разные по id (т.к. seed-hash в id) → остаются оба. Нужен детектор по статам.

**P3-NO-PREGEN:** Пред-генерации техник при создании мира нет. Все техники
  генерируются on-demand (TechniqueGrantPhase, CombatService). Это значит,
  NPC могут получать разные техники с одним и тем же уровнем/типом без
  возможности дедупликации на уровне мира.

**P4-NO-CHEAT-EQUIP:** В CheatPanel нет кнопок для генерации экипировки
  (оружие/броня по типу, по грейду, по материалу) и расходников.
  Только техники, Ци, формации, камни.

**P5-NO-CHEAT-FORMATION-VARIETY:** В CheatPanel есть только формация Сбора.
  Нужно: выбор типа (Barrier/Trap/Amplification/Suppression/Gathering/
  Detection/Teleportation/Summoning), размера (Small..Heavy), уровня.

**P6-FORMATION-RENDER:** Проверить визуализацию (FormationVisualizer?) —
  в headless sim-режиме работает, но визуальной проверки нет. Если есть
  FormationVisualizer, убедиться что он подключается к Active-формации.

**P7-LEGENDARY-IMPL:** ItemRarity.Legendary существует, ноEquipmentGenerator
  маппит Grade → Rarity (Transcendent=Mythic, Perfect=Epic). Legendary
  сейчас НЕ выдаётся. Для Legendary нужно разрешить «заход на +1 уровень»
  по некоторым характеристикам.

---

## Phase B — Границы уровней + разброс характеристик (LevelBoundaries)

- [x] B1. Создать `Core/Data/LevelBoundaries.cs`:
  - TechniqueBounds(level, type, grade) → {minCapacity, maxCapacity,
    minQiCost, maxQiCost, minDamage, maxDamage}
  - EquipmentBounds(level, weaponClass, slot, grade) → {minDamage, maxDamage,
    minDefense, maxDefense, minDurability, maxDurability, minCoverage,
    maxCoverage, minWeight, maxWeight}
  - FormationBounds(level, size) → {minContourQi, maxContourQi,
    minPoolCapacity, maxPoolCapacity}
- [x] B2. Формулы разброса (РЕШЕНО):
  - mastery 0..100 → capacity 1.0×..1.5× (factor = 1 + mastery×0.005) —
    УЖЕ существует в TechniqueGeneratorService; используется как базовый
    разброс для capacity. Min = при mastery=0, max = при mastery=100.
  - damage: capacity × gradeMult (0..100% mastery дает разброс 1.0×..1.5×
    для capacity → переносится в damage).
  - qiCost: floor(baseCapacity × 2^(L-1)) — НЕ зависит от mastery, диапазон
    узкий (±0%). Min=max.
  - coverage: min..max из ArmorBaseClass (рандом в генераторе).
  - weight: weaponClass.WeightKg × material.WeightMult (диапазон по
    материалам того же тира).
- [x] B3. Правило легендарных (РЕШЕНО):
  - Для техник: TechniqueGrade.Transcendent → «макс граница = Bound(L+1)»
    по ВСЕМ характеристикам (аналог Mythic). Perfect — по 2 статам (damage
    и qiCost, т.к. они критичны). Common/Refined — строго L.
  - Для экипировки: ItemRarity.Legendary → +1 уровень на 2 характеристиках
    (Damage и Durability). ItemRarity.Mythic → +1 на ВСЕХ.
  - Т.к. EquipmentGenerator маппит Grade→Rarity (Transcendent=Mythic,
    Perfect=Epic), Legendary сейчас НЕ выдаётся. Решение: ввести новый
    метод `GenerateLegendary` в EquipmentGenerator, который принудительно
    ставит Rarity=Legendary + Transcendent grade + часть статов на L+1.
- [x] B4. Юнит-дамп в GeneratorModule (RunGeneratorDebugDump): печатать
  границы для L1/L3/L5/L7/L9 по Combat-Common технике и sword-Common оружию.
  Сравнить с фактически сгенерированными значениями.

---

## Phase C — VerificationService

- [ ] C1. Создать `Core/Interfaces/IVerificationService.cs`:
  - ValidationResult Validate(TechniqueData tech, int cultivationLevel)
  - ValidationResult Validate(EquipmentData item, int cultivationLevel)
  - ValidationResult Validate(FormationData form, int cultivationLevel)
- [ ] C2. Создать `Modules/Generator/VerificationService.cs` — реализация:
  - Сравнивает каждый стат с LevelBoundaries.
  - Учитывает Rarity для +1 правила (Legendary/Mythic).
  - Возвращает ValidationResult { IsValid, OutOfBoundsFields[], Severity }
- [ ] C3. Регистрация в DI через GeneratorModuleServices (Singleton).
- [ ] C4. VerificationService.FilterAndRegisterTechniques(IEnumerable):
  генерирует пачку → валидирует каждую → регистрирует только валидные в
  TechniqueRegistry (или FormationRegistry для формаций).

---

## Phase D — DeduplicationService

- [ ] D1. Создать `Modules/Generator/DeduplicationService.cs`:
  - Fingerprint Technique: (Type, Subtype, Element, Grade, Level, capacity,
    qiCost, baseDamage, cooldown, range, castTime) → string.
  - Fingerprint Equipment: (Slot, Subtype, MaterialId, Grade, Level, Damage,
    Defense, Coverage, Durability) → string.
  - Fingerprint Formation: (Type, Size, Shape, Element, Level) → string.
- [ ] D2. Метод `Deduplicate(IEnumerable<TechniqueData>)`: оставить только
  уникальные по fingerprint, отбросить дубли (сохранять ПЕРВЫЙ).
- [ ] D3. Метод `Clean(TechniqueRegistry)`: пройтись по реестру, удалить
  дубли (оставить первого). Аналог для FormationRegistry.
- [ ] D4. Регистрация в DI.

---

## Phase E — Pred-генерация техник при создании мира

- [ ] E1. Создать `Entry/Phases/PreGenTechniquePhase.cs` (PhaseOrder ~46,
  после TechniqueGrantPhase):
  - Печёт N техник на каждый уровень 1..cultivationLevel по всем типам
    (Combat/Defense/Support/Healing/Movement/Sensory/Curse/Poison/Cultivation/
    Formation).
  - N = 3-5 на (тип × уровень × грейд).
  - Seed = sessionSeed + level*1000 + typeIndex*100 + gradeIndex*10.
  - Каждая генерация идёт через VerificationService → отбраковка.
  - Дедупликация по fingerprint.
  - Регистрация валидных уникальных в TechniqueRegistry.
- [ ] E2. Логирование: «[PreGen] level=L type=T grade=G generated=X valid=Y
  duplicates=Z registered=W».
- [ ] E3. Регистрация фазы в GameBoot/WorldInit sequence (где фазы
  регистрируются в DI).

---

## Phase F — Расширение CheatPanel

- [ ] F1. Добавить секцию «Экипировка»:
  - Generate Weapon (subtype cycle: dagger/sword/axe/spear/greatsword/bow/staff)
  - Generate Armor (subtype cycle: head/torso/arms/legs/feet/belt)
  - Generate Random Equipment
  - Spawn в инвентарь игрока.
- [ ] F2. Добавить секцию «Расходники»:
  - Generate Consumable (тип: healing/qi/cure)
  - Generate Charger (слот пояса с зарядом Ци)
- [ ] F3. Добавить секцию «Техника с формацией»:
  - Generate Technique Formation (combat-техника, которая создаёт формацию)
  - Spawn техники + автоматически старт формации в позиции игрока.
- [ ] F4. Добавить секцию «Формация (произвольная)»:
  - Cycle типа: Barrier/Trap/Amplification/Suppression/Gathering/Detection/
    Teleportation/Summoning
  - Cycle размера: Small/Medium/Large/Great/Heavy
  - Cycle уровня: L1..L9
  - Spawn в позиции игрока.
- [ ] F5. Добавить секцию «Верификация»:
  - Dump LevelBoundaries для текущего уровня (тост + в лог).
  - Dump количества дублей в TechniqueRegistry (по fingerprint).
- [ ] F6. Все новые кнопки — в #if DEBUG, проверка build.

---

## Phase G — Документация

- [ ] G1. Создать `docs/docs_v2/07_ui/CHEAT_PANEL.md` — спецификация
  чит-меню: какие кнопки есть, какие сервисы они вызывают, как добавить
  новую кнопку (паттерн).
- [ ] G2. Создать `docs/docs_v2/02_systems/LEVEL_BOUNDARIES.md` —
  таблицы границ уровней: формулы min/max, правило Legendary/Mythic +1.
- [ ] G3. Создать `docs/docs_v2/02_systems/VERIFICATION_SYSTEM.md` —
  VerificationService: API, как вызывать из генераторов, что возвращает,
  примеры валидных/невалидных техник.
- [ ] G4. Создать `docs/docs_v2/02_systems/PRE_GENERATION.md` —
  pred-generation pipeline: PreGenTechniquePhase, как печётся набор на
  уровень мира, как отбраковываются, как дедуплицируются.
- [ ] G5. Обновить `docs/docs_v2/README.md` — ссылки на новые доки.

---

## Phase H — Сборка + тесты + git

- [ ] H1. `dotnet build` — 0 errors (warnings pre-existing OK).
- [ ] H2. `GODOT_NEWGAME=1` — проверить, что PreGenTechniquePhase отработал.
- [ ] H3. `GODOT_GEN_DEBUG=1` — проверить debug dump + LevelBoundaries print.
- [ ] H4. Agent Browser — открыть / (порт 3000, Next.js wrapper), убедиться,
  что страница рендерится без ошибок.
- [ ] H5. Git commit всех изменений.
- [ ] H6. Push в origin/main.

---

## Журнал принятых решений (append-only)

| Время MSK | Этап | Решение |
|-----------|------|---------|
| — | A | Архитектура подтверждена: VerificationService живёт в Modules.Generator, читает Core.Data.LevelBoundaries (нет циклической зависимости). |
| — | B | Используем mastery 0..100 как УЖЕ существующий разброс (1.0×..1.5× для capacity). Не вводим дополнительный рандом внутри генератора, чтобы не ломать детерминизм. Дополнительный разброс damage ±15% — опциональный, вводится через SeededRandom. |
| — | C | Rarity берётся из EquipmentData.Rarity (там уже Legendary/Mythic есть). Для техник — вводим TechniqueGrade.Transcendent → аналог Mythic. |
| — | D | Fingerprint — tuple всех статов, кроме NameRu/NameEn/Description/TechniqueId. Hash в string для быстрого сравнения через Dictionary. |
| — | E | PreGenTechniquePhase — один новый Phase, ставится после TechniqueGrant (PhaseOrder=46). Seed = sessionSeed + level*1000 + typeIdx*100 + gradeIdx*10. |

---

## Журнал прогресса (append-only, обновлять после каждого этапа)

| Время MSK | Этап | Статус | Commit |
|-----------|------|--------|--------|
| 09:30 | A | ✅ завершён (аудит чтения) | — |
| 09:55 | B | ✅ завершён (LevelBoundaries.cs, 415 строк) | pending |
| — | C | pending | — |
| — | D | pending | — |
| — | E | pending | — |
| — | F | pending | — |
| — | G | pending | — |
| — | H | pending | — |

---

*Чекпоинт создан в основном потоке. После каждого закрытого подэтапа — `[x]` + git commit.*

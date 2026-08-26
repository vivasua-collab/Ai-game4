# ЧЕКПОИНТ сессии 2026-08-26 — Epic→Legendary оверкап + аудиты

**Дата:** 2026-08-26 (СР), старт 12:49 MSK
**Сессия:** основная, без субагентов
**HEAD на старте:** `31d679b` ( synced с origin/main, push 5 коммитов выполнен )
**План-компаньон:** `checkpoints/plans/2026-08-26_epic_legendary_overcap_and_audit_plan.md`
**Чекпоинты аудитов:** `2026-08-26_audit_pass1_architecture.md` (и др. по мере прохождения)

---

## Контекст (состояние на старте)

- Прошлая сессия (чекпоинт `2026-08-27_generators_verification_checkpoint.md`)
  завершила: LevelBoundaries.cs, VerificationService, DeduplicationService,
  PreGenTechniquePhase, расширение CheatPanel (экипировка/расходники/формации),
  docs_v2 (CHEAT_PANEL, LEVEL_BOUNDARIES, VERIFICATION_SYSTEM, PRE_GENERATION).
- **Пробел, обнаруженный при чтении кода:** `GenerateLegendary` из плана B3
  прошлой сессии НЕ был реализован. `EquipmentGenerator.GradeToRarity`:
  Transcendent→Epic. `ItemRarity.Legendary` не выдаётся НИКОГДА.
  Правила оверкапа в LevelBoundaries (Legendary→DamageAndQi, Mythic→All)
  существуют, но ни один предмет их не активирует.
- Веса грейдов экипировки (GeneratorTables.EquipmentGradeWeightsByLevel):
  Transcendent только на L7-8 (5%) и L9+ (20%).
- `WithOvershootApplied` расширяет max до границ **Common-грейда L+1**
  (`WeaponBoundsFor(L+1, wclass, EquipmentGrade.Common, ...)`) — для
  Transcendent-предмета с формулами L+1 этого НЕДОСТАТОЧНО
  (eff 2.0 vs 1.0): оверкапнутая легендарка выйдет за расширенные границы.
  Требуется фикс: next-границы должны считаться для ТОГО ЖЕ грейда.

---

## Phase A — Аудит чтения (подготовка фичи) ✅

- [x] A1. Прочитать EquipmentGenerator.cs (341 строка): «Матрёшка»,
  GradeToRarity (Transcendent→Epic, Legendary отсутствует), RollGradeForLevel,
  RollStatBonuses (2-5 бонусов × BonusPowerMult), TryApplyEnchant (§8).
- [x] A2. Прочитать LevelBoundaries.cs (416 строк): OvershootPolicy
  {None, DamageAndQi, All}; техники: Transcendent→All, Perfect→DamageAndQi;
  экипировка: Mythic→All, Legendary→DamageAndQi; WithOvershootApplied
  берёт next-границы для Common-грейда (нашёл изъян, см. контекст).
- [x] A3. Прочитать VerificationService.cs (256 строк): Validate(EquipmentData)
  ищет базовый класс по ItemType/NameEn, применяет WithOvershootApplied,
  проверяет Damage/Defense + MaxDurability + Weight (+Coverage для брони).
  Ultimate-множители для техник (×2). FilterValid — для батчей.
- [x] A4. Прочитать GeneratorTables.cs: TechniqueGradeWeights {60,30,9,1},
  EquipmentGradeWeightsByLevel[6][5].
- [x] A5. Прочитать CheatPanel.cs (518 строк): секции F1-F5 (экипировка,
  расходники, техника+формация, cycle-формация, верификация), паттерн кнопок.
- [x] A6. Прочитать GeneratorModule.cs: RunGeneratorDebugDump
  (GODOT_GEN_DEBUG=1) — секции Items/Матрёшка/Techniques/Loot/DB stats.
- [x] A7. Проверить headless-команды (SESSION_CONTEXT.md): godot-бинарь
  /home/z/godot/Godot_v4.7.1-stable_mono_linux.x86_64, таймауты.

---

## Phase B — Дизайн Epic→Legendary с шансом оверкапа ✅

**Требование пользователя:** процент шанса оверкапа, чтобы не каждая
легендарка улетала на новый ранг, а например 10–25%; логику продумать.

### Принятый дизайн (решения)

- [x] B1. **Двухступенчатая схема роллов** (детерминированные, из общего
  SeededRandom):
  1. **Промоушен Epic→Legendary:** при ролле grade=Transcendent бросается
     `EPIC_TO_LEGENDARY_PROMOTE_CHANCE = 0.20f` (20% эпиков → легендарки).
     Итоговые доли: L7-8 → 5%×20% = 1.0% легендарок; L9+ → 20%×20% = 4.0%
     (согласуется с комментарием ItemRarity: Legendary ~1%).
     Не прошёл → обычный Epic (как сейчас).
  2. **Оверкап легендарки:** `LEGENDARY_OVERCAP_CHANCE = 0.18f` (18% —
     середина диапазона 10–25% по требованию). Успех → часть характеристик
     считается по формулам L+1 («улетела на новый ранг»); неудача → статы
     в границах L, легендарность выражается ДРУГИМИ перками.
  Обе константы — в GameConstants, настраиваются одной строкой.

- [x] B2. **Что получает легендарка ВСЕГДА** (редкость ≠ оверкап):
  - Rarity = Legendary, суффикс имени «(легендар.)».
  - Гарантированное зачарование (TryApplyEnchant из eligible по MinGrade).
  - Максимум стат-бонусов грейда (BonusCountMax) с максимальной силой
    (5 вместо ролла 2..5).
  - Value × LEGENDARY_VALUE_MULTIPLIER (3.0).

- [x] B3. **Что даёт оверкап (только 18% легендарок)**:
  - Оружие: Damage по формуле уровня L+1 (base+perLevel×L, тот же материал,
    тот же speedScale и eff грейда).
  - Броня: Defense по формуле L+1 (аналогично).
  - Оба: MaxDurability = MaterialDurabilityByTier[tier(L+1)] × DurabilityMult.
  - Пометка в Description: «характеристики выходят на уровень L+1».
  - RequiredCultivationLevel остаётся L (носится на своём уровне — это и
    есть суть «захода по характеристикам на +1 уровень»).
  - Weight/Coverage НЕ оверкапятся (политика DamageAndQi = только
    Damage/Defense + Durability — «только в некоторых характеристиках»).

- [x] B4. **Фикс WithOvershootApplied (LevelBoundaries):** next-границы
  считать для ТОГО ЖЕ grade (не Common), иначе Transcendent-легендарка с
  формулами L+1 не впишется в окно верификации. Аналогично для техник
  (next = TechniqueBoundsFor(L+1, type, ТЕКУЩИЙ grade)) — консистентность
  семантики «+1 уровень при том же грейде».

- [x] B5. **Верификация совместима без изменений:** легендарка без оверкапа
  вписывается в базовые границы L (валидна); с оверкапом — в расширенные
  (после фикса B4). Разрешающее окно [min_L .. max_{L+1,grade}] покрывает
  оба случая; правило 10–25% — ГЕНЕРАЦИОННОЕ, а не валидационное.

- [x] B6. **API:** IEquipmentGenerator + EquipmentGenerator:
  - `GenerateLegendaryWeapon(level, subtype?, seed, forceOvercap?)`
  - `GenerateLegendaryArmor(level, subtype?, seed, forceOvercap?)`
  - forceOvercap: null → ролл по шансу; true/false → детерминированно
    (для чит-теста обеих веток).
  - GenerateWeapon/GenerateArmor получают встроенный промоушен
    (ролл после RollGradeForLevel → легендарный путь внутри).

- [x] B7. **Edge cases:**
  - L=MAX_CULTIVATION_LEVEL (10): оверкап упирается в потолок — формулы
    L+1 = L, легендарка получает только перки (задокументировано).
  - Enchants пуст/ни один не подходит по MinGrade → пропустить зачарование
    молча (не критично).
  - Детерминизм: новые роллы добавляются в ЕДИНУЮ последовательность rng
    (только для Transcendent-предметов) — сид воспроизводим.
  - TryApplyEnchant вызывается с явным seed (не counter) — стабильность
    headless-дампов.

---

## Phase C — Имплементация ✅

- [x] C1. GameConstants: EPIC_TO_LEGENDARY_PROMOTE_CHANCE (0.20f),
  LEGENDARY_OVERCAP_CHANCE (0.18f), LEGENDARY_VALUE_MULTIPLIER (3.0f).
- [x] C2. LevelBoundaries: фикс WithOvershootApplied (technique + weapon +
  armor) — next-границы для текущего grade. Вызовы в VerificationService
  обновлены (tech.Grade / item.Grade).
- [x] C3. EquipmentGenerator: TryPromoteToLegendary + RollLegendaryOvercap +
  DurabilityFor(statLevel) + ApplyLegendaryPerks (гарант. энчант + пометка
  оверкапа) + RollStatBonuses(forceMax) + GradeSuffix(legendary);
  GenerateWeapon/Armor → Core-методы с forceLegendary/forceOvercap.
- [x] C4. IEquipmentGenerator: +GenerateLegendaryWeapon/GenerateLegendaryArmor
  (forceOvercap?: null=ролл / true/false=детерминированно).
- [x] C5. CheatPanel: секция «Легендарки (промо 20% / оверкап 18%)» —
  3 кнопки (оружие/броня/×20 статистика + верификация).
- [x] C6. GeneratorModule.RunGeneratorDebugDump: секция Legendary —
  400 генераций L9 (распределение промоушена) + 40 принудительных
  легендарок с верификацией + семплы с/без оверкапа (один сид).
- [x] C7. dotnet build — 0 errors (271 warnings, прежний уровень).
- [x] C8. Headless GODOT_GEN_DEBUG=1 — PASS:
  Epic=16.8% (ожид. ~16%), Legendary=4.0% (ожид. ~4%),
  оверкап 2/16 (12.5%, биномиальный шум при n=16, ожид. ~18%),
  верификация 40/40, семплы: dmg 115 (оверкап) vs 104 (без).

### Баги, найденные и исправленные в Phase C (существовали ДО фичи)

- [x] **BUG-1 (VerificationService):** поиск базового класса по подстроке
  NameEn.Contains(w.Id) — «sword» ложно матчится в «greatsword»
  (greatsword-предметы валидировались по границам sword → ложные
  Damage out of bounds). Фикс: точное вхождение «_id_».
- [x] **BUG-2 (EquipmentGenerator.PickMaterial):** для оружия категории
  Metal/Bone/Crystal/Wood — на tier 5 НИ ОДНОГО материала
  (единственный T5 void_matter = категория Void) → fallback
  Materials[0] = ЖЕЛЕЗО (T1): всё оружие L9 генерировалось из железа!
  Фикс: +Void категория для оружия (согласовано с WeaponBoundsFor,
  оба фильтка категорий в LevelBoundaries тоже).
- [x] Подписи ожиданий в дампе исправлены: Epic ~16% / Legendary ~4%
  (было ошибочно ~32%/~8% — двойной учёт веса Transcendent).

---

## Phase D — Документация + git ✅

- [x] D1. docs_v2/02_systems/LEVEL_BOUNDARIES.md: раздел «Epic→Legendary
  промоушен и оверкап» (схема, константы, доли, перки, формулы L+1,
  верификация, API, детерминизм, замер шансов).
- [x] D2. docs_v2/07_ui/CHEAT_PANEL.md: секция «Легендарки» (3 кнопки).
- [x] D3. Коммит + push: `f0d11a6`.
- [x] D4 (доп.). BUGFIX-3: TechniqueGeneratorService.BaseDamage — MathF.Round
  вместо trunc (были отбои верификатора «BaseDamage 72 out of [73..109]»
  на дробных гранях при некоторых сидах;_capacity/qiCost уже были
  консистентны). После фикса: valid=100/100.

---

## Phase E — Аудит проход 1: Архитектура ✅

(отдельный файл `2026-08-26_audit_pass1_architecture.md`)
- [x] E1. Прочитать/аудит Core: DI-контейнер (292 стр, OK), EventBus (239,
  OK + минор ThreadStatic), interfaces (нарушение A-2 найдено), messaging.
- [x] E2. Прочитать/аудит Entry: 16 фаз (порядки — баг A-1), boot
  (GameEntryPoint OK), SceneOrchestrator (нестабильный Sort),
  GameLifetimeScope (OK), SceneAssemblyRegistrar.
- [x] E3. Adapter-граница: Core без Godot-ссылок ✓ (grep).
- [x] E4. Фиксы: перенумерация фаз 1–14 (Finalize последняя), дубль
  SceneReadyEvent удалён, стабильный OrderBy, FormationData → Core.Data
  (рецидив Core→Modules устранён, 10 файлов using).
- [x] E5. Коммит + push: `b8ddda1`.

## Phase F — Аудит проход 2: + Модуль мира (WorldGen/NPC) ✅

(отдельный файл `2026-08-26_audit_pass2_worldgen.md`)
- [x] F1. Аудит World/Tile (полностью, 988 строк) + NPC-спавн-контур
  (3 фазы + AnimalService/NPCSpawnerService структура).
- [x] F2. Фиксы: травы chance 1→100 (двойной ролл 0.01%!),
  ResourceHarvestedEvent с исходным ResourceId, сброс
  _placedGroupCentres между сборками, удалён dead NPCSpawnPhase.
- [x] F3. Коммит + push: `e7f2008`.

## Phase G — Аудит проход 3: + Боевой контур (Combat/Qi/Formation/Body/Trade)

(отдельный файл `2026-08-26_audit_pass3_combat_qi.md`)
- [ ] G1. Аудит Combat, Qi, TechniqueService, Formation, Body, Trade.
- [ ] G2. Фиксация проблем + критичные фиксы.
- [ ] G3. Коммит.

## Phase H — Финализация

- [ ] H1. Прогон всех headless-тестов (NEWGAME, COMBAT_SIM, TRADE_DEBUG,
  GEN_DEBUG) — регрессий нет.
- [ ] H2. SESSION_CONTEXT.md — обновить сводку сессии.
- [ ] H3. Финальный push.

---

## Журнал принятых решений (append-only)

| Время MSK | Этап | Решение |
|-----------|------|---------|
| 12:55 | A | Подтверждено: Legendary не генерировался вовсе; правила оверкапа LevelBoundaries мертвы без источника Legendary-предметов. |
| 12:56 | B1 | Двухступенчатая схема: промоушен 20% (Epic→Legendary) + оверкап 18% (в диапазоне 10-25% по ТЗ). Обе константы в GameConstants. |
| 12:56 | B2 | Легендарка БЕЗ оверкапа не пустышка: гарантированный энчант + макс бонусы + value×3. Оверкап — только Damage/Defense+Durability («только некоторые характеристики»). |
| 12:56 | B4 | Фикс WithOvershootApplied: next-границы для ТЕКУЩЕГО грейда (иначе Transcendent L+1 формулы вне окна верификации). |
| 12:57 | B6 | API: GenerateLegendaryWeapon/Armor с forceOvercap? (null=ролл). Промоушен встроен в GenerateWeapon/Armor. |
| 13:25 | C2 | WithOvershootApplied: next-границы для ТЕКУЩЕГО грейда (техники и экипировка); рекурсии нет (rarity=Common → Overshoot=None). |
| 13:40 | C3 | GenerateWeapon/Armor рефакторены в Core-методы (forceLegendary/forceOvercap); rng-роллы промо/оверкапа потребляются ТОЛЬКО для Transcendent — детерминизм обычных предметов не тронут. |
| 13:55 | C-BUG1 | VerificationService матчинг класса по «_id_» (было Contains: «sword»⊂«greatsword» — greatsword-предметы проверялись по границам sword, ложные out of bounds). |
| 13:55 | C-BUG2 | Оружию разрешена категория Void: tier 5 для оружия был ПУСТ (void_matter — единственный T5) → fallback iron T1: всё оружие L9 было ЖЕЛЕЗНЫМ. Синхронно: PickMaterial + оба фильтка в WeaponBoundsFor. |
| 13:58 | C8 | Headless PASS: промо Epic 16.8%/Legendary 4.0% (расчёт 16/4), оверкап 12.5% (n=16, шум), верификация 40/40. |
| 14:05 | D | BUGFIX-3: BaseDamage trunc→MathF.Round в обоих местах генератора техник (Generate + GenerateSpecified) — устранены отбои на дробных гранях (valid 98→100). |
| 14:10 | D | Коммит f0d11a6 (12 файлов, +791/−56), push в origin/main. |
| 14:55 | E | Аудит-1 (архитектура) завершён: 6 находок, 4 фикса (порядок фаз — Finalize стала последней; FormationData → Core.Data; стабильная сортировка; дубль SceneReadyEvent удалён). Коммит b8ddda1. |
| 15:45 | F | Аудит-2 (мир+NPC) завершён: 7 находок, 4 фикса (травы 0.01%→1%; событие с пустым ResourceId; stale-центры групп; dead NPCSpawnPhase). Коммит e7f2008. |

---

## Журнал прогресса (append-only)

| Время MSK | Этап | Статус | Commit |
|-----------|------|--------|--------|
| 12:49 | старт | синхронизация: push 5 коммитов прошлой сессии | — |
| 12:57 | A | ✅ завершён (аудит чтения) | — |
| 12:57 | B | ✅ завершён (дизайн зафиксирован) | — |
| 13:58 | C | ✅ завершён (фича + 2 баг-фикса, build 0 err, headless PASS) | — |
| 14:10 | D | ✅ завершён (доки + BUGFIX-3 + NEWGAME 100/100 + push) | f0d11a6 |
| 14:55 | E | ✅ аудит-1 архитектура (6 находок, 4 фикса, headless PASS) | b8ddda1 |
| 15:45 | F | ✅ аудит-2 мир+NPC (7 находок, 4 фикса, headless PASS) | e7f2008 |
| 15:50 | G | 🔄 аудит-3 боевой контур в работе | — |

---

*Чекпоинт ведётся в основном потоке, обновляется после каждого закрытого подэтапа.*

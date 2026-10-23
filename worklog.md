# Worklog — Ai-game4 (каноническая хроника проекта, append-only)

> Хроника разработки Cultivation World Simulator: записи сессий (R-серии),
> решения, метки NEXT:. Коммитится в git → GitHub (переживает сбросы песочницы).
>
> НЕ путать с платформенным протокол-логом Z.ai Code:
> `/home/z/my-project/worklog.md` (вне git) — туда пишутся Task ID-записи
> субагентов платформы и состояние песочницы. Игровая разработка — здесь.
>
> Архивы: checkpoints/worklog_backup_20260815.md (генезис, до ~15.08),
> checkpoints/worklog_backup_20261019.md (~22.08 → 19.10.2026),
> checkpoints/worklog_backup_20261023.md (19.10 → 23.10.2026, вкл. Этапы 13–14).
> Лимит §8: при >1000 строк — архивация (формат имени — исключение из §6.2).

---

## Сессия 2026-10-23 coder-1023 ч.6 — старт ЭТАПА-15: верификация аудита + статическое подтверждение кандидатов

**HEAD старт:** 295d0a5 (= база аудитора). **Коммиты эпизода:** 1d7ca6e
(build-reparation), 84c7c75 (канон этапа-15).

### Верификация ответа аудитора

1. **Сборка main 295d0a5 была СЛОМАНА** (аудитор не запускал build): закоммиченный
   Stage13NpcAiTruthSimDebug.cs вызывает QA-аксессор
   `NPCAIService.GetTopThreatIdForQa(NPCState)` (сценарий C3), но сам аксессор
   не вошёл в коммит NPCAIService.cs → CS1061 на свежем клоне.
   **Ремонт:** 1d7ca6e (только потерянный метод; поведение Stage13-фиксов не
   тронуто). Фактический базовый SHA этапа-15 = 1d7ca6e.
2. **Follow-up 11–12 — подтверждены аудитором ОТКРЫТЫМИ, статически верифицированы:**
   - BodyService.cs:877 — `_size = (SizeClass)data.size;` без проверки диапазона
     (ST11-006res) — ОТКРЫТ.
   - BuffTickProcessor.cs:61 — `buff.TickTimer = buff.TickInterval;` (полный
     интервал, дробный остаток теряется; комментарий «BF-I04 без дрифта» не
     соответствует коду) (ST12-003res) — ОТКРЫТ.
   Оба — ОТДЕЛЬНЫЙ пакет (канон 10_23_stage11_12_residual_followups.md),
   в коммиты этапа-15 НЕ смешивать. В отчёте этапа-15 — статус отдельно.

### Канон этапа-15

`checkpoints/10_23_stage15_generator_integrity_audit_request.md` (84c7c75) —
ST15-001..008: генераторы, верификация, дедупликация, ID-коллизии, Heavy,
границы/enum, жизненный цикл реестров. Критерии завершения и артефакты — в §5–6.

### Статическая пре-верификация кандидатов (до симулятора, полная карта)

- **ST15-001 ПОДТВЕРЖДЁН статически:** NPCAssemblyService.EquipHumanoid (стр.
  236–281) — легаси `GenerateWeaponForLevel/GenerateArmorForLevel` для ВСЕХ
  слотов NPC: WeaponClassId="" (визуал падает в fallback-парсинг), MaterialId="",
  Penetration=0, AttackRange=2 (дефолт), AttackSpeedPermil=1000 (кулаки),
  Damage=5+3L (линейная формула, не «матрёшка»), ItemType="Weapon"/NameEn=
  "Sword Level N" (не матчится верификатором). Морфология: все брони
  Slot=Torso, но кладутся в Head/Legs/Feet. Игрок (StartingGearPhase) уже на
  EquipmentGenerator — легаси-путь живёт ТОЛЬКО у NPC. Проверка %1000 (G-1
  старого аудита): УЖЕ ИСПРАВЛЕНО R35 — NextId counter-based
  (`{prefix}_{level}_{seed:x8}_{counter:x6}`), находку не реанимировать.
- **ST15-002 ПОДТВЕРЖДЁН статически:** VerificationService.Validate(Equipment):
  ненайденный класс → Message="Unknown base class — stat bounds skipped",
  IsValid остаётся true (стр. 144–148); CheckFloat `value < min || value > max`
  пропускает NaN (NaN-сравнения false → поле «в границах»). Легаси-оружие
  NPC проходит валидацию БЕЗ проверки границ.
- **ST15-003 ПОДТВЕРЖДЁН статически:** DeduplicationService.Clean(FormationRegistry)
  (стр. 174–189) собирает дубли и печатает счётчик, НО не удаляет; у
  FormationRegistry вообще НЕТ Remove/Clear (только Register/Get/GetAll/Count).
  TechniqueRegistry.Remove есть и Clean(TechniqueRegistry) работает.
- **ST15-004 ПОДТВЕРЖДЁН статически:** Equipment-fingerprint =
  Slot|MaterialId|Grade|ItemLevel|Damage|Defense|Coverage|MaxDurability|Weight|HandType
  — НЕТ Penetration/AttackRange/AttackSpeedPermil/DodgeBonus/DamageReduction/
  MoveSpeedPenalty/QiFlowPenalty. Technique-fingerprint — НЕТ Mastery/
  ArmorPenetration/AoE-полей (AoeShape/Radius/HalfAngle/Falloff/MaxTargets/
  SpareAllies)/IsHoming/ProjectileSpeed. Formation-fingerprint — НЕТ
  ControlType/TargetTag (Trap: Freeze vs Slow → один fingerprint!). Формат
  `:F2/:F3` — культура-зависимость через float.ToString надо проверить
  (инвариантCulture в interpolation C# — по умолчанию CurrentCulture!).
- **ST15-005 ПОДТВЕРЖДЁН статически:** TechniqueGeneratorService.
  GenerateTechniqueId: `(int)(seed ^ (seed >> 16)) & 0xFFFF` — 16 бит;
  коллизия воспроизводима аналитически: seed=0 и seed=0x10001 → оба хеш 0x0000
  (проверить в симе фактически). FormationGeneratorService.GenerateSpecified:
  тот же фрагмент в Id. Register() обоих реестров — молчаливая замена.
  Семантика seed=0 СМЕШАНАНА: ItemGeneratorService — seed=0=авто-счётчик;
  Technique/Formation генераторы — seed=0=валидный детерминированный
  (SeededRandom(0) имеет состояние). Контракт надо зафиксировать (ST15-008).
- **ST15-006 ПОДТВЕРЖДЁН статически:** SizePool = {Small×4, Medium×3, Large×2,
  Great×1} — Heavy НЕДОСТИЖИМ; даунгрейд-логика (стр. 121) мертва (труп кода).
  Спецификация FORMATION_SYSTEM §4: Heavy — размер L6+ с множителем ×10000.
- **ST15-007:** собраны входные точки: генераторы клампят level (Generate→
  [1..10], BuildCore→[1..cultivationLevel]), но НЕ клампят invalid-enum
  (subtype строкой у EquipmentGenerator — PickWeaponClass молча падает в
  случайный при неизвестном subtype); VerificationService не проверяет
  invalid-enum/NaN/Infinity. Проверить фактическими кейсами в симе.
- **ST15-008:** ItemDatabaseService уже имеет ISaveable (блок item_db,
  счётчики EquipmentIdCounter/ItemGenerationCounter, RestoreState=замена
  не merge, ResetWorld=очистка+ре-сид ClassicLoot) — аудитор прав: НЕ
  переделывать, подтвердить адресным тестом. WorldInitPhase чистит ТОЛЬКО
  TechniqueRegistry; FormationRegistry НЕ чистится при пересборке мира —
  stale-формации переживают NewGame (нет Clear API). Cold-load тест —
  отдельным процессом (GODOT_STAGE15 + LoadGame-хук или second-run хук).

### Прочитано (полностью): VerificationService, DeduplicationService,
TechniqueRegistry, FormationRegistry, ItemGeneratorService, EquipmentGenerator,
TechniqueGeneratorService, FormationGeneratorService, ItemDatabaseService,
WorldInitPhase, NPCAssemblyService (шаги 5–7), EquipmentData, TechniqueData,
FormationData, SeededRandom, GeneratorTables, StartingGearPhase/PreGen/
TechniqueGrant (grep-карта), FORMATION_SYSTEM §4, PRE_GENERATION, карта
аудита 09_11 (G-1..G-13 → соответствие ST15 подтверждено).

NEXT: 1) Эпизод-2: Stage15GeneratorIntegrityTruthSimDebug.cs — сценарии
A(ST15-001 NPC-экипировка: класс/материал/статы vs матрёшка) … H(ST15-008
save/load/cold/registries/seed=0), префикс-прогон GODOT_STAGE15_DEBUG=1,
лог → checkpoints/10_23_stage15_generator_integrity_prefix.log, коммит;
2) фиксы по подтверждённым блокам (по одному логическому блоку на коммит);
3) постфикс + отчёт + qa_regression.sh STAGE15 + TESTING_RULES §0.1;
4) follow-up ST11-006res/ST12-003res — отдельный пакет ПОСЛЕ этапа-15.

## Сессия 2026-10-23 coder-1023 ч.7 — ЭТАП-15 РЕАЛИЗОВАН и ЗАКРЫТ (27/27 PASS)

**HEAD старт:** 17be1cc (после ч.6). **Коммиты:** 94acd65 (симулятор
Stage15 + STAGE15-регистрация в qa_regression + префикс-лог 12/15),
8a941db (все фиксы ST15-001..008 + постфикс 27/27), + текущий (отчёт
10_23_stage15_generator_integrity_report.md + TESTING_RULES §0.1 STAGE15).

**Реализация [STAGE 15 AUDIT]** (канон: 10_23_stage15_generator_integrity_
audit_request.md):
- **Префикс Stage15Sim: 12 PASS / 15 FAIL** — все кандидаты воспроизведены
  фактами (A1/A3 легаси-NPC-экипировка с морфологией Torso→Head/Legs/Feet;
  B1 IsValid=true при unknown class; B2 NaN проходит; C1 Clean no-op;
  D1–D5 fingerprints слепы к 11 полям + культура + F2-схлопывание;
  F1 Heavy недостижим; G1/G3/G4; H3 FormationRegistry вне сброса мира).
- **ST15-001 FIXED:** NPCAssemblyService.EquipHumanoid → IEquipmentGenerator
  («Матрёшка»): все 7 слотов, броня по классам слотов, офхенд только 1H,
  зарядник остаётся легаси. Открытие: путь был ДВОЙНОЙ (спаунер уже
  перезаписывал WeaponMain) — легаси-остатки сидели в WeaponOff/Legs/Feet.
- **ST15-002 FIXED:** unknown class оружия/брони = Critical-отказ; NaN —
  отвергается; аксессуар — документированный skip. ±Infinity — DISPROVED
  (сравнения работали и раньше). G2 invalid-enum техник — DISPROVED
  (отвергался и раньше).
- **ST15-003 FIXED:** FormationRegistry.Remove/Clear + Clean реально удаляет
  (идемпотентен, уник выживает).
- **ST15-004 FIXED:** fingerprints = все поведенческие поля + invariant
  культура + R-точность.
- **ST15-005 HARDENED:** ID seed:x8 (32-бит, паттерн NextId) вместо 16-бит
  фрагмента (E1a: seed 0 ≡ 0x10001 по фрагменту — факт); safe-register в
  обоих реестрах (коллизия Id с другим контентом не подменяет; идемпотентность
  сохранена, E3 PASS до/после). Полная коллизия сканом 20000 сидов НЕ
  воспроизведена (ни до, ни после) — честно отражено в отчёте.
- **ST15-006 FIXED:** Heavy в SizePool (1/11), гейт L6+ жив; F1 постфикс:
  Heavy в скане 400 сидов L6, на L5 отсутствует.
- **ST15-007 FIXED:** кламп уровня [1..10]; ArgumentException на неизвестный
  subtype; enum-гарды в валидаторе (Grade=99 = диагностика, не крэш).
- **ST15-008 FIXED (FormationRegistry) + ПОДТВЕРЖДЁН R17 (каталог):**
  FormationRegistry : IWorldResettable — WorldDomainResetCoordinator сбрасывает
  (37 доменов). ItemDatabase R17/R35 — подтверждён адресно (H1 fresh-restore,
  H2 двойной ResetWorld, H4 seed=0 контракты, cold-пара НОВЫХ процессов:
  маркер переживает холодную загрузку, новые ID уникальны). In-session NewGame
  отклоняется by design — контракт пересборки проверен операцией координатора
  (прецедент Stage10-A4/A5).
- **Постфикс: 27/27 PASS — VERDICT: PASS** (лог:
  checkpoints/10_23_stage15_generator_integrity_report.log; отчёт:
  10_23_stage15_generator_integrity_report.md — вкл. верификацию всех
  генераторных находок аудита 09_11 с актуальными статусами).

**Харнес-факты (честно задокументированы):** A1 → характеристика легаси-
генератора (дефект-доказательство — A3 end-to-end; рефайнинг критерия в том
же коммите, что миграция); CloneForm → глубокая копия Effects (поверхностная
мутация ложно равняла fingerprints); A3-морфология учитывает WeaponMain-
домашний слот оружия. Отравление каталога +Inf (обнаружено самим префиксом:
автосейв падал на сериализации) — устранено проверкой копий, не живых предметов.

**Полная регрессия (34 сима, код 8a941db): 32/34 стабильно зелёные.**
- STAGE15 PASS (27/27 + COLD-SAVE + COLD-LOAD парой процессов).
- Этапы 4–14 зелёные — регрессий от фиксов нет.
- **COMBAT_SIM: флаки bug012** (1-й прогон FAIL → повтор PASS, тот же код) —
  прежний известный остаток, вне этапа-15.
- **STAGE8 D6a: НОВЫЙ обнаруженный флак** («зарядка для world-reset не
  стартовала» → NOT TESTED → FAIL; ретраи 2/4 PASS). **ДОКАЗАНО
  ПРЕДСУЩЕСТВУЮЩИМ:** worktree-эксперимент на префикс-коммите 94acd65 (ДО
  фиксов): 4 прогона → 2 PASS / 2 FAIL, тот же D6a. Timing-race харнесса
  (класс bug012), не связан с этапом-15. Прежняя сводка «STAGE8 стабильно»
  была удачей малого числа прогонов.
- Инфра-факт песочницы: бинарник Godot периодически ENOENT при exec
  (overlay-volatile; stat ок, open падает) — лечится копией в /tmp
  (GODOT_BIN-override qa_regression; /tmp/godot_bin/godot).

NEXT: 1) харнес-стабилизация (отдельный пакет): COMBAT_SIM bug012
residual-энтропия (сеять Random.Shared от GODOT_QA_SEED) + STAGE8 D6a
(timing-race старта зарядки: пин/увеличение окна/предусловие qi) → 34/34;
2) follow-up ST11-006res/ST12-003res (канон:
10_23_stage11_12_residual_followups.md) — BodyService SizeClass-диапазон +
BuffTickProcessor дробный остаток таймера — ОТКРЫТЫ, подтверждены
аудитором, отдельные коммиты; 3) владельцу: 2 GAP этапа-15 (удаление
легаси-методов оружия/брони из IItemGeneratorService; двойная генерация
оружия NPC) + раунд-2 физплейтестов (PT-AI01..06, PT-U04..08 — planned).

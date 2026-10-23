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

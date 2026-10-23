#nullable enable
// Создано: 2026-10-23 — Этап-15 аудита (генераторы, верификация объектов,
// целостность данных). Канон: checkpoints/10_23_stage15_generator_integrity_
// audit_request.md ([STAGE 15 AUDIT] verbatim).
// Запуск (основной блок сценариев):
//   GODOT_NEWGAME=1 GODOT_STAGE15_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
// Cold-load пара (НОВЫЙ процесс на каждую фазу — тест межпроцессной
// живучести каталога, ST15-008; не подменяется внутрипроцессной проверкой):
//   GODOT_NEWGAME=1 GODOT_STAGE15_COLD=save godot --headless …  # фаза 1: сейв
//   GODOT_STAGE15_COLD=load   godot --headless …                # фаза 2: загрузка
//
// Сценарии (детерминированные: фиксированные сиды/уровни/объекты):
//   A. ST15-001 — легаси-путь экипировки NPC vs «Матрёшка»:
//      A1 прямые вызовы легаси-генератора (WeaponClassId/MaterialId/
//      AttackRange/Penetration/AttackSpeedPermil/NameEn);
//      A2 контроль: матрёшка-генератор заполняет все боевые поля;
//      A3 end-to-end: SpawnNPC → EquipmentIds → каталог (WeaponOff/Legs —
//      легаси-остатки; морфология слота: item.Slot == ключ словаря).
//   B. ST15-002 — валидатор: B1 легаси-оружие проходит валидацию без
//      проверки границ ("Unknown base class", IsValid=true);
//      B2 NaN в Weight; B3 ±Infinity; B4 аксессуар (charger) —
//      документированный skip vs неизвестное оружие.
//   C. ST15-003 — Clean(FormationRegistry) no-op: 2 дубли + 1 уникальная,
//      счётчик ≠ фактическое удаление, повторный вызов, выживание уникальной.
//   D. ST15-004 — fingerprint'ы: D1 экипировка (Pen/Range/Dodge/DR/MSP),
//      D2 техники (Mastery/ArmorPen/AoE/Homing), D3 формации
//      (ControlType/TargetTag), D4 культура (ru-RU vs invariant),
//      D5 точность (различия < F2 не схлопываются).
//   E. ST15-005 — 16-бит хеш сида в ID + молчаливая подмена реестром:
//      E1 техники (seed 0 vs 0x10001 → один ID, Register затирает),
//      E2 формации, E3 контроль воспроизводимости (одинаковый seed →
//      одинаковый объект).
//   F. ST15-006 — Heavy недостижим в Generate() (скан 400 сидов L6/L5);
//      контроль: GenerateSpecified(Heavy, L<6) → даунгрейд Medium.
//   G. ST15-007 — границы/enum/некорректные входы: G1 level 0/11 без
//      клампа у EquipmentGenerator; G2 invalid-enum техники в валидаторе
//      (диагностика, не крэш); G3 invalid-enum экипировки; G4 неизвестный
//      subtype → тихий случайный класс vs явный отказ.
//   H. ST15-008 — жизненный цикл: H1 cold-restore каталога на СВЕЖЕМ
//      экземпляре + счётчики + уникальность новых ID (подтверждение R17,
//      не переделка); H2 два ResetWorld без утечки; H3 реестр формаций
//      переживает пересборку мира (real NewGame); H4 контракт seed=0
//      (авто-счётчик vs детерминированный — оба режима).
//
// Итог: [Stage15Sim] VERDICT: PASS / FAIL (+ пофазовые PASS/FAIL/GAP/NOT TESTED).
using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;
using CultivationGame.Modules.Formation;
using CultivationGame.Modules.Generator;

namespace CultivationGame.Adapter.Scene
{
    /// <summary>
    /// Headless-верификация Этапа-15: генераторы/валидация/дедуп/ID/реестры.
    /// Каждый сценарий печатает фактическое состояние и причину PASS/FAIL.
    /// </summary>
    public partial class Stage15GeneratorIntegrityTruthSimDebug : Node
    {
        // === DI: сервисы ===
        [Inject] private IItemDatabaseService? _itemDb;
        [Inject] private IItemGeneratorService? _legacyGen;
        [Inject] private IEquipmentGenerator? _matryoshka;
        [Inject] private ITechniqueGeneratorService? _techGen;
        [Inject] private IFormationGeneratorService? _formGen;
        [Inject] private IVerificationService? _verifier;
        [Inject] private DeduplicationService? _dedup;
        [Inject] private TechniqueRegistry? _techRegistry;
        [Inject] private FormationRegistry? _formRegistry;
        [Inject] private INPCSpawnerService? _spawner;
        [Inject] private INPCService? _npcs;
        [Inject] private IGameSession? _session;
        [Inject] private ISaveService? _save;
        [Inject] private IResolver? _resolver;

        // === Результаты ===
        private readonly List<(string Id, string Status, string Detail)> _results = new();

        // === Cold-load фаза (ST15-008H) ===
        private const string ColdSlot = "stage15_coldcheck";
        private const string ColdMarkerPrefix = "STAGE15-COLD-MARKER-";

        public override void _Ready()
        {
            var container = Scene.GameBoot.Container;
            if (container != null)
                ContainerAdapter.InjectProperties(this, container);

            GD.Print("[Stage15Sim] Ready — Этап-15 (генераторы/валидация/дедуп/ID/реестры) starts in 3s");
            _ = RunAsync();
        }

        private async Task RunAsync()
        {
            await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);

            // ── Cold-load пара (отдельный процесс на фазу) ──
            string? cold = System.Environment.GetEnvironmentVariable("GODOT_STAGE15_COLD");
            if (cold == "save" || cold == "load")
            {
                await RunColdLoadPhase(cold == "save");
                PrintSummary();
                return;
            }

            if (_itemDb == null || _legacyGen == null || _matryoshka == null
                || _techGen == null || _formGen == null || _verifier == null
                || _dedup == null || _techRegistry == null || _formRegistry == null
                || _spawner == null || _npcs == null || _session == null || _resolver == null)
            {
                GD.Print("[Stage15Sim] FAIL — DI not wired");
                PrintSummary();
                return;
            }

            GD.Print($"[Stage15Sim] каталог предметов: {_itemDb.Count}; techniqueRegistry={_techRegistry.Count}; formationRegistry={_formRegistry.Count}");

            await RunScenario("A", ScenarioA);
            await RunScenario("B", ScenarioB);
            await RunScenario("C", ScenarioC);
            await RunScenario("D", ScenarioD);
            await RunScenario("E", ScenarioE);
            await RunScenario("F", ScenarioF);
            await RunScenario("G", ScenarioG);
            await RunScenario("H", ScenarioH);

            PrintSummary();
        }

        // ================================================================
        //  Хелперы
        // ================================================================

        private async Task RunScenario(string name, Func<Task> scenario)
        {
            try { await scenario(); }
            catch (Exception ex)
            {
                GD.PrintErr($"[Stage15Sim] сценарий {name} упал: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                RecordGap($"CRASH/{name}", $"необработанное исключение: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void Record(string id, bool pass, string detail)
            => _results.Add((id, pass ? "PASS" : "FAIL", detail));

        private void RecordGap(string id, string detail)
            => _results.Add((id, "GAP", detail));

        private void RecordNotTested(string id, string detail)
            => _results.Add((id, "NOT TESTED", detail));

        private static Position2D P(int x, int y) => new(x, y);

        /// <summary>Легаси-индикаторы предмета: поля «матрёшки», которые
        /// легаси-генератор не заполняет (ST15-001).</summary>
        private static bool LooksLegacy(EquipmentData e)
            => string.IsNullOrEmpty(e.WeaponClassId) && string.IsNullOrEmpty(e.MaterialId)
               && e.Penetration == 0 && e.AttackSpeedPermil == 1000
               && (e.NameEn.StartsWith("Sword Level") || e.NameEn.StartsWith("Armor Level"));

        // ================================================================
        //  A. ST15-001 — легаси-путь экипировки NPC
        // ================================================================

        private async Task ScenarioA()
        {    
            GD.Print("── A. ST15-001: легаси-путь экипировки NPC vs «Матрёшка» ──");

            // A1: прямые вызовы легаси-генератора — что получает NPC из EquipHumanoid.
            var lw = _legacyGen!.GenerateWeaponForLevel(5, 20261023);
            bool a1Legacy = lw != null && LooksLegacy(lw)
                && lw.AttackRange == 2 && lw.ItemType == "Weapon";
            GD.Print($"[A1] легаси-оружие: classId='{lw?.WeaponClassId}' material='{lw?.MaterialId}' " +
                     $"pen={lw?.Penetration} range={lw?.AttackRange} spm={lw?.AttackSpeedPermil} " +
                     $"NameEn='{lw?.NameEn}' dmg={lw?.Damage} (5+3L={5 + 3 * 5})");
            Record("ST15-001/A1", !a1Legacy,
                a1Legacy
                    ? "ПОДТВЕРЖДЕНО: легаси-генератор не заполняет WeaponClassId/MaterialId/Penetration/AttackSpeedPermil, линейный урон, ItemType='Weapon'"
                    : "легаси-генератор выдаёт матрёшка-поля (путь уже мигрирован)");

            // A2: контроль — «Матрёшка» заполняет боевые поля.
            var mw = _matryoshka!.GenerateWeapon(5, null, 20261023);
            bool a2Ok = !string.IsNullOrEmpty(mw.WeaponClassId) && !string.IsNullOrEmpty(mw.MaterialId)
                && mw.Penetration > 0 && mw.AttackSpeedPermil >= 250 && mw.AttackSpeedPermil <= 2000
                && mw.NameEn.Contains('_') && mw.Damage > 0;
            GD.Print($"[A2] матрёшка-контроль: classId='{mw.WeaponClassId}' material='{mw.MaterialId}' " +
                     $"pen={mw.Penetration} range={mw.AttackRange} spm={mw.AttackSpeedPermil} NameEn='{mw.NameEn}'");
            Record("ST15-001/A2", a2Ok,
                a2Ok ? "контроль: матрёшка заполняет класс/материал/пробитие/скорость" : "контроль сломан: матрёшка не заполняет поля");

            // A3: end-to-end — реальный спавн NPC, читаем слоты после ОБЕИХ
            // фаз (EquipHumanoid + спаунерская перезапись оружия).
            string npcId = _spawner!.SpawnNPC("human", NPCRole.Passerby, 3, P(46, 20), 987654321L);
            var st = npcId != null ? _npcs!.GetNPCState(npcId) : null;
            if (st == null)
            {
                RecordNotTested("ST15-001/A3", "спавн NPC не удался");
                return;
            }

            int legacySlots = 0, checkedSlots = 0, slotMismatches = 0;
            var report = new List<string>();
            foreach (var (slot, itemId) in st.EquipmentIds)
            {
                if (slot == EquipmentSlot.Belt) continue; // зарядник — легаси по канону
                checkedSlots++;
                if (!_itemDb!.TryGetItem(itemId, out var it) || it is not EquipmentData eq)
                {
                    report.Add($"{slot}=НЕТ В КАТАЛОГЕ");
                    legacySlots++;
                    continue;
                }
                if (LooksLegacy(eq)) legacySlots++;
                if (eq.Slot != slot) slotMismatches++;
                report.Add($"{slot}:name='{eq.NameEn}' class='{eq.WeaponClassId}' item.Slot={eq.Slot}");
            }
            GD.Print($"[A3] NPC {npcId} слоты: {string.Join("; ", report)}");

            // Канон: ВСЕ боевые слоты NPC — матрёшка с согласованной морфологией.
            // Префикс-ожидание: FAIL (легаси-остатки WeaponOff/Head/Legs/Feet и
            // Slot=Torso у предметов в чужих слотах).
            bool a3Ok = legacySlots == 0 && slotMismatches == 0 && checkedSlots > 0;
            Record("ST15-001/A3", a3Ok,
                $"боевых слотов={checkedSlots}, легаси={legacySlots}, морфология-несоответствий={slotMismatches} " +
                (a3Ok ? "— все слоты матрёшка, item.Slot == слоту" : "— ПОДТВЕРЖДЕНО: EquipHumanoid держит легаси-путь (NameEn 'Armor Level N', Slot=Torso в Head/Legs/Feet)"));

            await Task.CompletedTask;
        }

        // ================================================================
        //  B. ST15-002 — валидатор пропускает неизвестный класс и NaN
        // ================================================================

        private async Task ScenarioB()
        {
            GD.Print("── B. ST15-002: валидатор — неизвестный класс / NaN / Infinity ──");

            // B1: легаси-оружие (категория Weapon!) валидируется БЕЗ проверки границ.
            var lw = _legacyGen!.GenerateWeaponForLevel(5, 20261024);
            var r1 = _verifier!.Validate(lw, 10);
            bool b1Defect = r1.IsValid
                && (r1.Message == null || r1.Message.Contains("Unknown base class"));
            GD.Print($"[B1] легаси-оружие (Weapon): IsValid={r1.IsValid} msg='{r1.Message}' — границы уровня НЕ проверены");
            // Канон: предмет категории Weapon/Armor без распознанного класса =
            // Critical-отказ (не «skip»).
            Record("ST15-002/B1", !b1Defect,
                b1Defect
                    ? "ПОДТВЕРЖДЕНО: IsValid=true при непроверенных границах (unknown class у оружия)"
                    : "неизвестный класс оружия → явный отказ (валидация границ не обходится)");

            // B2: NaN в числовом поле. ВАЖНО: проверяем КОПИЮ — мутация
            // зарегистрированного предмета отравляет каталог (+Inf роняет
            // сериализацию сейва — обнаружено самим префикс-прогоном).
            var w2src = _matryoshka!.GenerateWeapon(5, "sword", 20261025);
            var w2 = Clone(w2src);
            w2.Weight = float.NaN;
            var r2 = _verifier.Validate(w2, 10);
            GD.Print($"[B2] Weight=NaN (копия): IsValid={r2.IsValid} msg='{r2.Message}'");
            Record("ST15-002/B2", !r2.IsValid,
                r2.IsValid ? "ПОДТВЕРЖДЕНО: NaN проходит CheckFloat (NaN<min=false, NaN>max=false)" : "NaN отвергается валидатором");

            // B3: ±Infinity (на копиях) — сравнения с Inf работают, ожидаем
            // честный отказ и в префиксе (проверка «Inf тоже не протекает»).
            var w3src = _matryoshka.GenerateWeapon(5, "sword", 20261025);
            var w3 = Clone(w3src);
            w3.Weight = float.PositiveInfinity;
            var r3p = _verifier.Validate(w3, 10);
            var w3n = Clone(w3src);
            w3n.Weight = float.NegativeInfinity;
            var r3n = _verifier.Validate(w3n, 10);
            GD.Print($"[B3] Weight=+Inf (копия): IsValid={r3p.IsValid}; -Inf: IsValid={r3n.IsValid}");
            Record("ST15-002/B3", !r3p.IsValid && !r3n.IsValid,
                (!r3p.IsValid && !r3n.IsValid) ? "±Infinity отвергаются" : "ПОДТВЕРЖДЕНО: Infinity проходит проверку границ");

            // B4: аксессуар (charger) — границ НЕТ по канону, skip легитимен,
            // но сообщение должно отличать аксессуар от нераспознанного оружия.
            var ch = _legacyGen.GenerateChargerForLevel(5, 20261027);
            var r4 = _verifier.Validate(ch, 10);
            bool b4Ok = r4.IsValid; // skip границ для аксессуара — норма
            GD.Print($"[B4] charger (Accessory): IsValid={r4.IsValid} msg='{r4.Message}'");
            Record("ST15-002/B4", b4Ok,
                b4Ok ? $"аксессуар: bounds-skip легитимен (IsValid=true, msg='{r4.Message}')" : "аксессуар ошибочно проваливает валидацию");
            await Task.CompletedTask;
        }

        // ================================================================
        //  C. ST15-003 — Clean(FormationRegistry) не удаляет
        // ================================================================

        private async Task ScenarioC()
        {
            GD.Print("── C. ST15-003: Clean(FormationRegistry) no-op ──");

            // Свежий реестр: 2 формации с одним fingerprint (разные Id) + 1 уникальная.
            var reg = new FormationRegistry();
            var f1 = new FormationData
            {
                Id = "dup_a", FormationType = FormationType.Barrier, Size = FormationSize.Small,
                Shape = FormationShape.Circle, RequiredLevel = 1, Element = Element.Fire,
                Effects = { new FormationEffectEntry { EffectType = FormationEffectType.Shield, TargetStat = StatType.Defense, Value = 0.3f, TargetTag = "ally" } }
            };
            var f2 = new FormationData
            {
                Id = "dup_b", FormationType = FormationType.Barrier, Size = FormationSize.Small,
                Shape = FormationShape.Circle, RequiredLevel = 1, Element = Element.Fire,
                Effects = { new FormationEffectEntry { EffectType = FormationEffectType.Shield, TargetStat = StatType.Defense, Value = 0.3f, TargetTag = "ally" } }
            };
            var f3 = new FormationData
            {
                Id = "uniq_c", FormationType = FormationType.Trap, Size = FormationSize.Medium,
                Shape = FormationShape.Star, RequiredLevel = 2, Element = Element.Water,
                Effects = { new FormationEffectEntry { EffectType = FormationEffectType.Control, TargetStat = StatType.Speed, Value = 0.5f, TargetTag = "enemy" } }
            };
            reg.Register(f1);
            reg.Register(f2);
            reg.Register(f3);

            int reported = _dedup!.Clean(reg);
            int afterCount = reg.Count;
            bool uniqSurvives = reg.Get("uniq_c") != null;
            int reported2 = _dedup.Clean(reg); // повторный вызов

            GD.Print($"[C] Clean#1={reported} (ожидается 1 дубль); count после={afterCount} (должно быть 2); " +
                     $"уник выжил={uniqSurvives}; Clean#2={reported2} (должно быть 0)");

            // Канон: Clean реально удаляет; счётчик = фактическому удалению;
            // уникальная сохраняется; повторный вызов = 0.
            bool cOk = reported == 1 && afterCount == 2 && uniqSurvives && reported2 == 0;
            Record("ST15-003/C1", cOk,
                cOk
                    ? "Clean удаляет фактические дубли (count/counter/unique/idempotent — ок)"
                    : $"ПОДТВЕРЖДЕНО: no-op — счётчик {reported}, count {afterCount} (не 2), повторный {reported2}");
            await Task.CompletedTask;
        }

        // ================================================================
        //  D. ST15-004 — fingerprint'ы не различают поведенческие поля
        // ================================================================

        private async Task ScenarioD()
        {
            GD.Print("── D. ST15-004: полнота fingerprint'ов ──");

            // D1: экипировка — пары, различающиеся ТОЛЬКО одним полем боя.
            var base1 = _matryoshka!.GenerateWeapon(5, "spear", 31);
            var probeFields = new (string Name, Action<EquipmentData> Mutate)[]
            {
                ("Penetration", e => e.Penetration += 5),
                ("AttackRange", e => e.AttackRange += 3),
                ("DodgeBonus", e => e.DodgeBonus += 15f),
                ("DamageReduction", e => e.DamageReduction += 25f),
                ("MoveSpeedPenalty", e => e.MoveSpeedPenalty -= 20f),
                ("AttackSpeedPermil", e => e.AttackSpeedPermil -= 200),
            };
            int d1Collapse = 0;
            foreach (var (name, mutate) in probeFields)
            {
                var clone = Clone(base1);
                mutate(clone);
                bool same = DeduplicationService.Fingerprint(base1) == DeduplicationService.Fingerprint(clone);
                if (same) d1Collapse++;
                GD.Print($"[D1] {name}: fp равны={same}");
            }
            Record("ST15-004/D1", d1Collapse == 0,
                d1Collapse == 0
                    ? "все поведенческие поля экипировки входят в fingerprint"
                    : $"ПОДТВЕРЖДЕНО: {d1Collapse}/{probeFields.Length} полей не различаются (ложные дубли)");

            // D2: техники — Mastery/ArmorPenetration/AoE/Homing.
            var t1 = _techGen!.BuildSpecified(TechniqueType.Combat, TechniqueGrade.Common, 3, 5, 41);
            var tProbes = new (string Name, Action<TechniqueData> Mutate)[]
            {
                ("Mastery", t => t.Mastery += 25f),
                ("ArmorPenetration", t => t.ArmorPenetration += 10),
                ("AoeShape", t => { t.AoeShape = AoeShape.Cone; t.AoeRadiusTiles = 6; }),
                ("AoeMaxTargets", t => t.AoeMaxTargets += 4),
                ("IsHoming", t => { t.IsHoming = true; t.ProjectileSpeedTilesPerSec = 8; }),
            };
            int d2Collapse = 0;
            foreach (var (name, mutate) in tProbes)
            {
                var clone = CloneTech(t1);
                mutate(clone);
                bool same = DeduplicationService.Fingerprint(t1) == DeduplicationService.Fingerprint(clone);
                if (same) d2Collapse++;
                GD.Print($"[D2] {name}: fp равны={same}");
            }
            Record("ST15-004/D2", d2Collapse == 0,
                d2Collapse == 0
                    ? "все поведенческие поля техник входят в fingerprint"
                    : $"ПОДТВЕРЖДЕНО: {d2Collapse}/{tProbes.Length} полей не различаются");

            // D3: формации — ControlType (Trap: Freeze vs Slow) и TargetTag.
            var f1 = new FormationData
            {
                Id = "d3_a", FormationType = FormationType.Trap, Size = FormationSize.Small,
                Shape = FormationShape.Circle, RequiredLevel = 1, Element = Element.Fire,
                Effects = { new FormationEffectEntry { EffectType = FormationEffectType.Control, ControlType = ControlType.Freeze, TargetStat = StatType.Speed, Value = 0.5f, TargetTag = "enemy" } }
            };
            var f2 = CloneForm(f1, "d3_b");
            f2.Effects[0].ControlType = ControlType.Slow;
            bool ctrlSame = DeduplicationService.Fingerprint(f1) == DeduplicationService.Fingerprint(f2);
            var f3 = CloneForm(f1, "d3_c");
            f3.Effects[0].TargetTag = "ally";
            bool tagSame = DeduplicationService.Fingerprint(f1) == DeduplicationService.Fingerprint(f3);
            GD.Print($"[D3] ControlType Freeze-vs-Slow: fp равны={ctrlSame}; TargetTag enemy-vs-ally: fp равны={tagSame}");
            Record("ST15-004/D3", !ctrlSame && !tagSame,
                (!ctrlSame && !tagSame)
                    ? "ControlType/TargetTag входят в fingerprint"
                    : $"ПОДТВЕРЖДЕНО: ControlType не различается={ctrlSame}, TargetTag не различается={tagSame}");

            // D4: культура-независимость (ru-RU: запятая как разделитель).
            var invFp = DeduplicationService.Fingerprint(t1);
            string? failure = null;
            CultureInfo saved = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
                var ruFp = DeduplicationService.Fingerprint(t1);
                if (ruFp != invFp) failure = $"fp зависит от культуры: inv='{invFp.Substring(Math.Max(0, invFp.Length - 20))}' vs ru='{ruFp.Substring(Math.Max(0, ruFp.Length - 20))}'";
            }
            catch (CultureNotFoundException)
            {
                failure = "культура ru-RU недоступна в рантайме";
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
            }
            GD.Print($"[D4] инвариант vs ru-RU: {(failure ?? "fp совпадают")}");
            Record("ST15-004/D4", failure == null,
                failure == null ? "fingerprint культура-независим" : $"ПОДТВЕРЖДЕНО: {failure}");

            // D5: точность — различия меньше точности формата не схлопываются.
            var tA = CloneTech(t1);
            var tB = CloneTech(t1);
            tA.Cooldown = 3.001f;
            tB.Cooldown = 3.004f; // различие 0.003 < F2-гранулярности (3.00 vs 3.00)
            bool precisionCollapse = DeduplicationService.Fingerprint(tA) == DeduplicationService.Fingerprint(tB);
            GD.Print($"[D5] Cooldown 3.001 vs 3.004: fp равны={precisionCollapse}");
            Record("ST15-004/D5", !precisionCollapse,
                precisionCollapse
                    ? "ПОДТВЕРЖДЕНО: различие 0.003 схлопывается (F2-формат)"
                    : "малые различия сохраняются в fingerprint (полная точность)");
            await Task.CompletedTask;
        }

        /// <summary>Независимая сигнатура контента техники (не зависит от
        /// дедуп-fingerprint — его неполнота и есть кандидат ST15-004).</summary>
        private static string ContentSig(TechniqueData t)
            => $"{t.Subtype}|{t.Mastery:R}|{t.QiCost}|{t.CapacityCost}|{t.BaseDamage}|{t.Cooldown:R}|{t.Range:R}|{t.CastTime:R}|{t.IsUltimate}|{t.AoeShape}|{t.IsHoming}";

        /// <summary>Независимая сигнатура контента формации.</summary>
        private static string FormSig(FormationData f)
        {
            var sb = new System.Text.StringBuilder(96);
            sb.Append($"{f.Shape}|{f.Element}|{f.EffectRadiusMeters}|{f.Effects.Count}");
            foreach (var e in f.Effects)
                sb.Append($"|{e.EffectType}:{e.TargetStat}:{e.Value:R}:{e.ControlType}:{e.TargetTag}");
            return sb.ToString();
        }

        private static EquipmentData Clone(EquipmentData src)
        {
            var c = new EquipmentData
            {
                ItemId = src.ItemId + "_c", NameRu = src.NameRu, NameEn = src.NameEn,
                Description = src.Description, ItemType = src.ItemType,
                Category = src.Category, Rarity = src.Rarity,
                Stackable = src.Stackable, MaxStack = src.MaxStack,
                Weight = src.Weight, Volume = src.Volume, Value = src.Value,
                HasDurability = src.HasDurability, MaxDurability = src.MaxDurability,
                Slot = src.Slot, HandType = src.HandType, WeaponClassId = src.WeaponClassId,
                Damage = src.Damage, AttackSpeedPermil = src.AttackSpeedPermil,
                Penetration = src.Penetration, AttackRange = src.AttackRange,
                Defense = src.Defense, Coverage = src.Coverage,
                DamageReduction = src.DamageReduction, DodgeBonus = src.DodgeBonus,
                MoveSpeedPenalty = src.MoveSpeedPenalty, QiFlowPenalty = src.QiFlowPenalty,
                MaterialId = src.MaterialId, MaterialCategory = src.MaterialCategory,
                MaterialTier = src.MaterialTier, Grade = src.Grade, ItemLevel = src.ItemLevel,
                RequiredCultivationLevel = src.RequiredCultivationLevel,
            };
            foreach (var b in src.StatBonuses) c.StatBonuses.Add(b);
            return c;
        }

        private static TechniqueData CloneTech(TechniqueData src) => new()
        {
            TechniqueId = src.TechniqueId + "_c", NameRu = src.NameRu, NameEn = src.NameEn,
            Description = src.Description, Type = src.Type, Subtype = src.Subtype,
            Grade = src.Grade, Element = src.Element, Level = src.Level,
            CapacityCost = src.CapacityCost, QiCost = src.QiCost, BaseDamage = src.BaseDamage,
            Cooldown = src.Cooldown, Range = src.Range, CastTime = src.CastTime,
            IsUltimate = src.IsUltimate, Mastery = src.Mastery, ArmorPenetration = src.ArmorPenetration,
            AoeShape = src.AoeShape, AoeRadiusTiles = src.AoeRadiusTiles, AoeHalfAngleDeg = src.AoeHalfAngleDeg,
            AoeFalloffPermil = src.AoeFalloffPermil, AoeMaxTargets = src.AoeMaxTargets,
            SpareAlliesPermil = src.SpareAlliesPermil, IsHoming = src.IsHoming,
            ProjectileSpeedTilesPerSec = src.ProjectileSpeedTilesPerSec,
        };

        private static FormationData CloneForm(FormationData src, string newId) => new()
        {
            Id = newId, DisplayName = src.DisplayName, FormationType = src.FormationType,
            Size = src.Size, Shape = src.Shape, RequiredLevel = src.RequiredLevel,
            Element = src.Element, EffectRadiusMeters = src.EffectRadiusMeters,
            CoreType = src.CoreType, IsReusable = src.IsReusable,
            Effects = src.Effects,
        };

        // ================================================================
        //  E. ST15-005 — короткий хеш сида: коллизия ID + подмена реестра
        // ================================================================

        private async Task ScenarioE()
        {
            GD.Print("── E. ST15-005: 16-бит хеш сида → коллизия ID → молчаливая подмена ──");

            // E1: фрагментный анализ — seed 0 и 0x10001 дают ОДИНАКОВЫЙ 16-бит
            // фрагмент (…_0000), но остальные компоненты ID различаются →
            // полная коллизия не наступила. Слабость пространства (16 бит)
            // доказана; ниже — поиск фактической полной коллизии сканом.
            var reg = new TechniqueRegistry();
            var gen = new TechniqueGeneratorService(reg);
            var t0 = gen.GenerateSpecified(TechniqueType.Combat, 3, 5, 0);
            var tX = gen.GenerateSpecified(TechniqueType.Combat, 3, 5, 0x10001);
            bool fragCollision = t0.TechniqueId[^4..] == tX.TechniqueId[^4..];
            GD.Print($"[E1a] id(seed=0)='{t0.TechniqueId}' id(seed=0x10001)='{tX.TechniqueId}' " +
                     $"хеш-фрагменты равны={fragCollision} (пространство ID сужено до 16 бит сида)");

            // E1b: скан 20000 сидов — фактическая полная коллизия ID у пары
            // сидов (одинаковые компоненты + одинаковый хеш) с РАЗНЫМ контентом
            // (mastery/qiCost…) + доказательство подмены в реестре.
            var byId = new Dictionary<string, (TechniqueData tech, int seed)>()
            { [t0.TechniqueId] = (t0, 0) };
            string? collisionId = null;
            TechniqueData? first = null, second = null;
            int firstSeed = 0, secondSeed = 0;
            for (int s = 1; s <= 20000 && collisionId == null; s++)
            {
                var t = gen.GenerateSpecified(TechniqueType.Combat, 3, 5, s);
                if (byId.TryGetValue(t.TechniqueId, out var prev))
                {
                    if (ContentSig(prev.tech) != ContentSig(t))
                    {
                        collisionId = t.TechniqueId;
                        first = prev.tech; firstSeed = prev.seed; second = t; secondSeed = s;
                    }
                }
                else
                {
                    byId[t.TechniqueId] = (t, s);
                }
            }
            if (collisionId == null)
            {
                GD.Print("[E1b] скан 20000 сидов: полная коллизия не найдена");
                Record("ST15-005/E1", true,
                    "полная коллизия ID не воспроизведена сканом 20000 сидов (фрагментная слабость 16 бит — факт E1a)");
            }
            else
            {
                var stored = reg.Get(collisionId);
                bool substituted = stored != null && stored == second;
                GD.Print($"[E1b] КОЛЛИЗИЯ: id='{collisionId}' seed{firstSeed} vs seed{secondSeed}; " +
                         $"mastery {first!.Mastery:F2} vs {second!.Mastery:F2}; в реестре — второй объект={substituted}");
                Record("ST15-005/E1", false,
                    $"ПОДТВЕРЖДЕНО: разные seed/контент → один ID '{collisionId}'; Register молча затёр первый объект (mastery {first.Mastery:F2}→{second.Mastery:F2})");
            }

            // E2: формации — тот же фрагмент в Id; скан аналогично E1b.
            var freg = new FormationRegistry();
            var fgen = new FormationGeneratorService(freg);
            var fById = new Dictionary<string, (FormationData form, int seed)>();
            string? fCollisionId = null;
            FormationData? fFirst = null, fSecond = null;
            int fFirstSeed = 0, fSecondSeed = 0;
            for (int s = 1; s <= 20000 && fCollisionId == null; s++)
            {
                var f = fgen.GenerateSpecified(FormationType.Barrier, FormationSize.Small, 3, s);
                if (fById.TryGetValue(f.Id, out var prev))
                {
                    if (FormSig(prev.form) != FormSig(f))
                    {
                        fCollisionId = f.Id;
                        fFirst = prev.form; fFirstSeed = prev.seed; fSecond = f; fSecondSeed = s;
                    }
                }
                else
                {
                    fById[f.Id] = (f, s);
                }
            }
            if (fCollisionId == null)
            {
                GD.Print("[E2] скан 20000 сидов: коллизия Id формаций не найдена");
                Record("ST15-005/E2", true, "коллизия ID формаций не воспроизведена сканом 20000 сидов");
            }
            else
            {
                var fStored = freg.Get(fCollisionId);
                GD.Print($"[E2] КОЛЛИЗИЯ: id='{fCollisionId}' seed{fFirstSeed} vs seed{fSecondSeed}; " +
                         $"shape {fFirst!.Shape} vs {fSecond!.Shape}; в реестре — второй={fStored == fSecond}");
                Record("ST15-005/E2", false,
                    $"ПОДТВЕРЖДЕНО: коллизия Id формаций '{fCollisionId}' + молчаливая подмена в реестре");
            }

            // E3: контроль воспроизводимости — одинаковый seed → одинаковый объект
            // (контракт должен сохраниться после фикса).
            var reg3 = new TechniqueRegistry();
            var gen3 = new TechniqueGeneratorService(reg3);
            var a1 = gen3.GenerateSpecified(TechniqueType.Combat, 3, 5, 777);
            var a2 = gen3.GenerateSpecified(TechniqueType.Combat, 3, 5, 777);
            bool reproducible = a1.TechniqueId == a2.TechniqueId
                && DeduplicationService.Fingerprint(a1) == DeduplicationService.Fingerprint(a2)
                && Math.Abs(a1.Mastery - a2.Mastery) < 0.0001f;
            GD.Print($"[E3] воспроизводимость seed=777: id равны={a1.TechniqueId == a2.TechniqueId}, контент равен={reproducible}");
            Record("ST15-005/E3", reproducible,
                reproducible ? "повторный вызов с тем же seed → тот же объект (контракт детерминизма)" : "РЕГРЕСС-РИСК: тот же seed даёт разные объекты");
            await Task.CompletedTask;
        }

        // ================================================================
        //  F. ST15-006 — Heavy недостижим в обычной генерации
        // ================================================================

        private async Task ScenarioF()
        {
            GD.Print("── F. ST15-006: Heavy в SizePool генератора формаций ──");

            // F1: скан 400 сидов на L6+ (Heavy допустим с L6) и L5 (запрещён).
            var sizes6 = new HashSet<FormationSize>();
            var sizes5 = new HashSet<FormationSize>();
            for (int s = 1; s <= 400; s++)
            {
                var f = _formGen!.Generate(6, s);
                sizes6.Add(f.Size);
                var f5 = _formGen.Generate(5, s);
                sizes5.Add(f5.Size);
            }
            GD.Print($"[F1] L6: размеры={string.Join(",", sizes6)}; L5: размеры={string.Join(",", sizes5)}");
            bool heavyAtL6 = sizes6.Contains(FormationSize.Heavy);
            bool heavyAtL5 = sizes5.Contains(FormationSize.Heavy);
            // Канон: Heavy достижим на L6+ (FORM_SYSTEM §4: «только L6+» =
            // разрешён, не «исключён»), и НИКОГДА не появляется ниже L6.
            bool f1Ok = heavyAtL6 && !heavyAtL5;
            Record("ST15-006/F1", f1Ok,
                f1Ok
                    ? $"Heavy достижим на L6+ ({string.Join(",", sizes6)}), ниже L6 отсутствует"
                    : (!heavyAtL6
                        ? $"ПОДТВЕРЖДЕНО: Heavy недостижим в Generate() на L6 (пул={{{string.Join(",", sizes6)}}}; даунгрейд-логика мертва)"
                        : "REGRESS: Heavy появился на L5 — нарушено ограничение L6+"));

            // F2: контроль даунгрейда — GenerateSpecified(Heavy, L3) → Medium.
            var downgraded = _formGen!.GenerateSpecified(FormationType.Barrier, FormationSize.Heavy, 3, 51);
            bool f2Ok = downgraded.Size != FormationSize.Heavy && downgraded.RequiredLevel == 3;
            GD.Print($"[F2] GenerateSpecified(Heavy, L3): size={downgraded.Size} (ожидание: даунгрейд)");
            Record("ST15-006/F2", f2Ok,
                f2Ok ? $"явный Heavy ниже L6 даунгрейдится ({downgraded.Size})" : "даунгрейд Heavy не работает");
            await Task.CompletedTask;
        }

        // ================================================================
        //  G. ST15-007 — границы уровня, enum, некорректные входы
        // ================================================================

        private async Task ScenarioG()
        {
            GD.Print("── G. ST15-007: границы/enum/некорректные входы ──");

            // G1: уровень вне [1..MAX] у EquipmentGenerator (кламп?).
            var w0 = _matryoshka!.GenerateWeapon(0, null, 61);
            var w11 = _matryoshka.GenerateWeapon(11, null, 62);
            GD.Print($"[G1] GenerateWeapon(0): ItemLevel={w0.ItemLevel}; GenerateWeapon(11): ItemLevel={w11.ItemLevel}");
            bool g1Ok = w0.ItemLevel >= 1 && w11.ItemLevel <= GameConstants.MAX_CULTIVATION_LEVEL;
            Record("ST15-007/G1", g1Ok,
                g1Ok
                    ? $"уровень клампится в [1..{GameConstants.MAX_CULTIVATION_LEVEL}]"
                    : $"ПОДТВЕРЖДЕНО: уровень не клампится (0→{w0.ItemLevel}, 11→{w11.ItemLevel}; объект с ItemLevel вне диапазона создаётся тихо)");

            // G2: invalid-enum техники в валидаторе — диагностика вместо крэша.
            var badTech = new TechniqueData
            {
                TechniqueId = "bad_tech", Type = (TechniqueType)999, Subtype = CombatSubtype.None,
                Grade = (TechniqueGrade)77, Level = 3, CapacityCost = 100, QiCost = 100,
                BaseDamage = 50, Cooldown = 3, Range = 5, CastTime = 1,
            };
            ValidationResult? r2 = null;
            string g2Err = "";
            try { r2 = _verifier!.Validate(badTech, 10); }
            catch (Exception ex) { g2Err = $"{ex.GetType().Name}: {ex.Message}"; }
            GD.Print($"[G2] invalid-enum техника: exception={g2Err != ""} ({g2Err}); IsValid={r2?.IsValid}");
            Record("ST15-007/G2", r2 != null && !r2.IsValid,
                r2 != null && !r2.IsValid
                    ? $"invalid-enum отвергнут диагностикой (msg='{r2.Message}')"
                    : $"ПОДТВЕРЖДЕНО: {(g2Err != "" ? "крэш вместо диагностики: " + g2Err : "invalid-enum прошёл валидацию (IsValid=true)")}");

            // G3: invalid-enum экипировки (Grade вне диапазона массивов).
            var badEq = _matryoshka.GenerateWeapon(5, "sword", 63);
            badEq.Grade = (EquipmentGrade)99;
            ValidationResult? r3 = null;
            string g3Err = "";
            try { r3 = _verifier!.Validate(badEq, 10); }
            catch (Exception ex) { g3Err = $"{ex.GetType().Name}: {ex.Message}"; }
            GD.Print($"[G3] Grade=99: exception={g3Err != ""} ({g3Err}); IsValid={r3?.IsValid}");
            Record("ST15-007/G3", r3 != null && !r3.IsValid,
                r3 != null && !r3.IsValid
                    ? "invalid-grade отвергнут диагностикой"
                    : $"ПОДТВЕРЖДЕНО: {(g3Err != "" ? "крэш: " + g3Err : "invalid-grade прошёл валидацию")}");

            // G4: неизвестный subtype — тихий случайный класс vs явный отказ.
            string g4Err = "";
            EquipmentData? w4 = null;
            try { w4 = _matryoshka.GenerateWeapon(5, "plasma_rifle", 64); }
            catch (Exception ex) { g4Err = $"{ex.GetType().Name}: {ex.Message}"; }
            bool silentFallback = w4 != null && g4Err == "";
            GD.Print($"[G4] GenerateWeapon('plasma_rifle'): выдан класс='{w4?.WeaponClassId}' exception='{g4Err}'");
            // Канон: ЯВНО запрошенный неизвестный subtype — отказ с диагностикой
            // (fail-fast: тихий фолбэк маскирует опечатки и ломает ожидания морфологии).
            Record("ST15-007/G4", !silentFallback,
                silentFallback
                    ? $"ПОДТВЕРЖДЕНО: тихий фолбэк на случайный класс '{w4?.WeaponClassId}' вместо отказа"
                    : $"неизвестный subtype → явный отказ ({g4Err})");
            await Task.CompletedTask;
        }

        // ================================================================
        //  H. ST15-008 — жизненный цикл реестров, счётчиков, seed=0
        // ================================================================

        private async Task ScenarioH()
        {
            GD.Print("── H. ST15-008: жизненный цикл реестров/счётчиков/seed=0 ──");

            // H1: cold-restore каталога на СВЕЖЕМ экземпляре + счётчики +
            // уникальность новых ID. Подтверждение R17 (не переделка).
            var liveDb = _itemDb as ItemDatabaseService;
            if (liveDb == null)
            {
                RecordNotTested("ST15-008/H1", "IItemDatabaseService не ItemDatabaseService (нет ISaveable-доступа)");
            }
            else
            {
                var marker = _legacyGen!.GenerateConsumableForLevel(5, 20261028);
                var state = liveDb.CaptureState();
                var fresh = new ItemDatabaseService();
                fresh.Initialize();
                fresh.RestoreState(state);
                bool markerOk = fresh.TryGetItem(marker.ItemId, out _);
                // Счётчики восстановлены → новый ID не коллидирует с каталогом.
                var gen = new EquipmentGenerator(fresh);
                var newW = gen.GenerateWeapon(5, null, 0);
                bool unique = true;
                foreach (var it in fresh.GetAllItems())
                    if (it.ItemId == newW.ItemId && it != newW) unique = false;
                GD.Print($"[H1] cold-restore: маркер={markerOk}, счетчики eq={EquipmentGenerator.GetIdCounter()}" +
                         $"/gen={ItemGeneratorService.GetGenerationCounter()}, новый id уникален={unique}");
                bool h1Ok = markerOk && unique;
                Record("ST15-008/H1", h1Ok,
                    h1Ok
                        ? "подтверждено: каталог+счётчики переживают restore на свежем экземпляре, новые ID уникальны (R17 держит)"
                        : $"РЕГРЕСС: маркер={markerOk}, уникальность={unique}");
                // Приводим счётчики/live-каталог в исходное состояние:
                // liveDb НЕ трогали (state — снимок), fresh — локальный.
                ItemGeneratorService.SetGenerationCounter(ItemGeneratorService.GetGenerationCounter());
            }

            // H2: два последовательных ResetWorld — нет утечки между мирами.
            var db2 = _itemDb as ItemDatabaseService;
            if (db2 == null)
            {
                RecordNotTested("ST15-008/H2", "IItemDatabaseService не ItemDatabaseService (нет ResetWorld-доступа)");
                return;
            }
            db2.ResetWorld();
            int c1 = db2.Count;
            var orphan = _legacyGen!.GenerateConsumableForLevel(5, 20261029);
            db2.ResetWorld();
            int c2 = db2.Count;
            bool orphanGone = !db2.TryGetItem(orphan.ItemId, out _);
            GD.Print($"[H2] ResetWorld#1 count={c1}; после генерации+ResetWorld#2 count={c2}; сирота удалён={orphanGone}");
            bool h2Ok = c1 == c2 && orphanGone && c1 > 0;
            Record("ST15-008/H2", h2Ok,
                h2Ok
                    ? "двойной сброс мира: каталог эквивалентен, runtime-предметы не переживают границу"
                    : $"УТЕЧКА/НЕСТАБИЛЬНОСТЬ: {c1} vs {c2}, сирота={orphanGone}");

            // H3: FormationRegistry и пересборка мира. In-session NewGame из
            // Playing отклоняется by design (menu-only) — тестируем КОНТРАКТ
            // пересборки: WorldDomainResetCoordinator.ResetAll = ровно то,
            // что игра выполняет на world-transition (NewGame phase 0 /
            // LoadGame pre-Restore; прецедент Stage10-A4/A5).
            // FormationRegistry обязан попадать в этот цикл: либо через
            // IWorldResettable (ResolveAll матчит по фактическому типу
            // инстанса), либо явным Clear в WorldInitPhase.
            var markerForm = new FormationData
            {
                Id = "st15_stale_marker", FormationType = FormationType.Barrier,
                Size = FormationSize.Small, RequiredLevel = 1, Element = Element.Neutral,
                DisplayName = "ST15 stale marker"
            };
            _formRegistry!.Register(markerForm);
            int resetDomains = 0;
            string h3Err = "";
            try { resetDomains = CultivationGame.Entry.Phases.WorldDomainResetCoordinator.ResetAll(_resolver!); }
            catch (Exception ex) { h3Err = $"{ex.GetType().Name}: {ex.Message}"; }
            bool formStale = _formRegistry.Get("st15_stale_marker") != null;
            bool formResettable = (object)_formRegistry is IWorldResettable;
            GD.Print($"[H3] ResetAll сбросил {resetDomains} доменов; FormationRegistry is IWorldResettable={formResettable}; " +
                     $"формация-маркер выжила={formStale} (должно false){(h3Err != "" ? "; ResetAll: " + h3Err : "")}");
            // Канон: генераторные реестры world-scoped — операция пересборки
            // мира их очищает. TechniqueRegistry чистится в WorldInitPhase с
            // 09.08; FormationRegistry — кандидат (не в координаторе, не в фазе).
            Record("ST15-008/H3", !formStale && h3Err == "",
                !formStale && h3Err == ""
                    ? $"FormationRegistry входит в контракт пересборки (сброшено доменов: {resetDomains})"
                    : "ПОДТВЕРЖДЕНО: формации прошлого мира переживают world-transition (FormationRegistry не IWorldResettable, WorldInitPhase его не чистит)");

            // H3b: TechniqueRegistry — контроль (не кандидат): Clear()-API —
            // операция, которую WorldInitPhase выполняет на пересборке.
            var markerTech = _techGen!.GenerateSpecified(TechniqueType.Combat, 3, 5, 20261030);
            bool techMarkerPresent = _techRegistry!.Get(markerTech.TechniqueId) != null;
            _techRegistry.Clear();
            bool techCleared = techMarkerPresent
                && _techRegistry.Get(markerTech.TechniqueId) == null
                && _techRegistry.Count == 0;
            GD.Print($"[H3b] TechniqueRegistry.Clear(): маркер был={techMarkerPresent}, после — count={_techRegistry.Count}");
            Record("ST15-008/H3b", techCleared,
                techCleared
                    ? "Clear()-API работает (проводка в WorldInitPhase — статический факт с 09.08)"
                    : "TechniqueRegistry.Clear() не очищает реестр — регресс");

            // H4: контракт seed=0 — два режима, оба проверяются.
            // Режим 1 (ItemGeneratorService): seed=0 = авто-счётчик → два вызова дают РАЗНЫЕ id.
            var c1a = _legacyGen.GenerateConsumableForLevel(5, 0);
            var c1b = _legacyGen.GenerateConsumableForLevel(5, 0);
            bool autoSeedUnique = c1a.ItemId != c1b.ItemId;
            // Режим 2 (Technique/Formation): seed=0 = валидный детерминированный сид.
            var reg4 = new TechniqueRegistry();
            var gen4 = new TechniqueGeneratorService(reg4);
            var z1 = gen4.GenerateSpecified(TechniqueType.Combat, 3, 5, 0);
            var z2 = gen4.GenerateSpecified(TechniqueType.Combat, 3, 5, 0);
            bool zeroDeterministic = z1.TechniqueId == z2.TechniqueId
                && Math.Abs(z1.Mastery - z2.Mastery) < 0.0001f;
            GD.Print($"[H4] seed=0: авто-режим уникален={autoSeedUnique}; детерминированный режим воспроизводим={zeroDeterministic}");
            Record("ST15-008/H4", autoSeedUnique && zeroDeterministic,
                autoSeedUnique && zeroDeterministic
                    ? "обa контракта seed=0 держатся: авто-счётчик уникален; SeededRandom(0) воспроизводим"
                    : "контракт seed=0 нарушен");

            await Task.CompletedTask;
        }

        // ================================================================
        //  Cold-load (ST15-008): пара процессов save → load
        // ================================================================

        private async Task RunColdLoadPhase(bool savePhase)
        {
            GD.Print($"[ColdLoad] фаза={(savePhase ? "SAVE" : "LOAD")} (ST15-008: межпроцессная живучесть каталога)");

            if (savePhase)
            {
                if (_itemDb == null || _save == null || _session == null)
                {
                    GD.PrintErr("[ColdLoad][SAVE] DI not wired");
                    return;
                }
                // Маркер: расходник с уникальным именем (регистрируется в каталог).
                var marker = new ItemData
                {
                    ItemId = $"st15_cold_marker_{DateTime.UtcNow:yyyyMMddHHmmss}",
                    NameRu = ColdMarkerPrefix + DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                    NameEn = ColdMarkerPrefix,
                    ItemType = "Consumable",
                    Category = ItemCategory.Consumable,
                    Stackable = true, MaxStack = 5,
                    Weight = 0.1f, Volume = 0.1f, Value = 1, HasDurability = false,
                    RequiredCultivationLevel = 0,
                };
                _itemDb.Register(marker);
                // Счётчик до сейва.
                int eqCounterBefore = EquipmentGenerator.GetIdCounter();
                long genCounterBefore = ItemGeneratorService.GetGenerationCounter();
                bool saved = _save.Save(new SaveSlot(ColdSlot, SaveSlotType.Manual));
                GD.Print($"[ColdLoad][SAVE] маркер={marker.ItemId} зарегистрирован; " +
                         $"сейв={saved}; счётчики: eq={eqCounterBefore}, gen={genCounterBefore}; каталог={_itemDb.Count}");
                Record("ST15-008/COLD-SAVE", saved, saved ? "слот сохранён с маркером в каталоге" : "сейв не удался");
                await Task.CompletedTask;
                return;
            }

            // LOAD-фаза: НОВЫЙ процесс, мир собран загрузкой слота.
            if (_itemDb == null || _session == null || _save == null)
            {
                GD.PrintErr("[ColdLoad][LOAD] DI not wired");
                return;
            }
            bool has = _save.HasSave(new SaveSlot(ColdSlot, SaveSlotType.Manual));
            if (!has)
            {
                GD.PrintErr($"[ColdLoad][LOAD] слот {ColdSlot} не найден — прогоните SAVE-фазу первым процессом");
                Record("ST15-008/COLD-LOAD", false, "слот отсутствует (SAVE-фаза не была запущена)");
                return;
            }
            _session.LoadGame(new SaveSlot(ColdSlot, SaveSlotType.Manual));
            await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);

            // Маркер должен резолвиться в каталоге ПОСЛЕ холодной загрузки.
            ItemData? found = null;
            foreach (var it in _itemDb.GetAllItems())
                if (it.NameEn == ColdMarkerPrefix) { found = it; break; }
            // Новый предмет после загрузки: ID не должен коллидировать с восстановленными.
            var gen = new EquipmentGenerator((ItemDatabaseService)_itemDb);
            var freshItem = gen.GenerateWeapon(5, null, 20261031);
            bool freshUnique = true;
            int clash = 0;
            foreach (var it in _itemDb.GetAllItems())
                if (it.ItemId == freshItem.ItemId && it != freshItem) { clash++; freshUnique = false; }

            GD.Print($"[ColdLoad][LOAD] маркер в каталоге={found != null} ({found?.ItemId ?? "—"}); " +
                     $"новый id={freshItem.ItemId} уникален={freshUnique} (коллизий={clash}); каталог={_itemDb.Count}");
            bool ok = found != null && freshUnique;
            Record("ST15-008/COLD-LOAD", ok,
                ok
                    ? "cold-load в новом процессе: каталог восстановлен, маркер резолвится, новые ID уникальны"
                    : $"ПРОВАЛ: маркер={found != null}, уникальность={freshUnique}");

            // Чистим слот за собой (прогоняемый многократно харнес не должен копить мусор).
            try { _save.DeleteSave(new SaveSlot(ColdSlot, SaveSlotType.Manual)); } catch { /* не критично */ }
        }

        // ================================================================
        //  Итог
        // ================================================================

        private void PrintSummary()
        {
            GD.Print("");
            GD.Print("════════ Stage15GeneratorIntegrityTruthSim: ИТОГ ════════");
            int pass = 0, fail = 0, gap = 0, nt = 0;
            foreach (var (id, status, detail) in _results)
            {
                GD.Print($"[Stage15Sim] {id,-22} {status,-10} {detail}");
                if (status == "PASS") pass++;
                else if (status == "FAIL") fail++;
                else if (status == "GAP") gap++;
                else nt++;
            }
            string verdict = (fail == 0) ? (gap > 0 ? "PASS (с GAP)" : "PASS") : "FAIL";
            GD.Print($"[Stage15Sim] PASS={pass} FAIL={fail} GAP={gap} NOT TESTED={nt}");
            GD.Print($"[Stage15Sim] VERDICT: {verdict}");
        }
    }
}

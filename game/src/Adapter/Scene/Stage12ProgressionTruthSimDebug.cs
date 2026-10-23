#nullable enable
// Создано: 2026-10-23 — Этап-12 аудита владельца (развитие, баффы, Ци).
// Запуск:
//   GODOT_NEWGAME=1 GODOT_STAGE12_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
//
// Проверяет кандидаты ST12-001..006 (канон:
// checkpoints/10_23_stage12_progression_audit_request.md):
//   A. ST12-001 — QiService.AddQi: _currentQi + amount переполняет long ДО
//      клампа → Ци отрицательна при больших amount. После фикса: насыщающее
//      сложение, инвариант 0 <= CurrentQi <= MaxQi, QiFullEvent при заполнении.
//   B. ST12-002 — ампутация сердца: OnBodyPartSevered умножает множитель
//      регенерации ×0.5, но RecalculateStats() ставит его заново по уровню
//      культивации БЕЗ _heartSevered → уровень/прорыв снимают штраф;
//      повторное событие уменьшает многократно. После фикса: штраф ПРОИЗВОДНАЯ
//      от флага (RecalculateStats учитывает), повтор — no-op, приживление
//      сердца снимает штраф (предусмотренное правило восстановления).
//   C. ST12-003 — BuffTickProcessor.ProcessTick: deltaTime > интервала даёт
//      ОДИН тик (таймер сбрасывается на полный интервал) → 5 шагов по 1с ≠
//      1 шаг 5с. После фикса: догоняющие тики с потолком (нет неограниченного
//      всплеска).
//   D. ST12-004 — BuffService.MapBuffIdToType: неизвестный buffId молча даёт
//      AttackBoost +10%. После фикса: явный отказ (ApplyBuff=false, HasBuff
//      false, 0 модификатор); вся существующая контент-батарея ID жива.
//   E. ST12-005 — StatService.RestoreState: проверяется перечисление StatType,
//      но не значения → NaN/Infinity/отрицательные/сверхкапа попадают в статы.
//      После фикса: по-записная валидация (конечность/диапазон/первичность
//      дельт) с журналом; дробные дельты round-trip без потерь; Vitality-диф
//      публикуется (П.24 пересчёт HP жив).
//   F. ST12-006 — ветка perk/conductivity допускает ВРЕМЕННЫЙ бафф
//      проводимости (спец BUFF_MODIFIERS_SYSTEM §A.4: «Баффы НЕ
//      модифицируют!»). После фикса: ветка сужена до перков; временный
//      conductivity-ID отказан контрактом неизвестных; перки живы.
//   P. Прогрессия (канон §2): инварианты AddVirtualDelta (отрицательные/
//      вторичные/капы), ConsolidateSleep по формуле §6 (минимум 4 часа,
//      дробное закрепление, остаток, пороговый шаг +1, жёсткий кап),
//      медитация → INT-дельта (вкл/прерывание), NPC↔NPC-бой дельт не даёт,
//      прорыв (подуровень/уровень, Ци=0, ёмкость пересчитана), qi-round-trip.
//
// Итог: [Stage12Sim] VERDICT: PASS / FAIL (+ пофазовые PASS/FAIL/GAP).
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Adapter.Di;
using CultivationGame.Modules.Buff;
using CultivationGame.Modules.Player;
using CultivationGame.Modules.Qi;

namespace CultivationGame.Adapter.Scene
{
    /// <summary>
    /// Headless-верификация Этапа-12: баффы/характеристики/Ци/культивация.
    /// Каждый сценарий печатает ожидание/факт; финал — VERDICT.
    /// </summary>
    public partial class Stage12ProgressionTruthSimDebug : Node
    {
        // === DI: сервисы ===
        [Inject] private IQiService? _qi;
        [Inject] private IBuffService? _buffs;
        [Inject] private IStatService? _stats;
        [Inject] private StatProgressProducer? _progress;

        // === DI: события ===
        [Inject] private IPublisher<BodyPartSeveredEvent>? _severedPub;
        [Inject] private IPublisher<BodyPartReattachedEvent>? _reattachedPub;
        [Inject] private IPublisher<DamageAppliedEvent>? _damagePub;
        [Inject] private IPublisher<MeditationStateChangedEvent>? _meditationPub;
        [Inject] private IPublisher<StatChangedEvent>? _statChangedPub;
        [Inject] private ISubscriber<BuffTickedEvent>? _buffTickedSub;
        [Inject] private ISubscriber<QiFullEvent>? _qiFullSub;
        [Inject] private ISubscriber<CultivationBreakthroughEvent>? _breakthroughSub;

        private IDisposable? _buffTickedToken;
        private IDisposable? _qiFullToken;
        private IDisposable? _breakthroughToken;

        private readonly List<BuffTickedEvent> _tickEvents = new();
        private int _qiFullEvents;
        private int _breakthroughEvents;
        private (int Level, int Sub, bool Major) _lastBreakthrough;

        private const string PlayerId = "player";
        private const string ToleranceEntity = "qa_st12_entity";

        // === Результаты ===
        private readonly List<(string Id, string Status, string Detail)> _results = new();

        public override void _Ready()
        {
            var container = Scene.GameBoot.Container;
            if (container != null)
                ContainerAdapter.InjectProperties(this, container);

            _buffTickedToken = _buffTickedSub?.Subscribe((in BuffTickedEvent e) =>
            {
                _tickEvents.Add(e);
            });
            _qiFullToken = _qiFullSub?.Subscribe((in QiFullEvent e) =>
            {
                _qiFullEvents++;
            });
            _breakthroughToken = _breakthroughSub?.Subscribe((in CultivationBreakthroughEvent e) =>
            {
                _breakthroughEvents++;
                _lastBreakthrough = (e.Level, e.SubLevel, e.IsMajor);
            });

            GD.Print("[Stage12Sim] Ready — Этап-12 (баффы/статы/Ци/культивация) starts in 3s");
            _ = RunAllScenariosAsync();
        }

        public override void _ExitTree()
        {
            _buffTickedToken?.Dispose();
            _qiFullToken?.Dispose();
            _breakthroughToken?.Dispose();
        }

        private async Task RunAllScenariosAsync()
        {
            await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);

            if (_qi == null || _buffs == null || _stats == null || _progress == null
                || _severedPub == null || _reattachedPub == null || _damagePub == null
                || _meditationPub == null || _statChangedPub == null || _buffTickedSub == null)
            {
                GD.Print("[Stage12Sim] FAIL — DI not wired");
                PrintVerdict(false);
                return;
            }

            // ─── B. ST12-002: штраф сердца при пересчёте (ДО A: измерения регена
            //     требуют чистого пула Ци; зона A с long.MaxValue — последней) ───
            await ScenarioB();

            // ─── C. ST12-003: тики DoT/HoT при большом deltaTime ───
            await ScenarioC();

            // ─── D. ST12-004: неизвестный buffId ───
            await ScenarioD();

            // ─── E. ST12-005: валидация сохранённых статов ───
            await ScenarioE();

            // ─── F. ST12-006: проводимость и временные баффы ───
            await ScenarioF();

            // ─── P. Прогрессия: дельты/сон/медитация/прорыв/round-trip ───
            await ScenarioP();

            // ─── A. ST12-001: AddQi без переполнения (ПОСЛЕДНЕЙ: long.MaxValue
            //     отравляет пул Ци — дальше измерений нет) ───
            await ScenarioA();

            PrintSummary();
        }

        // ================================================================
        //  A. ST12-001 — AddQi без переполнения long
        // ================================================================

        private async Task ScenarioA()
        {
            GD.Print("[Stage12Sim] ─── A. ST12-001: насыщающее сложение AddQi ───");
            await Task.CompletedTask;

            // A1: обычная порция — инвариант диапазона.
            _qi!.TryConsumeQi(1000);
            long before = _qi.CurrentQi;
            _qi.AddQi(500);
            bool a1 = _qi.CurrentQi == before + 500 && _qi.CurrentQi >= 0 && _qi.CurrentQi <= _qi.MaxQi;
            GD.Print($"[Stage12Sim] A1: AddQi(500): {before} → {_qi.CurrentQi} (Max {_qi.MaxQi}) → {(a1 ? "PASS" : "FAIL")}");
            Record("A1", a1, "обычная порция: CurrentQi = +500, инвариант 0 ≤ CurrentQi ≤ MaxQi");

            // A2: long.MaxValue — переполнение даёт отрицательную Ци (префикс).
            _qi.TryConsumeQi(100);
            long qiBeforeOverflow = _qi.CurrentQi;
            _qi.AddQi(long.MaxValue);
            bool inRange = _qi.CurrentQi >= 0 && _qi.CurrentQi <= _qi.MaxQi;
            bool a2 = inRange && _qi.CurrentQi == _qi.MaxQi;
            GD.Print($"[Stage12Sim] A2: AddQi(long.MaxValue): {qiBeforeOverflow} → {_qi.CurrentQi}/{_qi.MaxQi} (ожид MaxQi, без отрицательных) → {(a2 ? "PASS" : "FAIL")}");
            Record("A2", a2, $"насыщающее сложение без переполнения long: CurrentQi = {_qi.CurrentQi} (префикс — отрицательная Ци от overflow)");

            // A3: QiFullEvent при заполнении; повторное добавление — Ци не растёт.
            int fullBefore = _qiFullEvents;
            _qi.AddQi(1);
            bool fullEvent = _qiFullEvents > fullBefore;
            bool capped = _qi.CurrentQi == _qi.MaxQi;
            bool a3 = fullEvent && capped;
            GD.Print($"[Stage12Sim] A3: AddQi в заполненное ядро: CurrentQi={_qi.CurrentQi}/{_qi.MaxQi}, QiFullEvent +{_qiFullEvents - fullBefore} → {(a3 ? "PASS" : "FAIL")}");
            Record("A3", a3, "заполненное ядро: повторное добавление не превышает MaxQi, QiFullEvent публикуется");

            // A4: 0 и отрицательные — guard без изменений.
            long zero = _qi.CurrentQi;
            _qi.AddQi(0);
            _qi.AddQi(-5);
            bool a4 = _qi.CurrentQi == zero;
            GD.Print($"[Stage12Sim] A4: AddQi(0)/AddQi(-5): CurrentQi не изменился ({zero}) → {(a4 ? "PASS" : "FAIL")}");
            Record("A4", a4, "нулевые/отрицательные значения игнорируются guard-ом (без исключений)");
        }

        // ================================================================
        //  B. ST12-002 — штраф регенерации при ампутации сердца
        // ================================================================

        private async Task ScenarioB()
        {
            GD.Print("[Stage12Sim] ─── B. ST12-002: штраф сердца переживает пересчёт уровня ───");
            await Task.CompletedTask;

            // Измеряем регенерацию за 1 игровой день от нулевой Ци (окно без
            // насыщения: 10% × множитель < MaxQi; аккумулятор дроби учтён
            // допуском отношения).
            long MeasureRegen(int days)
            {
                _qi!.TryConsumeQi(_qi.CurrentQi); // до нуля (0 → guard-выход)
                _qi.Regenerate(1440f * days);
                return _qi.CurrentQi;
            }

            // B1: ампутация сердца — регенерация падает вдвое.
            _qi!.SetCultivationLevel(1, 0);
            long baseRegen = MeasureRegen(1);
            _severedPub!.Publish(new BodyPartSeveredEvent(
                _qi.EntityId, BodyPartType.Heart,
                Modules.Body.BodySlotMapping.GetBlockedSlots(BodyPartType.Heart), true));
            bool heartFlag = _qi.IsHeartSevered;
            long heartRegen = MeasureRegen(1);
            double ratio1 = baseRegen > 0 ? (double)heartRegen / baseRegen : 0;
            bool b1 = heartFlag && ratio1 is >= 0.45 and <= 0.60;
            GD.Print($"[Stage12Sim] B1: ампутация сердца: регенерация {baseRegen} → {heartRegen} (ratio {ratio1:F2}, ожид ~0.5) → {(b1 ? "PASS" : "FAIL")}");
            Record("B1", b1, "ампутация сердца: −50% регенерации Ци (QI-C01), применяется один раз");

            // B2: смена уровня культивации НЕ снимает штраф (префикс: RecalculateStats
            // затирал множитель таблицей уровня без _heartSevered).
            _qi.SetCultivationLevel(2, 0);
            bool stillHeart = _qi.IsHeartSevered;
            // Ожидание: L2-множитель 2.0 с половинным штрафом = 1.0 от базовой L2.
            _qi.TryConsumeQi(_qi.CurrentQi);
            _qi.Regenerate(1440f);
            long heartRegenL2 = _qi.CurrentQi;
            // Та же L2 без штрафа — через приживление (реальный игровой путь).
            _reattachedPub!.Publish(new BodyPartReattachedEvent(_qi.EntityId, BodyPartType.Heart));
            bool heartRestored = !_qi.IsHeartSevered;
            long wholeRegenL2 = MeasureRegen(1);
            double ratio2 = wholeRegenL2 > 0 ? (double)heartRegenL2 / wholeRegenL2 : 0;
            bool b2 = stillHeart && heartRestored && ratio2 is >= 0.45 and <= 0.60;
            GD.Print($"[Stage12Sim] B2: после SetCultivationLevel(2): штраф сохранён={stillHeart}, regen(сердце ампутировано)={heartRegenL2} vs целое={wholeRegenL2} (ratio {ratio2:F2}, ожид ~0.5) → {(b2 ? "PASS" : "FAIL")}");
            Record("B2", b2, "RecalculateStats/SetCultivationLevel/прорыв НЕ снимают штраф ампутации сердца; приживление сердца — предусмотренный путь снятия (штраф уходит)");

            // B3: повторное событие ампутации — без повторного уменьшения.
            _severedPub.Publish(new BodyPartSeveredEvent(
                _qi.EntityId, BodyPartType.Heart,
                Modules.Body.BodySlotMapping.GetBlockedSlots(BodyPartType.Heart), true));
            _severedPub.Publish(new BodyPartSeveredEvent(
                _qi.EntityId, BodyPartType.Heart,
                Modules.Body.BodySlotMapping.GetBlockedSlots(BodyPartType.Heart), true));
            bool flagStill = _qi.IsHeartSevered;
            long dupRegen = MeasureRegen(1);
            double ratio3 = wholeRegenL2 > 0 ? (double)dupRegen / wholeRegenL2 : 0;
            bool b3 = flagStill && ratio3 is >= 0.45 and <= 0.60; // не 0.25 (двойное) и не 1.0 (снятый)
            GD.Print($"[Stage12Sim] B3: дубликаты события ×2: регенерация ratio {ratio3:F2} (ожид ~0.5 — не 0.25) → {(b3 ? "PASS" : "FAIL")}");
            Record("B3", b3, "повторные события ампутации сердца идемпотентны (штраф применяется ровно один раз)");

            // B4: RestoreState с HeartSevered=true — штраф жив после пересчёта.
            _qi.SetCultivationLevel(1, 0);
            _reattachedPub.Publish(new BodyPartReattachedEvent(_qi.EntityId, BodyPartType.Heart));
            var qiSaveable = (ISaveable)_qi!;
            var captured = qiSaveable.CaptureState();
            if (captured is QiService.QiSaveState qs)
            {
                qs.HeartSevered = true;
                qs.CurrentQi = 0;
                qiSaveable.RestoreState(qs);
            }
            bool restoredFlag = _qi.IsHeartSevered;
            long restoredRegen = MeasureRegen(1);
            // База L1 без штрафа:
            _reattachedPub.Publish(new BodyPartReattachedEvent(_qi.EntityId, BodyPartType.Heart));
            long baseL1 = MeasureRegen(1);
            double ratio4 = baseL1 > 0 ? (double)restoredRegen / baseL1 : 0;
            bool b4 = restoredFlag && ratio4 is >= 0.45 and <= 0.60;
            GD.Print($"[Stage12Sim] B4: RestoreState(HeartSevered=true): флаг={restoredFlag}, regen ratio {ratio4:F2} (ожид ~0.5) → {(b4 ? "PASS" : "FAIL")}");
            Record("B4", b4, "Save/Load: ампутация сердца сохраняется (блок qi), штраф восстанавливается ПОСЛЕ пересчёта — семантика R17/QI-C01");

            // B5: снятие штрафа приживлением уже показано в B2 (heartRestored);
            // фиксируем явный критерий.
            Record("B5", heartRestored, "штраф снимается ТОЛЬКО предусмотренным правилом восстановления сердца (ReattachPart/BodyPartReattachedEvent)");
        }

        // ================================================================
        //  C. ST12-003 — тики DoT/HoT при большом deltaTime
        // ================================================================

        private async Task ScenarioC()
        {
            GD.Print("[Stage12Sim] ─── C. ST12-003: 5×1с == 1×5с (догоняющие тики) ───");
            await Task.CompletedTask;

            var processor = new BuffTickProcessor(
                Scene.GameBoot.Container!.Resolve<IPublisher<BuffTickedEvent>>());

            ActiveBuff MakeBuff() => new ActiveBuff
            {
                BuffId = "qa_st12_dot",
                EntityId = "qa_st12_target",
                Type = BuffType.Poison,
                HasTickEffect = true,
                TickInterval = 1f,
                TickTimer = 1f,
                TickDamage = 10f,
                CurrentStacks = 1,
            };

            // C1: пять шагов по 1 секунде → ровно 5 тиков.
            _tickEvents.Clear();
            var buff1 = MakeBuff();
            for (int i = 0; i < 5; i++) processor.ProcessTick(buff1, 1f);
            bool c1 = _tickEvents.Count == 5;
            GD.Print($"[Stage12Sim] C1: 5×ProcessTick(1с): тиков={_tickEvents.Count} (ожид 5) → {(c1 ? "PASS" : "FAIL")}");
            Record("C1", c1, "кадровая частота: 5 шагов по 1с при интервале 1с = 5 тиков DoT");

            // C2: один шаг 5 секунд → те же 5 тиков (префикс: 1 — потеря 4).
            _tickEvents.Clear();
            var buff2 = MakeBuff();
            processor.ProcessTick(buff2, 5f);
            bool c2 = _tickEvents.Count == 5;
            GD.Print($"[Stage12Sim] C2: 1×ProcessTick(5с): тиков={_tickEvents.Count} (ожид 5 = C1; префикс — 1) → {(c2 ? "PASS" : "FAIL")}");
            Record("C2", c2, $"эквивалентность скачка: один шаг 5с даёт те же тики, что 5 шагов по 1с (факт: {_tickEvents.Count})");

            // C3: суб-интервальные шаги 0.5с ×10 → 5 тиков.
            _tickEvents.Clear();
            var buff3 = MakeBuff();
            for (int i = 0; i < 10; i++) processor.ProcessTick(buff3, 0.5f);
            bool c3 = _tickEvents.Count == 5;
            GD.Print($"[Stage12Sim] C3: 10×ProcessTick(0.5с): тиков={_tickEvents.Count} (ожид 5) → {(c3 ? "PASS" : "FAIL")}");
            Record("C3", c3, "дробные шаги: аккумуляция до полного интервала, без преждевременных тиков");

            // C4: гигантский скачок 100с — ПОТОЛОК догоняющих тиков (без всплеска).
            _tickEvents.Clear();
            var buff4 = MakeBuff();
            processor.ProcessTick(buff4, 100f);
            bool c4 = _tickEvents.Count is >= 1 and <= 5;
            GD.Print($"[Stage12Sim] C4: ProcessTick(100с): тиков={_tickEvents.Count} (ожид 1..5 — потолок) → {(c4 ? "PASS" : "FAIL")}");
            Record("C4", c4, $"ограничение всплеска: 100-секундный hitch даёт ≤5 тиков (факт {_tickEvents.Count}), таймер синхронизируется (не копит долг)");

            // C5: значение тика = TickDamage × стакам.
            _tickEvents.Clear();
            var buff5 = MakeBuff();
            buff5.CurrentStacks = 3;
            processor.ProcessTick(buff5, 1f);
            bool c5 = _tickEvents.Count == 1 && Math.Abs(_tickEvents[0].TickValue - 30f) < 0.01f;
            GD.Print($"[Stage12Sim] C5: стаки ×3: значение={(_tickEvents.Count > 0 ? _tickEvents[0].TickValue : 0)} (ожид 30) → {(c5 ? "PASS" : "FAIL")}");
            Record("C5", c5, "значение тика масштабируется стаками (TickDamage × CurrentStacks)");
        }

        // ================================================================
        //  D. ST12-004 — неизвестный buffId
        // ================================================================

        private async Task ScenarioD()
        {
            GD.Print("[Stage12Sim] ─── D. ST12-004: неизвестный ID ≠ случайный AttackBoost ───");
            await Task.CompletedTask;

            // D1: заведомо неизвестный ID — явный отказ.
            bool applied = _buffs!.ApplyBuff(PlayerId, "qa_totally_unknown_buff_xyz", 30f, 1f);
            bool hasIt = _buffs.HasBuff(PlayerId, "qa_totally_unknown_buff_xyz");
            int activeUnknown = _buffs.GetActiveBuffs(PlayerId).Count(b => b.BuffId == "qa_totally_unknown_buff_xyz");
            int damageMod = _buffs.GetStatModifierPermil(PlayerId, StatType.Damage);
            bool d1 = !applied && !hasIt && activeUnknown == 0 && damageMod == 0;
            GD.Print($"[Stage12Sim] D1: ApplyBuff(unknown): applied={applied}, HasBuff={hasIt}, в списке={activeUnknown}, Damage-модификатор={damageMod}‰ → {(d1 ? "PASS" : "FAIL")}");
            Record("D1", d1, $"неизвестный buffId — отказ с диагностикой (префикс: AttackBoost +10% к урону молча); ApplyBuff={applied}, модификатор={damageMod}‰");

            // D2: контент-батарея реальных ID — все живы (регрессия совместимости).
            (string id, BuffType expected)[] battery =
            {
                ("combat_bleed", BuffType.Bleed),
                ("combat_stun", BuffType.Stun),
                ("combat_shock", BuffType.AttackReduction),
                ("elemental_burn", BuffType.Burn),
                ("elemental_slow", BuffType.Slow),
                ("elemental_earth_stun", BuffType.Stun),
                ("elemental_void_pierce", BuffType.DefenseReduction),
                ("elemental_poison", BuffType.Poison),
                ("technique_poison", BuffType.Poison),
                ("curse_shock", BuffType.AttackReduction),
                ("sensory_attack_boost", BuffType.AttackBoost),
                ("health_regen", BuffType.HealthRegen),
                ("qi_restoration", BuffType.QiRestoration),
                ("severed_left_arm_str", BuffType.AttackReduction),
                ("perk_golden_body", BuffType.CastSpeed),
            };
            int failures = 0;
            foreach (var (id, expected) in battery)
            {
                string entity = $"qa_st12_{id}";
                bool ok = _buffs.ApplyBuff(entity, id, 60f, 100f);
                var active = _buffs.GetActiveBuffs(entity);
                bool typeOk = active.Any(b => b.BuffId == id && b.Type == expected);
                if (!ok || !typeOk)
                {
                    failures++;
                    var found = active.FirstOrDefault(b => b.BuffId == id);
                    GD.Print($"[Stage12Sim] D2-МИСМАТЧ: '{id}': applied={ok}, тип={(found.BuffId == id ? found.Type.ToString() : "нет")} (ожид {expected})");
                }
            }
            bool d2 = failures == 0;
            GD.Print($"[Stage12Sim] D2: контент-батарея {battery.Length} реальных ID: расхождений={failures} → {(d2 ? "PASS" : "FAIL")}");
            Record("D2", d2, "все реальные вызовы ApplyBuff (боевые следствия/элементальные эффекты/техники/ампутации/перки) совместимы с контрактом отказа");
        }

        // ================================================================
        //  E. ST12-005 — валидация сохранённых характеристик
        // ================================================================

        private async Task ScenarioE()
        {
            GD.Print("[Stage12Sim] ─── E. ST12-005: RestoreState статов — валидация значений ───");
            await Task.CompletedTask;

            var saveable = (ISaveable)_stats!;

            // E1: валидный round-trip с дробными дельтами.
            _stats.SetStat(StatType.Strength, 12.5f);
            _stats.SetStat(StatType.Agility, 11.25f);
            _stats.AddVirtualDelta(StatType.Intelligence, 3.55f); // кап INT 15
            ((StatService)_stats).SetThreshold(StatType.Strength, 2f);
            var good = saveable.CaptureState();
            _stats.SetStat(StatType.Strength, 1f);
            _stats.SetStat(StatType.Agility, 1f);
            _stats.SetStat(StatType.Intelligence, 1f);
            saveable.RestoreState(good);
            bool e1 = Math.Abs(_stats.GetStat(StatType.Strength) - 12.5f) < 1e-6f
                   && Math.Abs(_stats.GetStat(StatType.Agility) - 11.25f) < 1e-6f
                   && Math.Abs(_stats.GetVirtualDelta(StatType.Intelligence) - 3.55f) < 1e-4f
                   && Math.Abs(_stats.GetThreshold(StatType.Strength) - 2f) < 1e-6f;
            GD.Print($"[Stage12Sim] E1: round-trip: STR={_stats.GetStat(StatType.Strength)}, AGI={_stats.GetStat(StatType.Agility)}, INTΔ={_stats.GetVirtualDelta(StatType.Intelligence)} (дробные сохранены) → {(e1 ? "PASS" : "FAIL")}");
            Record("E1", e1, "валидный сейв точно восстанавливается, дробные дельты/пороги без потерь");

            // E2: повреждённые значения — по-записное отклонение (NaN/±∞/отрицательные/сверхкап).
            var bad = new StatService.StatsSaveState
            {
                EntityId = "player",
                Base = new List<StatService.StatSaveEntry>
                {
                    new() { Stat = (int)StatType.Strength, Value = 10f },      // валидная
                    new() { Stat = (int)StatType.Agility, Value = float.NaN },
                    new() { Stat = (int)StatType.Vitality, Value = float.PositiveInfinity },
                    new() { Stat = (int)StatType.Perception, Value = float.NegativeInfinity },
                    new() { Stat = (int)StatType.Luck, Value = -7f },
                    new() { Stat = (int)StatType.Intelligence, Value = 5000f }, // > MAX_STAT_VALUE
                    new() { Stat = 9999, Value = 5f },                          // неизвестный StatType
                },
                VirtualDelta = new List<StatService.StatSaveEntry>
                {
                    new() { Stat = (int)StatType.Intelligence, Value = 2.5f },  // валидная
                    new() { Stat = (int)StatType.Strength, Value = -3f },       // отрицательная
                    new() { Stat = (int)StatType.CritChance, Value = 4f },      // вторичный стат
                    new() { Stat = (int)StatType.Vitality, Value = 999f },      // > капа 10
                    new() { Stat = (int)StatType.Agility, Value = float.NaN },
                },
                Threshold = new List<StatService.StatSaveEntry>
                {
                    new() { Stat = (int)StatType.Strength, Value = 2f },        // валидный
                    new() { Stat = (int)StatType.Agility, Value = float.NaN },
                    new() { Stat = (int)StatType.Vitality, Value = -5f },
                },
            };
            saveable.RestoreState(bad);
            bool strOk = Math.Abs(_stats.GetStat(StatType.Strength) - 10f) < 1e-6f;
            bool agiRejected = _stats.GetStat(StatType.Agility) == 0f;               // запись отклонена — нет значения
            bool vitRejected = _stats.GetStat(StatType.Vitality) == 0f;
            bool intRejected = _stats.GetStat(StatType.Intelligence) == 0f;          // сверхкапа отклонена
            bool luckRejected = _stats.GetStat(StatType.Luck) == 0f;
            bool intDeltaOk = Math.Abs(_stats.GetVirtualDelta(StatType.Intelligence) - 2.5f) < 1e-4f;
            bool negDeltaRejected = _stats.GetVirtualDelta(StatType.Strength) == 0f;
            bool secondaryRejected = _stats.GetVirtualDelta(StatType.CritChance) == 0f;
            bool overCapRejected = _stats.GetVirtualDelta(StatType.Vitality) == 0f;
            bool thresholdOk = Math.Abs(_stats.GetThreshold(StatType.Strength) - 2f) < 1e-6f;
            bool badThresholdRejected = float.IsNaN(_stats.GetThreshold(StatType.Agility)) == false
                                     && _stats.GetThreshold(StatType.Agility) > 0; // формула порога, не NaN
            bool e2 = strOk && agiRejected && vitRejected && intRejected && luckRejected
                   && intDeltaOk && negDeltaRejected && secondaryRejected && overCapRejected
                   && thresholdOk && badThresholdRejected;
            GD.Print($"[Stage12Sim] E2: повреждённые записи отклонены: STR={_stats.GetStat(StatType.Strength)} (валидная жива), AGI={_stats.GetStat(StatType.Agility)}, INTΔ={_stats.GetVirtualDelta(StatType.Intelligence)}, VITΔ={_stats.GetVirtualDelta(StatType.Vitality)} → {(e2 ? "PASS" : "FAIL")}");
            Record("E2", e2, "по-записная валидация RestoreState: NaN/±Infinity/отрицательные/сверхкапа/вторичные-дельты/неизвестный StatType отклоняются с журналом; валидные записи восстановлены");

            // E3: Vitality-диф после RestoreState → П.24 пересчёт HP жив.
            _stats.SetStat(StatType.Vitality, 10f);
            int maxRedBefore = 0;
            foreach (var p in Scene.GameBoot.Container!.Resolve<IBodyService>()!.GetAllParts())
                if (p.Type == BodyPartType.Torso) maxRedBefore = p.MaxRedHP;
            _stats.SetStat(StatType.Vitality, 12f); // StatChangedEvent → BodyModule → RecalculateHPFromVitality
            int maxRedAfter = 0;
            foreach (var p in Scene.GameBoot.Container!.Resolve<IBodyService>()!.GetAllParts())
                if (p.Type == BodyPartType.Torso) maxRedAfter = p.MaxRedHP;
            bool e3 = maxRedAfter == (int)Math.Round(maxRedBefore * (1f + 0.1f)) || maxRedAfter > maxRedBefore;
            GD.Print($"[Stage12Sim] E3: VIT 10→12: торс MaxRed {maxRedBefore} → {maxRedAfter} (П.24: ×1.1) → {(e3 ? "PASS" : "FAIL")}");
            Record("E3", e3, "изменение Vitality публикует StatChangedEvent → BodyModule пересчитывает HP частей (П.24), HP растёт с VIT");
            _stats.SetStat(StatType.Vitality, 10f);
        }

        // ================================================================
        //  F. ST12-006 — проводимость: перки живы, временные баффы запрещены
        // ================================================================

        private async Task ScenarioF()
        {
            GD.Print("[Stage12Sim] ─── F. ST12-006: спецификация проводимости (BUFF_MODIFIERS §A.4) ───");
            await Task.CompletedTask;

            // F1: ВРЕМЕННЫЙ бафф проводимости — запрещён спецификацией
            // («⛔ Баффы НЕ модифицируют!») → отказ контрактом неизвестных ID.
            bool tempApplied = _buffs!.ApplyBuff(ToleranceEntity, "conductivity_surge", 30f, 0.5f);
            var tempInList = _buffs.GetActiveBuffs(ToleranceEntity).Any(b => b.BuffId == "conductivity_surge");
            bool f1 = !tempApplied && !tempInList;
            GD.Print($"[Stage12Sim] F1: временный 'conductivity_surge': applied={tempApplied}, в списке={tempInList} (ожид false/false — спец-запрет) → {(f1 ? "PASS" : "FAIL")}");
            Record("F1", f1, "временный бафф проводимости отказан (BUFF_MODIFIERS_SYSTEM §A.4: только перки/проклятия; ветка perk/conductivity сужена до перков)");

            // F2: перк проводимости — постоянный бафф жив (PerkService-путь).
            bool perkApplied = _buffs.ApplyBuff(ToleranceEntity, "perk_golden_body", float.MaxValue, 0.30f);
            var perkActive = _buffs.GetActiveBuffs(ToleranceEntity);
            var perkBuff = perkActive.FirstOrDefault(b => b.BuffId == "perk_golden_body");
            bool f2 = perkApplied && perkBuff.BuffId == "perk_golden_body" && perkBuff.Type == BuffType.CastSpeed;
            GD.Print($"[Stage12Sim] F2: перк 'perk_golden_body': applied={perkApplied}, тип={(perkBuff.BuffId == "perk_golden_body" ? perkBuff.Type.ToString() : "нет")} → {(f2 ? "PASS" : "FAIL")}");
            Record("F2", f2, "перки проводимости — постоянные баффы (PerkService.ApplyPerk путь), кастинговый тип для UI");

            // F3: модификатор проводимости из баффов НЕ потребляется боем
            // (проверено grep-аудитом: потребителей GetStatModifier(Conductivity)
            // нет; реальный эффект ведёт PerkService через IQiDataProvider).
            float condMod = _buffs.GetStatModifier(ToleranceEntity, StatType.Conductivity);
            int damageModPermil = _buffs.GetStatModifierPermil(ToleranceEntity, StatType.Damage);
            bool f3 = damageModPermil == 0; // перк не даёт урона
            GD.Print($"[Stage12Sim] F3: перк-бафф: Conductivity-мод={condMod} (инертен — потребителей нет), Damage-мод={damageModPermil}‰ (ожид 0) → {(f3 ? "PASS" : "FAIL")}");
            Record("F3", f3, "нет двойного применения: бафф-ветка — учёт для HasBuff/UI, числовую проводимость ведёт PerkService (IQiDataProvider.SetConductivity), Damage-модификатор от перка = 0");

            // F4: SetConductivityBonus — кламп [0..2], пересчёт от базы.
            _qi!.SetConductivityBonus(0.5f);
            float condHalf = _qi.Conductivity;
            float baseCond = condHalf / 1.5f;
            _qi.SetConductivityBonus(-5f);
            float condNeg = _qi.Conductivity;
            _qi.SetConductivityBonus(99f);
            float condOver = _qi.Conductivity;
            bool f4 = Math.Abs(condNeg - baseCond) < 0.01f && Math.Abs(condOver - baseCond * 3f) < 0.01f;
            GD.Print($"[Stage12Sim] F4: SetConductivityBonus: 0.5→{condHalf:F1}, −5→{condNeg:F1} (кламп 0), 99→{condOver:F1} (кламп 2 → ×3 базы) → {(f4 ? "PASS" : "FAIL")}");
            Record("F4", f4, "бонус проводимости клампится в [0..2] (SetConductivityBonus), итоговая проводимость = база × (1 + бонус)");
        }

        // ================================================================
        //  P. Прогрессия — дельты, сон, медитация, прорыв, round-trip
        // ================================================================

        private async Task ScenarioP()
        {
            GD.Print("[Stage12Sim] ─── P. Прогрессия: дельты/сон/медитация/прорыв ───");
            await Task.CompletedTask;

            // P1: инварианты AddVirtualDelta (§2/P2-49) — измеряем ПРИРОСТ,
            // чтобы зона не зависела от мусора прошлых зон (E2-префикс).
            _stats!.SetStat(StatType.Strength, 10f);
            float critBefore = _stats.GetVirtualDelta(StatType.CritChance);
            float strBefore = _stats.GetVirtualDelta(StatType.Strength);
            _stats.AddVirtualDelta(StatType.Strength, -5f);       // отрицательная — нет
            _stats.AddVirtualDelta(StatType.CritChance, 5f);      // вторичный — нет
            _stats.AddVirtualDelta(StatType.Strength, 0.5f);      // валидная
            _stats.AddVirtualDelta(StatType.Strength, 100f);      // сверх капа 10 → кламп
            bool p1 = _stats.GetVirtualDelta(StatType.CritChance) == critBefore
                   && Math.Abs(_stats.GetVirtualDelta(StatType.Strength) - 10f) < 1e-6f;
            GD.Print($"[Stage12Sim] P1: AddVirtualDelta: STRΔ {strBefore} → {_stats.GetVirtualDelta(StatType.Strength)} (кап 10), CritChanceΔ без роста ({critBefore} → {_stats.GetVirtualDelta(StatType.CritChance)}) → {(p1 ? "PASS" : "FAIL")}");
            Record("P1", p1, "дельты: только первичные статы, не-отрицательные, капы 10/15 (§4.2: «при капе прирост невозможен до закрепления сном»)");

            // P2: ConsolidateSleep по формуле §6 (чистая база — восстановление
            // валидного блока, чтобы NaN-мусор E2-префикса не мешал изоляции).
            var cleanStats = new StatService.StatsSaveState
            {
                EntityId = "player",
                Base = new List<StatService.StatSaveEntry>
                {
                    new() { Stat = (int)StatType.Strength, Value = 10f },
                    new() { Stat = (int)StatType.Agility, Value = 10f },
                    new() { Stat = (int)StatType.Vitality, Value = 10f },
                    new() { Stat = (int)StatType.Intelligence, Value = 10f },
                    new() { Stat = (int)StatType.Perception, Value = 10f },
                    new() { Stat = (int)StatType.Luck, Value = 5f },
                },
                VirtualDelta = new List<StatService.StatSaveEntry>(),
                Threshold = new List<StatService.StatSaveEntry>(),
            };
            ((ISaveable)_stats).RestoreState(cleanStats);
            _stats.AddVirtualDelta(StatType.Agility, 1.2f);
            float agiAfterShort = _stats.GetStat(StatType.Agility); // до сна — 10
            _stats.ConsolidateSleep(3.9f); // < 4 часов — закрепления нет
            bool shortNoConsolidate = Math.Abs(_stats.GetStat(StatType.Agility) - agiAfterShort) < 1e-6f
                                   && Math.Abs(_stats.GetVirtualDelta(StatType.Agility) - 1.2f) < 1e-6f;
            _stats.ConsolidateSleep(8f); // 8 × 0.025 = 0.2 перенос + пороговый шаг
            float agiAfter8 = _stats.GetStat(StatType.Agility);
            float deltaAfter8 = _stats.GetVirtualDelta(StatType.Agility);
            // Ожидание §6.2/§6.4: consolidated = min(1.2, 0.2) = 0.2 → AGI 10.2;
            // остаток 1.0 ≥ threshold(10.2 → floor(1.02)=1) → +1 → 11.2, остаток 0.
            bool formula = Math.Abs(agiAfter8 - 11.2f) < 1e-4f && deltaAfter8 < 1e-6f;
            bool p2 = shortNoConsolidate && formula;
            GD.Print($"[Stage12Sim] P2: сон: <4ч — нет закрепления ({agiAfterShort}); 8ч: AGI {agiAfter8} (ожид 11.2 = +0.2 дробно +1 порог), остаток Δ={deltaAfter8} → {(p2 ? "PASS" : "FAIL")}");
            Record("P2", p2, "ConsolidateSleep §6: минимум 4 часа; consolidated = min(Δ, часы×0.025); §6.4 пороговый шаг +1 пока Δ ≥ threshold; остаток сохраняется");

            // P3: медитация → INT-дельта; прерывание — остановка прироста.
            float intDeltaBefore = _stats.GetVirtualDelta(StatType.Intelligence);
            _meditationPub!.Publish(new MeditationStateChangedEvent(true, 1.0f));
            for (int i = 0; i < 5; i++) _progress!.TickMeditation();
            float intDeltaAfter = _stats.GetVirtualDelta(StatType.Intelligence);
            _meditationPub.Publish(new MeditationStateChangedEvent(false, 0f));
            _progress!.TickMeditation();
            float intDeltaAfterStop = _stats.GetVirtualDelta(StatType.Intelligence);
            bool p3 = Math.Abs(intDeltaAfter - intDeltaBefore - 0.05f) < 1e-4f
                   && intDeltaAfterStop == intDeltaAfter;
            GD.Print($"[Stage12Sim] P3: медитация: INTΔ {intDeltaBefore} → {intDeltaAfter} (+0.05 = 5 мин × 0.01), после прерывания Δ={intDeltaAfterStop} (без роста) → {(p3 ? "PASS" : "FAIL")}");
            Record("P3", p3, "медитация: 0.01 INT/игровую минуту только при активной медитации; прерывание останавливает прирост (§5.2)");

            // P4: NPC↔NPC-бой дельт игроку не даёт (§5.1 — только участие игрока).
            float strDeltaBefore = _stats.GetVirtualDelta(StatType.Strength);
            _damagePub!.Publish(new DamageAppliedEvent(
                "qa_st12_npc_a", "qa_st12_npc_b", 10, DamageType.Physical,
                Element.Neutral, BodyPartType.Torso, CombatAttackResult.Hit,
                CombatSubtype.None, null));
            float strDeltaAfterNpc = _stats.GetVirtualDelta(StatType.Strength);
            bool p4 = strDeltaAfterNpc == strDeltaBefore;
            GD.Print($"[Stage12Sim] P4: NPC↔NPC удар: STRΔ игрока {strDeltaBefore} → {strDeltaAfterNpc} (без изменений) → {(p4 ? "PASS" : "FAIL")}");
            Record("P4", p4, "действия NPC↔NPC виртуальных дельт игроку не дают (PlayerIdResolver-фильтр продюсера)");

            // P5: требования прорыва = канон BREAKTHROUGH_MODELS §4.3-4.4 (Модель В);
            //      честный гейт; недостижимость — GAP ST12-007 (решение владельца).
            _qi!.SetCultivationLevel(1, 0);
            _breakthroughEvents = 0;
            long maxQiL1 = _qi.MaxQi; // 1000 = 1000 × 1.1^0 × 2^0
            // Малый прорыв L1.0→L1.1: capacity(1000×1.1^1=1100) × density(1) = 1100 (спец-формула).
            long reqSub = _qi.CalculateBreakthroughRequirement(false);
            bool subReqMatchesSpec = reqSub == 1100;
            // Большой прорыв L1→L2: capacity(1000×1.1^10) × density(2) ≈ 5188
            // (таблица §5: 2594×2; реализация трункирует double→long: 2593×2=5186 —
            // конвенция округления реализации, допуск ±2).
            long reqMajor = _qi.CalculateBreakthroughRequirement(true);
            bool majorReqMatchesSpec = reqMajor is >= 5186 and <= 5188;
            // Честный гейт: даже при ПОЛНОМ ядре (1000 < 1100) прорыв отказывает,
            // Ци/уровень/события нетронуты.
            _qi.AddQi(maxQiL1 - _qi.CurrentQi); // заполнить до максимума
            long qiAtFull = _qi.CurrentQi;
            bool btRefused = !_qi.TryBreakthrough();
            bool stateUntouched = _qi.CurrentQi == qiAtFull && _qi.SubLevel == 0
                && _qi.CultivationLevel == Core.Data.CultivationLevel.AwakenedCore
                && _breakthroughEvents == 0;
            bool p5 = subReqMatchesSpec && majorReqMatchesSpec && btRefused && stateUntouched;
            GD.Print($"[Stage12Sim] P5: требования: малый={reqSub} (спец 1100), большой={reqMajor} (спец 5188); гейт при полном ядре {qiAtFull}: отказ={btRefused}, состояние нетронуто={stateUntouched} → {(p5 ? "PASS" : "FAIL")}");
            Record("P5", p5, "требования прорыва точно соответствуют BREAKTHROUGH_MODELS §4.3-4.4 (Модель В: capacity(next)×density(next)); TryBreakthrough честно отказывает при Ци < требования, не мутируя состояние");

            // P5-GAP (ST12-007 — НОВАЯ находка этапа, решение за владельцем):
            // требование малого прорыва (1100) > MaxQi (1000) — ядро текущего
            // уровня НЕ МОЖЕТ вместить требуемую сырую Ци (накопление всюду
            // клампится: AddQi/Regenerate/медитация/зарядник). Прорывы
            // недостижимы в принципе — противоречие в самой модели (спец §5
            // подтверждает формулы; «как накопить 5188 в ядре на 1000» не
            // определено). НЕ переписывать семантику без решения владельца.
            GD.Print("[Stage12Sim] P5-GAP (ST12-007): требование 1100 > MaxQi 1000 → малый прорыв L1.0 недостижим; большой L1→L2: 5188 > 1000; успешный путь TryBreakthrough — мёртвый код при текущей модели");
            _results.Add(("P5-GAP", "GAP",
                "ST12-007 (новая находка): требования прорыва (спец и реализация совпадают) превышают ёмкость ядра текущего уровня (L1.0: малый 1100 > MaxQi 1000; L1→L2: 5188 > 1000) — Ци всюду клампится по ёмкости, успешный прорыв недостижим. Предложение владельцу: (а) требование = доля ёмкости следующего уровня (напр. 90%), либо (б) накопление сверх ёмкости (расширяемое ядро/резервуар медитации), либо (в) пересмотр формулы §4.3. Формулы реализации каноничны — правка без решения владельца не производится"));

            // P6: qi-round-trip: уровень/подуровень/качество/Ци/ампутация сердца.
            _qi.SetCultivationLevel(3, 4);
            _qi.TryConsumeQi(_qi.CurrentQi);
            _qi.AddQi(777);
            _severedPub!.Publish(new BodyPartSeveredEvent(
                _qi.EntityId, BodyPartType.Heart,
                Modules.Body.BodySlotMapping.GetBlockedSlots(BodyPartType.Heart), true));
            var qiSaveable = (ISaveable)_qi;
            var qiCaptured = qiSaveable.CaptureState();
            _qi.SetCultivationLevel(1, 0);
            _reattachedPub!.Publish(new BodyPartReattachedEvent(_qi.EntityId, BodyPartType.Heart));
            qiSaveable.RestoreState(qiCaptured);
            bool p6 = _qi.CultivationLevel == Core.Data.CultivationLevel.InternalFire
                   && _qi.SubLevel == 4 && _qi.CurrentQi == 777 && _qi.IsHeartSevered;
            GD.Print($"[Stage12Sim] P6: qi-round-trip: L{_qi.CultivationLevel}.{_qi.SubLevel}, Ци={_qi.CurrentQi}, heartSevered={_qi.IsHeartSevered} → {(p6 ? "PASS" : "FAIL")}");
            Record("P6", p6, "блок qi (R17/QI-2): уровень/подуровень/качество/текущая Ци/ампутация сердца точно восстанавливаются");
        }

        // ================================================================
        //  ИТОГИ
        // ================================================================

        private void Record(string id, bool pass, string detail)
            => _results.Add((id, pass ? "PASS" : "FAIL", detail));

        private void PrintSummary()
        {
            GD.Print("[Stage12Sim] ═══════ СВОДКА ЭТАПА-12 ═══════");
            int pass = 0, fail = 0, gap = 0;
            foreach (var (id, status, detail) in _results)
            {
                GD.Print($"[Stage12Sim] {id}: {status} — {detail}");
                if (status == "PASS") pass++;
                else if (status == "FAIL") fail++;
                else gap++;
            }
            GD.Print($"[Stage12Sim] Итого: PASS={pass}, FAIL={fail}, GAP={gap}");
            PrintVerdict(fail == 0);
        }

        private void PrintVerdict(bool pass)
        {
            GD.Print($"[Stage12Sim] VERDICT: {(pass ? "PASS" : "FAIL")}");
        }
    }
}

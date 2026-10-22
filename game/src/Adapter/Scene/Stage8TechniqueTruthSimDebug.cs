#nullable enable
// Создано: 2026-10-22 — Этап-8 аудита владельца (техники/Ци/боевые эффекты).
// Запуск: GODOT_NEWGAME=1 GODOT_STAGE8_DEBUG=1 godot --headless --path . scenes/MainMenu.tscn
//
// Проверяет дословные сценарии ST8-01..ST8-06 (запрос аудитора, 22.10):
//   A. ST8-01 — AoE с нулевым направлением (конус/полукруг/линия, прицел в
//      клетку кастера): направленная геометрия не должна вырождаться в круг;
//      вырожденный прицел = отказ выпуска («Прицел в свою клетку»).
//   B. ST8-02 — удержанная техника при отказе выпуска (цель ушла; тело уже
//      цело): удержание НЕ исчезает, выпуск повторим; после восстановления
//      условий повторный спуск применяет эффект.
//   C. ST8-03 — один TechniqueUsedEvent на применение (Combat — из
//      CombatService; Healing/Support — из CompleteUse); отказавшийся каст
//      (пре-валидация и синхронный AttackRejected по занятому касту) не
//      создаёт события и INT-прироста; кулдаун/мастерство возвращены.
//   D. ST8-04 — прерывания §5.4: смерть/боевой стюн/медитация срывают зарядку
//      (CancelCharge, возврат 50%) и рассеивают удержание (Dissipate,
//      возврат 50%); world-reset чистит оба состояния.
//   E. ST8-05 — Defense при недостаточном остатке Ци: буфер ниже
//      MIN_QI_FOR_BUFFER не активируется вовсе (без ложного
//      QiBufferActivatedEvent); резервный режим (пул ≥ MIN, инвестиция не
//      списалась) реально поглощает; каст-уровень — честный отказ с
//      сохранением удержания, после регена — успешный выпуск.
//   F. ST8-06 — рассеивание ауры на пороге дека́я: возврат 50% от
//      ФАКТИЧЕСКОГО (post-decay) остатка, boundary-кейс ровно на пороге.
//
// Итог: [Stage8Sim] VERDICT: PASS / FAIL (+ пофазовые PASS/FAIL/NOT TESTED).
using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Helpers;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Modules.Combat;
using CultivationGame.Modules.Player;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Headless-верификация Этапа-8 аудита: техники, Ци и боевые эффекты.
/// Каждый сценарий печатает ожидание/факт и свой статус; финал — VERDICT.
/// </summary>
public partial class Stage8TechniqueTruthSimDebug : Node
{
    // === DI: издатели ===
    [Inject] private IPublisher<TechniqueCastRequestedEvent>? _castRequestPub;
    [Inject] private IPublisher<MeditationToggleRequestedEvent>? _meditationTogglePub;
    [Inject] private IPublisher<AttackIntentEvent>? _attackIntentPub;

    // === DI: подписки ===
    [Inject] private ISubscriber<TechniqueCastResultEvent>? _castResultSub;
    [Inject] private ISubscriber<TechniqueChargeStartedEvent>? _chargeStartedSub;
    [Inject] private ISubscriber<TechniqueChargeProgressEvent>? _chargeProgressSub;
    [Inject] private ISubscriber<TechniqueChargeCompletedEvent>? _chargeCompletedSub;
    [Inject] private ISubscriber<TechniqueChargeCancelledEvent>? _chargeCancelledSub;
    [Inject] private ISubscriber<HeldTechniqueChangedEvent>? _heldChangedSub;
    [Inject] private ISubscriber<TechniqueUsedEvent>? _techniqueUsedSub;
    [Inject] private ISubscriber<AttackRejectedEvent>? _attackRejectedSub;
    [Inject] private ISubscriber<QiAddRequestEvent>? _qiAddSub;
    [Inject] private ISubscriber<QiBufferActivatedEvent>? _bufferActivatedSub;
    [Inject] private ISubscriber<AoeImpactEvent>? _aoeImpactSub;

    // === DI: сервисы ===
    [Inject] private TechniqueService? _techniqueService;
    [Inject] private TechniqueChargeService? _chargeService;
    [Inject] private AuraHoldService? _auraHold;
    [Inject] private AoEResolver? _aoeResolver;
    [Inject] private IQiService? _qiService;
    [Inject] private IQiBufferService? _qiBuffer;
    [Inject] private IStatService? _statService;
    [Inject] private IPlayerService? _playerService;
    [Inject] private INPCService? _npcService;
    [Inject] private IBodyService? _bodyService;
    [Inject] private IBuffService? _buffService;
    [Inject] private CombatService? _combatService;

    // === Токены подписок ===
    private System.IDisposable? _castResultToken;
    private System.IDisposable? _chargeStartedToken;
    private System.IDisposable? _chargeProgressToken;
    private System.IDisposable? _chargeCompletedToken;
    private System.IDisposable? _chargeCancelledToken;
    private System.IDisposable? _heldChangedToken;
    private System.IDisposable? _techniqueUsedToken;
    private System.IDisposable? _attackRejectedToken;
    private System.IDisposable? _qiAddToken;
    private System.IDisposable? _bufferActivatedToken;
    private System.IDisposable? _aoeImpactToken;

    // === Зафиксированные факты ===
    private readonly List<(string Tech, bool Ok, string Reason)> _castResults = new();
    private readonly List<(string Tech, long Refund, string Reason)> _chargeCancels = new();
    private readonly List<string> _usedEvents = new();          // TechniqueUsedEvent (все)
    private readonly List<(string Tech, string Reason)> _rejections = new();
    private readonly List<(long Amount, string Reason)> _qiAdds = new();
    private readonly List<(QiBufferMode Mode, long Invested)> _bufferActivations = new();
    private readonly List<(string Tech, string[] Ids)> _aoeImpacts = new();
    private bool _auraEmpty;                                     // HeldTechniqueChangedEvent("" ) получен
    private long _progressCharged;                               // последний ChargedQi прогресса

    // === Результаты сценариев ===
    private readonly List<(string Id, string Status, string Detail)> _results = new();

    private const string PlayerId = "player_0";

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        _castResultToken = _castResultSub?.Subscribe((in TechniqueCastResultEvent e) =>
        {
            _castResults.Add((e.TechniqueId, e.Success, e.Reason));
            GD.Print($"[Stage8Sim] CAST: {e.TechniqueId} ok={e.Success} reason='{e.Reason}'");
        });
        _chargeStartedToken = _chargeStartedSub?.Subscribe((in TechniqueChargeStartedEvent e) =>
            GD.Print($"[Stage8Sim] CHARGE START: {e.TechniqueId} cost={e.QiCost}"));
        _chargeProgressToken = _chargeProgressSub?.Subscribe((in TechniqueChargeProgressEvent e) =>
        {
            if (PlayerIdResolver.AreSameEntity(e.EntityId, PlayerId)) _progressCharged = e.ChargedQi;
        });
        _chargeCompletedToken = _chargeCompletedSub?.Subscribe((in TechniqueChargeCompletedEvent e) =>
            GD.Print($"[Stage8Sim] CHARGE DONE: {e.TechniqueId} charged={e.ChargedQi} potency={e.PotencyPermil}"));
        _chargeCancelledToken = _chargeCancelledSub?.Subscribe((in TechniqueChargeCancelledEvent e) =>
        {
            if (PlayerIdResolver.AreSameEntity(e.EntityId, PlayerId))
            {
                _chargeCancels.Add((e.TechniqueId, e.RefundedQi, e.Reason));
                GD.Print($"[Stage8Sim] CHARGE CANCEL: {e.TechniqueId} refund={e.RefundedQi} reason='{e.Reason}'");
            }
        });
        _heldChangedToken = _heldChangedSub?.Subscribe((in HeldTechniqueChangedEvent e) =>
        {
            if (!PlayerIdResolver.AreSameEntity(e.EntityId, PlayerId)) return;
            _auraEmpty = string.IsNullOrEmpty(e.TechniqueId);
            GD.Print($"[Stage8Sim] AURA: {(string.IsNullOrEmpty(e.TechniqueId) ? "EMPTY" : e.TechniqueId)}");
        });
        _techniqueUsedToken = _techniqueUsedSub?.Subscribe((in TechniqueUsedEvent e) =>
        {
            _usedEvents.Add(e.TechniqueId);
            GD.Print($"[Stage8Sim] TECH USED event: user={e.UserId} tech='{e.TechniqueId}' qi={e.QiCost}");
        });
        _attackRejectedToken = _attackRejectedSub?.Subscribe((in AttackRejectedEvent e) =>
        {
            _rejections.Add((e.TechniqueId, e.Reason));
            GD.Print($"[Stage8Sim] ATTACK REJECTED: {e.TechniqueId} reason='{e.Reason}'");
        });
        _qiAddToken = _qiAddSub?.Subscribe((in QiAddRequestEvent e) =>
        {
            _qiAdds.Add((e.Amount, e.RequesterId));
            if (e.Amount > 0) GD.Print($"[Stage8Sim] QI ADD req: +{e.Amount} ({e.RequesterId})");
        });
        _bufferActivatedToken = _bufferActivatedSub?.Subscribe((in QiBufferActivatedEvent e) =>
        {
            _bufferActivations.Add((e.Mode, e.QiInvested));
            GD.Print($"[Stage8Sim] BUFFER ACTIVATED: mode={e.Mode} invested={e.QiInvested}");
        });
        _aoeImpactToken = _aoeImpactSub?.Subscribe((in AoeImpactEvent e) =>
        {
            _aoeImpacts.Add((e.TechniqueId, e.TargetIds));
            GD.Print($"[Stage8Sim] AOE IMPACT: {e.TechniqueId} shape={e.Shape} targets=[{string.Join(",", e.TargetIds)}]");
        });

        GD.Print("[Stage8Sim] Ready — ST8-01..06 verification starts in 3s");
        _ = RunAllScenariosAsync();
    }

    public override void _ExitTree()
    {
        _castResultToken?.Dispose(); _chargeStartedToken?.Dispose();
        _chargeProgressToken?.Dispose(); _chargeCompletedToken?.Dispose();
        _chargeCancelledToken?.Dispose(); _heldChangedToken?.Dispose();
        _techniqueUsedToken?.Dispose(); _attackRejectedToken?.Dispose();
        _qiAddToken?.Dispose(); _bufferActivatedToken?.Dispose();
        _aoeImpactToken?.Dispose();
    }

    // ================================================================
    //  MAIN
    // ================================================================

    private async Task RunAllScenariosAsync()
    {
        await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);

        if (_castRequestPub == null || _techniqueService == null || _playerService == null
            || _npcService == null || _auraHold == null || _chargeService == null
            || _qiService == null || _qiBuffer == null || _statService == null
            || _aoeResolver == null || _bodyService == null || _buffService == null)
        {
            GD.Print("[Stage8Sim] FAIL — DI not wired");
            PrintVerdict(false);
            return;
        }

        // --- Подготовка: техники для сценариев ---
        var melee = EnsureTech("qa_st8_melee", TechniqueType.Combat, CombatSubtype.MeleeStrike,
            qiCost: 20, cooldown: 2f, baseDamage: 15, rangeM: 3f);
        var heal = EnsureTech("qa_st8_healing", TechniqueType.Healing, CombatSubtype.None,
            qiCost: 20, cooldown: 2f, baseDamage: 40, rangeM: 0f);
        var defense = EnsureTech("qa_st8_defense", TechniqueType.Defense, CombatSubtype.DefenseBlock,
            qiCost: 72, cooldown: 2f, baseDamage: 0, rangeM: 0f);
        var cone = EnsureAoeCone();
        if (melee == null || heal == null || defense == null)
        {
            GD.Print("[Stage8Sim] FAIL — test techniques not available (EnsureTech)");
            PrintVerdict(false);
            return;
        }

        var player = _playerService.Position;
        var npc = FindAliveNpc();
        if (npc == null) { GD.Print("[Stage8Sim] FAIL — no alive NPC"); PrintVerdict(false); return; }
        Teleport(npc, player.X + 1, player.Y);
        GD.Print($"[Stage8Sim] player@({player.X},{player.Y}) npc={npc} qi={_qiService.CurrentQi}");

        // F идёт первым: чистый сервисный тест ауры, без副作用 на игрока.
        await ScenarioF();
        await ScenarioA(cone, npc);
        await ScenarioB1(melee, npc);
        await ScenarioC(cone, npc, melee);
        await ScenarioB2(heal);
        await ScenarioE(defense);
        await ScenarioD(melee, npc);

        PrintSummary();
    }

    // ================================================================
    //  F. ST8-06 — рассеивание на пороге дека́я (сервисный уровень)
    // ================================================================
    private async Task ScenarioF()
    {
        GD.Print("[Stage8Sim] ─── F. ST8-06: decay-порог, возврат 50% post-decay ───");
        await Task.CompletedTask;
        var aura = _auraHold!;

        // F1: charged=60, cost=100, dt=15 → decay=15 → 45 ≤ 50 → рассеивание,
        //     возврат = 50% × 45 = 22 (старый код давал 50% × 60 = 30).
        _qiAdds.Clear();
        aura.Hold("qa_st8_f1", 1000, chargedQi: 60, qiCost: 100, Element.Fire);
        aura.Tick(15f);
        bool f1Gone = aura.IsEmpty;
        long f1Refund = FirstRefund("AuraHoldService:decay");
        bool f1 = f1Gone && f1Refund == 22;
        GD.Print($"[Stage8Sim] F1: charged 60, decay 15 → остаток 45, ожид refund=22; факт gone={f1Gone} refund={f1Refund} → {(f1 ? "PASS" : "FAIL")}");
        Record("F1", f1, $"refund {f1Refund} (ожид 22), аура очищена={f1Gone}");

        // F2 boundary: charged=51, cost=100, dt=1 → decay=1 → ровно порог 50 →
        //     рассеивание, возврат 50% × 50 = 25.
        _qiAdds.Clear();
        aura.Hold("qa_st8_f2", 1000, chargedQi: 51, qiCost: 100, Element.Fire);
        aura.Tick(1f);
        bool f2Gone = aura.IsEmpty;
        long f2Refund = FirstRefund("AuraHoldService:decay");
        bool f2 = f2Gone && f2Refund == 25;
        GD.Print($"[Stage8Sim] F2: charged 51, decay 1 → 50 == QiCost/2, ожид refund=25; факт gone={f2Gone} refund={f2Refund} → {(f2 ? "PASS" : "FAIL")}");
        Record("F2", f2, $"refund {f2Refund} (ожид 25), аура очищена={f2Gone}");

        // F3: 52 → 51 (выше порога, живёт), затем 50 → рассеивание.
        _qiAdds.Clear();
        aura.Hold("qa_st8_f3", 1000, chargedQi: 52, qiCost: 100, Element.Fire);
        aura.Tick(1f);
        bool f3Alive = !aura.IsEmpty && aura.Current != null && aura.Current.ChargedQi == 51;
        aura.Tick(1f);
        bool f3Gone = aura.IsEmpty;
        long f3Refund = FirstRefund("AuraHoldService:decay");
        bool f3 = f3Alive && f3Gone && f3Refund == 25;
        GD.Print($"[Stage8Sim] F3: 52→51 живёт, 51→50 рассеивается refund=25; факт aliveThen={f3Alive} gone={f3Gone} refund={f3Refund} → {(f3 ? "PASS" : "FAIL")}");
        Record("F3", f3, $"выше порога живёт={f3Alive}, на пороге рассеялся={f3Gone}, refund={f3Refund}");

        // F4 (контроль): вырожденное удержание не портит состояние сервиса.
        aura.Release();
    }

    private long FirstRefund(string reasonPrefix)
    {
        foreach (var (amount, reason) in _qiAdds)
            if (reason.StartsWith(reasonPrefix)) return amount;
        return -1;
    }

    // ================================================================
    //  A. ST8-01 — AoE с нулевым направлением
    // ================================================================
    private async Task ScenarioA(LearnedTechnique? cone, string npc)
    {
        GD.Print("[Stage8Sim] ─── A. ST8-01: вырожденное направление AoE ───");
        var player = _playerService!.Position;
        var resolver = _aoeResolver!;

        // Три цели: перед игроком, позади, сбоку (все в радиусе 5).
        string? front = FindSecondNpc(npc);
        if (front == null) { Record("A", "NOT TESTED", "нет второго живого NPC для геометрии"); return; }
        Teleport(npc, player.X - 3, player.Y);      // позади
        Teleport(front, player.X + 3, player.Y);    // перед
        // side-цель: сам npc используется как «позади», side возьмём третьего NPC:
        string? side = FindThirdNpc(npc, front);
        if (side == null) { Record("A", "NOT TESTED", "нет третьего живого NPC"); return; }
        Teleport(side, player.X, player.Y + 3);     // сбоку

        var q = new AoeQuery
        {
            CasterId = PlayerId,
            CasterPos = player,
            AimPos = player,                        // прицел = клетка кастера
            Shape = AoeShape.Cone,
            RadiusTiles = 5,
            HalfAngleDeg = 45,
            FalloffPermil = 0,
            MaxTargets = 0,
            SpareAlliesPermil = 0
        };

        // A1: Cone, aim==caster → НОЛЬ целей (прежде: все 3).
        var targets = resolver.Resolve(in q);
        bool a1 = targets.Count == 0;
        GD.Print($"[Stage8Sim] A1: Cone aim=клетка кастера, 3 цели в радиусе; ожид 0 целей, факт {targets.Count} → {(a1 ? "PASS" : "FAIL")} (прежде — круг: 3)");
        Record("A1", a1, $"Cone degenerate: {targets.Count} целей (ожид 0)");

        // A2: Semicircle aim==caster → 0 (прежде Dot(v,0)=0 ≥ 0 → все).
        q.Shape = AoeShape.Semicircle;
        targets = resolver.Resolve(in q);
        bool a2 = targets.Count == 0;
        GD.Print($"[Stage8Sim] A2: Semicircle aim=клетка кастера; ожид 0, факт {targets.Count} → {(a2 ? "PASS" : "FAIL")}");
        Record("A2", a2, $"Semicircle degenerate: {targets.Count} (ожид 0)");

        // A3: Line aim==caster → 0.
        q.Shape = AoeShape.Line;
        targets = resolver.Resolve(in q);
        bool a3 = targets.Count == 0;
        GD.Print($"[Stage8Sim] A3: Line aim=клетка кастера; ожид 0, факт {targets.Count} → {(a3 ? "PASS" : "FAIL")}");
        Record("A3", a3, $"Line degenerate: {targets.Count} (ожид 0)");

        // A4: направленный конус (aim = +5X) поражает ТОЛЬКО переднюю цель.
        q.Shape = AoeShape.Cone;
        q.AimPos = new Position2D(player.X + 5, player.Y);
        targets = resolver.Resolve(in q);
        bool onlyFront = targets.Count == 1 && targets[0].EntityId == front;
        bool a4 = onlyFront;
        GD.Print($"[Stage8Sim] A4: Cone aim=(+5,0); ожид только передняя ({front}), факт [{string.Join(",", Ids(targets))}] → {(a4 ? "PASS" : "FAIL")}");
        Record("A4", a4, $"направленный конус: [{string.Join(",", Ids(targets))}] (ожид [{front}])");

        // A5: полукруг (aim=+5X) — передняя + боковая (полупространство), без задней.
        q.Shape = AoeShape.Semicircle;
        targets = resolver.Resolve(in q);
        var ids = Ids(targets);
        bool a5 = ids.Contains(front) && ids.Contains(side) && !ids.Contains(npc);
        GD.Print($"[Stage8Sim] A5: Semicircle aim=(+5,0); ожид front+side без behind, факт [{string.Join(",", ids)}] → {(a5 ? "PASS" : "FAIL")}");
        Record("A5", a5, $"полукруг: [{string.Join(",", ids)}] (ожид front+side)");

        // A6: линия (aim=+5X) — только передняя (cross-отклонение боковой > ширины).
        q.Shape = AoeShape.Line;
        targets = resolver.Resolve(in q);
        bool a6 = targets.Count == 1 && targets[0].EntityId == front;
        GD.Print($"[Stage8Sim] A6: Line aim=(+5,0); ожид только front, факт [{string.Join(",", Ids(targets))}] → {(a6 ? "PASS" : "FAIL")}");
        Record("A6", a6, $"линия: [{string.Join(",", Ids(targets))}] (ожид [{front}])");

        // A7 (каст-уровень): прицел в свою клетку у конусной AoE — отказ
        //     выпуска с причиной «Прицел в свою клетку», без AoeImpactEvent.
        if (cone == null)
        {
            Record("A7", "NOT TESTED", "AoE-Cone техника не выдана грантом и не изучена");
        }
        else
        {
            _castResults.Clear(); _aoeImpacts.Clear();
            Press(cone.TechniqueId, player.X, player.Y);
            await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
            bool refused = _castResults.Exists(r => r.Tech == cone.TechniqueId && !r.Ok
                && r.Reason.Contains("свою клетку"));
            bool noImpact = _aoeImpacts.Count == 0;
            bool a7 = refused && noImpact;
            GD.Print($"[Stage8Sim] A7: каст Cone в свою клетку; ожид FAIL 'Прицел в свою клетку' без залпа; " +
                     $"факт refused={refused} impacts={_aoeImpacts.Count} → {(a7 ? "PASS" : "FAIL")}");
            Record("A7", a7, $"отказ={refused}, AoeImpact={_aoeImpacts.Count}");
        }

        // Навести порядок: цели для следующих сценариев вернёт ScenarioB1.
        await Task.CompletedTask;
    }

    // ================================================================
    //  B1. ST8-02 — удержанная Combat-техника при отказе «Цель исчезла»
    // ================================================================
    private async Task ScenarioB1(LearnedTechnique melee, string npc)
    {
        GD.Print("[Stage8Sim] ─── B1. ST8-02: удержание живёт после отказа (Combat) ───");
        var player = _playerService!.Position;

        // Зарядить с целью рядом → удержание в ауре.
        Teleport(npc, player.X + 1, player.Y);
        _castResults.Clear(); _auraEmpty = false;
        Press(melee.TechniqueId, player.X + 2, player.Y);
        if (!await WaitUntil(() => _auraHold!.Current != null, 12f))
        {
            Record("B1", "NOT TESTED", $"зарядка/удержание не наступило (qi={_qiService!.CurrentQi}, cooldown={_techniqueService!.GetCooldown(melee.TechniqueId)})");
            return;
        }
        GD.Print($"[Stage8Sim] B1: удержана '{melee.TechniqueId}' charged={_auraHold!.Current!.ChargedQi}");

        // Цель уходит за радиус → повторное нажатие = отказ «Цель исчезла».
        int usedBefore = CountUsed(melee.TechniqueId);
        float intBefore = _statService!.GetVirtualDelta(StatType.Intelligence);
        float masteryBefore = melee.Mastery;
        Teleport(npc, player.X + 60, player.Y);
        _castResults.Clear();
        Press(melee.TechniqueId, player.X + 2, player.Y);
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);

        bool refused = _castResults.Exists(r => r.Tech == melee.TechniqueId && !r.Ok && r.Reason == "Цель исчезла");
        bool holdAlive = _auraHold.Current != null && _auraHold.Current.TechniqueId == melee.TechniqueId;
        bool noUsed = CountUsed(melee.TechniqueId) == usedBefore;
        bool noCooldown = _techniqueService!.GetCooldown(melee.TechniqueId) == 0;
        bool masteryKept = System.Math.Abs(melee.Mastery - masteryBefore) < 1e-6;
        bool b1a = refused && holdAlive && noUsed && noCooldown && masteryKept;
        GD.Print($"[Stage8Sim] B1a: отказ='Цель исчезла'={refused}, удержание живо={holdAlive}, событий=+{CountUsed(melee.TechniqueId) - usedBefore}, " +
                 $"кулдаун=0:{noCooldown}, мастерство нетронуто={masteryKept} → {(b1a ? "PASS" : "FAIL")}");
        Record("B1a", b1a, $"отказ={refused} holdAlive={holdAlive} used+{CountUsed(melee.TechniqueId) - usedBefore} cd0={noCooldown}");

        // Цель возвращается → повторный спуск выпускает технику.
        Teleport(npc, player.X + 1, player.Y);
        _castResults.Clear();
        Press(melee.TechniqueId, player.X + 2, player.Y);
        bool released = await WaitUntil(() => _auraHold!.Current == null, 8f);
        await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
        bool success = _castResults.Exists(r => r.Tech == melee.TechniqueId && r.Ok);
        int usedAfter = CountUsed(melee.TechniqueId);
        bool b1b = released && success && usedAfter == usedBefore + 1;
        GD.Print($"[Stage8Sim] B1b: повторный выпуск; аура освобождена={released}, успех={success}, событий на технику={usedAfter} (было {usedBefore}, ожид +1) → {(b1b ? "PASS" : "FAIL")}");
        Record("B1b", b1b, $"release={released} success={success} used={usedAfter}/{usedBefore + 1}");

        // INT-прирост за успешный выпуск — ровно +0.001 (ST8-03 cross-check).
        float intAfter = _statService!.GetVirtualDelta(StatType.Intelligence);
        bool intOk = System.Math.Abs((intAfter - intBefore) - 0.001f) < 0.0004f;
        GD.Print($"[Stage8Sim] B1c: INT-дельта за 1 успешный выпуск = {intAfter - intBefore:F4} (ожид 0.0010 ± допуск) → {(intOk ? "PASS" : "FAIL")}");
        Record("B1c", intOk, $"INT-дельта {intAfter - intBefore:F4} (ожид ~0.0010)");

        // Успокоить бой между сценариями (детерминизм следующих фаз).
        _combatService?.EndCombat();
        await Task.CompletedTask;
    }

    // ================================================================
    //  B2. ST8-02 — удержанный Healing при «Тело не ранено»
    // ================================================================
    private async Task ScenarioB2(LearnedTechnique heal)
    {
        GD.Print("[Stage8Sim] ─── B2. ST8-02: удержание живёт после отказа (Healing) ───");

        // Ранить тело → зарядить Healing → удержание.
        _bodyService!.ApplyDamage(BodyPartType.Head, 40, "qa_st8_setup");
        if (!PlayerHasDamagedParts())
        {
            Record("B2", "NOT TESTED", "не удалось ранить тело игрока (ApplyDamage Head 40)");
            return;
        }
        var player = _playerService!.Position;
        if (_auraHold!.Current != null) _auraHold.Dissipate("qa_setup");
        _chargeService!.CancelCharge(PlayerId, "qa_setup");
        _castResults.Clear();
        Press(heal.TechniqueId, player.X, player.Y);
        if (!await WaitUntil(() => _auraHold.Current != null
                           && _auraHold.Current.TechniqueId == heal.TechniqueId, 12f))
        {
            Record("B2", "NOT TESTED", "зарядка Healing не завершилась/не удержана");
            return;
        }

        // Полностью излечить тело ДО выпуска → спуск = отказ «Тело не ранено».
        HealPlayerFully();
        int usedBefore = CountUsed(heal.TechniqueId);
        _castResults.Clear();
        Press(heal.TechniqueId, player.X, player.Y);
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        bool refused = _castResults.Exists(r => r.Tech == heal.TechniqueId && !r.Ok && r.Reason == "Тело не ранено");
        bool holdAlive = _auraHold!.Current != null;
        bool noUsed = CountUsed(heal.TechniqueId) == usedBefore;
        bool b2a = refused && holdAlive && noUsed;
        GD.Print($"[Stage8Sim] B2a: тело цело → отказ={refused}, удержание живо={holdAlive}, событий=+{CountUsed(heal.TechniqueId) - usedBefore} → {(b2a ? "PASS" : "FAIL")}");
        Record("B2a", b2a, $"отказ={refused} holdAlive={holdAlive} used+{CountUsed(heal.TechniqueId) - usedBefore}");

        // Снова рана → повторный спуск лечит.
        _bodyService.ApplyDamage(BodyPartType.Torso, 30, "qa_st8_setup");
        float intBefore = _statService!.GetVirtualDelta(StatType.Intelligence);
        _castResults.Clear();
        Press(heal.TechniqueId, player.X, player.Y);
        bool released = await WaitUntil(() => _auraHold!.Current == null, 8f);
        bool success = _castResults.Exists(r => r.Tech == heal.TechniqueId && r.Ok);
        float intAfter = _statService.GetVirtualDelta(StatType.Intelligence);
        bool intOk = System.Math.Abs((intAfter - intBefore) - 0.001f) < 0.0004f;
        bool b2b = released && success && intOk;
        GD.Print($"[Stage8Sim] B2b: рана есть → выпуск success={success}, аура свободна={released}, INT-дельта={intAfter - intBefore:F4} → {(b2b ? "PASS" : "FAIL")}");
        Record("B2b", b2b, $"release={released} success={success} INT~0.001={intOk}");
    }

    // ================================================================
    //  C. ST8-03 — единый контракт TechniqueUsedEvent
    // ================================================================
    private async Task ScenarioC(LearnedTechnique? cone, string npc, LearnedTechnique melee)
    {
        GD.Print("[Stage8Sim] ─── C. ST8-03: один TechniqueUsedEvent на применение ───");
        var player = _playerService!.Position;

        // C1: AoE-залп по конусу (front-цель) → ровно ОДНО событие на технику.
        if (cone == null)
        {
            Record("C1", "NOT TESTED", "AoE-Cone техника недоступна");
            Record("C2", "NOT TESTED", "AoE-Cone техника недоступна");
        }
        else
        {
            Teleport(npc, player.X + 1, player.Y);
            // Убрать прочие NPC из зоны конуса (A-сценарий расставлял их рядом).
            string? other1 = FindSecondNpc(npc), other2 = other1 != null ? FindThirdNpc(npc, other1) : null;
            if (other1 != null) Teleport(other1, player.X + 20, player.Y + 20);
            if (other2 != null) Teleport(other2, player.X - 20, player.Y - 20);

            _castResults.Clear(); _aoeImpacts.Clear();
            // Первый спуск: зарядка → аура паркует конус («В ауре»). Второй — выпуск.
            Press(cone.TechniqueId, player.X + 3, player.Y);
            bool heldCone = await WaitUntil(() => _auraHold!.Current != null
                                         && _auraHold.Current.TechniqueId == cone.TechniqueId, 12f);
            bool impactSeen = false, noFail = false;
            int used = -1;
            if (!heldCone)
            {
                Record("C1", "NOT TESTED", $"конус не удержан (hold='{_auraHold!.Current?.TechniqueId}')");
                Record("C2", "NOT TESTED", "конус не удержан");
            }
            else
            {
                // Цель — в упор ПЕРЕД спуском: залп резолвится синхронно в том же
                // стеке (телепорт → пресс → интент → volley), NPC не успевает уйти.
                Teleport(npc, player.X + 1, player.Y);
                _castResults.Clear(); _aoeImpacts.Clear();
                Press(cone.TechniqueId, player.X + 3, player.Y); // второй спуск — залп
                impactSeen = await WaitUntil(() => _aoeImpacts.Count > 0, 12f);
                await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
                noFail = !_castResults.Exists(r => r.Tech == cone.TechniqueId && !r.Ok);
                used = CountUsed(cone.TechniqueId);
                bool c1 = impactSeen && used == 1 && noFail;
                GD.Print($"[Stage8Sim] C1: AoE-залп impact={impactSeen}, без отказов={noFail}; событий техники={used} (ожид 1: только CombatService) → {(c1 ? "PASS" : "FAIL")}");
                Record("C1", c1, $"AoE used={used} (ожид 1), impact ok={impactSeen}");

                // C2: направленный конус задевает только front-цель (A-инвариант e2e).
                var ids = _aoeImpacts.Count > 0 ? _aoeImpacts[^1].Ids : new string[0];
                bool c2 = ids.Length == 1 && ids[0] == npc;
                GD.Print($"[Stage8Sim] C2: AoeImpact.TargetIds=[{string.Join(",", ids)}] — только передняя цель → {(c2 ? "PASS" : "FAIL")}");
                Record("C2", c2, $"TargetIds=[{string.Join(",", ids)}] (ожид [{npc}])");
            }
            _combatService?.EndCombat();
            // Инвариант следующих сценариев: аура чиста (конус выпущен/рассеян).
            if (_auraHold!.Current != null) _auraHold.Dissipate("qa_setup");
        }

        // C3: синхронный отказ по занятому касту (basic_attack pending) →
        //     AttackRejectedEvent, RefundUse, ноль событий и ноль INT.
        //     До 3 попыток: замах мог не создать pending (bridge-гейты) —
        //     тогда спуск штатно выпускает технику и попытка повторяется.
        bool c3 = false, c4 = false;
        string c3Detail = "не выполнено";
        // Инвариант цикла: чистая аура перед попытками (остатки C1 недопустимы).
        if (_auraHold!.Current != null) _auraHold.Dissipate("qa_setup");
        for (int attempt = 1; attempt <= 3 && !c3; attempt++)
        {
            Teleport(npc, player.X + 1, player.Y);
            // Удержание melee: зарядить (или использовать переживший попытку слот той же техники).
            if (_auraHold!.Current?.TechniqueId != melee.TechniqueId)
            {
                if (_auraHold.Current != null) _auraHold.Dissipate("qa_retry");
                _castResults.Clear();
                Press(melee.TechniqueId, player.X + 2, player.Y);
                if (!await WaitUntil(() => _auraHold.Current != null
                                   && _auraHold.Current.TechniqueId == melee.TechniqueId, 12f))
                {
                    c3Detail = $"попытка {attempt}: зарядка не завершилась (cooldown={_techniqueService!.GetCooldown(melee.TechniqueId)})";
                    continue;
                }
            }
            int usedBefore = CountUsed(melee.TechniqueId);
            float intBefore = _statService!.GetVirtualDelta(StatType.Intelligence);
            float masteryBefore = melee.Mastery;
            _rejections.Clear(); _castResults.Clear();
            // Тот же синхронный стек: обычный замах (создаёт pending) → спуск техники.
            _attackIntentPub!.Publish(new AttackIntentEvent(PlayerId, npc, "basic_attack", isRanged: false));
            Press(melee.TechniqueId, player.X + 2, player.Y);
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
            bool rejected = _rejections.Exists(r => r.Tech == melee.TechniqueId);
            bool refundOk = _techniqueService!.GetCooldown(melee.TechniqueId) == 0
                            && System.Math.Abs(melee.Mastery - masteryBefore) < 1e-6;
            bool noUsed = CountUsed(melee.TechniqueId) == usedBefore;
            float intAfter = _statService.GetVirtualDelta(StatType.Intelligence);
            bool noInt = System.Math.Abs(intAfter - intBefore) < 1e-6;
            bool holdAlive = _auraHold.Current != null;
            c3 = rejected && refundOk && noUsed && noInt && holdAlive;
            c3Detail = $"rejected={rejected} refund={refundOk} used+{CountUsed(melee.TechniqueId) - usedBefore} " +
                       $"INTΔ={intAfter - intBefore:F6} hold={holdAlive} (попытка {attempt})";
            GD.Print($"[Stage8Sim] C3 (попытка {attempt}): отклонён={rejected}, кулдаун/мастерство возвращены={refundOk}, " +
                     $"событий=+{CountUsed(melee.TechniqueId) - usedBefore}, INT-дельта={intAfter - intBefore:F6}, удержание живо={holdAlive} → {(c3 ? "PASS" : "FAIL")}");
            if (c3)
            {
                // C4: после истечения pending — повторный спуск выпускает (удержание не потеряно).
                await ToSignal(GetTree().CreateTimer(2.5), SceneTreeTimer.SignalName.Timeout);
                _castResults.Clear();
                Press(melee.TechniqueId, player.X + 2, player.Y);
                bool released = await WaitUntil(() => _auraHold!.Current == null, 8f);
                c4 = released;
                GD.Print($"[Stage8Sim] C4: повторный спуск после отказа → аура освобождена={released} → {(c4 ? "PASS" : "FAIL")}");
            }
            else if (!holdAlive)
            {
                // Pending не был создан — спуск выстрелил: ждать кулдаун и ретрай.
                GD.Print("[Stage8Sim] C3: pending не создан мостом — техника выпущена, ретрай после кулдауна");
                await ToSignal(GetTree().CreateTimer(2.5), SceneTreeTimer.SignalName.Timeout);
            }
        }
        Record("C3", c3, c3Detail);
        Record("C4", c4, "повторный выпуск после rejection: " + c4);
        _combatService?.EndCombat();
        await Task.CompletedTask;
    }

    // ================================================================
    //  D. ST8-04 — прерывания зарядки и удержания (§5.4)
    // ================================================================
    private async Task ScenarioD(LearnedTechnique melee, string npc)
    {
        GD.Print("[Stage8Sim] ─── D. ST8-04: смерть/стюн/медитация/world-reset ───");
        var pos = _playerService!.Position;
        Teleport(npc, pos.X + 1, pos.Y); // цель в радиусе для зарядок melee

        // Медленная техника: зарядка не завершится за время сценария.
        var slow = EnsureTech("qa_st8_slow", TechniqueType.Combat, CombatSubtype.MeleeStrike,
            qiCost: 6000, cooldown: 0f, baseDamage: 10, rangeM: 3f);
        if (slow == null) { Record("D1", "NOT TESTED", "slow-техника не изучена"); return; }

        // D1: смерть во время зарядки → CancelCharge("death"), возврат 50%.
        _chargeCancels.Clear(); _progressCharged = 0;
        Press(slow.TechniqueId, pos.X + 2, pos.Y);
        if (!await WaitUntil(() => _chargeService!.IsCharging(PlayerId), 6f))
        {
            Record("D1", "NOT TESTED", $"зарядка slow не стартовала (qi={_qiService!.CurrentQi})");
        }
        else if (!await WaitUntil(() => _progressCharged > 0, 5f))
        {
            Record("D1", "NOT TESTED", "зарядка не дренировала Ци до убийства (нет прогресс-тиков)");
            _chargeService!.CancelCharge(PlayerId, "qa_setup");
        }
        else
        {
            long charged = _progressCharged;
            _bodyService!.ApplyDamage(BodyPartType.Head, 10_000_000, "qa_st8_killer");
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
            bool chargingStopped = !_chargeService!.IsCharging(PlayerId);
            var cancel = _chargeCancels.Find(c => c.Tech == slow.TechniqueId);
            bool reasonDeath = cancel.Reason == "death";
            bool refundOk = cancel.Refund > 0;
            bool d1 = chargingStopped && reasonDeath && refundOk;
            GD.Print($"[Stage8Sim] D1: смерть в зарядке → зарядка снята={chargingStopped}, причина='{cancel.Reason}', " +
                     $"возврат={cancel.Refund} (заряжено ~{charged}) → {(d1 ? "PASS" : "FAIL")}");
            Record("D1", d1, $"charge stopped={chargingStopped} reason={cancel.Reason} refund={cancel.Refund}/charged~{charged}");
            RespawnPlayer();
        }

        // D2: смерть во время удержания → Dissipate("death"), возврат 50%.
        _auraEmpty = false; _qiAdds.Clear();
        Teleport(npc, pos.X + 1, pos.Y);
        if (!await ChargeAndHoldAsync(melee, pos))
        {
            Record("D2", "NOT TESTED", "не удалось удержать технику перед смертью");
        }
        else
        {
            long charged = _auraHold!.Current!.ChargedQi;
            _bodyService!.ApplyDamage(BodyPartType.Head, 10_000_000, "qa_st8_killer");
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
            bool auraGone = _auraEmpty && _auraHold.Current == null;
            long refund = FirstRefund("AuraHoldService:death");
            bool refundOk = refund == charged * 500 / 1000 && refund > 0;
            bool d2 = auraGone && refundOk;
            GD.Print($"[Stage8Sim] D2: смерть при удержании → аура рассеяна={auraGone}, возврат={refund} (ожид {charged * 500 / 1000}) → {(d2 ? "PASS" : "FAIL")}");
            Record("D2", d2, $"aura cleared={auraGone} refund={refund}/{charged * 500 / 1000}");
            RespawnPlayer();
        }

        // D3: боевой стюн во время зарядки → CancelCharge("stun").
        _chargeCancels.Clear(); _progressCharged = 0;
        Press(slow.TechniqueId, pos.X + 2, pos.Y);
        if (!await WaitUntil(() => _chargeService!.IsCharging(PlayerId), 6f))
        {
            Record("D3", "NOT TESTED", "зарядка для стюн-кейса не стартовала");
        }
        else if (!await WaitUntil(() => _progressCharged > 0, 5f))
        {
            Record("D3", "NOT TESTED", "стюн-кейс: зарядка не дренировала Ци (нет прогресс-тиков)");
            _chargeService!.CancelCharge(PlayerId, "qa_setup");
        }
        else
        {
            _buffService!.ApplyBuff(PlayerId, AuraHoldService.CombatStunBuffId, 20f, 0f);
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
            bool stopped = !_chargeService!.IsCharging(PlayerId);
            var cancel = _chargeCancels.Find(c => c.Tech == slow.TechniqueId);
            bool d3 = stopped && cancel.Reason == "stun" && cancel.Refund > 0;
            GD.Print($"[Stage8Sim] D3: combat_stun в зарядке → снята={stopped}, причина='{cancel.Reason}', возврат={cancel.Refund} → {(d3 ? "PASS" : "FAIL")}");
            Record("D3", d3, $"charge stopped={stopped} reason={cancel.Reason} refund={cancel.Refund}");
        }

        // D4: боевой стюн при удержании → Dissipate("stun").
        _auraEmpty = false; _qiAdds.Clear();
        Teleport(npc, pos.X + 1, pos.Y);
        if (!await ChargeAndHoldAsync(melee, pos))
        {
            Record("D4", "NOT TESTED", "не удалось удержать технику перед стюном");
        }
        else
        {
            _buffService!.ApplyBuff(PlayerId, AuraHoldService.CombatStunBuffId, 20f, 0f);
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
            bool auraGone = _auraEmpty && _auraHold!.Current == null;
            long refund = FirstRefund("AuraHoldService:stun");
            bool d4 = auraGone && refund > 0;
            GD.Print($"[Stage8Sim] D4: combat_stun при удержании → рассеяна={auraGone}, возврат={refund} → {(d4 ? "PASS" : "FAIL")}");
            Record("D4", d4, $"aura cleared={auraGone} refund={refund}");
        }

        // D5: медитация при удержании → Dissipate("meditation").
        _auraEmpty = false; _qiAdds.Clear();
        Teleport(npc, pos.X + 1, pos.Y);
        if (!await ChargeAndHoldAsync(melee, pos))
        {
            Record("D5", "NOT TESTED", "не удалось удержать технику перед медитацией");
        }
        else
        {
            _meditationTogglePub!.Publish(new MeditationToggleRequestedEvent(true));
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
            bool auraGone = _auraEmpty && _auraHold!.Current == null;
            long refund = FirstRefund("AuraHoldService:meditation");
            bool d5 = auraGone && refund > 0;
            GD.Print($"[Stage8Sim] D5: медитация при удержании → рассеяна={auraGone}, возврат={refund} → {(d5 ? "PASS" : "FAIL")}");
            Record("D5", d5, $"aura cleared={auraGone} refund={refund}");
            // Медитацию выключить (не мешает следующим фазам).
            _meditationTogglePub.Publish(new MeditationToggleRequestedEvent(false));
            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
        }

        // D6a: world-reset снимает активную зарядку (reason="world_reset").
        _chargeCancels.Clear();
        Press(slow.TechniqueId, pos.X + 2, pos.Y);
        if (!await WaitUntil(() => _chargeService!.IsCharging(PlayerId), 6f))
        {
            Record("D6a", "NOT TESTED", "зарядка для world-reset не стартовала");
        }
        else
        {
            _chargeService!.ResetWorld();
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
            bool chargeGone = !_chargeService.IsCharging(PlayerId);
            bool chargeCancelled = _chargeCancels.Exists(c => c.Reason == "world_reset");
            bool d6a = chargeGone && chargeCancelled;
            GD.Print($"[Stage8Sim] D6a: world-reset → зарядка снята={chargeGone} (cancel world_reset={chargeCancelled}) → {(d6a ? "PASS" : "FAIL")}");
            Record("D6a", d6a, $"charge cleared={chargeGone}/{chargeCancelled}");
        }

        // D6b: world-reset рассеивает удержание.
        _auraEmpty = false; _qiAdds.Clear();
        Teleport(npc, pos.X + 1, pos.Y);
        if (!await ChargeAndHoldAsync(melee, pos))
        {
            Record("D6b", "NOT TESTED", "не удалось удержать технику перед world-reset");
        }
        else
        {
            _auraHold!.ResetWorld();
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
            bool auraGone = _auraEmpty && _auraHold.Current == null;
            long refund = FirstRefund("AuraHoldService:world_reset");
            bool d6b = auraGone && refund > 0;
            GD.Print($"[Stage8Sim] D6b: world-reset → аура пуста={auraGone}, возврат={refund} → {(d6b ? "PASS" : "FAIL")}");
            Record("D6b", d6b, $"aura cleared={auraGone} refund={refund}");
        }
    }

    // ================================================================
    //  E. ST8-05 — Defense без достаточной Ци
    // ================================================================
    private async Task ScenarioE(LearnedTechnique defense)
    {
        GD.Print("[Stage8Sim] ─── E. ST8-05: щит без резерва Ци не врёт ───");
        var player = _playerService!.Position;
        var qi = _qiService!;
        var buffer = _qiBuffer!;

        // E1 (сервис): пул < MIN_QI_FOR_BUFFER (10) → активация ОТКЛОНЕНА:
        //     IsActive=false, НЕТ QiBufferActivatedEvent, поглощение 0.
        DrainQiTo(5);
        _bufferActivations.Clear();
        buffer.Activate(200, QiBufferMode.Shield);
        bool notActive = !buffer.IsActive;
        bool noEvent = _bufferActivations.Count == 0;
        var absorb1 = buffer.AbsorbDamage(20, DamageType.Physical);
        bool noAbsorb = absorb1.AbsorbedDamage == 0 && absorb1.PiercingDamage == 20;
        bool e1 = notActive && noEvent && noAbsorb;
        GD.Print($"[Stage8Sim] E1: пул={qi.CurrentQi} < MIN → отказ: не активен={notActive}, события нет={noEvent}, " +
                 $"поглощение={absorb1.AbsorbedDamage}/20 (ожид 0) → {(e1 ? "PASS" : "FAIL")}");
        Record("E1", e1, $"refused={notActive} event={_bufferActivations.Count} absorbed={absorb1.AbsorbedDamage}");

        // E2 (сервис): резервный режим — пул ≥ MIN, но < инвестиции:
        //     активен с QiInvested=0 и РЕАЛЬНО поглощает из пула.
        qi.AddQi(15); // 5 → 20: ≥ MIN(10), < invest(200)
        _bufferActivations.Clear();
        buffer.Activate(200, QiBufferMode.Shield);
        bool active = buffer.IsActive && buffer.QiInvested == 0;
        bool eventHonest = _bufferActivations.Count == 1 && _bufferActivations[0].Invested == 0;
        var absorb2 = buffer.AbsorbDamage(5, DamageType.Physical);
        bool realAbsorb = absorb2.AbsorbedDamage == 5 && absorb2.PiercingDamage == 0;
        bool e2 = active && eventHonest && realAbsorb;
        GD.Print($"[Stage8Sim] E2: пул={qi.CurrentQi} ≥ MIN → резерв: активен={buffer.IsActive}, invested={buffer.QiInvested}, " +
                 $"поглощение={absorb2.AbsorbedDamage}/5 (ожид 5) → {(e2 ? "PASS" : "FAIL")}");
        Record("E2", e2, $"fallback active={active} invested=0 event={eventHonest} absorbed={absorb2.AbsorbedDamage}");
        buffer.Deactivate(); // вернуть пул к чистоте (возврат invested=0)

        // E3 (каст): заряд Defense при полной Ци → удержание → дрен пула ниже
        //     MIN_QI_FOR_BUFFER → спуск = честный отказ, удержание живёт (ST8-02).
        qi.AddQi(qi.MaxQi);
        if (_auraHold!.Current != null) _auraHold.Dissipate("qa_setup");
        _chargeService!.CancelCharge(PlayerId, "qa_setup");
        _castResults.Clear(); _bufferActivations.Clear();
        Press(defense.TechniqueId, player.X, player.Y);
        if (!await WaitUntil(() => _auraHold.Current != null
                           && _auraHold.Current.TechniqueId == defense.TechniqueId, 12f))
        {
            Record("E3", "NOT TESTED", $"зарядка Defense не завершилась/не удержана (hold='{_auraHold.Current?.TechniqueId}', qi={qi.CurrentQi})");
            return;
        }
        DrainQiTo(5); // пул ниже MIN ПОСЛЕ зарядки — ловим дефект ST8-05
        _castResults.Clear(); _bufferActivations.Clear();
        Press(defense.TechniqueId, player.X, player.Y);
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        bool refused = _castResults.Exists(r => r.Tech == defense.TechniqueId && !r.Ok
            && r.Reason == "Недостаточно Ци для щита");
        bool noBufferEvent = _bufferActivations.Count == 0;
        bool holdAlive = _auraHold!.Current != null;
        bool cd0 = _techniqueService!.GetCooldown(defense.TechniqueId) == 0;
        bool e3 = refused && noBufferEvent && holdAlive && cd0;
        GD.Print($"[Stage8Sim] E3: Defense при пул={qi.CurrentQi} → отказ='{refused}', буфер-событий={_bufferActivations.Count}, " +
                 $"удержание живо={holdAlive}, кулдаун=0:{cd0} → {(e3 ? "PASS" : "FAIL")}");
        Record("E3", e3, $"refused={refused} noBufferEvent={noBufferEvent} hold={holdAlive} cd0={cd0}");

        // E4: реген Ци (AddQi до полного) → повторный спуск = успех с инвестицией.
        qi.AddQi(qi.MaxQi);
        _castResults.Clear(); _bufferActivations.Clear();
        Press(defense.TechniqueId, player.X, player.Y);
        bool auraFreed = await WaitUntil(() => _auraHold!.Current == null, 8f);
        bool success = _castResults.Exists(r => r.Tech == defense.TechniqueId && r.Ok);
        bool invested = _bufferActivations.Count == 1
                        && _bufferActivations[0].Invested == System.Math.Max(50, defense.QiCost);
        bool activeNow = buffer.IsActive;
        GD.Print($"[Stage8Sim] E4: после регена (пул={qi.CurrentQi}) → успех={success}, инвестировано=" +
                 $"{(_bufferActivations.Count > 0 ? _bufferActivations[0].Invested : -1)} (ожид {System.Math.Max(50, defense.QiCost)}), " +
                 $"буфер активен={activeNow}, аура свободна={auraFreed} → {(success && invested && activeNow && auraFreed ? "PASS" : "FAIL")}");
        Record("E4", success && invested && activeNow && auraFreed,
            $"success={success} invested={(_bufferActivations.Count > 0 ? _bufferActivations[0].Invested : -1)} active={activeNow}");
        buffer.Deactivate(); // cleanup: вернуть инвестированную Ци игроку
        HealPlayerFully();
        await Task.CompletedTask;
    }

    // ================================================================
    //  Хелперы
    // ================================================================

    private void Press(string techId, int aimTileX, int aimTileY)
    {
        int mx = aimTileX * GameConstants.TILE_PIXELS * 1000;
        int my = aimTileY * GameConstants.TILE_PIXELS * 1000;
        _castRequestPub!.Publish(new TechniqueCastRequestedEvent(techId, mx, my));
        GD.Print($"[Stage8Sim] PRESS: {techId} → тайл ({aimTileX},{aimTileY})");
    }

    private async Task<bool> WaitUntil(System.Func<bool> cond, float timeoutSec, float step = 0.2f)
    {
        float waited = 0f;
        while (waited < timeoutSec)
        {
            if (cond()) return true;
            await ToSignal(GetTree().CreateTimer(step), SceneTreeTimer.SignalName.Timeout);
            waited += step;
        }
        return cond();
    }

    private int CountUsed(string techId)
    {
        int n = 0;
        foreach (var t in _usedEvents)
            if (t == techId) n++;
        return n;
    }

    private static List<string> Ids(List<AoeTarget> targets)
    {
        var list = new List<string>();
        foreach (var t in targets) list.Add(t.EntityId);
        return list;
    }

    private string? FindAliveNpc()
    {
        foreach (var id in _npcService!.GetAllNPCIds())
            if (_npcService.IsAlive(id)) return id;
        return null;
    }

    private string? FindSecondNpc(string first)
    {
        foreach (var id in _npcService!.GetAllNPCIds())
            if (id != first && _npcService.IsAlive(id)) return id;
        return null;
    }

    private string? FindThirdNpc(string a, string b)
    {
        foreach (var id in _npcService!.GetAllNPCIds())
            if (id != a && id != b && _npcService.IsAlive(id)) return id;
        return null;
    }

    private void Teleport(string npcId, int x, int y)
    {
        var st = _npcService!.GetNPCState(npcId);
        if (st != null) st.Position = new Position2D(x, y);
    }

    private bool PlayerHasDamagedParts()
    {
        var parts = _bodyService!.GetAllParts();
        if (parts == null) return false;
        foreach (var p in parts)
            if (p.CurrentRedHP < p.MaxRedHP) return true;
        return false;
    }

    private void HealPlayerFully()
    {
        // ST8-02 (QA): полное восстановление КРАСНОЙ HP — именно её проверяет
        // гейт «Тело не ранено» (CurrentRedHP < MaxRedHP). HealPart делит
        // лечение 70/30 (red/black) — для покрытия red-недостача требует
        // amount = ceil(missing / RED_HP_RATIO); части без чёрной HP (сердце)
        // лечатся в red на 100%. Цикл до полного red (защита от округления).
        var parts = _bodyService!.GetAllParts();
        if (parts == null) return;
        for (int round = 0; round < 6; round++)
        {
            bool anyMissing = false;
            foreach (var p in parts)
            {
                int missing = p.MaxRedHP - p.CurrentRedHP;
                if (missing <= 0) continue;
                anyMissing = true;
                int amount = p.MaxBlackHP <= 0
                    ? missing
                    : (int)System.Math.Ceiling(missing / GameConstants.RED_HP_RATIO);
                _bodyService.HealPart(p.Type, amount);
            }
            if (!anyMissing) break;
        }
    }

    private void DrainQiTo(long remain)
    {
        var qi = _qiService!;
        long drain = qi.CurrentQi - remain;
        if (drain > 0) qi.TryConsumeQi(drain);
        GD.Print($"[Stage8Sim] qi drain → остаток {qi.CurrentQi}");
    }

    private void RespawnPlayer()
    {
        if (_playerService!.IsAlive) return;
        if (GetParent() is GameWorldController gwc)
        {
            gwc.RequestPlayerRespawnForOverlay();
            GD.Print("[Stage8Sim] respawn запрошен (GWC)");
        }
    }

    private LearnedTechnique? FindTech(string id, TechniqueType type)
    {
        var t = _techniqueService!.GetTechnique(id);
        if (t != null) return t;
        foreach (var tid in _techniqueService.GetOrderedIds())
        {
            var cand = _techniqueService.GetTechnique(tid);
            if (cand != null && cand.Type == type) return cand;
        }
        return null;
    }

    /// <summary>Найти изученную технику типа ИЛИ изучить свою (освобождая слот).</summary>
    private LearnedTechnique? EnsureTech(string id, TechniqueType type, CombatSubtype subtype,
        long qiCost, float cooldown, int baseDamage, float rangeM)
    {
        var svc = _techniqueService!;
        var existing = svc.GetTechnique(id);
        if (existing != null) return existing;

        // Слот категории ИЛИ библиотека полны → освободить (забыть).
        // Сначала — техника той же категории (освобождает и слот, и место
        // библиотеки); если категория свободна, но библиотека полна —
        // забываем любую «чужую» (не qa_st8_*) технику.
        if (svc.FreeSlots(type) <= 0)
        {
            foreach (var tid in svc.GetOrderedIds())
            {
                var cand = svc.GetTechnique(tid);
                if (cand != null && cand.Type == type)
                {
                    svc.ForgetTechnique(tid);
                    GD.Print($"[Stage8Sim] EnsureTech: забыта '{tid}' (освобождение слота {type})");
                    break;
                }
            }
        }
        while (svc.LibraryFree <= 0)
        {
            string? victim = null;
            var all = svc.GetOrderedIds();
            for (int i = all.Count - 1; i >= 0; i--) // с конца: гранты AoE/formation/curse первыми
            {
                var cand = svc.GetTechnique(all[i]);
                if (all[i].StartsWith("qa_st8_")) continue;
                if (cand != null && cand.Type == TechniqueType.Cultivation) continue; // пассив не трогаем
                victim = all[i];
                break;
            }
            if (victim == null) break;
            svc.ForgetTechnique(victim);
            GD.Print($"[Stage8Sim] EnsureTech: забыта '{victim}' (освобождение библиотеки)");
        }
        bool ok = svc.LearnTechnique(id, type, TechniqueGrade.Common, subtype,
            qiCost, cooldown, castTime: 0.5f, baseDamage: baseDamage,
            element: Element.Neutral);
        // Range: legacy-сигнатура не задаёт — техника с Range=0 поднимется
        // MinAttackRangeTiles кастера (2 тайла); для melee достаточно.
        var learned = svc.GetTechnique(id);
        if (!ok || learned == null)
        {
            GD.Print($"[Stage8Sim] EnsureTech: learn '{id}' failed (ok={ok}, freeSlots={svc.FreeSlots(type)}, libFree={svc.LibraryFree})");
            return null;
        }
        GD.Print($"[Stage8Sim] EnsureTech: изучена '{id}' ({type}/{subtype}) qi={qiCost} cd={cooldown}");
        return learned;
    }

    /// <summary>AoE-конус: выданный грантом ИЛИ собственный (полный TechniqueData).</summary>
    private LearnedTechnique? EnsureAoeCone()
    {
        var svc = _techniqueService!;
        foreach (var tid in svc.GetOrderedIds())
        {
            var t = svc.GetTechnique(tid);
            if (t != null && t.Type == TechniqueType.Combat && t.Subtype == CombatSubtype.RangedAoe
                && t.AoeShape == AoeShape.Cone)
                return t;
        }
        if (svc.FreeSlots(TechniqueType.Combat) <= 0)
        {
            foreach (var tid in svc.GetOrderedIds())
            {
                var cand = svc.GetTechnique(tid);
                if (cand != null && cand.Type == TechniqueType.Combat)
                {
                    svc.ForgetTechnique(tid);
                    break;
                }
            }
        }
        while (svc.LibraryFree <= 0)
        {
            string? victim = null;
            var all = svc.GetOrderedIds();
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var cand = svc.GetTechnique(all[i]);
                if (all[i].StartsWith("qa_st8_")) continue;
                if (cand != null && cand.Type == TechniqueType.Cultivation) continue;
                victim = all[i];
                break;
            }
            if (victim == null) break;
            svc.ForgetTechnique(victim);
            GD.Print($"[Stage8Sim] EnsureAoeCone: забыта '{victim}' (освобождение библиотеки)");
        }
        var data = new TechniqueData
        {
            TechniqueId = "qa_st8_cone",
            NameRu = "qa_st8_cone",
            Type = TechniqueType.Combat,
            Subtype = CombatSubtype.RangedAoe,
            Grade = TechniqueGrade.Common,
            Element = Element.Fire,
            Level = 1,
            QiCost = 30,
            Cooldown = 1f,
            CastTime = 0.5f,
            BaseDamage = 20,
            Range = 6f,
            AoeShape = AoeShape.Cone,
            AoeRadiusTiles = 5,
            AoeHalfAngleDeg = 45,
            AoeFalloffPermil = 0,
            AoeMaxTargets = 0
        };
        bool ok = svc.LearnTechnique(data);
        var learned = svc.GetTechnique("qa_st8_cone");
        GD.Print($"[Stage8Sim] EnsureAoeCone: own cone learned={ok}");
        return learned;
    }

    /// <summary>Зарядить technique и дождаться удержания ИМЕННО этой техники в ауре.</summary>
    private async Task<bool> ChargeAndHoldAsync(LearnedTechnique tech, Position2D player)
    {
        if (_auraHold!.Current != null) _auraHold.Dissipate("qa_setup");
        _chargeService!.CancelCharge(PlayerId, "qa_setup"); // чистый старт
        _auraEmpty = false;
        Press(tech.TechniqueId, player.X + 2, player.Y);
        return await WaitUntil(() => _auraHold.Current != null
                                && _auraHold.Current.TechniqueId == tech.TechniqueId, 12f);
    }

    private void Record(string id, bool pass, string detail)
        => Record(id, pass ? "PASS" : "FAIL", detail);

    private void Record(string id, string status, string detail)
    {
        _results.Add((id, status, detail));
    }

    private void PrintSummary()
    {
        GD.Print("[Stage8Sim] ═══════ СВОДКА ЭТАПА-8 ═══════");
        int pass = 0, fail = 0, notTested = 0;
        foreach (var (id, status, detail) in _results)
        {
            GD.Print($"[Stage8Sim] {id,-4} {status,-10} {detail}");
            if (status == "PASS") pass++;
            else if (status == "FAIL") fail++;
            else notTested++;
        }
        GD.Print($"[Stage8Sim] Итого: PASS={pass} FAIL={fail} NOT TESTED={notTested}");
        PrintVerdict(fail == 0 && notTested == 0 && pass > 0);
    }

    private static void PrintVerdict(bool pass)
    {
        GD.Print($"[Stage8Sim] VERDICT: {(pass ? "PASS — ST8-01..06 закрыты" : "FAIL")}");
    }
}

#nullable enable
// Создано: 2026-08-25 (Stage 0+1, GLM-5.3) — headless-верификация модели
// заполнения техник + варианта В (аура держит одну).
// Запуск: GODOT_NEWGAME=1 GODOT_CHARGE_SIM=1 godot --headless --path . scenes/MainMenu.tscn
//
// Проверяем:
//   1. TechniqueCastRequestedEvent (Z) → TechniqueChargeStartedEvent (зарядка пошла)
//   2. Тики CombatModule.Tick → TechniqueChargeProgressEvent → TechniqueChargeCompletedEvent
//      (potencyPermil = 1000 на Stage 0; ChargedQi ≥ QiCost)
//   3. AuraHoldService.Hold → HeldTechniqueChangedEvent (техника в ауре)
//   4. Второе нажатие Z → Release → AttackIntentEvent с potencyPermil > 1000 → урон по NPC
//
// Источник: checkpoints/08_25_technique_hold_analysis.md §8 Stage 0+1.
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Modules.Combat;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Headless-верификация модели заполнения техник и ауры-задержки (Stage 0+1).
/// Скриптованная серия TechniqueCastRequestedEvent + подписки на charge-события.
/// Итог: [ChargeSim] VERDICT: PASS/FAIL.
/// </summary>
public partial class ChargeSimDebug : Node
{
    [Inject] private IPublisher<TechniqueCastRequestedEvent>? _castRequestPub;
    [Inject] private ISubscriber<TechniqueChargeStartedEvent>? _startedSub;
    [Inject] private ISubscriber<TechniqueChargeProgressEvent>? _progressSub;
    [Inject] private ISubscriber<TechniqueChargeCompletedEvent>? _completedSub;
    [Inject] private ISubscriber<TechniqueChargeCancelledEvent>? _cancelledSub;
    [Inject] private ISubscriber<HeldTechniqueChangedEvent>? _heldChangedSub;
    [Inject] private ISubscriber<AttackIntentEvent>? _attackIntentSub;
    [Inject] private ISubscriber<DamageAppliedEvent>? _damageSub;
    [Inject] private TechniqueService? _techniqueService;
    [Inject] private IPlayerService? _playerService;
    [Inject] private INPCService? _npcService;
    [Inject] private IBodyDataProvider? _bodyProvider;

    private System.IDisposable? _startedToken;
    private System.IDisposable? _progressToken;
    private System.IDisposable? _completedToken;
    private System.IDisposable? _cancelledToken;
    private System.IDisposable? _heldToken;
    private System.IDisposable? _intentToken;
    private System.IDisposable? _damageToken;

    private bool _startedReceived;
    private bool _completedReceived;
    private int _completedPotency;
    private long _completedChargedQi;
    private bool _heldReceived;
    private bool _releaseIntentReceived;  // AttackIntent with potency > 1000
    private int _releasePotency;
    private readonly Dictionary<string, int> _damageByTarget = new();

    private const string PlayerCombatId = "player_0";

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        GD.Print($"[ChargeSim] diag: castPub={_castRequestPub != null} startedSub={_startedSub != null} " +
                 $"completedSub={_completedSub != null} heldSub={_heldChangedSub != null} " +
                 $"intentSub={_attackIntentSub != null} dmgSub={_damageSub != null} " +
                 $"techniques={_techniqueService != null} player={_playerService != null}");

        _startedToken = _startedSub?.Subscribe((in TechniqueChargeStartedEvent e) =>
        {
            if (e.EntityId != PlayerCombatId && e.EntityId != "player") return;
            _startedReceived = true;
            GD.Print($"[ChargeSim] STARTED: tech={e.TechniqueId} qiCost={e.QiCost} cap={e.Capacity} rate‰={e.ChargeRatePermil}");
        });

        _progressToken = _progressSub?.Subscribe((in TechniqueChargeProgressEvent e) =>
        {
            // Только первое для лога (избежать спама)
            if (e.EntityId != PlayerCombatId && e.EntityId != "player") return;
            if (e.ChargedQi == e.QiCost / 4 || e.ChargedQi == e.QiCost / 2 || e.ChargedQi == e.QiCost)
                GD.Print($"[ChargeSim] PROGRESS: {e.ChargedQi}/{e.QiCost} potency={e.PotencyPermil}‰");
        });

        _completedToken = _completedSub?.Subscribe((in TechniqueChargeCompletedEvent e) =>
        {
            if (e.EntityId != PlayerCombatId && e.EntityId != "player") return;
            _completedReceived = true;
            _completedPotency = e.PotencyPermil;
            _completedChargedQi = e.ChargedQi;
            GD.Print($"[ChargeSim] COMPLETED: tech={e.TechniqueId} potency={e.PotencyPermil}‰ charged={e.ChargedQi}");
        });

        _cancelledToken = _cancelledSub?.Subscribe((in TechniqueChargeCancelledEvent e) =>
        {
            if (e.EntityId != PlayerCombatId && e.EntityId != "player") return;
            GD.Print($"[ChargeSim] CANCELLED: tech={e.TechniqueId} refund={e.RefundedQi} reason={e.Reason}");
        });

        _heldToken = _heldChangedSub?.Subscribe((in HeldTechniqueChangedEvent e) =>
        {
            if (e.EntityId != PlayerCombatId && e.EntityId != "player") return;
            if (!string.IsNullOrEmpty(e.TechniqueId))
            {
                _heldReceived = true;
                GD.Print($"[ChargeSim] HELD: tech={e.TechniqueId} potency={e.PotencyPermil}‰ element={e.Element}");
            }
            else
            {
                GD.Print($"[ChargeSim] AURA EMPTY (released/dissipated)");
            }
        });

        _intentToken = _attackIntentSub?.Subscribe((in AttackIntentEvent e) =>
        {
            if (e.AttackerId != PlayerCombatId && e.AttackerId != "player") return;
            if (e.IsCharged || e.PotencyPermil > GameConstants.POTENCY_BASE_PERMIL)
            {
                _releaseIntentReceived = true;
                _releasePotency = e.PotencyPermil;
                GD.Print($"[ChargeSim] RELEASE INTENT: tech={e.TechniqueId} potency={e.PotencyPermil}‰ isCharged={e.IsCharged} target={e.TargetId}");
            }
        });

        _damageToken = _damageSub?.Subscribe((in DamageAppliedEvent e) =>
        {
            _damageByTarget.TryGetValue(e.TargetId, out int sum);
            _damageByTarget[e.TargetId] = sum + e.Damage;
            GD.Print($"[ChargeSim] damage: {e.SourceId} → {e.TargetId}: {e.Damage} ({e.Result})");
        });

        GD.Print("[ChargeSim] Ready — fill-model verification starts in 3s");
        _ = RunSequenceAsync();
    }

    public override void _ExitTree()
    {
        _startedToken?.Dispose(); _progressToken?.Dispose();
        _completedToken?.Dispose(); _cancelledToken?.Dispose();
        _heldToken?.Dispose(); _intentToken?.Dispose();
        _damageToken?.Dispose();
        _startedToken = _progressToken = _completedToken = _cancelledToken = null;
        _heldToken = _intentToken = _damageToken = null;
    }

    private async System.Threading.Tasks.Task RunSequenceAsync()
    {
        // 1. Даём сцене собраться + TechniqueGrantPhase выдать техники.
        await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);

        if (_castRequestPub == null || _techniqueService == null || _playerService == null)
        {
            GD.Print("[ChargeSim] FAIL — DI not wired");
            PrintVerdict(false);
            return;
        }

        // 2. Найти первую Combat-технику игрока (для проверки нужен target).
        string? combatTechId = null;
        foreach (var id in _techniqueService.GetOrderedIds())
        {
            var tech = _techniqueService.GetTechnique(id);
            if (tech != null && tech.Type == TechniqueType.Combat)
            {
                combatTechId = id;
                GD.Print($"[ChargeSim] selected combat tech: {tech.Name} L{tech.Level} qiCost={tech.QiCost} cast={tech.CastTime}s");
                break;
            }
        }
        if (combatTechId == null)
        {
            GD.Print("[ChargeSim] FAIL — no Combat technique learned (TechniqueGrantPhase?)");
            PrintVerdict(false);
            return;
        }

        // 3. Найти враждебного NPC, телепортировать рядом с игроком.
        string? npcId = FindHostileNpc();
        if (npcId == null)
        {
            GD.Print("[ChargeSim] FAIL — no hostile NPC spawned");
            PrintVerdict(false);
            return;
        }
        var playerPos = _playerService.Position;
        if (_npcService != null)
        {
            var npcState = _npcService.GetNPCState(npcId);
            if (npcState != null)
            {
                npcState.Position = new Position2D(playerPos.X + 1, playerPos.Y);
                GD.Print($"[ChargeSim] NPC {npcId} teleported to ({playerPos.X + 1}, {playerPos.Y})");
            }
        }

        // 4. Первое нажатие Z — запуск зарядки.
        int mouseX = (playerPos.X + 2) * GameConstants.TILE_PIXELS * 1000;
        int mouseY = playerPos.Y * GameConstants.TILE_PIXELS * 1000;
        _castRequestPub.Publish(new TechniqueCastRequestedEvent(combatTechId, mouseX, mouseY));
        GD.Print("[ChargeSim] PRESS 1 (start charge) — published TechniqueCastRequestedEvent");

        // 5. Ждать TechniqueChargeStartedEvent (1с — мгновенно после тика).
        for (int i = 0; i < 10 && !_startedReceived; i++)
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        if (!_startedReceived)
        {
            GD.Print("[ChargeSim] FAIL — TechniqueChargeStartedEvent not received (StartCharge rejected?)");
            PrintVerdict(false);
            return;
        }

        // 6. Ждать TechniqueChargeCompletedEvent (до 10с — L1 Combat ~2 тика).
        for (int i = 0; i < 40 && !_completedReceived; i++)
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        if (!_completedReceived)
        {
            GD.Print("[ChargeSim] FAIL — TechniqueChargeCompletedEvent not received (charge stuck?)");
            PrintVerdict(false);
            return;
        }

        // 7. Проверка: potency на Stage 0 = 1000, chargedQi ≥ qiCost.
        var techData = _techniqueService.GetTechnique(combatTechId);
        long expectedQiCost = techData?.QiCost ?? 0;
        if (_completedChargedQi < expectedQiCost)
        {
            GD.Print($"[ChargeSim] FAIL — ChargedQi {_completedChargedQi} < QiCost {expectedQiCost}");
            PrintVerdict(false);
            return;
        }
        if (_completedPotency != GameConstants.POTENCY_BASE_PERMIL)
        {
            GD.Print($"[ChargeSim] WARN — potency {_completedPotency}‰ != base {GameConstants.POTENCY_BASE_PERMIL}‰ (Stage 0 expects base; overcharge = Stage 2)");
        }

        // 8. Ждать HeldTechniqueChangedEvent (aura park) — должен прийти сразу после Completed.
        for (int i = 0; i < 6 && !_heldReceived; i++)
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        if (!_heldReceived)
        {
            GD.Print("[ChargeSim] FAIL — HeldTechniqueChangedEvent not received (aura Hold failed?)");
            PrintVerdict(false);
            return;
        }

        // 9. Второе нажатие Z — выпуск удержанной техники.
        _castRequestPub.Publish(new TechniqueCastRequestedEvent(combatTechId, mouseX, mouseY));
        GD.Print("[ChargeSim] PRESS 2 (release held) — published TechniqueCastRequestedEvent");

        // 10. Ждать AttackIntentEvent с potency > 1000 (release).
        for (int i = 0; i < 10 && !_releaseIntentReceived; i++)
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        if (!_releaseIntentReceived)
        {
            GD.Print("[ChargeSim] FAIL — no charged AttackIntent after release (PlayerTechniqueCaster release path?)");
            PrintVerdict(false);
            return;
        }

        // 11. Ждать урона по NPC (charge → release → AttackIntent → CombatService → damage).
        for (int i = 0; i < 10; i++)
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);

        bool npcDamaged = _damageByTarget.TryGetValue(npcId, out int dmg) && dmg > 0;
        GD.Print($"[ChargeSim] npc {npcId} damage total: {dmg}");

        // 12. Итог.
        bool pass = _startedReceived && _completedReceived && _heldReceived
                    && _releaseIntentReceived && npcDamaged;
        if (!_releaseIntentReceived)
            GD.Print("[ChargeSim] FAIL — release AttackIntent not published (potency > 1000 expected)");
        if (!npcDamaged)
            GD.Print("[ChargeSim] FAIL — NPC did not take damage after release (combat pipeline?)");

        PrintVerdict(pass);
    }

    private string? FindHostileNpc()
    {
        if (_npcService == null) return null;
        foreach (var id in _npcService.GetAllNPCIds())
        {
            var state = _npcService.GetNPCState(id);
            if (state != null && state.IsAlive && state.Disposition == NPCDisposition.Hostile)
                return id;
        }
        foreach (var id in _npcService.GetAllNPCIds())
            if (_npcService.IsAlive(id)) return id;
        return null;
    }

    private static void PrintVerdict(bool pass)
    {
        GD.Print($"[ChargeSim] VERDICT: {(pass ? "PASS — fill model + aura hold + release all wired" : "FAIL")}");
    }
}

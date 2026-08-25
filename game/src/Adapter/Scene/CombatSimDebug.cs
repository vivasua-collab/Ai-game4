#nullable enable
// Создано: 2026-08-25 — NPC_COMBAT_PREP P0/Phase 8: headless-верификация боевого
// пайплайна (GODOT_COMBAT_SIM=1). Проверяет оба направления урона:
//   NPC → игрок  (P0-баг: BodyService не применял урон по "player_0")
//   игрок → NPC  (Phase 8: wiring урона оружия/статов экипировки)
// Запуск: GODOT_NEWGAME=1 GODOT_COMBAT_SIM=1 godot --headless --path . scenes/MainMenu.tscn
// Источник: docs/docs_v2/09_workflow/NPC_COMBAT_PREP.md §P0, SESSION_CONTEXT §7.
using Godot;
using System.Collections.Generic;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Headless-верификация боевого пайплайна (P0 из SESSION_CONTEXT §7).
/// Скриптованная серия AttackIntentEvent в обе стороны + подсчёт
/// DamageAppliedEvent. Итог: [CombatSim] VERDICT: PASS/FAIL.
///
/// Паттерн следует GODOT_NEWGAME/GODOT_GEN_DEBUG — env-хук только для
/// верификации, игровой код не затрагивает.
/// </summary>
public partial class CombatSimDebug : Node
{
    [Inject] private IPublisher<Core.Messaging.Contracts.AttackIntentEvent>? _attackIntentPub;
    [Inject] private ISubscriber<Core.Messaging.Contracts.DamageAppliedEvent>? _damageSub;
    [Inject] private ISubscriber<Core.Messaging.Contracts.AttackIntentEvent>? _attackIntentSub;
    [Inject] private INPCService? _npcService;
    [Inject] private IBodyDataProvider? _bodyProvider;
    [Inject] private IEquipmentDataProvider? _equipmentProvider;
    [Inject] private ITimeService? _timeService;
    [Inject] private IEquipmentService? _equipmentService;
    [Inject] private Modules.Generator.EquipmentGenerator? _equipmentGenerator;
    [Inject] private IPlayerService? _playerService;

    private System.IDisposable? _damageToken;
    private System.IDisposable? _intentEchoToken;

    // Учёт урона по целям за время симуляции.
    private readonly Dictionary<string, int> _damageByTarget = new();
    private int _eventsReceived;

    private const string PlayerCombatId = "player_0"; // NPCAIService.PlayerId

    public override void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
            ContainerAdapter.InjectProperties(this, container);

        // Диагностика: публикатор/подписчик/время (null → DI-проблема).
        GD.Print($"[CombatSim] diag: pub={_attackIntentPub != null} sub={_attackIntentSub != null} " +
                 $"dmgSub={_damageSub != null} time={_timeService != null} " +
                 $"(speed={_timeService?.Speed}, paused={_timeService?.IsPaused})");

        _intentEchoToken = _attackIntentSub?.Subscribe((in Core.Messaging.Contracts.AttackIntentEvent e) =>
        {
            GD.Print($"[CombatSim] intent echo: {e.AttackerId} → {e.TargetId} ({e.TechniqueId})");
        });

        _damageToken = _damageSub?.Subscribe((in Core.Messaging.Contracts.DamageAppliedEvent e) =>
        {
            _eventsReceived++;
            _damageByTarget.TryGetValue(e.TargetId, out int sum);
            _damageByTarget[e.TargetId] = sum + e.Damage;
            GD.Print($"[CombatSim] damage: {e.SourceId} → {e.TargetId}: {e.Damage} ({e.Result}, part={e.HitPart})");
        });

        GD.Print("[CombatSim] Ready — scripted combat verification starts in 2s");
        _ = RunSequenceAsync();
    }

    public override void _ExitTree()
    {
        _damageToken?.Dispose();
        _intentEchoToken?.Dispose();
        _damageToken = null;
        _intentEchoToken = null;
    }

    private async System.Threading.Tasks.Task RunSequenceAsync()
    {
        // 1. Даём сцене собраться (фазы спавна NPC завершаются в первые кадры).
        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);

        if (_attackIntentPub == null || _npcService == null || _bodyProvider == null)
        {
            GD.Print("[CombatSim] FAIL — DI not wired (publisher/npc/body missing)");
            PrintVerdict(false);
            return;
        }

        // 2. Диагностика Phase 8: wiring статов экипировки (ожидаем ненулевые
        //    значения для NPC — генератор «Матрёшка» выдаёт оружие всем).
        string? npcId = FindHostileNpc();
        if (npcId == null)
        {
            GD.Print("[CombatSim] FAIL — no hostile NPC spawned (HumanNPCSpawnPhase)");
            PrintVerdict(false);
            return;
        }

        if (_equipmentProvider != null)
        {
            GD.Print($"[CombatSim] equip[{npcId}]: pen={_equipmentProvider.GetWeaponPenetration(npcId)} " +
                     $"dodge={_equipmentProvider.GetDodgeBonusPermil(npcId)}‰ " +
                     $"block={_equipmentProvider.GetBlockBonusPermil(npcId)}‰ " +
                     $"crit={_equipmentProvider.GetCritBonusPermil(npcId)}‰ " +
                     $"dmg={_equipmentProvider.GetTotalDamage(npcId)}");
            GD.Print($"[CombatSim] equip[player]: pen={_equipmentProvider.GetWeaponPenetration(PlayerCombatId)} " +
                     $"dmg={_equipmentProvider.GetTotalDamage(PlayerCombatId)} " +
                     $"(без экипировки — 0 ожидаемо)");
        }

        int playerHpBefore = _bodyProvider.GetCurrentHealth("player");
        int npcHpBefore = _bodyProvider.GetCurrentHealth(npcId);
        GD.Print($"[CombatSim] start HP: player={playerHpBefore}, npc({npcId})={npcHpBefore}");

        // 2b. Телепортируем NPC рядом с игроком — бой происходит в кадре камеры
        // (для визуальной верификации HP-баров/цифр урона на скриншотах).
        // NPC, а не игрока: камера и спрайт игрока привязаны к _visualPosition
        // контроллера, а PlayerService.SetPosition двигает только логику.
        if (_playerService != null)
        {
            var playerPos = _playerService.Position;
            var npcState = _npcService.GetNPCState(npcId);
            if (npcState != null)
            {
                npcState.Position = new Position2D(playerPos.X + 1, playerPos.Y);
                GD.Print($"[CombatSim] NPC {npcId} teleported to ({playerPos.X + 1}, {playerPos.Y}) near player");
            }
        }

        // 3. Серия ударов в обе стороны (как это делает NPCModule/PlayerCombatAdapter).
        for (int round = 1; round <= 4; round++)
        {
            // NPC → игрок (P0-проверка: урон должен примениться к телу игрока).
            _attackIntentPub.Publish(new Core.Messaging.Contracts.AttackIntentEvent(
                npcId, PlayerCombatId, "npc_strike", false));
            await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);

            // Игрок → NPC (Phase 8: weapon damage wiring).
            _attackIntentPub.Publish(new Core.Messaging.Contracts.AttackIntentEvent(
                PlayerCombatId, npcId, "basic_attack", false));
            await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);

            GD.Print($"[CombatSim] round {round}: player HP={_bodyProvider.GetCurrentHealth("player")}, " +
                     $"npc HP={_bodyProvider.GetCurrentHealth(npcId)}");
        }

        // 3b. Phase 8 end-to-end: надеть оружие игроком и проверить, что урон
        // вырос (weapon damage + penetration проходят через EquipmentService →
        // EquipmentDataProvider → CombatService).
        bool weaponWiringOk = true; // остаётся true, если фаза пропущена
        if (_equipmentService != null && _equipmentGenerator != null)
        {
            var weapon = _equipmentGenerator.GenerateWeapon(level: 3);
            bool equipped = _equipmentService.TryEquip(EquipmentSlot.WeaponMain, weapon);
            if (equipped && _equipmentProvider != null)
            {
                GD.Print($"[CombatSim] player equipped '{weapon.NameRu}' (dmg={weapon.Damage}, pen={weapon.Penetration}) — " +
                         $"provider: dmg={_equipmentProvider.GetTotalDamage(PlayerCombatId)}, " +
                         $"pen={_equipmentProvider.GetWeaponPenetration(PlayerCombatId)}");
                int npcHpBeforeWeapon = _bodyProvider.GetCurrentHealth(npcId);
                _attackIntentPub.Publish(new Core.Messaging.Contracts.AttackIntentEvent(
                    PlayerCombatId, npcId, "basic_attack", false));
                // Каст 0.5с разрешится тиком (~1с при Normal) — ждём подольше.
                await ToSignal(GetTree().CreateTimer(1.6), SceneTreeTimer.SignalName.Timeout);
                int npcHpAfterWeapon = _bodyProvider.GetCurrentHealth(npcId);
                int weaponSwing = npcHpBeforeWeapon - npcHpAfterWeapon;
                GD.Print($"[CombatSim] armed swing: npc HP {npcHpBeforeWeapon}→{npcHpAfterWeapon} ({weaponSwing} RedHP dmg)");
                weaponWiringOk = weaponSwing > 0;
            }
            else if (!equipped)
            {
                GD.Print("[CombatSim] WARN — TryEquip(WeaponMain) failed");
            }
        }
        else
        {
            GD.Print("[CombatSim] skip weapon phase — IEquipmentService/Generator not injected");
        }

        // 4. Итоги.
        int playerHpAfter = _bodyProvider.GetCurrentHealth("player");
        int npcHpAfter = _bodyProvider.GetCurrentHealth(npcId);

        bool playerTookDamage = playerHpAfter < playerHpBefore;
        bool npcTookDamage = _damageByTarget.TryGetValue(npcId, out int npcDamage) && npcDamage > 0;

        GD.Print($"[CombatSim] summary: events={_eventsReceived}, " +
                 $"player {playerHpBefore}→{playerHpAfter} ({(playerHpBefore - playerHpAfter)} dmg), " +
                 $"npc {npcHpBefore}→{npcHpAfter}");

        bool pass = playerTookDamage && npcTookDamage && weaponWiringOk;
        if (!playerTookDamage)
            GD.Print("[CombatSim] FAIL — NPC→player damage did NOT apply (BodyService player-id mismatch?)");
        if (!npcTookDamage)
            GD.Print("[CombatSim] FAIL — player→NPC damage did NOT apply (attack pipeline broken?)");
        if (!weaponWiringOk)
            GD.Print("[CombatSim] FAIL — armed swing did no damage (weapon wiring broken?)");

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
        // Fallback: любой живой NPC (состав локации может измениться).
        foreach (var id in _npcService.GetAllNPCIds())
        {
            if (_npcService.IsAlive(id)) return id;
        }
        return null;
    }

    private static void PrintVerdict(bool pass)
    {
        GD.Print($"[CombatSim] VERDICT: {(pass ? "PASS — обе стороны боя получают урон" : "FAIL")}");
    }
}

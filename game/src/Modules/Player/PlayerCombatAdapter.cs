#nullable enable
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Player;

/// <summary>
/// PlayerCombatAdapter — bridges player input and combat via EventBus.
///
/// ARCHITECTURE: cross-module interactions go through EventBus only — no
/// direct injection of ICombatService / IBodyService. The one sanctioned
/// exception is INPCService for target selection (same pattern as
/// NPCCombatAdapter injecting NPCService). Subscribes to
/// <see cref="CombatStartedEvent"/> / <see cref="CombatEndedEvent"/> and
/// <see cref="DamageAppliedEvent"/>. Publishes <see cref="AttackIntentEvent"/>
/// with a resolved TargetId (NPC_COMBAT_PREP Phase 6: target selection).
/// </summary>
public sealed class PlayerCombatAdapter : IDisposable
{
    [Inject] private readonly IPlayerService _player = null!;
    [Inject] private readonly IPlayerInputService _input = null!;
    [Inject] private readonly INPCService _npcs = null!;
    [Inject] private readonly IPublisher<AttackIntentEvent> _attackIntentPub = null!;
    [Inject] private readonly ISubscriber<CombatStartedEvent> _combatStartedSub = null!;
    [Inject] private readonly ISubscriber<CombatEndedEvent> _combatEndedSub = null!;
    [Inject] private readonly ISubscriber<DamageAppliedEvent> _damageSub = null!;

    /// <summary>Max Chebyshev distance (tiles) for Space-key melee target lock.</summary>
    public const float AttackRangeTiles = 2.5f;

    private IDisposable? _combatStartedToken;
    private IDisposable? _combatEndedToken;
    private IDisposable? _damageToken;

    public void Start()
    {
        _combatStartedToken = _combatStartedSub.Subscribe(OnCombatStarted);
        _combatEndedToken = _combatEndedSub.Subscribe(OnCombatEnded);
        _damageToken = _damageSub.Subscribe(OnDamageApplied);
    }

    public void Tick(float deltaTime)
    {
        if (!_input.IsAttackPressed) return;

        string? target = FindNearestTarget();
        if (target == null) return; // Nothing in range — no attack intent.

        // TargetId resolves the attack: CombatModule starts combat and runs
        // the 11-layer damage pipeline on ExecuteAttack.
        _attackIntentPub.Publish(new AttackIntentEvent(
            _player.PlayerId, target, "basic_attack", false));
    }

    /// <summary>
    /// Target selection (Phase 6): nearest alive NPC within melee range of
    /// the player, Chebyshev distance (matches harvest/interaction reach).
    /// </summary>
    private string? FindNearestTarget()
    {
        if (_npcs == null || _player == null) return null;

        var playerPos = _player.Position;
        var nearby = _npcs.GetNearbyNPCIds(playerPos, AttackRangeTiles);
        if (nearby == null || nearby.Count == 0) return null;

        string? best = null;
        int bestDist = int.MaxValue;
        foreach (var id in nearby)
        {
            var npc = _npcs.GetNPC(id);
            if (npc == null || !_npcs.IsAlive(id)) continue;

            int dist = Math.Max(
                Math.Abs(npc.Position.X - playerPos.X),
                Math.Abs(npc.Position.Y - playerPos.Y));
            if (dist < bestDist) { bestDist = dist; best = id; }
        }
        return best;
    }

    private void OnCombatStarted(in CombatStartedEvent e)
    {
        // PlayerModule reads stance to gate non-combat actions.
        // Stance flip handled by PlayerService via its own CombatStarted subscription.
    }

    private void OnCombatEnded(in CombatEndedEvent e)
    {
        // Stance reset handled by PlayerService via its own subscription.
    }

    private void OnDamageApplied(in DamageAppliedEvent e)
    {
        // Death detection delegated to PlayerService (Q4: HP via BodyService).
    }

    public void Dispose()
    {
        _combatStartedToken?.Dispose();
        _combatEndedToken?.Dispose();
        _damageToken?.Dispose();
        _combatStartedToken = _combatEndedToken = _damageToken = null;
    }
}

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
    [Inject] private readonly IStatProvider _stats = null!;
    [Inject] private readonly IPublisher<AttackIntentEvent> _attackIntentPub = null!;
    [Inject] private readonly ISubscriber<CombatStartedEvent> _combatStartedSub = null!;
    [Inject] private readonly ISubscriber<CombatEndedEvent> _combatEndedSub = null!;
    [Inject] private readonly ISubscriber<DamageAppliedEvent> _damageSub = null!;

    /// <summary>Max Chebyshev distance (tiles) for Space-key melee target lock.</summary>
    public const float AttackRangeTiles = 2.5f;

    /// <summary>
    /// COMBAT_SYSTEM.md §8.1: базовая атака = 1 игровая минута = 1 сек (Normal).
    /// Раньше кулдауна не было: зажатый Space публиковал AttackIntentEvent
    /// каждый физ-кадр (~60/сек) — спам AttackRejectedEvent и тостов,
    /// атака быстрее спеки. Теперь удержание Space = автоатака с темпом §8.1-8.2.
    /// </summary>
    public const float BaseAttackCooldownSec = 1.0f;

    private float _attackCooldownSec;

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
        // §8.1: тикт кулдауна базовой атаки (секунды на Normal).
        if (_attackCooldownSec > 0f)
        {
            _attackCooldownSec -= deltaTime;
            if (_attackCooldownSec > 0f) return;
        }
        if (!_input.IsAttackPressed) return;

        string? target = FindNearestTarget();
        if (target == null) return; // Nothing in range — no attack intent.

        // TargetId resolves the attack: CombatModule starts combat and runs
        // the 11-layer damage pipeline on ExecuteAttack.
        _attackIntentPub.Publish(new AttackIntentEvent(
            _player.PlayerId, target, "basic_attack", false));

        // Кулдаун ставится только на УСПЕШНЫЙ интент (цель найдена) —
        // атака «вхолостую» не блокирует следующий замах.
        _attackCooldownSec = AttackCooldownSeconds();
    }

    /// <summary>
    /// COMBAT_SYSTEM.md §8.2 (только для базовых атак):
    /// actualDuration = baseDuration / (1 + agility × 0.01).
    /// AGI игрока — через IStatProvider (StatProviderAdapter).
    /// Дефолт AGI=10 → 1/(1.1) ≈ 0.91 сек.
    /// </summary>
    private float AttackCooldownSeconds()
    {
        int agi = _stats?.GetStat(_player.PlayerId, StatType.Agility) ?? 10;
        return BaseAttackCooldownSec / (1f + agi * 0.01f);
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

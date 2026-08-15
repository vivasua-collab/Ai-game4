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
/// direct injection of ICombatService / IBodyService. Subscribes to
/// <see cref="CombatStartedEvent"/> / <see cref="CombatEndedEvent"/> to flip
/// the player stance, and to <see cref="DamageAppliedEvent"/> to detect
/// death. Publishes <see cref="AttackIntentEvent"/> when the player attacks.
/// </summary>
public sealed class PlayerCombatAdapter : IDisposable
{
    [Inject] private readonly IPlayerService _player = null!;
    [Inject] private readonly IPlayerInputService _input = null!;
    [Inject] private readonly IPublisher<AttackIntentEvent> _attackIntentPub = null!;
    [Inject] private readonly ISubscriber<CombatStartedEvent> _combatStartedSub = null!;
    [Inject] private readonly ISubscriber<CombatEndedEvent> _combatEndedSub = null!;
    [Inject] private readonly ISubscriber<DamageAppliedEvent> _damageSub = null!;

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
        if (_input.IsAttackPressed)
        {
            _attackIntentPub.Publish(new AttackIntentEvent(
                _player.PlayerId, string.Empty, "basic_attack", false));
        }
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
        // V1 stub: delegate death detection to PlayerService.Die via HP check.
        // (Real adapter reads IBodyDataProvider for HP — added when Body
        // module is fully wired.)
    }

    public void Dispose()
    {
        _combatStartedToken?.Dispose();
        _combatEndedToken?.Dispose();
        _damageToken?.Dispose();
        _combatStartedToken = _combatEndedToken = _damageToken = null;
    }
}

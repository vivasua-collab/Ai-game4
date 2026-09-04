#nullable enable
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Modules.Combat; // Phase 8 ч.3: CombatLos (LOS-фильтр цели)

namespace CultivationGame.Modules.Player;

/// <summary>
/// PlayerCombatAdapter — bridges player input and combat via EventBus.
///
/// ARCHITECTURE: cross-module interactions go through EventBus only — no
/// direct injection of ICombatService / IBodyService. The two sanctioned
/// exceptions are INPCService for target selection and IEquipmentDataProvider
/// for weapon-mode resolution (same pattern as NPCCombatAdapter injecting
/// NPCService). Subscribes to
/// <see cref="CombatStartedEvent"/> / <see cref="CombatEndedEvent"/> and
/// <see cref="DamageAppliedEvent"/>. Publishes <see cref="AttackIntentEvent"/>
/// with a resolved TargetId (NPC_COMBAT_PREP Phase 6: target selection).
///
/// Phase 8 ч.2 (2026-09-03): режим оружия (Melee/Ranged). Клавиши 1/2
/// (GameWorldController) переключают режим; Space в Ranged-режиме с луком
/// атакует цель на дистанции оружия (EquipmentData.AttackRange, §10.2).
/// Phase 8 ч.3 (2026-09-03): LOS-фильтр выбора цели — прицеливание в
/// ближайшую ВИДИМУЮ цель (не сквозь дерево/камень); все цели перекрыты →
/// тост-отклонение + пауза прицеливания (анти-спам). Третья санкционированная
/// инъекция — ITileService (позиционная проверка LOS, паттерн NPC-B05).
/// </summary>
public sealed class PlayerCombatAdapter : IDisposable
{
    [Inject] private readonly IPlayerService _player = null!;
    [Inject] private readonly IPlayerInputService _input = null!;
    [Inject] private readonly INPCService _npcs = null!;
    [Inject] private readonly IStatProvider _stats = null!;
    [Inject] private readonly IEquipmentDataProvider _equipment = null!;
    // Phase 8 ч.3: LOS-фильтр цели (паттерн «sanctioned exceptions»)
    [Inject] private readonly ITileService? _tiles = null;
    [Inject] private readonly IPublisher<AttackIntentEvent> _attackIntentPub = null!;
    [Inject] private readonly IPublisher<AttackRejectedEvent> _attackRejectedPub = null!;
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

    /// <summary>
    /// Phase 8 ч.3: пауза повторного прицеливания, когда все цели в радиусе
    /// перекрыты препятствием (тост «нет линии огня» не чаще 1/0.4с —
    /// зажатый Space не спамит ни интенты, ни тосты).
    /// </summary>
    public const float LosRetryCooldownSec = 0.4f;

    private float _attackCooldownSec;

    // === Phase 8 ч.2 (2026-09-03): режим оружия (клавиши 1/2) ===

    /// <summary>Режим оружия игрока: Melee (кулаки/ближнее) или Ranged (лук).</summary>
    public WeaponMode CurrentWeaponMode { get; private set; } = WeaponMode.Melee;

    /// <summary>
    /// 2026-09-04 S1: остаток кулдауна атаки (сек) — для HUD-индикатора
    /// боевой готовности (GameWorldController). 0 = удар доступен.
    /// </summary>
    public float AttackCooldownRemaining => _attackCooldownSec > 0f ? _attackCooldownSec : 0f;

    private IDisposable? _combatStartedToken;
    private IDisposable? _combatEndedToken;
    private IDisposable? _damageToken;

    /// <summary>
    /// Phase 8 ч.2: режим оружия игрока (Melee по умолчанию).
    /// переключается клавишами 1/2 (GameWorldController).
    /// </summary>
    public enum WeaponMode
    {
        /// <summary>Кулаки или ближнее оружие (Space, дистанция 2.5)</summary>
        Melee = 0,
        /// <summary>Дальнобойное оружие — лук/арбалет (дистанция оружия)</summary>
        Ranged = 1,
    }

    /// <summary>
    /// Phase 8 ч.2: экипированное дальнобойное оружие или null.
    /// Дальнобойное = EquipmentData.AttackRange > 2 (фаза 9A: ≤2 ближний, >2 дальний).
    /// </summary>
    public EquipmentData? GetRangedWeapon()
    {
        var weapon = _equipment?.GetEquipped(_player.PlayerId, EquipmentSlot.WeaponMain);
        return weapon != null && weapon.AttackRange > 2 ? weapon : null;
    }

    /// <summary>
    /// Phase 8 ч.2: переключить в режим дальнего боя. Успешен только
    /// при экипированном дальнобойном оружии (иначе переключение не имеет
    /// смысла — стрелять нечем). Возвращает описание для тоста.
    /// </summary>
    public bool SwitchToRangedMode()
    {
        var weapon = GetRangedWeapon();
        if (weapon == null) return false;
        CurrentWeaponMode = WeaponMode.Ranged;
        return true;
    }

    /// <summary>Phase 8 ч.2: переключиться в режим ближнего боя (всегда доступно — кулаки).</summary>
    public void SwitchToMeleeMode()
    {
        CurrentWeaponMode = WeaponMode.Melee;
    }

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

        // Phase 8 ч.2: резолв режима — Ranged требует экипированный лук;
        // если лук сняли после переключения — атака уходит в melee (кулаки/ближнее).
        bool wantRanged = CurrentWeaponMode == WeaponMode.Ranged;
        var rangedWeapon = wantRanged ? GetRangedWeapon() : null;
        bool isRanged = rangedWeapon != null;
        float attackRange = isRanged ? rangedWeapon!.AttackRange : AttackRangeTiles;

        // Phase 8 ч.3: ranged — прицеливание только в ВИДИМЫЕ цели (LOS).
        // Ближайший по дистанции, но за камнем → берём ближайшего видимого.
        string? target = FindNearestTarget(attackRange, isRanged, out int blockedByLos);
        if (target == null)
        {
            // Цели в радиусе ЕСТЬ, но все перекрыты препятствием → тост
            // (не каждый кадр: пауза прицеливания 0.4с = анти-спам).
            if (isRanged && blockedByLos > 0)
            {
                _attackRejectedPub.Publish(new AttackRejectedEvent(
                    _player.PlayerId, "basic_attack",
                    "нет линии огня — препятствие на пути стрелы"));
                _attackCooldownSec = LosRetryCooldownSec;
            }
            return; // Nothing VISIBLE in range — no attack intent.
        }

        // TargetId resolves the attack: CombatModule starts combat and runs
        // the 11-layer damage pipeline on ExecuteAttack.
        // Phase 8 ч.2: isRanged → CombatService резолвит подтип RangedProjectile
        // и урон дальнобойного оружия (§4.2: AGI 2.5% + INT 5%).
        // Phase 8 ч.3: CombatModule-гейт проверит LOS ещё раз (авторитетно
        // для всех источников интентов) и спишет стрелу при прохождении.
        _attackIntentPub.Publish(new AttackIntentEvent(
            _player.PlayerId, target, "basic_attack", isRanged));

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
    /// Target selection (Phase 6): nearest alive NPC within attack range of
    /// the player, Chebyshev distance (matches harvest/interaction reach).
    /// Phase 8 ч.2: range параметризован — 2.5 для melee, AttackRange оружия
    /// для ranged (§10.2: лук — 18 тайлов).
    /// Phase 8 ч.3: requireLos — ranged-прицеливание пропускает цели без
    /// линии огня (CombatLos: Bresenham, блок — дерево/камень); melee без
    /// изменений (ближний бой сквозь препятствие не бывает — дистанция 2.5).
    /// blockedByLos: сколько целей в радиусе отброшено по LOS (для тоста).
    /// </summary>
    private string? FindNearestTarget(float rangeTiles, bool requireLos, out int blockedByLos)
    {
        blockedByLos = 0;
        if (_npcs == null || _player == null) return null;

        var playerPos = _player.Position;
        var nearby = _npcs.GetNearbyNPCIds(playerPos, rangeTiles);
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
            if (dist >= bestDist) continue; // строго ближе — иначе дёшево

            // Phase 8 ч.3: LOS-фильтр (только ranged).
            if (requireLos && !CombatLos.HasLineOfSight(
                    _tiles, playerPos.X, playerPos.Y, npc.Position.X, npc.Position.Y))
            {
                blockedByLos++;
                continue;
            }
            bestDist = dist;
            best = id;
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

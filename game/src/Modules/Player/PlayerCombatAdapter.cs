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
    // 2026-09-11 (аудит боя с животными D1): животные — легитимные цели Space
    // (волк/олень/кролик НЕ регистрируются в NPCService — живут в AnimalService).
    // Без этой инъекции атака по животному молча не находила цель.
    [Inject] private readonly IAnimalService? _animals = null;
    [Inject] private readonly IStatProvider _stats = null!;
    [Inject] private readonly IEquipmentDataProvider _equipment = null!;
    // Phase 8 ч.3: LOS-фильтр цели (паттерн «sanctioned exceptions»)
    [Inject] private readonly ITileService? _tiles = null;
    [Inject] private readonly IPublisher<AttackIntentEvent> _attackIntentPub = null!;
    [Inject] private readonly IPublisher<AttackRejectedEvent> _attackRejectedPub = null!;
    // R16: публикация стойки защиты (клавиша G → DefenseIntentEvent →
    // CombatModule → ICombatService.ExecuteDefense).
    [Inject] private readonly IPublisher<DefenseIntentEvent> _defenseIntentPub = null!;
    [Inject] private readonly ISubscriber<AttackRejectedEvent> _attackRejectedSub = null!;
    // R16-аудит (P3-3): подписки CombatStarted/DamageApplied удалены (пустые
    // обработчики, никто не читал).
    [Inject] private readonly ISubscriber<CombatEndedEvent> _combatEndedSub = null!;

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

    /// <summary>
    /// Review этап 3 (P0-1): бэкофф после ЛЮБОГО отклонения атаки игрока
    /// («не ваш ход» / каст идёт / не участник). Зажатый Space не шлёт
    /// холостые интенты в чужой ход — пауза 0.4с (паттерн LosRetry).
    /// </summary>
    public const float AttackRejectionBackoffSec = 0.4f;

    /// <summary>
    /// R16: анти-спам смены стойки (клавиша G) — повторы не чаще 0.3с
    /// (защита от автоповторов клавиатуры; каждая смена — событие + тост).
    /// </summary>
    public const float DefenseSwitchCooldownSec = 0.3f;

    private float _attackCooldownSec;

    // === R16 (2026-09-10): стойка защиты игрока (клавиша G) ===

    /// <summary>
    /// R16: текущая защитная стойка игрока (D5 «мёртвая проводка» —
    /// IsDefendPressed существовал, но не имел ни клавиши, ни потребителя).
    /// Цикл: None → Dodge → Parry (если оружие) → Shield (если щит) → None.
    /// Публикуется DefenseIntentEvent; CombatService применяет стойку к
    /// СЛЕДУЮЩЕЙ входящей атаке (слой активной защиты §7).
    /// </summary>
    public DefenseSubtype CurrentDefenseStance { get; private set; } = DefenseSubtype.None;

    private float _defenseSwitchCooldownSec;

    // === Phase 8 ч.2 (2026-09-03): режим оружия (клавиши 1/2) ===

    /// <summary>Режим оружия игрока: Melee (кулаки/ближнее) или Ranged (лук).</summary>
    public WeaponMode CurrentWeaponMode { get; private set; } = WeaponMode.Melee;

    /// <summary>
    /// 2026-09-04 S1: остаток кулдауна атаки (сек) — для HUD-индикатора
    /// боевой готовности (GameWorldController). 0 = удар доступен.
    /// </summary>
    public float AttackCooldownRemaining => _attackCooldownSec > 0f ? _attackCooldownSec : 0f;

    private IDisposable? _combatEndedToken;
    private IDisposable? _attackRejectedToken;

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
        // R16-аудит (P3-3): подписки OnCombatStarted/OnDamageApplied удалены —
        // пустые обработчики («handled by PlayerService» — не соответствует
        // коду: PlayerService на эти события не подписан).
        // CombatEnded нужна: сброс стойки игрока при конце боя (P3-2).
        _combatEndedToken = _combatEndedSub.Subscribe(OnCombatEnded);
        // Review этап 3 (P0-1): бэкофф-подписка на отклонения атак игрока.
        _attackRejectedToken = _attackRejectedSub.Subscribe(OnAttackRejected);
    }

    /// <summary>
    /// Review этап 3 (P0-1): отклонение атаки игрока (ход противника / каст /
    /// не участник) → короткий бэкофф автоатаки. Space в чужой ход больше не
    /// шлёт интент каждые 0.91с (тишина вместо спама тостов «не ваш ход»;
    /// ToastStack агрегирует повторы «×N», но лучше не создавать их вовсе).
    /// </summary>
    private void OnAttackRejected(in AttackRejectedEvent e)
    {
        if (CultivationGame.Core.Helpers.PlayerIdResolver.IsPlayer(e.AttackerId)
            && _attackCooldownSec < AttackRejectionBackoffSec)
        {
            _attackCooldownSec = AttackRejectionBackoffSec;
        }
    }

    public void Tick(float deltaTime)
    {
        // R16: смена стойки защиты (G) — до кулдауна атаки: защита не
        // блокируется замахом (клавиши независимы; ранний return ниже
        // не должен глотать защитный ввод).
        TickDefenseStance(deltaTime);

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

    // === R16: стойка защиты (клавиша G) ===

    /// <summary>
    /// R16: тик смены стойки. Клавиша G циклирует доступные стойки:
    /// None → Dodge → Parry (WeaponMain) → Shield (WeaponOff-щит) → None.
    /// Стойка публикуется DefenseIntentEvent → CombatModule →
    /// ExecuteDefense; применяется к СЛЕДУЮЩЕЙ атаке по игроку.
    /// Позиция в цикле вычисляется от ТЕКУЩЕЙ экипировки (снял оружие —
    /// Parry исчезнет из цикла, стойка сбросится на следующее нажатие).
    /// </summary>
    private void TickDefenseStance(float deltaTime)
    {
        if (_defenseSwitchCooldownSec > 0f)
        {
            _defenseSwitchCooldownSec -= deltaTime;
            if (_defenseSwitchCooldownSec > 0f) return;
        }
        if (!_input.IsDefendPressed) return;

        _defenseSwitchCooldownSec = DefenseSwitchCooldownSec;

        // Доступные стойки: Dodge — всегда; Parry — с оружием;
        // Shield — со щитом (WeaponOff). None — начало цикла.
        bool hasWeapon = _equipment?.GetEquipped(_player.PlayerId, EquipmentSlot.WeaponMain) != null;
        bool hasShield = _equipment?.GetEquipped(_player.PlayerId, EquipmentSlot.WeaponOff) != null;

        DefenseSubtype next = CurrentDefenseStance switch
        {
            DefenseSubtype.None => DefenseSubtype.Dodge,
            DefenseSubtype.Dodge => hasWeapon ? DefenseSubtype.Parry
                                   : (hasShield ? DefenseSubtype.Shield : DefenseSubtype.None),
            DefenseSubtype.Parry => hasShield ? DefenseSubtype.Shield : DefenseSubtype.None,
            _ => DefenseSubtype.None,
        };
        CurrentDefenseStance = next;

        _defenseIntentPub.Publish(new DefenseIntentEvent(_player.PlayerId, next));
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
    /// 2026-09-11 (аудит D1): кандидаты = NPC ∪ ЖИВОТНЫЕ (AnimalService) —
    /// строго ближайший живой. Животные видны тем же Chebyshev/LOS-фильтром;
    /// ДИКОЙ разрыв был: волки не регистрируются в NPCService → Space молча
    /// возвращал «нет цели» (жалоба пользователя: «бегу с посохом за волком —
    /// результата 0»).
    /// </summary>
    private string? FindNearestTarget(float rangeTiles, bool requireLos, out int blockedByLos)
    {
        blockedByLos = 0;
        if (_npcs == null || _player == null) return null;

        var playerPos = _player.Position;

        string? best = null;
        int bestDist = int.MaxValue;
        int blocked = 0; // локальный счётчик (out-параметр нельзя в локальной функции)

        // Проверка одного кандидата: дистанция → строго ближе → LOS (ranged).
        void Consider(string id, Position2D pos)
        {
            int dist = Math.Max(
                Math.Abs(pos.X - playerPos.X),
                Math.Abs(pos.Y - playerPos.Y));
            if (dist >= bestDist) return; // строго ближе — иначе дёшево

            if (requireLos && !CombatLos.HasLineOfSight(
                    _tiles, playerPos.X, playerPos.Y, pos.X, pos.Y))
            {
                blocked++;
                return;
            }
            bestDist = dist;
            best = id;
        }

        // 1. NPC-кандидаты (реестр NPCService).
        var nearby = _npcs.GetNearbyNPCIds(playerPos, rangeTiles);
        if (nearby != null)
        {
            foreach (var id in nearby)
            {
                var npc = _npcs.GetNPC(id);
                if (npc == null || !_npcs.IsAlive(id)) continue;
                Consider(id, npc.Position);
            }
        }

        // 2. Животные-кандидаты (D1): волк/олень/кролик — живые, в радиусе.
        if (_animals != null)
        {
            foreach (var animal in _animals.GetAliveAnimalsInRange(playerPos, rangeTiles))
            {
                Consider(animal.EntityId, animal.Position);
            }
        }

        blockedByLos = blocked;
        return best;
    }

    /// <summary>
    /// QA (AnimalCombatSimDebug): ближайшая цель Space-таргетинга —
    /// NPC ∪ ЖИВОТНЫЕ — БЕЗ ввода (головной прогон таргетинга D1:
    /// волк должен резолвиться так же, как человек-NPC).
    /// </summary>
    public string? DebugFindNearestTarget(float rangeTiles = AttackRangeTiles)
        => FindNearestTarget(rangeTiles, false, out _);

    private void OnCombatEnded(in CombatEndedEvent e)
    {
        // R16-аудит (P3-2): CombatService.EndCombat сбрасывает свою стойку
        // (_lastPlayerDefense=None), но адаптерский CurrentDefenseStance жил
        // дальше — после нового боя G-цикл шёл от «призрачной» стойки, а
        // PlayerModule гейтил действия по рассинхронизированному значению.
        // Сброс здесь синхронизирует адаптер с пайплайном.
        CurrentDefenseStance = DefenseSubtype.None;
    }

    public void Dispose()
    {
        _combatEndedToken?.Dispose();
        _attackRejectedToken?.Dispose();
        _combatEndedToken = null;
        _attackRejectedToken = null;
    }
}

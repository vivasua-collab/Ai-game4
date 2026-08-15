#nullable enable
// Создано: 2026-05-18 12:00:00 UTC — П.23/Этап 7: Дебаффы от потери частей тела
// При ампутации части тела — штраф к характеристикам через IBuffService.
// При приживлении — восстановление стата (снятие дебаффа).
// INT НЕ получает дебаффов от состояния тела.
// Migrated from Ai-game3 (Unity+MessagePipe+VContainer) to Ai-game4 (Godot+EventBus+DI) 2026-08-15:
//   - using MessagePipe → using CultivationGame.Core.Events
//   - using VContainer.Unity → using CultivationGame.Core.Interfaces (IStartable)
//   - IStartable.Start() → Start() (still IStartable, just method)
//   - Handler signature: void OnXxx(XxxEvent e) → void OnXxx(in XxxEvent e)
//   - UnityEngine.Debug.Log → Console.WriteLine
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Body;

/// <summary>
/// Система дебаффов от потери частей тела (П.23, Этап 7).
///
/// Правила:
/// - При BodyPartSeveredEvent → наложить дебафф на соответствующий стат
/// - При приживлении (BodyPartReattachedEvent) → снять дебафф
/// - INT НЕ зависит от состояния тела — даже безрукый практик сохраняет интеллект
/// - Дебаффы реализованы через IBuffService (постоянные, Permanent)
///
/// Таблица штрафов:
/// Рука (Arm)      → STR × 0.85 (-15%)
/// Кисть (Hand)    → AGI × 0.90 (-10%)
/// Нога (Leg)      → AGI × 0.80 (-20%)
/// Ступня (Foot)   → AGI × 0.95 (-5%)
/// Торс (Torso)    → Все физ. × 0.50 (-50%) — STR, AGI, VIT
/// Крыло (Wing)    → AGI × 0.70 (-30%)
/// </summary>
public sealed class SeveredDebuffSystem : IStartable, IDisposable
{
    // === DI-зависимости ===
    private readonly ISubscriber<BodyPartSeveredEvent> _severedSub;
    private readonly ISubscriber<BodyPartReattachedEvent> _reattachedSub;
    private readonly IBuffService _buffService;

    // === Подписки ===
    private IDisposable? _severedSubscription;
    private IDisposable? _reattachedSubscription;

    // === Трекинг активных дебаффов ===
    // Ключ: (entityId, partType) → список buffId для снятия
    private readonly Dictionary<(string, BodyPartType), List<string>> _activeDebuffs = new();

    // === Маппинг: BodyPartType → дебаффы ===
    private static readonly Dictionary<BodyPartType, SeveredDebuffDef[]> DebuffTable = new()
    {
        // Рука → STR -15%
        { BodyPartType.LeftArm, new[]
        {
            new SeveredDebuffDef("severed_left_arm_str", BuffType.AttackReduction, StatType.Strength, -0.15f)
        }},
        { BodyPartType.RightArm, new[]
        {
            new SeveredDebuffDef("severed_right_arm_str", BuffType.AttackReduction, StatType.Strength, -0.15f)
        }},

        // Кисть → AGI -10%
        { BodyPartType.LeftHand, new[]
        {
            new SeveredDebuffDef("severed_left_hand_agi", BuffType.SpeedReduction, StatType.Agility, -0.10f)
        }},
        { BodyPartType.RightHand, new[]
        {
            new SeveredDebuffDef("severed_right_hand_agi", BuffType.SpeedReduction, StatType.Agility, -0.10f)
        }},

        // Нога → AGI -20%
        { BodyPartType.LeftLeg, new[]
        {
            new SeveredDebuffDef("severed_left_leg_agi", BuffType.SpeedReduction, StatType.Agility, -0.20f)
        }},
        { BodyPartType.RightLeg, new[]
        {
            new SeveredDebuffDef("severed_right_leg_agi", BuffType.SpeedReduction, StatType.Agility, -0.20f)
        }},

        // Ступня → AGI -5%
        { BodyPartType.LeftFoot, new[]
        {
            new SeveredDebuffDef("severed_left_foot_agi", BuffType.SpeedReduction, StatType.Agility, -0.05f)
        }},
        { BodyPartType.RightFoot, new[]
        {
            new SeveredDebuffDef("severed_right_foot_agi", BuffType.SpeedReduction, StatType.Agility, -0.05f)
        }},

        // Торс → Все физические статы -50%
        { BodyPartType.Torso, new[]
        {
            new SeveredDebuffDef("severed_torso_str", BuffType.AttackReduction, StatType.Strength, -0.50f),
            new SeveredDebuffDef("severed_torso_agi", BuffType.SpeedReduction, StatType.Agility, -0.50f),
            new SeveredDebuffDef("severed_torso_vit", BuffType.DefenseReduction, StatType.Vitality, -0.50f)
        }},

        // Крыло → AGI -30% (потеря полёта)
        { BodyPartType.LeftWing, new[]
        {
            new SeveredDebuffDef("severed_left_wing_agi", BuffType.SpeedReduction, StatType.Agility, -0.30f)
        }},
        { BodyPartType.RightWing, new[]
        {
            new SeveredDebuffDef("severed_right_wing_agi", BuffType.SpeedReduction, StatType.Agility, -0.30f)
        }},

        // Четвероногие ноги
        { BodyPartType.FrontLeftLeg, new[]
        {
            new SeveredDebuffDef("severed_fl_leg_agi", BuffType.SpeedReduction, StatType.Agility, -0.15f)
        }},
        { BodyPartType.FrontRightLeg, new[]
        {
            new SeveredDebuffDef("severed_fr_leg_agi", BuffType.SpeedReduction, StatType.Agility, -0.15f)
        }},
        { BodyPartType.BackLeftLeg, new[]
        {
            new SeveredDebuffDef("severed_bl_leg_agi", BuffType.SpeedReduction, StatType.Agility, -0.15f)
        }},
        { BodyPartType.BackRightLeg, new[]
        {
            new SeveredDebuffDef("severed_br_leg_agi", BuffType.SpeedReduction, StatType.Agility, -0.15f)
        }},

        // Хвост → AGI -5% (потеря баланса)
        { BodyPartType.Tail, new[]
        {
            new SeveredDebuffDef("severed_tail_agi", BuffType.SpeedReduction, StatType.Agility, -0.05f)
        }},
        { BodyPartType.BirdTail, new[]
        {
            new SeveredDebuffDef("severed_birdtail_agi", BuffType.SpeedReduction, StatType.Agility, -0.05f)
        }},

        // Змеиные сегменты → AGI -10% каждый
        { BodyPartType.BodySegment1, new[]
        {
            new SeveredDebuffDef("severed_seg1_agi", BuffType.SpeedReduction, StatType.Agility, -0.10f)
        }},
        { BodyPartType.BodySegment2, new[]
        {
            new SeveredDebuffDef("severed_seg2_agi", BuffType.SpeedReduction, StatType.Agility, -0.10f)
        }},

        // Членистоногие: ноги → AGI -5% каждая
        { BodyPartType.Leg1, new[] { new SeveredDebuffDef("severed_leg1_agi", BuffType.SpeedReduction, StatType.Agility, -0.05f) } },
        { BodyPartType.Leg2, new[] { new SeveredDebuffDef("severed_leg2_agi", BuffType.SpeedReduction, StatType.Agility, -0.05f) } },
        { BodyPartType.Leg3, new[] { new SeveredDebuffDef("severed_leg3_agi", BuffType.SpeedReduction, StatType.Agility, -0.05f) } },
        { BodyPartType.Leg4, new[] { new SeveredDebuffDef("severed_leg4_agi", BuffType.SpeedReduction, StatType.Agility, -0.05f) } },
        { BodyPartType.Leg5, new[] { new SeveredDebuffDef("severed_leg5_agi", BuffType.SpeedReduction, StatType.Agility, -0.05f) } },
        { BodyPartType.Leg6, new[] { new SeveredDebuffDef("severed_leg6_agi", BuffType.SpeedReduction, StatType.Agility, -0.05f) } },
        { BodyPartType.Leg7, new[] { new SeveredDebuffDef("severed_leg7_agi", BuffType.SpeedReduction, StatType.Agility, -0.05f) } },
        { BodyPartType.Leg8, new[] { new SeveredDebuffDef("severed_leg8_agi", BuffType.SpeedReduction, StatType.Agility, -0.05f) } },

        // Педипальпы/Хелицеры → STR -10%
        { BodyPartType.Pedipalps, new[]
        {
            new SeveredDebuffDef("severed_pedipalps_str", BuffType.AttackReduction, StatType.Strength, -0.10f)
        }},
        { BodyPartType.Chelicerae, new[]
        {
            new SeveredDebuffDef("severed_chelicerae_str", BuffType.AttackReduction, StatType.Strength, -0.10f)
        }},

        // Головогрудь (P1-07 FIX) — аналог Torso для членистоногих
        { BodyPartType.Cephalothorax, new[]
        {
            new SeveredDebuffDef("severed_cephalothorax_str", BuffType.AttackReduction, StatType.Strength, -0.50f),
            new SeveredDebuffDef("severed_cephalothorax_agi", BuffType.SpeedReduction, StatType.Agility, -0.50f),
            new SeveredDebuffDef("severed_cephalothorax_vit", BuffType.DefenseReduction, StatType.Vitality, -0.50f)
        }},

        // Брюшко (P1-01 V2 FIX) — Digestion у членистоногих, потеря → VIT -20%
        { BodyPartType.Abdomen, new[]
        {
            new SeveredDebuffDef("severed_abdomen_vit", BuffType.DefenseReduction, StatType.Vitality, -0.20f)
        }},
    };

    public SeveredDebuffSystem(
        ISubscriber<BodyPartSeveredEvent> severedSub,
        ISubscriber<BodyPartReattachedEvent> reattachedSub,
        IBuffService buffService)
    {
        _severedSub = severedSub ?? throw new ArgumentNullException(nameof(severedSub));
        _reattachedSub = reattachedSub ?? throw new ArgumentNullException(nameof(reattachedSub));
        _buffService = buffService ?? throw new ArgumentNullException(nameof(buffService));
    }

    public void Start()
    {
        _severedSubscription = _severedSub.Subscribe(OnPartSevered);
        _reattachedSubscription = _reattachedSub.Subscribe(OnPartReattached);
    }

    /// <summary>
    /// Обработчик ампутации части тела.
    /// EventBus handler signature: void OnXxx(in XxxEvent e).
    /// </summary>
    private void OnPartSevered(in BodyPartSeveredEvent e)
    {
        if (!DebuffTable.TryGetValue(e.Part, out var debuffs)) return;

        var key = (e.EntityId, e.Part);
        if (!_activeDebuffs.TryGetValue(key, out var buffIds))
        {
            buffIds = new List<string>();
            _activeDebuffs[key] = buffIds;
        }

        foreach (var def in debuffs)
        {
            // П.23: INT НЕ получает дебаффов от состояния тела
            if (def.AffectedStat == StatType.Intelligence) continue;

            // Накладываем дебафф через IBuffService
            // Permanent = длится пока не снят явно
            _buffService.ApplyBuff(e.EntityId, def.BuffId, duration: -1f, potency: def.Potency);
            buffIds.Add(def.BuffId);
        }

        Console.WriteLine($"[SeveredDebuffSystem] Ампутация {e.Part} → наложено {buffIds.Count} дебаффов для {e.EntityId}");
    }

    /// <summary>
    /// Обработчик приживления конечности.
    /// </summary>
    private void OnPartReattached(in BodyPartReattachedEvent e)
    {
        var key = (e.EntityId, e.Part);
        if (!_activeDebuffs.TryGetValue(key, out var buffIds)) return;

        foreach (var buffId in buffIds)
        {
            _buffService.RemoveBuff(e.EntityId, buffId);
        }

        // P1-01 FIX: сохраняем count до Clear
        int removedCount = buffIds.Count;
        buffIds.Clear();
        _activeDebuffs.Remove(key);

        Console.WriteLine($"[SeveredDebuffSystem] Приживление {e.Part} → снято {removedCount} дебаффов для {e.EntityId}");
    }

    public void Dispose()
    {
        _severedSubscription?.Dispose();
        _severedSubscription = null;
        _reattachedSubscription?.Dispose();
        _reattachedSubscription = null;
    }

    /// <summary>
    /// Определение дебаффа при ампутации части тела.
    /// </summary>
    private readonly struct SeveredDebuffDef
    {
        public readonly string BuffId;
        public readonly BuffType BuffType;
        public readonly StatType AffectedStat;
        public readonly float Potency; // Отрицательное значение = штраф

        public SeveredDebuffDef(string buffId, BuffType buffType, StatType affectedStat, float potency)
        {
            BuffId = buffId;
            BuffType = buffType;
            AffectedStat = affectedStat;
            Potency = potency;
        }
    }
}

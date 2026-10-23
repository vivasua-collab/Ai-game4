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
public sealed class SeveredDebuffSystem : IStartable, IWorldResettable, IPostLoadSync, IDisposable
{
    // === DI-зависимости ===
    private readonly ISubscriber<BodyPartSeveredEvent> _severedSub;
    private readonly ISubscriber<BodyPartReattachedEvent> _reattachedSub;
    private readonly IBuffService _buffService;
    // ST11-007 (Этап-11 аудита): тело для пост-загрузочной пересборки дебаффов
    // игрока (внутримодульная зависимость Body — легальна).
    private readonly IBodyService? _bodyService;

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
        IBuffService buffService,
        IBodyService? bodyService = null)
    {
        _severedSub = severedSub ?? throw new ArgumentNullException(nameof(severedSub));
        _reattachedSub = reattachedSub ?? throw new ArgumentNullException(nameof(reattachedSub));
        _buffService = buffService ?? throw new ArgumentNullException(nameof(buffService));
        _bodyService = bodyService;
    }

        // P1-10 (аудит 09.22, Фазы 7/10): идемпотентный Start — повторный вызов
    // (прямой вызов вне GameEntryPoint / повторная инстанциация сцены) не
    // дублирует подписки/инициализацию. Флаг — только при УСПЕШНОМ завершении
    // (провал остаётся ретраимым). Основной слой защиты — GameEntryPoint
    // (_startedModules, resume from failure); это страховка от прямых вызовов.
    private bool _startCompleted;

    public void Start()
    {
        if (_startCompleted) return;

        _severedSubscription = _severedSub.Subscribe(OnPartSevered);
        _reattachedSubscription = _reattachedSub.Subscribe(OnPartReattached);
    
        _startCompleted = true;
    }

    /// <summary>
    /// Обработчик ампутации части тела.
    /// EventBus handler signature: void OnXxx(in XxxEvent e).
    /// ST11-007: идемпотентен — повторное событие для УЖЕ отслеживаемой
    /// (entity, part) не применяет дебаффы второй раз (ключ активен).
    /// </summary>
    private void OnPartSevered(in BodyPartSeveredEvent e)
    {
        if (!DebuffTable.TryGetValue(e.Part, out var debuffs)) return;

        var key = (e.EntityId, e.Part);
        if (_activeDebuffs.ContainsKey(key))
        {
            Console.WriteLine($"[SeveredDebuffSystem] Повторное событие ампутации {e.Part} для {e.EntityId} — дебаффы уже активны, пропуск");
            return;
        }

        ApplyDebuffs(key, debuffs);
    }

    /// <summary>Применить таблицу дебаффов ампутации и зарегистрировать ключ.</summary>
    private void ApplyDebuffs((string EntityId, BodyPartType Part) key, SeveredDebuffDef[] debuffs)
    {
        var buffIds = new List<string>();
        _activeDebuffs[key] = buffIds;

        foreach (var def in debuffs)
        {
            // П.23: INT НЕ получает дебаффов от состояния тела
            if (def.AffectedStat == StatType.Intelligence) continue;

            // Накладываем дебафф через IBuffService
            // Permanent = длится пока не снят явно
            _buffService.ApplyBuff(key.EntityId, def.BuffId, duration: -1f, potency: def.Potency);
            buffIds.Add(def.BuffId);
        }

        Console.WriteLine($"[SeveredDebuffSystem] Ампутация {key.Part} → наложено {buffIds.Count} дебаффов для {key.EntityId}");
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

    // ══════════════════════════════════════════════════════════════
    // ST11-002/ST11-007 (Этап-11 аудита, 23.10): reset-контракт и
    // пост-загрузочная синхронизация.
    //
    // Префикс-дефекты: (1) _activeDebuffs переживали New Game (ключи
    // (entity, part) накапливались — BuffService.ResetWorld чистил сами
    // баффы, но учёт системы оставался); (2) после LoadGame
    // RestoreState блока "body" не публикует BodyPartSeveredEvent →
    // ампутационные дебаффы игрока пропадали при загрузке (тело
    // ампутировано, штрафа нет).
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// IWorldResettable: пересборка мира = пустой учёт (сами баффы чистит
    /// BuffService.ResetWorld; тело пересоздаёт BodyService.ResetWorld).
    /// </summary>
    public void ResetWorld()
    {
        if (_activeDebuffs.Count > 0)
        {
            _activeDebuffs.Clear();
            Console.WriteLine("[SeveredDebuffSystem] ResetWorld: учёт ампутационных дебаффов очищен (New Game)");
        }
    }

    /// <summary>
    /// IPostLoadSync: тело восстановлено из сейва — повторно применить
    /// ампутационные дебаффы игрока ровно один раз (идемпотентно по ключу).
    /// NPC-сторона: перечисление сущностей тела не экспортировано —
    /// резидуальный GAP (NPC-баффы не переживают загрузку в принципе:
    /// BuffService не ISaveable).
    /// </summary>
    public void OnAfterLoad()
    {
        if (_bodyService == null) return;

        int applied = 0;
        foreach (var p in _bodyService.GetAllParts())
        {
            if (p.State != BodyPartState.Severed) continue;
            if (!DebuffTable.TryGetValue(p.Type, out var debuffs)) continue;

            var key = (_bodyService.EntityId, p.Type);
            if (_activeDebuffs.ContainsKey(key)) continue; // идемпотентность

            ApplyDebuffs(key, debuffs);
            applied++;
        }
        if (applied > 0)
            Console.WriteLine($"[SeveredDebuffSystem] OnAfterLoad: повторно применены дебаффы {applied} ампутаций игрока");
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

#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Messaging.Contracts;

// Created: 2026-05-08 (Ai-game3) — migrated 2026-08-15.
// Body / damage contracts: damage to body parts, severing, healing, reattachment, critical state.
// Kenshi-style dual HP (Red HP = current damage, Long HP = permanent damage).
// P1-07 (V3) FIX: StateChanged flag in BodyPartDamagedEvent.
// P2-04 (V3) FIX: +CurrentRedHP, +MaxRedHP for UI (HP-bar without extra request).
// P2-05 (V3) FIX: +IsVital in BodyPartSeveredEvent.

// BodyPartState is defined in CultivationGame.Core.Data (canonical).

/// <summary>
/// Событие повреждения части тела.
/// P1-07 (V3) FIX: публикуется при ЛЮБОМ применённом уроне (не только при смене состояния).
/// StateChanged = true если состояние изменилось (Healthy→Bruised, Wounded→Disabled и т.д.).
/// P2-04 (V3) FIX: +CurrentRedHP, +MaxRedHP для UI (HP-бар без доп. запроса).
/// </summary>
public readonly struct BodyPartDamagedEvent
{
    public readonly string EntityId;
    public readonly BodyPartType Part;
    public readonly int Damage;
    public readonly BodyPartState NewState;
    public readonly bool StateChanged;       // P1-07 (V3) FIX
    public readonly int CurrentRedHP;        // P2-04 (V3) FIX
    public readonly int MaxRedHP;            // P2-04 (V3) FIX

    // Обратная совместимость: конструктор без StateChanged/HP
    public BodyPartDamagedEvent(string entityId, BodyPartType part, int damage, BodyPartState newState)
        { EntityId = entityId; Part = part; Damage = damage; NewState = newState; StateChanged = true; CurrentRedHP = 0; MaxRedHP = 0; }

    // Полный конструктор (P1-07 + P2-04 FIX)
    public BodyPartDamagedEvent(string entityId, BodyPartType part, int damage, BodyPartState newState, bool stateChanged, int currentRedHP, int maxRedHP)
        { EntityId = entityId; Part = part; Damage = damage; NewState = newState; StateChanged = stateChanged; CurrentRedHP = currentRedHP; MaxRedHP = maxRedHP; }
}

/// <summary>
/// Событие отрубления части тела.
/// BlockedSlots — массив заблокированных слотов экипировки (обычно 1-3).
/// P2-05 (V3) FIX: +IsVital — vital-часть ампутирована = более критичное событие.
/// Используем EquipmentSlot[] вместо IReadOnlyList для совместимости
/// с zero-GC контрактом: массив — ссылочный тип, но аллоцируется
/// один раз при создании события, без интерфейсной диспетчеризации.
/// </summary>
public readonly struct BodyPartSeveredEvent
{
    public readonly string EntityId;
    public readonly BodyPartType Part;
    public readonly EquipmentSlot[] BlockedSlots;
    public readonly bool IsVital;  // P2-05 (V3) FIX

    // Обратная совместимость: конструктор без IsVital
    public BodyPartSeveredEvent(string entityId, BodyPartType part, EquipmentSlot[] blockedSlots)
        { EntityId = entityId; Part = part; BlockedSlots = blockedSlots; IsVital = false; }

    // Полный конструктор (P2-05 FIX)
    public BodyPartSeveredEvent(string entityId, BodyPartType part, EquipmentSlot[] blockedSlots, bool isVital)
        { EntityId = entityId; Part = part; BlockedSlots = blockedSlots; IsVital = isVital; }
}

/// <summary>
/// Событие исцеления части тела.
/// P2-04 (V3) FIX: +CurrentRedHP, +MaxRedHP для UI (HP-бар без доп. запроса).
/// </summary>
public readonly struct BodyPartHealedEvent
{
    public readonly string EntityId;
    public readonly BodyPartType Part;
    public readonly int Amount;
    public readonly int CurrentRedHP;  // P2-04 (V3) FIX
    public readonly int MaxRedHP;      // P2-04 (V3) FIX

    // Обратная совместимость: конструктор без HP
    public BodyPartHealedEvent(string entityId, BodyPartType part, int amount)
        { EntityId = entityId; Part = part; Amount = amount; CurrentRedHP = 0; MaxRedHP = 0; }

    // Полный конструктор (P2-04 FIX)
    public BodyPartHealedEvent(string entityId, BodyPartType part, int amount, int currentRedHP, int maxRedHP)
        { EntityId = entityId; Part = part; Amount = amount; CurrentRedHP = currentRedHP; MaxRedHP = maxRedHP; }
}

/// <summary>
/// Событие приживления конечности (П.23 Этап 7).
/// Публикуется когда ампутированная часть тела восстанавливается
/// (магическое лечение, регенерация практика и т.д.).
/// Снимает дебаффы от ампутации.
/// </summary>
public readonly struct BodyPartReattachedEvent
{
    public readonly string EntityId;
    public readonly BodyPartType Part;
    public BodyPartReattachedEvent(string entityId, BodyPartType part)
        { EntityId = entityId; Part = part; }
}

/// <summary>
/// Событие: критическое состояние тела (P2-07 FIX).
/// Публикуется когда жизненно важная часть (IsVital=true) переходит в Disabled.
/// Используется для:
/// - AI: NPC отступает при критическом состоянии
/// - UI: предупреждение о критическом состоянии
/// - Combat: изменение стратегии боя
/// </summary>
public readonly struct BodyCriticalEvent
{
    public readonly string EntityId;
    public readonly BodyPartType Part;
    public readonly BodyPartState State;  // Disabled — причина события
    public readonly float HealthRatio;    // 0.0 при Disabled
    public BodyCriticalEvent(string entityId, BodyPartType part, BodyPartState state, float healthRatio)
        { EntityId = entityId; Part = part; State = state; HealthRatio = healthRatio; }
}

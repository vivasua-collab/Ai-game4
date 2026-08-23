#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Messaging.Contracts;

// Created: 2026-05-08 (Ai-game3) — migrated 2026-08-15.
// Combat contracts: combat start/end, damage application, techniques, attack intent.
// Спринт 7 C8: Element field in DamageAppliedEvent.
// P2-7.3 FIX: AttackSubtype field in DamageAppliedEvent.

// Element, CombatAttackResult, CombatSubtype are defined in CultivationGame.Core.Data (canonical).

// === БОЙ ===
public readonly struct CombatStartedEvent
{
    public readonly string InstigatorId;
    public readonly string TargetId;
    public CombatStartedEvent(string instigatorId, string targetId)
        { InstigatorId = instigatorId; TargetId = targetId; }
}

public readonly struct CombatEndedEvent
{
    public readonly string WinnerId;
    public readonly string LoserId;
    public readonly bool Victory;
    public CombatEndedEvent(string winnerId, string loserId, bool victory)
        { WinnerId = winnerId; LoserId = loserId; Victory = victory; }
}

public readonly struct DamageAppliedEvent
{
    public readonly string SourceId;
    public readonly string TargetId;
    public readonly int Damage;
    public readonly DamageType Type;
    public readonly Element Element;           // Спринт 7 C8: стихия атаки
    public readonly BodyPartType HitPart;
    public readonly CombatAttackResult Result;
    public readonly CombatSubtype AttackSubtype; // P2-7.3 FIX: подтип атаки (для кровотечения: slashing/piercing vs blunt)

    /// <summary>
    /// Обратная совместимость — без Element.
    /// </summary>
    public DamageAppliedEvent(string sourceId, string targetId, int damage, DamageType type,
        BodyPartType hitPart, CombatAttackResult result)
        : this(sourceId, targetId, damage, type, Element.Neutral, hitPart, result, CombatSubtype.None) { }

    /// <summary>
    /// Обратная совместимость — с Element, без AttackSubtype.
    /// </summary>
    public DamageAppliedEvent(string sourceId, string targetId, int damage, DamageType type,
        Element element, BodyPartType hitPart, CombatAttackResult result)
        : this(sourceId, targetId, damage, type, element, hitPart, result, CombatSubtype.None) { }

    /// <summary>
    /// Полный конструктор с Element и AttackSubtype.
    /// Спринт 7 C8: Element для стихийных эффектов.
    /// P2-7.3 FIX: AttackSubtype для различения slashing/piercing от blunt.
    /// </summary>
    public DamageAppliedEvent(string sourceId, string targetId, int damage, DamageType type,
        Element element, BodyPartType hitPart, CombatAttackResult result, CombatSubtype attackSubtype)
    {
        SourceId = sourceId;
        TargetId = targetId;
        Damage = damage;
        Type = type;
        Element = element;
        HitPart = hitPart;
        Result = result;
        AttackSubtype = attackSubtype;
    }
}

public readonly struct TechniqueUsedEvent
{
    public readonly string UserId;
    public readonly string TechniqueId;
    /// <summary>Стоимость Ци (Фаза 9D: float→int, ЗАПРЕТ 3.9)</summary>
    public readonly int QiCost;
    public TechniqueUsedEvent(string userId, string techniqueId, int qiCost)
        { UserId = userId; TechniqueId = techniqueId; QiCost = qiCost; }
}

// === Этап 2 внедрения ЦИ (2026-08-23): слоты техник игрока (TECHNIQUE_SYSTEM.md §12) ===

/// <summary>
/// Событие: игрок изучил технику (слот занят).
/// Публикуется TechniqueService.LearnTechnique. Потребители: TechniquesPanel.
/// </summary>
public readonly struct TechniqueLearnedEvent
{
    public readonly string TechniqueId;
    public readonly string Name;
    public readonly Core.Data.TechniqueType Type;
    public readonly Core.Data.TechniqueGrade Grade;
    public TechniqueLearnedEvent(string techniqueId, string name,
        Core.Data.TechniqueType type, Core.Data.TechniqueGrade grade)
        { TechniqueId = techniqueId; Name = name; Type = type; Grade = grade; }
}

/// <summary>
/// Событие: игрок забыл/потерял технику (слот освобождён).
/// </summary>
public readonly struct TechniqueForgottenEvent
{
    public readonly string TechniqueId;
    public TechniqueForgottenEvent(string techniqueId)
        { TechniqueId = techniqueId; }
}

/// <summary>
/// Событие: изменился выбор активной техники игрока.
/// Публикуется TechniqueService.SelectTechnique. Потребители: HUD/панель техник.
/// </summary>
public readonly struct TechniqueSelectionChangedEvent
{
    public readonly string TechniqueId; // null/empty — выбор сброшен
    public TechniqueSelectionChangedEvent(string techniqueId)
        { TechniqueId = techniqueId ?? string.Empty; }
}

public readonly struct EnemyKilledEvent
{
    public readonly string EnemyId;
    public EnemyKilledEvent(string enemyId) { EnemyId = enemyId; }
}

/// <summary>
/// Намерение атаки — триггерный контракт (до расчёта урона).
/// Фаза 9A: публикуется PlayerCombatAdapter при ЛКМ/J атаке.
/// CombatModule подписан → CombatService.ExecuteAttack().
/// TechniqueUsedEvent — информационный (после расчёта), это два разных контракта!
/// </summary>
public readonly struct AttackIntentEvent
{
    /// <summary>ID атакующего</summary>
    public readonly string AttackerId;

    /// <summary>ID цели (null если авто-выбор ближайшего)</summary>
    public readonly string TargetId;

    /// <summary>ID техники ("basic_attack" для базовой)</summary>
    public readonly string TechniqueId;

    /// <summary>Дальняя атака? (attackRange > 2)</summary>
    public readonly bool IsRanged;

    public AttackIntentEvent(string attackerId, string targetId,
        string techniqueId, bool isRanged)
    {
        AttackerId = attackerId;
        TargetId = targetId;
        TechniqueId = techniqueId;
        IsRanged = isRanged;
    }
}

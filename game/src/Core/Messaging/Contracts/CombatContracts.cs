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
/// Публикуется TechniqueService.LearnTechnique. Потребители: TechniqueBookWindow.
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

/// <summary>
/// Команда: игрок запросил каст техники (этап 2 внедрения ЦИ).
/// Публикуется Adapter (клавиша Z / клик в панели техник).
/// PlayerTechniqueCaster подписан: проверка Ци/кулдауна → эффект по типу.
/// </summary>
public readonly struct TechniqueCastRequestedEvent
{
    public readonly string TechniqueId;
    public readonly int TargetMouseX;      // позиция курсора (милли-пиксели) для направления
    public readonly int TargetMouseY;
    public TechniqueCastRequestedEvent(string techniqueId, int mouseX, int mouseY)
        { TechniqueId = techniqueId; TargetMouseX = mouseX; TargetMouseY = mouseY; }
}

/// <summary>
/// Событие: результат попытки каста (этап 2 внедрения ЦИ).
/// Публикуется PlayerTechniqueCaster. Потребители: Adapter (тосты/визуал).
/// </summary>
public readonly struct TechniqueCastResultEvent
{
    public readonly string TechniqueId;
    public readonly bool Success;
    public readonly string Reason;      // человекочитаемая причина отказа (пусто при успехе)
    public readonly int OriginX;        // точка каста (милли-пиксели, мировые)
    public readonly int OriginY;
    public readonly int TargetX;        // точка применения (милли-пиксели, мировые)
    public readonly int TargetY;
    public readonly Core.Data.TechniqueType Type;
    public readonly Core.Data.Element Element;
    public readonly int VisualKind;     // 0=directional 1=expanding 2=self 3=heal 4=shield (этап 3)

    public TechniqueCastResultEvent(string techniqueId, bool success, string reason,
        int originX, int originY, int targetX, int targetY,
        Core.Data.TechniqueType type, Core.Data.Element element, int visualKind)
    {
        TechniqueId = techniqueId; Success = success; Reason = reason;
        OriginX = originX; OriginY = originY; TargetX = targetX; TargetY = targetY;
        Type = type; Element = element; VisualKind = visualKind;
    }
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

    /// <summary>
    /// Stage 0 (2026-08-25, GLM-5.3): мощность техники в промилле.
    /// 1000 = ×1.0 (базовая, NPC/uncharged); >1000 = заряженная игроком техника.
    /// CombatService: если PotencyPermil > 1000 → пропуск pending-таймера
    /// (зарядка УЖЕ была временем каста), немедленное применение с potency.
    /// По умолчанию 1000 (NPC и PlayerCombatAdapter без техники).
    /// </summary>
    public readonly int PotencyPermil;

    /// <summary>
    /// Stage 0: true = атака уже заряжена (игрок после зарядки/удержания в ауре);
    /// CombatService пропускает pending-таймер (зарядка была временем каста).
    /// false = NPC/базовая атака — используется pending (castTime техники).
    /// На Stage 0 potency всегда 1000 (нет overcharge), поэтому нужен явный флаг.
    /// </summary>
    public readonly bool IsCharged;

    /// <summary>
    /// R25 (план R23 §2.2 п.1): тайл прицеливания (эпицентр круга /
    /// направление конуса/линии) — конверсия курсора мыши у игрока;
    /// -1 = не указан (NPC/базовая атака — резолвер возьмёт позицию цели).
    /// int-геометрия (ЗАПРЕТ 3.9).
    /// </summary>
    public readonly int AimTileX;

    /// <summary>R25: Y-координата тайла прицеливания (-1 = не указан).</summary>
    public readonly int AimTileY;

    public AttackIntentEvent(string attackerId, string targetId,
        string techniqueId, bool isRanged, int potencyPermil = 1000, bool isCharged = false,
        int aimTileX = -1, int aimTileY = -1)
    {
        AttackerId = attackerId;
        TargetId = targetId;
        TechniqueId = techniqueId;
        IsRanged = isRanged;
        PotencyPermil = potencyPermil;
        IsCharged = isCharged;
        AimTileX = aimTileX;
        AimTileY = aimTileY;
    }
}

/// <summary>
/// C-5 (аудит-3): атака/техника ОТКЛОНЕНА боевой системой.
/// Публикуется CombatService.ExecuteAttack, когда новая атака приходит
/// во время незавершённого каста (раньше — тихий return, игрок не
/// понимал, почему атака не прошла). Потребители: Adapter/UI (тост),
/// паттерн EquipmentBlockedEvent.
/// </summary>
public readonly struct AttackRejectedEvent
{
    /// <summary>ID атакующего, чья атака отклонена</summary>
    public readonly string AttackerId;

    /// <summary>ID техники, которую пытались применить</summary>
    public readonly string TechniqueId;

    /// <summary>Человекочитаемая причина отклонения</summary>
    public readonly string Reason;

    public AttackRejectedEvent(string attackerId, string techniqueId, string reason)
        { AttackerId = attackerId; TechniqueId = techniqueId; Reason = reason; }
}

// === R16 (2026-09-10): доработка боевой системы — новые контракты ===

/// <summary>
/// R16: игрок запросил смену защитной стойки (клавиша G).
/// Публикуется PlayerCombatAdapter (цикл стойок). Потребители:
/// CombatModule → ICombatService.ExecuteDefense (запоминает стойку для
/// пайплайна урона — слой активной защиты COMBAT_SYSTEM §7);
/// GameWorldController — тост «Стойка: …».
/// Паттерн: command-событие, как TechniqueCastRequestedEvent.
/// </summary>
public readonly struct DefenseIntentEvent
{
    /// <summary>Сущность, выбравшая защиту (игрок)</summary>
    public readonly string EntityId;

    /// <summary>Выбранная стойка (Dodge/Parry/Shield/…)</summary>
    public readonly DefenseSubtype Defense;

    public DefenseIntentEvent(string entityId, DefenseSubtype defense)
        { EntityId = entityId; Defense = defense; }
}

/// <summary>
/// R16: NPC покидает бой по своей инициативе (не смерть):
/// бегство при HP&lt;20% (NPC_AI_SYSTEM §4.2) или leash — цель-игрок
/// дальше AggroRadius×3 (aggro-drop). Публикуется NPCAIService.
/// Потребители: CombatModule → CombatService.AbandonCombat (бой
/// завершается стадией Flee); NPCCombatAdapter — сброс IsInCombat/
/// TargetId участников-NPC (CombatEndedEvent при Flee несёт null
/// победителя/проигравшего — адаптер сам по нему никого не чистит).
/// </summary>
public readonly struct CombatDisengageEvent
{
    /// <summary>NPC, покидающий бой</summary>
    public readonly string NpcId;

    /// <summary>Причина (логирование/UI)</summary>
    public readonly string Reason;

    public CombatDisengageEvent(string npcId, string reason)
        { NpcId = npcId; Reason = reason; }
}

// === R25 (2026-09-21): площадные техники (AoE-A «мгновенный залп») ===

/// <summary>
/// R25: залп площадной техники состоялся — данные для VFX-фазы (R29+:
/// вспышка формы/полёты цифр урона; рендереру достаточно этих полей).
/// Публикуется CombatService.ExecuteAoeVolley ДО per-target резолва
/// (формы/цели уже известны, урон по конвейеру пойдёт следом —
/// DamageAppliedEvent per-target уже существующий контракт).
/// Лор: «Ци срывается волной» — мгновенный взрыв, залп атомарен.
/// </summary>
public readonly struct AoeImpactEvent
{
    /// <summary>Кастер залпа</summary>
    public readonly string CasterId;

    /// <summary>Площадная техника</summary>
    public readonly string TechniqueId;

    /// <summary>Форма области (Circle/Cone/Semicircle/Line)</summary>
    public readonly AoeShape Shape;

    /// <summary>Позиция кастера в тайлах (источник волны/вершина конуса)</summary>
    public readonly int OriginX;
    public readonly int OriginY;

    /// <summary>Точка прицеливания в тайлах (эпицентр круга/направление)</summary>
    public readonly int EpicenterX;
    public readonly int EpicenterY;

    /// <summary>Радиус/полудлина в тайлах</summary>
    public readonly int RadiusTiles;

    /// <summary>Полуугол конуса в градусах (0 — круг/линия)</summary>
    public readonly int HalfAngleDeg;

    /// <summary>Стихия залпа (цвет вспышки)</summary>
    public readonly Element Element;

    /// <summary>Цели залпа (длина = количество per-target резолвов)</summary>
    public readonly string[] TargetIds;

    public AoeImpactEvent(string casterId, string techniqueId, AoeShape shape,
        int originX, int originY, int epicenterX, int epicenterY,
        int radiusTiles, int halfAngleDeg, Element element, string[] targetIds)
    {
        CasterId = casterId;
        TechniqueId = techniqueId;
        Shape = shape;
        OriginX = originX;
        OriginY = originY;
        EpicenterX = epicenterX;
        EpicenterY = epicenterY;
        RadiusTiles = radiusTiles;
        HalfAngleDeg = halfAngleDeg;
        Element = element;
        TargetIds = targetIds;
    }
}

// === R28 (2026-09-21): самонаводящиеся снаряды (хоуминг A, §3.2) ===

/// <summary>
/// R28: снаряд-сущность создан и летит (CombatService-тик: Reynolds seek +
/// ограничение поворота). Урон НЕ мгновенный: контакт (Чебышёв ≤ 1 тайл)
/// → полный пайплайн по цели. Публикуется при ВЫПУСКЕ (позже снаряд живёт
/// в тике симуляции; координаты — милли-тайлы int, ЗАПРЕТ 3.9). VFX-фаза
/// (R29+) интерполирует полёт от FromX/Y без дополнительного события.
/// </summary>
public readonly struct ProjectileSpawnedEvent
{
    /// <summary>Кастер (владелец снаряда)</summary>
    public readonly string CasterId;

    /// <summary>Техника снаряда (урон при контакте)</summary>
    public readonly string TechniqueId;

    /// <summary>Цель самонаведения (снаряд доворачивает каждый тик)</summary>
    public readonly string TargetId;

    /// <summary>Точка выпуска (милли-тайлы)</summary>
    public readonly int FromX;
    public readonly int FromY;

    /// <summary>Скорость (тайлов/сек; тайл = Чебышёв)</summary>
    public readonly int SpeedTilesPerSec;

    /// <summary>Стихия снаряда (цвет VFX)</summary>
    public readonly Element Element;

    public ProjectileSpawnedEvent(string casterId, string techniqueId, string targetId,
        int fromX, int fromY, int speedTilesPerSec, Element element)
    {
        CasterId = casterId;
        TechniqueId = techniqueId;
        TargetId = targetId;
        FromX = fromX;
        FromY = fromY;
        SpeedTilesPerSec = speedTilesPerSec;
        Element = element;
    }
}

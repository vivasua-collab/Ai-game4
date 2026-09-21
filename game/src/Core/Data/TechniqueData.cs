#nullable enable
// Создано: 2026-05-20 18:18 UTC
// Редактировано: 2026-05-22 09:51:00 UTC — Спринт 6 C6: +ArmorPenetration
// Редактировано: 2026-05-22 13:08:27 UTC — P1-6.1 FIX: BaseDamage float→int (ЗАПРЕТ 3.9)
// Структура данных техники — генерируется в рантайме, НЕ ScriptableObject.
// Источник: docs/NPC_ASSEMBLY_PIPELINE.md §6, TECHNIQUE_SYSTEM.md
//
// Ключевые правила:
// - qiCost = floor(baseCapacity × 2^(level-1)) — ВСЕГДА ×1.0 по Grade (Grade НЕ влияет на стоимость Ци!)
// - UltimateDamageMultiplier = 2.0 (НЕ 1.3 как в Legacy!)
// - UltimateQiCostMultiplier = 2.0 (TECHNIQUE_SYSTEM.md §9.1)
namespace CultivationGame.Core.Data
{
    /// <summary>
    /// Данные техники — результат работы TechniqueGeneratorService.
    /// Генерируется в рантайме на основе уровня культивации, роли и seed.
    /// НЕ является ScriptableObject — техники создаются процедурно.
    ///
    /// Формулы:
    /// - capacity = baseCapacity(type) × 2^(level-1) × (1 + mastery × 0.005)
    /// - qiCost = floor(baseCapacity × 2^(level-1)) (ВСЕГДА ×1.0 по Grade!)
    /// - baseDamage = capacity × gradeMultiplier
    /// - cooldown = baseCooldown(type)
    /// - range/castTime = base по CombatSubtype
    ///
    /// Ultimate (5% шанс для Transcendent):
    /// - damage × 2.0 (НЕ ×1.3 как в Legacy!)
    /// - qiCost × 2.0 (TECHNIQUE_SYSTEM.md §9.1)
    /// </summary>
    public sealed class TechniqueData
    {
        // === Идентичность ===

        /// <summary>Уникальный идентификатор техники</summary>
        public string TechniqueId = string.Empty;

        /// <summary>Название на русском</summary>
        public string NameRu = string.Empty;

        /// <summary>Название на английском</summary>
        public string NameEn = string.Empty;

        /// <summary>Описание техники</summary>
        public string Description = string.Empty;

        // === Классификация ===

        /// <summary>Тип техники (Combat, Defense, Support, Healing и т.д.)</summary>
        public TechniqueType Type;

        /// <summary>Подтип (MeleeStrike, RangedProjectile, DefenseBlock и т.д.)</summary>
        public CombatSubtype Subtype;

        /// <summary>Грейд техники (Common ×1.0, Refined ×1.3, Perfect ×1.6, Transcendent ×2.0)</summary>
        public TechniqueGrade Grade;

        /// <summary>Стихия (Fire, Water, Earth и т.д.)</summary>
        public Element Element;

        // === Уровень и мощь ===

        /// <summary>Уровень техники (1..cultivationLevel)</summary>
        public int Level;

        /// <summary>Стоимость в единицах ёмкости = baseCapacity(type) × 2^(level-1)</summary>
        public int CapacityCost;

        /// <summary>Стоимость Ци = floor(baseCapacity × 2^(level-1)) (ВСЕГДА ×1.0 по Grade!)</summary>
        public long QiCost;

        /// <summary>Базовый урон = CapacityCost × gradeMultiplier (P1-6.1: integer — ЗАПРЕТ 3.9)</summary>
        public int BaseDamage = 0;

        /// <summary>Кулдаун (сек) = baseCooldown(type)</summary>
        public float Cooldown;

        /// <summary>Дальность (метры) = baseRange(subtype)</summary>
        public float Range;

        /// <summary>Время каста (сек) = baseCastTime(subtype)</summary>
        public float CastTime;

        // === Эффекты ===

        /// <summary>Является ли Ultimate-техникой (5% шанс для Transcendent)</summary>
        public bool IsUltimate;

        /// <summary>Множитель урона для Ultimate = 2.0 (НЕ 1.3!)</summary>
        public float UltimateDamageMultiplier = 2.0f;

        /// <summary>Множитель стоимости Ци для Ultimate = 2.0 (TECHNIQUE_SYSTEM.md §9.1)</summary>
        public float UltimateQiCostMultiplier = 2.0f;

        // === Мастерство ===

        /// <summary>Мастерство техники (0..100), влияет на ёмкость: ×(1 + mastery × 0.005)</summary>
        public float Mastery;

        // === Спринт 6 C6: Пробитие брони ===

        /// <summary>
        /// Пробитие брони техники. C6: Добавляется к penetration при атаке.
        /// penetration = weapon.penetration + attackerSTR × 0.5 + ArmorPenetration
        /// </summary>
        public int ArmorPenetration = 0;

        // === R25 (2026-09-21): площадная техника (план R23 §2.1) ===
        // Заполняется ТОЛЬКО генератором для Subtype=RangedAoe (существующие
        // техники не меняются: AoeShape.None по умолчанию — сейв совместим).
        // Все значения int (ЗАПРЕТ 3.9).

        /// <summary>Форма области. None = техника не площадная.</summary>
        public AoeShape AoeShape = AoeShape.None;

        /// <summary>Радиус/полудлина области в тайлах (Circle/Semicircle/Line; 0 = 1)</summary>
        public int AoeRadiusTiles = 0;

        /// <summary>Полуугол конуса в градусах (Cone: 30/45/60; Semicircle = 90)</summary>
        public int AoeHalfAngleDeg = 0;

        /// <summary>Спад урона на краю области в промилле (500‰ = ×0.5; 0 = без спада)</summary>
        public int AoeFalloffPermil = 0;

        /// <summary>Максимум целей залпа (баланс толпы; 0 = без лимита)</summary>
        public int AoeMaxTargets = 0;

        /// <summary>
        /// R23-эп.3 §6.4 ХУК «SpareAllies»: доля силы, которой мастер жертвует,
        /// чтобы пощадить «своих» (D&D Careful Spell). &gt;0 + party-аура цели →
        /// цель щадится. В v1 сопартийцев нет — резолвер ХУК проверяет, но
        /// результат всегда «не союзник» (хук всегда false, поле для будущих аур).
        /// </summary>
        public int SpareAlliesPermil = 0;
    }
}

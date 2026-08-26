#nullable enable
// Создано: 2026-05-20 18:00:11 UTC
// Редактировано: 2026-05-20 18:43:21 UTC — Фаза 3: AwakeningAge (3.4), статы (3.G)
// Редактировано: 2026-05-22 04:14:49 UTC — Спринт 3 B6: InnateElement
// Фаза 1: структура данных души NPC (Шаг 1 пайплайна)
// Источник: docs/NPC_ASSEMBLY_PIPELINE.md §1
//
// ПРОТИВОРЕЧИЕ #2: AwakeningType НЕ влияет на Conductivity.
// ПРОТИВОРЕЧИЕ #5: CurrentQi = CoreCapacity при генерации (полное ядро).
using CultivationGame.Core;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.NPC.Data
{
    /// <summary>
    /// Результат генерации души NPC (Шаг 1 пайплайна).
    /// Содержит все параметры, рассчитанные на этапе генерации души:
    /// уровень культивации, возраст, качество ядра, тип пробуждения,
    /// расчётные параметры Ци (ёмкость, проводимость, плотность).
    ///
    /// ПРОТИВОРЕЧИЕ #2: AwakeningType — только для flavour/черт,
    /// НЕ влияет на проводимость.
    /// ПРОТИВОРЕЧИЕ #5: CurrentQi = CoreCapacity при генерации.
    /// </summary>
    public sealed class SoulData
    {
        // === Идентичность души ===

        /// <summary>Уровень культивации</summary>
        public CultivationLevel CultivationLevel;

        /// <summary>Под-уровень (0-9 внутри уровня)</summary>
        public int SubLevel;

        /// <summary>Хронологический возраст (лет)</summary>
        public int Age;

        /// <summary>Возраст пробуждения (для расчёта latePenalty, задача 3.4)</summary>
        public int AwakeningAge;

        /// <summary>Этап смертного развития</summary>
        public MortalStage MortalStage;

        /// <summary>Качество ядра (определяет множитель ёмкости)</summary>
        public CoreQuality CoreQuality;

        /// <summary>Тип пробуждения (flavour/черты, НЕ влияет на проводимость — ПРОТИВОРЕЧИЕ #2)</summary>
        public AwakeningType AwakeningType;

        // === Расчётные параметры Ци ===

        /// <summary>Ёмкость ядра = 1000 × 1.1^totalSubLevels × qualityMultiplier</summary>
        public long CoreCapacity;

        /// <summary>Проводимость = coreCapacity / 360 × growthMultiplier (расширенная формула — ПРОТИВОРЕЧИЕ #4)</summary>
        public float Conductivity;

        /// <summary>Плотность Ци = 2^(level-1)</summary>
        public int QiDensity;

        /// <summary>Текущее Ци = CoreCapacity при генерации (ПРОТИВОРЕЧИЕ #5 — полное ядро)</summary>
        public long CurrentQi;

        // === Множители ===

        /// <summary>Множитель качества ядра (из NPCConfig.CoreQualityMultipliers)</summary>
        public float QualityMultiplier;

        /// <summary>Множитель роста проводимости с возрастом = 1.0 + 0.001 × effectiveAge</summary>
        public float ConductivityGrowthMultiplier;

        /// <summary>Максимальная продолжительность жизни (из SpeciesData.LifespanRange)</summary>
        public int MaxLifespan;

        // === Базовые статы (Фаза 3, задача 3.G) ===

        /// <summary>Базовая сила — ЗАПРЕТ 3.9: int</summary>
        public int Strength;

        /// <summary>Базовая ловкость — ЗАПРЕТ 3.9: int</summary>
        public int Agility;

        /// <summary>Базовая живучесть — ЗАПРЕТ 3.9: int</summary>
        public int Vitality;

        /// <summary>Базовый интеллект — ЗАПРЕТ 3.9: int</summary>
        public int Intelligence;

        // === Стихия (Спринт 3 B6) ===

        /// <summary>Врождённая стихия души. По умолчанию Neutral.</summary>
        public Element InnateElement = Element.Neutral;
    }
}

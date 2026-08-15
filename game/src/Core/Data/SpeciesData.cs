#nullable enable
// Создано: 2026-05-18 12:00:00 UTC — Body доработка: данные вида
// Level 3 иерархии: SoulType → Morphology → Species.
// Источник: ALGORITHMS.md П.25, ENTITY_TYPES.md
using System;

namespace CultivationGame.Core.Data
{
    /// <summary>
    /// Данные вида — Level 3 иерархии (SoulType → Morphology → Species).
    /// Содержит стартовые характеристики, жизненный цикл и врождённые способности.
    /// Источник: ALGORITHMS.md П.25, ENTITY_TYPES.md §4
    /// </summary>
    public sealed class SpeciesData
    {
        /// <summary>Идентификатор вида ("human", "wolf", "dragon" ...)</summary>
        public string SpeciesId { get; }

        /// <summary>Тип души (первичная классификация)</summary>
        public SoulType SoulType { get; }

        /// <summary>Морфология тела</summary>
        public Morphology Morphology { get; }

        /// <summary>Материал тела</summary>
        public BodyMaterial Material { get; }

        /// <summary>Класс размера</summary>
        public SizeClass Size { get; }

        /// <summary>Базовая сила (Human=10, Dragon=20)</summary>
        public float BaseStrength { get; }

        /// <summary>Базовая ловкость (Human=10, Wolf=14)</summary>
        public float BaseAgility { get; }

        /// <summary>Базовая живучесть (Human=10, Dragon=18)</summary>
        public float BaseVitality { get; }

        /// <summary>Базовый интеллект (Human=10, Ghost=12)</summary>
        public float BaseIntelligence { get; }

        /// <summary>Диапазон базового возраста взрослой особи</summary>
        public (float Min, float Max) BaseAgeRange { get; }

        /// <summary>Диапазон продолжительности жизни</summary>
        public (float Min, float Max) LifespanRange { get; }

        /// <summary>Врождённые способности вида</summary>
        public string[] InnateAbilities { get; }

        /// <summary>
        /// Создать данные вида.
        /// </summary>
        public SpeciesData(
            string speciesId,
            SoulType soulType,
            Morphology morphology,
            BodyMaterial material,
            SizeClass size,
            float baseStrength,
            float baseAgility,
            float baseVitality,
            float baseIntelligence,
            (float Min, float Max) baseAgeRange,
            (float Min, float Max) lifespanRange,
            string[]? innateAbilities = null)
        {
            SpeciesId = speciesId ?? throw new ArgumentNullException(nameof(speciesId));
            SoulType = soulType;
            Morphology = morphology;
            Material = material;
            Size = size;
            BaseStrength = baseStrength;
            BaseAgility = baseAgility;
            BaseVitality = baseVitality;
            BaseIntelligence = baseIntelligence;
            BaseAgeRange = baseAgeRange;
            LifespanRange = lifespanRange;
            InnateAbilities = innateAbilities ?? Array.Empty<string>();
        }

        public override string ToString() =>
            $"SpeciesData({SpeciesId}, {SoulType}/{Morphology}, STR={BaseStrength} AGI={BaseAgility} VIT={BaseVitality} INT={BaseIntelligence})";
    }
}

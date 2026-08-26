#nullable enable
// Создано: 2026-05-09
// Ёмкость техник — определяет, сколько техник может использовать сущность.
// Перенесено из legacy Combat/TechniqueCapacity.cs с адаптацией.
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Combat
{
    /// <summary>
    /// Ёмкость техник.
    /// Определяет максимальное количество и стоимость техник по типу.
    /// Источник: TECHNIQUE_SYSTEM.md, ALGORITHMS.md §4
    ///
    /// Формула: capacity = BaseCapacityByType[type] + (cultivationLevel - 1) × levelBonus
    /// </summary>
    public static class TechniqueCapacity
    {
        /// <summary>
        /// Бонус ёмкости за уровень культивации.
        /// </summary>
        public const int LEVEL_BONUS = 5;

        /// <summary>
        /// Рассчитать ёмкость техник указанного типа.
        /// </summary>
        public static int CalculateCapacity(TechniqueType type, int cultivationLevel)
        {
            if (!GameConstants.BaseCapacityByType.TryGetValue(type, out var baseCapacity))
                baseCapacity = 0;

            int levelBonus = (cultivationLevel - 1) * LEVEL_BONUS;
            return baseCapacity + levelBonus;
        }

        /// <summary>
        /// Рассчитать стоимость техники в единицах ёмкости.
        /// </summary>
        public static int CalculateCost(TechniqueType type, TechniqueGrade grade, CombatSubtype subtype)
        {
            // Базовая стоимость от подтипа
            int baseCost = GameConstants.BaseCapacityBySubtype.TryGetValue(subtype, out var cost)
                ? cost : 50;

            // Множитель грейда (лучшая техника = дороже)
            float gradeMult = grade switch
            {
                TechniqueGrade.Common => 1.0f,
                TechniqueGrade.Refined => 1.3f,
                TechniqueGrade.Perfect => 1.6f,
                TechniqueGrade.Transcendent => 2.0f,
                _ => 1.0f
            };

            return (int)(baseCost * gradeMult);
        }

        /// <summary>
        /// Проверить, достаточно ли ёмкости для изучения техники.
        /// </summary>
        public static bool CanLearn(int currentUsed, int capacity, int cost)
        {
            return currentUsed + cost <= capacity;
        }
    }
}

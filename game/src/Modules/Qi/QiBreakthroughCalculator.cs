#nullable enable
// Создано: 2026-05-09 04:35:17 UTC
// Статический калькулятор прорыва уровня культивации.
// Извлечено из legacy QiController.CanBreakthrough() / CalculateBreakthroughRequirement().
// Модель В: требование = capacity(nextLevel) × density(nextLevel).
// MIGRATION (Ai-game4): UnityEngine.MathF.Pow → System.Math.Pow (double overload).
using System;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Qi
{
    /// <summary>
    /// Статический калькулятор прорыва уровня культивации.
    /// Модель В: после прорыва Ци = 0, ядро перестраивается.
    /// QI-MDL-B01: Большой прорыв = capacity(nextLevel) × density(nextLevel).
    /// QI-MDL-B02: После прорыва RecalculateStats пересчитывает ёмкость.
    /// </summary>
    public static class QiBreakthroughCalculator
    {
        /// <summary>
        /// Безопасный максимум для Qi-значений.
        /// </summary>
        private const long MAX_SAFE_CAPACITY = long.MaxValue / 2;

        /// <summary>
        /// Рассчитать требование прорыва (Модель В).
        /// Большой прорыв: capacity(nextLevel) × density(nextLevel).
        /// Малый прорыв: capacity(nextSubLevel) × currentDensity.
        /// </summary>
        public static long CalculateRequirement(int currentLevel, int currentSubLevel,
            CoreQuality coreQuality, bool isMajorLevel)
        {
            if (isMajorLevel)
            {
                int nextLevel = currentLevel + 1;
                if (nextLevel > GameConstants.MAX_CULTIVATION_LEVEL)
                    return long.MaxValue; // Невозможно

                double nextDensity = Math.Pow(2, nextLevel - 1);
                long nextCapacity = EstimateCapacityAtLevel(nextLevel, coreQuality);
                return SafeMultiply(nextCapacity, nextDensity);
            }
            else
            {
                int nextSubLevel = currentSubLevel + 1;
                double currentDensity = Math.Pow(2, currentLevel - 1);
                long nextSubCapacity = EstimateCapacityAtSubLevel(currentLevel, nextSubLevel, coreQuality);
                return SafeMultiply(nextSubCapacity, currentDensity);
            }
        }

        /// <summary>
        /// Проверить возможность прорыва.
        /// </summary>
        public static bool CanBreakthrough(long currentQi, int currentLevel, int currentSubLevel,
            CoreQuality coreQuality, bool isMajorLevel)
        {
            long required = CalculateRequirement(currentLevel, currentSubLevel, coreQuality, isMajorLevel);
            return currentQi >= required;
        }

        /// <summary>
        /// Оценить ёмкость ядра на указанном уровне (без под-уровней).
        /// Используется для расчёта требований прорыва.
        /// </summary>
        public static long EstimateCapacityAtLevel(int level, CoreQuality coreQuality)
        {
            long baseCapacity = GameConstants.BASE_CORE_CAPACITY;
            float qualityMult = GetQualityMultiplier(coreQuality);
            double subLevelGrowth = Math.Pow(GameConstants.CORE_CAPACITY_GROWTH, (level - 1) * 10);
            double rawCapacity = (double)baseCapacity * qualityMult * subLevelGrowth;
            return rawCapacity > MAX_SAFE_CAPACITY ? MAX_SAFE_CAPACITY : (long)rawCapacity;
        }

        /// <summary>
        /// Оценить ёмкость ядра на указанном под-уровне.
        /// </summary>
        public static long EstimateCapacityAtSubLevel(int level, int subLevel, CoreQuality coreQuality)
        {
            long baseCapacity = GameConstants.BASE_CORE_CAPACITY;
            float qualityMult = GetQualityMultiplier(coreQuality);
            double subLevelGrowth = Math.Pow(GameConstants.CORE_CAPACITY_GROWTH,
                (level - 1) * 10 + subLevel);
            double rawCapacity = (double)baseCapacity * qualityMult * subLevelGrowth;
            return rawCapacity > MAX_SAFE_CAPACITY ? MAX_SAFE_CAPACITY : (long)rawCapacity;
        }

        /// <summary>
        /// Рассчитать полную ёмкость ядра для текущего уровня/под-уровня.
        /// Формула: baseCapacity × qualityMult × growth^(totalSubLevels)
        /// </summary>
        public static long CalculateFullCapacity(int level, int subLevel, CoreQuality coreQuality)
        {
            int totalSubLevels = (level - 1) * 10 + subLevel;
            long baseCapacity = GameConstants.BASE_CORE_CAPACITY;
            float qualityMult = GetQualityMultiplier(coreQuality);
            double growth = Math.Pow(GameConstants.CORE_CAPACITY_GROWTH, totalSubLevels);
            double rawCapacity = (double)baseCapacity * qualityMult * growth;
            return rawCapacity > MAX_SAFE_CAPACITY ? MAX_SAFE_CAPACITY : (long)rawCapacity;
        }

        /// <summary>
        /// Множитель качества ядра.
        /// </summary>
        public static float GetQualityMultiplier(CoreQuality quality)
        {
            return quality switch
            {
                CoreQuality.Fragmented => 0.5f,
                CoreQuality.Cracked => 0.7f,
                CoreQuality.Flawed => 0.85f,
                CoreQuality.Normal => 1.0f,
                CoreQuality.Refined => 1.2f,
                CoreQuality.Perfect => 1.5f,
                CoreQuality.Transcendent => 2.0f,
                _ => 1.0f
            };
        }

        /// <summary>
        /// Безопасное умножение long × float с защитой от переполнения.
        /// </summary>
        public static long SafeMultiplyImplicit(long value, float multiplier)
        {
            double result = (double)value * multiplier;
            return result > MAX_SAFE_CAPACITY ? MAX_SAFE_CAPACITY : (long)result;
        }

        /// <summary>
        /// Alias для SafeMultiplyImplicit (внутреннее использование).
        /// </summary>
        private static long SafeMultiply(long value, double multiplier) =>
            SafeMultiplyImplicit(value, (float)multiplier);
    }
}

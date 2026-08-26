#nullable enable
// Создано: 2026-05-09
// Редактировано: 2026-05-09 — FMT-D02/FMT-D03: TODO — CalculateFillRate, GetEnvironmentMultiplier не используются
// Калькулятор формаций — формулы и расчёты.
// Источник истины: FORMATION_SYSTEM.md, ALGORITHMS.md.
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Formation
{
    /// <summary>
    /// Статический калькулятор формаций.
    /// Содержит все формулы: contourQi, capacity, drain, fillRate.
    /// </summary>
    public static class FormationCalculator
    {
        /// <summary>
        /// Рассчитать стоимость прорисовки контура.
        /// Формула: contourQi = 80 × 2^(level-1)
        /// Источник: FORMATION_SYSTEM.md
        /// </summary>
        public static long CalculateContourQi(int formationLevel)
        {
            return GameConstants.FORMATION_BASE_CONTOUR_QI * (1L << (formationLevel - 1));
        }

        /// <summary>
        /// Рассчитать ёмкость пула формации.
        /// Формула: capacity = contourQi × sizeMultiplier
        /// Источник: FORMATION_SYSTEM.md
        /// </summary>
        public static long CalculateCapacity(int formationLevel, FormationSize size)
        {
            long contourQi = CalculateContourQi(formationLevel);
            long sizeMultiplier = GameConstants.FormationSizeMultipliers.TryGetValue(size, out var mult) ? mult : 10;
            return contourQi * sizeMultiplier;
        }

        /// <summary>
        /// Рассчитать скорость наполнения формации одним практиком.
        /// Формула: fillRate = conductivity × qiDensity
        /// Где conductivity — кэшированное значение из QiChangedEvent.
        /// TODO (FMT-D03): Использовать в ContributeQi для расчёта фактического вклада.
        /// Сейчас ContributeQi принимает точное количество — это упрощённая модель.
        /// </summary>
        public static long CalculateFillRate(float conductivity, float qiDensity)
        {
            return (long)(conductivity * qiDensity);
        }

        /// <summary>
        /// Рассчитать интервал утечки Ци в тиках.
        /// Источник: FORMATION_SYSTEM.md — таблица интервалов по уровню.
        /// </summary>
        public static int CalculateDrainInterval(int formationLevel)
        {
            if (GameConstants.FormationDrainIntervalByLevel.TryGetValue(formationLevel, out var interval))
                return interval;
            // Fallback: каждый час для неизвестных уровней
            return 60;
        }

        /// <summary>
        /// Рассчитать количество Ци, теряемое за одну утечку.
        /// Источник: FORMATION_SYSTEM.md — таблица по размеру.
        /// </summary>
        public static long CalculateDrainAmount(FormationSize size)
        {
            if (GameConstants.FormationDrainAmountBySize.TryGetValue(size, out var amount))
                return amount;
            return 1;
        }

        /// <summary>
        /// Рассчитать время прорисовки контура.
        /// Формула: contourQi / (conductivity × qiDensity)
        /// </summary>
        public static float CalculateDrawingTime(long contourQi, float conductivity, float qiDensity)
        {
            float effectiveSpeed = conductivity * qiDensity;
            if (effectiveSpeed <= 0f) return float.MaxValue;
            return contourQi / effectiveSpeed;
        }

        /// <summary>
        /// Получить множитель среды для формации.
        /// TODO (FMT-D02): Использовать в fill logic для расчёта скорости наполнения.
        /// FormationConfig.DefaultEnvironment определяет текущую среду.
        /// </summary>
        public static float GetEnvironmentMultiplier(string environmentType)
        {
            if (GameConstants.FormationEnvironmentMultipliers.TryGetValue(environmentType, out var mult))
                return mult;
            return 0.5f; // Default: normal
        }

        /// <summary>
        /// Проверить, допустим ли размер формации для данного уровня.
        /// Heavy формации доступны только с L6+.
        /// </summary>
        public static bool IsSizeAllowedForLevel(FormationSize size, int formationLevel)
        {
            if (size == FormationSize.Heavy && formationLevel < GameConstants.HEAVY_FORMATION_MIN_LEVEL)
                return false;
            return true;
        }
    }
}

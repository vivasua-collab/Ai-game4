#nullable enable
// Создано: 2026-05-09 04:35:17 UTC
// Статический калькулятор регенерации Ци.
// Извлечено из legacy QiController.ProcessPassiveRegeneration().
// НОВ-ДАН-01: double arithmetic для точности при высоких уровнях.
// FIX БАГ-МИР-06: Всегда накапливаем в аккумулятор, дробная часть не теряется.
// Migrated from Ai-game3 (Unity) to Ai-game4 (Godot).
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Qi
{
    /// <summary>
    /// Статический калькулятор регенерации Ци.
    /// Формула: 10% от ёмкости в сутки, умноженная на regenMultiplier.
    /// НОВ-ДАН-01: Используем double для промежуточных вычислений.
    /// </summary>
    public static class QiRegenCalculator
    {
        /// <summary>
        /// Секунд в сутках для расчёта регенерации.
        /// </summary>
        private const double SECONDS_PER_DAY = 86400.0;

        /// <summary>
        /// Безопасный максимум для Qi-значений.
        /// </summary>
        private const long MAX_SAFE_CAPACITY = long.MaxValue / 2;

        /// <summary>
        /// Рассчитать регенерацию Ци за один кадр.
        /// Возвращает количество Ци для добавления (может быть 0 — дробная часть накапливается).
        /// </summary>
        /// <param name="maxQiCapacity">Максимальная ёмкость Ци</param>
        /// <param name="regenMultiplier">Множитель регенерации уровня</param>
        /// <param name="deltaTime">Время кадра (секунды)</param>
        /// <param name="accumulator">Ссылка на аккумулятор дробной части (мутабельная!)</param>
        /// <returns>Количество Ци для добавления</returns>
        public static long CalculateRegen(long maxQiCapacity, float regenMultiplier, float deltaTime, ref double accumulator)
        {
            // Генерация микроядром: 10% от ёмкости в сутки
            // НОВ-КОР-03: (double)maxQiCapacity для точности при больших значениях
            double dailyGen = (double)maxQiCapacity * GameConstants.MICROCORE_GENERATION_RATE;
            double perSecond = dailyGen / SECONDS_PER_DAY;

            // Регенерация за кадр с множителем уровня
            double actualRegen = perSecond * regenMultiplier * deltaTime;

            // FIX БАГ-МИР-06: Накапливаем в аккумулятор, дробная часть не теряется
            accumulator += actualRegen;

            long result = 0;
            if (accumulator >= 1.0)
            {
                result = (long)accumulator;
                // Защита от переполнения
                if (result > MAX_SAFE_CAPACITY)
                    result = MAX_SAFE_CAPACITY;
                accumulator -= result;
            }

            return result;
        }

        /// <summary>
        /// Рассчитать Ци за один тик медитации.
        /// Формула: finalConductivity × environmentMult × meditationMult
        /// </summary>
        /// <param name="conductivity">Итоговая проводимость (с бонусами)</param>
        /// <param name="qiDensity">Плотность Ци (множитель окружения)</param>
        /// <param name="cultivationLevel">Уровень культивации</param>
        /// <returns>Ци за один тик медитации</returns>
        public static long CalculateMeditationQiPerTick(float conductivity, float qiDensity, int cultivationLevel)
        {
            // Множитель медитации: 1 + level × 0.1
            float meditationMult = 1f + cultivationLevel * 0.1f;
            double qiPerTick = (double)conductivity * qiDensity * meditationMult;

            if (qiPerTick > MAX_SAFE_CAPACITY)
                return MAX_SAFE_CAPACITY;

            return (long)qiPerTick;
        }
    }
}

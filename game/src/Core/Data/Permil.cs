#nullable enable
// Создано: 2026-05-25 06:23:33 UTC
// Утилитарный тип для промилле-арифметики (ЗАПРЕТ 3.9).
// 1 промилле (‰) = 1/1000. Все дробные значения ×1000 → int.
// Применяется в боевом пайплайне и системах, влияющих на бой.
using System;

namespace CultivationGame.Core.Data
{
    /// <summary>
    /// Статический класс для промилле-арифметики.
    /// ЗАПРЕТ 3.9: целочисленная арифметика, без float/double для игровых расчётов.
    ///
    /// Промилле (‰) = 1/1000:
    ///   0.5  → 500‰
    ///   1.0  → 1000‰
    ///   1.5  → 1500‰
    ///   2.78 → 2780‰
    ///
    /// Правила:
    ///   1. Все промежуточные вычисления через long (предотвращение overflow)
    ///   2. Результат → int (если в пределах int.MaxValue)
    ///   3. Конвертация float → промилле ТОЛЬКО на границе Adapter API
    /// </summary>
    public static class Permil
    {
        // === Константы ===

        /// <summary>100% = 1000‰</summary>
        public const int ONE = 1000;

        /// <summary>50% = 500‰</summary>
        public const int HALF = 500;

        /// <summary>0% = 0‰</summary>
        public const int ZERO = 0;

        // === Применение множителя ===

        /// <summary>
        /// Применить множитель промилле к базовому значению.
        /// result = baseValue × permil / 1000
        /// Промежуточное вычисление через long для предотвращения overflow.
        /// </summary>
        public static int Apply(int baseValue, int permil)
        {
            return (int)((long)baseValue * permil / ONE);
        }

        /// <summary>
        /// Применить множитель промилле к long-значению.
        /// Для Qi и других больших значений.
        /// </summary>
        public static long ApplyLong(long baseValue, int permil)
        {
            return baseValue * permil / ONE;
        }

        /// <summary>
        /// Применить множитель промилле к long-значению с long-множителем.
        /// Для случаев когда множитель тоже большой (проводимость).
        /// </summary>
        public static long ApplyLongLong(long baseValue, long permil)
        {
            return baseValue * permil / ONE;
        }

        // === Комбинирование множителей ===

        /// <summary>
        /// Объединить два множителя: (a × b / 1000).
        /// Пример: 1500‰ × 800‰ = 1200‰ (1.5 × 0.8 = 1.2).
        /// </summary>
        public static int Multiply(int permilA, int permilB)
        {
            return (int)((long)permilA * permilB / ONE);
        }

        /// <summary>
        /// Последовательное применение двух множителей к базовому значению.
        /// result = baseValue × permilA / 1000 × permilB / 1000
        /// </summary>
        public static int ApplyTwice(int baseValue, int permilA, int permilB)
        {
            return (int)((long)baseValue * permilA / ONE * permilB / ONE);
        }

        // === Конвертация на границе Adapter ===

        /// <summary>
        /// Конвертация float → промилле. ТОЛЬКО на границе Adapter API.
        /// Пример: 0.8f → 800, 1.5f → 1500, 2.78f → 2780.
        /// </summary>
        public static int FromFloat(float value)
        {
            return (int)(value * ONE);
        }

        /// <summary>
        /// Конвертация промилле → float. ТОЛЬКО для UI отображения.
        /// Пример: 800 → 0.8f, 1500 → 1.5f.
        /// </summary>
        public static float ToFloat(int permil)
        {
            return (float)permil / ONE;
        }

        // === Проценты ↔ Промилле ===

        /// <summary>Процент → промилле: 50% → 500‰</summary>
        public static int FromPercent(int percent)
        {
            return percent * 10;
        }

        /// <summary>Промилле → процент: 500‰ → 50%</summary>
        public static int ToPercent(int permil)
        {
            return permil / 10;
        }

        // === Отношение (ratio) ===

        /// <summary>
        /// Вычислить отношение двух значений в промилле.
        /// result = numerator × 1000 / denominator
        /// Пример: ratio(300, 1000) = 300 (0.3 = 30%)
        /// </summary>
        public static int Ratio(int numerator, int denominator)
        {
            if (denominator == 0) return 0;
            return (int)((long)numerator * ONE / denominator);
        }

        /// <summary>
        /// Вычислить отношение long-значений в промилле.
        /// </summary>
        public static int RatioLong(long numerator, long denominator)
        {
            if (denominator == 0) return 0;
            return (int)(numerator * ONE / denominator);
        }

        // === SoftCap ===

        /// <summary>
        /// Применить мягкий кап: если значение превышает cap,
        /// излишек умножается на decayRate (в промилле).
        /// result = min(value, cap) + max(0, value - cap) × decayRate / 1000
        /// </summary>
        public static int SoftCap(int value, int cap, int decayRatePermil)
        {
            if (value <= cap) return value;
            int excess = value - cap;
            return cap + Apply(excess, decayRatePermil);
        }

        // === Ограничение ===

        /// <summary>Ограничить промилле-значение диапазоном</summary>
        public static int Clamp(int permil, int min, int max)
        {
            return Math.Clamp(permil, min, max);
        }
    }
}

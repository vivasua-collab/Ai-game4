#nullable enable
// Создано: 2026-05-09
// Редактировано: 2026-05-25 06:23:33 UTC — ЗАПРЕТ 3.9: LevelSuppressionTable → LevelSuppressionTablePermil
// Редактировано: 2026-05-22 13:48:17 UTC — Этап 2.2: удалён мёртвый float CalculateSuppression + CanDamage (ЗАПРЕТ 3.9)
// Подавление уровнем — таблица и логика.
// Перенесено из legacy Combat/LevelSuppression.cs с адаптацией.
using CultivationGame.Core;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Combat
{
    /// <summary>
    /// Подавление уровнем.
    /// Определяет множитель урона на основе разницы уровней культивации.
    /// Источник: ALGORITHMS.md §2.1, GameConstants.LevelSuppressionTable
    ///
    /// Таблица: [разница уровней][тип атаки: 0=normal, 1=technique, 2=ultimate]
    /// Разница 0: 1.0 / 1.0 / 1.0
    /// Разница 1: 0.5 / 0.75 / 1.0
    /// Разница 2: 0.1 / 0.25 / 0.5
    /// Разница 3: 0.0 / 0.05 / 0.25
    /// Разница 4: 0.0 / 0.0 / 0.1
    /// Разница 5+: 0.0 / 0.0 / 0.0
    ///
    /// Этап 2.2: float-версия удалена — используется CalculateSuppressionPermil (ЗАПРЕТ 3.9).
    /// </summary>
    public static class LevelSuppression
    {
        /// <summary>
        /// Рассчитать множитель подавления в промилле.
        /// ЗАПРЕТ 3.9: integer вариант.
        /// 1000 = ×1.0, 500 = ×0.5, 100 = ×0.1, 0 = ×0.0.
        /// </summary>
        public static int CalculateSuppressionPermil(int attackerLevel, int defenderLevel, AttackType attackType)
        {
            // Защита: некорректные уровни
            if (attackerLevel <= 0) attackerLevel = 1;
            if (defenderLevel <= 0) defenderLevel = 1;

            int diff = defenderLevel - attackerLevel;

            // Атакующий сильнее — подавления нет
            if (diff <= 0) return 1000;

            // Разница превышает максимум — полный иммунитет
            if (diff >= GameConstants.MAX_LEVEL_DIFF) return 0;

            // Индекс типа атаки
            int typeIndex = attackType switch
            {
                AttackType.Technique => 1,
                AttackType.Ultimate => 2,
                _ => 0
            };

            // ЗАПРЕТ 3.9: прямое чтение из int-таблицы (без float)
            return GameConstants.LevelSuppressionTablePermil[diff][typeIndex];
        }

        /// <summary>
        /// Проверить, может ли атакующий нанести урон защищающемуся.
        /// </summary>
        public static bool CanDamage(int attackerLevel, int defenderLevel, AttackType attackType)
        {
            return CalculateSuppressionPermil(attackerLevel, defenderLevel, attackType) > 0;
        }
    }
}

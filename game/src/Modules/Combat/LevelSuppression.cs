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
            => CalculateSuppressionPermil(attackerLevel, defenderLevel, attackType, fromTechniqueAttack: false);

        /// <summary>
        /// PT2-004 (плейтест-2, 25.10): перегрузка с признаком «атака — техника».
        /// КОЛОНКА НЕ РАВНА ТИПУ СКЕЙЛИНГА: GetTechniqueAttackType мапит
        /// Combat-подтипы в MeleeStrike/MeleeWeapon/Ranged для stat scaling
        /// (STR/AGI/INT, DamageCalculator), но подавление по канону
        /// (Constants.LevelSuppressionTablePermil: 0=normal, 1=technique,
        /// 2=ultimate) должно читать технику по КОЛОНКЕ technique
        /// (750/250/50 против 500/100/0) — иначе любой удар техникой по цели
        /// на 3+ уровней выше гасится в ноль (репорт: «на NPC не отрабатывают
        /// техники»). fromTechniqueAttack=true → виртуальная колонка technique
        /// (кроме Ultimate — у него своя, мягче). Базовые атаки — прежняя логика.
        /// </summary>
        public static int CalculateSuppressionPermil(int attackerLevel, int defenderLevel,
            AttackType attackType, bool fromTechniqueAttack)
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
                AttackType.Ultimate => 2,
                _ when fromTechniqueAttack => 1, // PT2-004: техника → колонка technique
                AttackType.Technique => 1,
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

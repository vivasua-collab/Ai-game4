#nullable enable
// Создано: 2026-05-22 09:51:00 UTC — Спринт 6, задача C4
// Урон оружия для подтипа melee_weapon.
// Документация: COMBAT_SYSTEM.md §1b, EQUIPMENT_SYSTEM.md §7.3-7.4
// ЗАПРЕТ 3.9: целочисленная арифметика — нет float/double/decimal.
using System;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Combat
{
    /// <summary>
    /// Калькулятор урона оружия для подтипа melee_weapon.
    /// Источник: COMBAT_SYSTEM.md §1b, EQUIPMENT_SYSTEM.md §7.3-7.4
    ///
    /// Формулы (integer math, промилле):
    /// - handDamage = 3 + (STR-10) × 3 / 10
    /// - baseDamage = max(handDamage, weaponDamage / 2)
    /// - bonusDamage = weaponDamage × AGI × 50 / 1000 (stat scaling)
    ///
    /// ЗАПРЕТ 3.9: Все расчёты в integer math.
    /// </summary>
    public static class WeaponDamageCalculator
    {
        /// <summary>
        /// Рассчитать урон оружия для melee_weapon атаки.
        /// handDamage = 3 + (STR-10) × 0.3
        /// baseDamage = max(handDamage, weaponDamage × 0.5)
        /// bonusDamage = weaponDamage × AGI × 0.05 (stat scaling)
        ///
        /// В integer math:
        /// handDamage = 3 + (STR-10) × 3 / 10
        /// baseDmg = max(handDamage, weaponDamage / 2)
        /// bonusDmg = weaponDamage × attackerAGI × 50 / 1000
        /// </summary>
        public static int CalculateMeleeWeaponDamage(
            int weaponDamage, int attackerSTR, int attackerAGI)
        {
            // Урон голыми руками
            int handDamage = 3 + (attackerSTR - 10) * 3 / 10;

            // Базовый урон: max(кулак, половина оружия)
            int baseDmg = Math.Max(handDamage, weaponDamage / 2);

            // Бонус от AGI (stat scaling): weaponDamage × AGI × 5%
            // В integer math: weaponDamage × attackerAGI × 50 / 1000
            int bonusDmg = weaponDamage * attackerAGI * 50 / 1000;

            return baseDmg + bonusDmg;
        }

        /// <summary>
        /// Рассчитать урон голыми руками для melee_strike.
        /// handDamage = 3 + (STR-10) × 0.3
        /// В integer math: 3 + (STR-10) × 3 / 10
        /// </summary>
        public static int CalculateHandDamage(int attackerSTR)
        {
            return 3 + (attackerSTR - 10) * 3 / 10;
        }
    }
}

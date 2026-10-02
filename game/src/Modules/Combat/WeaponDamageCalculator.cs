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
    /// Источник: COMBAT_SYSTEM.md §1b/§4.3, EQUIPMENT_SYSTEM.md §7.3-7.4
    ///
    /// Формула R39-2 (кумулятивная, integer math, промилле):
    /// - вклад оружия = weaponDamage + weaponDamage × AGI × 50 / 1000
    /// - итог атаки   = кулак (GameConstants.UNARMED_BASE_DAMAGE) + вклад оружия
    ///
    /// ЗАПРЕТ 3.9: Все расчёты в integer math.
    /// </summary>
    public static class WeaponDamageCalculator
    {
        /// <summary>
        /// Рассчитать ЧИСТЫЙ вклад оружия для melee_weapon атаки (R39-2).
        /// CombatService СКЛАДЫВАЕТ его с базовым уроном кулака:
        /// итог = кулак (UNARMED_BASE_DAMAGE) + weaponDamage + AGI-скейл.
        /// Пример (стартовый кинжал L1, STR/AGI 10): 10 + 3 + 1 = 14.
        ///
        /// В integer math:
        /// baseDmg  = weaponDamage
        /// bonusDmg = weaponDamage × attackerAGI × 50 / 1000
        /// </summary>
        public static int CalculateMeleeWeaponDamage(
            int weaponDamage, int attackerSTR, int attackerAGI)
        {
            // R39-2 (указание владельца 02.10 №2): метод возвращает ЧИСТЫЙ вклад
            // оружия. «Кулак» (handDamage = 3 + (STR-10)×0.3) УДАЛЁН из формулы:
            // база кулака (UNARMED_BASE_DAMAGE = 10) складывается в CombatService —
            // иначе кулак учитывался ДВАЖДЫ. Раньше max(handDamage, weaponDamage/2)
            // + Math.Max в CombatService гасили вклад слабого оружия в ноль
            // (стартовый кинжал 3: с ним били ровно как кулаком 10).
            // attackerSTR оставлен в сигнатуре для совместимости вызовов —
            // в melee-формуле больше не участвует (база кулака не здесь).
            int baseDmg = weaponDamage;

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

        /// <summary>
        /// Phase 8 ч.2 (2026-09-03): урон дальнобойного оружия (лук/арбалет)
        /// для ranged-атаки без техники. COMBAT_SYSTEM.md §4.2:
        /// ranged-подтипы получают бонусы AGI 2.5% + INT 5%.
        ///
        /// Формула (по аналогии с §4.3 melee_weapon, integer math):
        /// - baseDmg  = max(2, weaponDamage / 2)        — лук не бьёт кулаком
        /// - bonusDmg = weaponDamage × (AGI×25 + INT×50) / 1000
        ///
        /// ЗАПРЕТ 3.9: целочисленная арифметика.
        /// </summary>
        public static int CalculateRangedWeaponDamage(
            int weaponDamage, int attackerAGI, int attackerINT)
        {
            // База — половина урона оружия (стрела слабее клинка,
            // минимальный порог 2, т.к. рук нет в схеме)
            int baseDmg = Math.Max(2, weaponDamage / 2);

            // §4.2: stat scaling AGI 2.5% + INT 5%
            // В integer math: weaponDamage × (AGI×25 + INT×50) / 1000
            int bonusDmg = weaponDamage * (attackerAGI * 25 + attackerINT * 50) / 1000;

            return baseDmg + bonusDmg;
        }
    }
}

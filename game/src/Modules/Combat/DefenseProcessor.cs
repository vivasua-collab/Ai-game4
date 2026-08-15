#nullable enable
// Создано: 2026-05-09
// Редактировано: 2026-05-25 06:23:33 UTC — ЗАПРЕТ 3.9: BodyMaterialReductionPermil вместо float-словаря
// Редактировано: 2026-05-25 07:01:36 UTC — ЗАПРЕТ 3.9: CalculateEffectiveArmor(float) → CalculateEffectiveArmor(int)
// Редактировано: 2026-05-09 — CMB-A03: исправлена инвертированная логика чистого урона
// Редактировано: 2026-05-22 07:55:00 UTC — Аудит CRIT-1: integer math (ЗАПРЕТ 3.9), DamageReductionPermil
// Редактировано: 2026-05-22 09:51:00 UTC — Спринт 6 C6: Penetration — уменьшает эффективную броню
// Редактировано: 2026-05-22 13:08:27 UTC — P3-4.2 FIX: Math.Round вместо truncation в CalculateMaterialReductionPermil
// Пайплайн защиты — расчёт снижения урона бронёй и материалом тела.
// Перенесено из legacy Combat/DefenseProcessor.cs с адаптацией.
using System;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Combat
{
    /// <summary>
    /// Пайплайн защиты.
    /// Рассчитывает снижение урона от брони и материала тела.
    /// ЕДИНЫЙ пайплайн — все типы урона проходят через этот процессор.
    ///
    /// Источник: ALGORITHMS.md §2.2, EQUIPMENT_SYSTEM.md §3
    /// Формула: finalDamage = baseDamage × (1000 - totalReductionPermil) / 1000
    /// totalReductionPermil = min(armorReductionPermil + materialReductionPermil + buffReductionPermil, MAX_PERMIL)
    ///
    /// CRIT-1: Полныи integer math (ЗАПРЕТ 3.9). Все снижения в промилле.
    /// </summary>
    public static class DefenseProcessor
    {
        // CRIT-1: MAX_DAMAGE_REDUCTION в промилле (80% = 800 промилле)
        // ЗАПРЕТ 3.9: теперь из константы Permil
        private const int MAX_REDUCTION_PERMIL = GameConstants.MAX_DAMAGE_REDUCTION_PERMIL;

        /// <summary>
        /// Рассчитать итоговый урон после защиты.
        /// CRIT-1: integer math — все снижения в промилле.
        /// </summary>
        /// <param name="baseDamage">Базовый урон (после подавления уровнем и Ци-буфера)</param>
        /// <param name="context">Контекст защиты (броня, материал, снижение в промилле)</param>
        /// <returns>Урон после защиты</returns>
        public static int ApplyDefense(int baseDamage, DefenseContext context)
        {
            if (baseDamage <= 0) return 0;

            // C6: Пробитие брони — уменьшает эффективную броню
            // penetration = weapon.penetration + attackerSTR × 0.5 + techniquePenetration
            // effectiveArmor = max(0, armorValue - penetration)
            int effectiveArmor = Math.Max(0, context.ArmorValue - context.Penetration);

            // Снижение урона бронёй (промилле) — от эффективной брони
            int armorReductionPermil = CalculateArmorReductionPermil(effectiveArmor);

            // Снижение урона материалом тела (промилле)
            int materialReductionPermil = CalculateMaterialReductionPermil(context.Material);

            // Суммарное снижение (кап MAX_REDUCTION_PERMIL = 80% = 800 промилле)
            int totalReductionPermil = armorReductionPermil + materialReductionPermil + context.DamageReductionPermil;
            if (totalReductionPermil > MAX_REDUCTION_PERMIL)
                totalReductionPermil = MAX_REDUCTION_PERMIL;
            if (totalReductionPermil < 0)
                totalReductionPermil = 0;

            // CRIT-1: integer math — finalDamage = baseDamage * (1000 - reductionPermil) / 1000
            int finalDamage = (int)((long)baseDamage * (1000 - totalReductionPermil) / 1000);
            return finalDamage >= 1 ? finalDamage : 1; // Минимум 1 урон
        }

        /// <summary>
        /// Рассчитать снижение урона от брони в промилле.
        /// Формула: armorReduction = armor / (armor + 100)
        /// В промилле: armorReductionPermil = armor * 1000 / (armor + 100)
        /// Источник: ALGORITHMS.md §2.2
        /// </summary>
        private static int CalculateArmorReductionPermil(int armorValue)
        {
            if (armorValue <= 0) return 0;
            return armorValue * 1000 / (armorValue + 100);
        }

        /// <summary>
        /// Рассчитать снижение урона от материала тела в промилле.
        /// Читает снижение урона из int-словаря BodyMaterialReductionPermil.
        /// Источник: GameConstants.BodyMaterialReductionPermil
        /// </summary>
        private static int CalculateMaterialReductionPermil(BodyMaterial material)
        {
            // ЗАПРЕТ 3.9: прямое чтение из int-словаря (без float)
            if (GameConstants.BodyMaterialReductionPermil.TryGetValue(material, out int reduction))
                return reduction;
            return 0;
        }

        /// <summary>
        /// Рассчитать эффективную броню.
        /// ЗАПРЕТ 3.9: принимает int (армор уже в целочисленном виде).
        /// Учитывает покрытие и грейд (уже учтено в EquipmentStatAggregator).
        /// </summary>
        public static int CalculateEffectiveArmor(int totalArmor)
        {
            return totalArmor;
        }
    }
}

#nullable enable
// Создано: 2026-05-09
// Редактировано: 2026-08-22 — IMPL-6 (Q5): Random.Shared → ICombatRng параметр (детерминированный бой).
// Редактировано: 2026-05-25 06:23:33 UTC — ЗАПРЕТ 3.9: (int)(float*1000f) → _PERMIL константы
// Редактировано: 2026-05-22 04:14:49 UTC — Спринт 3 B1: stat scaling (STR/AGI/INT), integer math
// Редактировано: 2026-05-22 07:55:00 UTC — Аудит CRIT-1: Potency→PotencyPermil, полныи integer math
// Редактировано: 2026-05-22 09:51:00 UTC — Спринт 5 C1/C2/C3: DetermineAttackResult из статов (промилле)
// Редактировано: 2026-05-22 11:30:00 UTC — Спринт 8 C10: DetermineHitPart с морфологическими таблицами (промилле)
// Редактировано: 2026-05-22 13:48:17 UTC — Этап 2.2: удалён мёртвый float GetElementMultiplier + GetGradeMultiplier
// Формулы урона — единый пайплайн расчёта урона.
// Перенесено из legacy Combat/DamageCalculator.cs с адаптацией под модульную архитектуру.
// КРИТИЧЕСКАЯ: ЕДИНЫЙ пайплайн урона (ICombatant и ICombatTarget объединены).
// ЗАПРЕТ 3.9: целочисленная арифметика (long + промилле), без float/double для урона.
using System;
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Combat
{
    /// <summary>
    /// Калькулятор урона.
    /// ЕДИНЫЙ пайплайн урона — заменяет два несовместимых пайплайна из legacy.
    ///
    /// Пайплайн:
    /// 1. BaseDamage из техники/оружия
    /// 2. × GradeMultiplier (грейд техники)
    /// 3. × LevelSuppression (разница уровней)
    /// 4. × ElementMultiplier (стихийные преимущества)
    /// 5. DefenseProcessor.ApplyDefense() — броня + материал
    /// 6. QiBufferService.AbsorbDamage() — Ци-буфер
    /// 7. Результат: DamageResult с деталями
    ///
    /// Источник: ALGORITHMS.md §2, COMBAT_SYSTEM.md §3
    /// </summary>
    public static class DamageCalculator
    {
        /// <summary>
        /// Рассчитать полный урон по единому пайплайну.
        /// Шаги 1-5: базовый урон → грейд → potency → stat scaling → ultimate.
        /// Спринт 3 B1: целочисленная арифметика (long + промилле), stat scaling.
        /// ЗАПРЕТ 3.9: нет float/double для урона — всё через long × 1000 / 1000.
        /// </summary>
        public static int CalculateRawDamage(DamageRequest request)
        {
            // 1. Базовый урон (long для промежуточных)
            long damage = request.BaseDamage;

            // 2. Грейд техники (промилле: 1000 = ×1.0, 1300 = ×1.3)
            damage = damage * GetGradeMultiplierInt(request.Grade) / 1000;

            // 3. Множитель мощности (CRIT-1: PotencyPermil — уже в промилле)
            damage = damage * request.PotencyPermil / 1000;

            // 4. Stat scaling — ALGORITHMS.md §11
            // STR → +5%/ед для MeleeStrike, AGI → +5%/ед для MeleeWeapon,
            // INT → +5%/ед для Ranged, Technique. 5% = 50 промилле.
            int statBonusPermil = 0;
            if (request.AttackType == AttackType.MeleeStrike)
                statBonusPermil = request.AttackerSTR * 50;
            else if (request.AttackType == AttackType.MeleeWeapon)
                statBonusPermil = request.AttackerAGI * 50;
            else if (request.AttackType == AttackType.Ranged)
                statBonusPermil = request.AttackerINT * 50;
            else if (request.AttackType == AttackType.Technique)
                statBonusPermil = request.AttackerINT * 50; // Техники — INT scaling

            if (statBonusPermil > 0)
                damage = damage * (1000 + statBonusPermil) / 1000;

            // 5. Ultimate-множитель
            if (request.AttackType == AttackType.Ultimate)
            {
                damage = damage * GameConstants.ULTIMATE_DAMAGE_MULTIPLIER_PERMIL / 1000;
            }

            return (int)damage;
        }

        /// <summary>
        /// Рассчитать множитель стихии в промилле.
        /// Этап 2.2: единственный метод (float-версия удалена — ЗАПРЕТ 3.9).
        /// 1000 = ×1.0, 1500 = ×1.5, 800 = ×0.8, 1200 = ×1.2.
        /// </summary>
        public static int GetElementMultiplierPermil(Element attacker, Element defender)
        {
            // Void — множитель по всем элементам
            // ЗАПРЕТ 3.9: прямые _PERMIL константы вместо (int)(float * 1000f)
            if (attacker == Element.Void)
                return GameConstants.VOID_ELEMENT_MULTIPLIER_PERMIL;

            // Противоположный элемент — усиление
            if (GameConstants.IsOppositeElement(attacker, defender))
                return GameConstants.OPPOSITE_ELEMENT_MULTIPLIER_PERMIL;

            // Тот же элемент — ослабление (сродство)
            if (attacker == defender && attacker != Element.Neutral)
                return GameConstants.AFFINITY_ELEMENT_MULTIPLIER_PERMIL;

            // Fire → Poison (одностороннее)
            if (attacker == Element.Fire && defender == Element.Poison)
                return GameConstants.FIRE_TO_POISON_MULTIPLIER_PERMIL;

            // Light → Poison (одностороннее)
            if (attacker == Element.Light && defender == Element.Poison)
                return GameConstants.LIGHT_TO_POISON_MULTIPLIER_PERMIL;

            return 1000; // ×1.0
        }

        /// <summary>
        /// Определить часть тела, в которую попадает удар.
        /// Источник: GameConstants.BodyPartHitChances (гуманоид) или MorphologyHitTables (по морфологии).
        /// Спринт 8 C10: принимает targetMorphology для выбора таблицы.
        /// ЗАПРЕТ 3.9: целочисленная арифметика (промилле), Random.Range(0, totalWeight).
        /// IMPL-6 (Q5): RNG передаётся через <paramref name="rng"/> (детерминированный бой).
        /// </summary>
        public static BodyPartType DetermineHitPart(ICombatRng rng, Morphology targetMorphology = Morphology.Humanoid)
        {
            if (rng is null) throw new ArgumentNullException(nameof(rng));

            // Выбрать таблицу по морфологии
            Dictionary<BodyPartType, int> hitTable;
            if (!GameConstants.MorphologyHitTables.TryGetValue(targetMorphology, out hitTable))
            {
                // Fallback — гуманоидная таблица (промилле)
                hitTable = GameConstants.BodyPartHitChancesPermil;
            }

            int totalWeight = 0;
            foreach (var kvp in hitTable)
                totalWeight += kvp.Value;

            if (totalWeight <= 0) return BodyPartType.Torso;

            // ЗАПРЕТ 3.9: integer roll вместо Random.value
            // Q5: ICombatRng.Next вместо Random.Shared.Next.
            int roll = rng.Next(0, totalWeight);
            int cumulative = 0;
            foreach (var kvp in hitTable)
            {
                cumulative += kvp.Value;
                if (roll < cumulative)
                    return kvp.Key;
            }
            return BodyPartType.Torso;
        }

        /// <summary>
        /// Определить результат атаки (Hit, CriticalHit, Miss, Dodge, Parry, Block).
        /// Спринт 5 C1: Уклонение из AGI — dodgeChance = 50 + (AGI-10)×5 - armorDodgePenalty промилле.
        /// Спринт 5 C2: Крит из удачи — critChance = 50 + luck×10 + techniqueCritBonus промилле.
        /// Спринт 5 C3: Блок из STR — blockChance = shieldBlock + (STR-10)×2 промилле.
        /// Спринт 5 C3: Парирование из AGI — parryChance = weaponParryBonus + (AGI-10)×3 промилле.
        /// P2-5.2 FIX: blockChance использует defenderSTR (сила ЗАЩИЩАЮЩЕГОСЯ), не атакующего.
        /// ЗАПРЕТ 3.9: Все расчёты в промилле (integer math), ролл через Random.Range(0, 1000).
        /// IMPL-6 (Q5): RNG передаётся через <paramref name="rng"/> (детерминированный бой).
        /// </summary>
        public static CombatAttackResult DetermineAttackResult(
            ICombatRng rng,
            DefenseSubtype defense,
            int defenderAGI = 10,
            int armorDodgePenalty = 0,
            int defenderSTR = 10, // P2-5.2 FIX: переименован из attackerSTR — для блока используется STR защищающегося
            int weaponParryBonus = 0,
            int shieldBlock = 0,
            int attackerLuck = 0,
            int techniqueCritBonus = 0)
        {
            if (rng is null) throw new ArgumentNullException(nameof(rng));

            // C1: Уклонение из AGI — ALGORITHMS.md §4
            // dodgeChance = 5% + (AGI-10) × 0.5% - armorDodgePenalty
            // В промилле: 50 + (AGI-10) × 5 - armorDodgePenalty
            if (defense == DefenseSubtype.Dodge)
            {
                int dodgeChancePermil = 50 + (defenderAGI - 10) * 5 - armorDodgePenalty;
                dodgeChancePermil = Math.Max(0, Math.Min(600, dodgeChancePermil)); // кап 60%
                int dodgeRoll = rng.Next(0, 1000);
                if (dodgeRoll < dodgeChancePermil)
                    return CombatAttackResult.Dodge;
            }

            // C3: Блок из STR — ALGORITHMS.md §4
            // P2-5.2 FIX: blockChance = shieldBlock + (defenderSTR-10) × 2 (STR ЗАЩИЩАЮЩЕГОСЯ)
            // В промилле: shieldBlock + (defenderSTR-10) × 2
            if (defense == DefenseSubtype.Block || defense == DefenseSubtype.Shield)
            {
                int blockChancePermil = shieldBlock + (defenderSTR - 10) * 2;
                blockChancePermil = Math.Max(0, Math.Min(700, blockChancePermil)); // кап 70%
                int blockRoll = rng.Next(0, 1000);
                if (blockRoll < blockChancePermil)
                    return CombatAttackResult.Block;
                // Провал блока — обычный Hit
            }

            // C3: Парирование из AGI — ALGORITHMS.md §4
            // parryChance = weaponParryBonus + (AGI-10) × 0.3%
            // В промилле: weaponParryBonus + (defenderAGI-10) × 3
            if (defense == DefenseSubtype.Parry)
            {
                int parryChancePermil = weaponParryBonus + (defenderAGI - 10) * 3;
                parryChancePermil = Math.Max(0, Math.Min(500, parryChancePermil)); // кап 50%
                int parryRoll = rng.Next(0, 1000);
                if (parryRoll < parryChancePermil)
                    return CombatAttackResult.Parry;
            }

            // C2: Крит из удачи — ALGORITHMS.md §4
            // critChance = базовый 5% + luck × 1% + techniqueCritBonus
            // В промилле: 50 + luck × 10 + techniqueCritBonus
            int critChancePermil = 50 + attackerLuck * 10 + techniqueCritBonus;
            critChancePermil = Math.Max(0, Math.Min(500, critChancePermil)); // кап 50%
            int critRoll = rng.Next(0, 1000);
            if (critRoll < critChancePermil)
                return CombatAttackResult.CriticalHit;

            return CombatAttackResult.Hit;
        }

        /// <summary>
        /// Проверить, является ли удар смертельным.
        /// Смертельный удар = цель в жизненно важную часть с уроном > FATAL_DAMAGE_THRESHOLD.
        /// </summary>
        public static bool IsFatalHit(BodyPartType hitPart, int finalDamage)
        {
            bool isVital = hitPart == BodyPartType.Head || hitPart == BodyPartType.Heart;
            return isVital && finalDamage >= GameConstants.FATAL_DAMAGE_THRESHOLD;
        }

        // === Вспомогательные методы ===

        /// <summary>
        /// Множитель грейда в промилле (1000 = ×1.0, 1300 = ×1.3).
        /// Спринт 3 B1: целочисленный вариант для integer math.
        /// </summary>
        private static int GetGradeMultiplierInt(TechniqueGrade grade)
        {
            // Спринт 3 B1: таблица промилле-множителей (float × 1000)
            return grade switch
            {
                TechniqueGrade.Common => 1000,       // ×1.0
                TechniqueGrade.Refined => 1300,       // ×1.3
                TechniqueGrade.Perfect => 1600,       // ×1.6
                TechniqueGrade.Transcendent => 2000,  // ×2.0
                _ => 1000
            };
        }

        // Этап 2.2: float GetGradeMultiplier() удалён — заменён на GetGradeMultiplierInt()
    }
}

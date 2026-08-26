#nullable enable
// Создано: 2026-05-09 05:15:31 UTC
// Редактировано: 2026-05-09 — BF-A01: исправлена формула, добавлен baseValue
// Редактировано: 2026-05-09 — BF-A05: добавлены недостающие мягкие капы
// Статический калькулятор модификаторов баффов.
// Формула: modifier = flatSum × (1 + percentSum) + baseValue × percentSum
// Источник: ALGORITHMS.md §6, BUFF_MODIFIERS_SYSTEM.md §«Формула расчёта»
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Buff
{
    /// <summary>
    /// Статический калькулятор модификаторов баффов.
    /// Шаг 1: Собрать все flat и percent модификаторы
    /// Шаг 2: Суммировать (аддитивно, НЕ мультипликативно!)
    /// Шаг 3: Применить: modifier = flatSum × (1 + percentSum) + baseValue × percentSum
    /// Шаг 4: Применить мягкий кап
    /// </summary>
    public static class BuffCalculator
    {
        /// <summary>
        /// Рассчитать итоговый модификатор характеристики.
        /// Собирает все flat и percent бонусы из активных баффов.
        /// BF-A01: Проценты хранятся как дроби (0.2 для 20%), деление на 100 убрано.
        /// Формула модификатора: flatSum × (1 + percentSum) + baseValue × percentSum
        /// При baseValue=0: modifier = flatSum × (1 + percentSum)
        /// </summary>
        public static float CalculateStatModifier(List<ActiveBuff> buffs, StatType stat, float baseValue = 0f)
        {
            float flatSum = 0f;
            float percentSum = 0f;

            for (int i = 0; i < buffs.Count; i++)
            {
                var buff = buffs[i];
                if (buff.AffectedStat != stat) continue;

                if (buff.IsPercentage)
                {
                    percentSum += buff.TotalValue;
                }
                else
                {
                    flatSum += buff.TotalValue;
                }
            }

            // BF-A01: Проценты суммируются аддитивно, хранятся как дроби (0.2 = 20%)
            // modifier = (baseValue + flatSum) × (1 + percentSum) - baseValue
            //          = flatSum × (1 + percentSum) + baseValue × percentSum
            return flatSum * (1f + percentSum) + baseValue * percentSum;
        }

        /// <summary>
        /// Применить мягкий кап к бонусу.
        /// Формула: effectiveBonus = cap × (1 - e^(-bonus / (cap × decayRate)))
        /// Источник: ALGORITHMS.md §6
        /// </summary>
        public static float ApplySoftCap(float bonus, float cap, float decayRate)
        {
            if (cap == 0f) return bonus;

            // Для отрицательных капов (qi_cost, cooldown)
            float absCap = System.Math.Abs(cap);
            float sign = bonus >= 0 ? 1f : -1f;
            float absBonus = System.Math.Abs(bonus);

            float effective = (float)(absCap * (1.0 - System.Math.Exp(-absBonus / (absCap * decayRate))));
            return sign * effective;
        }

        /// <summary>
        /// Получить параметры мягкого капа для характеристики.
        /// </summary>
        public static (float cap, float decay) GetSoftCapParams(StatType stat)
        {
            // SoftCaps — static class, доступ только через GameConstants.SoftCaps.MEMBER
            return stat switch
            {
                StatType.Speed => (GameConstants.SoftCaps.SPEED_CAP, GameConstants.SoftCaps.SPEED_DECAY),
                StatType.AttackSpeed => (GameConstants.SoftCaps.ATTACK_SPEED_CAP, GameConstants.SoftCaps.ATTACK_SPEED_DECAY),
                StatType.Damage => (GameConstants.SoftCaps.DAMAGE_CAP, GameConstants.SoftCaps.DAMAGE_DECAY),
                StatType.CritChance => (GameConstants.SoftCaps.CRIT_CHANCE_CAP, GameConstants.SoftCaps.CRIT_CHANCE_DECAY),
                StatType.CritDamage => (GameConstants.SoftCaps.CRIT_DAMAGE_CAP, GameConstants.SoftCaps.CRIT_DAMAGE_DECAY),
                StatType.Defense => (GameConstants.SoftCaps.DEFENSE_CAP, GameConstants.SoftCaps.DEFENSE_DECAY),
                StatType.Armor => (GameConstants.SoftCaps.ARMOR_CAP, GameConstants.SoftCaps.ARMOR_DECAY),
                StatType.QiCost => (GameConstants.SoftCaps.QI_COST_CAP, GameConstants.SoftCaps.QI_COST_DECAY),
                StatType.QiEfficiency => (GameConstants.SoftCaps.QI_EFFICIENCY_CAP, GameConstants.SoftCaps.QI_EFFICIENCY_DECAY),
                StatType.Cooldown => (GameConstants.SoftCaps.COOLDOWN_CAP, GameConstants.SoftCaps.COOLDOWN_DECAY),
                StatType.Lifesteal => (GameConstants.SoftCaps.LIFESTEAL_CAP, GameConstants.SoftCaps.LIFESTEAL_DECAY),
                // BF-A05: Добавлены недостающие мягкие капы
                StatType.Stealth => (GameConstants.SoftCaps.STEALTH_CAP, GameConstants.SoftCaps.STEALTH_DECAY),
                StatType.Perception => (GameConstants.SoftCaps.PERCEPTION_CAP, GameConstants.SoftCaps.PERCEPTION_DECAY),
                StatType.HealingReceived => (GameConstants.SoftCaps.HEALING_RECEIVED_CAP, GameConstants.SoftCaps.HEALING_RECEIVED_DECAY),
                StatType.HpRegen => (GameConstants.SoftCaps.HP_REGEN_CAP, GameConstants.SoftCaps.HP_REGEN_DECAY),
                StatType.Thorns => (GameConstants.SoftCaps.THORNS_CAP, GameConstants.SoftCaps.THORNS_DECAY),
                StatType.Luck => (GameConstants.SoftCaps.LUCK_CAP, GameConstants.SoftCaps.LUCK_DECAY),
                StatType.ExpBonus => (GameConstants.SoftCaps.EXP_BONUS_CAP, GameConstants.SoftCaps.EXP_BONUS_DECAY),
                StatType.StaminaCost => (GameConstants.SoftCaps.STAMINA_COST_CAP, GameConstants.SoftCaps.STAMINA_COST_DECAY),
                StatType.StaminaRegen => (GameConstants.SoftCaps.STAMINA_REGEN_CAP, GameConstants.SoftCaps.STAMINA_REGEN_DECAY),
                StatType.QiRestoration => (GameConstants.SoftCaps.QI_RESTORATION_CAP, GameConstants.SoftCaps.QI_RESTORATION_DECAY),
                StatType.Evasion => (GameConstants.SoftCaps.EVASION_CAP, GameConstants.SoftCaps.EVASION_DECAY),
                _ => (0f, 1f) // Без капа
            };
        }

        /// <summary>
        /// Рассчитать модификатор с применением мягкого капа.
        /// BF-A01: добавлен параметр baseValue, передаётся в CalculateStatModifier.
        /// </summary>
        public static float CalculateCappedModifier(List<ActiveBuff> buffs, StatType stat, float baseValue = 0f)
        {
            float raw = CalculateStatModifier(buffs, stat, baseValue);
            var (cap, decay) = GetSoftCapParams(stat);
            if (cap == 0f) return raw;
            return ApplySoftCap(raw, cap, decay);
        }
    }
}

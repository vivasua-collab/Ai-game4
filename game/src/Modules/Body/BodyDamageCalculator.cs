#nullable enable
// Создано: 2026-05-08 15:44:00 UTC
// Редактировано: 2026-05-09 12:00:00 UTC — аудит: BD-01 добавлен using System
// Редактировано: 2026-05-19 — P1-03 FIX: SplitDamage гарантированный минимум 1 в redDmg
// Редактировано: 2026-05-22 07:55:00 UTC — Аудит LOW-1: удалён мёртвый код ApplyMaterialReduction
// Migrated from Ai-game3 (Unity) to Ai-game4 (Godot).
// Статическая утилита расчёта урона для тела.
// НЕ сервис, НЕ регистрируется в DI.
// Источник: ALGORITHMS.md §9, BODY_SYSTEM.md
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Body
{
    /// <summary>
    /// Калькулятор телесного урона.
    /// Статический класс-утилита для распределения урона,
    /// материального снижения и расчёта штрафов.
    /// </summary>
    public static class BodyDamageCalculator
    {
        /// <summary>
        /// Распределение урона: 70% → RedHP, 30% → BlackHP.
        /// Источник: ALGORITHMS.md §9, Constants.RED_HP_RATIO / BLACK_HP_RATIO
        ///
        /// P1-03 FIX: При totalDamage=1, (int)(1×0.7)=0 → весь урон шёл в BlackHP.
        /// Гарантируем redDmg ≥ 1, чтобы при малом уроне функциональный урон не
        /// обнулялся. Остаток (blackDmg) может быть 0.
        /// </summary>
        public static (int redDmg, int blackDmg) SplitDamage(int totalDamage)
        {
            if (totalDamage <= 0) return (0, 0);

            int redDmg = (int)(totalDamage * GameConstants.RED_HP_RATIO);
            // P1-03 FIX: гарантированный минимум 1 в RedHP
            redDmg = Math.Max(1, redDmg);
            int blackDmg = totalDamage - redDmg;
            // blackDmg может быть 0 при totalDamage=1 — это корректно
            return (redDmg, blackDmg);
        }

        /// <summary>
        /// Расчёт штрафов от состояния частей тела.
        /// Severed: 30%, Disabled: 15%, Wounded: 5%.
        /// Максимум 90%.
        /// Источник: ALGORITHMS.md, Legacy BodyDamage.CalculateDamagePenalty
        /// </summary>
        public static float CalculateDamagePenalty(IReadOnlyList<BodyPart> parts)
        {
            float penalty = 0f;

            foreach (var part in parts)
            {
                switch (part.State)
                {
                    case BodyPartState.Severed:
                        penalty += 0.3f;
                        break;
                    case BodyPartState.Disabled:
                        penalty += 0.15f;
                        break;
                    case BodyPartState.Wounded:
                        penalty += 0.05f;
                        break;
                }
            }

            // Кэп 90%
            return Math.Min(0.9f, penalty);
        }

        /// <summary>
        /// Проверка: жив ли организм.
        /// Смерть при RedHP ≤ 0 жизненно важной части (Head, Heart).
        /// Источник: BODY_SYSTEM.md "Жизненно важные части"
        /// </summary>
        public static bool IsAlive(IReadOnlyList<BodyPart> parts)
        {
            foreach (var part in parts)
            {
                if (part.IsVital && part.CurrentRedHP <= 0)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Общий процент здоровья организма (по красной HP).
        /// </summary>
        public static float GetOverallHealthPercent(IReadOnlyList<BodyPart> parts)
        {
            int totalMax = 0;
            int totalCurrent = 0;

            foreach (var part in parts)
            {
                totalMax += part.MaxRedHP;
                totalCurrent += part.CurrentRedHP;
            }

            return totalMax > 0 ? (float)totalCurrent / totalMax : 0f;
        }
    }
}

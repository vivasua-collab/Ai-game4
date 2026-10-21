#nullable enable
// Создано: 2026-05-08 15:44:00 UTC
// Редактировано: 2026-05-09 12:00:00 UTC — аудит: BD-01 добавлен using System
// Редактировано: 2026-05-19 — P1-03 FIX: SplitDamage гарантированный минимум 1 в redDmg
// Редактировано: 2026-05-22 07:55:00 UTC — Аудит LOW-1: удалён мёртвый код ApplyMaterialReduction
// Редактировано: 2026-10-22 — BUG-016 (Этап-5 аудита): SplitDamage учитывает
//   способности части тела — части без структурной HP (сердце, CORE-C01)
//   получают ВЕСЬ урон в красную HP (зеркало BD-24 для HealPart).
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
        ///
        /// BUG-016 FIX (Этап-5 аудита боя): сплит зависит от способностей части
        /// тела. У части без структурной HP (сердце, CORE-C01: MaxBlackHP == 0)
        /// ВЕСЬ урон идёт в красную HP — прежний безусловный 70/30 отправлял 30%
        /// в несуществующий структурный слой, где BodyPart.TakeDamage их
        /// отбрасывал: удар 50 оставлял сердцу 45/80 красной HP вместо честных
        /// 30/80 (30% урона терялось, бой затягивался, а порог смертельного
        /// удара FATAL_DAMAGE_THRESHOLD=50 сравнивался с «фантомным» уроном).
        /// Обычные части (MaxBlackHP &gt; 0) сохраняют сплит 70/30. Зеркально
        /// правилу лечения BD-24 (Heart: 100% лечения в RedHP).
        /// </summary>
        public static (int redDmg, int blackDmg) SplitDamage(int totalDamage, BodyPart? targetPart = null)
        {
            if (totalDamage <= 0) return (0, 0);

            // BUG-016: у части нет структурной HP → весь урон в функциональную
            if (targetPart != null && targetPart.MaxBlackHP <= 0)
                return (totalDamage, 0);

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
                if (IsFatalPart(part) && part.CurrentRedHP <= 0)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// R20 (баг №4, запрос 09_09_22_40): часть, уничтожение которой =
        /// СМЕРТЬ — ТОЛЬКО Head | Heart (канон: единое правило тел,
        /// аудит-0911 BOD-2; PlayerService.Die-путь считает так же).
        /// ВАЖНО: IsVital-флаг ≠ смертность: торс — жизненно важный ОРГАН
        /// (IsVital=true в шаблоне — не ампутируется), но его разрушение =
        /// критическая инвалидность, НЕ смерть. Раньше IsAlive проверял
        /// IsVital → торс 0/100 при живых голове/сердце давал «Мёртв»
        /// (статус листа персонажа при живом игроке) И отключал регенерацию.
        /// </summary>
        public static bool IsFatalPart(BodyPart part)
            => part.Type == BodyPartType.Head || part.Type == BodyPartType.Heart;

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

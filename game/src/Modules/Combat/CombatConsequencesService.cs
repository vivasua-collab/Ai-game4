#nullable enable
// Создано: 2026-05-22 11:30:00 UTC
// Редактировано: 2026-05-22 13:08:27 UTC — P0-7.1 FIX: Stun chance ×1000→×100 (завышен в 10 раз)
// Редактировано: 2026-05-22 13:55:00 UTC — Этап 3.5: P2-7.3 FIX: кровотечение различает slashing/piercing от blunt (MeleeStrike)
// Редактировано: 2026-05-25 07:01:36 UTC — ЗАПРЕТ 3.9: float duration → int тики + конверсия на границе Unity Time API
// Спринт 7, задача C7: Сервис последствий боя — кровотечение, шок, оглушение.
// Подписывается на DamageAppliedEvent и применяет дебаффы через IBuffService.
// Документация: COMBAT_SYSTEM.md §10, ALGORITHMS.md §10
// ЗАПРЕТ 3.9: Все пороговые и шанс-расчёты в промилле (integer math).
using System;
using CultivationGame.Core;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Combat
{
    /// <summary>
    /// Сервис последствий боя.
    /// Реагирует на DamageAppliedEvent и накладывает дебаффы:
    /// - Кровотечение: slashing/piercing урон > 30% maxHP → Bleed DoT
    /// - Оглушение: урон в голову > 50% headHP → Stun шанс
    /// - Шок: redHP < 30% maxHP → Shock debuff (снижение статов)
    ///
    /// Документация: COMBAT_SYSTEM.md §10, ALGORITHMS.md §10
    /// ЗАПРЕТ 3.9: Все расчёты шансов в промилле.
    /// </summary>
    public class CombatConsequencesService : IDisposable
    {
        private readonly IBuffService _buffService;
        private readonly IBodyDataProvider _bodyDataProvider;
        private readonly IDisposable _subscription;

        // Пороги из документации (в процентах, для integer math)
        private const int BLEED_DAMAGE_THRESHOLD_PERCENT = 30;  // >30% maxHP
        private const int SHOCK_HP_THRESHOLD_PERCENT = 30;     // redHP <30% maxHP
        private const int BLEED_TICK_PERCENT = 5;              // 5% maxHP за тик
        private const int BLEED_DURATION_TICKS = 3;            // 3 тика
        private const int STUN_DURATION_TICKS = 1;             // 1 тик (ЗАПРЕТ 3.9: int вместо 3.0f)
        private const int SHOCK_DURATION_TICKS = 2;            // 2 тика (ЗАПРЕТ 3.9: int вместо 5.0f)
        private const float TICK_SECONDS = 3.0f;               // Unity API boundary: секунды за тик

        public CombatConsequencesService(
            ISubscriber<DamageAppliedEvent> damageAppliedSub,
            IBuffService buffService,
            IBodyDataProvider bodyDataProvider)
        {
            _buffService = buffService;
            _bodyDataProvider = bodyDataProvider;
            // Подписка в конструкторе — стандартный паттерн проекта (как DamageService и др.)
            _subscription = damageAppliedSub.Subscribe(OnDamageApplied);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        private void OnDamageApplied(in DamageAppliedEvent e)
        {
            if (e.Damage <= 0) return;

            // Кровотечение — от slashing/piercing урона
            TryApplyBleed(e);

            // Оглушение — от урона в голову
            TryApplyStun(e);

            // Шок — при низком HP
            TryApplyShock(e);
        }

        /// <summary>
        /// Кровотечение: slashing/piercing урон > 30% maxHP → Bleed DoT
        /// (3 тика по 5% maxHP). ЗАПРЕТ 3.9: расчёт в промилле.
        /// P2-7.3 FIX: MeleeStrike (безоружный удар / blunt) снижает шанс кровотечения на 80%.
        /// Реалистично: кулаки и дубинки НЕ вызывают сильное кровотечение.
        /// MeleeWeapon (режущее/колющее оружие) — полный шанс.
        /// </summary>
        private void TryApplyBleed(DamageAppliedEvent e)
        {
            // Проверка типа урона — только физический урон может вызвать кровотечение
            if (e.Type != DamageType.Physical) return;

            // P2-7.3 FIX: различаем slashing/piercing от blunt через AttackSubtype
            // MeleeStrike = безоружный удар (кулак, дубинка) — blunt, шанс кровотечения ×0.2
            // MeleeWeapon = режущее/колющее оружие — полный шанс кровотечения
            // Ranged* = колющие снаряды — полный шанс
            // Defense* = не атака — кровотечения не вызывает
            bool isBluntAttack = e.AttackSubtype == CombatSubtype.MeleeStrike
                              || e.AttackSubtype == CombatSubtype.DefenseBlock
                              || e.AttackSubtype == CombatSubtype.DefenseShield
                              || e.AttackSubtype == CombatSubtype.DefenseDodge;

            int maxHP = _bodyDataProvider.GetMaxHealth(e.TargetId);
            if (maxHP <= 0) return;

            // Порог: >30% maxHP (промилле: damage * 1000 / maxHP > 300)
            int damagePermil = e.Damage * 1000 / maxHP;

            // P2-7.3 FIX: blunt-атаки снижают шанс кровотечения на 80%
            // (damagePermil × 200 / 1000 = 20% от исходного значения)
            // Формула: effectiveDamagePermil = damagePermil * 200 / 1000 для blunt
            if (isBluntAttack)
            {
                // Тупой удар: кровотечение маловероятно — снижаем эффективный урон в 5 раз
                damagePermil = damagePermil * 200 / 1000;
            }

            if (damagePermil <= BLEED_DAMAGE_THRESHOLD_PERCENT * 10) return;

            // Не накладывать, если уже есть кровотечение
            if (_buffService.HasBuff(e.TargetId, "combat_bleed")) return;

            // Potency = 5% maxHP за тик (integer)
            int potency = maxHP * BLEED_TICK_PERCENT / 100;
            // Duration = 3 тика × 3 сек/тик (ЗАПРЕТ 3.9: тики × константа, float только на границе Unity)
            float duration = BLEED_DURATION_TICKS * TICK_SECONDS;
            _buffService.ApplyBuff(e.TargetId, "combat_bleed", duration, potency);
        }

        /// <summary>
        /// Оглушение: урон в голову → шанс = damage/maxHP × 10%.
        /// ЗАПРЕТ 3.9: шанс в промилле, ролл через Random.Range(0, 1000).
        /// </summary>
        private void TryApplyStun(DamageAppliedEvent e)
        {
            // Только при попадании в голову
            if (e.HitPart != BodyPartType.Head) return;

            int maxHP = _bodyDataProvider.GetMaxHealth(e.TargetId);
            if (maxHP <= 0) return;

            // Шанс оглушения: damage / maxHP × 10%
            // В промилле: damage × 100 / maxHP (P0-7.1 FIX: было ×1000 — завышено в 10 раз)
            int stunChancePermil = e.Damage * 100 / maxHP;
            // Кап 80% = 800 промилле
            stunChancePermil = Math.Min(800, stunChancePermil);

            // ЗАПРЕТ 3.9: integer roll вместо Random.value
            int stunRoll = Random.Shared.Next(0, 1000);
            if (stunRoll < stunChancePermil)
            {
                // Не накладывать, если уже есть оглушение
                if (_buffService.HasBuff(e.TargetId, "combat_stun")) return;
                // ЗАПРЕТ 3.9: duration из int-тиков, float только на границе Unity API
                float stunDuration = STUN_DURATION_TICKS * TICK_SECONDS;
                _buffService.ApplyBuff(e.TargetId, "combat_stun", stunDuration, 0);
            }
        }

        /// <summary>
        /// Шок: redHP < 30% maxHP → Shock debuff (снижение статов на 20%).
        /// ЗАПРЕТ 3.9: порог в промилле.
        /// </summary>
        private void TryApplyShock(DamageAppliedEvent e)
        {
            int currentHP = _bodyDataProvider.GetCurrentHealth(e.TargetId);
            int maxHP = _bodyDataProvider.GetMaxHealth(e.TargetId);
            if (maxHP <= 0) return;

            // Порог: redHP < 30% maxHP
            int threshold = maxHP * SHOCK_HP_THRESHOLD_PERCENT / 100;
            if (currentHP >= threshold) return;

            // Не накладывать, если уже есть шок
            if (_buffService.HasBuff(e.TargetId, "combat_shock")) return;
            // ЗАПРЕТ 3.9: duration из int-тиков, float только на границе Unity API
            float shockDuration = SHOCK_DURATION_TICKS * TICK_SECONDS;
            // potency=200 = -20% (промилле: 200/1000 = 0.2)
            _buffService.ApplyBuff(e.TargetId, "combat_shock", shockDuration, 200);
        }
    }
}

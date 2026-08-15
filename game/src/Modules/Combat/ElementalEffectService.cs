#nullable enable
// Создано: 2026-05-22 11:30:00 UTC
// Редактировано: 2026-05-22 13:50:00 UTC — Этап 3.2: P2-7.1 FIX: ApplyPurify — .ToList() для безопасной итерации
// Редактировано: 2026-05-23 07:30:00 UTC — FIX CS0246: убран IInitializable, подписка в конструкторе (как все сервисы)
// Спринт 7, задача C8: Стихийные эффекты при попадании.
// Подписывается на DamageAppliedEvent и применяет эффекты через IBuffService.
// Документация: ELEMENTS_SYSTEM.md, ALGORITHMS.md §10
// ЗАПРЕТ 3.9: Все шанс-расчёты в промилле (integer math).
using System;
using System.Collections.Generic;
using System.Linq; // P2-7.1: для .ToList() в ApplyPurify
using CultivationGame.Core;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Combat
{
    /// <summary>
    /// Стихийные эффекты при попадании.
    /// Определяет стихию из DamageAppliedEvent.Element и применяет
    /// соответствующий эффект через IBuffService.
    ///
    /// Таблица эффектов (ELEMENTS_SYSTEM.md):
    /// | Стихия   | Эффект       | Механика                            |
    /// |----------|-------------|-------------------------------------|
    /// | Fire     | Burn         | DoT, 3 тика по 5% от начального урона |
    /// | Water    | Slow         | -30% скорости на 3 сек               |
    /// | Earth    | Stun шанс 15%| Оглушение при ударе                  |
    /// | Air      | Knockback    | Отбрасывание цели (заглушка)         |
    /// | Lightning| Chain        | 50% урона × 2 ближайшие цели (заглушка) |
    /// | Void     | +30% pierce  | Игнор 30% брони (дебафф)             |
    /// | Light    | Purify       | Снятие дебаффов с цели               |
    /// | Poison   | Poison DoT   | Яд, 3 тика по 3% maxHP              |
    ///
    /// ЗАПРЕТ 3.9: Все расчёты шансов в промилле.
    /// </summary>
    public class ElementalEffectService : IDisposable
    {
        private readonly IBuffService _buffService;
        private readonly IBodyDataProvider _bodyDataProvider;
        private readonly IDisposable _subscription;

        // Пороги и константы (integer math)
        private const int EARTH_STUN_CHANCE_PERMIL = 150;   // 15% = 150 промилле
        private const int POISON_TICK_PERCENT = 3;           // 3% maxHP за тик
        private const int POISON_DURATION_TICKS = 3;         // 3 тика
        private const int BURN_TICK_PERCENT = 5;             // 5% от начального урона за тик
        private const int BURN_DURATION_TICKS = 3;           // 3 тика

        public ElementalEffectService(
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
            // Стихийные эффекты применяются только при обычных попаданиях (Hit/CriticalHit)
            if (e.Result != CombatAttackResult.Hit &&
                e.Result != CombatAttackResult.CriticalHit) return;

            // Нейтральная стихия — без эффектов
            if (e.Element == Element.Neutral) return;

            ApplyElementalEffect(e);
        }

        /// <summary>
        /// Применить стихийный эффект по Element из DamageAppliedEvent.
        /// Спринт 7 C8: стихия передаётся через DamageAppliedEvent.Element.
        /// </summary>
        private void ApplyElementalEffect(DamageAppliedEvent e)
        {
            switch (e.Element)
            {
                case Element.Fire:
                    ApplyBurn(e);
                    break;
                case Element.Water:
                    ApplySlow(e);
                    break;
                case Element.Earth:
                    ApplyEarthStun(e);
                    break;
                case Element.Air:
                    ApplyKnockback(e);
                    break;
                case Element.Lightning:
                    ApplyChain(e);
                    break;
                case Element.Void:
                    ApplyVoidPierce(e);
                    break;
                case Element.Light:
                    ApplyPurify(e);
                    break;
                case Element.Poison:
                    ApplyPoison(e);
                    break;
            }
        }

        /// <summary>
        /// Fire → Burn (DoT, 3 тика по 5% от начального урона).
        /// ЗАПРЕТ 3.9: potency = integer.
        /// </summary>
        private void ApplyBurn(DamageAppliedEvent e)
        {
            int tickDamage = e.Damage * BURN_TICK_PERCENT / 100;
            if (tickDamage <= 0) return;
            // Не накладывать, если уже есть горение
            if (_buffService.HasBuff(e.TargetId, "elemental_burn")) return;
            float duration = BURN_DURATION_TICKS * 3.0f;
            _buffService.ApplyBuff(e.TargetId, "elemental_burn", duration, tickDamage);
        }

        /// <summary>
        /// Water → Slow (-30% скорости на 3 сек).
        /// Potency = 300 = -30% (промилле).
        /// </summary>
        private void ApplySlow(DamageAppliedEvent e)
        {
            if (_buffService.HasBuff(e.TargetId, "elemental_slow")) return;
            _buffService.ApplyBuff(e.TargetId, "elemental_slow", 3.0f, 300);
        }

        /// <summary>
        /// Earth → 15% шанс Stun.
        /// ЗАПРЕТ 3.9: integer roll через Random.Range(0, 1000).
        /// </summary>
        private void ApplyEarthStun(DamageAppliedEvent e)
        {
            int roll = Random.Shared.Next(0, 1000);
            if (roll >= EARTH_STUN_CHANCE_PERMIL) return;

            if (_buffService.HasBuff(e.TargetId, "elemental_earth_stun")) return;
            _buffService.ApplyBuff(e.TargetId, "elemental_earth_stun", 2.0f, 0);
        }

        /// <summary>
        /// Air → Knockback (публикация события для MovementService).
        /// Заглушка — требует IPositionService.
        /// </summary>
        private void ApplyKnockback(DamageAppliedEvent e)
        {
            // TODO: Опубликовать KnockbackEvent для движения
            // Требует IPositionService или IWorldService для поиска целей
            // Пока — заглушка, логика будет добавлена в будущих фазах
        }

        /// <summary>
        /// Lightning → Chain 50% × 2 ближайшие цели.
        /// Заглушка — требует IPositionService.
        /// </summary>
        private void ApplyChain(DamageAppliedEvent e)
        {
            // TODO: Найти 2 ближайшие цели и нанести 50% урона
            // Требует IPositionService или IWorldService для поиска целей
            // Пока — заглушка, логика будет добавлена в будущих фазах
        }

        /// <summary>
        /// Void → +30% pierce (пробитие брони) — дебафф на цель.
        /// Potency = 300 = -30% брони (промилле).
        /// </summary>
        private void ApplyVoidPierce(DamageAppliedEvent e)
        {
            if (_buffService.HasBuff(e.TargetId, "elemental_void_pierce")) return;
            _buffService.ApplyBuff(e.TargetId, "elemental_void_pierce", 5.0f, 300);
        }

        /// <summary>
        /// Light → Purify (снятие дебаффов с цели).
        /// Light атака снимает все дебаффы с цели.
        /// P2-7.1 FIX: итерируем по копии коллекции, чтобы избежать InvalidOperationException.
        /// </summary>
        private void ApplyPurify(DamageAppliedEvent e)
        {
            var activeBuffs = _buffService.GetActiveBuffs(e.TargetId).ToList(); // P2-7.1: копия коллекции
            foreach (var buff in activeBuffs)
            {
                if (buff.IsDebuff)
                    _buffService.RemoveBuff(e.TargetId, buff.BuffId);
            }
        }

        /// <summary>
        /// Poison → Poison DoT (3 тика по 3% maxHP).
        /// ЗАПРЕТ 3.9: potency = integer.
        /// </summary>
        private void ApplyPoison(DamageAppliedEvent e)
        {
            int maxHP = _bodyDataProvider.GetMaxHealth(e.TargetId);
            if (maxHP <= 0) return;

            int tickDamage = maxHP * POISON_TICK_PERCENT / 100;
            if (tickDamage <= 0) return;

            if (_buffService.HasBuff(e.TargetId, "elemental_poison")) return;
            float duration = POISON_DURATION_TICKS * 3.0f;
            _buffService.ApplyBuff(e.TargetId, "elemental_poison", duration, tickDamage);
        }
    }
}

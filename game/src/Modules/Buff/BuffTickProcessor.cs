#nullable enable
// Создано: 2026-05-09 05:15:31 UTC
// Редактировано: 2026-05-09 — BF-I04: исправлен дрифт таймера тиков
// Редактировано: 2026-10-23 — ST12-003 (Этап-12 аудита): догоняющие тики.
// Обработка тиков периодических эффектов (DoT/HoT/Stun).
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Buff
{
    /// <summary>
    /// Обработчик тиков периодических эффектов.
    /// DoT — урон во времени (Poison, Burn, Bleed, Freeze)
    /// HoT — исцеление во времени (HealthRegen, QiRestoration, StaminaRegen)
    /// CC — обновление контроля (Stun, Slow, Blind, Silence)
    /// </summary>
    public class BuffTickProcessor
    {
        private readonly IPublisher<BuffTickedEvent> _tickedPub;

        /// <summary>
        /// ST12-003: потолок догоняющих тиков за один ProcessTick. Скачок
        /// deltaTime (лаг/пауза-снимок) отрабатывает эквивалентно покадровой
        /// симуляции (5 шагов по 1с == 1 шаг 5с), но без неограниченного
        /// всплеска: долгий hitch не выстреливает сотни тиков DoT разом —
        /// остаток долга отбрасывается, таймер синхронизируется.
        /// </summary>
        public const int MaxCatchUpTicks = 5;

        public BuffTickProcessor(IPublisher<BuffTickedEvent> tickedPub)
        {
            _tickedPub = tickedPub;
        }

        /// <summary>
        /// Обработать тик баффа.
        /// Возвращает true, если произошёл хотя бы один тик.
        /// ST12-003: deltaTime, кратный интервалу, даёт пропорциональное
        /// число тиков (раньше — один, таймер сбрасывался на полный интервал:
        /// 5 шагов по 1с давали 5 тиков, а 1 шаг 5с — только 1).
        /// </summary>
        public bool ProcessTick(ActiveBuff buff, float deltaTime)
        {
            if (!buff.HasTickEffect) return false;
            if (deltaTime <= 0f) return false;

            buff.TickTimer -= deltaTime;
            if (buff.TickTimer > 0f)
                return false;

            // Сколько полных интервалов накопилось (>= 1).
            float elapsed = -buff.TickTimer;
            int dueTicks = (int)(elapsed / buff.TickInterval) + 1;
            if (dueTicks > MaxCatchUpTicks)
                dueTicks = MaxCatchUpTicks; // анти-всплеск: долг не копится

            // BF-I04 (сохранён): синхронизация с сеткой интервалов — без дрифта.
            buff.TickTimer = buff.TickInterval;

            bool anyTicked = false;
            for (int i = 0; i < dueTicks; i++)
            {
                float tickValue = 0f;
                if (buff.TickDamage > 0)
                {
                    tickValue = buff.TickDamage * buff.CurrentStacks;
                }
                else if (buff.TickHealing > 0)
                {
                    tickValue = buff.TickHealing * buff.CurrentStacks;
                }

                if (tickValue != 0f)
                {
                    // BUG-017 (Этап-5 аудита боя): SourceId — автор наложения
                    // баффа (атрибуция DoT-смерти, прокидывается дальше в
                    // BodyModule → DamageAppliedEvent.OriginalAttackerId).
                    _tickedPub.Publish(new BuffTickedEvent(
                        buff.EntityId, buff.BuffId, buff.Type, tickValue, buff.SourceId));
                    anyTicked = true;
                }
            }

            return anyTicked;
        }
    }
}

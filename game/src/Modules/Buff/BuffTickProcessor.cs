#nullable enable
// Создано: 2026-05-09 05:15:31 UTC
// Редактировано: 2026-05-09 — BF-I04: исправлен дрифт таймера тиков
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

        public BuffTickProcessor(IPublisher<BuffTickedEvent> tickedPub)
        {
            _tickedPub = tickedPub;
        }

        /// <summary>
        /// Обработать тик баффа.
        /// Возвращает true, если тик произошёл.
        /// </summary>
        public bool ProcessTick(ActiveBuff buff, float deltaTime)
        {
            if (!buff.HasTickEffect) return false;

            buff.TickTimer -= deltaTime;
            if (buff.TickTimer <= 0f)
            {
                // BF-I04: Сброс таймера вместо добавления — предотвращает дрифт при лагах
                buff.TickTimer = buff.TickInterval;

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
                }

                return true;
            }

            return false;
        }
    }
}

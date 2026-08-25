#nullable enable
// Создано: 2026-08-25 (Stage 1 — вариант В, GLM-5.3)
// AuraHoldService — единый слот удержания заряженной техники в ауре игрока.
//
// Источник: checkpoints/08_25_technique_hold_analysis.md §4 (вариант В) + §6.
// Правило (вариант В): ВСЕ техники могут удерживаться, но аура держит ОДНУ.
//   - При TechniqueChargeCompletedEvent: PlayerTechniqueCaster решает —
//     аура свободна → Hold (park); аура занята → Fire немедленно.
//   - При повторном нажатии Z (TechniqueCastRequestedEvent для той же техники)
//     с удержанием в ауре → Release → fire.
//   - Декей: AURA_HOLD_DECAY_PERMIL (1%/тик) от QiCost; при ChargedQi < QiCost/2 →
//     рассеивание (возврат 50% остаточного Ци, техника теряется).
//
// АРХИТЕКТУРА: сервис модуля Player; подписан на QiAddRequestEvent (через
// IPublisher) для возврата Ци при рассеивании; публикует HeldTechniqueChangedEvent.
// ITimeService.DeltaTime — единица времени (тик). ОДИН слот на сервис (только игрок).
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Player
{
    /// <summary>
    /// Сервис удержания техники в ауре игрока (Stage 1, вариант В).
    /// Одиночный слот. Декей 1%/тик. Рассеивание при &lt; QiCost/2 (возврат 50%).
    /// </summary>
    public sealed class AuraHoldService : IDisposable
    {
        // === Зависимости ===
        private readonly IPublisher<HeldTechniqueChangedEvent> _heldPub;
        private readonly IPublisher<QiAddRequestEvent> _qiAddPub;
        private readonly IPlayerService _player;

        /// <summary>Текущее удержание (null = аура пуста).</summary>
        private HeldTechnique? _held;
        private bool _disposed;

        public AuraHoldService(
            IPublisher<HeldTechniqueChangedEvent> heldPub,
            IPublisher<QiAddRequestEvent> qiAddPub,
            IPlayerService player)
        {
            _heldPub = heldPub;
            _qiAddPub = qiAddPub;
            _player = player;
        }

        /// <summary>Аура пуста?</summary>
        public bool IsEmpty => _held == null;

        /// <summary>Текущая удерживаемая техника (null = пусто).</summary>
        public HeldTechnique? Current => _held;

        /// <summary>
        /// Подвязать технику в ауру (Stage 1, вариант В).
        /// Событие HeldTechniqueChangedEvent публикуется наружу (UI/визуал ауры).
        /// </summary>
        public bool Hold(string techniqueId, int potencyPermil, long chargedQi,
            long qiCost, Element element)
        {
            if (string.IsNullOrEmpty(techniqueId)) return false;
            if (_held != null) return false; // слот занят — caller должен Fire вместо Hold

            _held = new HeldTechnique
            {
                TechniqueId = techniqueId,
                PotencyPermil = potencyPermil,
                ChargedQi = chargedQi,
                QiCost = qiCost,
                Element = element
            };
            _heldPub.Publish(new HeldTechniqueChangedEvent(
                _player.PlayerId, techniqueId, potencyPermil, element));
            return true;
        }

        /// <summary>
        /// Снять технику с ауры (для выпуска). НЕ публикует refund.
        /// Возвращает удержание или null, если аура пуста.
        /// </summary>
        public HeldTechnique? Release()
        {
            if (_held == null) return null;
            var h = _held;
            _held = null;
            _heldPub.Publish(new HeldTechniqueChangedEvent(
                _player.PlayerId, "", GameConstants.POTENCY_BASE_PERMIL, Element.Neutral));
            return h;
        }

        /// <summary>
        /// Принудительно рассеять удержание (стюн/смерть/медитация).
        /// Возвращает 50% остаточного ChargedQi как QiAddRequestEvent.
        /// </summary>
        public void Dissipate(string reason = "stun")
        {
            if (_held == null) return;
            var h = _held;

            // Возврат 50% остаточного ChargedQi (как CancelCharge — 50%)
            long refund = h.ChargedQi * GameConstants.CHARGE_CANCEL_REFUND_PERMIL / 1000;
            if (refund > 0)
                _qiAddPub.Publish(new QiAddRequestEvent(refund, "AuraHoldService:" + reason));

            _held = null;
            _heldPub.Publish(new HeldTechniqueChangedEvent(
                _player.PlayerId, "", GameConstants.POTENCY_BASE_PERMIL, Element.Neutral));
        }

        /// <summary>
        /// Тик декея (вызывается из PlayerModule.Tick).
        /// ChargedQi -= max(1, QiCost × AURA_HOLD_DECAY_PERMIL / 1000).
        /// При ChargedQi &lt; QiCost/2 → Dissipate("decay").
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_disposed || _held == null) return;
            if (deltaTime <= 0f) return;

            var h = _held;
            long decayPerTick = Math.Max(1, h.QiCost * GameConstants.AURA_HOLD_DECAY_PERMIL / 1000);
            long newCharged = h.ChargedQi - decayPerTick;

            if (newCharged <= h.QiCost / 2)
            {
                // Рассеяние (декей превысил половину)
                _held = null;
                long refund = h.ChargedQi * GameConstants.CHARGE_CANCEL_REFUND_PERMIL / 1000;
                if (refund > 0)
                    _qiAddPub.Publish(new QiAddRequestEvent(refund, "AuraHoldService:decay"));
                _heldPub.Publish(new HeldTechniqueChangedEvent(
                    _player.PlayerId, "", GameConstants.POTENCY_BASE_PERMIL, Element.Neutral));
                return;
            }

            // Обновляем (class — мутируем поле)
            h.ChargedQi = newCharged;
        }

        public void Dispose()
        {
            _disposed = true;
            _held = null;
        }
    }

    /// <summary>
    /// Удержание техники в ауре. Mutable (decay уменьшает ChargedQi).
    /// </summary>
    public sealed class HeldTechnique
    {
        public string TechniqueId = "";
        public int PotencyPermil = GameConstants.POTENCY_BASE_PERMIL;
        public long ChargedQi;     // остаточная энергия (тает от декея)
        public long QiCost;
        public Element Element = Element.Neutral;
    }
}

#nullable enable
// Создано: 2026-05-09
// Редактировано: 2026-05-09 — CMB-C05: ChargedQi изменён с float на long (Fix-01)
// Редактировано: 2026-05-09 — EVT-01: убрана инъекция IQiService,
//   кросс-модульное общение через QiChangedEvent + QiConsumeRequestEvent + QiAddRequestEvent
// Редактировано: 2026-05-22 07:55:00 UTC — Аудит CRIT-1: +GetPotencyPermil() для ЗАПРЕТ 3.9
// Редактировано: 2026-05-25 07:01:36 UTC — ЗАПРЕТ 3.9: Potency float → PotencyPermil int, удалён float GetPotency/ReleaseCharge
// Сервис накачки техник — управление зарядкой техник перед применением.
// Перенесено из legacy Combat/TechniqueChargeSystem.cs с адаптацией.
using System;
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Combat
{
    /// <summary>
    /// Сервис накачки техник.
    /// Управляет зарядкой техники: расход Ци, множитель мощности, тепловой лимит.
    ///
    /// Источник: TECHNIQUE_SYSTEM.md §4, CHARGER_SYSTEM.md
    /// Накачка: чем больше Ци вложено, тем мощнее техника.
    /// Лимит: тепловая перегрузка зарядника ограничивает максимальную накачку.
    ///
    /// CMB-C05: ChargedQi — long (Fix-01: все Qi-значения long).
    /// EVT-01: кэш Qi из QiChangedEvent вместо инъекции IQiService.
    /// Расход Ци через QiConsumeRequestEvent, возврат через QiAddRequestEvent.
    /// </summary>
    public class TechniqueChargeService : IDisposable
    {
        // === Зависимости ===
        // EVT-01: подписки на кросс-модульные события (вместо инъекции IQiService)
        private readonly ISubscriber<QiChangedEvent> _qiChangedSub;
        private readonly IPublisher<QiConsumeRequestEvent> _qiConsumeRequestPub;
        private readonly IPublisher<QiAddRequestEvent> _qiAddRequestPub;

        // EVT-01: кэш состояния из событий
        private long _cachedCurrentQi;

        // IDisposable для подписок
        private IDisposable _qiChangedSubscription;

        // === Состояние ===
        private readonly Dictionary<string, ChargeState> _activeCharges = new();

        /// <summary>
        /// Состояние зарядки одной техники.
        /// CMB-C05: ChargedQi — long (Fix-01).
        /// </summary>
        private class ChargeState
        {
            public string TechniqueId;
            public long ChargedQi;    // CMB-C05: long вместо float (Fix-01)
            public long MaxCharge;    // CMB-C05: long вместо float (Fix-01)
            public int PotencyPermil; // ЗАПРЕТ 3.9: множитель мощности в промилле (1000 = ×1.0, 2000 = ×2.0)
            public bool IsComplete;
        }

        // === Конструктор ===
        public TechniqueChargeService(
            ISubscriber<QiChangedEvent> qiChangedSub,
            IPublisher<QiConsumeRequestEvent> qiConsumeRequestPub,
            IPublisher<QiAddRequestEvent> qiAddRequestPub)
        {
            _qiChangedSub = qiChangedSub;
            _qiConsumeRequestPub = qiConsumeRequestPub;
            _qiAddRequestPub = qiAddRequestPub;

            // EVT-01: подписка на кэш состояния Ци
            _qiChangedSubscription = _qiChangedSub.Subscribe((in QiChangedEvent e) => {
                _cachedCurrentQi = e.Current;
            });
        }

        /// <summary>
        /// Начать зарядку техники.
        /// EVT-01: проверка Ци из кэша вместо _qiService.CurrentQi.
        /// </summary>
        public bool StartCharge(string techniqueId, long maxCharge)
        {
            if (string.IsNullOrEmpty(techniqueId)) return false;

            // Проверяем: достаточно ли Ци для начала зарядки
            if (_cachedCurrentQi < GameConstants.MIN_QI_FOR_BUFFER) return false; // EVT-01: из кэша

            _activeCharges[techniqueId] = new ChargeState
            {
                TechniqueId = techniqueId,
                ChargedQi = 0L,
                MaxCharge = maxCharge,
                PotencyPermil = 1000, // ЗАПРЕТ 3.9: 1000‰ = ×1.0
                IsComplete = false
            };
            return true;
        }

        /// <summary>
        /// Обновить зарядку за один кадр.
        /// CMB-C05: ChargedQi — long.
        /// EVT-01: проверка Ци из кэша + QiConsumeRequestEvent вместо IQiService.TryConsumeQi.
        /// </summary>
        public void UpdateCharge(string techniqueId, float deltaTime, float chargeRate)
        {
            if (!_activeCharges.TryGetValue(techniqueId, out var state)) return;
            if (state.IsComplete) return;

            // Расход Ци на зарядку
            long qiToCharge = (long)(chargeRate * deltaTime);
            if (qiToCharge <= 0) return;

            // EVT-01: проверка Ци из кэша (best-effort: кэш актуален в рамках кадра)
            if (_cachedCurrentQi >= qiToCharge)
            {
                // EVT-01: запрашиваем расход Ци через событие
                _qiConsumeRequestPub.Publish(new QiConsumeRequestEvent(qiToCharge, "TechniqueChargeService"));

                state.ChargedQi += qiToCharge;
                // ЗАПРЕТ 3.9: potency = 1000 + chargedQi×1000/maxCharge (integer math)
                state.PotencyPermil = 1000 + (int)(state.ChargedQi * 1000 / state.MaxCharge);

                // Проверка завершения зарядки
                if (state.ChargedQi >= state.MaxCharge)
                {
                    state.PotencyPermil = 2000; // ЗАПРЕТ 3.9: ×2 максимум = 2000‰
                    state.IsComplete = true;
                }
            }
            else
            {
                // Ци исчерпан — зарядка прерывается с текущей мощностью
                state.IsComplete = true;
            }
        }

        /// <summary>
        /// Получить текущую мощность зарядки в промилле (ЗАПРЕТ 3.9).
        /// 1000 = ×1.0, 2000 = ×2.0.
        /// </summary>
        public int GetPotencyPermil(string techniqueId)
        {
            if (!_activeCharges.TryGetValue(techniqueId, out var state)) return 1000;
            return state.PotencyPermil;
        }

        /// <summary>
        /// Завершить зарядку и вернуть мощность в промилле (ЗАПРЕТ 3.9).
        /// 1000 = ×1.0, 2000 = ×2.0.
        /// </summary>
        public int ReleaseChargePermil(string techniqueId)
        {
            if (!_activeCharges.TryGetValue(techniqueId, out var state)) return 1000;

            int potencyPermil = state.PotencyPermil;
            _activeCharges.Remove(techniqueId);
            return potencyPermil;
        }

        /// <summary>
        /// Отменить зарядку (возвращает часть Ци).
        /// EVT-01: QiAddRequestEvent вместо _qiService.AddQi.
        /// </summary>
        public void CancelCharge(string techniqueId)
        {
            if (!_activeCharges.TryGetValue(techniqueId, out var state)) return;

            // Возвращаем 50% зарядженного Ци (ЗАПРЕТ 3.9: integer division вместо * 0.5f)
            long refund = state.ChargedQi / 2;
            // EVT-01: запрашиваем добавление Ци через событие
            if (refund > 0) _qiAddRequestPub.Publish(new QiAddRequestEvent(refund, "TechniqueChargeService"));

            _activeCharges.Remove(techniqueId);
        }

        /// <summary>
        /// Проверить, заряжается ли техника.
        /// </summary>
        public bool IsCharging(string techniqueId)
        {
            return _activeCharges.ContainsKey(techniqueId);
        }

        public void Dispose()
        {
            _qiChangedSubscription?.Dispose();
            _qiChangedSubscription = null;
        }
    }
}

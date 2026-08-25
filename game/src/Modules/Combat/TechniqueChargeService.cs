#nullable enable
// Создано: 2026-05-09
// Редактировано: 2026-08-25 (Stage 0 — GLM-5.3, полная переработка):
//   - зарядка тиками по проводимости (модель заполнения TECHNIQUE_SYSTEM §5.3)
//   - per-entity ChargeState (исправляет баг глобального PendingTechnique)
//   - окно перезарядки [qiCost..capacity], potency 1000→2000‰ (дестабилизация §7)
//   - события наружу (TechniqueChargeStarted/Progress/Completed/Cancelled)
//   - зарядка берёт Ци тиками из ядра (CHARGER_SYSTEM §5.2: ядро — первичный источник)
//
// Источник: checkpoints/08_25_technique_hold_analysis.md (план, подтверждён).
//
// Модель:
//   chargeRate = finalConductivity × COMBAT_CHANNEL_MULT × (1 + mastery × 0.005)  [Ци/тик]
//   Каждый тик: drain min(chargeRate, remaining) Ци через QiConsumeRequestEvent.
//   ChargedQi ≥ QiCost → завершение ( potency = 1000 + 1000 × (charged - qiCost)/(capacity - qiCost), кап 2000)
//   Сверх capacity → дестабилизация (Stage 2, пока только лог).
//
// Проводимость берётся из кэша QiChangedEvent (прецедент EVT-01) — QiService.UpdateState
// публикует QiChangedEvent с finalConductivity (см. QiService.cs:53,77).
using System;
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Combat
{
    /// <summary>
    /// Сервис зарядки техник (Stage 0 — модель заполнения).
    /// Управляет per-entity зарядками: запуск, тики (расход Ци по проводимости),
    /// завершение, отмена. Окно перезарядки [qiCost..capacity] → potency 1000–2000‰.
    ///
    /// АРХИТЕКТУРА: кэш Ци/проводимости из QiChangedEvent; расход через
    /// QiConsumeRequestEvent; возврат через QiAddRequestEvent. НЕ инжектит
    /// IQiService (паттерн EVT-01). TechniqueService поставляет данные техники
    /// (qiCost, capacity, mastery). ITimeService.DeltaTime — единица времени (тик).
    /// </summary>
    public class TechniqueChargeService : IDisposable
    {
        // === Зависимости ===
        private readonly ISubscriber<QiChangedEvent> _qiChangedSub;
        private readonly IPublisher<QiConsumeRequestEvent> _qiConsumeRequestPub;
        private readonly IPublisher<QiAddRequestEvent> _qiAddRequestPub;
        private readonly IPublisher<TechniqueChargeStartedEvent> _startedPub;
        private readonly IPublisher<TechniqueChargeProgressEvent> _progressPub;
        private readonly IPublisher<TechniqueChargeCompletedEvent> _completedPub;
        private readonly IPublisher<TechniqueChargeCancelledEvent> _cancelledPub;
        private readonly TechniqueService _techniqueService;

        // EVT-01: кэш состояния из событий (per-entity)
        private readonly Dictionary<string, QiCache> _qiCache = new();

        // IDisposable для подписок
        private IDisposable _qiChangedSubscription;

        // === Состояние ===
        // per-entity: одна активная зарядка на сущность (игрок/каждый NPC, если бы NPC юзал)
        private readonly Dictionary<string, ChargeState> _activeCharges = new();

        /// <summary>
        /// Состояние зарядки одной техники (per-entity).
        /// </summary>
        private class ChargeState
        {
            public string EntityId = "";
            public string TechniqueId = "";
            public long ChargedQi;       // накоплено Ци (как long, Fix-01)
            public long QiCost;         // цель зарядки (базовая мощность)
            public long Capacity;       // потолок перезарядки (для potency)
            public float Mastery;      // мастерство (для chargeRate)
            public int PotencyPermil;  // текущая мощность (1000-2000)
            public bool IsComplete;
            public int LastMouseX;       // курсор на момент последнего тика (милли-пиксели)
            public int LastMouseY;
        }

        /// <summary>Кэш состояния Ци для одной сущности (EVT-01).</summary>
        private struct QiCache
        {
            public long CurrentQi;
            public float Conductivity;
            public int CultivationLevel;
            public bool Valid;
        }

        // === Конструктор ===
        public TechniqueChargeService(
            ISubscriber<QiChangedEvent> qiChangedSub,
            IPublisher<QiConsumeRequestEvent> qiConsumeRequestPub,
            IPublisher<QiAddRequestEvent> qiAddRequestPub,
            IPublisher<TechniqueChargeStartedEvent> startedPub,
            IPublisher<TechniqueChargeProgressEvent> progressPub,
            IPublisher<TechniqueChargeCompletedEvent> completedPub,
            IPublisher<TechniqueChargeCancelledEvent> cancelledPub,
            TechniqueService techniqueService)
        {
            _qiChangedSub = qiChangedSub;
            _qiConsumeRequestPub = qiConsumeRequestPub;
            _qiAddRequestPub = qiAddRequestPub;
            _startedPub = startedPub;
            _progressPub = progressPub;
            _completedPub = completedPub;
            _cancelledPub = cancelledPub;
            _techniqueService = techniqueService;

            // EVT-01: подписка на кэш состояния Ци (per-entity)
            _qiChangedSubscription = _qiChangedSub.Subscribe((in QiChangedEvent e) => {
                _qiCache[e.EntityId] = new QiCache
                {
                    CurrentQi = e.Current,
                    Conductivity = e.Conductivity,
                    CultivationLevel = e.CultivationLevel,
                    Valid = true
                };
            });
        }

        /// <summary>
        /// Начать зарядку техники для сущности.
        /// Проверяет: нет активной зарядки; техника изучена; кулдаун = 0;
        /// достаточно Ци для начала (≥ MIN_QI_FOR_BUFFER); chargeRate ≥ MIN_CHARGE_RATE.
        /// НЕ списывает Ци тут — расход тиками в UpdateCharges.
        /// </summary>
        public bool StartCharge(string entityId, string techniqueId, int mouseX = 0, int mouseY = 0)
        {
            if (string.IsNullOrEmpty(entityId) || string.IsNullOrEmpty(techniqueId)) return false;
            if (_activeCharges.ContainsKey(entityId)) return false; // уже заряжает

            var tech = _techniqueService.GetTechnique(techniqueId);
            if (tech == null) return false;

            // Cultivation — пассивная, зарядка невозможна (TECHNIQUE_SYSTEM §11.5)
            if (tech.Type == TechniqueType.Cultivation) return false;

            // Кулдаун (TechniqueService.GetCooldown)
            if (_techniqueService.GetCooldown(techniqueId) > 0) return false;

            // Кэш Ци сущности (P0-DUAL-PLAYER-ID: QiChangedEvent публикуется под "player",
            // а PlayerService.PlayerId = "player_0"; нормализуем как в BodyService:455).
            if (!TryGetQiCache(entityId, out var cache)) return false;
            if (cache.CurrentQi < GameConstants.MIN_QI_FOR_BUFFER) return false;

            // chargeRate ≥ MIN_CHARGE_RATE (иначе зарядка бесконечная)
            float rate = ComputeChargeRate(cache.Conductivity, tech.Mastery);
            if (rate < GameConstants.MIN_CHARGE_RATE) return false;

            // Окно перезарядки: capacity = структурная ёмкость техники.
            // TECHNIQUE_SYSTEM §4: capacity = baseCapacity × 2^(L-1) × (1 + mastery/100 × 0.5).
            // qiCost = baseCapacity × 2^(L-1) (§5.2). Generator уже посчитал qiCost и capacity
            // как поле CapacityCost (см. TechniqueGeneratorService.CalculateCapacity).
            long qiCost = tech.QiCost;
            long capacity = tech.CapacityCost > 0 ? Math.Max(tech.CapacityCost, qiCost) : qiCost;

            _activeCharges[entityId] = new ChargeState
            {
                EntityId = entityId,
                TechniqueId = techniqueId,
                ChargedQi = 0L,
                QiCost = qiCost,
                Capacity = capacity,
                Mastery = tech.Mastery,
                PotencyPermil = GameConstants.POTENCY_BASE_PERMIL,
                IsComplete = false,
                LastMouseX = mouseX,
                LastMouseY = mouseY
            };

            // Событие начала (UI/визуал)
            int chargeRatePermil = qiCost > 0 ? (int)(rate * 1000L / qiCost) : 1000;
            _startedPub.Publish(new TechniqueChargeStartedEvent(
                entityId, techniqueId, qiCost, capacity, chargeRatePermil));

            return true;
        }

        /// <summary>
        /// Обновить все активные зарядки (вызывается из CombatModule.Tick).
        /// Каждый тик: drain min(chargeRate × deltaTime, remaining) Ци через QiConsumeRequestEvent.
        /// Завершение при ChargedQi ≥ QiCost → TechniqueChargeCompletedEvent.
        /// </summary>
        public void UpdateCharges(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            if (_activeCharges.Count == 0) return;

            List<string>? completedIds = null;
            List<string>? cancelledIds = null;

            foreach (var kvp in _activeCharges)
            {
                var state = kvp.Value;
                if (state.IsComplete) continue;

                string entityId = state.EntityId;
                string techId = state.TechniqueId;

                // Кэш Ци мог устареть — проверяем актуальный остаток
                // P0-DUAL-PLAYER-ID: QiChangedEvent публикуется под "player", charge keyed "player_0"
                if (!TryGetQiCache(entityId, out var cache))
                {
                    // Нет данных о Ци — прервать с возвратом
                    CancelChargeInternal(state, "no_qi_state", ref cancelledIds);
                    continue;
                }

                // Пересчёт chargeRate (проводимость/мастерство могли измениться)
                float rate = ComputeChargeRate(cache.Conductivity, state.Mastery);
                if (rate < GameConstants.MIN_CHARGE_RATE)
                {
                    CancelChargeInternal(state, "low_conductivity", ref cancelledIds);
                    continue;
                }

                // Сколько Ци можно влить в этот тик
                long remainingToFull = state.QiCost - state.ChargedQi;
                long qiToCharge = (long)Math.Ceiling(rate * deltaTime);
                if (qiToCharge <= 0) qiToCharge = 1; // минимум 1 Ци/тик (нулевые зарядки недопустимы)
                if (qiToCharge > remainingToFull && remainingToFull > 0)
                    qiToCharge = remainingToFull;

                // Если ещё позволяем перезарядку (Stage 2 опционально) — позволяем дойти до capacity.
                // На Stage 0 останавливаемся на qiCost (potency = 1000).
                if (remainingToFull <= 0)
                {
                    // Достигли qiCost — завершаем
                    state.PotencyPermil = GameConstants.POTENCY_BASE_PERMIL;
                    state.IsComplete = true;
                    completedIds ??= new List<string>();
                    completedIds.Add(entityId);
                    continue;
                }

                // Проверяем наличие Ци
                if (cache.CurrentQi < qiToCharge)
                {
                    // Ци не хватает на полный тик — вливаем остаток и завершаем на текущей мощности
                    if (cache.CurrentQi > 0)
                    {
                        _qiConsumeRequestPub.Publish(new QiConsumeRequestEvent(cache.CurrentQi, "TechniqueChargeService"));
                        state.ChargedQi += cache.CurrentQi;
                    }
                    // Завершаем (потенция ниже базовой, но ≥ 1000 — технически невозможно < 1000
                    // т.к. PotencyPermil = 1000 + 1000×(charged-qiCost)/(...) ; если charged < qiCost,
                    // не достигли завершения → отмена
                    CancelChargeInternal(state, "no_qi", ref cancelledIds);
                    continue;
                }

                // Списываем Ци (EVT-01: через событие)
                _qiConsumeRequestPub.Publish(new QiConsumeRequestEvent(qiToCharge, "TechniqueChargeService"));
                state.ChargedQi += qiToCharge;

                // Прогресс-событие (UI; раз в тик)
                _progressPub.Publish(new TechniqueChargeProgressEvent(
                    entityId, techId, state.ChargedQi, state.QiCost, state.PotencyPermil));

                // Проверка завершения
                if (state.ChargedQi >= state.QiCost)
                {
                    state.PotencyPermil = GameConstants.POTENCY_BASE_PERMIL; // Stage 0: только базовая мощность
                    state.IsComplete = true;
                    completedIds ??= new List<string>();
                    completedIds.Add(entityId);
                }
            }

            // Обработка завершённых зарядок (после цикла, чтобы не модифицировать словарь во время итерации)
            if (completedIds != null)
            {
                foreach (var id in completedIds)
                {
                    if (_activeCharges.TryGetValue(id, out var state))
                    {
                        _completedPub.Publish(new TechniqueChargeCompletedEvent(
                            state.EntityId, state.TechniqueId,
                            state.PotencyPermil, state.ChargedQi,
                            state.LastMouseX, state.LastMouseY));
                        // Не удаляем сразу — OnChargeCompleted (PlayerTechniqueCaster) вызовет
                        // CompleteUse, который может попросить ReleaseCharge. Но для упрощения —
                        // удаляем здесь, potency уже в событии.
                        _activeCharges.Remove(id);
                    }
                }
            }

            if (cancelledIds != null)
            {
                foreach (var id in cancelledIds)
                {
                    // Уже отменено в CancelChargeInternal (событие опубликовано), просто удаляем
                    _activeCharges.Remove(id);
                }
            }
        }

        /// <summary>
        /// Отменить зарядку сущности (возврат 50% ChargedQi).
        /// Причины: stun, user_cancel, save, low_conductivity, no_qi.
        /// </summary>
        public void CancelCharge(string entityId, string reason = "user_cancel")
        {
            if (!_activeCharges.TryGetValue(entityId, out var state)) return;
            List<string>? cancelledIds = null;
            CancelChargeInternal(state, reason, ref cancelledIds);
            if (cancelledIds != null)
                foreach (var id in cancelledIds) _activeCharges.Remove(id);
        }

        private void CancelChargeInternal(ChargeState state, string reason, ref List<string>? cancelledIds)
        {
            // Возврат 50% ChargedQi (ЗАПРЕТ 3.9: integer division)
            long refund = state.ChargedQi * GameConstants.CHARGE_CANCEL_REFUND_PERMIL / 1000;
            if (refund > 0) _qiAddRequestPub.Publish(new QiAddRequestEvent(refund, "TechniqueChargeService"));

            _cancelledPub.Publish(new TechniqueChargeCancelledEvent(
                state.EntityId, state.TechniqueId, refund, reason));

            cancelledIds ??= new List<string>();
            if (!cancelledIds.Contains(state.EntityId)) cancelledIds.Add(state.EntityId);
        }

        /// <summary>
        /// Получить мощность зарядки сущности (для CombatService.GetTechniquePotencyPermil).
        /// Возвращает 1000, если зарядка не активна/не завершена (базовая мощность).
        /// </summary>
        public int GetPotencyPermil(string entityId)
        {
            return _activeCharges.TryGetValue(entityId, out var s) ? s.PotencyPermil : GameConstants.POTENCY_BASE_PERMIL;
        }

        /// <summary>Заряжается ли сущность сейчас.</summary>
        public bool IsCharging(string entityId) => _activeCharges.ContainsKey(entityId);

        /// <summary>Получить прогресс зарядки (0..1) для UI; 0 если не заряжает.</summary>
        public float GetProgress(string entityId)
        {
            if (!_activeCharges.TryGetValue(entityId, out var s)) return 0f;
            if (s.QiCost <= 0) return 0f;
            return Math.Clamp((float)s.ChargedQi / s.QiCost, 0f, 1f);
        }

        /// <summary>Активная техника сущности (для UI). null если не заряжает.</summary>
        public string? GetActiveTechniqueId(string entityId)
        {
            return _activeCharges.TryGetValue(entityId, out var s) ? s.TechniqueId : null;
        }

        /// <summary>
        /// chargeRate = finalConductivity × COMBAT_CHANNEL_MULT × (1 + mastery × 0.005).
        /// См. Constants.COMBAT_CHANNEL_MULT и analysis §2.3.
        /// </summary>
        private static float ComputeChargeRate(float conductivity, float mastery)
        {
            float masteryBonus = 1f + mastery * 0.005f;
            return conductivity * GameConstants.COMBAT_CHANNEL_MULT * masteryBonus;
        }

        /// <summary>
        /// P0-DUAL-PLAYER-ID: QiChangedEvent публикуется под "player" (QiConfig.EntityId),
        /// а PlayerService.PlayerId = "player_0" (P0-баг 08_25: BodyService:455 — та же пара).
        /// Нормализуем lookup: ищем по entityId, иначе по альтернативному player-id.
        /// </summary>
        private bool TryGetQiCache(string entityId, out QiCache cache)
        {
            if (!string.IsNullOrEmpty(entityId) && _qiCache.TryGetValue(entityId, out var c) && c.Valid)
            {
                cache = c;
                return true;
            }
            // Альтернативный player-id
            string alt = IsPlayerId(entityId) ? (entityId == "player" ? "player_0" : "player") : entityId;
            if (_qiCache.TryGetValue(alt, out var altCache) && altCache.Valid)
            {
                cache = altCache;
                return true;
            }
            cache = default;
            return false;
        }

        private static bool IsPlayerId(string? id) => id == "player" || id == "player_0";

        public void Dispose()
        {
            _qiChangedSubscription?.Dispose();
            _qiChangedSubscription = null;
        }
    }
}

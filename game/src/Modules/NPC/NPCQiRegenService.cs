#nullable enable
// Создано: 2026-05-20 18:43:21 UTC
// Редактировано: 2026-05-21 08:15:33 UTC — Волна 2.4: подписка на DayChangedEvent (мёртвый код → живой)
// NPC L1+ имеет микроядро → Qi регенерация
// Формула: regenRate = coreCapacity × 0.1 / 86400 (в секунду)
// Источник: QiRegenCalculator (10% coreCapacity/сутки через микроядро)

using System;
using CultivationGame.Core;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.NPC
{
    /// <summary>
    /// Сервис регенерации Ци для NPC.
    /// NPC L1+ имеет микроядро → Qi регенерация по формуле QiRegenCalculator.
    /// Подписывается на DayChangedEvent для ежесуточной регенерации.
    /// Формула: regen = maxQi × 0.1 (10% от максимума в сутки).
    /// Волна 2.4: подключён через ISubscriber<DayChangedEvent> + Initialize().
    /// </summary>
    public sealed class NPCQiRegenService : IDisposable
    {
        private readonly IQiDataProvider _qiDataProvider;
        private readonly INPCService _npcService;
        private readonly ISubscriber<DayChangedEvent> _dayChangedSub;
        private IDisposable _dayChangedSubscription;

        public NPCQiRegenService(
            IQiDataProvider qiDataProvider,
            INPCService npcService,
            ISubscriber<DayChangedEvent> dayChangedSub)
        {
            _qiDataProvider = qiDataProvider;
            _npcService = npcService;
            _dayChangedSub = dayChangedSub;
        }

        /// <summary>
        /// Инициализация: подписка на DayChangedEvent.
        /// Вызывается из NPCModule.Start().
        /// Волна 2.4: без этого метода сервис — мёртвый код.
        /// </summary>
        public void Initialize()
        {
            _dayChangedSubscription = _dayChangedSub.Subscribe(OnDayChanged);
        }

        /// <summary>
        /// Обработать регенерацию Ци для всех NPC за один игровой день.
        /// Вызывается при DayChangedEvent.
        /// Формула: regen = maxQi × 0.1 (10% от максимума в сутки).
        /// Источник: QiRegenCalculator — coreCapacity × 0.1 / 86400 × 86400.
        /// </summary>
        public void OnDayChanged(in DayChangedEvent e)
        {
            var npcIds = _npcService.GetAllNPCIds();
            foreach (var npcId in npcIds)
            {
                if (!_qiDataProvider.HasEntity(npcId)) continue;

                long maxQi = _qiDataProvider.GetMaxQi(npcId);
                if (maxQi <= 0) continue;

                long currentQi = _qiDataProvider.GetCurrentQi(npcId);

                // 10% от maxQi в сутки (QiRegenCalculator: coreCapacity × 0.1 / 86400 × 86400)
                long regenAmount = (long)(maxQi * 0.1);
                long newQi = System.Math.Min(maxQi, currentQi + regenAmount);

                _qiDataProvider.SetQiState(npcId, newQi, maxQi, _qiDataProvider.GetConductivity(npcId));
            }
        }

        public void Dispose()
        {
            _dayChangedSubscription?.Dispose();
            _dayChangedSubscription = null;
        }
    }
}

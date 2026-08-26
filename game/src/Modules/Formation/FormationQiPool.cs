#nullable enable
// Создано: 2026-05-09
// Пул Ци формации.
// Управляет текущим/максимальным Ци, утечкой и заполнением.
// НЕ дублирует QiBuffer — это простой контейнер с drain-логикой.
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Formation
{
    /// <summary>
    /// Пул Ци формации.
    /// Управляет ёмкостью, текущим Ци, утечкой и заполнением.
    ///
    /// Архитектурное решение: FormationQiPool НЕ дублирует QiBuffer.
    /// QiBuffer — защита практика от урона, FormationQiPool — ресурс формации.
    /// У них разные формулы, разное назначение, разная логика drain.
    /// </summary>
    public class FormationQiPool : IDisposable
    {
        // === Зависимости ===
        private readonly IPublisher<FormationQiPoolChangedEvent> _poolChangedPub;

        // === Состояние ===
        private string _formationId;
        private long _currentQi;
        private long _maxQi;
        private int _drainInterval;    // Интервал утечки в тиках
        private long _drainAmount;     // Количество Ци за утечку
        private int _tickAccumulator;  // Аккумулятор тиков для дискретной утечки

        /// <summary>Текущее Ци в пуле</summary>
        public long CurrentQi => _currentQi;

        /// <summary>Максимальная ёмкость пула</summary>
        public long MaxQi => _maxQi;

        /// <summary>Коэффициент заполнения (0..1)</summary>
        public float FillRatio => _maxQi > 0 ? (float)_currentQi / _maxQi : 0f;

        /// <summary>Пул полностью заполнен</summary>
        public bool IsFull => _currentQi >= _maxQi;

        /// <summary>Пул пуст</summary>
        public bool IsEmpty => _currentQi <= 0;

        /// <summary>
        /// Конструктор пула Ци формации.
        /// </summary>
        public FormationQiPool(IPublisher<FormationQiPoolChangedEvent> poolChangedPub)
        {
            _poolChangedPub = poolChangedPub;
        }

        /// <summary>
        /// Инициализировать пул для формации.
        /// </summary>
        /// <param name="formationId">Идентификатор формации</param>
        /// <param name="formationLevel">Уровень формации</param>
        /// <param name="size">Размер формации</param>
        public void Initialize(string formationId, int formationLevel, FormationSize size)
        {
            _formationId = formationId;
            _maxQi = FormationCalculator.CalculateCapacity(formationLevel, size);
            _currentQi = 0;
            _drainInterval = FormationCalculator.CalculateDrainInterval(formationLevel);
            _drainAmount = FormationCalculator.CalculateDrainAmount(size);
            _tickAccumulator = 0;

            PublishPoolChanged();
        }

        /// <summary>
        /// Добавить Ци в пул (от участника или ядра).
        /// </summary>
        /// <param name="amount">Количество Ци</param>
        /// <returns>Фактически добавленное количество</returns>
        public long AddQi(long amount)
        {
            if (amount <= 0 || IsFull) return 0;

            long before = _currentQi;
            _currentQi = Math.Min(_maxQi, _currentQi + amount);
            long added = _currentQi - before;

            if (added > 0)
            {
                PublishPoolChanged();
            }

            return added;
        }

        /// <summary>
        /// Обработать тик утечки Ци (дискретная утечка).
        /// Вызывается из FormationModule при каждом TimeChangedEvent.
        /// Утечка происходит ТОЛЬКО в стадии Active.
        /// </summary>
        /// <param name="gameMinutesElapsed">Количество игровых минут (тиков)</param>
        /// <param name="drainSpeedMultiplier">Множитель скорости утечки</param>
        /// <returns>Количество потерянного Ци</returns>
        public long ProcessDrain(int gameMinutesElapsed, float drainSpeedMultiplier = 1.0f)
        {
            if (gameMinutesElapsed <= 0 || _drainInterval <= 0) return 0;

            long totalDrained = 0;
            _tickAccumulator += gameMinutesElapsed;

            // Дискретная утечка: N Ци каждые M тиков
            int drainCycles = _tickAccumulator / _drainInterval;
            if (drainCycles > 0)
            {
                _tickAccumulator -= drainCycles * _drainInterval;

                long drainTotal = (long)(drainCycles * _drainAmount * drainSpeedMultiplier);
                long before = _currentQi;
                _currentQi = Math.Max(0, _currentQi - drainTotal);
                totalDrained = before - _currentQi;

                if (totalDrained > 0)
                {
                    PublishPoolChanged();
                }
            }

            return totalDrained;
        }

        /// <summary>
        /// Сбросить пул (при деактивации формации).
        /// </summary>
        public void Reset()
        {
            _currentQi = 0;
            _tickAccumulator = 0;
            PublishPoolChanged();
        }

        /// <summary>
        /// Установить пул полностью заполненным (для тестов/отладки).
        /// </summary>
        public void FillToMax()
        {
            _currentQi = _maxQi;
            PublishPoolChanged();
        }

        public void Dispose()
        {
            // Сброс состояния при уничтожении (предотвращаем утечку событий)
            _formationId = null;
            _currentQi = 0;
            _tickAccumulator = 0;
        }

        private void PublishPoolChanged()
        {
            _poolChangedPub.Publish(new FormationQiPoolChangedEvent(
                _formationId, _currentQi, _maxQi));
        }
    }
}

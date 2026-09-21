#nullable enable
// Создано: 2026-05-08 12:52:40 UTC
// Буфер Ци зарядника — накопление и отдача Ци.
// Адаптировано из Legacy/Charger/ChargerBuffer.cs
// Замена: C# events → MessagePipe, прямой Time.deltaTime → ITimeService
// Редактировано: 2026-05-08 15:20 UTC — аудит CH-18: фикс потери Qi в аккумуляторе при почти полном буфере
using System;
using CultivationGame.Core;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Charger
{
    /// <summary>
    /// Буфер Ци зарядника.
    /// Управляет накоплением Ци от камней и отдачей практику.
    /// Источник: CHARGER_SYSTEM.md §2.2 "Буфер Ци"
    ///
    /// Формула: effectiveRate = min(totalRate, conductivity) × (1 - efficiencyLoss)
    /// </summary>
    public class ChargerBuffer
    {
        // === Состояние ===
        private long _capacity;
        private long _currentQi;
        private float _conductivity;
        private float _inputRate;
        private float _outputRate;
        private float _efficiencyLoss;

        // Аккумуляторы дробной части (предотвращают потерю Ци при малых deltaTime)
        private float _accumulationAccumulator;
        private double _transferAccumulator;

        // === Messaging ===
        private readonly IPublisher<ChargerBufferChangedEvent> _bufferChangedPublisher;

        // === Свойства ===

        public long Capacity => _capacity;
        public long CurrentQi => _currentQi;
        public float Conductivity => _conductivity;
        public bool IsFull => _currentQi >= _capacity;
        public bool IsEmpty => _currentQi <= 0;
        public float QiPercent => _capacity > 0 ? (float)_currentQi / _capacity : 0f;

        // === Конструктор ===

        public ChargerBuffer(IPublisher<ChargerBufferChangedEvent> bufferChangedPublisher)
        {
            _bufferChangedPublisher = bufferChangedPublisher;
        }

        // === Настройка ===

        /// <summary>
        /// Настроить буфер.
        /// </summary>
        public void Configure(long capacity, float conductivity, float efficiencyLoss = 0.1f)
        {
            _capacity = capacity;
            _conductivity = conductivity;
            _inputRate = conductivity;
            _outputRate = conductivity;
            _efficiencyLoss = efficiencyLoss;
            _currentQi = 0;
            _accumulationAccumulator = 0f;
            _transferAccumulator = 0.0;
        }

        // === Управление Ци ===

        /// <summary>
        /// Добавить Ци в буфер (от камней).
        /// </summary>
        /// <returns>Фактически добавленное количество</returns>
        public long AddQi(long amount)
        {
            if (amount <= 0) return 0;

            long spaceAvailable = _capacity - _currentQi;
            long added = Math.Min(amount, spaceAvailable);

            _currentQi += added;
            PublishBufferChanged();

            return added;
        }

        /// <summary>
        /// Извлечь Ци из буфера.
        /// </summary>
        /// <returns>Фактически извлечённое количество</returns>
        public long ExtractQi(long amount)
        {
            if (amount <= 0 || _currentQi <= 0) return 0;

            long extracted = Math.Min(amount, _currentQi);
            _currentQi -= extracted;

            PublishBufferChanged();

            return extracted;
        }

        // === Накопление (кадровое) ===

        // AUDIT-0921 B3 (P1/P0): AccumulateFromStones заменён парой
        // PeekAccumulation/CommitAccumulation — схема «извлечение-первым».
        // Прежний контракт (AddQi по СКОРОСТИ, DepleteStones долями с клампом)
        // при полупустых камнях добавлял в буфер БОЛЬШЕ, чем извлекал из
        // камней — Ци материализовалась из воздуха. Теперь буфер получает
        // только подтверждённо извлеченное (см. ChargerService.ProcessChargerOperation).

        /// <summary>
        /// Пик накопления: обновить дробный аккумулятор кадром и вернуть,
        /// сколько буфер ГОТОВ принять (формула ФОРМ-CHR-01 + floor),
        /// с клампом по свободному месту. БЕЗ AddQi — добавит только
        /// CommitAccumulation с подтверждённым камнями объёмом.
        /// </summary>
        public long PeekAccumulation(float totalStoneRate, float deltaTime)
        {
            if (IsFull) return 0; // аккумулятор заморожен (прежняя семантика)

            // ФОРМ-CHR-01: Унифицированная формула
            float effectiveRate = Math.Min(totalStoneRate, _conductivity) * (1f - _efficiencyLoss);
            _accumulationAccumulator += effectiveRate * deltaTime;

            long desired = (long)Math.Floor(_accumulationAccumulator);
            long freeSpace = _capacity - _currentQi;
            if (desired > freeSpace) desired = freeSpace; // B2-инвариант: извлечение не превысит буфер
            return desired;
        }

        /// <summary>
        /// Завершить кадр накопления: списать rate-intent (желаемое) из
        /// аккумулятора и добавить только confirmed (фактически извлечённое
        /// из камней — ≤ intent ≤ freeSpace, AddQi без потерь).
        /// Неисполненная часть intent исчезает: камни её не имели —
        /// банкировать её = обещать Ци из воздуха.
        /// </summary>
        public long CommitAccumulation(long intent, long confirmed)
        {
            if (intent > 0)
                _accumulationAccumulator = Math.Max(0f, _accumulationAccumulator - intent);
            if (confirmed <= 0) return 0;
            return AddQi(confirmed);
        }

        /// <summary>
        /// Передать Ци практику (медитация).
        /// Формула: effectiveRate = min(outputRate, practitionerConductivity) × (1 - efficiencyLoss)
        /// </summary>
        /// <returns>Переданное Ци</returns>
        public long TransferToPractitioner(float practitionerConductivity, float deltaTime)
        {
            if (IsEmpty) return 0;

            float effectiveRate = Math.Min(_outputRate, practitionerConductivity) * (1f - _efficiencyLoss);
            float qiThisFrame = effectiveRate * deltaTime;

            // Накапливаем дробную часть между кадрами
            double totalQi = qiThisFrame + _transferAccumulator;
            long toTransfer = (long)totalQi;
            _transferAccumulator = totalQi - toTransfer;

            if (toTransfer >= 1)
            {
                return ExtractQi(toTransfer);
            }

            return 0;
        }

        // === Использование для техник ===

        /// <summary>
        /// Использовать Ци для техники.
        /// Порядок: сначала ядро практика, потом буфер.
        /// </summary>
        public ChargerBufferResult UseQiForTechnique(long qiCost, long practitionerCurrentQi)
        {
            var result = new ChargerBufferResult
            {
                QiFromCore = 0,
                QiFromBuffer = 0,
                QiRemaining = _currentQi,
                QiLost = 0,
                WasBufferUsed = false,
                WasBufferDepleted = false
            };

            // 1. Сначала Ци из ядра практика
            if (practitionerCurrentQi >= qiCost)
            {
                result.QiFromCore = qiCost;
                return result;
            }

            // 2. Всё из ядра
            result.QiFromCore = practitionerCurrentQi;
            long remaining = qiCost - practitionerCurrentQi;

            // 3. Добираем из буфера (с потерями)
            if (_currentQi > 0 && remaining > 0)
            {
                long requiredFromBuffer = (long)Math.Ceiling(remaining / (1f - _efficiencyLoss));
                long availableFromBuffer = Math.Min(requiredFromBuffer, _currentQi);

                _currentQi -= availableFromBuffer;
                result.QiFromBuffer = availableFromBuffer;
                result.QiRemaining = _currentQi;
                result.QiLost = (long)(availableFromBuffer * _efficiencyLoss);
                result.WasBufferUsed = true;
                result.WasBufferDepleted = _currentQi <= 0;

                PublishBufferChanged();
            }

            return result;
        }

        /// <summary>
        /// Проверить, можно ли использовать технику.
        /// </summary>
        public bool CanUseTechnique(long qiCost, long practitionerCurrentQi)
        {
            long effectiveAvailable = practitionerCurrentQi + (long)(_currentQi * (1f - _efficiencyLoss));
            return effectiveAvailable >= qiCost;
        }

        /// <summary>
        /// Доступное Ци с учётом потерь.
        /// </summary>
        public long GetEffectiveQiAvailable(long practitionerCurrentQi)
        {
            return practitionerCurrentQi + (long)(_currentQi * (1f - _efficiencyLoss));
        }

        // === Утилиты ===

        /// <summary>Полная разрядка буфера</summary>
        public void Discharge()
        {
            _currentQi = 0;
            PublishBufferChanged();
        }

        // === Приватные ===

        private void PublishBufferChanged()
        {
            _bufferChangedPublisher.Publish(new ChargerBufferChangedEvent(_currentQi, _capacity));
        }
    }
}

#nullable enable
// Создано: 2026-05-08 12:52:40 UTC
// Редактировано: 2026-05-08 14:22:17 UTC — аудит CH-01: нормализация тепла (0-100 → 0-1.0), CH-02: убрано тепло от накопления
// Редактировано: 2026-05-08 15:20 UTC — аудит CH-16: добавлено свойство IsInCombat
// Тепловой баланс зарядника — нагрев, рассеивание, перегрев.
// Адаптировано из Legacy/Charger/ChargerHeat.cs
// Замена: C# events → MessagePipe, UnityEngine.Mathf → System.Math
using System;
using CultivationGame.Core;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;

namespace CultivationGame.Modules.Charger
{
    /// <summary>
    /// Тепловой баланс зарядника.
    /// Управляет нагревом, рассеиванием и перегревом.
    /// Источник: CHARGER_SYSTEM.md §4.3 "Тепловой баланс"
    ///
    /// Константы из GameConstants:
    /// - CHARGER_HEAT_GAIN_RATE = 0.05 (5% от Ци = тепло)
    /// - CHARGER_OVERHEAT_THRESHOLD = 1.0 (100%)
    /// - CHARGER_OVERHEAT_COOLDOWN = 30 сек
    /// - CHARGER_PASSIVE_COOLING_RATE = 0.01 (1%/сек)
    /// - CHARGER_COMBAT_COOLING_RATE = 0.005 (0.5%/сек)
    /// </summary>
    public class ChargerHeat
    {
        // === Состояние ===
        private float _currentHeat;      // 0-1.0 (нормализованное)
        private bool _isOverheated;
        private float _cooldownTimer;
        private bool _isInCombat;

        // === Messaging ===
        private readonly IPublisher<ChargerHeatChangedEvent> _heatChangedPublisher;
        private readonly IPublisher<ChargerOverheatedEvent> _overheatedPublisher;
        private readonly IPublisher<ChargerCooledDownEvent> _cooledDownPublisher;

        // === Свойства ===

        public float HeatLevel => _currentHeat;
        public HeatState State => GetHeatState();
        public bool IsOverheated => _isOverheated;
        public float CooldownRemaining => _cooldownTimer;
        public bool CanOperate => !_isOverheated;

        /// <summary>Зарядник в боевом режиме (медленное рассеивание, нет передачи Ци практика)</summary>
        public bool IsInCombat => _isInCombat;

        /// <summary>Эффективность зарядника (% от нормы). Снижается при высокой температуре.</summary>
        public float GetEfficiency()
        {
            if (_isOverheated) return 0f;
            // 100% при 0-30%, линейно снижается до 50% при 90%
            float heatPercent = _currentHeat * 100f;
            if (heatPercent <= 30f) return 1.0f;
            if (heatPercent >= 90f) return 0.5f;
            return 1.0f - (heatPercent - 30f) / 120f;
        }

        // === Конструктор ===

        public ChargerHeat(
            IPublisher<ChargerHeatChangedEvent> heatChangedPublisher,
            IPublisher<ChargerOverheatedEvent> overheatedPublisher,
            IPublisher<ChargerCooledDownEvent> cooledDownPublisher)
        {
            _heatChangedPublisher = heatChangedPublisher;
            _overheatedPublisher = overheatedPublisher;
            _cooledDownPublisher = cooledDownPublisher;
        }

        // === Управление теплом ===

        /// <summary>
        /// Добавить тепло от использования Ци.
        /// Формула: heatGain = qiUsed × CHARGER_HEAT_GAIN_RATE / 100
        /// Деление на 100 — приведение legacy-коэффициента (0-100) к нормализованному (0-1.0)
        /// FIX CH-01: без /100 мгновенный перегрев при любом qiUsed
        /// </summary>
        public void AddHeatFromQi(long qiUsed)
        {
            // Legacy: heat = qiUsed * 0.05 (в диапазоне 0-100)
            // Новый:  heat = qiUsed * 0.05 / 100 = qiUsed * 0.0005 (в диапазоне 0-1.0)
            float heatGained = (float)((double)qiUsed * GameConstants.CHARGER_HEAT_GAIN_RATE / 100.0);
            AddHeat(heatGained);
        }

        /// <summary>
        /// Добавить тепло (прямое значение).
        /// </summary>
        public void AddHeat(float amount)
        {
            if (_isOverheated) return;

            HeatState previousState = State;
            _currentHeat = Math.Min(1.0f, _currentHeat + amount);

            // Проверяем перегрев
            if (_currentHeat >= GameConstants.CHARGER_OVERHEAT_THRESHOLD && !_isOverheated)
            {
                _isOverheated = true;
                _cooldownTimer = GameConstants.CHARGER_OVERHEAT_COOLDOWN;
                _overheatedPublisher.Publish(new ChargerOverheatedEvent(_currentHeat, _cooldownTimer));
            }

            _heatChangedPublisher.Publish(new ChargerHeatChangedEvent(_currentHeat, State));
        }

        /// <summary>
        /// Рассеять тепло (вызывать каждый кадр).
        /// </summary>
        public void DissipateHeat(float deltaTime)
        {
            // Перегрев — уменьшаем таймер кулдауна
            if (_isOverheated)
            {
                _cooldownTimer -= deltaTime;

                if (_cooldownTimer <= 0f)
                {
                    _currentHeat = 0f;
                    _isOverheated = false;
                    _cooldownTimer = 0f;

                    _heatChangedPublisher.Publish(new ChargerHeatChangedEvent(0f, HeatState.Cool));
                    _cooledDownPublisher.Publish(new ChargerCooledDownEvent(0f));
                }
                return;
            }

            // Нормальное рассеивание
            float rate = _isInCombat
                ? GameConstants.CHARGER_COMBAT_COOLING_RATE
                : GameConstants.CHARGER_PASSIVE_COOLING_RATE;

            if (_currentHeat > 0f)
            {
                HeatState previousState = State;
                _currentHeat = Math.Max(0f, _currentHeat - rate * deltaTime);

                _heatChangedPublisher.Publish(new ChargerHeatChangedEvent(_currentHeat, State));
            }
        }

        // === Боевой режим ===

        /// <summary>Войти в боевой режим (медленное рассеивание)</summary>
        public void EnterCombat() => _isInCombat = true;

        /// <summary>Выйти из боевого режима</summary>
        public void ExitCombat() => _isInCombat = false;

        // === Утилиты ===

        /// <summary>Получить текущее состояние тепла</summary>
        public HeatState GetHeatState()
        {
            float heatPercent = _currentHeat * 100f;
            if (_isOverheated) return HeatState.Overheated;
            if (heatPercent >= 90f) return HeatState.Critical;
            if (heatPercent >= 60f) return HeatState.Hot;
            if (heatPercent >= 30f) return HeatState.Warm;
            return HeatState.Cool;
        }

        /// <summary>Сбросить тепло (для тестов)</summary>
        public void ResetHeat()
        {
            _currentHeat = 0f;
            _isOverheated = false;
            _cooldownTimer = 0f;
            _heatChangedPublisher.Publish(new ChargerHeatChangedEvent(0f, HeatState.Cool));
        }
    }
}

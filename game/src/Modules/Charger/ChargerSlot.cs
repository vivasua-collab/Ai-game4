#nullable enable
// Создано: 2026-05-08 12:52:40 UTC
// Слот для камня Ци в заряднике.
// Адаптировано из Legacy/Charger/ChargerSlot.cs + QiStone
using System;
using CultivationGame.Core;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Charger
{
    /// <summary>
    /// Камень Ци — источник энергии для зарядника.
    /// </summary>
    public class QiStone
    {
        private readonly string _stoneId;
        private readonly string _stoneName;
        private readonly QiStoneQuality _quality;
        private readonly QiStoneSize _size;
        private readonly Element _element;
        private long _currentQi;
        private readonly long _maxQi;
        private readonly float _releaseRate;

        // === Свойства ===

        public string StoneId => _stoneId;
        public string StoneName => _stoneName;
        public QiStoneQuality Quality => _quality;
        public QiStoneSize Size => _size;
        public Element Element => _element;
        public long CurrentQi => _currentQi;
        public long MaxQi => _maxQi;
        public float ReleaseRate => _releaseRate;
        public bool IsEmpty => _currentQi <= 0;
        public float QiPercent => _maxQi > 0 ? (float)_currentQi / _maxQi : 0f;

        // === Конструктор ===

        public QiStone(QiStoneQuality quality, QiStoneSize size, Element element = Element.Neutral)
        {
            _stoneId = Guid.NewGuid().ToString().Substring(0, 8);
            _quality = quality;
            _size = size;
            _element = element;

            // Характеристики из конфигураций
            float qualityMult = ChargerConfigs.GetStoneQualityMultiplier(quality);
            _maxQi = (long)(ChargerConfigs.GetStoneBaseQi(size) * qualityMult);
            _currentQi = _maxQi;
            _releaseRate = ChargerConfigs.GetStoneBaseRate(size) * qualityMult;
            _stoneName = $"{quality} {size} {element} Камень";
        }

        // === Управление Ци ===

        /// <summary>Извлечь Ци из камня</summary>
        public long ExtractQi(long amount)
        {
            if (_currentQi <= 0) return 0;
            long extracted = Math.Min(_currentQi, amount);
            _currentQi -= extracted;
            return extracted;
        }

        /// <summary>Рассчитать максимальную скорость высвобождения</summary>
        public float GetEffectiveReleaseRate(float conductivity)
        {
            return Math.Min(_releaseRate, conductivity);
        }
    }

    /// <summary>
    /// Слот для камня Ци в заряднике.
    /// </summary>
    public class ChargerSlot
    {
        private readonly int _slotIndex;
        private QiStone _insertedStone;
        private readonly bool _isActive;
        private readonly bool _isSealed;
        private readonly QiStoneQuality _minQualityRequired;
        private readonly QiStoneSize _maxSizeAllowed;
        private readonly float _absorptionBonus;
        private readonly float _qiRetention;

        // === Свойства ===

        public int SlotIndex => _slotIndex;
        public QiStone InsertedStone => _insertedStone;
        public bool IsActive => _isActive;
        public bool IsSealed => _isSealed;
        public bool HasStone => _insertedStone != null && !_insertedStone.IsEmpty;
        public bool CanInsert => _isActive && !_isSealed && (_insertedStone == null || _insertedStone.IsEmpty);
        public float AbsorptionBonus => _absorptionBonus;
        public float QiRetention => _qiRetention;

        /// <summary>Состояние слота (для IChargerService)</summary>
        public ChargerSlotState State
        {
            get
            {
                if (_isSealed) return ChargerSlotState.Sealed;
                if (!_isActive) return ChargerSlotState.Inactive;
                if (_insertedStone == null) return ChargerSlotState.Empty;
                if (_insertedStone.IsEmpty) return ChargerSlotState.Depleted;
                return ChargerSlotState.Active;
            }
        }

        // === Конструктор ===

        public ChargerSlot(ChargerSlotConfig config)
        {
            _slotIndex = config.Index;
            _isActive = config.IsActive;
            _isSealed = config.IsSealed;
            _minQualityRequired = config.MinQualityRequired;
            _maxSizeAllowed = config.MaxSizeAllowed;
            _absorptionBonus = config.AbsorptionBonus;
            _qiRetention = config.QiRetention;
        }

        // === Управление камнем ===

        /// <summary>Вставить камень в слот</summary>
        public bool InsertStone(QiStone stone)
        {
            if (!CanInsert) return false;
            if (!CanAcceptStone(stone)) return false;

            // Если старый камень истощён — убираем
            if (_insertedStone != null && _insertedStone.IsEmpty)
            {
                _insertedStone = null;
            }

            _insertedStone = stone;
            return true;
        }

        /// <summary>Извлечь камень из слота</summary>
        public QiStone RemoveStone()
        {
            if (_insertedStone == null) return null;
            QiStone stone = _insertedStone;
            _insertedStone = null;
            return stone;
        }

        /// <summary>Проверить, можно ли принять камень</summary>
        public bool CanAcceptStone(QiStone stone)
        {
            if (!_isActive || _isSealed) return false;
            if ((int)stone.Quality < (int)_minQualityRequired) return false;
            if ((int)stone.Size > (int)_maxSizeAllowed) return false;
            return true;
        }

        /// <summary>
        /// Получить текущее Ци камня в слоте (0 если пуст).
        /// </summary>
        public long GetStoneQi()
        {
            return _insertedStone?.CurrentQi ?? 0;
        }
    }
}

#nullable enable
// Создано: 2026-05-09 04:35:17 UTC
// Реализация IQiService.
// Перенесено из legacy QiController.cs с адаптацией под наш EventBus + DI.
// Fix-01: Все Qi-значения — long.
// НОВ-ДАН-01: double arithmetic для точности при высоких уровнях.
// EVT-01: подписка на QiConsumeRequestEvent и QiAddRequestEvent (command-события),
//   расширенный QiChangedEvent (CultivationLevel + Conductivity).
// Migrated from Ai-game3 (Unity+MessagePipe) to Ai-game4 (Godot+EventBus) 2026-08-15:
//   - using MessagePipe → using CultivationGame.Core.Events
//   - using UnityEngine → using System (Math/MathF)
//   - Handler signature: void OnXxx(XxxEvent e) → void OnXxx(in XxxEvent e)
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Qi;

/// <summary>
/// Реализация IQiService.
/// Управляет накоплением, расходом, регенерацией Ци и уровнями культивации.
/// Публикует события через EventBus вместо C# events.
/// </summary>
public class QiService : IQiService, IDisposable
{
    // === Зависимости (инжекция через конструктор) ===
    private readonly IPublisher<QiChangedEvent> _qiChangedPub;
    private readonly IPublisher<QiDepletedEvent> _qiDepletedPub;
    private readonly IPublisher<QiFullEvent> _qiFullPub;
    private readonly IPublisher<CultivationBreakthroughEvent> _breakthroughPub;
    private readonly IPublisher<CultivationLevelChangedEvent> _cultivationLevelChangedPub;

    // === Состояние ===
    private string _entityId = string.Empty;
    private IDisposable? _severedSubscription;
    private IDisposable? _qiConsumeRequestSubscription;
    private IDisposable? _qiAddRequestSubscription;
    private bool _heartSevered; // QI-C01: Флаг ампутации сердца

    /// <summary>QI-C01: Флаг ампутации сердца (влияет на регенерацию)</summary>
    public bool IsHeartSevered => _heartSevered;
    private int _cultivationLevel;
    private int _subLevel;
    private CoreQuality _coreQuality;

    private long _coreCapacity;      // Базовая ёмкость (без density)
    private long _currentQi;
    private long _maxQiCapacity;     // coreCapacity × quality × subLevelGrowth × density
    private float _qiDensity;
    private double _baseConductivity; // НОВ-ДАН-01: double для точности
    private float _conductivity;
    private float _conductivityBonus;
    private float _regenMultiplier;
    private bool _enablePassiveRegen;

    // НОВ-ДАН-01: double аккумулятор для точности регенерации
    private double _dailyAccumulator = 0.0;

    private bool _isInitialized;

    // === IQiService Properties ===

    public string EntityId => _entityId;
    public long CurrentQi => _currentQi;
    public long MaxQi => _maxQiCapacity;
    public float QiRatio => _maxQiCapacity > 0 ? (float)_currentQi / _maxQiCapacity : 0f;
    public bool IsEmpty => _currentQi <= 0;
    public bool IsFull => _currentQi >= _maxQiCapacity;
    public CultivationLevel CultivationLevel => (CultivationLevel)_cultivationLevel;
    public int SubLevel => _subLevel;
    public CoreQuality CoreQuality => _coreQuality;
    public long CoreCapacity => _coreCapacity;
    public float QiDensity => _qiDensity;
    public long EffectiveQi => (long)(_currentQi * _qiDensity);
    public float Conductivity => _conductivity;
    public float ConductivityBonus => _conductivityBonus;

    // === Конструктор (DI) ===

    public QiService(
        IPublisher<QiChangedEvent> qiChangedPub,
        IPublisher<QiDepletedEvent> qiDepletedPub,
        IPublisher<QiFullEvent> qiFullPub,
        IPublisher<CultivationBreakthroughEvent> breakthroughPub,
        IPublisher<CultivationLevelChangedEvent> cultivationLevelChangedPub,
        ISubscriber<BodyPartSeveredEvent> severedSubscriber,
        ISubscriber<QiConsumeRequestEvent> qiConsumeRequestSub,
        ISubscriber<QiAddRequestEvent> qiAddRequestSub)
    {
        _qiChangedPub = qiChangedPub;
        _qiDepletedPub = qiDepletedPub;
        _qiFullPub = qiFullPub;
        _breakthroughPub = breakthroughPub;
        _cultivationLevelChangedPub = cultivationLevelChangedPub;

        // QI-C01: подписка на ампутацию части тела
        _severedSubscription = severedSubscriber.Subscribe(OnBodyPartSevered);

        // EVT-01: подписка на command-события (вместо прямых вызовов от других модулей)
        // P0-X1 FIX: фильтрация по EntityId — обрабатываем только свои события (пустой EntityId = игрок)
        _qiConsumeRequestSubscription = qiConsumeRequestSub.Subscribe(OnQiConsumeRequest);
        _qiAddRequestSubscription = qiAddRequestSub.Subscribe(OnQiAddRequest);
    }

    // === Инициализация ===

    /// <summary>
    /// Инициализировать сервис конфигурацией.
    /// Вызывается из QiModule.Start().
    /// </summary>
    public void Initialize(QiConfig config)
    {
        _entityId = config.EntityId;
        _cultivationLevel = config.CultivationLevel;
        _subLevel = config.SubLevel;
        _coreQuality = config.CoreQuality;
        _conductivityBonus = config.ConductivityBonus;
        _enablePassiveRegen = config.EnablePassiveRegen;
        _regenMultiplier = config.RegenMultiplier;

        RecalculateStats();

        // Установить начальное Ци
        if (config.InitialQi < 0)
            _currentQi = _maxQiCapacity; // Заполнить до максимума
        else
            _currentQi = Math.Min(config.InitialQi, _maxQiCapacity);

        _isInitialized = true;

        // Оповестить о начальном состоянии
        PublishQiChanged();
    }

    // === IQiService: Операции с Qi ===

    public bool TryConsumeQi(long amount)
    {
        if (amount <= 0) return false;
        if (_currentQi >= amount)
        {
            _currentQi -= amount;
            PublishQiChanged();

            if (_currentQi <= 0)
            {
                _qiDepletedPub.Publish(new QiDepletedEvent(_entityId));
            }

            return true;
        }
        return false;
    }

    public void AddQi(long amount)
    {
        if (amount < 0) return;
        _currentQi = Math.Min(_maxQiCapacity, _currentQi + amount);
        PublishQiChanged();

        if (IsFull)
        {
            _qiFullPub.Publish(new QiFullEvent(_entityId));
        }
    }

    public void Regenerate(float delta)
    {
        if (!_isInitialized || !_enablePassiveRegen || delta <= 0f) return;

        long regenQi = QiRegenCalculator.CalculateRegen(
            _maxQiCapacity, _regenMultiplier, delta, ref _dailyAccumulator);

        if (regenQi > 0)
        {
            AddQi(regenQi);
        }
    }

    // === IQiService: Культивация ===

    public void SetConductivityBonus(float bonus)
    {
        _conductivityBonus = Math.Clamp(bonus, 0f, 2f);
        // НОВ-ДАН-01: пересчёт итоговой проводимости
        _conductivity = (float)(_baseConductivity * (1.0 + _conductivityBonus));
    }

    public bool CanBreakthrough(bool isMajorLevel)
    {
        return QiBreakthroughCalculator.CanBreakthrough(
            _currentQi, _cultivationLevel, _subLevel, _coreQuality, isMajorLevel);
    }

    public long CalculateBreakthroughRequirement(bool isMajorLevel)
    {
        return QiBreakthroughCalculator.CalculateRequirement(
            _cultivationLevel, _subLevel, _coreQuality, isMajorLevel);
    }

    public bool TryBreakthrough()
    {
        // Определяем тип прорыва
        bool isMajor = _subLevel >= 9;
        if (!CanBreakthrough(isMajor)) return false;

        // После прорыва Ци = 0
        _currentQi = 0;

        // P1-14 FIX: запоминаем старый уровень для события
        int oldLevel = _cultivationLevel;

        if (isMajor)
        {
            _cultivationLevel++;
            _subLevel = 0;
        }
        else
        {
            _subLevel++;
            if (_subLevel > 9)
            {
                _subLevel = 0;
                _cultivationLevel++;
            }
        }

        // Пересчёт характеристик
        RecalculateStats();

        // P1-14 FIX: публикуем CultivationLevelChangedEvent при изменении уровня
        if (_cultivationLevel != oldLevel)
        {
            _cultivationLevelChangedPub.Publish(new CultivationLevelChangedEvent(
                _entityId, oldLevel, _cultivationLevel));
        }

        // Оповестить о прорыве
        _breakthroughPub.Publish(new CultivationBreakthroughEvent(
            _entityId, _cultivationLevel, _subLevel, isMajor, true));

        PublishQiChanged();
        return true;
    }

    public void SetCultivationLevel(int level, int subLevel = 0)
    {
        int oldLevel = _cultivationLevel;
        _cultivationLevel = Math.Clamp(level, 1, 10);
        _subLevel = Math.Clamp(subLevel, 0, 9);
        RecalculateStats();
        PublishQiChanged();

        // P1-14 FIX: публикуем CultivationLevelChangedEvent при изменении уровня
        if (_cultivationLevel != oldLevel)
        {
            _cultivationLevelChangedPub.Publish(new CultivationLevelChangedEvent(
                _entityId, oldLevel, _cultivationLevel));
        }
    }

    // === Внутренние методы ===

    /// <summary>
    /// Пересчитать все характеристики на основе уровня культивации.
    /// </summary>
    private void RecalculateStats()
    {
        // Плотность Ци = 2^(level-1)
        _qiDensity = MathF.Pow(2, _cultivationLevel - 1);

        // Полная ёмкость (с под-уровнями и качеством)
        _coreCapacity = QiBreakthroughCalculator.CalculateFullCapacity(
            _cultivationLevel, _subLevel, _coreQuality);

        // MaxQi = coreCapacity × qiDensity
        _maxQiCapacity = QiBreakthroughCalculator.SafeMultiplyImplicit(
            _coreCapacity, _qiDensity);

        // Ограничиваем текущее Ци
        if (_currentQi > _maxQiCapacity)
            _currentQi = _maxQiCapacity;

        // НОВ-ДАН-01: double arithmetic для проводимости
        // FIX В-07: Проводимость от coreCapacity, не от maxQiCapacity
        _baseConductivity = (double)_coreCapacity / 360.0;
        _conductivity = (float)(_baseConductivity * (1.0 + _conductivityBonus));

        // Множитель регенерации по уровню
        if (_cultivationLevel >= 1 && _cultivationLevel <= 10)
        {
            _regenMultiplier = GameConstants.RegenerationMultipliers[_cultivationLevel - 1];
        }
    }

    /// <summary>
    /// QI-C01: Обработка ампутации части тела.
    /// При ампутации сердца — -50% регенерация Ци.
    /// EventBus handler signature: void OnXxx(in XxxEvent e).
    /// </summary>
    private void OnBodyPartSevered(in BodyPartSeveredEvent e)
    {
        if (e.EntityId != _entityId) return;

        if (e.Part == BodyPartType.Heart)
        {
            _heartSevered = true;
            _regenMultiplier *= 0.5f; // -50% регенерация
        }
    }

    /// <summary>P0-X1 FIX: обработать запрос расхода Ци.</summary>
    private void OnQiConsumeRequest(in QiConsumeRequestEvent e)
    {
        if (string.IsNullOrEmpty(e.EntityId) || e.EntityId == _entityId)
            TryConsumeQi(e.Amount);
    }

    /// <summary>P0-X1 FIX: обработать запрос добавления Ци.</summary>
    private void OnQiAddRequest(in QiAddRequestEvent e)
    {
        if (string.IsNullOrEmpty(e.EntityId) || e.EntityId == _entityId)
            AddQi(e.Amount);
    }

    /// <summary>
    /// QI-C01: Освобождение подписок.
    /// </summary>
    public void Dispose()
    {
        _severedSubscription?.Dispose();
        _severedSubscription = null;
        _qiConsumeRequestSubscription?.Dispose();
        _qiConsumeRequestSubscription = null;
        _qiAddRequestSubscription?.Dispose();
        _qiAddRequestSubscription = null;
    }

    private void PublishQiChanged()
    {
        _qiChangedPub.Publish(new QiChangedEvent(_entityId, _currentQi, _maxQiCapacity, _cultivationLevel, _conductivity));
    }
}

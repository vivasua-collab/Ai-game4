#nullable enable
// Создано: 2026-05-09 04:35:17 UTC
// Реализация IQiBufferService.
// Перенесено из legacy Combat/QiBuffer.cs в модуль Qi.
// Разрывает циклическую зависимость Qi ↔ Combat.
// Fix-01: Qi-значения — long.
// Migrated from Ai-game3 (Unity+MessagePipe) to Ai-game4 (Godot+EventBus) 2026-08-15.
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Qi;

/// <summary>
/// Реализация IQiBufferService.
/// Управляет Ци-буфером — защитой от урона через Ци.
/// Перенесён из Combat/QiBuffer.cs для разрыва цикла Qi ↔ Combat.
///
/// ТЕХНИКИ ЦИ (с резонансом):
///   Сырая Ци:  90% поглощение, 3:1 соотношение, 10% ВСЕГДА пробивает
///   Щит:       100% поглощение, 1:1 соотношение, 0% пробитие
/// ФИЗИЧЕСКИЙ УРОН (без резонанса):
///   Сырая Ци:  80% поглощение, 5:1 соотношение, 20% ВСЕГДА пробивает
///   Щит:       100% поглощение, 2:1 соотношение, 0% пробитие
/// </summary>
public class QiBufferService : IQiBufferService, IDisposable
{
    // === Зависимости ===
    private readonly IQiService _qiService;
    private readonly IPublisher<QiBufferActivatedEvent> _activatedPub;
    private readonly IPublisher<QiBufferDeactivatedEvent> _deactivatedPub;
    private readonly IPublisher<QiBufferStateChangedEvent> _bufferStateChangedPub;

    // === Подписки (command-события) ===
    private IDisposable? _bufferActivateSubscription;
    private IDisposable? _bufferDeactivateSubscription;

    // === Состояние ===
    private bool _isActive;
    private QiBufferMode _mode;
    private long _qiInvested;
    private long _qiConsumedDuringActivation; // QI-A05: трекинг потраченного Ци для корректного возврата
    private string _entityId;

    // === IQiBufferService Properties ===

    public bool IsActive => _isActive;
    public QiBufferMode Mode => _mode;
    public long QiInvested => _qiInvested;

    // === Конструктор (DI) ===

    public QiBufferService(
        IQiService qiService,
        IPublisher<QiBufferActivatedEvent> activatedPub,
        IPublisher<QiBufferDeactivatedEvent> deactivatedPub,
        IPublisher<QiBufferStateChangedEvent> bufferStateChangedPub,
        ISubscriber<QiBufferActivateRequestEvent> bufferActivateReqSub,
        ISubscriber<QiBufferDeactivateRequestEvent> bufferDeactivateReqSub)
    {
        _qiService = qiService;
        _activatedPub = activatedPub;
        _deactivatedPub = deactivatedPub;
        _bufferStateChangedPub = bufferStateChangedPub;
        _entityId = qiService.EntityId;

        // EVT-01: подписка на command-события (вместо прямых вызовов от CombatModule)
        _bufferActivateSubscription = bufferActivateReqSub.Subscribe(OnBufferActivateRequest);
        _bufferDeactivateSubscription = bufferDeactivateReqSub.Subscribe(OnBufferDeactivateRequest);
    }

    private void OnBufferActivateRequest(in QiBufferActivateRequestEvent e)
    {
        Activate(e.QiInvested, e.Mode);
    }

    private void OnBufferDeactivateRequest(in QiBufferDeactivateRequestEvent e)
    {
        Deactivate();
    }

    // === IQiBufferService ===

    public void Activate(long qiInvested, QiBufferMode mode)
    {
        if (_isActive) Deactivate();

        // Инвестируем Ци из QiService
        if (qiInvested > 0 && _qiService.TryConsumeQi(qiInvested))
        {
            _qiInvested = qiInvested;
        }
        else
        {
            // Не хватило Ци — активируем без инвестиции (сырая защита)
            _qiInvested = 0;
        }

        _mode = mode;
        _isActive = true;

        _activatedPub.Publish(new QiBufferActivatedEvent(_entityId, _mode, _qiInvested));
        _bufferStateChangedPub.Publish(new QiBufferStateChangedEvent(true, _mode, _qiInvested, _entityId));
    }

    public void Deactivate()
    {
        if (!_isActive) return;

        // QI-A05: Возвращаем ТОЛЬКО неизрасходованный остаток инвестированного Ци
        long returned = 0;
        long remaining = _qiInvested - _qiConsumedDuringActivation;
        if (remaining > 0)
        {
            returned = remaining;
            _qiService.AddQi(returned);
        }

        _isActive = false;
        _qiInvested = 0;
        _qiConsumedDuringActivation = 0;

        _deactivatedPub.Publish(new QiBufferDeactivatedEvent(_entityId, returned));
        _bufferStateChangedPub.Publish(new QiBufferStateChangedEvent(false, QiBufferMode.None, 0, _entityId));
    }

    public QiBufferResult AbsorbDamage(int incomingDamage, DamageType damageType)
    {
        if (!_isActive || incomingDamage <= 0)
        {
            return new QiBufferResult(0, incomingDamage, 0, _qiService.CurrentQi, false, false);
        }

        long currentQi = _qiService.CurrentQi;

        // Минимальное Ци для активации буфера
        if (currentQi < GameConstants.MIN_QI_FOR_BUFFER)
        {
            return new QiBufferResult(0, incomingDamage, 0, currentQi, _mode == QiBufferMode.Shield, false);
        }

        // Различаем: Qi-урон vs Физический урон
        bool isQiDamage = damageType == DamageType.Qi || damageType == DamageType.Elemental;

        QiBufferResult result = _mode == QiBufferMode.Shield
            ? ProcessShieldDamage(incomingDamage, currentQi, isQiDamage)
            : ProcessRawQiDamage(incomingDamage, currentQi, isQiDamage);

        // Тратим Ци
        if (result.QiConsumed > 0)
        {
            _qiService.TryConsumeQi(result.QiConsumed);
            // QI-A05: Отслеживаем потраченное из инвестиции для корректного возврата
            _qiConsumedDuringActivation += result.QiConsumed;
            // Обновляем QiRemaining после траты
            result = new QiBufferResult(
                result.AbsorbedDamage, result.PiercingDamage,
                result.QiConsumed, _qiService.CurrentQi,
                result.WasShieldActive, result.WasQiDepleted);

            // EVT-01: Оповещаем потребителей об изменении состояния буфера
            _bufferStateChangedPub.Publish(new QiBufferStateChangedEvent(true, _mode, _qiInvested, _entityId));
        }

        return result;
    }

    // === Приватные методы расчёта ===

    /// <summary>
    /// Обработка урона в режиме сырой Ци.
    /// Этап 2.1: integer math (промилле), без float.
    /// </summary>
    private QiBufferResult ProcessRawQiDamage(int damage, long currentQi, bool isQiDamage)
    {
        int absorptionPermil = isQiDamage
            ? GameConstants.RAW_QI_ABSORPTION_PERMIL
            : GameConstants.PHYSICAL_RAW_QI_ABSORPTION_PERMIL;

        int piercingPermil = isQiDamage
            ? GameConstants.RAW_QI_PIERCING_PERMIL
            : GameConstants.PHYSICAL_RAW_QI_PIERCING_PERMIL;

        int ratio = isQiDamage
            ? GameConstants.RAW_QI_RATIO_INT
            : GameConstants.PHYSICAL_RAW_QI_RATIO_INT;

        int absorbableDamage = (int)((long)damage * absorptionPermil / 1000);
        int guaranteedPiercing = (int)((long)damage * piercingPermil / 1000);
        long requiredQi = (long)absorbableDamage * ratio;

        if (currentQi >= requiredQi)
        {
            return new QiBufferResult(
                absorbableDamage, guaranteedPiercing,
                requiredQi, currentQi - requiredQi,
                false, false);
        }
        else
        {
            // Недостаточно Ци — частичное поглощение
            int absorbed = requiredQi > 0 ? (int)(absorbableDamage * currentQi / requiredQi) : 0;
            int piercingDamage = damage - absorbed;
            return new QiBufferResult(
                absorbed, piercingDamage,
                currentQi, 0,
                false, true);
        }
    }

    /// <summary>
    /// Обработка урона в режиме щита.
    /// </summary>
    private QiBufferResult ProcessShieldDamage(int damage, long currentQi, bool isQiDamage)
    {
        int ratio = isQiDamage
            ? GameConstants.SHIELD_QI_RATIO_INT
            : GameConstants.PHYSICAL_SHIELD_QI_RATIO_INT;

        long requiredQi = (long)damage * ratio;

        if (currentQi >= requiredQi)
        {
            return new QiBufferResult(
                damage, 0,
                requiredQi, currentQi - requiredQi,
                true, false);
        }
        else
        {
            // Недостаточно Ци — частичное поглощение
            int absorbed = requiredQi > 0 ? (int)((long)damage * currentQi / requiredQi) : 0;
            int piercingDamage = damage - absorbed;
            return new QiBufferResult(
                absorbed, piercingDamage,
                currentQi, 0,
                true, true);
        }
    }

    // === IDisposable ===

    public void Dispose()
    {
        _bufferActivateSubscription?.Dispose();
        _bufferActivateSubscription = null;
        _bufferDeactivateSubscription?.Dispose();
        _bufferDeactivateSubscription = null;
    }
}

#nullable enable
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Core.Messaging.Contracts;

// Created: 2026-05-08 (Ai-game3) — migrated 2026-08-15.
// Qi / cultivation contracts: changes, depletion, breakthrough, buffers, command-events.
// Fix-01: Current/Max are long (ЗАПРЕТ 2: no float for Qi values).
// EVT-01: extended for event-driven model — modules publish commands, QiService subscribes.

// QiBufferMode is defined in CultivationGame.Core.Interfaces (canonical — matches IQiService).

/// <summary>
/// Событие: изменение текущего Ци.
/// Fix-01: Current и Max — long.
/// EVT-01: расширено для событийной модели — добавлены CultivationLevel, Conductivity.
/// Все модули-потребители могут кэшировать эти данные вместо инъекции IQiService.
/// </summary>
public readonly struct QiChangedEvent
{
    public readonly string EntityId;
    public readonly long Current;
    public readonly long Max;
    public readonly int CultivationLevel;     // EVT-01: уровень культивации (1-10)
    public readonly float Conductivity;       // EVT-01: проводимость меридиан

    public QiChangedEvent(string entityId, long current, long max)
        { EntityId = entityId; Current = current; Max = max; CultivationLevel = 1; Conductivity = 0f; }

    public QiChangedEvent(string entityId, long current, long max, int cultivationLevel, float conductivity)
        { EntityId = entityId; Current = current; Max = max; CultivationLevel = cultivationLevel; Conductivity = conductivity; }
}

/// <summary>
/// Событие: Ци исчерпан (currentQi ≤ 0).
/// </summary>
public readonly struct QiDepletedEvent
{
    public readonly string EntityId;
    public QiDepletedEvent(string entityId) { EntityId = entityId; }
}

/// <summary>
/// Событие: Ци достиг максимума.
/// </summary>
public readonly struct QiFullEvent
{
    public readonly string EntityId;
    public QiFullEvent(string entityId) { EntityId = entityId; }
}

/// <summary>
/// Событие: прорыв уровня культивации.
/// Уровень — новый уровень после прорыва.
/// </summary>
public readonly struct CultivationBreakthroughEvent
{
    public readonly string EntityId;
    public readonly int Level;
    public readonly int SubLevel;
    public readonly bool IsMajor;
    public readonly bool Success;
    public CultivationBreakthroughEvent(string entityId, int level, int subLevel, bool isMajor, bool success)
        { EntityId = entityId; Level = level; SubLevel = subLevel; IsMajor = isMajor; Success = success; }
}

/// <summary>
/// Событие: изменение уровня культивации (P1-14 FIX).
/// Публикуется ТОЛЬКО при изменении уровня (не при каждом изменении Ци).
/// BodyService подписывается на это событие вместо QiChangedEvent
/// для кэширования CultivationLevel → регенерация.
/// </summary>
public readonly struct CultivationLevelChangedEvent
{
    public readonly string EntityId;
    public readonly int OldLevel;
    public readonly int NewLevel;

    public CultivationLevelChangedEvent(string entityId, int oldLevel, int newLevel)
        { EntityId = entityId; OldLevel = oldLevel; NewLevel = newLevel; }
}

/// <summary>
/// Событие: Ци-буфер активирован.
/// QiInvested — количество инвестированного Ци.
/// </summary>
public readonly struct QiBufferActivatedEvent
{
    public readonly string EntityId;
    public readonly QiBufferMode Mode;
    public readonly long QiInvested;
    public QiBufferActivatedEvent(string entityId, QiBufferMode mode, long qiInvested)
        { EntityId = entityId; Mode = mode; QiInvested = qiInvested; }
}

/// <summary>
/// Событие: Ци-буфер деактивирован.
/// QiReturned — количество возвращённого Ци.
/// </summary>
public readonly struct QiBufferDeactivatedEvent
{
    public readonly string EntityId;
    public readonly long QiReturned;
    public QiBufferDeactivatedEvent(string entityId, long qiReturned)
        { EntityId = entityId; QiReturned = qiReturned; }
}

// === EVT-01: НОВЫЕ COMMAND-СОБЫТИЯ (для полной независимости модулей) ===
// Модули-потребители публикуют эти события вместо прямых вызовов IQiService/IQiBufferService.
// Модуль Qi подписывается и обрабатывает.

/// <summary>
/// Команда: запрос на расход Ци.
/// Публикуется модулями-потребителями (Combat, Charger).
/// QiService подписывается и вызывает TryConsumeQi() внутренне.
/// P0-X1 FIX: добавлен EntityId — для NPC QiService должен знать, с какой сущности списывать Ци.
/// </summary>
public readonly struct QiConsumeRequestEvent
{
    public readonly long Amount;
    public readonly string RequesterId; // Идентификатор источника (для логирования)
    public readonly string EntityId;    // P0-X1: Сущность, с которой списывается Ци

    public QiConsumeRequestEvent(long amount, string requesterId = "", string entityId = "")
        { Amount = amount; RequesterId = requesterId; EntityId = entityId; }
}

/// <summary>
/// Команда: запрос на добавление Ци.
/// Публикуется модулями-потребителями (Charger, TechniqueChargeService).
/// QiService подписывается и вызывает AddQi() внутренне.
/// P0-X1 FIX: добавлен EntityId — для NPC QiService должен знать, какой сущности добавлять Ци.
/// </summary>
public readonly struct QiAddRequestEvent
{
    public readonly long Amount;
    public readonly string RequesterId;
    public readonly string EntityId;    // P0-X1: Сущность, которой добавляется Ци

    public QiAddRequestEvent(long amount, string requesterId = "", string entityId = "")
        { Amount = amount; RequesterId = requesterId; EntityId = entityId; }
}

/// <summary>
/// Команда: запрос на активацию Ци-буфера.
/// Публикуется CombatModule вместо прямого вызова IQiBufferService.Activate().
/// QiBufferService подписывается и обрабатывает.
/// </summary>
public readonly struct QiBufferActivateRequestEvent
{
    public readonly long QiInvested;
    public readonly QiBufferMode Mode;

    public QiBufferActivateRequestEvent(long qiInvested, QiBufferMode mode)
        { QiInvested = qiInvested; Mode = mode; }
}

/// <summary>
/// Команда: запрос на деактивацию Ци-буфера.
/// Публикуется CombatModule вместо прямого вызова IQiBufferService.Deactivate().
/// QiBufferService подписывается и обрабатывает.
/// </summary>
public readonly struct QiBufferDeactivateRequestEvent
{
    // C# 9.0 не поддерживает parameterless struct constructor — используем параметр-заглушку
    public readonly string RequesterId;
    public QiBufferDeactivateRequestEvent(string requesterId = "")
        { RequesterId = requesterId; }
}

/// <summary>
/// Событие: изменение состояния Ци-буфера (для кэша потребителей).
/// Публикуется QiBufferService при любом изменении состояния.
/// Combat/DamageService кэшируют вместо инъекции IQiBufferService.
/// </summary>
public readonly struct QiBufferStateChangedEvent
{
    public readonly bool IsActive;
    public readonly QiBufferMode Mode;
    public readonly long QiInvested;
    public readonly string EntityId;

    public QiBufferStateChangedEvent(bool isActive, QiBufferMode mode, long qiInvested, string entityId)
        { IsActive = isActive; Mode = mode; QiInvested = qiInvested; EntityId = entityId; }
}

// === Медитация (QI_SYSTEM.md §5.2) — события этапа 1 внедрения ЦИ (2026-08-23) ===

/// <summary>
/// Команда: переключить состояние медитации игрока.
/// Публикуется Adapter (клавиша V) и модулями-потребителями (бой/движение — отмена).
/// QiModule подписывается, владеет состоянием, публикует MeditationStateChangedEvent.
/// </summary>
public readonly struct MeditationToggleRequestedEvent
{
    public readonly bool DesiredState; // true = включить, false = выключить (toggle при DesiredState != текущего)

    public MeditationToggleRequestedEvent(bool desiredState)
        { DesiredState = desiredState; }
}

/// <summary>
/// Событие: изменилось состояние медитации игрока.
/// Публикуется QiModule. Потребители: Adapter (индикация), Combat (отмена).
/// </summary>
public readonly struct MeditationStateChangedEvent
{
    public readonly bool IsActive;
    public readonly float RatePerSecond; // текущая скорость поглощения (conductivity × environmentMult)

    public MeditationStateChangedEvent(bool isActive, float ratePerSecond)
        { IsActive = isActive; RatePerSecond = ratePerSecond; }
}

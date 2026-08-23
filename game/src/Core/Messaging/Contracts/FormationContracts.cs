#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Messaging.Contracts;

// Created: 2026-05-08 (Ai-game3) — migrated 2026-08-15.
// Formation contracts: activation, deactivation, Qi pool, stage changes, contribution commands.
// Этап 5 внедрения ЦИ (2026-08-23): FormationActivatedEvent +FormationType +позиция
// (для Gathering-бонуса медитации и визуализатора), старые конструкторы сохранены.

// FormationStage is defined in CultivationGame.Core.Data (canonical).

/// <summary>
/// Событие: формация активирована.
/// Публикуется FormationService при переходе в стадию Active.
/// </summary>
public readonly struct FormationActivatedEvent
{
    public readonly string FormationId;
    public readonly string CasterId;
    public readonly FormationType Type;      // Этап 5: тип (Gathering → ×2 медитация и т.д.)
    public readonly int PositionX;           // Этап 5: позиция центра (тайлы)
    public readonly int PositionY;
    public readonly int EffectRadiusMeters;  // Этап 5: радиус действия

    public FormationActivatedEvent(string formationId, string casterId)
        { FormationId = formationId; CasterId = casterId; Type = FormationType.Barrier; PositionX = 0; PositionY = 0; EffectRadiusMeters = 50; }

    public FormationActivatedEvent(string formationId, string casterId,
        FormationType type, int posX, int posY, int effectRadiusMeters)
    {
        FormationId = formationId; CasterId = casterId; Type = type;
        PositionX = posX; PositionY = posY; EffectRadiusMeters = effectRadiusMeters;
    }
}

/// <summary>
/// Событие: формация деактивирована.
/// Публикуется FormationService при деактивации (DeactivateFormation).
/// </summary>
public readonly struct FormationDeactivatedEvent
{
    public readonly string FormationId;
    public readonly FormationStage PreviousStage;
    public readonly FormationType Type;      // Этап 5: тип (сброс Gathering-бонуса)
    public FormationDeactivatedEvent(string formationId, FormationStage previousStage)
        { FormationId = formationId; PreviousStage = previousStage; Type = FormationType.Barrier; }

    public FormationDeactivatedEvent(string formationId, FormationStage previousStage, FormationType type)
        { FormationId = formationId; PreviousStage = previousStage; Type = type; }
}

/// <summary>
/// Событие: изменение пула Ци формации.
/// Публикуется при каждом изменении текущего Ци в пуле.
/// </summary>
public readonly struct FormationQiPoolChangedEvent
{
    public readonly string FormationId;
    public readonly long CurrentQi;
    public readonly long MaxQi;
    public readonly float FillRatio;
    public FormationQiPoolChangedEvent(string formationId, long currentQi, long maxQi)
    {
        FormationId = formationId;
        CurrentQi = currentQi;
        MaxQi = maxQi;
        FillRatio = maxQi > 0 ? (float)currentQi / maxQi : 0f;
    }
}

/// <summary>
/// Событие: изменение стадии формации.
/// Публикуется при каждом переходе между стадиями.
/// </summary>
public readonly struct FormationStageChangedEvent
{
    public readonly string FormationId;
    public readonly FormationStage PreviousStage;
    public readonly FormationStage NewStage;
    public FormationStageChangedEvent(string formationId, FormationStage previousStage, FormationStage newStage)
        { FormationId = formationId; PreviousStage = previousStage; NewStage = newStage; }
}

/// <summary>
/// Команда: запрос на внесение Ци в формацию.
/// Публикуется внешними системами (UI, AI) для внесения Ци.
/// FormationModule подписывается и вызывает FormationService.ContributeQi().
/// </summary>
public readonly struct FormationContributeQiRequestEvent
{
    public readonly string ContributorId;
    public readonly long Amount;
    public FormationContributeQiRequestEvent(string contributorId, long amount)
        { ContributorId = contributorId; Amount = amount; }
}

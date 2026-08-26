#nullable enable
// Создано: 2026-08-25 (Stage 0 — модель заполнения техник, GLM-5.3)
// Контракты событий зарядки техник и удержания в ауре (Stage 1, вариант В).
//
// Источник: checkpoints/08_25_technique_hold_analysis.md (план, подтверждён 2026-08-25).
// Модель: TechniqueChargeService.StartCharge → UpdateCharges (тик) → Completed/Cancelled;
// AuraHoldService.Hold/Release/Decay → HeldTechniqueChangedEvent.
//
// Все события — readonly struct (zero-alloc, паттерн проекта).
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Messaging.Contracts
{
    /// <summary>
    /// Событие: началась зарядка техники (Stage 0).
    /// Публикуется TechniqueChargeService.StartCharge. Потребители: UI (полоса зарядки),
    /// Adapter (визуал начала каста).
    /// </summary>
    public readonly struct TechniqueChargeStartedEvent
    {
        public readonly string EntityId;
        public readonly string TechniqueId;
        public readonly long QiCost;          // Цель зарядки (сколько Ци нужно влить)
        public readonly long Capacity;       // Потолок перезарядки (для UI; -1 = без перезарядки)
        public readonly int ChargeRatePermil; // Скорость в промилле от qiCost/тик (для UI темпа)
        public TechniqueChargeStartedEvent(string entityId, string techniqueId,
            long qiCost, long capacity, int chargeRatePermil)
        {
            EntityId = entityId;
            TechniqueId = techniqueId;
            QiCost = qiCost;
            Capacity = capacity;
            ChargeRatePermil = chargeRatePermil;
        }
    }

    /// <summary>
    /// Событие: прогресс зарядки (публикуется раз в тик во время зарядки).
    /// Публикуется TechniqueChargeService.UpdateCharges. Потребители: UI (полоса).
    /// </summary>
    public readonly struct TechniqueChargeProgressEvent
    {
        public readonly string EntityId;
        public readonly string TechniqueId;
        public readonly long ChargedQi;
        public readonly long QiCost;
        public readonly int PotencyPermil;  // текущая мощность (1000-2000 при перезарядке)
        public TechniqueChargeProgressEvent(string entityId, string techniqueId,
            long chargedQi, long qiCost, int potencyPermil)
        {
            EntityId = entityId;
            TechniqueId = techniqueId;
            ChargedQi = chargedQi;
            QiCost = qiCost;
            PotencyPermil = potencyPermil;
        }
    }

    /// <summary>
    /// Событие: зарядка завершена (ChargedQi ≥ QiCost).
    /// Публикуется TechniqueChargeService.UpdateCharges. Потребители:
    /// PlayerTechniqueCaster (dispatch эффекта или подвязка в ауру), UI.
    ///
    /// ВАРИАНТ В (Stage 1): после этого события PlayerTechniqueCaster решает —
    /// если аура свободна → Hold (AuraHoldService); если занята → Fire немедленно.
    /// </summary>
    public readonly struct TechniqueChargeCompletedEvent
    {
        public readonly string EntityId;
        public readonly string TechniqueId;
        public readonly int PotencyPermil;     // мощность на момент завершения (1000-2000)
        public readonly long ChargedQi;        // сколько Ци влито (≥ QiCost)
        public readonly int TargetMouseX;      // курсор на момент завершения (милли-пиксели)
        public readonly int TargetMouseY;
        public TechniqueChargeCompletedEvent(string entityId, string techniqueId,
            int potencyPermil, long chargedQi, int mouseX, int mouseY)
        {
            EntityId = entityId;
            TechniqueId = techniqueId;
            PotencyPermil = potencyPermil;
            ChargedQi = chargedQi;
            TargetMouseX = mouseX;
            TargetMouseY = mouseY;
        }
    }

    /// <summary>
    /// Событие: зарядка отменена (старание/конецЦи/отмена/сейв).
    /// Публикуется TechniqueChargeService.CancelCharge. Потребители: UI.
    /// </summary>
    public readonly struct TechniqueChargeCancelledEvent
    {
        public readonly string EntityId;
        public readonly string TechniqueId;
        public readonly long RefundedQi;      // возвращено Ци (CancelCharge: 50%)
        public readonly string Reason;        // "stun" / "no_qi" / "user_cancel" / "save"
        public TechniqueChargeCancelledEvent(string entityId, string techniqueId,
            long refundedQi, string reason)
        {
            EntityId = entityId;
            TechniqueId = techniqueId;
            RefundedQi = refundedQi;
            Reason = reason ?? string.Empty;
        }
    }

    /// <summary>
    /// Событие: изменилось удержание техники в ауре (Stage 1, вариант В).
    /// Публикуется AuraHoldService. Потребители: UI (индикатор), Adapter (визуал ауры).
    ///
    /// TechniqueId == "" — аура пуста (release/dissipate); иначе — техника удерживается.
    /// </summary>
    public readonly struct HeldTechniqueChangedEvent
    {
        public readonly string EntityId;
        public readonly string TechniqueId;   // "" = аура пуста
        public readonly int PotencyPermil;     // текущая мощность удерживаемой
        public readonly Element Element;      // стихия (для цвета ауры)
        public HeldTechniqueChangedEvent(string entityId, string techniqueId,
            int potencyPermil, Element element)
        {
            EntityId = entityId;
            TechniqueId = techniqueId ?? string.Empty;
            PotencyPermil = potencyPermil;
            Element = element;
        }
    }
}

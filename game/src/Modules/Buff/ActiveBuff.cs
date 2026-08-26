#nullable enable
// Создано: 2026-05-09 05:15:31 UTC
// Редактировано: 2026-05-09 — BF-A02: добавлено поле Potency, исправлен ToData()
// Редактировано: 2026-05-10 11:05:00 UTC — B5-E01 FIX: TotalValue учитывает Potency
// Runtime-состояние активного баффа.
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Buff
{
    /// <summary>
    /// Runtime-состояание активного баффа на сущности.
    /// Внутренний класс модуля Buff — НЕ экспортируется через интерфейс.
    /// Для внешнего доступа используется ActiveBuffData (readonly struct).
    /// </summary>
    public class ActiveBuff
    {
        // === Идентификация ===
        public string BuffId;
        public BuffType Type;
        public bool IsDebuff;
        public Element Element;

        // === Параметры ===
        public float Value;             // Значение эффекта
        public bool IsPercentage;       // true = %, false = абсолютное
        public StatType? AffectedStat;  // Затрагиваемая характеристика (null для DoT/CC)
        public float Potency;           // BF-A02: Множитель мощности при наложении (из ApplyBuff)

        // === Длительность ===
        public BuffApplication Application;
        public float Duration;          // Исходная длительность
        public float RemainingDuration;
        public int MaxStacks;
        public BuffStacking StackingBehavior;
        public int CurrentStacks = 1;

        // === Тики (DoT/HoT) ===
        public bool HasTickEffect;
        public float TickInterval;
        public float TickTimer;
        public float TickDamage;
        public float TickHealing;

        // === Цель ===
        public string EntityId;

        // === Свойства ===

        /// <summary>Прогресс длительности (0.0 - 1.0)</summary>
        public float Progress => Duration > 0 ? 1f - (RemainingDuration / Duration) : 0f;

        /// <summary>Итоговое значение с учётом мощности и стеков (BF-A02)</summary>
        public float TotalValue => Value * Potency * CurrentStacks;

        /// <summary>Истёк ли бафф</summary>
        public bool IsExpired => Application != BuffApplication.Permanent && RemainingDuration <= 0f;

        /// <summary>Преобразовать в readonly struct для внешнего доступа</summary>
        /// <remarks>BF-A02: Передаём Potency вместо Value в ActiveBuffData</remarks>
        public ActiveBuffData ToData() => new ActiveBuffData(
            EntityId, BuffId, Type, IsDebuff, Element,
            RemainingDuration, Potency, CurrentStacks);
    }
}

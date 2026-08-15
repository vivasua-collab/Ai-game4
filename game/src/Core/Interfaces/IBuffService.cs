#nullable enable
// Создано: 2026-05-09 05:15:31 UTC
// Редактировано: 2026-05-22 07:55:00 UTC — Аудит CRIT-1: +GetStatModifierPermil() для ЗАПРЕТ 3.9
// Интерфейс сервиса баффов/дебаффов.
// Источник: BUFF_MODIFIERS_SYSTEM.md
using System.Collections.Generic;

using CultivationGame.Core;
using CultivationGame.Core.Data;
namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Интерфейс сервиса баффов/дебаффов.
    /// Управляет наложением, снятием, тиканием и расчётом модификаторов.
    /// ⛔ НЕ модифицирует: первичные статы, coreCapacity, qiDensity, qiRegen.
    /// </summary>
    public interface IBuffService
    {
        // === Управление баффами ===

        /// <summary>Наложить бафф на сущность. duration=-1 — использовать длительность из BuffData.</summary>
        bool ApplyBuff(string entityId, string buffId, float duration = -1f, float potency = 1f);

        /// <summary>Снять бафф с сущности.</summary>
        bool RemoveBuff(string entityId, string buffId);

        /// <summary>Снять все баффы с сущности.</summary>
        void RemoveAllBuffs(string entityId);

        /// <summary>Проверить наличие баффа.</summary>
        bool HasBuff(string entityId, string buffId);

        // === Запросы модификаторов ===

        /// <summary>Получить суммарный модификатор характеристики (flat + percent).</summary>
        float GetStatModifier(string entityId, StatType stat);

        /// <summary>
        /// Получить суммарный модификатор характеристики в промилле (ЗАПРЕТ 3.9).
        /// 1000 = ×1.0 (нет модификатора), 1200 = ×1.2 (+20%), 800 = ×0.8 (-20%).
        /// Аудит CRIT-1: для integer math в боевом пайплайне.
        /// </summary>
        int GetStatModifierPermil(string entityId, StatType stat);

        /// <summary>Получить сопротивление элементу (0.0 - 1.0).</summary>
        float GetElementResistance(string entityId, Element element);

        /// <summary>Проверить иммунитет к типу эффекта.</summary>
        bool HasImmunity(string entityId, BuffType immunityType);

        /// <summary>Получить список активных баффов сущности.</summary>
        IReadOnlyList<ActiveBuffData> GetActiveBuffs(string entityId);

        // === Тикание ===

        /// <summary>Обновить все баффы (вызывается из BuffModule.ITickable.Tick()).</summary>
        void TickBuffs(float deltaTime);
    }

    /// <summary>
    /// Данные активного баффа (readonly struct — zero-GC).
    /// </summary>
    public readonly struct ActiveBuffData
    {
        public readonly string EntityId;
        public readonly string BuffId;
        public readonly BuffType Type;
        public readonly bool IsDebuff;
        public readonly Element Element;
        public readonly float RemainingDuration;
        public readonly float Potency;
        public readonly int CurrentStacks;

        public ActiveBuffData(string entityId, string buffId, BuffType type, bool isDebuff,
            Element element, float remainingDuration, float potency, int currentStacks)
        {
            EntityId = entityId;
            BuffId = buffId;
            Type = type;
            IsDebuff = isDebuff;
            Element = element;
            RemainingDuration = remainingDuration;
            Potency = potency;
            CurrentStacks = currentStacks;
        }
    }
}

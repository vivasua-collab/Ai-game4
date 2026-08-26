#nullable enable
// Создано: 2026-05-08 10:07:00 UTC
// Редактировано: 2026-05-18 — Body доработка: GetStatDomain, SetStat, VirtualDelta, ConsolidateSleep, Threshold
using CultivationGame.Core;

using CultivationGame.Core.Data;
namespace CultivationGame.Core.Interfaces
{
    public interface IStatService
    {
        // === Текущие значения ===
        float GetStat(StatType type);
        float GetStatBonus(StatType type);

        // === Модификация (с публикацией StatChangedEvent) ===
        void ModifyStat(StatType type, float delta);
        void SetStat(StatType type, float value);

        // === Домены (П.23) ===
        StatDomain GetStatDomain(StatType type);

        // === Виртуальная дельта (STAT_THRESHOLD_SYSTEM.md) ===
        float GetVirtualDelta(StatType type);
        void AddVirtualDelta(StatType type, float amount);

        /// <summary>Закрепление при сне (минимум 4 часа)</summary>
        void ConsolidateSleep(float hours);

        // === Порог развития ===
        float GetThreshold(StatType type);
        bool CanAdvance(StatType type);
    }
}

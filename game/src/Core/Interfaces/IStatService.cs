#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Interfaces;

/// <summary>
/// Stat service: base stats + additive bonuses (from equipment, buffs,
/// techniques). <c>GetStatWithBonuses</c> is the hot-path read.
/// </summary>
public interface IStatService
{
    float GetStat(int entityId, StatType stat);
    void AddBonus(int entityId, StatType stat, float value);
    void RemoveBonus(int entityId, StatType stat, float value);
    float GetStatWithBonuses(int entityId, StatType stat);
}

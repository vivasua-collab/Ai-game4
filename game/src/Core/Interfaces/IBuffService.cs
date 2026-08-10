#nullable enable
using System.Collections.Generic;

namespace CultivationGame.Core.Interfaces;

/// <summary>Buffs / debuffs with duration and per-tick expiry.</summary>
public interface IBuffService
{
    void ApplyBuff(int entityId, string buffId, float duration);
    void RemoveBuff(int entityId, string buffId);
    void TickBuffs(int entityId);
    IReadOnlyList<string> GetActiveBuffs(int entityId);
}

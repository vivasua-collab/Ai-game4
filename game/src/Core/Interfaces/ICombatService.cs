#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Interfaces;

/// <summary>Combat: tick-resolved real-time-with-pause, NOT physics-driven.</summary>
public interface ICombatService
{
    void ProcessAttack(int attackerId, int targetId, TechniqueData technique);
    float CalculateDamage(int attackerId, int targetId, TechniqueData technique);
    bool IsInCombat(int entityId);
}

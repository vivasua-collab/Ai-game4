#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Interfaces;

/// <summary>Kenshi-style body parts: dual HP, amputation, regeneration.</summary>
public interface IBodyService
{
    void DamagePart(int entityId, BodyPartType part, float damage, DamageType type);
    void HealPart(int entityId, BodyPartType part, float amount);
    bool IsPartSevered(int entityId, BodyPartType part);
    float GetPartHealth(int entityId, BodyPartType part);
    void ProcessRegeneration(int entityId);
}

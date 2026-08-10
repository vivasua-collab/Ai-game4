#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Interfaces;

/// <summary>Magical formation management: barrier / trap / amplification / etc.</summary>
public interface IFormationService
{
    void CreateFormation(int creatorId, Position2D center, FormationType type, int level);
    void DissolveFormation(int formationId);
    void ContributeQi(int formationId, int contributorId, long amount);
    void ProcessDrain();
}

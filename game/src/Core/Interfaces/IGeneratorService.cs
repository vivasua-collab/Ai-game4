#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Interfaces;

/// <summary>
/// Procedural generation service ("Matryoshka" pattern:
/// Base × Material × Grade × Specialization). Deterministic via seed.
/// </summary>
public interface IGeneratorService
{
    InventoryItem GenerateItem(string baseId, int grade, string? specialization = null);
    TechniqueData GenerateTechnique(string baseId, int grade, string? specialization = null);
}

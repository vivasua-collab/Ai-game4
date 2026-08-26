#nullable enable
namespace CultivationGame.Core.Data;

/// <summary>
/// Биом (страта 0) — определяет только цвет фона и базовую Ци.
/// Не влияет на проходимость.
/// </summary>
public enum BiomeType
{
    Ocean,
    Sea,
    Coast,
    Grassland,
    Steppe,
    Forest,
    Highlands,
    Mountains,
    Peak,
    // Legacy aliases (Ai-game3 compatibility)
    Plains = Grassland,
    Desert = Steppe,
    Swamp = Forest,
    Tundra = Highlands,
    Jungle = Forest,
    Volcanic = Mountains,
    Spiritual = Peak
}

#nullable enable
namespace CultivationGame.Modules.Combat;

public sealed class CombatConfig
{
    public float BaseDamage { get; set; } = 10f;
    public float LevelSuppressionPerLevel { get; set; } = 0.1f;
}

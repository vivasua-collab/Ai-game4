#nullable enable
namespace CultivationGame.Modules.Body;

public sealed class BodyConfig
{
    public float DefaultPartMaxHealth { get; set; } = 100f;
    public float RegenPerTick { get; set; } = 0.5f;
    public float BleedDamagePerTick { get; set; } = 1f;
}

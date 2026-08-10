#nullable enable
namespace CultivationGame.Modules.Charger;

public sealed class ChargerConfig
{
    public int DefaultMaxSlots { get; set; } = 4;
    public float HeatPerStonePerTick { get; set; } = 0.1f;
    public float HeatDissipationPerTick { get; set; } = 0.05f;
    public float OverheatThreshold { get; set; } = 10f;
    public float CoolDownThreshold { get; set; } = 5f;
}

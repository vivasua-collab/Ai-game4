#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Player;

public sealed class PlayerConfig
{
    public Direction StartFacing { get; set; } = Direction.South;
    public float MoveSpeedTilesPerTick { get; set; } = 1f;
    public float BaseMaxHealth { get; set; } = 100f;
    public long BaseMaxQi { get; set; } = 100L;
}

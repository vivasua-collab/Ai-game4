#nullable enable
namespace CultivationGame.Modules.Inventory;

public sealed class InventoryConfig
{
    public float MaxWeight { get; set; } = 100f;
    public float MaxVolume { get; set; } = 200f;
    public int MaxSlots { get; set; } = 40;
}

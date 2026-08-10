#nullable enable
namespace CultivationGame.Modules.NPC;

public sealed class NPCConfig
{
    public int SpinalTickEvery { get; set; } = 1;
    public int NeuralTickEvery { get; set; } = 3;
    public int BrainTickEvery { get; set; } = 10;
    public int MaxTempNPCs { get; set; } = 100;
}

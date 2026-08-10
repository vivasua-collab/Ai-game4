#nullable enable
namespace CultivationGame.Modules.Qi;

public sealed class QiConfig
{
    public float BaseConductivity { get; set; } = 1f;
    public float RegenFractionPerBatch { get; set; } = 0.05f;
}

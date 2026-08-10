#nullable enable
namespace CultivationGame.Modules.Formation;

public sealed class FormationConfig
{
    public long DefaultMaxCapacity { get; set; } = 1000L;
    public long DrainPerTickPerParticipant { get; set; } = 1L;
}

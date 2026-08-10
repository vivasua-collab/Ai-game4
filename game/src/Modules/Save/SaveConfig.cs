#nullable enable
namespace CultivationGame.Modules.Save;

public sealed class SaveConfig
{
    public string SaveDirectory { get; set; } = "user://saves";
    public int MaxSaves { get; set; } = 20;
    public bool AutosaveEnabled { get; set; } = true;
    public int AutosaveEveryTicks { get; set; } = 60;
}

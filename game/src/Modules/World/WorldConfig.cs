#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.World;

/// <summary>Configuration for the World/Time module.</summary>
public sealed class WorldConfig
{
    public TimeSpeed DefaultSpeed { get; set; } = TimeSpeed.Normal;
    public bool AutosaveEnabled { get; set; } = true;
    public int AutosaveEveryTicks { get; set; } = GameConstants.AUTOSAVE_INTERVAL_TICKS;
}

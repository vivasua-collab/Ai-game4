#nullable enable
namespace CultivationGame.Modules.Quest;

public sealed class QuestConfig
{
    public int MaxActiveQuests { get; set; } = 50;
    public int ProgressCheckEveryTicks { get; set; } = 1;
}

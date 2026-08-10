#nullable enable
using System.Collections.Generic;

namespace CultivationGame.Core.Interfaces;

/// <summary>Quest tracking: start / progress / complete.</summary>
public interface IQuestService
{
    IReadOnlyList<string> GetActiveQuests();
    void StartQuest(string questId);
    void CompleteQuest(string questId);
    void UpdateProgress(string questId, int progress);
}

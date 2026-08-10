#nullable enable
using System;
using System.Collections.Generic;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Quest;

/// <summary>
/// Internal quest record — V1 stub. Real impl uses a QuestData data model.
/// </summary>
internal sealed class QuestRecord
{
    public string Id { get; set; } = "";
    public bool IsActive { get; set; }
    public bool IsCompleted { get; set; }
    public int Progress { get; set; }
}

/// <summary>
/// QuestService — active quest list. V1 stub: Dictionary&lt;string, QuestRecord&gt;.
/// </summary>
public sealed class QuestService : IQuestService
{
    private readonly Dictionary<string, QuestRecord> _quests = new();
    private readonly QuestConfig _config;

    public QuestService(QuestConfig? config = null) => _config = config ?? new QuestConfig();

    public IReadOnlyList<string> GetActiveQuests()
    {
        var list = new List<string>();
        foreach (var kv in _quests)
        {
            if (kv.Value.IsActive && !kv.Value.IsCompleted) list.Add(kv.Key);
        }
        return list;
    }

    public void StartQuest(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return;
        if (!_quests.TryGetValue(questId, out var q))
        {
            q = new QuestRecord { Id = questId };
            _quests[questId] = q;
        }
        q.IsActive = true;
        q.IsCompleted = false;
        Console.WriteLine($"[QuestService] Started quest '{questId}'");
    }

    public void CompleteQuest(string questId)
    {
        if (!_quests.TryGetValue(questId, out var q)) return;
        q.IsCompleted = true;
        q.IsActive = false;
        Console.WriteLine($"[QuestService] Completed quest '{questId}'");
    }

    public void UpdateProgress(string questId, int progress)
    {
        if (!_quests.TryGetValue(questId, out var q)) return;
        q.Progress = progress;
        Console.WriteLine($"[QuestService] Quest '{questId}' progress → {progress}");
    }
}

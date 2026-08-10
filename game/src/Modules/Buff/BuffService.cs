#nullable enable
using System;
using System.Collections.Generic;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Buff;

/// <summary>
/// BuffInstance — internal representation of an active buff on an entity.
/// </summary>
public sealed class BuffInstance
{
    public string BuffId { get; set; } = "";
    public float RemainingDuration { get; set; }
    public int Stacks { get; set; } = 1;
    public float Magnitude { get; set; } = 1f;
}

/// <summary>
/// BuffService — per-entity buff list. V1 stub: stores BuffInstance in a
/// Dictionary&lt;int, List&lt;BuffInstance&gt;&gt;.
/// </summary>
public sealed class BuffService : IBuffService
{
    private readonly Dictionary<int, List<BuffInstance>> _buffs = new();
    private readonly BuffConfig _config;

    public BuffService(BuffConfig? config = null) => _config = config ?? new BuffConfig();

    public void ApplyBuff(int entityId, string buffId, float duration)
    {
        if (string.IsNullOrEmpty(buffId)) return;
        if (!_buffs.TryGetValue(entityId, out var list))
        {
            list = new List<BuffInstance>();
            _buffs[entityId] = list;
        }

        // Find existing instance of same buffId to refresh
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].BuffId == buffId)
            {
                list[i].RemainingDuration = duration;
                list[i].Stacks++;
                return;
            }
        }

        list.Add(new BuffInstance
        {
            BuffId = buffId,
            RemainingDuration = duration > 0 ? duration : _config.DefaultDuration,
            Stacks = 1,
            Magnitude = 1f
        });
        Console.WriteLine($"[BuffService] {entityId} +buff '{buffId}' dur={duration:F1}s");
    }

    public void RemoveBuff(int entityId, string buffId)
    {
        if (!_buffs.TryGetValue(entityId, out var list)) return;
        list.RemoveAll(b => b.BuffId == buffId);
    }

    public void TickBuffs(int entityId)
    {
        if (!_buffs.TryGetValue(entityId, out var list)) return;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            list[i].RemainingDuration -= 1f;
            if (list[i].RemainingDuration <= 0f)
            {
                list.RemoveAt(i);
            }
        }
    }

    public IReadOnlyList<string> GetActiveBuffs(int entityId)
    {
        if (!_buffs.TryGetValue(entityId, out var list)) return Array.Empty<string>();
        var ids = new string[list.Count];
        for (int i = 0; i < list.Count; i++) ids[i] = list[i].BuffId;
        return ids;
    }

    /// <summary>Internal — tick all entities' buffs. Not on interface.</summary>
    public void TickAllBuffs()
    {
        foreach (var entityId in _buffs.Keys)
        {
            TickBuffs(entityId);
        }
    }

    /// <summary>Internal — entity IDs with active buffs. Not on interface.</summary>
    public IReadOnlyCollection<int> GetEntitiesWithBuffs() => _buffs.Keys;
}

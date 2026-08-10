#nullable enable
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Interaction;

/// <summary>
/// Internal interactable record — V1 stub.
/// </summary>
internal sealed class InteractableRecord
{
    public int Id { get; set; }
    public Position2D Position { get; set; }
    public float InteractionRange { get; set; } = 1.5f;
    public string DisplayName { get; set; } = "";
    public string InteractionType { get; set; } = "";
}

/// <summary>
/// InteractionService — registry of interactables.
/// V1 stub: Dictionary&lt;int, InteractableRecord&gt;.
/// </summary>
public sealed class InteractionService : IInteractionService
{
    private int _nextId = 1;
    private readonly Dictionary<int, InteractableRecord> _targets = new();
    private readonly InteractionConfig _config;

    public InteractionService(InteractionConfig? config = null) => _config = config ?? new InteractionConfig();

    /// <summary>Register an interactable. Not on interface.</summary>
    public int RegisterInteractable(Position2D position, string displayName = "", string interactionType = "")
    {
        int id = _nextId++;
        _targets[id] = new InteractableRecord
        {
            Id = id,
            Position = position,
            DisplayName = displayName,
            InteractionType = interactionType,
            InteractionRange = _config.DefaultInteractionRange
        };
        Console.WriteLine($"[InteractionService] Registered interactable {id} ('{displayName}') @ {position}");
        return id;
    }

    /// <summary>Unregister an interactable. Not on interface.</summary>
    public void UnregisterInteractable(int targetId) => _targets.Remove(targetId);

    public IReadOnlyList<int> GetInteractablesInRange(Position2D position, float range)
    {
        var list = new List<int>();
        foreach (var kv in _targets)
        {
            if (kv.Value.Position.DistanceTo(position) <= range)
                list.Add(kv.Key);
        }
        return list;
    }

    public void Interact(int playerId, int targetId)
    {
        if (!_targets.TryGetValue(targetId, out var t))
        {
            Console.WriteLine($"[InteractionService] Interact({targetId}) — NOT FOUND");
            return;
        }
        Console.WriteLine($"[InteractionService] Player {playerId} interacts with {targetId} ('{t.DisplayName}', type={t.InteractionType})");
    }
}

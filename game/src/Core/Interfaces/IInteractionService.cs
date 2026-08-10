#nullable enable
using System.Collections.Generic;
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Interfaces;

/// <summary>Player-initiated interactions with world entities.</summary>
public interface IInteractionService
{
    void Interact(int playerId, int targetId);
    IReadOnlyList<int> GetInteractablesInRange(Position2D position, float range);
}

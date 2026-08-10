#nullable enable
using System.Collections.Generic;
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Interfaces;

/// <summary>Row-model inventory: weight + volume caps, no grid.</summary>
public interface IInventoryService
{
    IReadOnlyList<InventorySlot> GetSlots();

    bool AddItem(string itemId, int count);
    bool RemoveItem(string itemId, int count);
    int GetItemCount(string itemId);

    float GetCurrentWeight();
    float GetCurrentVolume();
    float GetMaxWeight();
    float GetMaxVolume();
}

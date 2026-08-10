#nullable enable
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Inventory;

/// <summary>
/// InventoryService — row-model inventory with weight/volume caps.
/// V1 stub: keeps slots in a List&lt;InventorySlot&gt; (string ItemId).
/// </summary>
public sealed class InventoryService : IInventoryService
{
    private readonly List<InventorySlot> _slots = new();
    private readonly InventoryConfig _config;

    // V1 hardcoded item weight/volume table (stub). Keyed by string itemId.
    private static readonly Dictionary<string, (float weight, float volume)> ItemStats = new()
    {
        { "bread", (0.5f, 0.2f) },
        { "sword", (1.0f, 0.5f) },
        { "armor", (2.0f, 1.0f) },
        { "herb", (0.1f, 0.05f) },
        { "qi_stone", (0.3f, 0.1f) },
    };

    public InventoryService(InventoryConfig? config = null) => _config = config ?? new InventoryConfig();

    public bool AddItem(string itemId, int count)
    {
        if (string.IsNullOrEmpty(itemId) || count <= 0) return false;

        // Try to stack
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].ItemId == itemId && _slots[i].Count < 99)
            {
                int add = Math.Min(99 - _slots[i].Count, count);
                _slots[i] = new InventorySlot(itemId, _slots[i].Count + add, _slots[i].Weight, _slots[i].Volume);
                count -= add;
                if (count <= 0) return true;
            }
        }
        // New slot
        var stats = GetItemStats(itemId);
        while (count > 0)
        {
            if (_slots.Count >= _config.MaxSlots) return false;
            int add = Math.Min(99, count);
            _slots.Add(new InventorySlot(itemId, add, stats.weight, stats.volume));
            count -= add;
        }
        Console.WriteLine($"[InventoryService] Added item '{itemId}' (now {GetItemCount(itemId)} total)");
        return true;
    }

    public bool RemoveItem(string itemId, int count)
    {
        if (GetItemCount(itemId) < count) return false;
        for (int i = _slots.Count - 1; i >= 0 && count > 0; i--)
        {
            if (_slots[i].ItemId != itemId) continue;
            int take = Math.Min(_slots[i].Count, count);
            int remaining = _slots[i].Count - take;
            if (remaining <= 0) _slots.RemoveAt(i);
            else _slots[i] = new InventorySlot(itemId, remaining, _slots[i].Weight, _slots[i].Volume);
            count -= take;
        }
        return true;
    }

    public int GetItemCount(string itemId)
    {
        int total = 0;
        foreach (var s in _slots) if (s.ItemId == itemId) total += s.Count;
        return total;
    }

    public IReadOnlyList<InventorySlot> GetSlots() => _slots;

    public float GetCurrentWeight()
    {
        float w = 0f;
        foreach (var s in _slots)
            w += s.Weight * s.Count;
        return w;
    }

    public float GetCurrentVolume()
    {
        float v = 0f;
        foreach (var s in _slots)
            v += s.Volume * s.Count;
        return v;
    }

    public float GetMaxWeight() => _config.MaxWeight;
    public float GetMaxVolume() => _config.MaxVolume;

    private static (float weight, float volume) GetItemStats(string itemId)
    {
        return ItemStats.TryGetValue(itemId, out var s) ? s : (0.1f, 0.1f);
    }
}

/// <summary>
/// EquipmentService — per-entity equipment slots.
/// V1 stub: Dictionary&lt;entityId, Dictionary&lt;EquipmentSlot, string itemId&gt;&gt;.
/// </summary>
public sealed class EquipmentService : IEquipmentService
{
    private readonly Dictionary<int, Dictionary<EquipmentSlot, string>> _equipped = new();

    public bool Equip(int entityId, EquipmentSlot slot, string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return false;
        if (!_equipped.TryGetValue(entityId, out var dict))
        {
            dict = new Dictionary<EquipmentSlot, string>();
            _equipped[entityId] = dict;
        }
        dict[slot] = itemId;
        Console.WriteLine($"[EquipmentService] Entity {entityId} equipped {slot} ← '{itemId}'");
        return true;
    }

    public bool Unequip(int entityId, EquipmentSlot slot)
    {
        if (!_equipped.TryGetValue(entityId, out var dict)) return false;
        return dict.Remove(slot);
    }

    public string? GetEquippedItem(int entityId, EquipmentSlot slot)
    {
        if (_equipped.TryGetValue(entityId, out var dict) && dict.TryGetValue(slot, out var id))
            return id;
        return null;
    }

    public IReadOnlyDictionary<EquipmentSlot, string> GetAllEquipped(int entityId)
    {
        return _equipped.TryGetValue(entityId, out var dict)
            ? dict
            : new Dictionary<EquipmentSlot, string>();
    }
}

/// <summary>
/// CraftingService — V1 stub. Knows a fixed set of recipe IDs.
/// </summary>
public sealed class CraftingService
{
    private static readonly string[] Recipes = { "recipe_101", "recipe_102", "recipe_103" };

    public bool CanCraft(string recipeId) => Array.IndexOf(Recipes, recipeId) >= 0;

    public bool Craft(string recipeId, out string producedItemId)
    {
        producedItemId = "";
        if (!CanCraft(recipeId)) return false;
        producedItemId = "item_" + recipeId;
        Console.WriteLine($"[CraftingService] Crafted recipe '{recipeId}' → '{producedItemId}'");
        return true;
    }

    public IReadOnlyCollection<string> GetAvailableRecipes() => Recipes;
}

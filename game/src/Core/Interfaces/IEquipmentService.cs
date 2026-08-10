#nullable enable
using System.Collections.Generic;
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Interfaces;

/// <summary>Equipment slot management across the 15-slot body doll.</summary>
public interface IEquipmentService
{
    bool Equip(int entityId, EquipmentSlot slot, string itemId);
    bool Unequip(int entityId, EquipmentSlot slot);
    string? GetEquippedItem(int entityId, EquipmentSlot slot);
    IReadOnlyDictionary<EquipmentSlot, string> GetAllEquipped(int entityId);
}

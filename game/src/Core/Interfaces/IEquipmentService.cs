#nullable enable
// Создано: 2026-05-08 10:07:00 UTC
using CultivationGame.Core.Data;
using CultivationGame.Core;
namespace CultivationGame.Core.Interfaces
{
    public interface IEquipmentService
    {
        string EntityId { get; }
        EquipmentData GetEquipped(EquipmentSlot slot);
        bool TryEquip(EquipmentSlot slot, EquipmentData item);
        bool TryUnequip(EquipmentSlot slot, out EquipmentData item);
        bool IsSlotBlocked(EquipmentSlot slot);
        float GetTotalArmor();
        float GetTotalDamage();
        float GetTotalWeight();
        float GetTotalMoveSpeedPenalty();
        WeaponHandType GetWeaponHandType();
        bool IsTwoHandEquipped { get; }
    }
}

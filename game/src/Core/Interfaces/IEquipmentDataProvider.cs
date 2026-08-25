#nullable enable
// Создано: 2026-05-20 18:43:21 UTC
// Редактировано: 2026-05-22 09:51:00 UTC — Спринт 6 C5: +GetArmorCoverage() для coverage roll
// Провайдер данных экипировки per-entity.
// Позволяет получать экипировку по entityId.
// NPCAssemblyService при создании NPC → SetEquipment(npcId, state.EquipmentIds)
// DamageService получает armor через IEquipmentDataProvider.
using System.Collections.Generic;
using CultivationGame.Core.Data;

using CultivationGame.Core;
namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Провайдер данных экипировки per-entity.
    /// Позволяет получать экипировку по entityId.
    /// NPCAssemblyService при создании NPC → SetEquipment(npcId, state.EquipmentIds)
    /// DamageService получает armor через IEquipmentDataProvider.
    /// Волна 4: добавлены SetTotalArmor/SetTotalDamage/SetEquippedItemId в интерфейс.
    /// </summary>
    public interface IEquipmentDataProvider
    {
        /// <summary>Получить экипированный предмет в слоте</summary>
        EquipmentData GetEquipped(string entityId, EquipmentSlot slot);

        /// <summary>Получить суммарную броню сущности</summary>
        float GetTotalArmor(string entityId);

        /// <summary>Получить суммарный урон сущности</summary>
        float GetTotalDamage(string entityId);

        /// <summary>Установить экипировку для сущности (при создании NPC)</summary>
        void SetEquipment(string entityId, Dictionary<EquipmentSlot, string> equipmentIds);

        /// <summary>Установить предрассчитанную суммарную броню</summary>
        void SetTotalArmor(string entityId, float armor);

        /// <summary>Установить предрассчитанный суммарный урон</summary>
        void SetTotalDamage(string entityId, float damage);

        /// <summary>Получить ID экипированного предмета в слоте (строковый ID)</summary>
        string GetEquippedItemId(string entityId, EquipmentSlot slot);

        /// <summary>Проверить существование сущности</summary>
        bool HasEntity(string entityId);

        /// <summary>Удалить сущность (при деспавне)</summary>
        void RemoveEntity(string entityId);

        /// <summary>Инвалидировать кэш брони для сущности</summary>
        void InvalidateCache(string entityId);

        /// <summary>
        /// Получить средний процент покрытия брони для сущности.
        /// Возвращает 0-100 (процент покрытия).
        /// Спринт 6 C5: Используется для coverage roll в DamageService.
        /// </summary>
        int GetArmorCoverage(string entityId);

        /// <summary>
        /// Установить покрытие брони для сущности (0-100%).
        /// Вызывается из NPCAssemblyService после расчёта параметров NPC.
        /// Спринт 6 C5: для coverage roll в DamageService.
        /// </summary>
        void SetArmorCoverage(string entityId, int coverage);

        // === NPC_COMBAT_PREP Phase 8: wiring боевых статов экипировки ===
        // Все агрегаты в промилле (ЗАПРЕТ 3.9): 1% = 10 промилле.
        // Источники: EquipmentData.DodgeBonus (%) + StatBonuses §7.1 (EQUIPMENT_SYSTEM.md).
        // Резолв ID → EquipmentData идёт через IItemDatabaseService (кэш данных
        // для игрока имеет приоритет — EquipmentService пушит полные объекты).

        /// <summary>
        /// Суммарный модификатор уклонения от экипировки (промилле, может быть
        /// отрицательным — штраф тяжёлой брони). COMBAT_SYSTEM.md §7.1:
        /// dodgeChance = 5% + (AGI-10)×0.5% - armorDodgePenalty.
        /// </summary>
        int GetDodgeBonusPermil(string entityId);

        /// <summary>
        /// Плоский бонус блока от экипировки (промилле). COMBAT_SYSTEM.md §7.3:
        /// blockChance = shieldBlock + (STR-10)×0.2%. Источник — StatBonus
        /// "blockChance" (EQUIPMENT_SYSTEM.md §7.1 Defense).
        /// </summary>
        int GetBlockBonusPermil(string entityId);

        /// <summary>
        /// Плоский бонус парирования от экипировки (промилле). COMBAT_SYSTEM.md §7.2:
        /// parryChance = weaponParryBonus + (AGI-10)×0.3%. Источник — StatBonus
        /// "parryChance" (дата-driven: 0, пока контент не выдаёт такие бонусы).
        /// </summary>
        int GetParryBonusPermil(string entityId);

        /// <summary>
        /// Плоский бонус крит-шанса от экипировки атакующего (промилле).
        /// COMBAT_SYSTEM.md §9.1: critChance = базовый + luck×0.01 + techniqueBonus.
        /// Источник — StatBonus "critChance" (EQUIPMENT_SYSTEM.md §7.1 Combat).
        /// </summary>
        int GetCritBonusPermil(string entityId);

        /// <summary>
        /// Пробитие оружия основной руки (ед. брони). COMBAT_SYSTEM.md §11.5:
        /// penetration = weapon.penetration + attackerSTR×0.5 + techniquePenetration.
        /// </summary>
        int GetWeaponPenetration(string entityId);

        /// <summary>
        /// Установить экипировку сущности напрямую (полные EquipmentData).
        /// Путь игрока: EquipmentService пушит свой словарь после каждого
        /// equip/unequip — резолв через базу предметов не нужен.
        /// </summary>
        void SetEquipmentData(string entityId, Dictionary<EquipmentSlot, EquipmentData> equipment);
    }
}

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
    }
}

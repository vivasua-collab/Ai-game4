#nullable enable
// Создано: 2026-05-09 00:00:00 UTC
// Редактировано: 2026-05-09 — INV-04: IBodyService заменён на HashSet<EquipmentSlot> (событийная модель).
// Валидатор экипировки — проверка слотов, требований, состояния тела.
// Разделение God Object EquipmentController (1418 LOC) → EquipmentService + EquipmentValidator + EquipmentStatAggregator.
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Inventory
{
    /// <summary>
    /// Валидатор экипировки.
    /// Проверяет: совпадение слота, двуручное оружие, требования, заблокированные слоты.
    /// КРИТИЧЕСКАЯ СВЯЗЬ: Body→Equipment — ампутация блокирует слот.
    ///
    /// INV-04: НЕ требует IBodyService напрямую.
    /// Вместо этого принимает HashSet заблокированных слотов (кэшированных из BodyPartSeveredEvent).
    /// </summary>
    public static class EquipmentValidator
    {
        /// <summary>
        /// Проверить, можно ли экипировать предмет в указанный слот.
        /// Возвращает true, если экипировка возможна.
        /// </summary>
        /// <param name="item">Предмет экипировки</param>
        /// <param name="targetSlot">Целевой слот</param>
        /// <param name="blockedSlots">Заблокированные слоты (кэш из BodyPartSeveredEvent)</param>
        /// <param name="currentEquipment">Текущая экипировка (слот → предмет)</param>
        /// <param name="reason">Причина отказа (если false)</param>
        public static bool ValidateEquip(
            EquipmentData item,
            EquipmentSlot targetSlot,
            HashSet<EquipmentSlot> blockedSlots,
            Dictionary<EquipmentSlot, EquipmentData> currentEquipment,
            out string reason)
        {
            reason = null;

            // 1. Проверка: слот предмета совпадает с целевым
            if (item.Slot != targetSlot)
            {
                reason = $"Предмет '{item.NameRu}' предназначен для слота {item.Slot}, а не {targetSlot}";
                return false;
            }

            // 2. КРИТИЧЕСКАЯ СВЯЗЬ: Body→Equipment — ампутация блокирует слот
            if (blockedSlots != null && blockedSlots.Contains(targetSlot))
            {
                reason = $"Слот {targetSlot} заблокирован: ампутированная часть тела";
                return false;
            }

            // 3. Проверка двуручного оружия
            if (item.HandType == WeaponHandType.TwoHand)
            {
                // Двуручное занимает WeaponMain — проверяем, не заблокирован ли WeaponOff
                if (blockedSlots != null && blockedSlots.Contains(EquipmentSlot.WeaponOff))
                {
                    reason = "Двуручное оружие требует обе руки, но вторичная рука заблокирована";
                    return false;
                }
            }

            // 4. Проверка: если экипируем в WeaponOff, а WeaponMain — двуручное
            if (targetSlot == EquipmentSlot.WeaponOff)
            {
                if (currentEquipment.TryGetValue(EquipmentSlot.WeaponMain, out var mainWeapon)
                    && mainWeapon != null && mainWeapon.HandType == WeaponHandType.TwoHand)
                {
                    reason = "Нельзя экипировать во вторичную руку: основная рука занята двуручным оружием";
                    return false;
                }
            }

            // 5. Проверка требований к уровню культивации
            if (item.RequiredCultivationLevel > 0)
            {
                // В будущих фазах: проверка через IQiService.CultivationLevel
                // Пока — заглушка (всегда проходит)
            }

            // 6. Проверка требований к характеристикам
            if (item.StatRequirements != null && item.StatRequirements.Count > 0)
            {
                // В будущих фазах: проверка через IStatService
                // Пока — заглушка (всегда проходит)
            }

            return true;
        }

        /// <summary>
        /// Определить, нужно ли освободить WeaponOff при экипировке двуручного оружия.
        /// </summary>
        public static bool ShouldUnequipOffHand(
            EquipmentData item,
            EquipmentSlot targetSlot)
        {
            return item.HandType == WeaponHandType.TwoHand && targetSlot == EquipmentSlot.WeaponMain;
        }
    }
}

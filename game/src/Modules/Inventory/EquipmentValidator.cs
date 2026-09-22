#nullable enable
// Создано: 2026-05-09 00:00:00 UTC
// Редактировано: 2026-05-09 — INV-04: IBodyService заменён на HashSet<EquipmentSlot> (событийная модель).
// Валидатор экипировки — проверка слотов, требований, состояния тела.
// Разделение God Object EquipmentController (1418 LOC) → EquipmentService + EquipmentValidator + EquipmentStatAggregator.
using System;
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
        /// <param name="playerCultivationLevel">R35 (Фаза 12 / P1-17): уровень культивации игрока;
        ///     -1 — не проверять (legacy-вызовы). NPC экипируются мимо валидатора (ID-словарь NPCAssemblyService) — гейт только игрока.</param>
        /// <param name="statService">R35 (Фаза 12 / P1-17): статы игрока для StatRequirements; null — не проверять.</param>
        public static bool ValidateEquip(
            EquipmentData item,
            EquipmentSlot targetSlot,
            HashSet<EquipmentSlot> blockedSlots,
            Dictionary<EquipmentSlot, EquipmentData> currentEquipment,
            out string reason,
            int playerCultivationLevel = -1,
            IStatService? statService = null)
        {
            reason = null;

            // 1. Проверка: слот предмета совпадает с целевым.
            // R35 (Фаза 12 / P1-18): одноручное оружие — ГИБКИЙ слот: генератор
            // даёт Slot=WeaponMain, но архитектура (и CharacterDollPanel)
            // разрешают его и во вторую руку. Прежде жёсткое равенство
            // делало WeaponOff недостижимым через штатный UI (UI разрешал,
            // backend отвергал).
            bool weaponFlexible = item.Category == ItemCategory.Weapon
                && item.HandType == WeaponHandType.OneHand
                && (targetSlot == EquipmentSlot.WeaponMain || targetSlot == EquipmentSlot.WeaponOff);
            if (item.Slot != targetSlot && !weaponFlexible)
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
            // R35 (Фаза 12 / P1-17) ФИКС: реальный гейт. Прежде — заглушка
            // «всегда проходит»: генератор записывал RequiredCultivationLevel=L
            // (L9-предмет требовал 9-ю ступень), а валидатор это ИГНОРИРОВАЛ —
            // L1-практик экипил L9-гримуар. NPC экипируются мимо валидатора
            // (ID-словарь NPCAssemblyService) — гейт действует только на игрока.
            // playerCultivationLevel < 0 — легаси-вызов без данных (не проверяем).
            if (item.RequiredCultivationLevel > 0 && playerCultivationLevel >= 0
                && playerCultivationLevel < item.RequiredCultivationLevel)
            {
                reason = $"Требуется уровень культивации {item.RequiredCultivationLevel} (у игрока {playerCultivationLevel})";
                return false;
            }

            // 6. Проверка требований к характеристикам
            // R35 (Фаза 12 / P1-17) ФИКС: реальный гейт по IStatService.
            // Неразрешимое имя стата — пропуск с логом (не блокируем контент
            // с опечаткой в требованиях); разрешимое — честное сравнение.
            if (statService != null && item.StatRequirements != null)
            {
                foreach (var req in item.StatRequirements)
                {
                    if (req == null || string.IsNullOrEmpty(req.StatName)) continue;
                    if (!System.Enum.TryParse(req.StatName, ignoreCase: true, out StatType statType))
                    {
                        Console.WriteLine($"[EquipmentValidator] StatRequirements: неизвестный стат '{req.StatName}' — проверка пропущена");
                        continue;
                    }
                    float actual = statService.GetStat(statType);
                    if (actual < req.MinValue)
                    {
                        reason = $"Требуется {req.StatName} ≥ {req.MinValue} (у игрока {actual:0})";
                        return false;
                    }
                }
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

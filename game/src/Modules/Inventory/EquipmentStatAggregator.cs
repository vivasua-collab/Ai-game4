#nullable enable
// Создано: 2026-05-09 00:00:00 UTC
// Редактировано: INV-A15/A16 FIX: разделение flat и percentage бонусов
// Агрегатор статов экипировки — подсчёт бонусов, брони, урона.
// Разделение God Object EquipmentController (1418 LOC) → EquipmentService + EquipmentValidator + EquipmentStatAggregator.
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Inventory
{
    /// <summary>
    /// Агрегатор статов экипировки.
    /// Подсчитывает суммарные бонусы со всех надетых предметов.
    /// Учитывает состояние прочности. R38-4b: грейд-эффективность НЕ
    /// применяется — генератор уже вложил её в стат предмета (§2).
    ///
    /// INV-A15/A16 FIX: Разделение flat и percentage бонусов.
    /// Формула: (base + flatSum) × (1 + percentSum)
    /// </summary>
    public static class EquipmentStatAggregator
    {
        /// <summary>
        /// Подсчитать суммарную броню со всей экипировки.
        /// Броня = Σ(equipment.Defense × durabilityEfficiency × coverage)
        /// R38-4b: БЕЗ грейд-множителя — генератор УЖЕ вкладывает эффективность
        /// грейда в стат предмета (§2 «Eff = Base × Grade × (1+Material)»);
        /// умножение здесь считало грейд ДВАЖДЫ (легендарка = ×4, ×2×2).
        /// </summary>
        public static float GetTotalArmor(Dictionary<EquipmentSlot, EquipmentData> equipment)
        {
            float total = 0f;
            foreach (var kvp in equipment)
            {
                var item = kvp.Value;
                if (item == null || item.Defense <= 0) continue;

                float durabilityMult = GetDurabilityEfficiency(item);
                total += item.Defense * durabilityMult * (item.Coverage / 100f);
            }
            return total;
        }

        /// <summary>
        /// Подсчитать суммарный урон со всей экипировки.
        /// Урон = Σ(equipment.Damage × durabilityEfficiency)
        /// R38-4b: БЕЗ грейд-множителя — см. GetTotalArmor (двойной счёт).
        /// </summary>
        public static float GetTotalDamage(Dictionary<EquipmentSlot, EquipmentData> equipment)
        {
            float total = 0f;
            foreach (var kvp in equipment)
            {
                var item = kvp.Value;
                if (item == null || item.Damage <= 0) continue;

                float durabilityMult = GetDurabilityEfficiency(item);
                total += item.Damage * durabilityMult;
            }
            return total;
        }

        /// <summary>
        /// Подсчитать суммарные бонусы к характеристикам.
        /// INV-A15/A16 FIX: Разделение flat и percentage бонусов.
        /// Формула: (base + flatSum) × (1 + percentSum)
        /// </summary>
        public static Dictionary<string, float> GetStatBonuses(Dictionary<EquipmentSlot, EquipmentData> equipment)
        {
            // Раздельные словари для flat и percentage бонусов
            var flatBonuses = new Dictionary<string, float>();
            var percentBonuses = new Dictionary<string, float>();

            foreach (var kvp in equipment)
            {
                var item = kvp.Value;
                if (item == null || item.StatBonuses == null) continue;

                // R38-4b: без gradeMult — генератор уже вложил §8.4 в значение.
                float durabilityMult = GetDurabilityEfficiency(item);

                foreach (var bonus in item.StatBonuses)
                {
                    // INV-A16 FIX: Разделяем absolute и percentage бонусы
                    float value = bonus.Value * durabilityMult;

                    if (bonus.IsPercentage)
                    {
                        // Percentage бонус: множитель (например +20% = +0.2)
                        if (percentBonuses.ContainsKey(bonus.StatName))
                            percentBonuses[bonus.StatName] += value;
                        else
                            percentBonuses[bonus.StatName] = value;
                    }
                    else
                    {
                        // Flat бонус: абсолютное значение (например +10)
                        if (flatBonuses.ContainsKey(bonus.StatName))
                            flatBonuses[bonus.StatName] += value;
                        else
                            flatBonuses[bonus.StatName] = value;
                    }
                }
            }

            // Объединяем: итоговый бонус = flat + (flat × percent)
            // Вызывающий использует: finalStat = (baseStat + flatBonus) × (1 + percentBonus)
            var result = new Dictionary<string, float>();

            // Flat бонусы
            foreach (var kvp in flatBonuses)
                result[kvp.Key] = kvp.Value;

            // Percentage бонусы (ключ суффикс "_pct" для различения)
            foreach (var kvp in percentBonuses)
                result[kvp.Key + "_pct"] = kvp.Value;

            return result;
        }

        /// <summary>
        /// Подсчитать суммарный flat бонус к указанной характеристике.
        /// </summary>
        public static float GetFlatBonus(Dictionary<EquipmentSlot, EquipmentData> equipment, string statName)
        {
            float total = 0f;
            foreach (var kvp in equipment)
            {
                var item = kvp.Value;
                if (item == null || item.StatBonuses == null) continue;

                float durabilityMult = GetDurabilityEfficiency(item); // R38-4b: без gradeMult

                foreach (var bonus in item.StatBonuses)
                {
                    if (bonus.StatName == statName && !bonus.IsPercentage)
                        total += bonus.Value * durabilityMult;
                }
            }
            return total;
        }

        /// <summary>
        /// Подсчитать суммарный percentage бонус к указанной характеристике.
        /// </summary>
        public static float GetPercentBonus(Dictionary<EquipmentSlot, EquipmentData> equipment, string statName)
        {
            float total = 0f;
            foreach (var kvp in equipment)
            {
                var item = kvp.Value;
                if (item == null || item.StatBonuses == null) continue;

                float durabilityMult = GetDurabilityEfficiency(item); // R38-4b: без gradeMult

                foreach (var bonus in item.StatBonuses)
                {
                    if (bonus.StatName == statName && bonus.IsPercentage)
                        total += bonus.Value * durabilityMult;
                }
            }
            return total;
        }

        /// <summary>
        /// Определить тип хвата оружия (одноручное/двуручное).
        /// </summary>
        public static WeaponHandType GetWeaponHandType(Dictionary<EquipmentSlot, EquipmentData> equipment)
        {
            if (equipment.TryGetValue(EquipmentSlot.WeaponMain, out var mainWeapon) && mainWeapon != null)
            {
                return mainWeapon.HandType;
            }
            return WeaponHandType.OneHand;
        }

        /// <summary>
        /// Проверить, экипировано ли двуручное оружие.
        /// </summary>
        public static bool IsTwoHandEquipped(Dictionary<EquipmentSlot, EquipmentData> equipment)
        {
            return GetWeaponHandType(equipment) == WeaponHandType.TwoHand;
        }

        // === Вспомогательные методы ===

        // R38-4b: GetGradeMultiplier удалён — грейд-эффективность уже вложена
        // генератором в стат предмета (§2/§8.4); двойное умножение давало
        // легендаркам ×4 вместо ×2 (баг-репорт 02.10 №4, «золотой сет»).

        /// <summary>
        /// Получить множитель эффективности от состояния прочности.
        /// У предметов без прочности — всегда 1.0.
        /// </summary>
        private static float GetDurabilityEfficiency(EquipmentData item)
        {
            if (!item.HasDurability) return 1.0f;

            // В будущих фазах: отслеживание текущей прочности через InstanceData
            // Пока — предполагаем Pristine (1.0)
            return 1.0f;
        }

        /// <summary>
        /// Подсчитать суммарный вес всей надетой экипировки (кг).
        /// Для глобального расчёта веса персонажа (инвентарь + экипировка).
        /// </summary>
        public static float GetTotalWeight(Dictionary<EquipmentSlot, EquipmentData> equipment)
        {
            float total = 0f;
            foreach (var kvp in equipment)
            {
                if (kvp.Value != null)
                    total += kvp.Value.Weight;
            }
            return total;
        }

        /// <summary>
        /// Подсчитать суммарный штраф скорости от экипировки (%).
        /// EquipmentData.MoveSpeedPenalty — отрицательное значение (например, -15 = -15%).
        /// Возвращает сумму (отрицательное число).
        /// </summary>
        public static float GetTotalMoveSpeedPenalty(Dictionary<EquipmentSlot, EquipmentData> equipment)
        {
            float total = 0f;
            foreach (var kvp in equipment)
            {
                if (kvp.Value != null)
                    total += kvp.Value.MoveSpeedPenalty;
            }
            return total;
        }
    }
}

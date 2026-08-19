#nullable enable
// Создано: 2026-08-19 — Task 2-b: Test items for inventory + character doll verification.
// Регистрирует тестовые предметы (экипировка + расходники) в IItemDatabaseService
// и наполняет ими инвентарь игрока для проверки drag&drop на куклу.
//
// Design per docs_v2/06_player/EQUIPMENT_SYSTEM.md and INVENTORY_SYSTEM.md.
// This is a DEBUG/TEST seeder — disabled in release by removing the Seed() call
// from GameBoot or gating it behind a DEBUG flag.
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Adapter.UI
{
    /// <summary>
    /// Тестовый сидер предметов. Создаёт набор экипировки и расходников
    /// для проверки работы инвентаря и куклы персонажа.
    ///
    /// Создаёт предметы по категориям:
    /// - Оружие (1H меч, 2H копьё, посох)
    /// - Броня (шлем, нагрудник, поножи, сапоги, пояс, перчатки)
    /// - Аксессуары (амулет, кольцо, плащ)
    /// - Расходники (пилюля лечения, пилюля Ци, свиток телепорта)
    ///
    /// Все предметы регистрируются в IItemDatabaseService, затем часть
    /// кладётся в инвентарь игрока (IInventoryService.TryAddItem).
    /// </summary>
    public static class TestItemSeeder
    {
        private static bool _seeded = false;

        /// <summary>
        /// Засидить базу предметов и инвентарь тестовыми данными.
        /// Idempotent — повторные вызовы игнорируются.
        /// </summary>
        public static void Seed(IItemDatabaseService database, IInventoryService inventory)
        {
            if (_seeded) return;
            _seeded = true;

            var items = CreateTestItems();
            database.RegisterRange(items);

            // Положить часть предметов в инвентарь (для проверки drag&drop на куклу).
            // Экипировка — по 1 экземпляру (не стакается), расходники — стаками.
            foreach (var item in items)
            {
                if (item is EquipmentData eq)
                {
                    inventory.TryAddItem(eq, 1);
                }
                else
                {
                    // Расходники — по 5 штук
                    inventory.TryAddItem(item, 5);
                }
            }
        }

        /// <summary>Создать полный набор тестовых предметов.</summary>
        private static List<ItemData> CreateTestItems()
        {
            var list = new List<ItemData>();

            // === Оружие ===
            list.Add(CreateWeapon("wpn_jian_iron", "Железный меч-цзянь", "Iron Jian Sword",
                "Одноручный прямой меч. Стандартное оружие внешних сект.",
                EquipmentSlot.WeaponMain, WeaponHandType.OneHand,
                damage: 18, penetration: 4, attackRange: 2,
                weight: 1.8f, rarity: ItemRarity.Uncommon, grade: EquipmentGrade.Common,
                materialCategory: MaterialCategory.Metal, materialTier: 2));

            list.Add(CreateWeapon("wpn_qiang_steel", "Стальное копьё цян", "Steel Qiang Spear",
                "Двуручное копьё с листовидным наконечником. Длинный бой.",
                EquipmentSlot.WeaponMain, WeaponHandType.TwoHand,
                damage: 28, penetration: 8, attackRange: 3,
                weight: 3.2f, rarity: ItemRarity.Rare, grade: EquipmentGrade.Refined,
                materialCategory: MaterialCategory.Metal, materialTier: 3));

            list.Add(CreateWeapon("wpn_zhang_wood", "Деревянный посох", "Wooden Staff",
                "Одноручный посох для медитации и фокусировки Ци.",
                EquipmentSlot.WeaponOff, WeaponHandType.OneHand,
                damage: 8, penetration: 2, attackRange: 2,
                weight: 1.2f, rarity: ItemRarity.Common, grade: EquipmentGrade.Common,
                materialCategory: MaterialCategory.Wood, materialTier: 1));

            // === Броня: Head ===
            list.Add(CreateArmor("arm_helmet_iron", "Железный шлем", "Iron Helmet",
                "Закрытый шлем с назащитником. Тяжёлый, но надёжный.",
                EquipmentSlot.Head, defense: 12, coverage: 85f, damageReduction: 15f,
                weight: 2.5f, rarity: ItemRarity.Uncommon, grade: EquipmentGrade.Common,
                materialCategory: MaterialCategory.Metal, materialTier: 2));

            // === Броня: Torso ===
            list.Add(CreateArmor("arm_breastplate_steel", "Стальной нагрудник", "Steel Breastplate",
                "Кованый нагрудник поверх кольчуги. Защита витальных органов.",
                EquipmentSlot.Torso, defense: 25, coverage: 90f, damageReduction: 25f,
                weight: 6.0f, rarity: ItemRarity.Rare, grade: EquipmentGrade.Refined,
                materialCategory: MaterialCategory.Metal, materialTier: 3));

            list.Add(CreateArmor("arm_robe_silk", "Шёлковая роба", "Silk Robe",
                "Лёгкая роба из духовного шёлка. Проводит Ци, слабая защита.",
                EquipmentSlot.Torso, defense: 5, coverage: 70f, damageReduction: 5f,
                weight: 1.0f, rarity: ItemRarity.Uncommon, grade: EquipmentGrade.Common,
                materialCategory: MaterialCategory.Cloth, materialTier: 2,
                qiFlowBonus: 10f, moveSpeedPenalty: 0f));

            // === Броня: Legs ===
            list.Add(CreateArmor("arm_greaves_leather", "Кожаные поножи", "Leather Greaves",
                "Поножи из дублёной кожи. Баланс защиты и подвижности.",
                EquipmentSlot.Legs, defense: 8, coverage: 75f, damageReduction: 10f,
                weight: 1.8f, rarity: ItemRarity.Common, grade: EquipmentGrade.Common,
                materialCategory: MaterialCategory.Leather, materialTier: 2));

            // === Броня: Feet ===
            list.Add(CreateArmor("arm_boots_leather", "Кожаные сапоги", "Leather Boots",
                "Прочные дорожные сапоги. Защита стоп, бесшумный шаг.",
                EquipmentSlot.Feet, defense: 4, coverage: 80f, damageReduction: 5f,
                weight: 1.2f, rarity: ItemRarity.Common, grade: EquipmentGrade.Common,
                materialCategory: MaterialCategory.Leather, materialTier: 1,
                dodgeBonus: 3f));

            // === Броня: Belt ===
            list.Add(CreateArmor("arm_belt_spirit", "Пояс духовных камней", "Spirit Stone Belt",
                "Кожаный пояс с гнёздами для духовных камней. Небольшой бонус Ци.",
                EquipmentSlot.Belt, defense: 2, coverage: 30f, damageReduction: 2f,
                weight: 0.5f, rarity: ItemRarity.Uncommon, grade: EquipmentGrade.Common,
                materialCategory: MaterialCategory.Leather, materialTier: 2,
                qiFlowBonus: 5f));

            // === Броня: Hands ===
            list.Add(CreateArmor("arm_gloves_iron", "Железные перчатки", "Iron Gauntlets",
                "Латные перчатки с кожаной подкладкой. Защита кистей.",
                EquipmentSlot.Hands, defense: 6, coverage: 90f, damageReduction: 8f,
                weight: 1.5f, rarity: ItemRarity.Uncommon, grade: EquipmentGrade.Common,
                materialCategory: MaterialCategory.Metal, materialTier: 2));

            // === Аксессуары ===
            list.Add(CreateAccessory("acc_amulet_jade", "Нефритовый амулет", "Jade Amulet",
                "Амулет из духовного нефрита. Усиливает проводимость Ци.",
                EquipmentSlot.Amulet, weight: 0.2f, rarity: ItemRarity.Rare,
                qiFlowBonus: 15f));

            list.Add(CreateAccessory("acc_ring_wood", "Кольцо из духовного дерева", "Spirit Wood Ring",
                "Кольцо с гравировкой. Хранит немного Ци носителя.",
                EquipmentSlot.RingLeft1, weight: 0.1f, rarity: ItemRarity.Uncommon,
                qiFlowBonus: 5f));

            list.Add(CreateAccessory("acc_cloak_wolf", "Плащ из волчьей шкуры", "Wolf Pelt Cloak",
                "Тёплый плащ. Снижает урон от холода, слегка скрывает присутствие.",
                EquipmentSlot.Back, weight: 1.5f, rarity: ItemRarity.Uncommon,
                dodgeBonus: 5f));

            // === Расходники ===
            list.Add(CreateConsumable("con_pill_healing", "Пилюля лечения", "Healing Pill",
                "Базовая лечебная пилюля. Восстанавливает 30 HP.",
                weight: 0.05f, volume: 0.1f, rarity: ItemRarity.Common, maxStack: 20,
                effectType: "heal", effectValue: 30));

            list.Add(CreateConsumable("con_pill_qi", "Пилюля Ци", "Qi Pill",
                "Восстанавливает 50 единиц Ци. Стандарт для медитаций.",
                weight: 0.05f, volume: 0.1f, rarity: ItemRarity.Uncommon, maxStack: 20,
                effectType: "qi_restore", effectValue: 50));

            list.Add(CreateConsumable("con_scroll_teleport", "Свиток телепорта", "Teleport Scroll",
                "Одноразовый свиток возврата в безопасную зону.",
                weight: 0.1f, volume: 0.2f, rarity: ItemRarity.Rare, maxStack: 5,
                effectType: "teleport", effectValue: 1));

            list.Add(CreateConsumable("con_elixir_vitality", "Эликсир жизненной силы", "Vitality Elixir",
                "Пол permanently повышает максимальный HP на 10. Очень редкий.",
                weight: 0.2f, volume: 0.3f, rarity: ItemRarity.Epic, maxStack: 1,
                effectType: "vitality_boost", effectValue: 10));

            // === Материалы (добываемые из окружения) ===
            // IDs match ObjectDefaults.ItemId so ResourceHarvestedEvent → ItemAddRequestEvent resolves correctly.
            list.Add(CreateMaterial("material_wood", "Древесина", "Wood",
                "Обработанное дерево. Базовый строительный материал.",
                weight: 0.5f, volume: 1.0f, rarity: ItemRarity.Common, maxStack: 100,
                materialCategory: MaterialCategory.Wood, materialTier: 1));

            list.Add(CreateMaterial("material_stone", "Камень", "Stone",
                "Кусок необработанного камня. Для строительства и крафта.",
                weight: 1.0f, volume: 1.0f, rarity: ItemRarity.Common, maxStack: 100,
                materialCategory: MaterialCategory.Organic, materialTier: 1));

            list.Add(CreateMaterial("material_iron_ore", "Железная руда", "Iron Ore",
                "Руда с примесью железа. Нужна переплавка в горне.",
                weight: 1.5f, volume: 1.0f, rarity: ItemRarity.Uncommon, maxStack: 50,
                materialCategory: MaterialCategory.Metal, materialTier: 2));

            list.Add(CreateMaterial("material_copper_ore", "Медная руда", "Copper Ore",
                "Мягкий металл. Проводит Ци лучше железа.",
                weight: 1.3f, volume: 1.0f, rarity: ItemRarity.Uncommon, maxStack: 50,
                materialCategory: MaterialCategory.Metal, materialTier: 2));

            // === Расходники из окружения ===
            list.Add(CreateConsumable("consumable_berry", "Ягоды", "Berries",
                "Дикие ягоды. Восстанавливают 5 HP и немного утоляют голод.",
                weight: 0.05f, volume: 0.1f, rarity: ItemRarity.Common, maxStack: 50,
                effectType: "heal", effectValue: 5));

            list.Add(CreateConsumable("consumable_herb", "Лекарственная трава", "Medicinal Herb",
                "Целебная трава. Компонент для пилюль и отваров.",
                weight: 0.03f, volume: 0.1f, rarity: ItemRarity.Uncommon, maxStack: 50,
                effectType: "material", effectValue: 0));

            return list;
        }

        // === Фабричные методы ===

        private static EquipmentData CreateWeapon(
            string id, string nameRu, string nameEn, string desc,
            EquipmentSlot slot, WeaponHandType handType,
            int damage, int penetration, int attackRange,
            float weight, ItemRarity rarity, EquipmentGrade grade,
            MaterialCategory materialCategory, int materialTier)
        {
            return new EquipmentData
            {
                ItemId = id,
                NameRu = nameRu,
                NameEn = nameEn,
                Description = desc,
                Category = ItemCategory.Weapon,
                ItemType = "Weapon",
                Rarity = rarity,
                Stackable = false,
                Weight = weight,
                Volume = 2.0f,
                Value = damage * 5,
                HasDurability = true,
                MaxDurability = 100,
                Slot = slot,
                HandType = handType,
                Damage = damage,
                Penetration = penetration,
                AttackRange = attackRange,
                Grade = grade,
                ItemLevel = 1,
                MaterialCategory = materialCategory,
                MaterialTier = materialTier,
            };
        }

        private static EquipmentData CreateArmor(
            string id, string nameRu, string nameEn, string desc,
            EquipmentSlot slot, int defense, float coverage, float damageReduction,
            float weight, ItemRarity rarity, EquipmentGrade grade,
            MaterialCategory materialCategory, int materialTier,
            float qiFlowBonus = 0f, float dodgeBonus = 0f, float moveSpeedPenalty = 0f)
        {
            return new EquipmentData
            {
                ItemId = id,
                NameRu = nameRu,
                NameEn = nameEn,
                Description = desc,
                Category = ItemCategory.Armor,
                ItemType = "Armor",
                Rarity = rarity,
                Stackable = false,
                Weight = weight,
                Volume = 3.0f,
                Value = defense * 8,
                HasDurability = true,
                MaxDurability = 120,
                Slot = slot,
                HandType = WeaponHandType.None,
                Defense = defense,
                Coverage = coverage,
                DamageReduction = damageReduction,
                DodgeBonus = dodgeBonus,
                MoveSpeedPenalty = moveSpeedPenalty,
                QiFlowPenalty = -qiFlowBonus, // negative penalty = bonus
                Grade = grade,
                ItemLevel = 1,
                MaterialCategory = materialCategory,
                MaterialTier = materialTier,
            };
        }

        private static EquipmentData CreateAccessory(
            string id, string nameRu, string nameEn, string desc,
            EquipmentSlot slot, float weight, ItemRarity rarity,
            float qiFlowBonus = 0f, float dodgeBonus = 0f)
        {
            return new EquipmentData
            {
                ItemId = id,
                NameRu = nameRu,
                NameEn = nameEn,
                Description = desc,
                Category = ItemCategory.Accessory,
                ItemType = "Accessory",
                Rarity = rarity,
                Stackable = false,
                Weight = weight,
                Volume = 0.5f,
                Value = 50,
                HasDurability = false,
                Slot = slot,
                HandType = WeaponHandType.None,
                Grade = EquipmentGrade.Common,
                ItemLevel = 1,
                QiFlowPenalty = -qiFlowBonus,
                DodgeBonus = dodgeBonus,
            };
        }

        private static ItemData CreateConsumable(
            string id, string nameRu, string nameEn, string desc,
            float weight, float volume, ItemRarity rarity, int maxStack,
            string effectType, int effectValue)
        {
            return new ItemData
            {
                ItemId = id,
                NameRu = nameRu,
                NameEn = nameEn,
                Description = desc,
                Category = ItemCategory.Consumable,
                ItemType = "Consumable",
                Rarity = rarity,
                Stackable = true,
                MaxStack = maxStack,
                Weight = weight,
                Volume = volume,
                Value = effectValue,
                HasDurability = false,
            };
        }

        private static ItemData CreateMaterial(
            string id, string nameRu, string nameEn, string desc,
            float weight, float volume, ItemRarity rarity, int maxStack,
            MaterialCategory materialCategory, int materialTier)
        {
            return new ItemData
            {
                ItemId = id,
                NameRu = nameRu,
                NameEn = nameEn,
                Description = desc,
                Category = ItemCategory.Material,
                ItemType = "Material",
                Rarity = rarity,
                Stackable = true,
                MaxStack = maxStack,
                Weight = weight,
                Volume = volume,
                Value = materialTier * 5,
                HasDurability = false,
            };
        }
    }
}

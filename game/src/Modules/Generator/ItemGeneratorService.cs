#nullable enable
// Создано: 2026-05-18 17:58:25 UTC
// Редактировано: 2026-03-05 12:00:00 UTC — Task 2.2: грейд по уровню, зарядник Ци, тир материала, бонусы Refined+
// Фасад генерации предметов. Создаёт EquipmentData/ItemData через ScriptableObject.CreateInstance,
// заполняет поля на основе уровня и сида, регистрирует в ItemDatabaseService и возвращает.
using System;
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Generator
{
    /// <summary>
    /// Реализация IItemGeneratorService.
    /// Генерирует предметы (оружие, броня, зарядники, расходники) как ScriptableObject-ы,
    /// заполняет поля по формулам на основе уровня культивации и сида,
    /// затем регистрирует в ItemDatabaseService.
    ///
    /// Task 2.2: добавлена зависимая от уровня дистрибуция грейда
    /// (Core.Data.GeneratorTables.EquipmentGradeWeightsByLevel),
    /// генерация зарядников Ци, тир материала clamp((level+1)/2,1,5),
    /// бонусные свойства для грейда Refined+.
    ///
    /// IMPL-5 (Q6): weight tables moved from NPCConfig to
    /// Core.Data.GeneratorTables — Generator no longer depends on the NPC
    /// module for configuration data.
    /// </summary>
    public class ItemGeneratorService : IItemGeneratorService
    {
        // === Зависимости ===
        private readonly IItemDatabaseService _itemDatabase;

        // === Счётчик для уникальных ID (на случай seed=0) ===
        private long _generationCounter;

        // === Суффиксы грейда для русских названий ===
        private static readonly string[] GradeSuffixRu = new string[]
        {
            " (Повреждённый)",  // Damaged
            "",                  // Common — без суффикса
            " (Очищенный)",     // Refined
            " (Совершенный)",   // Perfect
            " (Трансцендентный)" // Transcendent
        };

        /// <summary>
        /// Конструктор. Принимает сервис базы данных предметов.
        /// Весовые таблицы для генерации грейда берутся из статического
        /// <see cref="GeneratorTables"/> (Core.Data) — больше не зависит
        /// от NPCConfig (Q6).
        /// </summary>
        /// <param name="itemDatabase">Сервис базы данных предметов.</param>
        public ItemGeneratorService(IItemDatabaseService itemDatabase)
        {
            _itemDatabase = itemDatabase ?? throw new ArgumentNullException(nameof(itemDatabase));
            _generationCounter = 0;
        }

        // ===================================================================
        // Публичные методы — генерация экипировки
        // ===================================================================

        /// <inheritdoc/>
        public EquipmentData GenerateWeaponForLevel(int cultivationLevel, long seed = 0)
        {
            long effectiveSeed = seed != 0 ? seed : System.Threading.Interlocked.Increment(ref _generationCounter);
            var rng = new SeededRandom(effectiveSeed);

            // Грейд зависит от уровня (взвешенный рандом)
            EquipmentGrade grade = RollGradeForLevel(cultivationLevel, rng);
            int tier = CalculateMaterialTier(cultivationLevel);

            var weapon = new EquipmentData();

            // === Идентификация ===
            weapon.ItemId = $"weapon_{cultivationLevel}_{effectiveSeed % 1000:D3}";
            weapon.NameRu = $"Меч уровня {cultivationLevel}{GradeSuffixRu[(int)grade]}";
            weapon.NameEn = $"Sword Level {cultivationLevel}";
            weapon.Description = $"Сгенерированное оружие для уровня культивации {cultivationLevel}";
            weapon.ItemType = "Weapon";

            // === Классификация ===
            weapon.Category = ItemCategory.Weapon;
            weapon.Rarity = GradeToRarity(grade);

            // === Стакинг ===
            weapon.Stackable = false;
            weapon.MaxStack = 1;

            // === Физические свойства — тир материала снижает вес ===
            float baseWeight = 2.0f + cultivationLevel * 0.3f;
            weapon.Weight = baseWeight * (1f - (tier - 1) * 0.08f);
            weapon.Volume = 2.0f;
            weapon.Value = (10 + cultivationLevel * 5) * tier;

            // === Прочность — тир материала увеличивает ===
            weapon.HasDurability = true;
            weapon.MaxDurability = (50 + cultivationLevel * 20) + (tier - 1) * 25;

            // === Экипировка ===
            weapon.Slot = EquipmentSlot.WeaponMain;
            weapon.HandType = WeaponHandType.OneHand;

            // === Характеристики ===
            weapon.Damage = 5 + cultivationLevel * 3;
            weapon.Defense = 0;

            // === Материал и грейд ===
            weapon.Grade = grade;
            weapon.ItemLevel = cultivationLevel;
            weapon.MaterialTier = tier;

            // === Требования ===
            weapon.RequiredCultivationLevel = cultivationLevel;

            // === Бонусные свойства для Refined+ ===
            ApplyBonusProperties(weapon, grade, rng);

            // Регистрируем в базе данных перед возвратом
            _itemDatabase.Register(weapon);

            return weapon;
        }

        /// <inheritdoc/>
        public EquipmentData GenerateArmorForLevel(int cultivationLevel, long seed = 0)
        {
            long effectiveSeed = seed != 0 ? seed : System.Threading.Interlocked.Increment(ref _generationCounter);
            var rng = new SeededRandom(effectiveSeed);

            // Грейд зависит от уровня (взвешенный рандом)
            EquipmentGrade grade = RollGradeForLevel(cultivationLevel, rng);
            int tier = CalculateMaterialTier(cultivationLevel);

            var armor = new EquipmentData();

            // === Идентификация ===
            armor.ItemId = $"armor_{cultivationLevel}_{effectiveSeed % 1000:D3}";
            armor.NameRu = $"Броня уровня {cultivationLevel}{GradeSuffixRu[(int)grade]}";
            armor.NameEn = $"Armor Level {cultivationLevel}";
            armor.Description = $"Сгенерированная броня для уровня культивации {cultivationLevel}";
            armor.ItemType = "Armor";

            // === Классификация ===
            armor.Category = ItemCategory.Armor;
            armor.Rarity = GradeToRarity(grade);

            // === Стакинг ===
            armor.Stackable = false;
            armor.MaxStack = 1;

            // === Физические свойства — тир материала снижает вес ===
            float baseWeight = 4.0f + cultivationLevel * 0.5f;
            armor.Weight = baseWeight * (1f - (tier - 1) * 0.08f);
            armor.Volume = 3.0f;
            armor.Value = (15 + cultivationLevel * 6) * tier;

            // === Прочность — тир материала увеличивает ===
            armor.HasDurability = true;
            armor.MaxDurability = (60 + cultivationLevel * 25) + (tier - 1) * 30;

            // === Экипировка ===
            armor.Slot = EquipmentSlot.Torso;
            armor.HandType = WeaponHandType.None; // Броня не занимает руки

            // === Характеристики ===
            armor.Damage = 0;
            armor.Defense = 3 + cultivationLevel * 2;

            // === Покрытие и снижение урона ===
            armor.Coverage = 80f + cultivationLevel * 2f;
            armor.DamageReduction = 5f + cultivationLevel * 1.5f;

            // === Штрафы ===
            armor.MoveSpeedPenalty = -5f - cultivationLevel * 0.5f;

            // === Материал и грейд ===
            armor.Grade = grade;
            armor.ItemLevel = cultivationLevel;
            armor.MaterialTier = tier;
            armor.MaterialCategory = MaterialCategory.Metal;

            // === Требования ===
            armor.RequiredCultivationLevel = cultivationLevel;

            // === Бонусные свойства для Refined+ ===
            ApplyBonusProperties(armor, grade, rng);

            // Регистрируем в базе данных перед возвратом
            _itemDatabase.Register(armor);

            return armor;
        }

        /// <inheritdoc/>
        public EquipmentData GenerateChargerForLevel(int cultivationLevel, long seed = 0)
        {
            // Зарядники доступны с L3+
            if (cultivationLevel < 3)
            {
                Console.WriteLine($"[ItemGeneratorService] GenerateChargerForLevel: уровень {cultivationLevel} < 3, зарядник недоступен. Возвращаем null.");
                return null;
            }

            long effectiveSeed = seed != 0 ? seed : System.Threading.Interlocked.Increment(ref _generationCounter);
            var rng = new SeededRandom(effectiveSeed);

            // Грейд зависит от уровня (взвешенный рандом)
            EquipmentGrade grade = RollGradeForLevel(cultivationLevel, rng);
            int tier = CalculateMaterialTier(cultivationLevel);

            var charger = new EquipmentData();

            // === Идентификация ===
            charger.ItemId = $"charger_{cultivationLevel}_{effectiveSeed % 1000:D3}";
            charger.NameRu = $"Зарядник Ци{GradeSuffixRu[(int)grade]}";
            charger.NameEn = $"Qi Charger{GradeSuffixRu[(int)grade]}";
            charger.Description = $"Зарядник Ци для ускорения накачки техник. Уровень культивации {cultivationLevel}.";
            charger.ItemType = "Charger";

            // === Классификация ===
            charger.Category = ItemCategory.Accessory;
            charger.Rarity = GradeToRarity(grade);

            // === Стакинг ===
            charger.Stackable = false;
            charger.MaxStack = 1;

            // === Физические свойства — зарядники лёгкие ===
            charger.Weight = 0.5f + tier * 0.2f;
            charger.Volume = 1.0f;
            charger.Value = (20 + cultivationLevel * 8) * tier;

            // === Прочность ===
            charger.HasDurability = true;
            charger.MaxDurability = (40 + cultivationLevel * 15) + (tier - 1) * 20;

            // === Экипировка — слот Belt, без захвата руками ===
            charger.Slot = EquipmentSlot.Belt;
            charger.HandType = WeaponHandType.None;

            // === Характеристики — зарядник не даёт урон/защиту ===
            charger.Damage = 0;
            charger.Defense = 0;

            // === Бонус скорости накачки техник ===
            charger.ChargeSpeedBonus = ChargerChargeSpeedBonus(cultivationLevel);

            // === Материал и грейд ===
            charger.Grade = grade;
            charger.ItemLevel = cultivationLevel;
            charger.MaterialTier = tier;
            charger.MaterialCategory = MaterialCategory.Spirit;

            // === Требования ===
            charger.RequiredCultivationLevel = cultivationLevel;

            // === Бонусные свойства для Refined+ ===
            ApplyBonusProperties(charger, grade, rng);

            // Регистрируем в базе данных перед возвратом
            _itemDatabase.Register(charger);

            return charger;
        }

        /// <inheritdoc/>
        public ItemData GenerateConsumableForLevel(int cultivationLevel, long seed = 0)
        {
            // Убеждаемся, что seed ненулевой для уникальности
            long effectiveSeed = seed != 0 ? seed : System.Threading.Interlocked.Increment(ref _generationCounter);

            // Создаём SO-экземпляр расходника (базовый ItemData, не EquipmentData)
            var consumable = new ItemData();

            // === Идентификация ===
            consumable.ItemId = $"consumable_{cultivationLevel}_{effectiveSeed % 1000:D3}";
            consumable.NameRu = $"Лекарство уровня {cultivationLevel}";
            consumable.NameEn = $"Medicine Level {cultivationLevel}";
            consumable.Description = $"Сгенерированное лекарство для уровня культивации {cultivationLevel}";
            consumable.ItemType = "Consumable";

            // === Классификация ===
            consumable.Category = ItemCategory.Consumable;
            consumable.Rarity = ItemRarity.Common;

            // === Стакинг ===
            consumable.Stackable = true;
            consumable.MaxStack = 20;

            // === Физические свойства ===
            consumable.Weight = 0.1f;
            consumable.Volume = 0.1f;
            consumable.Value = 2 + cultivationLevel * 2;

            // === Прочность (расходники не имеют прочности) ===
            consumable.HasDurability = false;
            consumable.MaxDurability = 0;

            // === Эффекты при использовании ===
            consumable.Effects = new List<ItemEffect>
            {
                new ItemEffect
                {
                    EffectType = "Heal",
                    Value = 10 + cultivationLevel * 5,
                    Duration = 0 // Мгновенное исцеление
                }
            };

            // === Требования ===
            consumable.RequiredCultivationLevel = 0; // Расходники доступны всем

            // Регистрируем в базе данных перед возвратом
            _itemDatabase.Register(consumable);

            return consumable;
        }

        /// <inheritdoc/>
        public EquipmentData GenerateRandomEquipment(int playerLevel, long seed = 0)
        {
            // Определяем тип экипировки: 50% оружие, 50% броня
            long effectiveSeed = seed != 0 ? seed : System.Threading.Interlocked.Increment(ref _generationCounter);
            bool isWeapon = (effectiveSeed % 2) == 0;

            if (isWeapon)
            {
                return GenerateWeaponForLevel(playerLevel, effectiveSeed);
            }
            else
            {
                return GenerateArmorForLevel(playerLevel, effectiveSeed);
            }
        }

        /// <inheritdoc/>
        public List<EquipmentData> GenerateLoot(int playerLevel, int count, long seed = 0)
        {
            var loot = new List<EquipmentData>();

            if (count <= 0)
            {
                return loot;
            }

            long effectiveSeed = seed != 0 ? seed : System.Threading.Interlocked.Increment(ref _generationCounter);

            for (int i = 0; i < count; i++)
            {
                // Каждый предмет получает уникальный сид на основе базового сида и индекса
                long itemSeed = effectiveSeed + i * 7919; // 7919 — простое число для распределения
                var equipment = GenerateRandomEquipment(playerLevel, itemSeed);
                loot.Add(equipment);
            }

            return loot;
        }

        /// <inheritdoc/>
        public List<ItemData> GenerateConsumableLoot(int playerLevel, int count, long seed = 0)
        {
            var loot = new List<ItemData>();

            if (count <= 0)
            {
                return loot;
            }

            long effectiveSeed = seed != 0 ? seed : System.Threading.Interlocked.Increment(ref _generationCounter);

            for (int i = 0; i < count; i++)
            {
                // Каждый расходник получает уникальный сид
                long itemSeed = effectiveSeed + i * 6271; // 6271 — простое число для распределения
                var consumable = GenerateConsumableForLevel(playerLevel, itemSeed);
                loot.Add(consumable);
            }

            return loot;
        }

        // ===================================================================
        // Приватные вспомогательные методы
        // ===================================================================

        /// <summary>
        /// Маппинг уровня культивации на индекс массива EquipmentGradeWeightsByLevel.
        /// L1→0, L2→1, L3-4→2, L5-6→3, L7-8→4, L9+→5.
        /// </summary>
        private static int LevelToGradeWeightsIndex(int level)
        {
            if (level <= 1) return 0;
            if (level == 2) return 1;
            if (level <= 4) return 2;
            if (level <= 6) return 3;
            if (level <= 8) return 4;
            return 5;
        }

        /// <summary>
        /// Бросить грейд экипировки по весам, зависимым от уровня.
        /// Использует Core.Data.GeneratorTables.EquipmentGradeWeightsByLevel
        /// (Q6: перенесено из NPCConfig).
        /// </summary>
        private EquipmentGrade RollGradeForLevel(int level, SeededRandom rng)
        {
            int index = LevelToGradeWeightsIndex(level);
            float[] weights = GeneratorTables.EquipmentGradeWeightsByLevel[index];

            int gradeIndex = rng.NextWeighted(weights);
            // Гарантируем валидный индекс грейда
            gradeIndex = Math.Clamp(gradeIndex, 0, 4);
            return (EquipmentGrade)gradeIndex;
        }

        /// <summary>
        /// Рассчитать тир материала: clamp((level+1)/2, 1, 5).
        /// Влияет на вес (снижение) и прочность (увеличение).
        /// </summary>
        private static int CalculateMaterialTier(int level)
        {
            return Math.Clamp((level + 1) / 2, 1, 5);
        }

        /// <summary>
        /// Бонус скорости накачки зарядника по уровню.
        /// L3=5%, L5=10%, L7=15%, L9=25%.
        /// </summary>
        private static float ChargerChargeSpeedBonus(int level)
        {
            if (level <= 4) return 5f;   // L3-4
            if (level <= 6) return 10f;  // L5-6
            if (level <= 8) return 15f;  // L7-8
            return 25f;                   // L9+
        }

        /// <summary>
        /// Применить бонусные свойства к экипировке для грейда Refined+.
        /// Количество бонусов: Damaged/Common=0, Refined=1, Perfect=2, Transcendent=3.
        /// Типы: StatBonus (STR/AGI/VIT/INT), ConductivityBonus (qiFlowPenalty+), WeightReduction.
        /// Значения масштабируются по грейду.
        /// </summary>
        private void ApplyBonusProperties(EquipmentData equip, EquipmentGrade grade, SeededRandom rng)
        {
            int gradeIndex = (int)grade;
            // Количество бонусов: Damaged/Common — 0, Refined — 1, Perfect — 2, Transcendent — 3
            int bonusCount = gradeIndex >= 2 ? gradeIndex - 1 : 0;

            if (bonusCount <= 0) return;

            // Названия характеристик для StatBonus
            string[] statNames = { "STR", "AGI", "VIT", "INT" };

            // Масштаб значений по грейду
            float statValue = gradeIndex switch
            {
                2 => 2f,    // Refined: +2 к характеристике
                3 => 4f,    // Perfect: +4
                4 => 7f,    // Transcendent: +7
                _ => 0f
            };

            float conductivityValue = gradeIndex switch
            {
                2 => 3f,    // Refined: +3% проводимость
                3 => 7f,    // Perfect: +7%
                4 => 12f,   // Transcendent: +12%
                _ => 0f
            };

            float weightReductionValue = gradeIndex switch
            {
                2 => 5f,    // Refined: 5% снижение веса
                3 => 10f,   // Perfect: 10%
                4 => 15f,   // Transcendent: 15%
                _ => 0f
            };

            for (int i = 0; i < bonusCount; i++)
            {
                // Тип бонуса: 0-3 = StatBonus (STR/AGI/VIT/INT), 4 = Conductivity, 5 = WeightReduction
                int bonusType = rng.Next(0, 6);

                if (bonusType < 4)
                {
                    // StatBonus — добавляется в список, стекается
                    equip.StatBonuses.Add(new StatBonus
                    {
                        StatName = statNames[bonusType],
                        Value = statValue,
                        IsPercentage = false
                    });
                }
                else if (bonusType == 4)
                {
                    // ConductivityBonus — положительный qiFlowPenalty, суммируется с ограничением
                    equip.QiFlowPenalty = Math.Clamp(
                        equip.QiFlowPenalty + conductivityValue,
                        -30f, 30f);
                }
                else
                {
                    // WeightReduction — суммируется с ограничением
                    equip.WeightReduction = Math.Clamp(
                        equip.WeightReduction + weightReductionValue,
                        0f, 50f);
                }
            }
        }

        /// <summary>
        /// Конвертировать EquipmentGrade в ItemRarity.
        /// Damaged→Common, Common→Common, Refined→Uncommon, Perfect→Rare, Transcendent→Epic.
        /// </summary>
        private static ItemRarity GradeToRarity(EquipmentGrade grade)
        {
            return grade switch
            {
                EquipmentGrade.Damaged => ItemRarity.Common,
                EquipmentGrade.Common => ItemRarity.Common,
                EquipmentGrade.Refined => ItemRarity.Uncommon,
                EquipmentGrade.Perfect => ItemRarity.Rare,
                EquipmentGrade.Transcendent => ItemRarity.Epic,
                _ => ItemRarity.Common
            };
        }
    }
}

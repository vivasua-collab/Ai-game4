#nullable enable
// Создано: 2026-08-22 — генератор экипировки «Матрёшка»
// (EQUIPMENT_SYSTEM.md §2). Слои:
//   Базовый класс (EquipmentGenerationTables.Weapons/Armors)
//   × Материал (EquipmentGenerationTables.Materials, §5)
//   × Грейд (GeneratorTables.EquipmentGradeWeightsByLevel, §4.1-4.2)
//   × Зачарование (EquipmentGenerationTables.Enchants, §8 — опц. слой)
//
// Заменяет линейные формулы ItemGeneratorService (все мечи / все Torso /
// penetration=0 / dodge=0 / id-коллизии — NPC_COMBAT_PREP §8).
// ItemGeneratorService остаётся для consumables/chargers.
using System;
using System.Collections.Generic;
using System.Threading;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Generator;

/// <summary>
/// Детерминированный генератор экипировки. Итоговые параметры (§2):
///   Эффективность = BaseStats × GradeEfficiency × (1 + MaterialBonus)
///   Прочность     = MaterialBaseDurability × GradeDurability
///   Бонусы        = по грейду §7.3 (кол-во и сила)
/// </summary>
public sealed class EquipmentGenerator : IEquipmentGenerator
{
    private readonly IItemDatabaseService _itemDatabase;
    private static int _idCounter;

    // §7.1 — пул бонусов для ролла по грейду (combat/defense/qi).
    private static readonly string[] BonusPool =
    {
        "damage", "armorPenetration", "critChance", "attackSpeed",
        "armor", "dodgeChance", "blockChance", "healthMax",
        "qiRestoration", "qiCostReduction",
    };

    public EquipmentGenerator(IItemDatabaseService itemDatabase)
    {
        _itemDatabase = itemDatabase ?? throw new ArgumentNullException(nameof(itemDatabase));
    }

    public EquipmentData GenerateWeapon(int level, string? subtype = null, long seed = 0)
    {
        var rng = Rng(level, seed);
        var @class = PickWeaponClass(subtype, rng);
        var material = PickMaterial(level, rng, @class);
        var grade = RollGradeForLevel(level, rng);
        float eff = GradeProfiles.EfficiencyMult[(int)grade];

        var item = new EquipmentData
        {
            // ItemId без коллизий: полный счётчик + seed (фикс modulo-1000).
            ItemId = NextId("wep", @class.Id, level, seed),
            NameRu = $"{material.NameRu} {@class.NameRu}{GradeSuffix(grade)}",
            NameEn = $"{material.Id}_{@class.Id}_{level}",
            Description = $"{@class.NameRu} ({@class.SpeedClass}), {@class.Id}. " +
                          $"Материал: {material.NameRu} T{material.Tier}, грейд {grade}.",
            ItemType = "Weapon",
            Category = ItemCategory.Weapon,
            Rarity = GradeToRarity(grade),
            Stackable = false,
            MaxStack = 1,

            // §11.5: вес = база класса × материал; объём = clamp(weight, 1, 4).
            Weight = @class.WeightKg * material.WeightMult,
            Volume = 1.0f,
            Value = (10 + level * 5) * material.Tier,

            // §2: Прочность = MaterialBaseDurability × GradeDurability.
            HasDurability = true,
            MaxDurability = (int)(MaterialDurabilityByTier.For(material.Tier)
                                  * GradeProfiles.DurabilityMult[(int)grade]),

            Slot = @class.HandType == WeaponHandType.TwoHand
                ? EquipmentSlot.WeaponMain
                : EquipmentSlot.WeaponMain,
            HandType = @class.HandType,

            // §2: Эффективность = Base × Grade × (1 + MaterialDamage).
            // Скорость-класс модулирует урон: быстрое — легче, медленное — больнее.
            Damage = ScaleInt(
                (@class.DamageBase + @class.DamagePerLevel * (level - 1))
                * SpeedDamageScale(@class.AttackSpeedFactor)
                * eff * (1f + material.DamageBonus / 100f)),
            Penetration = @class.Penetration + (material.Tier - 1),
            AttackRange = @class.AttackRangeTiles,

            Grade = grade,
            ItemLevel = level,
            MaterialId = material.Id,
            MaterialCategory = material.Category,
            MaterialTier = material.Tier,
            RequiredCultivationLevel = level,
        };
        item.Volume = Math.Clamp(item.Weight, 1f, 4f); // §11.5

        RollStatBonuses(item, grade, rng);
        _itemDatabase.Register(item);
        return item;
    }

    public EquipmentData GenerateArmor(int level, string? subtype = null, long seed = 0)
    {
        var rng = Rng(level, seed);
        var @class = PickArmorClass(subtype, rng);
        var material = PickMaterial(level, rng, armor: true);
        var grade = RollGradeForLevel(level, rng);
        float eff = GradeProfiles.EfficiencyMult[(int)grade];

        float weight = @class.WeightKg * material.WeightMult;

        var item = new EquipmentData
        {
            ItemId = NextId("arm", @class.Id, level, seed),
            NameRu = $"{material.NameRu} {@class.NameRu}{GradeSuffix(grade)}",
            NameEn = $"{material.Id}_{@class.Id}_{level}",
            Description = $"{@class.NameRu} ({@class.Slot}). " +
                          $"Материал: {material.NameRu} T{material.Tier}, грейд {grade}.",
            ItemType = "Armor",
            Category = ItemCategory.Armor,
            Rarity = GradeToRarity(grade),
            Stackable = false,
            MaxStack = 1,

            Weight = weight,
            Volume = Math.Clamp(weight, 1f, 4f), // §11.5
            Value = (15 + level * 6) * material.Tier,

            HasDurability = true,
            MaxDurability = (int)(MaterialDurabilityByTier.For(material.Tier)
                                  * GradeProfiles.DurabilityMult[(int)grade]),

            Slot = @class.Slot,
            HandType = WeaponHandType.None,

            // §2: Эффективность = Base × Grade × (1 + MaterialDefense).
            Damage = 0,
            Defense = ScaleInt(
                (@class.DefenseBase + @class.DefensePerLevel * (level - 1))
                * eff * (1f + material.DefenseBonus / 100f)),

            // §11.2: покрытие — диапазон базового класса.
            Coverage = @class.CoverageMin
                     + rng.NextFloat() * (@class.CoverageMax - @class.CoverageMin),
            DamageReduction = Math.Min(80f, 3f + level * 1.5f), // §11.1: 0-80%

            // §11.1: dodgePenalty −40..0 — лёгкий материал частично компенсирует.
            DodgeBonus = -@class.DodgePenalty + LightMaterialDodgeBonus(material),
            // §11.1: штраф скорости от веса.
            MoveSpeedPenalty = -Math.Min(50f, weight * 0.8f),

            Grade = grade,
            ItemLevel = level,
            MaterialId = material.Id,
            MaterialCategory = material.Category,
            MaterialTier = material.Tier,
            RequiredCultivationLevel = level,
        };

        RollStatBonuses(item, grade, rng);
        _itemDatabase.Register(item);
        return item;
    }

    public EquipmentData GenerateRandom(int level, long seed = 0)
    {
        var rng = Rng(level, seed);
        return rng.Next(0, 2) == 0
            ? GenerateWeapon(level, null, seed + 1)
            : GenerateArmor(level, null, seed + 2);
    }

    /// <summary>
    /// §8: наложить зачарование. Сила роллится в диапазоне определения и
    /// умножается на множитель эффективности грейда (§8.4 правило 4).
    /// </summary>
    public bool TryApplyEnchant(EquipmentData item, string? enchantId = null, long seed = 0)
    {
        if (item == null) return false;
        var rng = new SeededRandom(seed != 0 ? seed : Interlocked.Increment(ref _idCounter));

        EnchantDefinition? def = null;
        if (!string.IsNullOrEmpty(enchantId))
        {
            foreach (var e in EquipmentGenerationTables.Enchants)
                if (e.Id == enchantId) { def = e; break; }
        }
        else
        {
            // Случайное зачарование, допустимое по грейду (§8.3: MinGrade).
            var eligible = new List<EnchantDefinition>();
            foreach (var e in EquipmentGenerationTables.Enchants)
                if (item.Grade >= e.MinGrade) eligible.Add(e);
            if (eligible.Count > 0) def = eligible[rng.Next(0, eligible.Count)];
        }

        if (def == null || item.Grade < def.MinGrade) return false;

        float baseValue = def.ValueMin + rng.NextFloat() * (def.ValueMax - def.ValueMin);
        float effective = baseValue * GradeProfiles.EfficiencyMult[(int)item.Grade]; // §8.4

        item.SpecialEffects.Add(new SpecialEffect
        {
            EffectName = def.Id,
            Description = $"Зачарование «{def.NameRu}» T{def.Tier}: +{effective:F1}% {def.EffectType}",
            TriggerChance = 100f,
        });
        item.StatBonuses.Add(new StatBonus
        {
            StatName = def.EffectType,
            Value = effective,
            IsPercentage = def.IsPercent,
        });

        // Имя отражает зачарование (§8.5 примеры).
        item.NameRu = $"{item.NameRu} «{def.NameRu}»";
        return true;
    }

    // === Вспомогательные ===

    private static SeededRandom Rng(int level, long seed) =>
        new(seed != 0 ? seed : Interlocked.Increment(ref _idCounter) * 1_000_003 + level);

    private static string NextId(string prefix, string subtype, int level, long seed)
    {
        int counter = Interlocked.Increment(ref _idCounter);
        return $"eq_{prefix}_{subtype}_L{level}_{seed & 0xFFFF:x4}_{counter:x6}";
    }

    private static WeaponBaseClass PickWeaponClass(string? subtype, SeededRandom rng)
    {
        foreach (var w in EquipmentGenerationTables.Weapons)
            if (w.Id == subtype) return w;
        var list = EquipmentGenerationTables.Weapons;
        return list[rng.Next(0, list.Count)];
    }

    private static ArmorBaseClass PickArmorClass(string? subtype, SeededRandom rng)
    {
        foreach (var a in EquipmentGenerationTables.Armors)
            if (a.Id == subtype) return a;
        var list = EquipmentGenerationTables.Armors;
        return list[rng.Next(0, list.Count)];
    }

    /// <summary>
    /// Материал по тиру уровня (§5.1): тир = clamp((level+1)/2, 1, 5).
    /// Оружие — metal/bone/crystal; броня — любой (вкл. leather/cloth/silk).
    /// </summary>
    private static MaterialDef PickMaterial(int level, SeededRandom rng, WeaponBaseClass? @class = null, bool armor = false)
    {
        int tier = Math.Clamp((level + 1) / 2, 1, 5);
        var candidates = new List<MaterialDef>();
        foreach (var m in EquipmentGenerationTables.Materials)
        {
            if (m.Tier != tier) continue;
            bool ok = armor
                ? true
                : m.Category is MaterialCategory.Metal or MaterialCategory.Bone
                              or MaterialCategory.Crystal or MaterialCategory.Wood;
            if (ok) candidates.Add(m);
        }
        if (candidates.Count == 0) return EquipmentGenerationTables.Materials[0];
        return candidates[rng.Next(0, candidates.Count)];
    }

    private static EquipmentGrade RollGradeForLevel(int level, SeededRandom rng)
    {
        int index = LevelToGradeWeightsIndex(level);
        float[] weights = GeneratorTables.EquipmentGradeWeightsByLevel[index];
        return (EquipmentGrade)Math.Clamp(rng.NextWeighted(weights), 0, 4);
    }

    private static int LevelToGradeWeightsIndex(int level) => level switch
    {
        <= 1 => 0,
        2 => 1,
        <= 4 => 2,
        <= 6 => 3,
        <= 8 => 4,
        _ => 5,
    };

    /// <summary>
    /// Скорость-класс → масштаб урона: быстрое (1.3) → 0.85×, среднее → 1.0×,
    /// медленное (0.75) → 1.125×. Формула 1 + (1 - factor) / 2.
    /// </summary>
    private static float SpeedDamageScale(float speedFactor) =>
        1f + (1f - speedFactor) / 2f;

    /// <summary>Лёгкий материал даёт бонус уклонения (до +8% при weightMult→0).</summary>
    private static float LightMaterialDodgeBonus(MaterialDef m) =>
        Math.Max(0f, (1f - m.WeightMult) * 8f);

    /// <summary>§7.3 — бонусы по грейду: кол-во min..max, сила × множитель грейда.</summary>
    private void RollStatBonuses(EquipmentData item, EquipmentGrade grade, SeededRandom rng)
    {
        int gi = (int)grade;
        int count = rng.Next(GradeProfiles.BonusCountMin[gi],
                             GradeProfiles.BonusCountMax[gi] + 1);
        float power = GradeProfiles.BonusPowerMult[gi];

        for (int i = 0; i < count; i++)
        {
            string stat = BonusPool[rng.Next(0, BonusPool.Length)];
            // Базовая сила 2-5% (или 2-5 ед.), масштаб грейдом.
            float value = (2 + rng.Next(0, 4)) * power;
            item.StatBonuses.Add(new StatBonus
            {
                StatName = stat,
                Value = value,
                IsPercentage = true,
            });
        }
    }

    private static int ScaleInt(float f) => Math.Max(1, (int)MathF.Round(f));

    private static string GradeSuffix(EquipmentGrade g) => g switch
    {
        EquipmentGrade.Damaged => " (поврежд.)",
        EquipmentGrade.Refined => " (очищ.)",
        EquipmentGrade.Perfect => " (соверш.)",
        EquipmentGrade.Transcendent => " (трансц.)",
        _ => "",
    };

    private static ItemRarity GradeToRarity(EquipmentGrade g) => g switch
    {
        EquipmentGrade.Damaged => ItemRarity.Common,
        EquipmentGrade.Common => ItemRarity.Common,
        EquipmentGrade.Refined => ItemRarity.Uncommon,
        EquipmentGrade.Perfect => ItemRarity.Rare,
        EquipmentGrade.Transcendent => ItemRarity.Epic,
        _ => ItemRarity.Common,
    };
}

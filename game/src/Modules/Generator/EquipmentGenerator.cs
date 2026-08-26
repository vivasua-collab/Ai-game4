#nullable enable
// Создано: 2026-08-22 — генератор экипировки «Матрёшка»
// (EQUIPMENT_SYSTEM.md §2). Слои:
//   Базовый класс (EquipmentGenerationTables.Weapons/Armors)
//   × Материал (EquipmentGenerationTables.Materials, §5)
//   × Грейд (GeneratorTables.EquipmentGradeWeightsByLevel, §4.1-4.2)
//   × Зачарование (EquipmentGenerationTables.Enchants, §8 — опц. слой)
//
// 2026-08-26 — Epic→Legendary промоушен с шансом оверкапа:
//   • при ролле grade=Transcendent (Epic) — шанс EPIC_TO_LEGENDARY_PROMOTE_CHANCE
//     (20%) на промоушен в Legendary;
//   • легендарка ВСЕГДА получает: гарант. зачарование + макс. бонусы грейда +
//     value ×3 + суффикс «(легендар.)»;
//   • шанс LEGENDARY_OVERCAP_CHANCE (18%) на ОВЕРКАП: Damage/Defense и
//     Durability считаются по формулам уровня L+1 (не каждая легендарка
//     «улетает» на новый ранг — только ~18%, диапазон ТЗ 10–25%);
//   • GenerateLegendaryWeapon/Armor — принудительный легендарный путь
//     (forceOvercap?: null=ролл, true/false=детерминированно для тестов).
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
        => GenerateWeaponCore(level, subtype, seed, forceLegendary: false, forceOvercap: null);

    /// <summary>
    /// Сгенерировать ЛЕГЕНДАРНОЕ оружие (принудительный путь Epic→Legendary).
    /// forceOvercap: null → ролл шанса LEGENDARY_OVERCAP_CHANCE (18%);
    /// true/false — детерминированный оверкап (для тестов обеих веток).
    /// </summary>
    public EquipmentData GenerateLegendaryWeapon(int level, string? subtype = null, long seed = 0, bool? forceOvercap = null)
        => GenerateWeaponCore(level, subtype, seed, forceLegendary: true, forceOvercap);

    private EquipmentData GenerateWeaponCore(int level, string? subtype, long seed,
        bool forceLegendary, bool? forceOvercap)
    {
        var rng = Rng(level, seed);
        var @class = PickWeaponClass(subtype, rng);
        var material = PickMaterial(level, rng, @class);
        var rolledGrade = RollGradeForLevel(level, rng);

        // Epic→Legendary промоушен: только для ролла Transcendent (rng не
        // потребляется для других грейдов — детерминизм обычных предметов
        // не меняется).
        bool isLegendary = forceLegendary || TryPromoteToLegendary(rolledGrade, rng);
        var grade = isLegendary ? EquipmentGrade.Transcendent : rolledGrade;

        // Оверкап — только у легендарок: статы по формулам L+1.
        bool overcap = false;
        if (isLegendary)
            overcap = forceOvercap ?? RollLegendaryOvercap(rng);

        float eff = GradeProfiles.EfficiencyMult[(int)grade];
        int statLevel = overcap
            ? Math.Min(level + 1, GameConstants.MAX_CULTIVATION_LEVEL)
            : level;

        var item = new EquipmentData
        {
            // ItemId без коллизий: полный счётчик + seed (фикс modulo-1000).
            ItemId = NextId("wep", @class.Id, level, seed),
            NameRu = $"{material.NameRu} {@class.NameRu}{GradeSuffix(grade, isLegendary)}",
            NameEn = $"{material.Id}_{@class.Id}_{level}",
            Description = $"{@class.NameRu} ({@class.SpeedClass}), {@class.Id}. " +
                          $"Материал: {material.NameRu} T{material.Tier}, грейд {grade}" +
                          (isLegendary ? ", ЛЕГЕНДАРНАЯ" : "") + ".",
            ItemType = "Weapon",
            Category = ItemCategory.Weapon,
            Rarity = isLegendary ? ItemRarity.Legendary : GradeToRarity(grade),
            Stackable = false,
            MaxStack = 1,

            // §11.5: вес = база класса × материал; объём = clamp(weight, 1, 4).
            Weight = @class.WeightKg * material.WeightMult,
            Volume = 1.0f,
            // Легендарка: value × LEGENDARY_VALUE_MULTIPLIER.
            Value = (int)MathF.Round((10 + level * 5) * material.Tier
                * (isLegendary ? GameConstants.LEGENDARY_VALUE_MULTIPLIER : 1f)),

            // §2: Прочность = MaterialBaseDurability × GradeDurability.
            // Оверкап: прочность по тиру материала уровня statLevel (L+1).
            HasDurability = true,
            MaxDurability = DurabilityFor(statLevel, grade),

            Slot = @class.HandType == WeaponHandType.TwoHand
                ? EquipmentSlot.WeaponMain
                : EquipmentSlot.WeaponMain,
            HandType = @class.HandType,

            // §2: Эффективность = Base × Grade × (1 + MaterialDamage).
            // Скорость-класс модулирует урон: быстрое — легче, медленное — больнее.
            // Оверкап: базовая линия (base + perLevel×(L-1)) считается для statLevel.
            Damage = ScaleInt(
                (@class.DamageBase + @class.DamagePerLevel * (statLevel - 1))
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

        RollStatBonuses(item, grade, rng, forceMax: isLegendary);
        if (isLegendary)
            ApplyLegendaryPerks(item, overcap, seed, isWeapon: true);
        _itemDatabase.Register(item);
        return item;
    }

    public EquipmentData GenerateArmor(int level, string? subtype = null, long seed = 0)
        => GenerateArmorCore(level, subtype, seed, forceLegendary: false, forceOvercap: null);

    /// <summary>
    /// Сгенерировать ЛЕГЕНДАРНУЮ броню (принудительный путь Epic→Legendary).
    /// forceOvercap: null → ролл шанса LEGENDARY_OVERCAP_CHANCE (18%);
    /// true/false — детерминированный оверкап (для тестов обеих веток).
    /// </summary>
    public EquipmentData GenerateLegendaryArmor(int level, string? subtype = null, long seed = 0, bool? forceOvercap = null)
        => GenerateArmorCore(level, subtype, seed, forceLegendary: true, forceOvercap);

    private EquipmentData GenerateArmorCore(int level, string? subtype, long seed,
        bool forceLegendary, bool? forceOvercap)
    {
        var rng = Rng(level, seed);
        var @class = PickArmorClass(subtype, rng);
        var material = PickMaterial(level, rng, armor: true);
        var rolledGrade = RollGradeForLevel(level, rng);

        // Epic→Legendary промоушен (см. GenerateWeaponCore).
        bool isLegendary = forceLegendary || TryPromoteToLegendary(rolledGrade, rng);
        var grade = isLegendary ? EquipmentGrade.Transcendent : rolledGrade;

        bool overcap = false;
        if (isLegendary)
            overcap = forceOvercap ?? RollLegendaryOvercap(rng);

        float eff = GradeProfiles.EfficiencyMult[(int)grade];
        int statLevel = overcap
            ? Math.Min(level + 1, GameConstants.MAX_CULTIVATION_LEVEL)
            : level;

        float weight = @class.WeightKg * material.WeightMult;

        var item = new EquipmentData
        {
            ItemId = NextId("arm", @class.Id, level, seed),
            NameRu = $"{material.NameRu} {@class.NameRu}{GradeSuffix(grade, isLegendary)}",
            NameEn = $"{material.Id}_{@class.Id}_{level}",
            Description = $"{@class.NameRu} ({@class.Slot}). " +
                          $"Материал: {material.NameRu} T{material.Tier}, грейд {grade}" +
                          (isLegendary ? ", ЛЕГЕНДАРНАЯ" : "") + ".",
            ItemType = "Armor",
            Category = ItemCategory.Armor,
            Rarity = isLegendary ? ItemRarity.Legendary : GradeToRarity(grade),
            Stackable = false,
            MaxStack = 1,

            Weight = weight,
            Volume = Math.Clamp(weight, 1f, 4f), // §11.5
            Value = (int)MathF.Round((15 + level * 6) * material.Tier
                * (isLegendary ? GameConstants.LEGENDARY_VALUE_MULTIPLIER : 1f)),

            HasDurability = true,
            // Оверкап: прочность по тиру материала уровня statLevel (L+1).
            MaxDurability = DurabilityFor(statLevel, grade),

            Slot = @class.Slot,
            HandType = WeaponHandType.None,

            // §2: Эффективность = Base × Grade × (1 + MaterialDefense).
            // Оверкап: базовая линия считается для statLevel (L+1).
            Damage = 0,
            Defense = ScaleInt(
                (@class.DefenseBase + @class.DefensePerLevel * (statLevel - 1))
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

        RollStatBonuses(item, grade, rng, forceMax: isLegendary);
        if (isLegendary)
            ApplyLegendaryPerks(item, overcap, seed, isWeapon: false);
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

    /// <summary>
    /// 2026-08-26 — Epic→Legendary промоушен: только при ролле Transcendent
    /// (Epic). rng НЕ потребляется для других грейдов — детерминизм
    /// обычных (не-Transcendent) предметов полностью сохранён.
    /// </summary>
    private static bool TryPromoteToLegendary(EquipmentGrade rolledGrade, SeededRandom rng) =>
        rolledGrade == EquipmentGrade.Transcendent
        && rng.NextFloat() < GameConstants.EPIC_TO_LEGENDARY_PROMOTE_CHANCE;

    /// <summary>
    /// 2026-08-26 — ролл оверкапа легендарки (шанс LEGENDARY_OVERCAP_CHANCE,
    /// 18%: НЕ каждая легендарка уходит на ранг L+1 по статам).
    /// </summary>
    private static bool RollLegendaryOvercap(SeededRandom rng) =>
        rng.NextFloat() < GameConstants.LEGENDARY_OVERCAP_CHANCE;

    /// <summary>
    /// Прочность = MaterialBaseDurability[тир уровня statLevel] × GradeDurability.
    /// Для оверкапа statLevel = L+1 → тир материала следующего уровня.
    /// </summary>
    private static int DurabilityFor(int statLevel, EquipmentGrade grade)
    {
        int tier = Math.Clamp((statLevel + 1) / 2, 1, 5);
        return (int)(MaterialDurabilityByTier.For(tier)
                     * GradeProfiles.DurabilityMult[(int)grade]);
    }

    /// <summary>
    /// 2026-08-26 — перки легендарки (всегда, независимо от оверкапа):
    /// гарантированное зачарование + пометка оверкапа в описании.
    /// Бонусы грейда (макс. кол-во/сила) — в RollStatBonuses(forceMax).
    /// </summary>
    private void ApplyLegendaryPerks(EquipmentData item, bool overcap, long seed, bool isWeapon)
    {
        // Явный seed (детерминизм headless-дампов); при seed=0 — counter-схема Rng.
        long enchantSeed = seed != 0
            ? seed * 31 + 7
            : Interlocked.Increment(ref _idCounter) * 1_000_003 + item.ItemLevel;
        TryApplyEnchant(item, null, enchantSeed);

        if (overcap)
            item.Description += isWeapon
                ? " ОВЕРКАП: урон и прочность по формулам уровня L+1."
                : " ОВЕРКАП: защита и прочность по формулам уровня L+1.";
    }

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
    /// Оружие — metal/bone/crystal/wood/void (2026-08-26: +void — иначе
    /// tier 5 для оружия ПУСТ (единственный T5-материал void_matter имеет
    /// категорию Void) и генератор молча падал в fallback Materials[0]=iron
    /// T1: L9-оружие получало ЖЕЛЕЗО. Броня — любой (вкл. leather/cloth/silk).
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
                              or MaterialCategory.Crystal or MaterialCategory.Wood
                              or MaterialCategory.Void;
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

    /// <summary>§7.3 — бонусы по грейду: кол-во min..max, сила × множитель грейда.
    /// forceMax (легендарки): кол-во = BonusCountMax, сила = 5 × множитель.</summary>
    private void RollStatBonuses(EquipmentData item, EquipmentGrade grade, SeededRandom rng, bool forceMax = false)
    {
        int gi = (int)grade;
        int count = forceMax
            ? GradeProfiles.BonusCountMax[gi]
            : rng.Next(GradeProfiles.BonusCountMin[gi],
                       GradeProfiles.BonusCountMax[gi] + 1);
        float power = GradeProfiles.BonusPowerMult[gi];

        for (int i = 0; i < count; i++)
        {
            string stat = BonusPool[rng.Next(0, BonusPool.Length)];
            // Базовая сила 2-5% (или 2-5 ед.), масштаб грейдом;
            // forceMax — верхняя грань ролла (5).
            float value = (forceMax ? 5 : 2 + rng.Next(0, 4)) * power;
            item.StatBonuses.Add(new StatBonus
            {
                StatName = stat,
                Value = value,
                IsPercentage = true,
            });
        }
    }

    private static int ScaleInt(float f) => Math.Max(1, (int)MathF.Round(f));

    private static string GradeSuffix(EquipmentGrade g, bool legendary = false) => legendary
        ? " (легендар.)"
        : g switch
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

#nullable enable
// Создано: 2026-08-22 — таблицы генерации экипировки «Матрёшка»
// (EQUIPMENT_SYSTEM.md §2): Базовый класс × Материал × Грейд × Зачарование.
// Таблицы в Core/Data по правилу Q6 (генераторные таблицы не зависят от
// модулей — NPC↔Generator cycle).
//
// Слои:
//   1. Базовый класс (неизменен): подтип оружия/брони, базовые статы (§10.2, §11.2)
//   2. Материал (неизменен): MaterialService (тир 1-5, §5)
//   3. Грейд (изменяемое качество): множители эффективности/прочности (§4.1)
//   4. Зачарование (опц., накладываемый модификатор): тиры T1-T5 (§8)

using System.Collections.Generic;

namespace CultivationGame.Core.Data;

/// <summary>
/// Базовый класс оружия (§10.2 Подтипы оружия). Неизменяемый слой «Матрёшки».
/// Weight — базовый вес кг (§11.5); Penetration — базовое пробитие
/// (piercing-оружие выше, §10.3).
/// </summary>
public sealed class WeaponBaseClass
{
    public string Id = string.Empty;          // "sword", "dagger", ...
    public string NameRu = string.Empty;
    public string SpeedClass = "Medium";      // Light / Medium / Heavy / Ranged / Magic (§10.1)
    public WeaponHandType HandType = WeaponHandType.OneHand;
    public int DamageBase = 5;                // урон на 1 уровне
    public int DamagePerLevel = 3;            // прирост за уровень
    public int Penetration = 0;               // базовое пробитие
    public int AttackRangeTiles = 1;          // дальность в тайлах (§10.2: 0.5-2.0 м)
    public float WeightKg = 2.5f;             // §11.5
    public float AttackSpeedFactor = 1.0f;    // быстрые легче, медленные больнее
}

/// <summary>
/// Базовый класс брони (§11.2 Подтипы брони). Незменяемый слой «Матрёшки».
/// </summary>
public sealed class ArmorBaseClass
{
    public string Id = string.Empty;          // "armor_head", ...
    public string NameRu = string.Empty;
    public EquipmentSlot Slot = EquipmentSlot.Torso;
    public int DefenseBase = 2;
    public int DefensePerLevel = 2;
    public float CoverageMin = 50f;           // §11.2 покрытие части тела, %
    public float CoverageMax = 85f;
    public float WeightKg = 3.0f;
    public float DodgePenalty = 2f;           // штраф уклонения, % (лёгкая броня меньше)
}

/// <summary>
/// Профиль грейда (§4.1): множители эффективности и прочности.
/// Индексируется значением EquipmentGrade.
/// </summary>
public static class GradeProfiles
{
    public static readonly float[] EfficiencyMult =
    {
        0.5f,   // Damaged
        1.0f,   // Common
        1.3f,   // Refined
        1.6f,   // Perfect
        2.0f,   // Transcendent
    };

    public static readonly float[] DurabilityMult =
    {
        0.5f,   // Damaged
        1.0f,   // Common
        1.5f,   // Refined
        2.5f,   // Perfect
        4.0f,   // Transcendent
    };

    /// <summary>Кол-во бонусов по грейду (§7.3) — min/max.</summary>
    public static readonly int[] BonusCountMin = { 0, 0, 1, 2, 4 };
    public static readonly int[] BonusCountMax = { 0, 1, 2, 4, 6 };

    /// <summary>Множитель силы бонусов по грейду (§7.3).</summary>
    public static readonly float[] BonusPowerMult = { 0f, 1.0f, 1.3f, 1.6f, 2.0f };
}

/// <summary>Базовая прочность материала по тиру (§5.1: 20-50 … 400-600).</summary>
public static class MaterialDurabilityByTier
{
    public static readonly int[] Values = { 35, 65, 115, 275, 500 }; // index = tier-1

    public static int For(int tier) =>
        tier is >= 1 and <= 5 ? Values[tier - 1] : Values[0];
}

/// <summary>
/// Материал генерации (§5): тир, категория, бонусы урона/защиты (§5.3),
/// множители веса/ценности. Слой «Материал» Матрёшки — таблица, а не
/// MaterialService, чтобы Generator не зависел от Inventory-модуля (Q6-стиль).
/// </summary>
public sealed class MaterialDef
{
    public string Id = string.Empty;
    public string NameRu = string.Empty;
    public MaterialCategory Category = MaterialCategory.Metal;
    public int Tier = 1;                       // 1-5 (§5.1)
    public float DamageBonus = 0f;             // % к урону (§5.3)
    public float DefenseBonus = 0f;            // % к защите (§5.3)
    public float WeightMult = 1.0f;
    public float ValueMult = 1.0f;
}

/// <summary>
/// Зачарование (§8.2-8.3): категория, тир T1-T5, диапазон силы,
/// минимальный грейд предмета-носителя.
/// </summary>
public sealed class EnchantDefinition{
    public string Id = string.Empty;
    public string NameRu = string.Empty;
    public string Category = "combat";        // combat / defense / qi / vampire / special (§8.2)
    public int Tier = 1;                      // T1-T5 (§8.3)
    public EquipmentGrade MinGrade = EquipmentGrade.Common;
    public string EffectType = string.Empty;  // ключ эффекта (§7.1)
    public float ValueMin = 5f;               // % силы бонуса
    public float ValueMax = 10f;
    public bool IsPercent = true;
}

/// <summary>
/// Статические таблицы генерации экипировки. Заполняются один раз;
/// генераторы читают без копирования.
/// </summary>
public static class EquipmentGenerationTables
{
    // §10.2 — подтипы оружия (7). База × MaterialService × Grade.
    public static readonly List<WeaponBaseClass> Weapons = new()
    {
        new WeaponBaseClass
        {
            Id = "dagger", NameRu = "Кинжал", SpeedClass = "Light",
            HandType = WeaponHandType.OneHand,
            DamageBase = 3, DamagePerLevel = 2, Penetration = 4,
            AttackRangeTiles = 1, WeightKg = 0.5f, AttackSpeedFactor = 1.3f,
        },
        new WeaponBaseClass
        {
            Id = "sword", NameRu = "Меч", SpeedClass = "Medium",
            HandType = WeaponHandType.OneHand,
            DamageBase = 5, DamagePerLevel = 3, Penetration = 2,
            AttackRangeTiles = 1, WeightKg = 2.5f, AttackSpeedFactor = 1.0f,
        },
        new WeaponBaseClass
        {
            Id = "axe", NameRu = "Топор", SpeedClass = "Medium",
            HandType = WeaponHandType.OneHand,
            DamageBase = 6, DamagePerLevel = 3, Penetration = 1,
            AttackRangeTiles = 1, WeightKg = 3.5f, AttackSpeedFactor = 0.9f,
        },
        new WeaponBaseClass
        {
            Id = "spear", NameRu = "Копьё", SpeedClass = "Medium",
            HandType = WeaponHandType.TwoHand,
            DamageBase = 5, DamagePerLevel = 3, Penetration = 6,
            AttackRangeTiles = 2, WeightKg = 3.0f, AttackSpeedFactor = 1.0f,
        },
        new WeaponBaseClass
        {
            Id = "greatsword", NameRu = "Двуручный меч", SpeedClass = "Heavy",
            HandType = WeaponHandType.TwoHand,
            DamageBase = 8, DamagePerLevel = 4, Penetration = 2,
            AttackRangeTiles = 1, WeightKg = 6.0f, AttackSpeedFactor = 0.75f,
        },
        new WeaponBaseClass
        {
            Id = "bow", NameRu = "Лук", SpeedClass = "Ranged",
            HandType = WeaponHandType.TwoHand,
            DamageBase = 4, DamagePerLevel = 3, Penetration = 5,
            AttackRangeTiles = 18, WeightKg = 1.5f, AttackSpeedFactor = 1.0f,
        },
        new WeaponBaseClass
        {
            Id = "staff", NameRu = "Посох", SpeedClass = "Magic",
            HandType = WeaponHandType.TwoHand,
            DamageBase = 2, DamagePerLevel = 2, Penetration = 0,
            AttackRangeTiles = 2, WeightKg = 2.0f, AttackSpeedFactor = 0.8f,
        },
    };

    // §11.2 — подтипы брони (6 активных слотов).
    public static readonly List<ArmorBaseClass> Armors = new()
    {
        new ArmorBaseClass
        {
            Id = "armor_head", NameRu = "Шлем", Slot = EquipmentSlot.Head,
            DefenseBase = 2, DefensePerLevel = 1, CoverageMin = 70f, CoverageMax = 95f,
            WeightKg = 2.0f, DodgePenalty = 2f,
        },
        new ArmorBaseClass
        {
            Id = "armor_torso", NameRu = "Нагрудник", Slot = EquipmentSlot.Torso,
            DefenseBase = 4, DefensePerLevel = 2, CoverageMin = 60f, CoverageMax = 90f,
            WeightKg = 5.0f, DodgePenalty = 4f,
        },
        new ArmorBaseClass
        {
            Id = "armor_arms", NameRu = "Наручи", Slot = EquipmentSlot.Hands,
            DefenseBase = 2, DefensePerLevel = 1, CoverageMin = 50f, CoverageMax = 85f,
            WeightKg = 1.5f, DodgePenalty = 1f,
        },
        new ArmorBaseClass
        {
            Id = "armor_legs", NameRu = "Поножи", Slot = EquipmentSlot.Legs,
            DefenseBase = 3, DefensePerLevel = 2, CoverageMin = 50f, CoverageMax = 85f,
            WeightKg = 3.0f, DodgePenalty = 3f,
        },
        new ArmorBaseClass
        {
            Id = "armor_feet", NameRu = "Сапоги", Slot = EquipmentSlot.Feet,
            DefenseBase = 1, DefensePerLevel = 1, CoverageMin = 30f, CoverageMax = 70f,
            WeightKg = 1.5f, DodgePenalty = 1f,
        },
        new ArmorBaseClass
        {
            Id = "armor_belt", NameRu = "Пояс", Slot = EquipmentSlot.Belt,
            DefenseBase = 1, DefensePerLevel = 1, CoverageMin = 30f, CoverageMax = 60f,
            WeightKg = 0.5f, DodgePenalty = 0f,
        },
    };

    // §5.1 + §5.3 — материалы (тиры 1-5). Прочность по тиру —
    // MaterialDurabilityByTier; бонусы урона/защиты — §5.3.
    public static readonly List<MaterialDef> Materials = new()
    {
        // T1 (§5.1: Iron, Leather, Cloth, Wood, Bone)
        new MaterialDef { Id = "iron",        NameRu = "Железо",           Category = MaterialCategory.Metal,   Tier = 1, DamageBonus = 0f,   DefenseBonus = 0f,   WeightMult = 1.0f,  ValueMult = 1.0f },
        new MaterialDef { Id = "leather",     NameRu = "Кожа",             Category = MaterialCategory.Leather, Tier = 1, DamageBonus = 0f,   DefenseBonus = 5f,   WeightMult = 0.5f,  ValueMult = 0.8f },
        new MaterialDef { Id = "cloth",       NameRu = "Ткань",            Category = MaterialCategory.Cloth,   Tier = 1, DamageBonus = 0f,   DefenseBonus = 0f,   WeightMult = 0.2f,  ValueMult = 0.5f },
        new MaterialDef { Id = "wood",        NameRu = "Дерево",           Category = MaterialCategory.Wood,     Tier = 1, DamageBonus = 0f,   DefenseBonus = 0f,   WeightMult = 0.4f,  ValueMult = 0.3f },
        new MaterialDef { Id = "bone",        NameRu = "Кость",            Category = MaterialCategory.Bone,     Tier = 1, DamageBonus = 5f,   DefenseBonus = 0f,   WeightMult = 0.6f,  ValueMult = 0.6f },
        // T2 (Steel, Silk, Silver, Treated Leather)
        new MaterialDef { Id = "steel",       NameRu = "Сталь",            Category = MaterialCategory.Metal,   Tier = 2, DamageBonus = 10f,  DefenseBonus = 15f,  WeightMult = 1.2f,  ValueMult = 2.0f },
        new MaterialDef { Id = "silk",        NameRu = "Шёлк",             Category = MaterialCategory.Cloth,   Tier = 2, DamageBonus = 0f,   DefenseBonus = 5f,   WeightMult = 0.1f,  ValueMult = 1.5f },
        new MaterialDef { Id = "silver",      NameRu = "Серебро",          Category = MaterialCategory.Metal,   Tier = 2, DamageBonus = 5f,   DefenseBonus = 10f,  WeightMult = 1.1f,  ValueMult = 1.8f },
        // T3 (Spirit Iron, Cold Iron, Jade)
        new MaterialDef { Id = "spirit_iron", NameRu = "Духовное железо",  Category = MaterialCategory.Metal,   Tier = 3, DamageBonus = 25f,  DefenseBonus = 30f,  WeightMult = 0.8f,  ValueMult = 5.0f },
        new MaterialDef { Id = "cold_iron",   NameRu = "Холодное железо",  Category = MaterialCategory.Metal,   Tier = 3, DamageBonus = 20f,  DefenseBonus = 20f,  WeightMult = 1.0f,  ValueMult = 4.0f },
        new MaterialDef { Id = "jade",        NameRu = "Нефрит",           Category = MaterialCategory.Crystal, Tier = 3, DamageBonus = 5f,   DefenseBonus = 25f,  WeightMult = 1.5f,  ValueMult = 4.0f },
        // T4 (Star Metal, Dragon Bone)
        new MaterialDef { Id = "star_metal",  NameRu = "Звёздный металл",  Category = MaterialCategory.Metal,   Tier = 4, DamageBonus = 50f,  DefenseBonus = 45f,  WeightMult = 1.0f,  ValueMult = 15.0f },
        new MaterialDef { Id = "dragon_bone", NameRu = "Кость дракона",    Category = MaterialCategory.Bone,    Tier = 4, DamageBonus = 40f,  DefenseBonus = 35f,  WeightMult = 0.6f,  ValueMult = 20.0f },
        // T5 (Void Matter)
        new MaterialDef { Id = "void_matter", NameRu = "Вещество Пустоты", Category = MaterialCategory.Void,    Tier = 5, DamageBonus = 80f,  DefenseBonus = 70f,  WeightMult = 0.1f,  ValueMult = 100.0f },
    };

    // §8.2-8.3 — зачарования (скелет: по одному на категорию, тиры T1-T3).
    public static readonly List<EnchantDefinition> Enchants = new()    {
        new EnchantDefinition
        {
            Id = "flaming_blade", NameRu = "Пылающий клинок", Category = "combat",
            Tier = 2, MinGrade = EquipmentGrade.Refined,
            EffectType = "damage", ValueMin = 10f, ValueMax = 20f,
        },
        new EnchantDefinition
        {
            Id = "stone_skin", NameRu = "Каменная кожа", Category = "defense",
            Tier = 2, MinGrade = EquipmentGrade.Refined,
            EffectType = "armor", ValueMin = 10f, ValueMax = 20f,
        },
        new EnchantDefinition
        {
            Id = "qi_flow", NameRu = "Поток Ци", Category = "qi",
            Tier = 3, MinGrade = EquipmentGrade.Perfect,
            EffectType = "qiRestoration", ValueMin = 20f, ValueMax = 30f,
        },
        new EnchantDefinition
        {
            Id = "blood_blade", NameRu = "Кровавый клинок", Category = "vampire",
            Tier = 3, MinGrade = EquipmentGrade.Perfect,
            EffectType = "life_steal", ValueMin = 10f, ValueMax = 30f,
        },
        new EnchantDefinition
        {
            Id = "fortune", NameRu = "Удача", Category = "special",
            Tier = 1, MinGrade = EquipmentGrade.Common,
            EffectType = "luck", ValueMin = 5f, ValueMax = 10f,
        },
    };
}

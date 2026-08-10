#nullable enable
using System;

namespace CultivationGame.Core.Data;

// ── Time ────────────────────────────────────────────────────────────────
public enum TimeSpeed
{
    Pause = 0,
    Normal = 1,
    Fast = 5,
    Quick = 15,
}

public enum TimeOfDay
{
    Night,
    Dawn,
    Morning,
    Day,
    Evening,
    Dusk,
}

public enum Season
{
    Warm,
    Cold,
}

public enum Direction
{
    North,
    South,
    East,
    West,
    Northeast,
    Northwest,
    Southeast,
    Southwest,
}

// ── World / tiles ───────────────────────────────────────────────────────
public enum TerrainType
{
    Grass,
    Dirt,
    Stone,
    Water,
    Sand,
    Snow,
    Lava,
    Void,
    Road,
    Bush,
    TallGrass,
    ShallowWater,
    DeepWater,
    Mountain,
    Ice,
}

public enum WaterType
{
    None,
    Fresh,
    Salt,
    Spiritual,
    Poisoned,
}

public enum LocationType
{
    Megapolis,
    BigCity,
    MediumCity,
    Settlement,
    Village,
    Farm,
    Temple,
    Dungeon,
    WildLands,
}

// ── Entities ────────────────────────────────────────────────────────────
public enum BodyPartType
{
    Head,
    Torso,
    LeftArm,
    RightArm,
    LeftLeg,
    RightLeg,
    Heart,
}

public enum SoulType
{
    Character,
    Creature,
    Spirit,
    Artifact,
    Construct,
}

public enum Morphology
{
    Humanoid,
    Quadruped,
    Bird,
    Serpentine,
    Arthropod,
    Amorphous,
    HybridCentaur,
    HybridMermaid,
    HybridHarpy,
    HybridLamia,
}

public enum BodyMaterial
{
    Organic,
    Scaled,
    Chitin,
    Ethereal,
    Mineral,
    Chaos,
}

public enum ConsciousnessType
{
    Full,
    Instinct,
    Simple,
}

// ── Damage / combat ─────────────────────────────────────────────────────
public enum DamageType
{
    Bludgeoning,
    Slashing,
    Piercing,
    Fire,
    Cold,
    Lightning,
    Poison,
    Spirit,
    Chaos,
}

public enum ElementType
{
    None,
    Fire,
    Water,
    Earth,
    Wind,
    Lightning,
    Light,
    Dark,
}

public enum TechniqueType
{
    MeleeStrike,
    MeleeWeapon,
    Ranged,
    Defense,
    Cultivation,
    Support,
}

public enum TechniqueSubtype
{
    MeleeStrike,
    MeleeWeapon,
    RangedProjectile,
    RangedBeam,
    RangedAoe,
    DefenseBlock,
    DefenseDodge,
    DefenseCounter,
    CultivationMeditate,
    CultivationBreakthrough,
    SupportHeal,
    SupportBuff,
    SupportDebuff,
}

public enum FormationType
{
    Barrier,
    Trap,
    Amplification,
    Suppression,
    Gathering,
    Detection,
    Teleportation,
    Summoning,
}

public enum FormationCoreType
{
    Disk,
    Altar,
}

// ── Cultivation / progression ───────────────────────────────────────────
/// <summary>
/// Cultivation level 1..9. Raw int values preserved so they can be
/// used directly in arithmetic (e.g. level suppression).
/// </summary>
public enum CultivationLevel
{
    L1 = 1,
    L2 = 2,
    L3 = 3,
    L4 = 4,
    L5 = 5,
    L6 = 6,
    L7 = 7,
    L8 = 8,
    L9 = 9,
}

// ── Items / inventory ───────────────────────────────────────────────────
public enum ItemCategory
{
    Weapon,
    Armor,
    Accessory,
    Consumable,
    Material,
    QiStone,
    Charger,
    Misc,
}

public enum GameItemType
{
    Consumable,
    Material,
    Equipment,
    QiStone,
    Key,
    Quest,
}

public enum EquipmentSlot
{
    Head,
    Torso,
    Belt,
    Legs,
    Feet,
    WeaponMain,
    WeaponOff,
    Amulet,
    RingLeft1,
    RingLeft2,
    RingRight1,
    RingRight2,
    Charger,
    Hands,
    Back,
}

// ── Stats ───────────────────────────────────────────────────────────────
public enum StatType
{
    Strength,
    Agility,
    Intelligence,
    Vitality,
    Conductivity,
}

// ── Personality ─────────────────────────────────────────────────────────
[Flags]
public enum PersonalityTrait
{
    None = 0,
    Aggressive = 1,
    Cautious = 2,
    Treacherous = 4,
    Ambitious = 8,
    Loyal = 16,
    Pacifist = 32,
    Curious = 64,
    Vengeful = 128,
}

// ── Rendering ───────────────────────────────────────────────────────────
public enum RenderLayer
{
    Default = 0,
    Background = 1,
    Terrain = 2,
    Objects = 3,
    Player = 4,
    UI = 5,
}

// ── Save / session ──────────────────────────────────────────────────────
public enum SaveSlotType
{
    Manual,
    AutoSave,
    QuickSave,
}

#nullable enable
using System;
using System.Collections.Generic;

namespace CultivationGame.Core.Data;

// ── Session ─────────────────────────────────────────────────────────────
/// <summary>Top-level save slot for an in-progress game session.</summary>
[Serializable]
public class GameSessionData
{
    public string Id { get; set; } = string.Empty;
    public string WorldId { get; set; } = string.Empty;
    public string WorldName { get; set; } = string.Empty;
    /// <summary>1=sect, 2=random, 3=custom.</summary>
    public int StartVariant { get; set; }
    public WorldTime WorldTime { get; set; }
    public int DaysSinceStart { get; set; }
    public bool IsPaused { get; set; }
}

// ── Character (player) ──────────────────────────────────────────────────
[Serializable]
public class CharacterData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    // Stats
    public float Strength { get; set; }
    public float Agility { get; set; }
    public float Intelligence { get; set; }
    public float Vitality { get; set; }
    public float Conductivity { get; set; }

    // Cultivation
    public int CultivationLevel { get; set; }
    public int CultivationSubLevel { get; set; }
    public long CoreCapacity { get; set; }
    public float CoreQuality { get; set; }
    public long CurrentQi { get; set; }
    public long AccumulatedQi { get; set; }

    // Physiology
    public float Health { get; set; }
    public float Fatigue { get; set; }
    public float MentalFatigue { get; set; }
    public int Age { get; set; }
    public float BodyHeight { get; set; }

    // Memory
    public bool HasAmnesia { get; set; }
    public bool KnowsAboutSystem { get; set; }

    // Resources
    public int ContributionPoints { get; set; }
    public long SpiritStones { get; set; }

    // Spatial
    public Position2D Position { get; set; }
    public Direction Facing { get; set; }
}

// ── NPC ─────────────────────────────────────────────────────────────────
// NPCState is defined in Core/Data/NPCState.cs (IMPL-1: moved back from Modules/NPC/Data
// to fix Core→Modules architecture violation — BodyPart now also lives in Core.Data).
// BodyPart is defined in Core/Data/BodyPart.cs (moved from Modules/Body for the same reason).

// ── Tile ────────────────────────────────────────────────────────────────
[Serializable]
public class TileData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }

    public TerrainType Terrain { get; set; }
    public float MoveCost { get; set; } = 1f;
    public bool HasImpassableObject { get; set; }
    public bool BlocksVision { get; set; }

    // Qi
    public int BaseQiDensity { get; set; }
    public int CurrentQiDensity { get; set; }
    public float QiModifier { get; set; } = 1f;

    // Temperature
    public float BaseTemperature { get; set; }
    public float CurrentTemperature { get; set; }
    public float TempModifier { get; set; } = 1f;

    // Water
    public bool HasWater { get; set; }
    public WaterType WaterType { get; set; } = WaterType.None;
    public float WaterDepth { get; set; }
    public float WaterPurity { get; set; }

    // Visibility
    public bool IsExplored { get; set; }
    public bool IsVisible { get; set; }

    // POI / danger
    public bool HasPOI { get; set; }
    public bool IsDangerZone { get; set; }

    /// <summary>Object/entity IDs sitting on this tile (simple flat list for v1).</summary>
    public List<string> Objects { get; set; } = new();
}

// ── Inventory item ──────────────────────────────────────────────────────
[Serializable]
public class InventoryItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameId { get; set; } = string.Empty;
    public ItemCategory Category { get; set; }
    public int Rarity { get; set; }
    public string Icon { get; set; } = string.Empty;

    public int Quantity { get; set; }
    public int MaxStack { get; set; } = 1;
    public bool Stackable { get; set; }

    // Row-model physical params
    public float Weight { get; set; }
    public float Volume { get; set; }

    // Equipment V2
    public string MaterialId { get; set; } = string.Empty;
    public int MaterialTier { get; set; }
    public int Grade { get; set; }
    public float DurabilityCurrent { get; set; }
    public float DurabilityMax { get; set; }
    public string DurabilityCondition { get; set; } = "pristine";
    public int ItemLevel { get; set; }
    public float EffectiveDamage { get; set; }
    public float EffectiveDefense { get; set; }
}

// ── Technique ───────────────────────────────────────────────────────────
// NOTE: TechniqueData moved to its own file (TechniqueData.cs) — migrated from
// Ai-game3 with the full technique model (CapacityCost, BaseDamage, Mastery, ...).

// ── Location ────────────────────────────────────────────────────────────
[Serializable]
public class LocationData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Coordinates in meters (logical Z for vertical layering)
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int DistanceFromCenter { get; set; }

    public int QiDensity { get; set; }
    public int QiFlowRate { get; set; }
    public TerrainType TerrainType { get; set; }
    public LocationType LocationType { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int Seed { get; set; }

    /// <summary>Parent sector identifier (e.g. "0_0"). Defaults to origin sector.</summary>
    public string ParentSectorId { get; set; } = "0_0";
}

// ── Factions ────────────────────────────────────────────────────────────
[Serializable]
public class FactionData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NationId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

[Serializable]
public class FactionRelation
{
    public string Id { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    /// <summary>ally / enemy / neutral / vassal (string for extensibility).</summary>
    public string RelationType { get; set; } = "neutral";
    public float Strength { get; set; }
}

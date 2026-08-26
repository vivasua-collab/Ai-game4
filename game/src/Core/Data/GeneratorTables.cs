#nullable enable
// Создано: 2026-08-22 UTC — IMPL-5 (Q6): weight tables moved from NPCConfig to Core.
// ============================================================================
// GeneratorTables — weight tables for procedural generation.
// ----------------------------------------------------------------------------
// These tables used to live in Modules.NPC.NPCConfig and were consumed by BOTH
// the NPC module (SoulGenerator) AND the Generator module (ItemGeneratorService,
// TechniqueGeneratorService). That created a Generator → NPC dependency which
// broke layering: Generator (a leaf module used by NPC) reached up into NPC
// config. Moving the tables to Core/Data breaks the cycle — both NPC and
// Generator now read from Core.
//
// Scope (Q6, AUDIT-2 M6): ONLY the tables that were consumed by Generator
// services are moved here. NPC-internal soul-generation weights
// (CoreQualityMultipliers, AwakeningTypeWeights, LevelDeltaWeights,
// ConductivityGrowthFactors, etc.) remain in NPCConfig because they are
// consumed exclusively by SoulGenerator inside the NPC module and do not
// create a cross-module dependency.
// ============================================================================
using System.Collections.Generic;

namespace CultivationGame.Core.Data;

/// <summary>
/// Static weight tables for procedural generation (weapons, armor, techniques).
/// Moved from <c>NPCConfig</c> to <c>Core.Data</c> (Q6, AUDIT-2 M6) to break
/// the NPC↔Generator dependency cycle. Both <c>Modules.NPC</c> and
/// <c>Modules.Generator</c> read these tables via Core — neither module now
/// reaches into the other.
/// </summary>
public static class GeneratorTables
{
    // =======================================================================
    // Technique generation weights (consumed by TechniqueGeneratorService)
    // =======================================================================

    /// <summary>
    /// Weights for rolling a technique's <see cref="TechniqueGrade"/>.
    /// 4 entries — one per grade: Common, Refined, Perfect, Transcendent.
    /// Source: TECHNIQUE_SYSTEM.md §9.1 / NPC_ASSEMBLY_PIPELINE.md §6.
    /// </summary>
    public static readonly float[] TechniqueGradeWeights = { 60f, 30f, 9f, 1f };

    /// <summary>
    /// Multipliers applied to a technique's base damage for each grade.
    /// 4 entries — one per grade: Common, Refined, Perfect, Transcendent.
    /// IMPORTANT: from documentation — {1.0, 1.3, 1.6, 2.0}. Do NOT use the
    /// legacy {1.0, 1.2, 1.4, 1.6} values (AUDIT-4 F1).
    /// Note: this multiplier affects damage only; QiCost is grade-independent
    /// (see TechniqueGeneratorService.CalculateQiCost).
    /// </summary>
    public static readonly float[] TechniqueGradeMultipliers = { 1.0f, 1.3f, 1.6f, 2.0f };

    // =======================================================================
    // Equipment generation weights (consumed by ItemGeneratorService)
    // =======================================================================

    /// <summary>
    /// Per-level weights for rolling an equipment's
    /// <see cref="EquipmentGrade"/>. Indexed via
    /// <c>ItemGeneratorService.LevelToGradeWeightsIndex</c>.
    /// 5 entries per row: Damaged, Common, Refined, Perfect, Transcendent.
    /// Rows correspond to cultivation level buckets:
    /// [0]=L1, [1]=L2, [2]=L3-4, [3]=L5-6, [4]=L7-8, [5]=L9+.
    /// Source: NPC_ASSEMBLY_PIPELINE.md §5.
    /// </summary>
    public static readonly float[][] EquipmentGradeWeightsByLevel = new[]
    {
        new float[] { 30f, 60f, 10f, 0f, 0f },   // L1
        new float[] { 20f, 50f, 25f, 5f, 0f },   // L2
        new float[] { 10f, 50f, 35f, 5f, 0f },   // L3-4
        new float[] { 5f,  30f, 45f, 20f, 0f },  // L5-6
        new float[] { 0f,  20f, 40f, 35f, 5f },  // L7-8
        new float[] { 0f,  10f, 30f, 40f, 20f }  // L9+
    };
}

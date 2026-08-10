#nullable enable
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Generator;

/// <summary>
/// GeneratorService — Matryoshka-style procedural generation:
///   Base template × Grade × Specialization.
/// Uses SeededRandom for determinism. V1 stub produces InventoryItem /
/// TechniqueData with placeholder field values; full template tables are V2.
/// </summary>
public sealed class GeneratorService : IGeneratorService
{
    private readonly GeneratorConfig _config;

    public GeneratorService(GeneratorConfig? config = null) => _config = config ?? new GeneratorConfig();

    public InventoryItem GenerateItem(string baseId, int grade, string? specialization = null)
    {
        if (string.IsNullOrEmpty(baseId))
            throw new ArgumentException("GenerateItem: baseId must be non-empty", nameof(baseId));

        // Deterministic seed from baseId + grade + specialization
        int seed = HashSeed(baseId, grade, specialization);
        var rng = new SeededRandom(seed);

        var item = new InventoryItem
        {
            Id = $"gen_item_{baseId}_{grade}_{specialization ?? "none"}",
            Name = $"{baseId} {specialization ?? ""} G{grade}".Trim(),
            NameId = baseId,
            Category = ItemCategory.Material, // V1 default
            Rarity = grade,
            Quantity = 1,
            MaxStack = 1,
            Stackable = false,
            Weight = 0.1f + rng.NextFloat() * 2f,
            Volume = 0.05f + rng.NextFloat() * 0.5f,
            Grade = grade,
            DurabilityCurrent = 100f,
            DurabilityMax = 100f,
            ItemLevel = 1 + grade,
            EffectiveDamage = grade * 5f + rng.NextFloat() * 10f,
            EffectiveDefense = grade * 3f + rng.NextFloat() * 5f,
        };

        Console.WriteLine($"[GeneratorService] GenerateItem(baseId='{baseId}', grade={grade}, spec='{specialization}') → '{item.Name}' (dmg={item.EffectiveDamage:F1}, def={item.EffectiveDefense:F1})");
        return item;
    }

    public TechniqueData GenerateTechnique(string baseId, int grade, string? specialization = null)
    {
        if (string.IsNullOrEmpty(baseId))
            throw new ArgumentException("GenerateTechnique: baseId must be non-empty", nameof(baseId));

        int seed = HashSeed(baseId, grade, specialization) ^ 0x5A5A5A5A;
        var rng = new SeededRandom(seed);

        var tech = new TechniqueData
        {
            Id = $"gen_tech_{baseId}_{grade}_{specialization ?? "none"}",
            Name = $"{baseId} {specialization ?? ""} T{grade}".Trim(),
            NameId = baseId,
            Description = $"Procedurally generated technique (base={baseId}, grade={grade}, spec={specialization ?? "none"}).",
            Type = TechniqueType.Cultivation,
            Subtype = TechniqueSubtype.CultivationMeditate,
            Element = ElementType.None,
            Grade = grade,
            Level = 1,
            MinLevel = 1,
            MaxLevel = 10,
            BaseCapacity = 100L * grade,
            MinCultivationLevel = grade,
            QiCost = 10L * grade,
            PhysicalFatigueCost = 1f * grade,
            MentalFatigueCost = 2f * grade,
        };

        Console.WriteLine($"[GeneratorService] GenerateTechnique(baseId='{baseId}', grade={grade}, spec='{specialization}') → '{tech.Name}' (qiCost={tech.QiCost})");
        return tech;
    }

    private static int HashSeed(string baseId, int grade, string? specialization)
    {
        int h = baseId.GetHashCode();
        h = (h * 31) + grade;
        if (!string.IsNullOrEmpty(specialization))
            h = (h * 31) + specialization.GetHashCode();
        return h;
    }
}

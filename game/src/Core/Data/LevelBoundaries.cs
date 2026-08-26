#nullable enable
// Создано: 2026-08-27 — Phase B: границы уровней для техник, экипировки,
// формаций. Источник истины для VerificationService.
//
// Принципы (из плана 2026-08-27):
// - mastery 0..100 даёт разброс capacity 1.0×..1.5× → min/max берутся
//   из крайних значений mastery.
// - qiCost НЕ зависит от mastery → min=max (одна точная формула).
// - damage = capacity × gradeMult → min/max выводятся из min/max capacity.
// - cooldown/range/castTime — детерминированные таблицы → не в границах.
// - coverage/weight — диапазоны по таблицам базовых классов / материалов.
//
// Легендарные / Mythic (Phase B, правило B3):
// - Для техник: Transcendent → max граница = Bound(L+1) по ВСЕМ статам;
//   Perfect → +1 уровень по damage и qiCost (т.к. они критичны).
// - Для экипировки: ItemRarity.Legendary → +1 по Damage и Durability;
//   Mythic → +1 по ВСЕМ статам.
// - VerificationService использует AllowLegendaryOvershoot для проверки.

using System;
using System.Collections.Generic;
using CultivationGame.Modules.Formation.Data;

namespace CultivationGame.Core.Data;

// =====================================================================
// Technique bounds
// =====================================================================

/// <summary>
/// Границы значений для техники данного (level, type, grade).
/// min/max включительны. VerificationService проверяет, что CapacityCost,
/// QiCost, BaseDamage попадают в [Min..Max] (с учётом легендарного оверсама).
/// </summary>
public sealed class TechniqueBounds
{
    public int MinCapacity;
    public int MaxCapacity;
    public long MinQiCost;
    public long MaxQiCost;
    public int MinDamage;
    public int MaxDamage;
    /// <summary>Какие поля разрешено выходить на +1 уровень (легендарки).</summary>
    public OvershootPolicy Overshoot = OvershootPolicy.None;
}

/// <summary>
/// Политика оверсама +1 уровня для характеристик.
/// None = строго в границах L.
/// DamageAndQi = +1 по damage и qiCost (Perfect-техники, Epic-экипировка).
/// All = +1 по всем статам (Transcendent-техники, Legendary/Mythic-экипировка).
/// </summary>
public enum OvershootPolicy { None, DamageAndQi, All }

// =====================================================================
// Equipment bounds
// =====================================================================

/// <summary>
/// Границы значений для экипировки данного (level, weaponClass/armorClass,
/// grade, rarity). min/max включительны.
/// </summary>
public sealed class EquipmentBounds
{
    public int MinDamage;
    public int MaxDamage;
    public int MinDefense;
    public int MaxDefense;
    public int MinDurability;
    public int MaxDurability;
    public float MinCoverage;
    public float MaxCoverage;
    public float MinWeight;
    public float MaxWeight;
    public OvershootPolicy Overshoot = OvershootPolicy.None;
}

// =====================================================================
// Formation bounds
// =====================================================================

/// <summary>
/// Границы для формации данного (level, size). Контур и пул Ци считаются
/// по FormationCalculator (детерминированные формулы) → min=max.
/// </summary>
public sealed class FormationBounds
{
    public long MinContourQi;
    public long MaxContourQi;
    public long MinPoolCapacity;
    public long MaxPoolCapacity;
}

// =====================================================================
// LevelBoundaries — статический калькулятор границ
// =====================================================================

/// <summary>
/// Статический калькулятор границ уровней для техник, экипировки и формаций.
/// Используется VerificationService и CheatPanel (dump).
/// Все формулы соответствуют TechniqueGeneratorService / EquipmentGenerator /
/// FormationCalculator — это зеркало тех же вычислений, но в виде min/max.
/// </summary>
public static class LevelBoundaries
{
    // =================================================================
    // Technique bounds
    // =================================================================

    /// <summary>
    /// Рассчитать границы техники для (level, type, grade).
    /// Формулы из TechniqueGeneratorService (capacity, qiCost, baseDamage).
    /// mastery: 0..100 → factor 1.0..1.5 (разброс capacity).
    /// </summary>
    public static TechniqueBounds TechniqueBoundsFor(int level, TechniqueType type, TechniqueGrade grade)
    {
        if (level < 1) level = 1;
        if (level > GameConstants.MAX_CULTIVATION_LEVEL) level = GameConstants.MAX_CULTIVATION_LEVEL;

        int baseCapacity = GameConstants.BaseCapacityByType.TryGetValue(type, out var bc) ? bc : 50;

        // Cultivation — пассивная техника: capacity=0, qiCost=0, damage=0.
        bool isPassive = type == TechniqueType.Cultivation;
        if (isPassive)
        {
            return new TechniqueBounds
            {
                MinCapacity = 0, MaxCapacity = 0,
                MinQiCost = 0,  MaxQiCost = 0,
                MinDamage = 0,  MaxDamage = 0,
                Overshoot = OvershootPolicy.None,
            };
        }

        // capacity = baseCapacity × 2^(L-1) × (1 + mastery × 0.005)
        // mastery=0 → 1.0×, mastery=100 → 1.5×
        double levelFactor = Math.Pow(2, level - 1);
        int minCap = (int)(baseCapacity * levelFactor * 1.0);
        int maxCap = (int)(baseCapacity * levelFactor * 1.5);

        // qiCost = floor(baseCapacity × 2^(L-1)) — НЕ зависит от mastery
        long qiCost = (long)Math.Floor(baseCapacity * levelFactor);

        // damage = capacity × gradeMult → min и max
        float gradeMult = GradeMultFor(grade);
        int minDmg = Math.Max(1, (int)MathF.Round((float)minCap * gradeMult));
        int maxDmg = Math.Max(minDmg, (int)MathF.Round((float)maxCap * gradeMult));

        var bounds = new TechniqueBounds
        {
            MinCapacity = minCap,
            MaxCapacity = maxCap,
            MinQiCost = qiCost,
            MaxQiCost = qiCost,
            MinDamage = minDmg,
            MaxDamage = maxDmg,
        };

        // Overshoot-политика (B3): Transcendent → All, Perfect → DamageAndQi.
        bounds.Overshoot = grade switch
        {
            TechniqueGrade.Transcendent => OvershootPolicy.All,
            TechniqueGrade.Perfect      => OvershootPolicy.DamageAndQi,
            _                           => OvershootPolicy.None,
        };

        // Ultimate: ×2 damage и ×2 qiCost → это известный верхний сдвиг.
        // VerificationService учитывает Ultimate отдельно (поле IsUltimate).
        return bounds;
    }

    /// <summary>
    /// Применить политику легендарного оверсама: расширить max на +1 уровень.
    /// Возвращает расширенные границы (оригинал не меняется).
    /// </summary>
    public static TechniqueBounds WithOvershootApplied(TechniqueBounds original, int level, TechniqueType type)
    {
        if (original.Overshoot == OvershootPolicy.None) return original;
        // Bound(L+1) для расширения
        var next = TechniqueBoundsFor(Math.Min(level + 1, GameConstants.MAX_CULTIVATION_LEVEL), type, TechniqueGrade.Common);
        var result = new TechniqueBounds
        {
            MinCapacity = original.MinCapacity,
            MaxCapacity = original.MaxCapacity,
            MinQiCost   = original.MinQiCost,
            MaxQiCost   = original.MaxQiCost,
            MinDamage   = original.MinDamage,
            MaxDamage   = original.MaxDamage,
            Overshoot   = original.Overshoot,
        };
        if (original.Overshoot == OvershootPolicy.All)
        {
            result.MaxCapacity = Math.Max(result.MaxCapacity, next.MaxCapacity);
            result.MaxQiCost   = Math.Max(result.MaxQiCost,   next.MaxQiCost);
            result.MaxDamage   = Math.Max(result.MaxDamage,   next.MaxDamage);
        }
        else if (original.Overshoot == OvershootPolicy.DamageAndQi)
        {
            result.MaxQiCost = Math.Max(result.MaxQiCost, next.MaxQiCost);
            result.MaxDamage = Math.Max(result.MaxDamage, next.MaxDamage);
        }
        return result;
    }

    /// <summary>
    /// Минимальный множитель грейда для техники (используется в calc).
    /// Transcendent ×2.0, Perfect ×1.6, Refined ×1.3, Common ×1.0.
    /// </summary>
    private static float GradeMultFor(TechniqueGrade g) => g switch
    {
        TechniqueGrade.Common       => 1.0f,
        TechniqueGrade.Refined      => 1.3f,
        TechniqueGrade.Perfect      => 1.6f,
        TechniqueGrade.Transcendent => 2.0f,
        _                            => 1.0f,
    };

    // =================================================================
    // Equipment bounds
    // =================================================================

    /// <summary>
    /// Границы для ОРУЖИЯ данного (level, weaponClass, grade, rarity).
    /// Формулы из EquipmentGenerator.GenerateWeapon.
    /// </summary>
    public static EquipmentBounds WeaponBoundsFor(int level, WeaponBaseClass wclass, EquipmentGrade grade, ItemRarity rarity)
    {
        if (level < 1) level = 1;
        if (level > GameConstants.MAX_CULTIVATION_LEVEL) level = GameConstants.MAX_CULTIVATION_LEVEL;

        float eff = GradeProfiles.EfficiencyMult[(int)grade];

        // damage = (base + perLevel×(L-1)) × speedScale × eff × (1 + matBonus/100)
        // matBonus: из Materials тира = clamp((L+1)/2,1,5). Диапазон по тиру.
        int tier = Math.Clamp((level + 1) / 2, 1, 5);
        float minMat = float.MaxValue, maxMat = float.MinValue;
        foreach (var m in EquipmentGenerationTables.Materials)
        {
            if (m.Tier != tier) continue;
            if (m.Category is MaterialCategory.Metal or MaterialCategory.Bone
                                    or MaterialCategory.Crystal or MaterialCategory.Wood)
            {
                if (m.DamageBonus < minMat) minMat = m.DamageBonus;
                if (m.DamageBonus > maxMat) maxMat = m.DamageBonus;
            }
        }
        if (minMat == float.MaxValue) { minMat = 0f; maxMat = 0f; }

        float speedScale = 1f + (1f - wclass.AttackSpeedFactor) / 2f;
        float baseDamageLine = (wclass.DamageBase + wclass.DamagePerLevel * (level - 1)) * speedScale * eff;
        int minDmg = Math.Max(1, (int)MathF.Round(baseDamageLine * (1f + minMat / 100f)));
        int maxDmg = Math.Max(minDmg, (int)MathF.Round(baseDamageLine * (1f + maxMat / 100f)));

        // durability = MaterialDurabilityByTier[tier] × DurabilityMult
        int matDur = MaterialDurabilityByTier.For(tier);
        float durMult = GradeProfiles.DurabilityMult[(int)grade];
        int dur = (int)(matDur * durMult);

        // weight: weaponClass.WeightKg × material.WeightMult (min..max по тиру)
        float minW = float.MaxValue, maxW = float.MinValue;
        foreach (var m in EquipmentGenerationTables.Materials)
        {
            if (m.Tier != tier) continue;
            if (m.Category is MaterialCategory.Metal or MaterialCategory.Bone
                                    or MaterialCategory.Crystal or MaterialCategory.Wood)
            {
                if (m.WeightMult < minW) minW = m.WeightMult;
                if (m.WeightMult > maxW) maxW = m.WeightMult;
            }
        }
        if (minW == float.MaxValue) { minW = 1f; maxW = 1f; }
        float minWeight = wclass.WeightKg * minW;
        float maxWeight = wclass.WeightKg * maxW;

        var bounds = new EquipmentBounds
        {
            MinDamage = minDmg, MaxDamage = maxDmg,
            MinDefense = 0, MaxDefense = 0,
            MinDurability = dur, MaxDurability = dur,
            MinCoverage = 0f, MaxCoverage = 0f,
            MinWeight = minWeight, MaxWeight = maxWeight,
        };

        // Overshoot по rarity (B3): Legendary → DamageAndQi (но для экипировки
        // нет qiCost, поэтому DamageAndQi = damage + durability). Mythic → All.
        bounds.Overshoot = rarity switch
        {
            ItemRarity.Mythic    => OvershootPolicy.All,
            ItemRarity.Legendary  => OvershootPolicy.DamageAndQi,
            _                     => OvershootPolicy.None,
        };

        return bounds;
    }

    /// <summary>
    /// Границы для БРОНИ данного (level, armorClass, grade, rarity).
    /// Формулы из EquipmentGenerator.GenerateArmor.
    /// </summary>
    public static EquipmentBounds ArmorBoundsFor(int level, ArmorBaseClass aclass, EquipmentGrade grade, ItemRarity rarity)
    {
        if (level < 1) level = 1;
        if (level > GameConstants.MAX_CULTIVATION_LEVEL) level = GameConstants.MAX_CULTIVATION_LEVEL;

        float eff = GradeProfiles.EfficiencyMult[(int)grade];

        int tier = Math.Clamp((level + 1) / 2, 1, 5);
        float minDef = float.MaxValue, maxDef = float.MinValue;
        float minW = float.MaxValue, maxW = float.MinValue;
        foreach (var m in EquipmentGenerationTables.Materials)
        {
            if (m.Tier != tier) continue;
            if (m.DefenseBonus < minDef) minDef = m.DefenseBonus;
            if (m.DefenseBonus > maxDef) maxDef = m.DefenseBonus;
            if (m.WeightMult < minW) minW = m.WeightMult;
            if (m.WeightMult > maxW) maxW = m.WeightMult;
        }
        if (minDef == float.MaxValue) { minDef = 0f; maxDef = 0f; }
        if (minW == float.MaxValue) { minW = 1f; maxW = 1f; }

        float line = (aclass.DefenseBase + aclass.DefensePerLevel * (level - 1)) * eff;
        int minDefense = Math.Max(1, (int)MathF.Round(line * (1f + minDef / 100f)));
        int maxDefense = Math.Max(minDefense, (int)MathF.Round(line * (1f + maxDef / 100f)));

        int matDur = MaterialDurabilityByTier.For(tier);
        float durMult = GradeProfiles.DurabilityMult[(int)grade];
        int dur = (int)(matDur * durMult);

        float minWeight = aclass.WeightKg * minW;
        float maxWeight = aclass.WeightKg * maxW;

        var bounds = new EquipmentBounds
        {
            MinDamage = 0, MaxDamage = 0,
            MinDefense = minDefense, MaxDefense = maxDefense,
            MinDurability = dur, MaxDurability = dur,
            MinCoverage = aclass.CoverageMin, MaxCoverage = aclass.CoverageMax,
            MinWeight = minWeight, MaxWeight = maxWeight,
        };

        bounds.Overshoot = rarity switch
        {
            ItemRarity.Mythic    => OvershootPolicy.All,
            ItemRarity.Legendary  => OvershootPolicy.DamageAndQi,
            _                     => OvershootPolicy.None,
        };
        // Для брони DamageAndQi = defense + durability (damage == 0).
        return bounds;
    }

    /// <summary>
    /// Применить политику легендарного оверсама к границам экипировки.
    /// Возвращает расширенные границы.
    /// </summary>
    public static EquipmentBounds WithOvershootApplied(EquipmentBounds original, int level,
        WeaponBaseClass? wclass = null, ArmorBaseClass? aclass = null)
    {
        if (original.Overshoot == OvershootPolicy.None) return original;
        var next = wclass != null
            ? WeaponBoundsFor(Math.Min(level + 1, GameConstants.MAX_CULTIVATION_LEVEL), wclass, EquipmentGrade.Common, ItemRarity.Common)
            : aclass != null
                ? ArmorBoundsFor(Math.Min(level + 1, GameConstants.MAX_CULTIVATION_LEVEL), aclass, EquipmentGrade.Common, ItemRarity.Common)
                : original;
        var result = new EquipmentBounds
        {
            MinDamage = original.MinDamage, MaxDamage = original.MaxDamage,
            MinDefense = original.MinDefense, MaxDefense = original.MaxDefense,
            MinDurability = original.MinDurability, MaxDurability = original.MaxDurability,
            MinCoverage = original.MinCoverage, MaxCoverage = original.MaxCoverage,
            MinWeight = original.MinWeight, MaxWeight = original.MaxWeight,
            Overshoot = original.Overshoot,
        };
        if (original.Overshoot == OvershootPolicy.All)
        {
            result.MaxDamage     = Math.Max(result.MaxDamage, next.MaxDamage);
            result.MaxDefense    = Math.Max(result.MaxDefense, next.MaxDefense);
            result.MaxDurability = Math.Max(result.MaxDurability, next.MaxDurability);
        }
        else if (original.Overshoot == OvershootPolicy.DamageAndQi)
        {
            // Damage → для оружия Damage, для брони Defense
            if (wclass != null) result.MaxDamage = Math.Max(result.MaxDamage, next.MaxDamage);
            if (aclass != null) result.MaxDefense = Math.Max(result.MaxDefense, next.MaxDefense);
            result.MaxDurability = Math.Max(result.MaxDurability, next.MaxDurability);
        }
        return result;
    }

    // =================================================================
    // Formation bounds
    // =================================================================

    /// <summary>
    /// Границы для формации (level, size). contourQi и poolCapacity —
    /// детерминированные формулы из FormationCalculator → min=max.
    /// </summary>
    public static FormationBounds FormationBoundsFor(int level, FormationSize size)
    {
        if (level < 1) level = 1;
        if (level > 9) level = 9;
        long contourQi = GameConstants.FORMATION_BASE_CONTOUR_QI * (1L << (level - 1));
        long sizeMult = GameConstants.FormationSizeMultipliers.TryGetValue(size, out var m) ? m : 10;
        long poolCap = contourQi * sizeMult;
        return new FormationBounds
        {
            MinContourQi = contourQi, MaxContourQi = contourQi,
            MinPoolCapacity = poolCap, MaxPoolCapacity = poolCap,
        };
    }
}

// =====================================================================
// (Helper extensions removed — methods renamed to *For to avoid type/method name clash.)
// =====================================================================


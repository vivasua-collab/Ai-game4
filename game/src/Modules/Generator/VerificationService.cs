#nullable enable
// Создано: 2026-08-27 — Phase C: VerificationService.
// Реализация IVerificationService. Сравнивает каждый стат с LevelBoundaries,
// учитывает OvershootPolicy (Legendary/Mythic +1 уровень) и Ultimate-множители.
//
// Использование:
//   var result = _verifier.Validate(tech, level);
//   if (!result.IsValid) Console.WriteLine(string.Join(", ", result.OutOfBoundsFields));
//
// PreGenTechniquePhase использует FilterValid для отбраковки невалидных техник.
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core;

namespace CultivationGame.Modules.Generator
{
    public sealed class VerificationService : IVerificationService
    {
        // =================================================================
        // Technique validation
        // =================================================================

        public ValidationResult Validate(TechniqueData tech, int cultivationLevel)
        {
            if (tech == null) return ValidationResult.Fail("tech", "null technique");

            var result = new ValidationResult { IsValid = true };

            // 1. Уровень техники ≤ cultivationLevel (резонанс §8.1).
            if (tech.Level < 1 || tech.Level > cultivationLevel)
            {
                result.AddOutOfBounds("Level");
                result.Message = $"Level {tech.Level} out of range [1..{cultivationLevel}]";
                result.Severity = ValidationSeverity.Critical;
            }

            // 2. Границы по статам.
            var bounds = LevelBoundaries.TechniqueBoundsFor(tech.Level, tech.Type, tech.Grade);
            var effective = LevelBoundaries.WithOvershootApplied(bounds, tech.Level, tech.Type, tech.Grade);

            // 3. CapacityCost ∈ [min..max] (легендарный оверсам расширяет max).
            if (tech.CapacityCost < effective.MinCapacity || tech.CapacityCost > effective.MaxCapacity)
            {
                result.AddOutOfBounds("CapacityCost");
                result.Message += $" CapacityCost {tech.CapacityCost} out of [{effective.MinCapacity}..{effective.MaxCapacity}];";
            }

            // 4. QiCost ∈ [min..max]. Ultimate удваивает qiCost — это учтено.
            long minQi = effective.MinQiCost;
            long maxQi = effective.MaxQiCost;
            if (tech.IsUltimate)
            {
                minQi = (long)(minQi * GameConstants.ULTIMATE_QI_COST_MULTIPLIER);
                maxQi = (long)(maxQi * GameConstants.ULTIMATE_QI_COST_MULTIPLIER);
            }
            if (tech.QiCost < minQi || tech.QiCost > maxQi)
            {
                result.AddOutOfBounds("QiCost");
                result.Message += $" QiCost {tech.QiCost} out of [{minQi}..{maxQi}];";
            }

            // 5. BaseDamage ∈ [min..max]. Ultimate удваивает damage — это учтено.
            int minDmg = effective.MinDamage;
            int maxDmg = effective.MaxDamage;
            if (tech.IsUltimate)
            {
                minDmg = (int)(minDmg * GameConstants.ULTIMATE_DAMAGE_MULTIPLIER);
                maxDmg = (int)(maxDmg * GameConstants.ULTIMATE_DAMAGE_MULTIPLIER);
            }
            if (tech.BaseDamage < minDmg || tech.BaseDamage > maxDmg)
            {
                result.AddOutOfBounds("BaseDamage");
                result.Message += $" BaseDamage {tech.BaseDamage} out of [{minDmg}..{maxDmg}];";
            }

            // 6. Cultivation — пассивная: capacity/qiCost/damage должны быть 0.
            if (tech.Type == TechniqueType.Cultivation)
            {
                if (tech.CapacityCost != 0) result.AddOutOfBounds("CapacityCost(Cultivation passive)");
                if (tech.QiCost != 0)       result.AddOutOfBounds("QiCost(Cultivation passive)");
                if (tech.BaseDamage != 0)   result.AddOutOfBounds("BaseDamage(Cultivation passive)");
            }

            if (result.OutOfBoundsFields.Count > 0)
            {
                result.IsValid = false;
                if (result.Severity == ValidationSeverity.None)
                    result.Severity = ValidationSeverity.Major;
            }
            return result;
        }

        // =================================================================
        // Equipment validation
        // =================================================================

        public ValidationResult Validate(EquipmentData item, int cultivationLevel)
        {
            if (item == null) return ValidationResult.Fail("item", "null equipment");

            var result = new ValidationResult { IsValid = true };

            // 1. ItemLevel ≤ cultivationLevel (RequiredCultivationLevel).
            if (item.ItemLevel < 1 || item.ItemLevel > cultivationLevel)
            {
                result.AddOutOfBounds("ItemLevel");
                result.Message = $"ItemLevel {item.ItemLevel} out of [1..{cultivationLevel}]";
                result.Severity = ValidationSeverity.Critical;
            }

            // 2. Найти базовый класс для сравнения.
            // 2026-08-26: точное матчинг "_id_" вместо подстроки — иначе
            // "sword" ложно матчится внутри "greatsword" (NameEn =
            // materialId_classId_level, id класса всегда окружён '_').
            WeaponBaseClass? wclass = null;
            ArmorBaseClass? aclass = null;
            foreach (var w in EquipmentGenerationTables.Weapons)
                if (w.Id == item.ItemType || item.NameEn.Contains("_" + w.Id + "_")) { wclass = w; break; }
            if (wclass == null)
            {
                foreach (var a in EquipmentGenerationTables.Armors)
                    if (a.Id == item.ItemType || item.NameEn.Contains("_" + a.Id + "_")) { aclass = a; break; }
            }

            if (wclass != null)
            {
                var bounds = LevelBoundaries.WeaponBoundsFor(item.ItemLevel, wclass, item.Grade, item.Rarity);
                var effective = LevelBoundaries.WithOvershootApplied(bounds, item.ItemLevel, item.Grade, wclass, null);
                CheckInt(result, "Damage", item.Damage, effective.MinDamage, effective.MaxDamage);
                CheckInt(result, "MaxDurability", item.MaxDurability, effective.MinDurability, effective.MaxDurability);
                CheckFloat(result, "Weight", item.Weight, effective.MinWeight, effective.MaxWeight);
            }
            else if (aclass != null)
            {
                var bounds = LevelBoundaries.ArmorBoundsFor(item.ItemLevel, aclass, item.Grade, item.Rarity);
                var effective = LevelBoundaries.WithOvershootApplied(bounds, item.ItemLevel, item.Grade, null, aclass);
                CheckInt(result, "Defense", item.Defense, effective.MinDefense, effective.MaxDefense);
                CheckInt(result, "MaxDurability", item.MaxDurability, effective.MinDurability, effective.MaxDurability);
                CheckFloat(result, "Coverage", item.Coverage, effective.MinCoverage, effective.MaxCoverage);
                CheckFloat(result, "Weight", item.Weight, effective.MinWeight, effective.MaxWeight);
            }
            else
            {
                // Неизвестный базовый класс (например, accessory) — пропускаем статы.
                result.Message = "Unknown base class — stat bounds skipped";
            }

            if (result.OutOfBoundsFields.Count > 0)
            {
                result.IsValid = false;
                if (result.Severity == ValidationSeverity.None)
                    result.Severity = ValidationSeverity.Major;
            }
            return result;
        }

        // =================================================================
        // Formation validation
        // =================================================================

        public ValidationResult Validate(FormationData form, int cultivationLevel)
        {
            if (form == null) return ValidationResult.Fail("form", "null formation");

            var result = new ValidationResult { IsValid = true };

            // 1. RequiredLevel ≤ cultivationLevel.
            if (form.RequiredLevel < 1 || form.RequiredLevel > cultivationLevel)
            {
                result.AddOutOfBounds("RequiredLevel");
                result.Message = $"RequiredLevel {form.RequiredLevel} out of [1..{cultivationLevel}]";
                result.Severity = ValidationSeverity.Critical;
            }

            // 2. Heavy формации — только с L6+ (GameConstants.HEAVY_FORMATION_MIN_LEVEL).
            if (form.Size == FormationSize.Heavy &&
                form.RequiredLevel < GameConstants.HEAVY_FORMATION_MIN_LEVEL)
            {
                result.AddOutOfBounds("Size(Heavy)");
                result.Message += $" Heavy size requires L≥{GameConstants.HEAVY_FORMATION_MIN_LEVEL};";
                result.Severity = ValidationSeverity.Critical;
            }

            // 3. contourQi / poolCapacity — детерминированные, не варьируются.
            // Проверяем только RequiredLevel/Size (более глубокая проверка — в FormationCalculator).
            if (result.OutOfBoundsFields.Count > 0)
            {
                result.IsValid = false;
            }
            return result;
        }

        // =================================================================
        // Filter methods (for batch pre-generation)
        // =================================================================

        public List<TechniqueData> FilterValid(IEnumerable<TechniqueData> techniques, int cultivationLevel)
        {
            var valid = new List<TechniqueData>();
            foreach (var t in techniques)
            {
                var r = Validate(t, cultivationLevel);
                if (r.IsValid) valid.Add(t);
                else Console.WriteLine($"[Verifier] Reject technique {t.TechniqueId}: {r.Message}");
            }
            return valid;
        }

        public List<EquipmentData> FilterValid(IEnumerable<EquipmentData> items, int cultivationLevel)
        {
            var valid = new List<EquipmentData>();
            foreach (var i in items)
            {
                var r = Validate(i, cultivationLevel);
                if (r.IsValid) valid.Add(i);
                else Console.WriteLine($"[Verifier] Reject equipment {i.ItemId}: {r.Message}");
            }
            return valid;
        }

        public List<FormationData> FilterValid(IEnumerable<FormationData> forms, int cultivationLevel)
        {
            var valid = new List<FormationData>();
            foreach (var f in forms)
            {
                var r = Validate(f, cultivationLevel);
                if (r.IsValid) valid.Add(f);
                else Console.WriteLine($"[Verifier] Reject formation {f.Id}: {r.Message}");
            }
            return valid;
        }

        // =================================================================
        // Helpers
        // =================================================================

        private static void CheckInt(ValidationResult result, string field, int value, int min, int max)
        {
            if (value < min || value > max)
            {
                result.AddOutOfBounds(field);
                result.Message += $" {field}={value} out of [{min}..{max}];";
            }
        }

        private static void CheckFloat(ValidationResult result, string field, float value, float min, float max)
        {
            if (value < min || value > max)
            {
                result.AddOutOfBounds(field);
                result.Message += $" {field}={value:F2} out of [{min:F2}..{max:F2}];";
            }
        }
    }
}

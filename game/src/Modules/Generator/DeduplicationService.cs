#nullable enable
// Создано: 2026-08-27 — Phase D: DeduplicationService.
// Детектор дублей по «fingerprint» — кортежу статов, БЕЗ учёта NameRu/Id.
// Применяется в PreGenTechniquePhase для удаления дублей перед регистрацией.
//
// Fingerprint'ы:
//   Technique: (Type, Subtype, Element, Grade, Level, CapacityCost, QiCost,
//               BaseDamage, Cooldown, Range, CastTime, IsUltimate)
//   Equipment: (Slot, MaterialId, Grade, ItemLevel, Damage, Defense, Coverage,
//               MaxDurability, Weight)
//   Formation: (FormationType, Size, Shape, Element, RequiredLevel,
//               EffectRadiusMeters, EffectsCount)
//
// Использование:
//   var unique = _dedup.Deduplicate(techniques);
//   _dedup.Clean(registry); // удалить дубли из TechniqueRegistry
using System;
using System.Collections.Generic;
using System.Text;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Modules.Formation;

namespace CultivationGame.Modules.Generator
{
    /// <summary>
    /// Сервис дедупликации: находит и удаляет дубли по характеристикам
    /// (не по имени/Id). Используется в PreGenTechniquePhase и в CheatPanel
    /// для очистки TechniqueRegistry / FormationRegistry от дублей.
    /// </summary>
    public sealed class DeduplicationService
    {
        // =================================================================
        // Fingerprint builders
        // =================================================================

        /// <summary>
        /// Построить fingerprint для техники. Две техники с одинаковым fingerprint
        /// считаются дублями (одинаковые характеристики, разное имя/id).
        /// </summary>
        public static string Fingerprint(TechniqueData tech)
        {
            if (tech == null) return "null";
            return $"T:{tech.Type}|S:{tech.Subtype}|E:{tech.Element}|G:{tech.Grade}|L:{tech.Level}|" +
                   $"cap:{tech.CapacityCost}|qi:{tech.QiCost}|dmg:{tech.BaseDamage}|" +
                   $"cd:{tech.Cooldown:F2}|rng:{tech.Range:F2}|ct:{tech.CastTime:F2}|U:{tech.IsUltimate}";
        }

        /// <summary>
        /// Построить fingerprint для экипировки.
        /// </summary>
        public static string Fingerprint(EquipmentData item)
        {
            if (item == null) return "null";
            return $"S:{item.Slot}|M:{item.MaterialId}|G:{item.Grade}|L:{item.ItemLevel}|" +
                   $"dmg:{item.Damage}|def:{item.Defense}|cov:{item.Coverage:F1}|" +
                   $"dur:{item.MaxDurability}|wt:{item.Weight:F2}|HT:{item.HandType}";
        }

        /// <summary>
        /// Построить fingerprint для формации.
        /// </summary>
        public static string Fingerprint(FormationData form)
        {
            if (form == null) return "null";
            var sb = new StringBuilder(128);
            sb.Append($"T:{form.FormationType}|S:{form.Size}|Sh:{form.Shape}|E:{form.Element}|L:{form.RequiredLevel}|");
            sb.Append($"R:{form.EffectRadiusMeters}|Eff:{form.Effects.Count}");
            // Каждый эффект в fingerprint (тип + стат + значение).
            foreach (var e in form.Effects)
                sb.Append($"|{e.EffectType}:{e.TargetStat}:{e.Value:F3}");
            return sb.ToString();
        }

        // =================================================================
        // Deduplicate (in-memory list)
        // =================================================================

        /// <summary>
        /// Удалить дубли из списка техник: оставить первых, отбросить дублей.
        /// </summary>
        public List<TechniqueData> Deduplicate(IEnumerable<TechniqueData> techniques)
        {
            var seen = new HashSet<string>();
            var unique = new List<TechniqueData>();
            int duplicates = 0;
            foreach (var t in techniques)
            {
                var fp = Fingerprint(t);
                if (seen.Add(fp))
                    unique.Add(t);
                else
                    duplicates++;
            }
            if (duplicates > 0)
                Console.WriteLine($"[Dedup] Techniques: removed {duplicates} duplicates, kept {unique.Count}");
            return unique;
        }

        /// <summary>
        /// Удалить дубли из списка экипировки.
        /// </summary>
        public List<EquipmentData> Deduplicate(IEnumerable<EquipmentData> items)
        {
            var seen = new HashSet<string>();
            var unique = new List<EquipmentData>();
            int duplicates = 0;
            foreach (var i in items)
            {
                var fp = Fingerprint(i);
                if (seen.Add(fp))
                    unique.Add(i);
                else
                    duplicates++;
            }
            if (duplicates > 0)
                Console.WriteLine($"[Dedup] Equipment: removed {duplicates} duplicates, kept {unique.Count}");
            return unique;
        }

        /// <summary>
        /// Удалить дубли из списка формаций.
        /// </summary>
        public List<FormationData> Deduplicate(IEnumerable<FormationData> forms)
        {
            var seen = new HashSet<string>();
            var unique = new List<FormationData>();
            int duplicates = 0;
            foreach (var f in forms)
            {
                var fp = Fingerprint(f);
                if (seen.Add(fp))
                    unique.Add(f);
                else
                    duplicates++;
            }
            if (duplicates > 0)
                Console.WriteLine($"[Dedup] Formations: removed {duplicates} duplicates, kept {unique.Count}");
            return unique;
        }

        // =================================================================
        // Clean (registry in-place)
        // =================================================================

        /// <summary>
        /// Очистить TechniqueRegistry от дублей. Считает fingerprint по всем
        /// зарегистрированным техникам, удаляет дубли (оставляет первого по id).
        /// </summary>
        /// <returns>Количество удалённых дублей.</returns>
        public int Clean(TechniqueRegistry registry)
        {
            if (registry == null) return 0;
            var all = new List<TechniqueData>(registry.GetAll());
            var seen = new HashSet<string>();
            var duplicates = new List<string>();
            foreach (var t in all)
            {
                var fp = Fingerprint(t);
                if (!seen.Add(fp))
                    duplicates.Add(t.TechniqueId);
            }
            // Удаляем дубли из реестра.
            foreach (var id in duplicates)
                registry.Remove(id);
            if (duplicates.Count > 0)
                Console.WriteLine($"[Dedup.Clean] TechniqueRegistry: removed {duplicates.Count} duplicates, ids: {string.Join(",", duplicates)}");
            return duplicates.Count;
        }

        /// <summary>
        /// Очистить FormationRegistry от дублей.
        /// </summary>
        public int Clean(FormationRegistry registry)
        {
            if (registry == null) return 0;
            var all = new List<FormationData>(registry.GetAll());
            var seen = new HashSet<string>();
            var duplicates = new List<string>();
            foreach (var f in all)
            {
                var fp = Fingerprint(f);
                if (!seen.Add(fp))
                    duplicates.Add(f.Id);
            }
            if (duplicates.Count > 0)
                Console.WriteLine($"[Dedup.Clean] FormationRegistry: {duplicates.Count} duplicates found, ids: {string.Join(",", duplicates)}");
            return duplicates.Count;
        }

        /// <summary>
        /// Подсчитать количество дублей в коллекции (без удаления).
        /// </summary>
        public int CountDuplicates<T>(IEnumerable<T> items, Func<T, string> fingerprint)
        {
            var seen = new HashSet<string>();
            int dups = 0;
            foreach (var item in items)
            {
                var fp = fingerprint(item);
                if (!seen.Add(fp)) dups++;
            }
            return dups;
        }
    }
}

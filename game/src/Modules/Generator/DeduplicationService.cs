#nullable enable
// Создано: 2026-08-27 — Phase D: DeduplicationService.
// Детектор дублей по «fingerprint» — кортежу статов, БЕЗ учёта NameRu/Id.
// Применяется в PreGenTechniquePhase для удаления дублей перед регистрацией.
//
// Этап-15 (ST15-004): fingerprint'ы включают ВСЕ поведенческие поля
// (экипировка: +Pen/Range/SPM/Dodge/DR/MSP/QiFlow; техника: +Mastery/
// ArmorPen/AoE/Homing; формация: +ControlType/TargetTag), инвариантная
// культура и полная точность (R) — культура-зависимость и схлопывание
// малых различий устранены (доказаны префиксом D1-D5).
//
// Fingerprint'ы (Этап-15):
//   Technique: (Type, Subtype, Element, Grade, Level, CapacityCost, QiCost,
//               BaseDamage, Cooldown, Range, CastTime, IsUltimate, Mastery,
//               ArmorPenetration, Aoe*, IsHoming, ProjectileSpeed)
//   Equipment: (Slot, MaterialId, Grade, ItemLevel, Damage, Defense, Coverage,
//               MaxDurability, Weight, HandType, Penetration, AttackRange,
//               AttackSpeedPermil, DodgeBonus, DamageReduction,
//               MoveSpeedPenalty, QiFlowPenalty)
//   Formation: (FormationType, Size, Shape, Element, RequiredLevel,
//               EffectRadiusMeters, EffectsCount, per-effect:
//               EffectType, TargetStat, Value, ControlType, TargetTag)
//
// Использование:
//   var unique = _dedup.Deduplicate(techniques);
//   _dedup.Clean(registry); // удалить дубли из TechniqueRegistry/FormationRegistry
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
        ///
        /// Этап-15 (ST15-004): включены ВСЕ поведенческие поля — Mastery
        /// (влияет на ёмкость), ArmorPenetration, AoE-геометрия (форма/радиус/
        /// угол/спад/лимит целей/SpareAllies), самонаведение (IsHoming,
        /// скорость снаряда). Формат — инвариантная культура + полная
        /// точность (R): fingerprint не зависит от локали и не схлопывает
        /// малые различия (префикс: F2 превращал 3.001 и 3.004 в «3.00»).
        /// Имена/описания НЕ входят (дубли механически одинаковых объектов
        /// должны удаляться — PRE_GENERATION §дедуп).
        /// </summary>
        public static string Fingerprint(TechniqueData tech)
        {
            if (tech == null) return "null";
            return $"T:{tech.Type}|S:{tech.Subtype}|E:{tech.Element}|G:{tech.Grade}|L:{tech.Level}|" +
                   $"cap:{tech.CapacityCost}|qi:{tech.QiCost}|dmg:{tech.BaseDamage}|" +
                   $"cd:{R(tech.Cooldown)}|rng:{R(tech.Range)}|ct:{R(tech.CastTime)}|U:{tech.IsUltimate}|" +
                   $"m:{R(tech.Mastery)}|ap:{tech.ArmorPenetration}|" +
                   $"aoe:{tech.AoeShape}/{tech.AoeRadiusTiles}/{tech.AoeHalfAngleDeg}/{tech.AoeFalloffPermil}/{tech.AoeMaxTargets}/{tech.SpareAlliesPermil}|" +
                   $"hom:{tech.IsHoming}/{tech.ProjectileSpeedTilesPerSec}";
        }

        /// <summary>
        /// Построить fingerprint для экипировки.
        /// Этап-15 (ST15-004): + поведенческие поля боя/применения —
        /// Penetration, AttackRange, AttackSpeedPermil, DodgeBonus,
        /// DamageReduction, MoveSpeedPenalty, QiFlowPenalty. Инвариантная
        /// культура + R-точность (прежние F1/F2 зависели от локали и
        /// теряли малые различия).
        /// </summary>
        public static string Fingerprint(EquipmentData item)
        {
            if (item == null) return "null";
            return $"S:{item.Slot}|M:{item.MaterialId}|G:{item.Grade}|L:{item.ItemLevel}|" +
                   $"dmg:{item.Damage}|def:{item.Defense}|cov:{R(item.Coverage)}|" +
                   $"dur:{item.MaxDurability}|wt:{R(item.Weight)}|HT:{item.HandType}|" +
                   $"pen:{item.Penetration}|rng:{item.AttackRange}|spm:{item.AttackSpeedPermil}|" +
                   $"dodge:{R(item.DodgeBonus)}|dr:{R(item.DamageReduction)}|msp:{R(item.MoveSpeedPenalty)}|qfp:{R(item.QiFlowPenalty)}";
        }

        /// <summary>
        /// Построить fingerprint для формации.
        /// Этап-15 (ST15-004): каждый эффект включает ControlType (Trap:
        /// Freeze ≠ Slow — прежде один fingerprint) и TargetTag; значения —
        /// R-точность, инвариантная культура.
        /// </summary>
        public static string Fingerprint(FormationData form)
        {
            if (form == null) return "null";
            var sb = new StringBuilder(160);
            sb.Append($"T:{form.FormationType}|S:{form.Size}|Sh:{form.Shape}|E:{form.Element}|L:{form.RequiredLevel}|");
            sb.Append($"R:{form.EffectRadiusMeters}|Eff:{form.Effects.Count}");
            // Каждый эффект в fingerprint (тип + стат + значение + контроль + цель).
            foreach (var e in form.Effects)
                sb.Append($"|{e.EffectType}:{e.TargetStat}:{R(e.Value)}:{e.ControlType}:{e.TargetTag}");
            return sb.ToString();
        }

        /// <summary>Инвариантная R-точность (культура-независимость + малые
        /// различия не схлопываются). Сокр. имя — компактность fingerprint.
        /// </summary>
        private static string R(float value)
            => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

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
        /// Этап-15 (ST15-003): прежняя версия только СЧИТАЛА дубли и печатала
        /// счётчик — реестр не менялся (no-op, имитация очистки). Теперь
        /// дубли реально удаляются через FormationRegistry.Remove (API
        /// добавлен Этапом-15), уникальные записи сохраняются, повторный
        /// вызов возвращает 0 (идемпотентность).
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
            // Удаляем дубли из реестра (реальная мутация, не только счётчик).
            int removed = 0;
            foreach (var id in duplicates)
                if (registry.Remove(id)) removed++;
            if (removed > 0)
                Console.WriteLine($"[Dedup.Clean] FormationRegistry: removed {removed} duplicates, ids: {string.Join(",", duplicates)}");
            else if (duplicates.Count > 0)
                Console.WriteLine($"[Dedup.Clean] FormationRegistry: {duplicates.Count} duplicates already absent (idempotent)");
            return removed;
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

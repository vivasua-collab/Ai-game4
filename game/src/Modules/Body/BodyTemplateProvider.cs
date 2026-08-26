#nullable enable
// Создано: 2026-05-18 12:00:00 UTC — Body доработка: провайдер шаблонов тел
// Редактировано: 2026-05-18 12:00:00 UTC — P1-02 FIX: добавлены шаблоны гибридов (Centaur, Mermaid, Harpy, Lamia)
// Редактировано: 2026-05-18 — V3 FIX: P1-06 IStartable вместо RegisterBuildCallback
// Редактировано: 2026-05-21 13:43 UTC — FIX: ленивая инициализация GetTemplate() — IStartable порядок не гарантирован
// Migrated from Ai-game3 (Unity) to Ai-game4 (Godot). Removed VContainer.Unity.IStartable — caller invokes Initialize() explicitly.
// Data-driven замена BodyMorphology.cs — все HP из BODY_SYSTEM.md.
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Body
{
    /// <summary>
    /// Провайдер шаблонов тел.
    /// Создаёт BodyTemplate для каждой морфологии на основе BODY_SYSTEM.md.
    /// Заменяет хардкод из BodyMorphology.cs на data-driven подход.
    /// MIGRATION: IStartable removed (was VContainer lifecycle hook). Caller must
    /// invoke Initialize() once before first GetTemplate() — or rely on the
    /// lazy self-initialization in GetTemplate().
    /// </summary>
    public sealed class BodyTemplateProvider
    {
        private readonly Dictionary<Morphology, BodyTemplate> _templates = new();
        private bool _initialized;

        /// <summary>Инициализировать все шаблоны. Idempotent.</summary>
        public void Initialize()
        {
            if (_initialized) return;
            _templates[Morphology.Humanoid] = CreateHumanoidTemplate();
            _templates[Morphology.Quadruped] = CreateQuadrupedTemplate();
            _templates[Morphology.Bird] = CreateBirdTemplate();
            _templates[Morphology.Serpentine] = CreateSerpentineTemplate();
            _templates[Morphology.Arthropod] = CreateArthropodTemplate();
            _templates[Morphology.Amorphous] = CreateAmorphousTemplate();

            // P1-02 FIX: шаблоны гибридов
            _templates[Morphology.HybridCentaur] = CreateHybridCentaurTemplate();
            _templates[Morphology.HybridMermaid] = CreateHybridMermaidTemplate();
            _templates[Morphology.HybridHarpy] = CreateHybridHarpyTemplate();
            _templates[Morphology.HybridLamia] = CreateHybridLamiaTemplate();

            _initialized = true;
        }

        /// <summary>
        /// Получить шаблон тела по морфологии.
        /// FIX: ленивая инициализация — порядок инициализации не гарантирован.
        /// </summary>
        public BodyTemplate GetTemplate(Morphology morphology)
        {
            if (!_initialized)
                Initialize();
            if (_templates.TryGetValue(morphology, out var template))
                return template;
            throw new ArgumentException($"No BodyTemplate for Morphology={morphology}");
        }

        // ===== ГУМАНОИД =====
        // Источник: BODY_SYSTEM.md §"Части тела гуманоида"
        private BodyTemplate CreateHumanoidTemplate()
        {
            var parts = new List<BodyPartTemplate>
            {
                new("head",       BodyPartType.Head,      50, 100, BodyPartFunction.Sensory | BodyPartFunction.Breathing, new[]{ EquipmentSlot.Head }, "torso", true),
                new("torso",      BodyPartType.Torso,    100, 200, BodyPartFunction.Circulation | BodyPartFunction.Digestion, new[]{ EquipmentSlot.Torso, EquipmentSlot.Belt }, null, true),
                new("heart",      BodyPartType.Heart,     80, 0,   BodyPartFunction.Circulation, Array.Empty<EquipmentSlot>(), "torso", true),
                new("left_arm",   BodyPartType.LeftArm,   40, 80,  BodyPartFunction.Manipulation, new[]{ EquipmentSlot.WeaponOff }, "torso", false),
                new("right_arm",  BodyPartType.RightArm,  40, 80,  BodyPartFunction.Manipulation, new[]{ EquipmentSlot.WeaponMain }, "torso", false),
                new("left_hand",  BodyPartType.LeftHand,  20, 40,  BodyPartFunction.Manipulation, Array.Empty<EquipmentSlot>(), "left_arm", false),
                new("right_hand", BodyPartType.RightHand, 20, 40,  BodyPartFunction.Manipulation, Array.Empty<EquipmentSlot>(), "right_arm", false),
                new("left_leg",   BodyPartType.LeftLeg,   50, 100, BodyPartFunction.Movement, new[]{ EquipmentSlot.Legs }, "torso", false),
                new("right_leg",  BodyPartType.RightLeg,  50, 100, BodyPartFunction.Movement, new[]{ EquipmentSlot.Legs }, "torso", false),
                new("left_foot",  BodyPartType.LeftFoot,  25, 50,  BodyPartFunction.Movement, new[]{ EquipmentSlot.Feet }, "left_leg", false),
                new("right_foot", BodyPartType.RightFoot, 25, 50,  BodyPartFunction.Movement, new[]{ EquipmentSlot.Feet }, "right_leg", false),
            };
            var hierarchy = new Dictionary<string, string?>
            {
                ["head"] = "torso", ["heart"] = "torso",
                ["left_arm"] = "torso", ["right_arm"] = "torso",
                ["left_leg"] = "torso", ["right_leg"] = "torso",
                ["left_hand"] = "left_arm", ["right_hand"] = "right_arm",
                ["left_foot"] = "left_leg", ["right_foot"] = "right_leg"
            };
            return new("humanoid", Morphology.Humanoid, BodyMaterial.Organic, SizeClass.Medium, VitalityScalingMode.Standard, parts, hierarchy);
        }

        // ===== ЧЕТВЕРОНОГОЕ =====
        // Источник: BODY_SYSTEM.md §"Части тела четвероногих"
        private BodyTemplate CreateQuadrupedTemplate()
        {
            var parts = new List<BodyPartTemplate>
            {
                new("head",           BodyPartType.Head,          50, 100, BodyPartFunction.Sensory | BodyPartFunction.Breathing, Array.Empty<EquipmentSlot>(), "torso", true),
                new("torso",          BodyPartType.Torso,        100, 200, BodyPartFunction.Circulation | BodyPartFunction.Digestion, Array.Empty<EquipmentSlot>(), null, true),
                new("heart",          BodyPartType.Heart,         80, 0,   BodyPartFunction.Circulation, Array.Empty<EquipmentSlot>(), "torso", true),
                new("front_left_leg", BodyPartType.FrontLeftLeg,  50, 100, BodyPartFunction.Movement, Array.Empty<EquipmentSlot>(), "torso", false),
                new("front_right_leg",BodyPartType.FrontRightLeg, 50, 100, BodyPartFunction.Movement, Array.Empty<EquipmentSlot>(), "torso", false),
                new("back_left_leg",  BodyPartType.BackLeftLeg,   50, 100, BodyPartFunction.Movement, Array.Empty<EquipmentSlot>(), "torso", false),
                new("back_right_leg", BodyPartType.BackRightLeg,  50, 100, BodyPartFunction.Movement, Array.Empty<EquipmentSlot>(), "torso", false),
                new("tail",           BodyPartType.Tail,          30, 60,  BodyPartFunction.Balance, Array.Empty<EquipmentSlot>(), "torso", false),
            };
            var hierarchy = new Dictionary<string, string?>
            {
                ["head"] = "torso", ["heart"] = "torso",
                ["front_left_leg"] = "torso", ["front_right_leg"] = "torso",
                ["back_left_leg"] = "torso", ["back_right_leg"] = "torso",
                ["tail"] = "torso"
            };
            return new("quadruped", Morphology.Quadruped, BodyMaterial.Organic, SizeClass.Medium, VitalityScalingMode.Standard, parts, hierarchy);
        }

        // ===== ПТИЦА =====
        // Источник: BODY_SYSTEM.md §"Части тела птиц"
        private BodyTemplate CreateBirdTemplate()
        {
            var parts = new List<BodyPartTemplate>
            {
                new("head",       BodyPartType.Head,      60, 120, BodyPartFunction.Sensory | BodyPartFunction.Breathing, Array.Empty<EquipmentSlot>(), "torso", true),
                new("torso",      BodyPartType.Torso,    120, 240, BodyPartFunction.Circulation | BodyPartFunction.Digestion, Array.Empty<EquipmentSlot>(), null, true),
                new("heart",      BodyPartType.Heart,     96, 0,   BodyPartFunction.Circulation, Array.Empty<EquipmentSlot>(), "torso", true),
                new("left_wing",  BodyPartType.LeftWing,  50, 100, BodyPartFunction.Flight, Array.Empty<EquipmentSlot>(), "torso", false),
                new("right_wing", BodyPartType.RightWing, 50, 100, BodyPartFunction.Flight, Array.Empty<EquipmentSlot>(), "torso", false),
                new("bird_tail",  BodyPartType.BirdTail,  40, 80,  BodyPartFunction.Balance | BodyPartFunction.Flight, Array.Empty<EquipmentSlot>(), "torso", false),
                new("left_leg",   BodyPartType.LeftLeg,   30, 60,  BodyPartFunction.Movement, Array.Empty<EquipmentSlot>(), "torso", false),
                new("right_leg",  BodyPartType.RightLeg,  30, 60,  BodyPartFunction.Movement, Array.Empty<EquipmentSlot>(), "torso", false),
            };
            var hierarchy = new Dictionary<string, string?>
            {
                ["head"] = "torso", ["heart"] = "torso",
                ["left_wing"] = "torso", ["right_wing"] = "torso",
                ["bird_tail"] = "torso",
                ["left_leg"] = "torso", ["right_leg"] = "torso"
            };
            return new("bird", Morphology.Bird, BodyMaterial.Organic, SizeClass.Medium, VitalityScalingMode.Standard, parts, hierarchy);
        }

        // ===== ЗМЕЕПОДОБНОЕ =====
        // Источник: BODY_SYSTEM.md §"Части тела змееподобных"
        private BodyTemplate CreateSerpentineTemplate()
        {
            var parts = new List<BodyPartTemplate>
            {
                new("head",            BodyPartType.Head,           37, 74, BodyPartFunction.Sensory | BodyPartFunction.Breathing, Array.Empty<EquipmentSlot>(), "torso", true),
                new("torso",           BodyPartType.Torso,         75, 150, BodyPartFunction.Circulation | BodyPartFunction.Digestion, Array.Empty<EquipmentSlot>(), null, true),
                new("heart",           BodyPartType.Heart,         60, 0,   BodyPartFunction.Circulation, Array.Empty<EquipmentSlot>(), "torso", true),
                new("body_segment_1",  BodyPartType.BodySegment1,  22, 44,  BodyPartFunction.Movement, Array.Empty<EquipmentSlot>(), "torso", false),
                new("body_segment_2",  BodyPartType.BodySegment2,  22, 44,  BodyPartFunction.Movement, Array.Empty<EquipmentSlot>(), "body_segment_1", false),
                new("serpentine_tail", BodyPartType.SerpentineTail,22, 44,  BodyPartFunction.Movement, Array.Empty<EquipmentSlot>(), "body_segment_2", false),
            };
            var hierarchy = new Dictionary<string, string?>
            {
                ["head"] = "torso", ["heart"] = "torso",
                ["body_segment_1"] = "torso",
                ["body_segment_2"] = "body_segment_1",
                ["serpentine_tail"] = "body_segment_2"
            };
            return new("serpentine", Morphology.Serpentine, BodyMaterial.Scaled, SizeClass.Small, VitalityScalingMode.Standard, parts, hierarchy);
        }

        // ===== ЧЛЕНИСТОНОГОЕ (Паук) =====
        // Источник: BODY_SYSTEM.md §"Части тела членистоногих"
        private BodyTemplate CreateArthropodTemplate()
        {
            var parts = new List<BodyPartTemplate>
            {
                new("cephalothorax", BodyPartType.Cephalothorax, 30, 60, BodyPartFunction.Sensory | BodyPartFunction.Circulation, Array.Empty<EquipmentSlot>(), null, true),
                new("abdomen",       BodyPartType.Abdomen,      50, 100, BodyPartFunction.Digestion, Array.Empty<EquipmentSlot>(), "cephalothorax", false),
                new("heart",         BodyPartType.Heart,        24, 0,   BodyPartFunction.Circulation, Array.Empty<EquipmentSlot>(), "cephalothorax", true),
                new("leg1",  BodyPartType.Leg1,  8, 16, BodyPartFunction.Movement, Array.Empty<EquipmentSlot>(), "cephalothorax", false),
                new("leg2",  BodyPartType.Leg2,  8, 16, BodyPartFunction.Movement, Array.Empty<EquipmentSlot>(), "cephalothorax", false),
                new("leg3",  BodyPartType.Leg3,  8, 16, BodyPartFunction.Movement, Array.Empty<EquipmentSlot>(), "cephalothorax", false),
                new("leg4",  BodyPartType.Leg4,  8, 16, BodyPartFunction.Movement, Array.Empty<EquipmentSlot>(), "cephalothorax", false),
                new("leg5",  BodyPartType.Leg5,  8, 16, BodyPartFunction.Movement, Array.Empty<EquipmentSlot>(), "cephalothorax", false),
                new("leg6",  BodyPartType.Leg6,  8, 16, BodyPartFunction.Movement, Array.Empty<EquipmentSlot>(), "cephalothorax", false),
                new("leg7",  BodyPartType.Leg7,  8, 16, BodyPartFunction.Movement, Array.Empty<EquipmentSlot>(), "cephalothorax", false),
                new("leg8",  BodyPartType.Leg8,  8, 16, BodyPartFunction.Movement, Array.Empty<EquipmentSlot>(), "cephalothorax", false),
                new("pedipalps",   BodyPartType.Pedipalps,   6, 12, BodyPartFunction.Manipulation | BodyPartFunction.WebProduction, Array.Empty<EquipmentSlot>(), "cephalothorax", false),
                new("chelicerae",  BodyPartType.Chelicerae, 10, 20, BodyPartFunction.Venom, Array.Empty<EquipmentSlot>(), "cephalothorax", false),
            };
            var hierarchy = new Dictionary<string, string?>
            {
                ["abdomen"] = "cephalothorax", ["heart"] = "cephalothorax",
                ["leg1"] = "cephalothorax", ["leg2"] = "cephalothorax",
                ["leg3"] = "cephalothorax", ["leg4"] = "cephalothorax",
                ["leg5"] = "cephalothorax", ["leg6"] = "cephalothorax",
                ["leg7"] = "cephalothorax", ["leg8"] = "cephalothorax",
                ["pedipalps"] = "cephalothorax", ["chelicerae"] = "cephalothorax"
            };
            return new("arthropod", Morphology.Arthropod, BodyMaterial.Chitin, SizeClass.Tiny, VitalityScalingMode.Standard, parts, hierarchy);
        }

        // ===== БЕСФОРМЕННОЕ (Дух) =====
        // Источник: BODY_SYSTEM.md §"Части тела духов"
        private BodyTemplate CreateAmorphousTemplate()
        {
            var parts = new List<BodyPartTemplate>
            {
                new("core",    BodyPartType.Core,    100, 0, BodyPartFunction.Sensory | BodyPartFunction.Circulation, Array.Empty<EquipmentSlot>(), null, true, vitalityScalesHP: false),
                new("essence", BodyPartType.Essence, 200, 0, BodyPartFunction.None, Array.Empty<EquipmentSlot>(), "core", false, vitalityScalesHP: false),
            };
            var hierarchy = new Dictionary<string, string?>
            {
                ["essence"] = "core"
            };
            return new("amorphous", Morphology.Amorphous, BodyMaterial.Ethereal, SizeClass.Medium, VitalityScalingMode.Amorphous, parts, hierarchy);
        }

        // ===== КЕНТАВР (P1-02 FIX) =====
        // Гибрид: верх гуманоид, низ четвероногое
        private BodyTemplate CreateHybridCentaurTemplate()
        {
            var parts = new List<BodyPartTemplate>
            {
                new("head",           BodyPartType.Head,          50, 100, BodyPartFunction.Sensory | BodyPartFunction.Breathing, new[]{ EquipmentSlot.Head }, "torso", true),
                new("torso",          BodyPartType.Torso,        120, 240, BodyPartFunction.Circulation | BodyPartFunction.Digestion, new[]{ EquipmentSlot.Torso }, null, true),
                new("heart",          BodyPartType.Heart,         80, 0,   BodyPartFunction.Circulation, Array.Empty<EquipmentSlot>(), "torso", true),
                new("left_arm",       BodyPartType.LeftArm,       40, 80,  BodyPartFunction.Manipulation, new[]{ EquipmentSlot.WeaponOff }, "torso", false),
                new("right_arm",      BodyPartType.RightArm,      40, 80,  BodyPartFunction.Manipulation, new[]{ EquipmentSlot.WeaponMain }, "torso", false),
                new("front_left_leg", BodyPartType.FrontLeftLeg,  60, 120, BodyPartFunction.Movement, Array.Empty<EquipmentSlot>(), "torso", false),
                new("front_right_leg",BodyPartType.FrontRightLeg, 60, 120, BodyPartFunction.Movement, Array.Empty<EquipmentSlot>(), "torso", false),
                new("back_left_leg",  BodyPartType.BackLeftLeg,   60, 120, BodyPartFunction.Movement, new[]{ EquipmentSlot.Legs }, "torso", false),
                new("back_right_leg", BodyPartType.BackRightLeg,  60, 120, BodyPartFunction.Movement, new[]{ EquipmentSlot.Legs }, "torso", false),
                new("tail",           BodyPartType.Tail,          30, 60,  BodyPartFunction.Balance, Array.Empty<EquipmentSlot>(), "torso", false),
            };
            var hierarchy = new Dictionary<string, string?>
            {
                ["head"] = "torso", ["heart"] = "torso",
                ["left_arm"] = "torso", ["right_arm"] = "torso",
                ["front_left_leg"] = "torso", ["front_right_leg"] = "torso",
                ["back_left_leg"] = "torso", ["back_right_leg"] = "torso",
                ["tail"] = "torso"
            };
            return new("hybrid_centaur", Morphology.HybridCentaur, BodyMaterial.Organic, SizeClass.Large, VitalityScalingMode.Standard, parts, hierarchy);
        }

        // ===== РУСАЛКА (P1-02 FIX) =====
        // Гибрид: верх гуманоид, низ змеиный хвост
        private BodyTemplate CreateHybridMermaidTemplate()
        {
            var parts = new List<BodyPartTemplate>
            {
                new("head",       BodyPartType.Head,           50, 100, BodyPartFunction.Sensory | BodyPartFunction.Breathing, new[]{ EquipmentSlot.Head }, "torso", true),
                new("torso",      BodyPartType.Torso,         100, 200, BodyPartFunction.Circulation | BodyPartFunction.Digestion, new[]{ EquipmentSlot.Torso, EquipmentSlot.Belt }, null, true),
                new("heart",      BodyPartType.Heart,          80, 0,   BodyPartFunction.Circulation, Array.Empty<EquipmentSlot>(), "torso", true),
                new("left_arm",   BodyPartType.LeftArm,        40, 80,  BodyPartFunction.Manipulation, new[]{ EquipmentSlot.WeaponOff }, "torso", false),
                new("right_arm",  BodyPartType.RightArm,       40, 80,  BodyPartFunction.Manipulation, new[]{ EquipmentSlot.WeaponMain }, "torso", false),
                new("serpentine_tail", BodyPartType.SerpentineTail, 100, 200, BodyPartFunction.Movement, Array.Empty<EquipmentSlot>(), "torso", false),
            };
            var hierarchy = new Dictionary<string, string?>
            {
                ["head"] = "torso", ["heart"] = "torso",
                ["left_arm"] = "torso", ["right_arm"] = "torso",
                ["serpentine_tail"] = "torso"
            };
            return new("hybrid_mermaid", Morphology.HybridMermaid, BodyMaterial.Organic, SizeClass.Medium, VitalityScalingMode.Standard, parts, hierarchy);
        }

        // ===== ГАРПИЯ (P1-02 FIX) =====
        // Гибрид: гуманоид + крылья вместо рук
        private BodyTemplate CreateHybridHarpyTemplate()
        {
            var parts = new List<BodyPartTemplate>
            {
                new("head",       BodyPartType.Head,       50, 100, BodyPartFunction.Sensory | BodyPartFunction.Breathing, new[]{ EquipmentSlot.Head }, "torso", true),
                new("torso",      BodyPartType.Torso,     100, 200, BodyPartFunction.Circulation | BodyPartFunction.Digestion, new[]{ EquipmentSlot.Torso }, null, true),
                new("heart",      BodyPartType.Heart,      80, 0,   BodyPartFunction.Circulation, Array.Empty<EquipmentSlot>(), "torso", true),
                new("left_wing",  BodyPartType.LeftWing,   35, 70,  BodyPartFunction.Flight | BodyPartFunction.Manipulation, new[]{ EquipmentSlot.WeaponOff }, "torso", false),
                new("right_wing", BodyPartType.RightWing,  35, 70,  BodyPartFunction.Flight | BodyPartFunction.Manipulation, new[]{ EquipmentSlot.WeaponMain }, "torso", false),
                new("left_leg",   BodyPartType.LeftLeg,    50, 100, BodyPartFunction.Movement, new[]{ EquipmentSlot.Legs }, "torso", false),
                new("right_leg",  BodyPartType.RightLeg,   50, 100, BodyPartFunction.Movement, new[]{ EquipmentSlot.Legs }, "torso", false),
                new("bird_tail",  BodyPartType.BirdTail,   40, 80,  BodyPartFunction.Balance | BodyPartFunction.Flight, Array.Empty<EquipmentSlot>(), "torso", false),
            };
            var hierarchy = new Dictionary<string, string?>
            {
                ["head"] = "torso", ["heart"] = "torso",
                ["left_wing"] = "torso", ["right_wing"] = "torso",
                ["left_leg"] = "torso", ["right_leg"] = "torso",
                ["bird_tail"] = "torso"
            };
            return new("hybrid_harpy", Morphology.HybridHarpy, BodyMaterial.Organic, SizeClass.Medium, VitalityScalingMode.Standard, parts, hierarchy);
        }

        // ===== ЛАМИЯ (P1-02 FIX) =====
        // Гибрид: верх гуманоид + змеиный хвост вместо ног
        private BodyTemplate CreateHybridLamiaTemplate()
        {
            var parts = new List<BodyPartTemplate>
            {
                new("head",       BodyPartType.Head,           50, 100, BodyPartFunction.Sensory | BodyPartFunction.Breathing, new[]{ EquipmentSlot.Head }, "torso", true),
                new("torso",      BodyPartType.Torso,         120, 240, BodyPartFunction.Circulation | BodyPartFunction.Digestion, new[]{ EquipmentSlot.Torso, EquipmentSlot.Belt }, null, true),
                new("heart",      BodyPartType.Heart,          80, 0,   BodyPartFunction.Circulation, Array.Empty<EquipmentSlot>(), "torso", true),
                new("left_arm",   BodyPartType.LeftArm,        40, 80,  BodyPartFunction.Manipulation, new[]{ EquipmentSlot.WeaponOff }, "torso", false),
                new("right_arm",  BodyPartType.RightArm,       40, 80,  BodyPartFunction.Manipulation, new[]{ EquipmentSlot.WeaponMain }, "torso", false),
                new("body_segment_1", BodyPartType.BodySegment1, 80, 160, BodyPartFunction.Movement, Array.Empty<EquipmentSlot>(), "torso", false),
                new("serpentine_tail", BodyPartType.SerpentineTail, 60, 120, BodyPartFunction.Movement, Array.Empty<EquipmentSlot>(), "body_segment_1", false),
            };
            var hierarchy = new Dictionary<string, string?>
            {
                ["head"] = "torso", ["heart"] = "torso",
                ["left_arm"] = "torso", ["right_arm"] = "torso",
                ["body_segment_1"] = "torso",
                ["serpentine_tail"] = "body_segment_1"
            };
            return new("hybrid_lamia", Morphology.HybridLamia, BodyMaterial.Organic, SizeClass.Large, VitalityScalingMode.Standard, parts, hierarchy);
        }
    }
}

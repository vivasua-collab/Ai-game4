#nullable enable
using System.Collections.Generic;
// Этап 4 внедрения ЦИ (2026-08-23): FormationGenerator — процедурная генерация
// формаций по GENERATORS_SYSTEM.md §9 + FORMATION_SYSTEM.md.
// Слои «Матрёшка»: Тип (8) × Размер (5) × Уровень (1-9) × Стихия × Форма контура.
// Все расчёты contourQi/capacity/drain — FormationCalculator (единственный
// источник формул). Имена — без родовых конфликтов: «Барьер Огня · Звезда · L3».
using System;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Modules.Formation;

namespace CultivationGame.Modules.Generator
{
    /// <summary>
    /// Генератор формаций. Детерминирован (SeededRandom).
    /// Регистрирует результат в FormationRegistry.
    /// </summary>
    public sealed class FormationGeneratorService : IFormationGeneratorService
    {
        private readonly FormationRegistry _registry;

        public FormationGeneratorService(FormationRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        // === Названия (родительный падеж — без грамматических конфликтов) ===

        private static readonly Dictionary<FormationType, string> TypeNameGenitive = new()
        {
            { FormationType.Barrier,        "Барьер" },
            { FormationType.Trap,           "Ловушка" },
            { FormationType.Amplification,  "Круг усиления" },
            { FormationType.Suppression,    "Печать подавления" },
            { FormationType.Gathering,      "Воронка сбора" },
            { FormationType.Detection,      "Сеть обнаружения" },
            { FormationType.Teleportation,  "Врата" },
            { FormationType.Summoning,      "Алтарь призыва" }
        };

        private static readonly Dictionary<Element, string> ElementGenitive = new()
        {
            { Element.Fire,      "Огня" },
            { Element.Water,     "Воды" },
            { Element.Earth,     "Земли" },
            { Element.Air,       "Воздуха" },
            { Element.Lightning, "Молнии" },
            { Element.Void,      "Пустоты" },
            { Element.Light,     "Света" },
            { Element.Neutral,   "Чистого Ци" }
        };

        private static readonly Dictionary<FormationShape, string> ShapeName = new()
        {
            { FormationShape.Circle,    "Круг" },
            { FormationShape.Triangle,  "Треугольник" },
            { FormationShape.Square,    "Квадрат" },
            { FormationShape.Pentagon,  "Пятиугольник" },
            { FormationShape.Star,      "Звезда" },
            { FormationShape.Hexagram,  "Гексаграмма" }
        };

        private static readonly Dictionary<FormationSize, string> SizeName = new()
        {
            { FormationSize.Small,  "Малая" },
            { FormationSize.Medium, "Средняя" },
            { FormationSize.Large,  "Большая" },
            { FormationSize.Great,  "Великая" },
            { FormationSize.Heavy,  "Тяжёлая" }
        };

        /// <summary>Радиус действия по размеру, м (FORMATION_SYSTEM.md §4).</summary>
        private static readonly Dictionary<FormationSize, int> EffectRadiusBySize = new()
        {
            { FormationSize.Small,  50 },
            { FormationSize.Medium, 200 },
            { FormationSize.Large,  600 },
            { FormationSize.Great,  1000 },
            { FormationSize.Heavy,  5000 }
        };

        private static readonly FormationType[] AllTypes =
        {
            FormationType.Barrier, FormationType.Trap, FormationType.Amplification,
            FormationType.Suppression, FormationType.Gathering, FormationType.Detection,
            FormationType.Teleportation, FormationType.Summoning
        };

        // Размеры, взвешенные к малым (на тестовой карте 50×50 большие бессмысленны,
        // но генератор честно их выдаёт — вес убывает).
        private static readonly FormationSize[] SizePool =
        {
            FormationSize.Small, FormationSize.Small, FormationSize.Small, FormationSize.Small,
            FormationSize.Medium, FormationSize.Medium, FormationSize.Medium,
            FormationSize.Large, FormationSize.Large,
            FormationSize.Great
        };

        private static readonly FormationShape[] AllShapes =
        {
            FormationShape.Circle, FormationShape.Triangle, FormationShape.Square,
            FormationShape.Pentagon, FormationShape.Star, FormationShape.Hexagram
        };

        private static readonly Element[] AllElements =
        {
            Element.Fire, Element.Water, Element.Earth, Element.Air,
            Element.Lightning, Element.Void, Element.Light, Element.Neutral
        };

        // === IFormationGeneratorService ===

        public FormationData Generate(int level, long seed)
        {
            var rng = new SeededRandom(seed);
            var type = rng.NextElement(AllTypes);
            var size = rng.NextElement(SizePool);
            // Heavy — только L6+ (замена выпавшего Heavy на Medium при низком уровне).
            if (size == FormationSize.Heavy && level < GameConstants.HEAVY_FORMATION_MIN_LEVEL)
                size = FormationSize.Medium;
            return GenerateSpecified(type, size, level, seed);
        }

        public FormationData GenerateSpecified(FormationType type, FormationSize size, int level, long seed)
        {
            if (level < 1) level = 1;
            if (level > 9) level = 9;
            if (size == FormationSize.Heavy && level < GameConstants.HEAVY_FORMATION_MIN_LEVEL)
                size = FormationSize.Medium;

            var rng = new SeededRandom(seed);
            var shape = rng.NextElement(AllShapes);
            var element = type == FormationType.Gathering || type == FormationType.Teleportation
                ? Element.Neutral // сбор Ци и телепортация — чистое Ци
                : rng.NextElement(AllElements);

            var data = new FormationData
            {
                Id = $"form_{type}_{size}_{element}_{shape}_L{level}_{(seed ^ (seed >> 16)) & 0xFFFF:X4}",
                FormationType = type,
                Size = size,
                Shape = shape,
                RequiredLevel = level,
                Element = element,
                EffectRadiusMeters = EffectRadiusBySize.TryGetValue(size, out var r) ? r : 50,
                IsReusable = false, // Вариант А: одноразовая (без физического ядра)
                CoreType = FormationCoreType.Array,
                DisplayName = $"{TypeNameGenitive[type]} {ElementGenitive[element]} · {ShapeName[shape]} · L{level} ({SizeName[size]})"
            };

            BuildEffects(data, rng);
            _registry.Register(data);
            return data;
        }

        /// <summary>Эффекты по типу (FORMATION_SYSTEM.md §12 примеры, значения масштабируются уровнем).</summary>
        private static void BuildEffects(FormationData data, SeededRandom rng)
        {
            int l = data.RequiredLevel;
            switch (data.FormationType)
            {
                case FormationType.Barrier:
                    data.Effects.Add(new FormationEffectEntry
                    {
                        EffectType = FormationEffectType.Shield,
                        TargetStat = StatType.Defense,
                        Value = MathF.Min(0.6f, 0.2f + 0.05f * l),
                        TargetTag = "ally"
                    });
                    break;

                case FormationType.Trap:
                    data.Effects.Add(new FormationEffectEntry
                    {
                        EffectType = FormationEffectType.Control,
                        ControlType = rng.NextBool(0.4f) ? ControlType.Freeze : ControlType.Slow,
                        TargetStat = StatType.Speed,
                        Value = MathF.Min(0.8f, 0.3f + 0.03f * l),
                        TargetTag = "enemy"
                    });
                    break;

                case FormationType.Amplification:
                    data.Effects.Add(new FormationEffectEntry
                    {
                        EffectType = FormationEffectType.Buff,
                        TargetStat = StatType.Damage,
                        Value = MathF.Min(0.6f, 0.2f + 0.02f * l),
                        TargetTag = "ally"
                    });
                    break;

                case FormationType.Suppression:
                    data.Effects.Add(new FormationEffectEntry
                    {
                        EffectType = FormationEffectType.Debuff,
                        TargetStat = StatType.Speed,
                        Value = MathF.Min(0.8f, 0.3f + 0.03f * l),
                        TargetTag = "enemy"
                    });
                    break;

                case FormationType.Gathering:
                    // Сбор Ци: +environmentMult в зоне (сервис меди-тации читает
                    // активные Gathering-формации). Эффект-запись — для UI.
                    data.Effects.Add(new FormationEffectEntry
                    {
                        EffectType = FormationEffectType.Buff,
                        TargetStat = StatType.Conductivity,
                        Value = 1.0f, // ×2 к поглощению (EnvironmentMult Богатая Ци)
                        TargetTag = "ally"
                    });
                    break;

                case FormationType.Detection:
                    data.Effects.Add(new FormationEffectEntry
                    {
                        EffectType = FormationEffectType.Buff,
                        TargetStat = StatType.Intelligence,
                        Value = 0.1f,
                        TargetTag = "ally"
                    });
                    break;

                case FormationType.Teleportation:
                    data.Effects.Add(new FormationEffectEntry
                    {
                        EffectType = FormationEffectType.Control,
                        ControlType = ControlType.Root,
                        TargetStat = StatType.Speed,
                        Value = 0.2f,
                        TargetTag = "enemy"
                    });
                    break;

                case FormationType.Summoning:
                    data.Effects.Add(new FormationEffectEntry
                    {
                        EffectType = FormationEffectType.Summon,
                        TargetStat = StatType.Damage,
                        Value = MathF.Min(0.5f, 0.1f + 0.04f * l),
                        TargetTag = "ally"
                    });
                    break;
            }
        }
    }
}

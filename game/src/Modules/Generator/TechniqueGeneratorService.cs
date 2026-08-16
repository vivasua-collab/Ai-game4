#nullable enable
// Создано: 2026-05-20 18:18 UTC
// Редактировано: 2026-05-24 05:45:00 UTC — FIX CS0266: BaseDamage float→int (ЗАПРЕТ 3.9)
// Фаза 2.1: генератор техник NPC (Шаг 6 пайплайна)
// Источник: docs/NPC_ASSEMBLY_PIPELINE.md §6, TECHNIQUE_SYSTEM.md
//
// Ключевые правила (ПРОТИВОРЕЧИЯ с Legacy):
// - Grade НЕ влияет на стоимость Ци! qiCost = capacity × 0.15 ВСЕГДА ×1.0
// - Grade множители из ДОКУМЕНТАЦИИ: {1.0, 1.3, 1.6, 2.0} — НЕ Legacy {1.0, 1.2, 1.4, 1.6}
// - Ultimate damage ×2.0 (НЕ ×1.3!)
// - Ultimate qiCost ×1.5
using System;
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Modules.NPC;

namespace CultivationGame.Modules.Generator
{
    /// <summary>
    /// Генератор техник NPC (Шаг 6 пайплайна).
    /// Создаёт TechniqueData на основе уровня культивации, роли и seed.
    ///
    /// Алгоритм детерминирован: одинаковый seed → одинаковый результат.
    /// Все формулы из документации (NPC_ASSEMBLY_PIPELINE.md, TECHNIQUE_SYSTEM.md).
    ///
    /// КРИТИЧЕСКИЕ ОТЛИЧИЯ от Legacy:
    /// - Grade множители: {1.0, 1.3, 1.6, 2.0} (НЕ {1.0, 1.2, 1.4, 1.6})
    /// - QiCost НЕ зависит от Grade! Всегда capacity × 0.15
    /// - Ultimate damage ×2.0 (НЕ ×1.3)
    /// </summary>
    public sealed class TechniqueGeneratorService : ITechniqueGeneratorService
    {
        private readonly NPCConfig _config;
        private readonly TechniqueRegistry _registry;

        // ===================================================================
        // Таблицы типа техники по роли
        // ===================================================================

        /// <summary>Доступные типы техник по роли NPC</summary>
        private static readonly Dictionary<NPCRole, TechniqueType[]> RoleTypeMap = new()
        {
            { NPCRole.Monster,    new[] { TechniqueType.Combat, TechniqueType.Poison } },
            { NPCRole.Guard,      new[] { TechniqueType.Combat, TechniqueType.Defense } },
            { NPCRole.Merchant,   new[] { TechniqueType.Support, TechniqueType.Healing } },
            { NPCRole.Cultivator, new[] { TechniqueType.Combat, TechniqueType.Support, TechniqueType.Cultivation } },
            { NPCRole.Elder,      new[] { TechniqueType.Combat, TechniqueType.Cultivation, TechniqueType.Defense,
                                          TechniqueType.Support, TechniqueType.Healing, TechniqueType.Movement,
                                          TechniqueType.Sensory, TechniqueType.Curse, TechniqueType.Poison, TechniqueType.Formation } },
            { NPCRole.Enemy,      new[] { TechniqueType.Combat, TechniqueType.Curse } },
            { NPCRole.Disciple,   new[] { TechniqueType.Combat, TechniqueType.Support } },
            { NPCRole.Passerby,   new[] { TechniqueType.Support, TechniqueType.Healing } }
        };

        // ===================================================================
        // Таблицы подтипов по типу техники
        // ===================================================================

        /// <summary>Подтипы для Combat-техник</summary>
        private static readonly CombatSubtype[] CombatSubtypes =
        {
            CombatSubtype.MeleeStrike,
            CombatSubtype.MeleeWeapon,
            CombatSubtype.RangedProjectile,
            CombatSubtype.RangedBeam
        };

        /// <summary>Подтипы для Defense-техник</summary>
        private static readonly CombatSubtype[] DefenseSubtypes =
        {
            CombatSubtype.DefenseBlock,
            CombatSubtype.DefenseShield,
            CombatSubtype.DefenseDodge
        };

        // ===================================================================
        // Таблицы кулдаунов по типу (секунды)
        // ===================================================================

        private static readonly Dictionary<TechniqueType, float> BaseCooldownByType = new()
        {
            { TechniqueType.Combat,      3f },
            { TechniqueType.Defense,     5f },
            { TechniqueType.Support,     8f },
            { TechniqueType.Healing,    10f },
            { TechniqueType.Cultivation, 30f },
            { TechniqueType.Movement,   15f },
            { TechniqueType.Poison,      6f },
            { TechniqueType.Curse,       8f },
            { TechniqueType.Sensory,     5f },
            { TechniqueType.Formation,  20f }
        };

        // ===================================================================
        // Таблицы дальности по подтипу (метры)
        // ===================================================================

        private static readonly Dictionary<CombatSubtype, float> BaseRangeBySubtype = new()
        {
            { CombatSubtype.None,             5f },
            { CombatSubtype.MeleeStrike,      1.5f },
            { CombatSubtype.MeleeWeapon,      2.0f },
            { CombatSubtype.RangedProjectile, 15f },
            { CombatSubtype.RangedBeam,       20f },
            { CombatSubtype.RangedAoe,        10f },
            { CombatSubtype.DefenseBlock,     1.5f },
            { CombatSubtype.DefenseShield,    0f },
            { CombatSubtype.DefenseDodge,     3f }
        };

        // ===================================================================
        // Таблицы времени каста по подтипу (секунды)
        // ===================================================================

        private static readonly Dictionary<CombatSubtype, float> BaseCastTimeBySubtype = new()
        {
            { CombatSubtype.None,             1.0f },
            { CombatSubtype.MeleeStrike,      0.5f },
            { CombatSubtype.MeleeWeapon,      0.8f },
            { CombatSubtype.RangedProjectile, 1.0f },
            { CombatSubtype.RangedBeam,       1.5f },
            { CombatSubtype.RangedAoe,        2.0f },
            { CombatSubtype.DefenseBlock,     0.3f },
            { CombatSubtype.DefenseShield,    1.0f },
            { CombatSubtype.DefenseDodge,     0.2f }
        };

        // ===================================================================
        // Стихии для рандома (кроме Neutral и Poison)
        // ===================================================================

        private static readonly Element[] CombatElements =
        {
            Element.Fire, Element.Water, Element.Earth, Element.Air,
            Element.Lightning, Element.Void, Element.Light, Element.Poison
        };

        // ===================================================================
        // Русские названия типов техник (для генерации имён)
        // ===================================================================

        private static readonly Dictionary<TechniqueType, string> TypeNameRu = new()
        {
            { TechniqueType.Combat,      "Боевая" },
            { TechniqueType.Defense,     "Защитная" },
            { TechniqueType.Support,     "Поддержка" },
            { TechniqueType.Healing,     "Исцеление" },
            { TechniqueType.Cultivation, "Культивация" },
            { TechniqueType.Movement,    "Перемещение" },
            { TechniqueType.Sensory,     "Восприятие" },
            { TechniqueType.Curse,       "Проклятие" },
            { TechniqueType.Poison,      "Яд" },
            { TechniqueType.Formation,   "Формация" }
        };

        private static readonly Dictionary<TechniqueGrade, string> GradeNameRu = new()
        {
            { TechniqueGrade.Common,       "" },
            { TechniqueGrade.Refined,      "Очищенная " },
            { TechniqueGrade.Perfect,      "Совершенная " },
            { TechniqueGrade.Transcendent, "Трансцендентная " }
        };

        private static readonly Dictionary<Element, string> ElementNameRu = new()
        {
            { Element.Neutral,   "Нейтральная" },
            { Element.Fire,      "Огненная" },
            { Element.Water,     "Водяная" },
            { Element.Earth,     "Земляная" },
            { Element.Air,       "Воздушная" },
            { Element.Lightning, "Молниевая" },
            { Element.Void,      "Пустотная" },
            { Element.Light,     "Светлая" },
            { Element.Poison,    "Ядовитая" }
        };

        private static readonly Dictionary<Element, string> ElementNameEn = new()
        {
            { Element.Neutral,   "Neutral" },
            { Element.Fire,      "Fire" },
            { Element.Water,     "Water" },
            { Element.Earth,     "Earth" },
            { Element.Air,       "Air" },
            { Element.Lightning, "Lightning" },
            { Element.Void,      "Void" },
            { Element.Light,     "Light" },
            { Element.Poison,    "Poison" }
        };

        // ===================================================================
        // Конструктор
        // ===================================================================

        /// <summary>
        /// Конструктор TechniqueGeneratorService.
        /// </summary>
        /// <param name="config">Конфигурация NPC (содержит TechniqueGradeWeights и TechniqueGradeMultipliers)</param>
        /// <param name="registry">Реестр техник (для регистрации сгенерированных техник)</param>
        public TechniqueGeneratorService(NPCConfig config, TechniqueRegistry registry)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        // ===================================================================
        // Публичные методы (ITechniqueGeneratorService)
        // ===================================================================

        /// <summary>
        /// Сгенерировать одну технику.
        /// Алгоритм детерминирован: одинаковый seed → одинаковый результат.
        /// </summary>
        public TechniqueData Generate(int cultivationLevel, NPCRole roleId, long seed)
        {
            // Валидация: уровень культивации должен быть ≥ 1
            if (cultivationLevel < 1)
                cultivationLevel = 1;
            if (cultivationLevel > GameConstants.MAX_CULTIVATION_LEVEL)
                cultivationLevel = GameConstants.MAX_CULTIVATION_LEVEL;

            var rng = new SeededRandom(seed);

            // === Шаг 6.1: Выбор типа техники по роли ===
            TechniqueType techniqueType = DetermineType(roleId, rng);

            // === Шаг 6.2: Выбор подтипа ===
            CombatSubtype subtype = DetermineSubtype(techniqueType, rng);

            // === Шаг 6.3: Уровень техники ===
            int level = rng.Next(1, cultivationLevel + 1);

            // === Шаг 6.4: Грейд техники (взвешенный рандом) ===
            TechniqueGrade grade = DetermineGrade(rng);

            // === Шаг 6.5: Стихия ===
            Element element = DetermineElement(techniqueType, rng);

            // === Шаг 6.6: Мастерство (0..100) ===
            float mastery = rng.NextFloat() * 100f;

            // === Шаг 6.7: Расчёт параметров ===
            int baseCapacity = GetBaseCapacity(techniqueType);
            int capacity = CalculateCapacity(baseCapacity, level, mastery);
            float gradeMultiplier = GetGradeMultiplier(grade);
            long qiCost = CalculateQiCost(capacity);
            float baseDamage = CalculateBaseDamage(capacity, gradeMultiplier);
            float cooldown = GetBaseCooldown(techniqueType);
            float range = GetBaseRange(subtype);
            float castTime = GetBaseCastTime(subtype);

            // === Шаг 6.8: Ultimate (5% шанс для Transcendent) ===
            bool isUltimate = grade == TechniqueGrade.Transcendent && rng.NextBool(0.05f);
            if (isUltimate)
            {
                baseDamage *= GameConstants.ULTIMATE_DAMAGE_MULTIPLIER; // ×2.0, НЕ ×1.3!
                qiCost = (long)(qiCost * GameConstants.ULTIMATE_QI_COST_MULTIPLIER); // ×1.5 стоимость Ци для Ultimate
            }

            // === Шаг 6.9: Сборка TechniqueData ===
            var technique = new TechniqueData
            {
                TechniqueId = GenerateTechniqueId(techniqueType, element, grade, level, seed),
                NameRu = GenerateNameRu(techniqueType, element, grade, level, isUltimate),
                NameEn = GenerateNameEn(techniqueType, element, grade, level, isUltimate),
                Description = GenerateDescription(techniqueType, element, grade, level, isUltimate),

                // Классификация
                Type = techniqueType,
                Subtype = subtype,
                Grade = grade,
                Element = element,

                // Уровень и мощь
                Level = level,
                CapacityCost = capacity,
                QiCost = qiCost,
                BaseDamage = (int)baseDamage, // FIX CS0266: cast float→int (ЗАПРЕТ 3.9)
                Cooldown = cooldown,
                Range = range,
                CastTime = castTime,

                // Эффекты
                IsUltimate = isUltimate,
                UltimateDamageMultiplier = isUltimate ? 2.0f : 1.0f,
                UltimateQiCostMultiplier = isUltimate ? 1.5f : 1.0f,

                // Мастерство
                Mastery = mastery
            };

            // === Шаг 6.10: Регистрация в TechniqueRegistry ===
            _registry.Register(technique);

            return technique;
        }

        /// <summary>
        /// Сгенерировать несколько техник.
        /// Каждая техника получает уникальный seed = baseSeed + i.
        /// </summary>
        public List<TechniqueData> GenerateMultiple(int cultivationLevel, NPCRole roleId, int count, long seed)
        {
            if (count <= 0)
                return new List<TechniqueData>();

            var results = new List<TechniqueData>(count);
            for (int i = 0; i < count; i++)
            {
                // Каждая техника со своим seed для детерминизма
                long techniqueSeed = seed + i;
                var technique = Generate(cultivationLevel, roleId, techniqueSeed);
                results.Add(technique);
            }
            return results;
        }

        // ===================================================================
        // Шаг 6.1: Определение типа техники по роли
        // ===================================================================

        /// <summary>
        /// Выбрать тип техники на основе роли NPC.
        /// Каждая роль имеет набор доступных типов — выбирается случайно.
        /// Elder имеет доступ ко ВСЕМ типам.
        /// </summary>
        private TechniqueType DetermineType(NPCRole roleId, SeededRandom rng)
        {
            if (RoleTypeMap.TryGetValue(roleId, out var types) && types.Length > 0)
                return rng.NextElement(types);

            // Fallback: Combat для неизвестных ролей
            return TechniqueType.Combat;
        }

        // ===================================================================
        // Шаг 6.2: Определение подтипа
        // ===================================================================

        /// <summary>
        /// Выбрать CombatSubtype на основе TechniqueType.
        /// Combat → MeleeStrike, MeleeWeapon, RangedProjectile, RangedBeam
        /// Defense → DefenseBlock, DefenseShield, DefenseDodge
        /// Support → Healing/Buff → CombatSubtype.None (метаданные в Type)
        /// Остальные → CombatSubtype.None
        /// </summary>
        private CombatSubtype DetermineSubtype(TechniqueType type, SeededRandom rng)
        {
            return type switch
            {
                TechniqueType.Combat => rng.NextElement(CombatSubtypes),
                TechniqueType.Defense => rng.NextElement(DefenseSubtypes),
                // Support/Healing/Buff → None (подтип хранится в Type)
                _ => CombatSubtype.None
            };
        }

        // ===================================================================
        // Шаг 6.4: Определение грейда (взвешенный рандом)
        // ===================================================================

        /// <summary>
        /// Определить грейд техники через взвешенный рандом.
        /// Веса из NPCConfig.TechniqueGradeWeights: {60, 30, 9, 1}
        /// Множители из NPCConfig.TechniqueGradeMultipliers: {1.0, 1.3, 1.6, 2.0}
        /// </summary>
        private TechniqueGrade DetermineGrade(SeededRandom rng)
        {
            int index = rng.NextWeighted(_config.TechniqueGradeWeights);
            // TechniqueGrade: Common=0, Refined=1, Perfect=2, Transcendent=3
            index = Math.Max(0, Math.Min(index, 3));
            return (TechniqueGrade)index;
        }

        // ===================================================================
        // Шаг 6.5: Определение стихии
        // ===================================================================

        /// <summary>
        /// Определить стихию техники по типу:
        /// Healing/Cultivation → Neutral
        /// Poison → Poison только
        /// Остальные → случайная из {Fire, Water, Earth, Air, Lightning, Void, Light, Poison}
        /// </summary>
        private Element DetermineElement(TechniqueType type, SeededRandom rng)
        {
            return type switch
            {
                TechniqueType.Healing => Element.Neutral,
                TechniqueType.Cultivation => Element.Neutral,
                TechniqueType.Poison => Element.Poison,
                _ => rng.NextElement(CombatElements)
            };
        }

        // ===================================================================
        // Шаг 6.7: Расчёт параметров
        // ===================================================================

        /// <summary>
        /// Получить базовую ёмкость по типу техники из GameConstants.
        /// </summary>
        private int GetBaseCapacity(TechniqueType type)
        {
            if (GameConstants.BaseCapacityByType.TryGetValue(type, out int capacity))
                return capacity;
            return 50; // Fallback
        }

        /// <summary>
        /// Рассчитать стоимость ёмкости техники.
        /// Формула: baseCapacity(type) × 2^(level-1) × (1 + mastery × 0.005)
        /// </summary>
        private int CalculateCapacity(int baseCapacity, int level, float mastery)
        {
            // 2^(level-1) — экспоненциальный рост с уровнем
            double levelFactor = Math.Pow(2, level - 1);
            // Мастерство: +0.5% за каждую единицу мастерства
            double masteryFactor = 1.0 + mastery * 0.005;
            return (int)(baseCapacity * levelFactor * masteryFactor);
        }

        /// <summary>
        /// Получить множитель грейда из NPCConfig.
        /// ВНИМАНИЕ: из ДОКУМЕНТАЦИИ {1.0, 1.3, 1.6, 2.0}, НЕ Legacy {1.0, 1.2, 1.4, 1.6}!
        /// </summary>
        private float GetGradeMultiplier(TechniqueGrade grade)
        {
            int index = (int)grade;
            if (index >= 0 && index < _config.TechniqueGradeMultipliers.Length)
                return _config.TechniqueGradeMultipliers[index];
            // Fallback на GameConstants
            if (GameConstants.TechniqueGradeMultipliers.TryGetValue(grade, out float mult))
                return mult;
            return 1.0f;
        }

        /// <summary>
        /// Рассчитать стоимость Ци техники.
        /// Формула: capacity × 0.15
        /// ВНИМАНИЕ: ВСЕГДА ×1.0 по Grade! Grade НЕ влияет на стоимость Ци!
        /// </summary>
        private long CalculateQiCost(int capacity)
        {
            return (long)(capacity * 0.15);
        }

        /// <summary>
        /// Рассчитать базовый урон техники.
        /// Формула: capacity × gradeMultiplier
        /// </summary>
        private float CalculateBaseDamage(int capacity, float gradeMultiplier)
        {
            return capacity * gradeMultiplier;
        }

        /// <summary>
        /// Получить базовый кулдаун по типу техники.
        /// </summary>
        private float GetBaseCooldown(TechniqueType type)
        {
            if (BaseCooldownByType.TryGetValue(type, out float cooldown))
                return cooldown;
            return 5f; // Fallback
        }

        /// <summary>
        /// Получить базовую дальность по подтипу.
        /// </summary>
        private float GetBaseRange(CombatSubtype subtype)
        {
            if (BaseRangeBySubtype.TryGetValue(subtype, out float range))
                return range;
            return 5f; // Fallback
        }

        /// <summary>
        /// Получить базовое время каста по подтипу.
        /// </summary>
        private float GetBaseCastTime(CombatSubtype subtype)
        {
            if (BaseCastTimeBySubtype.TryGetValue(subtype, out float castTime))
                return castTime;
            return 1.0f; // Fallback
        }

        // ===================================================================
        // Генерация идентификаторов и имён
        // ===================================================================

        /// <summary>
        /// Сгенерировать уникальный идентификатор техники.
        /// Формат: tech_{type}_{element}_{grade}_{level}_{seed_hash}
        /// </summary>
        private string GenerateTechniqueId(TechniqueType type, Element element, TechniqueGrade grade, int level, long seed)
        {
            // Хеш seed для короткого идентификатора
            int seedHash = (int)(seed ^ (seed >> 16)) & 0xFFFF;
            return $"tech_{type}_{element}_{grade}_L{level}_{seedHash:X4}";
        }

        /// <summary>
        /// Сгенерировать русское название техники.
        /// Формат: [Грейд] [Стихия] [Тип] [Уровень] [Ultimate]
        /// </summary>
        private string GenerateNameRu(TechniqueType type, Element element, TechniqueGrade grade, int level, bool isUltimate)
        {
            string gradeStr = GradeNameRu.TryGetValue(grade, out var g) ? g : "";
            string elementStr = ElementNameRu.TryGetValue(element, out var e) ? e : "";
            string typeStr = TypeNameRu.TryGetValue(type, out var t) ? t : "Техника";
            string ultimate = isUltimate ? "Предельная " : "";

            return $"{ultimate}{gradeStr}{elementStr} {typeStr} {level}";
        }

        /// <summary>
        /// Сгенерировать английское название техники.
        /// </summary>
        private string GenerateNameEn(TechniqueType type, Element element, TechniqueGrade grade, int level, bool isUltimate)
        {
            string elementStr = ElementNameEn.TryGetValue(element, out var e) ? e : "Unknown";
            string gradeStr = grade.ToString();
            string ultimate = isUltimate ? "Ultimate " : "";

            return $"{ultimate}{gradeStr} {elementStr} {type} Lv{level}";
        }

        /// <summary>
        /// Сгенерировать описание техники.
        /// </summary>
        private string GenerateDescription(TechniqueType type, Element element, TechniqueGrade grade, int level, bool isUltimate)
        {
            string desc = type switch
            {
                TechniqueType.Combat => "Наносит урон противнику",
                TechniqueType.Defense => "Защищает от атак",
                TechniqueType.Support => "Усиливает союзников",
                TechniqueType.Healing => "Восстанавливает здоровье",
                TechniqueType.Cultivation => "Ускоряет культивацию",
                TechniqueType.Movement => "Увеличивает скорость перемещения",
                TechniqueType.Sensory => "Расширяет восприятие",
                TechniqueType.Curse => "Накладывает проклятие на противника",
                TechniqueType.Poison => "Отравляет противника",
                TechniqueType.Formation => "Создаёт формацию",
                _ => "Неизвестная техника"
            };

            if (isUltimate)
                desc += ". Предельная техника — усиленный урон и стоимость";

            return desc;
        }
    }
}

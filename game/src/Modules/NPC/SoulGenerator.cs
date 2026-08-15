#nullable enable
// Создано: 2026-05-20 18:00:11 UTC
// Редактировано: 2026-05-20 18:43:21 UTC — Фаза 3: MaxLifespan формула (3.4), CalculateStats (3.G), AwakeningAge
// Редактировано: 2026-05-22 04:14:49 UTC — Спринт 3 B6: InnateElement из SoulType
// Фаза 1: генератор души NPC (Шаг 1 пайплайна)
// Источник: docs/NPC_ASSEMBLY_PIPELINE.md §1
//
// ПРОТИВОРЕЧИЕ #1: Унифицированные модули — те же формулы, что и для игрока.
// ПРОТИВОРЕЧИЕ #2: AwakeningType НЕ влияет на Conductivity.
// ПРОТИВОРЕЧИЕ #4: Расширенная формула проводимости с levelGrowthFactor.
// ПРОТИВОРЕЧИЕ #5: CurrentQi = CoreCapacity при генерации.
using System;
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Modules.Body;
using CultivationGame.Modules.NPC.Data;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.NPC
{
    /// <summary>
    /// Генератор души NPC (Шаг 1 пайплайна).
    /// Создаёт SoulData на основе входных параметров:
    /// speciesId, roleId, locationLevel, seed.
    ///
    /// Алгоритм детерминирован: одинаковый seed → одинаковый NPC.
    /// Все формулы из документации (NPC_ASSEMBLY_PIPELINE.md, QI_SYSTEM.md, ALGORITHMS.md).
    ///
    /// ПРОТИВОРЕЧИЕ #1: Унифицированные модули — формулы совпадают с QiBreakthroughCalculator.
    /// ПРОТИВОРЕЧИЕ #2: AwakeningType НЕ влияет на проводимость.
    /// ПРОТИВОРЕЧИЕ #4: Расширенная формула ConductivityGrowth с levelGrowthFactor.
    /// ПРОТИВОРЕЧИЕ #5: CurrentQi = CoreCapacity (полное ядро при генерации).
    /// </summary>
    public sealed class SoulGenerator
    {
        private readonly NPCConfig _config;
        private readonly SpeciesRegistry _speciesRegistry;

        /// <summary>
        /// Бонус к максимальной продолжительности жизни по уровню культивации.
        /// L0:0, L1:+20, L2:+50, L3:+100, L4:+200, L5:+400, L6:+800, L7+:+2000
        /// Фаза 3, задача 3.4
        /// </summary>
        private static readonly int[] LifespanLevelBonus = { 0, 20, 50, 100, 200, 400, 800, 2000 };

        /// <summary>
        /// Конструктор SoulGenerator.
        /// SpeciesRegistry — конкретный класс (нет интерфейса ISpeciesRegistry).
        /// </summary>
        public SoulGenerator(NPCConfig config, SpeciesRegistry speciesRegistry)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _speciesRegistry = speciesRegistry ?? throw new ArgumentNullException(nameof(speciesRegistry));
        }

        /// <summary>
        /// Генерация души NPC.
        /// Детерминирована: одинаковый seed → одинаковый результат.
        /// </summary>
        /// <param name="speciesId">Идентификатор вида ("human", "wolf", ...)</param>
        /// <param name="roleId">Роль NPC</param>
        /// <param name="locationLevel">Уровень локации (0-10)</param>
        /// <param name="seed">Seed для детерминированной генерации</param>
        /// <returns>SoulData — результат генерации души</returns>
        public SoulData Generate(string speciesId, NPCRole roleId, int locationLevel, long seed)
        {
            var rng = new SeededRandom(seed);
            var species = _speciesRegistry.GetSpecies(speciesId);
            if (species == null)
                throw new ArgumentException($"Вид не найден: {speciesId}", nameof(speciesId));

            var soul = new SoulData();

            // === Шаг 1.1: Уровень культивации ===
            soul.CultivationLevel = DetermineCultivationLevel(locationLevel, species, rng);
            soul.SubLevel = soul.CultivationLevel != CultivationLevel.None
                ? rng.Next(0, GameConstants.MAX_SUB_LEVEL_VALUE + 1)
                : 0;

            // === Шаг 1.2: Возраст ===
            var (age, awakeningAge) = DetermineAge(soul.CultivationLevel, species, rng);
            soul.Age = age;
            soul.AwakeningAge = awakeningAge;
            soul.MortalStage = DetermineMortalStage(soul.Age, soul.CultivationLevel);

            // === Шаг 1.3: Качество ядра ===
            if (species.SoulType == SoulType.Spirit || species.SoulType == SoulType.Construct)
            {
                // Spirit/Construct — reservoir, не core. Качество не применяется.
                soul.CoreQuality = CoreQuality.Normal;
                soul.QualityMultiplier = 1.0f;
            }
            else
            {
                soul.CoreQuality = DetermineCoreQuality(species.SoulType, rng);
                soul.QualityMultiplier = GetQualityMultiplier(soul.CoreQuality);
            }

            // === Шаг 1.4: Тип пробуждения (ПРОТИВОРЕЧИЕ #2 — НЕ влияет на проводимость) ===
            soul.AwakeningType = DetermineAwakeningType(soul.CultivationLevel, rng);
            // TODO (3.N): AwakeningType бонусы TBD — не влияют на проводимость (ПРОТИВОРЕЧИЕ #2)
            // Будущее: Natural +5% статов, Artifact +50 Qi, Forced +1 черта

            // === Шаг 1.5: Расчёт Ци (расширенная формула — ПРОТИВОРЕЧИЕ #4) ===
            CalculateQi(soul, species);

            // === Шаг 1.6: MaxLifespan (Фаза 3, задача 3.4 — полная формула) ===
            soul.MaxLifespan = DetermineMaxLifespan(species, soul.AwakeningAge, soul.CultivationLevel);

            // === Шаг 1.7: Базовые статы (Фаза 3, задача 3.G) ===
            var stats = CalculateStats(species, (int)soul.CultivationLevel, rng);
            soul.Strength = stats[StatType.Strength];
            soul.Agility = stats[StatType.Agility];
            soul.Vitality = stats[StatType.Vitality];
            soul.Intelligence = stats[StatType.Intelligence];

            // === Шаг 1.8: Врождённая стихия (Спринт 3 B6) ===
            soul.InnateElement = DetermineInnateElement(species, rng);

            return soul;
        }

        // ===================================================================
        // Шаг 1.1: Определение уровня культивации
        // ===================================================================

        /// <summary>
        /// Определить уровень культивации на основе уровня локации и дельты.
        /// Формула: npcLevel = locationLevel + delta(-2..+1)
        /// Кап: npcLevel ≤ locationLevel + LocationLevelCapOffset
        /// </summary>
        private CultivationLevel DetermineCultivationLevel(int locationLevel, SpeciesData species, SeededRandom rng)
        {
            // Дельта: -2(18%), -1(36%), 0(41%), +1(5%)
            int deltaIndex = rng.NextWeighted(_config.LevelDeltaWeights);
            int delta = deltaIndex - 2; // [0,1,2,3] → [-2,-1,0,+1]

            int npcLevel = Math.Max(0, locationLevel + delta);

            // Кап локации: npcLevel ≤ locationLevel + LocationLevelCapOffset
            float cappedLevel = Math.Min(npcLevel, locationLevel + _config.LocationLevelCapOffset);
            npcLevel = (int)cappedLevel;

            // Ограничения по виду:
            // Spirit → L0 (reservoir, не core)
            // Construct → L0-L2
            if (species.SoulType == SoulType.Spirit)
                npcLevel = 0;
            else if (species.SoulType == SoulType.Construct)
                npcLevel = Math.Min(npcLevel, 2);

            // Кламп в допустимый диапазон
            npcLevel = Math.Max(0, Math.Min(npcLevel, GameConstants.MAX_CULTIVATION_LEVEL));

            return (CultivationLevel)npcLevel;
        }

        // ===================================================================
        // Шаг 1.2: Определение возраста
        // ===================================================================

        /// <summary>
        /// Определить возраст NPC на основе уровня культивации и вида.
        /// Смертные (L0): age = random(16, 80), awakeningAge = 0
        /// Практики (L1+): awakeningAge + cultivationYears
        /// Фаза 3, задача 3.4: возвращает кортеж (age, awakeningAge)
        /// </summary>
        private (int age, int awakeningAge) DetermineAge(CultivationLevel level, SpeciesData species, SeededRandom rng)
        {
            if (level == CultivationLevel.None)
            {
                // Смертный: возраст в диапазоне BaseAgeRange или 16-80
                int minAge = Math.Max(16, (int)species.BaseAgeRange.Min);
                int maxAge = Math.Max(minAge + 1, (int)species.BaseAgeRange.Max);
                return (rng.Next(minAge, maxAge), 0);
            }

            // Практик: awakeningAge + cultivationYears
            int awakeningAge = rng.Next(
                GameConstants.OPTIMAL_AWAKENING_AGE_MIN,
                GameConstants.OPTIMAL_AWAKENING_AGE_MAX + 1);

            int lvl = (int)level;
            int cultivationYears = lvl * rng.Next(10, 31) + rng.Next(0, 21);

            return (awakeningAge + cultivationYears, awakeningAge);
        }

        /// <summary>
        /// Определить MortalStage по возрасту и уровню культивации.
        /// Внимание: Adolescent НЕ СУЩЕСТВУЕТ в enum — использовать Adult.
        /// Awakening(9) — для пробуждённых смертных.
        /// </summary>
        private MortalStage DetermineMortalStage(int age, CultivationLevel level)
        {
            if (level != CultivationLevel.None)
                return MortalStage.None; // Практик — не смертный

            if (age < 7) return MortalStage.Newborn;
            if (age < 16) return MortalStage.Child;
            if (age < 50) return MortalStage.Adult;
            if (age < 65) return MortalStage.Mature;
            return MortalStage.Elder;
        }

        // ===================================================================
        // Шаг 1.3: Качество ядра
        // ===================================================================

        /// <summary>
        /// Определить качество ядра через взвешенный рандом.
        /// Character и Creature используют разные таблицы весов.
        /// </summary>
        private CoreQuality DetermineCoreQuality(SoulType soulType, SeededRandom rng)
        {
            float[] weights = soulType == SoulType.Creature
                ? _config.CoreQualityWeightsCreature
                : _config.CoreQualityWeightsCharacter;

            int index = rng.NextWeighted(weights);

            // CoreQuality enum: Fragmented=1..Transcendent=7
            // Индексы весов: 0..6 → CoreQuality = index + 1
            return (CoreQuality)(index + 1);
        }

        /// <summary>
        /// Получить множитель качества ядра из NPCConfig.
        /// ПРОТИВОРЕЧИЕ #1: ЕДИНЫЕ множители для игрока и NPC.
        /// Источник: QiBreakthroughCalculator = {0.5, 0.7, 0.85, 1.0, 1.2, 1.5, 2.0}
        /// </summary>
        private float GetQualityMultiplier(CoreQuality quality)
        {
            int index = (int)quality - 1; // Fragmented=1 → index=0
            if (index < 0 || index >= _config.CoreQualityMultipliers.Length)
                return 1.0f;
            return _config.CoreQualityMultipliers[index];
        }

        // ===================================================================
        // Шаг 1.4: Тип пробуждения
        // ===================================================================

        /// <summary>
        /// Определить тип пробуждения.
        /// L0 → None
        /// L1+ → взвешенный рандом из AwakeningTypeWeights (5 записей).
        /// ПРОТИВОРЕЧИЕ #2: AwakeningType НЕ влияет на проводимость.
        /// </summary>
        private AwakeningType DetermineAwakeningType(CultivationLevel level, SeededRandom rng)
        {
            if (level == CultivationLevel.None)
                return AwakeningType.None;

            // 5 записей: None=0, Natural=20, Guided=50, Artifact=20, Forced=10
            int index = rng.NextWeighted(_config.AwakeningTypeWeights);
            return (AwakeningType)index; // AwakeningType enum: None=0..Forced=4
        }

        // ===================================================================
        // Шаг 1.5: Расчёт Ци (расширенная формула — ПРОТИВОРЕЧИЕ #4)
        // ===================================================================

        /// <summary>
        /// Рассчитать параметры Ци души.
        ///
        /// Формулы:
        /// - totalSubLevels = (level-1) × 10 + subLevel
        /// - coreCapacity = 1000 × 1.1^totalSubLevels × qualityMultiplier
        /// - qiDensity = 2^(level-1)
        /// - conductivityGrowthMultiplier = 1.0 + 0.001 × effectiveAge
        ///   effectiveAge = age × levelGrowthFactor(level)
        /// - conductivity = coreCapacity / 360 × conductivityGrowthMultiplier
        /// - currentQi = coreCapacity (ПРОТИВОРЕЧИЕ #5 — полное ядро)
        ///
        /// ПРОТИВОРЕЧИЕ #4: levelGrowthFactor из ConductivityGrowthFactors.
        /// ПРОТИВОРЕЧИЕ #1: Формулы совпадают с QiBreakthroughCalculator.
        /// </summary>
        private void CalculateQi(SoulData soul, SpeciesData species)
        {
            // Ёмкость ядра
            if (soul.CultivationLevel == CultivationLevel.None)
            {
                // Смертный: нет ядра, Ци = 0
                soul.CoreCapacity = 0;
                soul.CurrentQi = 0;
                soul.QiDensity = 0;
                soul.Conductivity = 0f;
                soul.ConductivityGrowthMultiplier = 1.0f;
                return;
            }

            int level = (int)soul.CultivationLevel;
            int totalSubLevels = (level - 1) * GameConstants.MAX_SUB_LEVEL + soul.SubLevel;

            // coreCapacity = BASE_CORE_CAPACITY × CORE_CAPACITY_GROWTH^totalSubLevels × qualityMultiplier
            double growthFactor = Math.Pow(GameConstants.CORE_CAPACITY_GROWTH, totalSubLevels);
            soul.CoreCapacity = (long)(GameConstants.BASE_CORE_CAPACITY * growthFactor * soul.QualityMultiplier);

            // Плотность Ци = 2^(level-1)
            soul.QiDensity = level > 0 && level <= GameConstants.QiDensityByLevel.Length
                ? GameConstants.QiDensityByLevel[level - 1]
                : (int)Math.Pow(2, level - 1);

            // Проводимость (расширенная формула — ПРОТИВОРЕЧИЕ #4)
            // baseConductivity = coreCapacity / 360
            float baseConductivity = soul.CoreCapacity / 360f;

            // conductivityGrowthMultiplier = 1.0 + 0.001 × effectiveAge
            // effectiveAge = age × levelGrowthFactor(level)
            float levelGrowthFactor = GetLevelGrowthFactor(level);
            float effectiveAge = soul.Age * levelGrowthFactor;
            soul.ConductivityGrowthMultiplier = 1.0f + 0.001f * effectiveAge;

            // Итоговая проводимость
            // Spirit: без возрастного роста (нет физического тела)
            // Construct: без возрастного роста (фиксированные меридианы)
            if (species.SoulType == SoulType.Spirit || species.SoulType == SoulType.Construct)
            {
                soul.Conductivity = baseConductivity;
                soul.ConductivityGrowthMultiplier = 1.0f;
            }
            else
            {
                soul.Conductivity = baseConductivity * soul.ConductivityGrowthMultiplier;
            }

            // Текущее Ци = CoreCapacity (ПРОТИВОРЕЧИЕ #5 — полное ядро при генерации)
            soul.CurrentQi = soul.CoreCapacity;
        }

        /// <summary>
        /// Получить levelGrowthFactor проводимости по уровню.
        /// ПРОТИВОРЕЧИЕ #4: конкретные значения L0..L7+.
        /// Источник: NPCConfig.ConductivityGrowthFactors
        /// </summary>
        private float GetLevelGrowthFactor(int level)
        {
            if (level < 0) return 1.0f;
            if (level < _config.ConductivityGrowthFactors.Length)
                return _config.ConductivityGrowthFactors[level];
            // L7+ — последний элемент массива
            return _config.ConductivityGrowthFactors[_config.ConductivityGrowthFactors.Length - 1];
        }

        // ===================================================================
        // Шаг 1.6: MaxLifespan (Фаза 3, задача 3.4 — полная формула)
        // ===================================================================

        /// <summary>
        /// Определить максимальную продолжительность жизни.
        /// Формула: maxLifespan = baseLifespan(species) + levelBonus(level) - latePenalty(awakeningAge)
        /// levelBonus: L0:0, L1:+20, L2:+50, L3:+100, L4:+200, L5:+400, L6:+800, L7+:+2000
        /// latePenalty: age≤20:0, 20&lt;age≤40:(age-20)×2, age&gt;40: 40+(age-40)×5
        /// </summary>
        private int DetermineMaxLifespan(SpeciesData species, int age, CultivationLevel level)
        {
            // Базовая продолжительность жизни из вида
            int baseLifespan = (int)species.LifespanRange.Max;

            // Бонус по уровню культивации
            int lvl = (int)level;
            int levelBonus = lvl < LifespanLevelBonus.Length
                ? LifespanLevelBonus[lvl]
                : LifespanLevelBonus[LifespanLevelBonus.Length - 1]; // L7+: последний элемент

            // Штраф за позднее пробуждение
            int latePenalty = CalculateLatePenalty(age);

            // Итог: минимум 1 год
            return Math.Max(1, baseLifespan + levelBonus - latePenalty);
        }

        /// <summary>
        /// Рассчитать штраф за позднее пробуждение.
        /// age≤20: 0
        /// 20&lt;age≤40: (age-20)×2
        /// age&gt;40: 40 + (age-40)×5
        /// </summary>
        private static int CalculateLatePenalty(int awakeningAge)
        {
            if (awakeningAge <= 20) return 0;
            if (awakeningAge <= 40) return (awakeningAge - 20) * 2;
            return 40 + (awakeningAge - 40) * 5;
        }

        // ===================================================================
        // Шаг 1.7: Расчёт статов (Фаза 3, задача 3.G)
        // ===================================================================

        /// <summary>
        /// Базовые статы по типу души (SpeciesData может не иметь BaseSTR/BaseAGI/BaseVIT/BaseINT).
        /// Character (human/elf/demon): STR=10, AGI=10, VIT=10, INT=10
        /// Creature: STR=15, AGI=12, VIT=14, INT=5
        /// Spirit: STR=3, AGI=8, VIT=5, INT=20
        /// Construct: STR=20, AGI=5, VIT=25, INT=3
        /// </summary>
        private static readonly Dictionary<SoulType, (int str, int agi, int vit, int intl)> DefaultBaseStats = new()
        {
            { SoulType.Character, (10, 10, 10, 10) },
            { SoulType.Creature,  (15, 12, 14, 5) },
            { SoulType.Spirit,    (3, 8, 5, 20) },
            { SoulType.Construct, (20, 5, 25, 3) },
        };

        /// <summary>
        /// Рассчитать статы NPC на основе вида и уровня.
        /// Формула: stat = baseStat(species) + levelGrowth(level) + randomBonus(rng)
        /// levelGrowth = (level - 1) × 2
        /// randomBonus = Next(-1, 3) // Небольшая вариация
        /// ЗАПРЕТ 3.9: все статы — int, округление при необходимости.
        /// </summary>
        public Dictionary<StatType, int> CalculateStats(SpeciesData species, int level, SeededRandom rng)
        {
            // Получить базовые статы из вида или использовать дефолтные по SoulType
            int baseStr, baseAgi, baseVit, baseIntl;
            if (species.BaseStrength > 0 || species.BaseAgility > 0
                || species.BaseVitality > 0 || species.BaseIntelligence > 0)
            {
                // Вид имеет заданные статы — используем их с округлением
                baseStr = (int)species.BaseStrength;
                baseAgi = (int)species.BaseAgility;
                baseVit = (int)species.BaseVitality;
                baseIntl = (int)species.BaseIntelligence;
            }
            else if (DefaultBaseStats.TryGetValue(species.SoulType, out var defaults))
            {
                // Дефолтные статы по типу души
                baseStr = defaults.str;
                baseAgi = defaults.agi;
                baseVit = defaults.vit;
                baseIntl = defaults.intl;
            }
            else
            {
                // Fallback: Character-подобные статы
                baseStr = 10; baseAgi = 10; baseVit = 10; baseIntl = 10;
            }

            // Рост по уровню: levelGrowth = (level - 1) × 2
            int levelGrowth = Math.Max(0, level - 1) * 2;

            // Случайная вариация: randomBonus = rng.Next(-1, 3)
            int randomBonusStr = rng.Next(-1, 3);
            int randomBonusAgi = rng.Next(-1, 3);
            int randomBonusVit = rng.Next(-1, 3);
            int randomBonusIntl = rng.Next(-1, 3);

            return new Dictionary<StatType, int>
            {
                { StatType.Strength, baseStr + levelGrowth + randomBonusStr },
                { StatType.Agility, baseAgi + levelGrowth + randomBonusAgi },
                { StatType.Vitality, baseVit + levelGrowth + randomBonusVit },
                { StatType.Intelligence, baseIntl + levelGrowth + randomBonusIntl },
            };
        }

        // ===================================================================
        // Шаг 1.8: Врождённая стихия (Спринт 3 B6)
        // ===================================================================

        /// <summary>
        /// Определить врождённую стихию NPC на основе типа души.
        /// Character → Neutral (люди не имеют врождённой стихии)
        /// Creature → Neutral (зависит от вида — TBD)
        /// Spirit → случайная из Fire/Water/Air/Earth/Light
        /// Construct → Neutral (искусственные существа)
        /// Спринт 3 B6: используется в DamageService для стихийных множителей.
        /// </summary>
        private static Element DetermineInnateElement(SpeciesData species, SeededRandom rng)
        {
            return species.SoulType switch
            {
                SoulType.Spirit => RandomSpiritElement(rng),
                _ => Element.Neutral // Character, Creature, Construct
            };
        }

        /// <summary>
        /// Случайная стихия для Spirit-существ.
        /// Веса: Fire=20%, Water=20%, Air=20%, Earth=20%, Light=20%
        /// </summary>
        private static Element RandomSpiritElement(SeededRandom rng)
        {
            int roll = rng.Next(0, 5);
            return roll switch
            {
                0 => Element.Fire,
                1 => Element.Water,
                2 => Element.Air,
                3 => Element.Earth,
                _ => Element.Light
            };
        }
    }
}

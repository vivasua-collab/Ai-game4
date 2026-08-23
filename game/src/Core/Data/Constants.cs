#nullable enable
// Создано: 2026-05-08 10:07:00 UTC
// Редактировано: 2026-05-09 — аудит: добавлено BASE_BODY_REGEN_RATE (BD-47)
// Редактировано: 2026-05-09 — BF-A05: добавлены 11 недостающих мягких капов
// Редактировано: 2026-05-09 — V1: BASE_CARRY_WEIGHT 50→10, V2: BASE_CONDUCTIVITY 1.0→2.78, V3: MAX_SUB_LEVEL_VALUE
// Редактировано: 2026-05-22 11:30:00 UTC — Спринт 8 C10: MorphologyHitTables (промилле)
// Редактировано: 2026-05-22 13:08:27 UTC — P0-8.1 FIX: Bird table Torso 300→310 (сумма=990→1000)
// Редактировано: 2026-05-22 13:48:17 UTC — Этап 2.1: integer константы для Qi buffer (ЗАПРЕТ 3.9)
// Редактировано: 2026-05-25 06:23:33 UTC — ЗАПРЕТ 3.9: добавлены _PERMIL варианты боевых констант
// Migrated from Ai-game3 (Unity) to Ai-game4 (Godot). UnityEngine.Mathf replaced with System.Math.
// Константы игры — модульная архитектура
using System;
using System.Collections.Generic;

namespace CultivationGame.Core.Data
{
    /// <summary>
    /// Статический класс с основными константами игры.
    /// Все числовые значения, таблицы и параметры.
    /// </summary>
    public static class GameConstants
    {
        #region Version

        public const string VERSION = "0.1.0";
        public const int SAVE_VERSION = 1;

        #endregion

        #region Character Base Stats

        /// <summary>Базовое значение характеристик</summary>
        public const int BASE_STAT_VALUE = 10;

        /// <summary>Минимальное значение характеристики</summary>
        public const int MIN_STAT_VALUE = 1;

        /// <summary>Максимальное значение характеристики</summary>
        public const int MAX_STAT_VALUE = 1000;

        /// <summary>
        /// Базовая ёмкость ядра (L1.0)
        /// Формула: coreCapacity = 1000 × 1.1^totalSubLevels
        /// </summary>
        public const int BASE_CORE_CAPACITY = 1000;

        /// <summary>
        /// Множитель роста ёмкости ядра
        /// Формула: coreCapacity = 1000 × 1.1^totalSubLevels
        /// </summary>
        public const float CORE_CAPACITY_GROWTH = 1.1f;

        /// <summary>Множитель роста ёмкости ядра (промилле: 1100 = 1.1)</summary>
        public const int CORE_CAPACITY_GROWTH_PERMIL = 1100;

        /// <summary>
        /// Базовая проводимость (L1.0, Normal ядро).
        /// Формула: coreCapacity / 360 = 1000/360 ≈ 2.78.
        /// Внимание: QiService.RecalculateStats() вычисляет проводимость динамически.
        /// Эта константа — только для справки/дефолтов.
        /// </summary>
        public const float BASE_CONDUCTIVITY = 2.78f;

        /// <summary>Базовый вес переносимого груза (STR=10) — 50 кг per user request 2026-08-22</summary>
        public const float BASE_CARRY_WEIGHT = 50f;

        #endregion

        #region Mortal Stages (до культивации)

        /// <summary>
        /// Формирование дремлющего ядра по этапам смертного (%)
        /// </summary>
        public static readonly Dictionary<MortalStage, (float min, float max)> DormantCoreFormation = new Dictionary<MortalStage, (float min, float max)>
        {
            { MortalStage.None, (0f, 0f) },
            { MortalStage.Newborn, (0f, 0.3f) },
            { MortalStage.Child, (0.3f, 0.6f) },
            { MortalStage.Adult, (0.6f, 0.9f) },
            { MortalStage.Mature, (0.9f, 1.0f) },
            { MortalStage.Elder, (0.5f, 0.8f) },
            { MortalStage.Awakening, (0.8f, 1.0f) }
        };

        /// <summary>
        /// Максимальная естественная Ци для смертных
        /// </summary>
        public static readonly Dictionary<MortalStage, int> MaxMortalQi = new Dictionary<MortalStage, int>
        {
            { MortalStage.None, 0 },
            { MortalStage.Newborn, 30 },
            { MortalStage.Child, 100 },
            { MortalStage.Adult, 200 },
            { MortalStage.Mature, 150 },
            { MortalStage.Elder, 80 },
            { MortalStage.Awakening, 250 }
        };

        /// <summary>
        /// Шанс естественного пробуждения.
        /// Проценты (0-100): X% шанс пробуждения.
        /// </summary>
        public static readonly Dictionary<MortalStage, float> AwakeningChance = new Dictionary<MortalStage, float>
        {
            { MortalStage.None, 0f },
            { MortalStage.Newborn, 0f },
            { MortalStage.Child, 0.01f },
            { MortalStage.Adult, 0.1f },
            { MortalStage.Mature, 1f },
            { MortalStage.Elder, 0.5f },
            { MortalStage.Awakening, 5f }
        };

        /// <summary>
        /// Возрастные диапазоны для этапов смертного
        /// </summary>
        public static readonly Dictionary<MortalStage, (int min, int max)> AgeRanges = new Dictionary<MortalStage, (int min, int max)>
        {
            { MortalStage.Newborn, (0, 7) },
            { MortalStage.Child, (7, 16) },
            { MortalStage.Adult, (16, 30) },
            { MortalStage.Mature, (30, 50) },
            { MortalStage.Elder, (50, 100) }
        };

        /// <summary>
        /// Множители шанса пробуждения по типу.
        /// Проценты (0-100): значение прямо в %.
        /// </summary>
        public static readonly Dictionary<AwakeningType, float> AwakeningTypeMultipliers = new Dictionary<AwakeningType, float>
        {
            { AwakeningType.None, 0f },
            { AwakeningType.Natural, 0.01f },
            { AwakeningType.Guided, 0.6f },
            { AwakeningType.Artifact, 0.3f },
            { AwakeningType.Forced, 0.4f }
        };

        /// <summary>Минимальная сформированность ядра для пробуждения</summary>
        public const float MIN_DORMANT_CORE_FOR_AWAKENING = 0.8f;

        /// <summary>Оптимальный возраст для пробуждения (начало)</summary>
        public const int OPTIMAL_AWAKENING_AGE_MIN = 16;

        /// <summary>Оптимальный возраст для пробуждения (конец)</summary>
        public const int OPTIMAL_AWAKENING_AGE_MAX = 40;

        #endregion

        #region Cultivation Levels

        /// <summary>Максимальный уровень культивации</summary>
        public const int MAX_CULTIVATION_LEVEL = 10;

        /// <summary>
        /// Количество под-уровней (0-9 = 10 значений).
        /// Для максимального ЗНАЧЕНИЯ под-уровня используйте MAX_SUB_LEVEL_VALUE.
        /// </summary>
        public const int MAX_SUB_LEVEL = 10;

        /// <summary>Максимальное значение под-уровня (9)</summary>
        public const int MAX_SUB_LEVEL_VALUE = 9;

        /// <summary>Ци для малого прорыва (под-уровень)</summary>
        public const float SMALL_BREAKTHROUGH_MULTIPLIER = 10f;

        /// <summary>Ци для большого прорыва (основной уровень)</summary>
        public const float BIG_BREAKTHROUGH_MULTIPLIER = 100f;

        /// <summary>Генерация микроядром (% от ёмкости в сутки)</summary>
        public const float MICROCORE_GENERATION_RATE = 0.1f;

        /// <summary>
        /// Плотность Ци по уровням (Qi Density = 2^(level-1))
        /// </summary>
        public static readonly int[] QiDensityByLevel = new int[]
        {
            1,      // L1
            2,      // L2
            4,      // L3
            8,      // L4
            16,     // L5
            32,     // L6
            64,     // L7
            128,    // L8
            256,    // L9
            512     // L10
        };

        /// <summary>
        /// Множители регенерации по уровням
        /// </summary>
        public static readonly float[] RegenerationMultipliers = new float[]
        {
            1.1f,   // L1
            2.0f,   // L2
            3.0f,   // L3
            5.0f,   // L4
            8.0f,   // L5
            15.0f,  // L6
            30.0f,  // L7
            100.0f, // L8
            1000.0f,// L9
            float.MaxValue    // L10 — мгновенное восстановление
        };

        #endregion

        #region Combat - Level Suppression

        /// <summary>
        /// Таблица подавления уровнем.
        /// [разница уровней][тип атаки: 0=normal, 1=technique, 2=ultimate]
        /// </summary>
        public static readonly float[][] LevelSuppressionTable = new float[][]
        {
            new float[] { 1.0f, 1.0f, 1.0f },    // Разница 0
            new float[] { 0.5f, 0.75f, 1.0f },   // Разница 1
            new float[] { 0.1f, 0.25f, 0.5f },   // Разница 2
            new float[] { 0.0f, 0.05f, 0.25f },  // Разница 3
            new float[] { 0.0f, 0.0f, 0.1f },    // Разница 4
            new float[] { 0.0f, 0.0f, 0.0f }     // Разница 5+
        };

        /// <summary>
        /// Таблица подавления уровнем (промилле).
        /// [разница уровней][тип атаки: 0=normal, 1=technique, 2=ultimate]
        /// ЗАПРЕТ 3.9: целочисленная арифметика.
        /// </summary>
        public static readonly int[][] LevelSuppressionTablePermil = new int[][]
        {
            new int[] { 1000, 1000, 1000 },    // Разница 0
            new int[] { 500, 750, 1000 },       // Разница 1
            new int[] { 100, 250, 500 },        // Разница 2
            new int[] { 0, 50, 250 },           // Разница 3
            new int[] { 0, 0, 100 },            // Разница 4
            new int[] { 0, 0, 0 }               // Разница 5+
        };

        /// <summary>Максимальная разница уровней для таблицы</summary>
        public const int MAX_LEVEL_DIFF = 5;

        #endregion

        #region Combat - Qi Buffer

        /// <summary>
        /// Поглощение сырой Ци для техник Ци (%)
        /// </summary>
        public const float RAW_QI_ABSORPTION = 0.9f;

        /// <summary>
        /// Пробивающий урон сырой Ци для техник Ци (%)
        /// </summary>
        public const float RAW_QI_PIERCING = 0.1f;

        /// <summary>
        /// Соотношение Ци:Урон для сырой Ци (техники Ци)
        /// </summary>
        public const float RAW_QI_RATIO = 3.0f;

        /// <summary>
        /// Соотношение Ци:Урон для щитовой техники
        /// </summary>
        public const float SHIELD_QI_RATIO = 1.0f;

        /// <summary>Минимальное Ци для активации буфера</summary>
        public const int MIN_QI_FOR_BUFFER = 10;

        // === Этап 2.1: ЗАПРЕТ 3.9 — integer константы для Qi buffer (промилле) ===

        /// <summary>Поглощение сырой Ци для техник Ци (промилле: 900 = 90%)</summary>
        public const int RAW_QI_ABSORPTION_PERMIL = 900;

        /// <summary>Пробивающий урон сырой Ци для техник Ци (промилле: 100 = 10%)</summary>
        public const int RAW_QI_PIERCING_PERMIL = 100;

        /// <summary>Соотношение Ци:Урон для сырой Ци (техники Ци, integer)</summary>
        public const int RAW_QI_RATIO_INT = 3;

        /// <summary>Соотношение Ци:Урон для щитовой техники (integer)</summary>
        public const int SHIELD_QI_RATIO_INT = 1;

        #endregion

        #region Combat - Physical Qi Buffer

        /// <summary>
        /// Поглощение сырой Ци для физического урона (%)
        /// </summary>
        public const float PHYSICAL_RAW_QI_ABSORPTION = 0.8f;

        /// <summary>
        /// Пробивающий урон для физического урона (%)
        /// </summary>
        public const float PHYSICAL_RAW_QI_PIERCING = 0.2f;

        /// <summary>
        /// Соотношение Ци:Урон для сырой Ци (физический урон)
        /// </summary>
        public const float PHYSICAL_RAW_QI_RATIO = 5.0f;

        /// <summary>
        /// Соотношение Ци:Урон для щита (физический урон)
        /// </summary>
        public const float PHYSICAL_SHIELD_QI_RATIO = 2.0f;

        // === Этап 2.1: ЗАПРЕТ 3.9 — integer константы для Physical Qi buffer (промилле) ===

        /// <summary>Поглощение сырой Ци для физического урона (промилле: 800 = 80%)</summary>
        public const int PHYSICAL_RAW_QI_ABSORPTION_PERMIL = 800;

        /// <summary>Пробивающий урон для физического урона (промилле: 200 = 20%)</summary>
        public const int PHYSICAL_RAW_QI_PIERCING_PERMIL = 200;

        /// <summary>Соотношение Ци:Урон для сырой Ци (физический урон, integer)</summary>
        public const int PHYSICAL_RAW_QI_RATIO_INT = 5;

        /// <summary>Соотношение Ци:Урон для щита (физический урон, integer)</summary>
        public const int PHYSICAL_SHIELD_QI_RATIO_INT = 2;

        #endregion

        #region Combat - Technique Capacity

        /// <summary>
        /// Базовая ёмкость техник по типу
        /// </summary>
        public static readonly Dictionary<TechniqueType, int> BaseCapacityByType = new Dictionary<TechniqueType, int>
        {
            { TechniqueType.Formation, 80 },
            { TechniqueType.Defense, 72 },
            { TechniqueType.Combat, 64 },
            { TechniqueType.Support, 56 },
            { TechniqueType.Healing, 56 },
            { TechniqueType.Movement, 40 },
            { TechniqueType.Curse, 40 },
            { TechniqueType.Poison, 40 },
            { TechniqueType.Sensory, 32 },
            { TechniqueType.Cultivation, 0 }
        };

        /// <summary>
        /// Базовая ёмкость по подтипу атаки
        /// </summary>
        public static readonly Dictionary<CombatSubtype, int> BaseCapacityBySubtype = new Dictionary<CombatSubtype, int>
        {
            { CombatSubtype.MeleeStrike, 64 },
            { CombatSubtype.MeleeWeapon, 48 },
            { CombatSubtype.RangedProjectile, 32 },
            { CombatSubtype.RangedBeam, 32 },
            { CombatSubtype.RangedAoe, 32 },
            { CombatSubtype.DefenseBlock, 72 },
            { CombatSubtype.DefenseShield, 72 },
            { CombatSubtype.DefenseDodge, 72 }
        };

        #endregion

        #region Combat - Grade Multipliers

        /// <summary>
        /// Множители урона по грейду техники.
        /// Стоимость Ци всегда ×1.0 — не зависит от Grade!
        /// </summary>
        public static readonly Dictionary<TechniqueGrade, float> TechniqueGradeMultipliers = new Dictionary<TechniqueGrade, float>
        {
            { TechniqueGrade.Common, 1.0f },
            { TechniqueGrade.Refined, 1.3f },
            { TechniqueGrade.Perfect, 1.6f },
            { TechniqueGrade.Transcendent, 2.0f }
        };

        /// <summary>
        /// Множители ЭФФЕКТИВНОСТИ по грейду экипировки
        /// Источник: EQUIPMENT_SYSTEM.md §2.1
        /// </summary>
        public static readonly Dictionary<EquipmentGrade, float> EquipmentGradeMultipliers = new Dictionary<EquipmentGrade, float>
        {
            { EquipmentGrade.Damaged, 0.5f },
            { EquipmentGrade.Common, 1.0f },
            { EquipmentGrade.Refined, 1.3f },
            { EquipmentGrade.Perfect, 1.6f },
            { EquipmentGrade.Transcendent, 2.0f }
        };

        /// <summary>
        /// Множители ПРОЧНОСТИ по грейду экипировки
        /// Источник: EQUIPMENT_SYSTEM.md §2.1 (отдельно от эффективности!)
        /// </summary>
        public static readonly Dictionary<EquipmentGrade, float> EquipmentGradeDurabilityMultipliers = new Dictionary<EquipmentGrade, float>
        {
            { EquipmentGrade.Damaged, 0.5f },
            { EquipmentGrade.Common, 1.0f },
            { EquipmentGrade.Refined, 1.5f },
            { EquipmentGrade.Perfect, 2.5f },
            { EquipmentGrade.Transcendent, 4.0f }
        };

        /// <summary>
        /// Множители урона по грейду техники (промилле).
        /// ЗАПРЕТ 3.9: целочисленная арифметика.
        /// </summary>
        public static readonly Dictionary<TechniqueGrade, int> TechniqueGradeMultipliersPermil = new Dictionary<TechniqueGrade, int>
        {
            { TechniqueGrade.Common, 1000 },
            { TechniqueGrade.Refined, 1300 },
            { TechniqueGrade.Perfect, 1600 },
            { TechniqueGrade.Transcendent, 2000 }
        };

        /// <summary>
        /// Множители эффективности по грейду экипировки (промилле).
        /// ЗАПРЕТ 3.9: целочисленная арифметика.
        /// </summary>
        public static readonly Dictionary<EquipmentGrade, int> EquipmentGradeMultipliersPermil = new Dictionary<EquipmentGrade, int>
        {
            { EquipmentGrade.Damaged, 500 },
            { EquipmentGrade.Common, 1000 },
            { EquipmentGrade.Refined, 1300 },
            { EquipmentGrade.Perfect, 1600 },
            { EquipmentGrade.Transcendent, 2000 }
        };

        /// <summary>
        /// Множители прочности по грейду экипировки (промилле).
        /// ЗАПРЕТ 3.9: целочисленная арифметика.
        /// </summary>
        public static readonly Dictionary<EquipmentGrade, int> EquipmentGradeDurabilityMultipliersPermil = new Dictionary<EquipmentGrade, int>
        {
            { EquipmentGrade.Damaged, 500 },
            { EquipmentGrade.Common, 1000 },
            { EquipmentGrade.Refined, 1500 },
            { EquipmentGrade.Perfect, 2500 },
            { EquipmentGrade.Transcendent, 4000 }
        };

        /// <summary>Множитель урона Ultimate-техники (промилле: 2000 = ×2.0)</summary>
        public const int ULTIMATE_DAMAGE_MULTIPLIER_PERMIL = 2000;

        /// <summary>Множитель стоимости Ци Ultimate-техники (промилле: 2000 = ×2.0)</summary>
        public const int ULTIMATE_QI_COST_MULTIPLIER_PERMIL = 2000;

        /// <summary>
        /// Множитель урона Ultimate-техники
        /// </summary>
        public const float ULTIMATE_DAMAGE_MULTIPLIER = 2.0f;

        /// <summary>
        /// Множитель стоимости Ци Ultimate-техники (×2.0 — TECHNIQUE_SYSTEM.md §9.1)
        /// </summary>
        public const float ULTIMATE_QI_COST_MULTIPLIER = 2.0f;

        #endregion

        #region Combat - Defense Pipeline

        /// <summary>
        /// Максимальное снижение урона бронёй (%)
        /// </summary>
        public const float MAX_DAMAGE_REDUCTION = 0.8f;

        /// <summary>
        /// Порог урона для смертельного удара по жизненно важной части (fallback)
        /// ЗАПРЕТ 3.9: int вместо float (сравнивается с int finalDamage)
        /// </summary>
        public const int FATAL_DAMAGE_THRESHOLD = 50;

        /// <summary>
        /// Снижение урона по материалу тела
        /// </summary>
        public static readonly Dictionary<BodyMaterial, float> BodyMaterialReduction = new Dictionary<BodyMaterial, float>
        {
            { BodyMaterial.Organic, 0.0f },
            { BodyMaterial.Scaled, 0.3f },
            { BodyMaterial.Chitin, 0.2f },
            { BodyMaterial.Mineral, 0.5f },
            { BodyMaterial.Ethereal, 0.7f },
            { BodyMaterial.Chaos, 0.4f }
        };

        /// <summary>
        /// Твёрдость материалов тела
        /// </summary>
        public static readonly Dictionary<BodyMaterial, int> BodyMaterialHardness = new Dictionary<BodyMaterial, int>
        {
            { BodyMaterial.Organic, 3 },
            { BodyMaterial.Scaled, 6 },
            { BodyMaterial.Chitin, 5 },
            { BodyMaterial.Mineral, 8 },
            { BodyMaterial.Ethereal, 1 },
            { BodyMaterial.Chaos, 5 }
        };

        /// <summary>Максимальное снижение урона бронёй (промилле: 800 = 80%)</summary>
        public const int MAX_DAMAGE_REDUCTION_PERMIL = 800;

        /// <summary>
        /// Снижение урона по материалу тела (промилле).
        /// ЗАПРЕТ 3.9: целочисленная арифметика.
        /// </summary>
        public static readonly Dictionary<BodyMaterial, int> BodyMaterialReductionPermil = new Dictionary<BodyMaterial, int>
        {
            { BodyMaterial.Organic, 0 },
            { BodyMaterial.Scaled, 300 },
            { BodyMaterial.Chitin, 200 },
            { BodyMaterial.Mineral, 500 },
            { BodyMaterial.Ethereal, 700 },
            { BodyMaterial.Chaos, 400 }
        };

        #endregion

        #region Combat - Body Part Hit Chances

        /// <summary>
        /// Базовые шансы попадания по частям тела (гуманоид)
        /// В промилле (1000 = 100%). Сумма = 1000.
        /// ЗАПРЕТ 3.9: целочисленная арифметика для шансов.
        /// </summary>
        public static readonly Dictionary<BodyPartType, int> BodyPartHitChancesPermil = new Dictionary<BodyPartType, int>
        {
            { BodyPartType.Head, 50 },           // 5%
            { BodyPartType.Torso, 400 },          // 40%
            { BodyPartType.Heart, 20 },           // 2%
            { BodyPartType.LeftArm, 100 },        // 10%
            { BodyPartType.RightArm, 100 },       // 10%
            { BodyPartType.LeftLeg, 120 },        // 12%
            { BodyPartType.RightLeg, 120 },       // 12%
            { BodyPartType.LeftHand, 40 },        // 4%
            { BodyPartType.RightHand, 40 },       // 4%
            { BodyPartType.LeftFoot, 5 },         // 0.5%
            { BodyPartType.RightFoot, 5 }         // 0.5%
        };

        /// <summary>
        /// Базовые шансы попадания по частям тела (гуманоид) — float (legacy).
        /// </summary>
        public static readonly Dictionary<BodyPartType, float> BodyPartHitChances = new Dictionary<BodyPartType, float>
        {
            { BodyPartType.Head, 0.05f },
            { BodyPartType.Torso, 0.40f },
            { BodyPartType.Heart, 0.02f },
            { BodyPartType.LeftArm, 0.10f },
            { BodyPartType.RightArm, 0.10f },
            { BodyPartType.LeftLeg, 0.12f },
            { BodyPartType.RightLeg, 0.12f },
            { BodyPartType.LeftHand, 0.04f },
            { BodyPartType.RightHand, 0.04f },
            { BodyPartType.LeftFoot, 0.005f },
            { BodyPartType.RightFoot, 0.005f }
        };

        /// <summary>
        /// Спринт 8 C10: Таблицы попадания по морфологиям.
        /// Ключ — Morphology, значение — Dictionary{BodyPartType, шанс в промилле}.
        /// Сумма значений каждой таблицы = 1000.
        /// ЗАПРЕТ 3.9: целочисленная арифметика для шансов.
        /// </summary>
        public static readonly Dictionary<Morphology, Dictionary<BodyPartType, int>> MorphologyHitTables = new Dictionary<Morphology, Dictionary<BodyPartType, int>>
        {
            {
                Morphology.Humanoid, BodyPartHitChancesPermil
            },
            {
                Morphology.Quadruped, new Dictionary<BodyPartType, int>
                {
                    { BodyPartType.Head, 30 },            // 3%
                    { BodyPartType.Torso, 350 },           // 35%
                    { BodyPartType.Heart, 10 },            // 1%
                    { BodyPartType.LeftArm, 50 },          // 5% (передняя левая)
                    { BodyPartType.RightArm, 50 },         // 5% (передняя правая)
                    { BodyPartType.LeftLeg, 150 },         // 15% (задняя левая)
                    { BodyPartType.RightLeg, 150 },        // 15% (задняя правая)
                    { BodyPartType.LeftHand, 30 },         // 3%
                    { BodyPartType.RightHand, 30 },        // 3%
                    { BodyPartType.Tail, 150 }             // 15% (хвост)
                }
            },
            {
                Morphology.Bird, new Dictionary<BodyPartType, int>
                {
                    { BodyPartType.Head, 50 },            // 5%
                    { BodyPartType.Torso, 310 },           // 31% (P0-8.1 FIX: было 300, сумма=990→1000)
                    { BodyPartType.Heart, 10 },            // 1%
                    { BodyPartType.LeftArm, 150 },         // 15% (левое крыло)
                    { BodyPartType.RightArm, 150 },        // 15% (правое крыло)
                    { BodyPartType.LeftLeg, 100 },         // 10%
                    { BodyPartType.RightLeg, 100 },        // 10%
                    { BodyPartType.Tail, 130 }             // 13% (хвост)
                }
            },
            {
                Morphology.Serpentine, new Dictionary<BodyPartType, int>
                {
                    { BodyPartType.Head, 80 },            // 8%
                    { BodyPartType.Torso, 400 },           // 40%
                    { BodyPartType.Heart, 20 },            // 2%
                    { BodyPartType.Tail, 500 }             // 50% (хвост = большая часть тела)
                }
            },
            {
                Morphology.Arthropod, new Dictionary<BodyPartType, int>
                {
                    { BodyPartType.Head, 80 },             // 8%
                    { BodyPartType.Torso, 400 },            // 40% (головогрудь)
                    { BodyPartType.Heart, 20 },             // 2%
                    { BodyPartType.LeftLeg, 100 },          // 10% (усреднённо)
                    { BodyPartType.RightLeg, 100 },         // 10%
                    { BodyPartType.LeftArm, 50 },           // 5% (педипальпы)
                    { BodyPartType.RightArm, 50 },          // 5%
                    { BodyPartType.Tail, 200 }              // 20% (брюшко)
                }
            },
            {
                Morphology.Amorphous, new Dictionary<BodyPartType, int>
                {
                    { BodyPartType.Core, 100 },            // 10% (ядро)
                    { BodyPartType.Torso, 500 },            // 50% (эфирное тело)
                    { BodyPartType.Essence, 400 }           // 40% (сущность)
                }
            },
            // Гибридные формы — используют базовую Humanoid таблицу
            // с частичной заменой через MorphologyHitTables (TBD)
        };

        #endregion

        #region Combat - Damage Distribution

        /// <summary>
        /// Доля урона на красную HP (функциональная)
        /// </summary>
        public const float RED_HP_RATIO = 0.7f;

        /// <summary>
        /// Доля урона на чёрную HP (структурная)
        /// </summary>
        public const float BLACK_HP_RATIO = 0.3f;

        /// <summary>
        /// Множитель структурной HP от функциональной
        /// </summary>
        public const float STRUCTURAL_HP_MULTIPLIER = 2.0f;

        /// <summary>Доля урона на красную HP (промилле: 700 = 70%)</summary>
        public const int RED_HP_RATIO_PERMIL = 700;

        /// <summary>Доля урона на чёрную HP (промилле: 300 = 30%)</summary>
        public const int BLACK_HP_RATIO_PERMIL = 300;

        /// <summary>Множитель структурной HP (промилле: 2000 = ×2.0)</summary>
        public const int STRUCTURAL_HP_MULTIPLIER_PERMIL = 2000;

        #endregion

        #region Soft Caps

        /// <summary>
        /// Конфигурация мягких капов
        /// </summary>
        public static class SoftCaps
        {
            // Скорость
            public const float SPEED_CAP = 0.5f;
            public const float SPEED_DECAY = 1.5f;

            // Скорость атаки
            public const float ATTACK_SPEED_CAP = 0.75f;
            public const float ATTACK_SPEED_DECAY = 1.2f;

            // Урон
            public const float DAMAGE_CAP = 1.0f;
            public const float DAMAGE_DECAY = 1.0f;

            // Критический шанс
            public const float CRIT_CHANCE_CAP = 0.5f;
            public const float CRIT_CHANCE_DECAY = 0.8f;

            // Критический урон
            public const float CRIT_DAMAGE_CAP = 1.5f;
            public const float CRIT_DAMAGE_DECAY = 1.0f;

            // Защита
            public const float DEFENSE_CAP = 0.8f;
            public const float DEFENSE_DECAY = 1.2f;

            // Броня
            public const float ARMOR_CAP = 200f;
            public const float ARMOR_DECAY = 1.5f;

            // Стоимость Ци (отрицательный кап)
            public const float QI_COST_CAP = -0.5f;
            public const float QI_COST_DECAY = 1.0f;

            // Эффективность Ци
            public const float QI_EFFICIENCY_CAP = 0.5f;
            public const float QI_EFFICIENCY_DECAY = 1.0f;

            // Кулдаун (отрицательный кап)
            public const float COOLDOWN_CAP = -0.6f;
            public const float COOLDOWN_DECAY = 1.2f;

            // Вампиризм
            public const float LIFESTEAL_CAP = 0.3f;
            public const float LIFESTEAL_DECAY = 0.8f;

            // BF-A05: Добавлены недостающие мягкие капы (11 штук)

            // Скрытность
            public const float STEALTH_CAP = 0.6f;
            public const float STEALTH_DECAY = 1.0f;

            // Восприятие
            public const float PERCEPTION_CAP = 0.8f;
            public const float PERCEPTION_DECAY = 1.0f;

            // Получаемое исцеление
            public const float HEALING_RECEIVED_CAP = 1.0f;
            public const float HEALING_RECEIVED_DECAY = 1.2f;

            // Регенерация HP
            public const float HP_REGEN_CAP = 0.5f;
            public const float HP_REGEN_DECAY = 1.0f;

            // Шипы (возврат урона)
            public const float THORNS_CAP = 0.3f;
            public const float THORNS_DECAY = 0.8f;

            // Удача
            public const float LUCK_CAP = 0.5f;
            public const float LUCK_DECAY = 1.0f;

            // Бонус опыта
            public const float EXP_BONUS_CAP = 1.0f;
            public const float EXP_BONUS_DECAY = 1.5f;

            // Стоимость выносливости (отрицательный кап)
            public const float STAMINA_COST_CAP = -0.5f;
            public const float STAMINA_COST_DECAY = 1.0f;

            // Регенерация выносливости
            public const float STAMINA_REGEN_CAP = 0.5f;
            public const float STAMINA_REGEN_DECAY = 1.0f;

            // Восстановление Ци
            public const float QI_RESTORATION_CAP = 0.5f;
            public const float QI_RESTORATION_DECAY = 1.0f;

            // Уклонение
            public const float EVASION_CAP = 0.4f;
            public const float EVASION_DECAY = 1.0f;
        }

        /// <summary>
        /// Конфигурация мягких капов (промилле).
        /// ЗАПРЕТ 3.9: целочисленная арифметика.
        /// Все значения ×1000 от float-аналогов.
        /// </summary>
        public static class PermilValues
        {
            // Скорость
            public const int SPEED_CAP = 500;
            public const int SPEED_DECAY = 1500;

            // Скорость атаки
            public const int ATTACK_SPEED_CAP = 750;
            public const int ATTACK_SPEED_DECAY = 1200;

            // Урон
            public const int DAMAGE_CAP = 1000;
            public const int DAMAGE_DECAY = 1000;

            // Критический шанс
            public const int CRIT_CHANCE_CAP = 500;
            public const int CRIT_CHANCE_DECAY = 800;

            // Критический урон
            public const int CRIT_DAMAGE_CAP = 1500;
            public const int CRIT_DAMAGE_DECAY = 1000;

            // Защита
            public const int DEFENSE_CAP = 800;
            public const int DEFENSE_DECAY = 1200;

            // Броня (значение ×1000: 200 → 200000)
            public const int ARMOR_CAP = 200000;
            public const int ARMOR_DECAY = 1500;

            // Стоимость Ци (отрицательный кап)
            public const int QI_COST_CAP = -500;
            public const int QI_COST_DECAY = 1000;

            // Эффективность Ци
            public const int QI_EFFICIENCY_CAP = 500;
            public const int QI_EFFICIENCY_DECAY = 1000;

            // Кулдаун (отрицательный кап)
            public const int COOLDOWN_CAP = -600;
            public const int COOLDOWN_DECAY = 1200;

            // Вампиризм
            public const int LIFESTEAL_CAP = 300;
            public const int LIFESTEAL_DECAY = 800;

            // Скрытность
            public const int STEALTH_CAP = 600;
            public const int STEALTH_DECAY = 1000;

            // Восприятие
            public const int PERCEPTION_CAP = 800;
            public const int PERCEPTION_DECAY = 1000;

            // Получаемое исцеление
            public const int HEALING_RECEIVED_CAP = 1000;
            public const int HEALING_RECEIVED_DECAY = 1200;

            // Регенерация HP
            public const int HP_REGEN_CAP = 500;
            public const int HP_REGEN_DECAY = 1000;

            // Шипы
            public const int THORNS_CAP = 300;
            public const int THORNS_DECAY = 800;

            // Удача
            public const int LUCK_CAP = 500;
            public const int LUCK_DECAY = 1000;

            // Бонус опыта
            public const int EXP_BONUS_CAP = 1000;
            public const int EXP_BONUS_DECAY = 1500;

            // Стоимость выносливости
            public const int STAMINA_COST_CAP = -500;
            public const int STAMINA_COST_DECAY = 1000;

            // Регенерация выносливости
            public const int STAMINA_REGEN_CAP = 500;
            public const int STAMINA_REGEN_DECAY = 1000;

            // Восстановление Ци
            public const int QI_RESTORATION_CAP = 500;
            public const int QI_RESTORATION_DECAY = 1000;

            // Уклонение
            public const int EVASION_CAP = 400;
            public const int EVASION_DECAY = 1000;
        }

        #endregion

        #region Time System

        /// <summary>Минут в часе</summary>
        public const int MINUTES_PER_HOUR = 60;

        /// <summary>Часов в сутках</summary>
        public const int HOURS_PER_DAY = 24;

        /// <summary>Дней в месяце</summary>
        public const int DAYS_PER_MONTH = 30;

        /// <summary>Месяцев в году</summary>
        public const int MONTHS_PER_YEAR = 12;

        /// <summary>Тиков в минуте игрового времени</summary>
        public const int TICKS_PER_MINUTE = 1;

        /// <summary>
        /// Множители скорости времени
        /// </summary>
        public static readonly Dictionary<TimeSpeed, float> TimeSpeedMultipliers = new Dictionary<TimeSpeed, float>
        {
            { TimeSpeed.Paused, 0f },
            { TimeSpeed.Normal, 1f },
            { TimeSpeed.Fast, 5f },
            { TimeSpeed.Quick, 15f }
        };

        #endregion

        #region Durability

        /// <summary>
        /// Состояния прочности по диапазону
        /// | Pristine | 100%   |
        /// | Good     | 80-99% |
        /// | Worn     | 60-79% |
        /// | Damaged  | 20-59% |
        /// | Broken   | <20%   |
        /// </summary>
        public static readonly Dictionary<DurabilityCondition, (float min, float max)> DurabilityRanges = new Dictionary<DurabilityCondition, (float min, float max)>
        {
            { DurabilityCondition.Pristine, (1.0f, 1.0f) },
            { DurabilityCondition.Good, (0.8f, 0.99f) },
            { DurabilityCondition.Worn, (0.6f, 0.79f) },
            { DurabilityCondition.Damaged, (0.2f, 0.59f) },
            { DurabilityCondition.Broken, (0.0f, 0.19f) }
        };

        /// <summary>
        /// Эффективность по состоянию прочности
        /// </summary>
        public static readonly Dictionary<DurabilityCondition, float> DurabilityEfficiency = new Dictionary<DurabilityCondition, float>
        {
            { DurabilityCondition.Pristine, 1.0f },
            { DurabilityCondition.Good, 0.95f },
            { DurabilityCondition.Worn, 0.85f },
            { DurabilityCondition.Damaged, 0.60f },
            { DurabilityCondition.Broken, 0.20f }
        };

        /// <summary>
        /// Состояния прочности по диапазону (промилле).
        /// ЗАПРЕТ 3.9: целочисленная арифметика.
        /// </summary>
        public static readonly Dictionary<DurabilityCondition, (int min, int max)> DurabilityRangesPermil = new Dictionary<DurabilityCondition, (int min, int max)>
        {
            { DurabilityCondition.Pristine, (1000, 1000) },
            { DurabilityCondition.Good, (800, 990) },
            { DurabilityCondition.Worn, (600, 790) },
            { DurabilityCondition.Damaged, (200, 590) },
            { DurabilityCondition.Broken, (0, 190) }
        };

        /// <summary>
        /// Эффективность по состоянию прочности (промилле).
        /// ЗАПРЕТ 3.9: целочисленная арифметика.
        /// </summary>
        public static readonly Dictionary<DurabilityCondition, int> DurabilityEfficiencyPermil = new Dictionary<DurabilityCondition, int>
        {
            { DurabilityCondition.Pristine, 1000 },
            { DurabilityCondition.Good, 950 },
            { DurabilityCondition.Worn, 850 },
            { DurabilityCondition.Damaged, 600 },
            { DurabilityCondition.Broken, 200 }
        };

        #endregion

        #region Elements

        /// <summary>
        /// Противоположные элементы
        /// Fire ↔ Water, Earth ↔ Air, Lightning ↔ Void, Light ↔ Void
        /// Void имеет ДВЕ противоположности (Lightning и Light).
        /// </summary>
        public static readonly Dictionary<Element, Element> OppositeElements = new Dictionary<Element, Element>
        {
            { Element.Fire, Element.Water },
            { Element.Water, Element.Fire },
            { Element.Earth, Element.Air },
            { Element.Air, Element.Earth },
            { Element.Lightning, Element.Void },
            { Element.Void, Element.Lightning },
            { Element.Light, Element.Void },
        };

        /// <summary>
        /// Вторая противоположность для Void (Light).
        /// </summary>
        public static readonly Element? VoidSecondaryOpposite = Element.Light;

        /// <summary>
        /// Проверяет, являются ли два элемента противоположными.
        /// Учитывает, что Void имеет две противоположности (Lightning и Light).
        /// </summary>
        public static bool IsOppositeElement(Element a, Element b)
        {
            // Прямая проверка в словаре
            if (OppositeElements.TryGetValue(a, out var opposite) && opposite == b)
                return true;
            // Обратная проверка
            if (OppositeElements.TryGetValue(b, out var oppositeB) && oppositeB == a)
                return true;
            // Проверка второй противоположности Void
            if (a == Element.Void && VoidSecondaryOpposite == b)
                return true;
            if (b == Element.Void && VoidSecondaryOpposite == a)
                return true;
            return false;
        }

        /// <summary>
        /// Множитель урона при атаке противоположного элемента
        /// </summary>
        public const float OPPOSITE_ELEMENT_MULTIPLIER = 1.5f;

        /// <summary>
        /// Множитель урона при сродстве элементов
        /// </summary>
        public const float AFFINITY_ELEMENT_MULTIPLIER = 0.8f;

        /// <summary>
        /// Множитель урона Void по всем элементам
        /// </summary>
        public const float VOID_ELEMENT_MULTIPLIER = 1.2f;

        /// <summary>
        /// Множитель урона Fire по Poison (выжигание токсинов, одностороннее)
        /// </summary>
        public const float FIRE_TO_POISON_MULTIPLIER = 1.2f;

        /// <summary>
        /// Множитель урона Light по Poison (очищение, одностороннее)
        /// Источник: ALGORITHMS.md §10.2
        /// </summary>
        public const float LIGHT_TO_POISON_MULTIPLIER = 1.2f;

        /// <summary>Множитель урона при атаке противоположного элемента (промилле: 1500 = ×1.5)</summary>
        public const int OPPOSITE_ELEMENT_MULTIPLIER_PERMIL = 1500;

        /// <summary>Множитель урона при сродстве элементов (промилле: 800 = ×0.8)</summary>
        public const int AFFINITY_ELEMENT_MULTIPLIER_PERMIL = 800;

        /// <summary>Множитель урона Void по всем элементам (промилле: 1200 = ×1.2)</summary>
        public const int VOID_ELEMENT_MULTIPLIER_PERMIL = 1200;

        /// <summary>Множитель урона Fire по Poison (промилле: 1200 = ×1.2)</summary>
        public const int FIRE_TO_POISON_MULTIPLIER_PERMIL = 1200;

        /// <summary>Множитель урона Light по Poison (промилле: 1200 = ×1.2)</summary>
        public const int LIGHT_TO_POISON_MULTIPLIER_PERMIL = 1200;

        #endregion

        #region Item Rarity

        /// <summary>
        /// Шанс выпадения по редкости
        /// </summary>
        public static readonly Dictionary<ItemRarity, float> RarityDropChances = new Dictionary<ItemRarity, float>
        {
            { ItemRarity.Common, 0.50f },
            { ItemRarity.Uncommon, 0.30f },
            { ItemRarity.Rare, 0.15f },
            { ItemRarity.Epic, 0.04f },
            { ItemRarity.Legendary, 0.01f },
            { ItemRarity.Mythic, 0.001f }
        };

        /// <summary>
        /// Шанс выпадения по редкости (промилле).
        /// ЗАПРЕТ 3.9: целочисленная арифметика.
        /// </summary>
        public static readonly Dictionary<ItemRarity, int> RarityDropChancesPermil = new Dictionary<ItemRarity, int>
        {
            { ItemRarity.Common, 500 },
            { ItemRarity.Uncommon, 300 },
            { ItemRarity.Rare, 150 },
            { ItemRarity.Epic, 40 },
            { ItemRarity.Legendary, 10 },
            { ItemRarity.Mythic, 1 }
        };

        #endregion

        // STR-MODEL: #region Inventory удалён (мёртвый код).
        // Сеточные константы INVENTORY_WIDTH/HEIGHT, MAX_ITEM_WIDTH/HEIGHT
        // заменены на строчную модель (weight + volume) в InventoryConfig.
        // См. 05_18_audit_06_inventory_string_model.md (BUG-CONST-01).

        #region Interaction

        /// <summary>
        /// Типы взаимодействия (string-константы для NPCInteractedEvent.InteractionType).
        /// A03-fix: замена магических строк.
        /// </summary>
        public static class InteractionType
        {
            public const string Talk = "talk";
            public const string Trade = "trade";
            public const string Attack = "attack";
            public const string Gift = "gift";
            public const string Interact = "interact";
            public const string Examine = "examine";
        }

        #endregion

        #region NPC

        /// <summary>Максимальное расстояние обнаружения NPC</summary>
        public const float NPC_DETECTION_RANGE = 20f;

        /// <summary>Частота обновления AI (секунды)</summary>
        public const float AI_UPDATE_INTERVAL = 0.5f;

        /// <summary>Расстояние для взаимодействия</summary>
        public const float INTERACTION_DISTANCE = 2f;

        #endregion

        #region Body System

        /// <summary>
        /// Базовая скорость регенерации тела (HP/сек).
        /// Источник: BODY_SYSTEM.md — 0.1 HP/тик.
        /// ALGORITHMS.md: 1 тик = 1 минута игрового времени.
        /// При нормальной скорости (1 реальная секунда = 1 игровая минута): 0.1 HP/тик = 0.1 HP/сек.
        /// </summary>
        public const float BASE_BODY_REGEN_RATE = 0.1f;

        /// <summary>
        /// Множитель Vitality → HP.
        /// Формула: hpMultiplier = 1 + (Vit - 10) × VITALITY_HP_COEFFICIENT
        /// Источник: BODY_SYSTEM.md §"Живучесть"
        /// </summary>
        public const float VITALITY_HP_COEFFICIENT = 0.05f;

        /// <summary>Множитель Vitality → HP (промилле: 50 = 0.05)</summary>
        public const int VITALITY_HP_COEFFICIENT_PERMIL = 50;

        /// <summary>
        /// Множители HP по классу размера.
        /// Источник: BODY_SYSTEM.md §"Классы размера"
        /// </summary>
        public static readonly Dictionary<SizeClass, float> SizeClassHPMultipliers = new Dictionary<SizeClass, float>
        {
            { SizeClass.Tiny, 0.3f },
            { SizeClass.Small, 0.5f },
            { SizeClass.Medium, 1.0f },
            { SizeClass.Large, 1.5f },
            { SizeClass.Huge, 2.0f },
            { SizeClass.Gargantuan, 3.0f },
            { SizeClass.Colossal, 5.0f }
        };

        /// <summary>
        /// Множители силы по классу размера.
        /// Источник: BODY_SYSTEM.md §"Классы размера"
        /// </summary>
        public static readonly Dictionary<SizeClass, float> SizeClassStrengthMultipliers = new Dictionary<SizeClass, float>
        {
            { SizeClass.Tiny, 0.1f },
            { SizeClass.Small, 0.3f },
            { SizeClass.Medium, 1.0f },
            { SizeClass.Large, 2.0f },
            { SizeClass.Huge, 5.0f },
            { SizeClass.Gargantuan, 15.0f },
            { SizeClass.Colossal, 50.0f }
        };

        /// <summary>
        /// Множители HP по классу размера (промилле).
        /// ЗАПРЕТ 3.9: целочисленная арифметика.
        /// </summary>
        public static readonly Dictionary<SizeClass, int> SizeClassHPMultipliersPermil = new Dictionary<SizeClass, int>
        {
            { SizeClass.Tiny, 300 },
            { SizeClass.Small, 500 },
            { SizeClass.Medium, 1000 },
            { SizeClass.Large, 1500 },
            { SizeClass.Huge, 2000 },
            { SizeClass.Gargantuan, 3000 },
            { SizeClass.Colossal, 5000 }
        };

        /// <summary>
        /// Множители силы по классу размера (промилле).
        /// ЗАПРЕТ 3.9: целочисленная арифметика.
        /// </summary>
        public static readonly Dictionary<SizeClass, int> SizeClassStrengthMultipliersPermil = new Dictionary<SizeClass, int>
        {
            { SizeClass.Tiny, 100 },
            { SizeClass.Small, 300 },
            { SizeClass.Medium, 1000 },
            { SizeClass.Large, 2000 },
            { SizeClass.Huge, 5000 },
            { SizeClass.Gargantuan, 15000 },
            { SizeClass.Colossal, 50000 }
        };

        #endregion

        #region Charger System

        /// <summary>
        /// Минимальное время накачки (сек).
        /// minChargeTime = tickInterval / 10.
        /// </summary>
        public const float MIN_CHARGE_TIME = 0.1f;

        /// <summary>
        /// Доля затраченного Ци, преобразуемая в тепло
        /// Источник: CHARGER_SYSTEM.md
        /// </summary>
        public const float CHARGER_HEAT_GAIN_RATE = 0.05f;

        /// <summary>
        /// Порог перегрева (1.0 = 100%)
        /// </summary>
        public const float CHARGER_OVERHEAT_THRESHOLD = 1.0f;

        /// <summary>
        /// Время блокировки при перегреве (сек)
        /// </summary>
        public const float CHARGER_OVERHEAT_COOLDOWN = 30f;

        /// <summary>
        /// Доля потерь Ци при активном заряднике
        /// </summary>
        public const float CHARGER_EFFICIENCY_LOSS = 0.1f;

        /// <summary>
        /// Пассивная скорость остывания (%/сек)
        /// </summary>
        public const float CHARGER_PASSIVE_COOLING_RATE = 0.01f;

        /// <summary>
        /// Скорость остывания в бою (%/сек)
        /// </summary>
        public const float CHARGER_COMBAT_COOLING_RATE = 0.005f;

        // === ЗАПРЕТ 3.9: промилле-константы зарядника ===

        /// <summary>Доля Ци → тепло (промилле: 50 = 5%)</summary>
        public const int CHARGER_HEAT_GAIN_RATE_PERMIL = 50;

        /// <summary>Порог перегрева (промилле: 1000 = 100%)</summary>
        public const int CHARGER_OVERHEAT_THRESHOLD_PERMIL = 1000;

        /// <summary>Доля потерь Ци (промилле: 100 = 10%)</summary>
        public const int CHARGER_EFFICIENCY_LOSS_PERMIL = 100;

        #endregion

        #region Formation System

        /// <summary>
        /// Базовая стоимость прорисовки контура (L1).
        /// Формула: contourQi = FORMATION_BASE_CONTOUR_QI × 2^(level-1)
        /// Источник: FORMATION_SYSTEM.md
        /// </summary>
        public const int FORMATION_BASE_CONTOUR_QI = 80;

        /// <summary>
        /// Множители ёмкости по размеру формации.
        /// capacity = contourQi × sizeMultiplier
        /// </summary>
        public static readonly Dictionary<FormationSize, long> FormationSizeMultipliers = new Dictionary<FormationSize, long>
        {
            { FormationSize.Small, 10 },
            { FormationSize.Medium, 50 },
            { FormationSize.Large, 200 },
            { FormationSize.Great, 1000 },
            { FormationSize.Heavy, 10000 }
        };

        /// <summary>
        /// Минимальный уровень для формации Heavy
        /// </summary>
        public const int HEAVY_FORMATION_MIN_LEVEL = 6;

        /// <summary>
        /// Радиус прорисовки контура по размеру формации (в метрах)
        /// </summary>
        public static readonly Dictionary<FormationSize, float> FormationDrawingRadius = new Dictionary<FormationSize, float>
        {
            { FormationSize.Small, 10f },
            { FormationSize.Medium, 20f },
            { FormationSize.Large, 30f },
            { FormationSize.Great, 50f },
            { FormationSize.Heavy, 100f }
        };

        /// <summary>
        /// Радиус действия формации по размеру (в метрах)
        /// </summary>
        public static readonly Dictionary<FormationSize, float> FormationEffectRadius = new Dictionary<FormationSize, float>
        {
            { FormationSize.Small, 50f },
            { FormationSize.Medium, 200f },
            { FormationSize.Large, 600f },
            { FormationSize.Great, 1000f },
            { FormationSize.Heavy, 5000f }
        };

        /// <summary>
        /// Максимум помощников по размеру формации
        /// </summary>
        public static readonly Dictionary<FormationSize, int> FormationMaxHelpers = new Dictionary<FormationSize, int>
        {
            { FormationSize.Small, 2 },
            { FormationSize.Medium, 5 },
            { FormationSize.Large, 10 },
            { FormationSize.Great, 20 },
            { FormationSize.Heavy, 50 }
        };

        /// <summary>
        /// Интервал утечки Ци в тиках (1 тик = 1 минута игрового времени).
        /// Утечка происходит дискретно: N Ци за раз через M тиков.
        /// </summary>
        public static readonly Dictionary<int, int> FormationDrainIntervalByLevel = new Dictionary<int, int>
        {
            { 1, 60 },  // L1: каждый час
            { 2, 60 },  // L2: каждый час
            { 3, 40 },  // L3: каждые 40 мин
            { 4, 40 },  // L4: каждые 40 мин
            { 5, 20 },  // L5: каждые 20 мин
            { 6, 20 },  // L6: каждые 20 мин
            { 7, 10 },  // L7: каждые 10 мин
            { 8, 10 },  // L8: каждые 10 мин
            { 9, 5 },   // L9: каждые 5 мин
            { 10, 5 }   // L10: каждые 5 мин
        };

        /// <summary>
        /// Количество Ци за раз при утечке, по размеру формации
        /// </summary>
        public static readonly Dictionary<FormationSize, long> FormationDrainAmountBySize = new Dictionary<FormationSize, long>
        {
            { FormationSize.Small, 1 },
            { FormationSize.Medium, 3 },
            { FormationSize.Large, 10 },
            { FormationSize.Great, 30 },
            { FormationSize.Heavy, 100 }
        };

        /// <summary>
        /// Множители среды (environmentMult) для формаций.
        /// Влияет на скорость заполнения и эффективность.
        /// </summary>
        public static readonly Dictionary<string, float> FormationEnvironmentMultipliers = new Dictionary<string, float>
        {
            { "desert", 0.1f },
            { "normal", 0.5f },
            { "rich", 1.0f },
            { "spiritual", 2.0f },
            { "sacred", 5.0f }
        };

        /// <summary>
        /// Минимальный уровень помощника: max(1, formationLevel - 2)
        /// </summary>
        public static int GetMinHelperLevel(int formationLevel)
        {
            return Math.Max(1, formationLevel - 2);
        }

        /// <summary>
        /// Проводимость ядер формаций (ед/сек) по варианту материала
        /// </summary>
        public static readonly Dictionary<FormationCoreVariant, float> FormationCoreConductivity = new Dictionary<FormationCoreVariant, float>
        {
            { FormationCoreVariant.Stone, 5f },
            { FormationCoreVariant.Jade, 10f },
            { FormationCoreVariant.Iron, 15f },
            { FormationCoreVariant.SpiritIron, 25f },
            { FormationCoreVariant.Crystal, 55f },
            { FormationCoreVariant.StarMetal, 0f },   // Расширение
            { FormationCoreVariant.VoidMatter, 0f }    // Расширение
        };

        /// <summary>
        /// Ёмкость ядер формаций по варианту материала
        /// </summary>
        public static readonly Dictionary<FormationCoreVariant, long> FormationCoreCapacity = new Dictionary<FormationCoreVariant, long>
        {
            { FormationCoreVariant.Stone, 10000 },
            { FormationCoreVariant.Jade, 50000 },
            { FormationCoreVariant.Iron, 200000 },
            { FormationCoreVariant.SpiritIron, 500000 },
            { FormationCoreVariant.Crystal, 20000000 },
            { FormationCoreVariant.StarMetal, 0 },     // Расширение
            { FormationCoreVariant.VoidMatter, 0 }     // Расширение
        };

        /// <summary>
        /// Количество слотов для камней Ци по варианту материала ядра
        /// </summary>
        public static readonly Dictionary<FormationCoreVariant, int> FormationCoreSlots = new Dictionary<FormationCoreVariant, int>
        {
            { FormationCoreVariant.Stone, 1 },
            { FormationCoreVariant.Jade, 1 },
            { FormationCoreVariant.Iron, 2 },
            { FormationCoreVariant.SpiritIron, 3 },
            { FormationCoreVariant.Crystal, 5 },
            { FormationCoreVariant.StarMetal, 0 },
            { FormationCoreVariant.VoidMatter, 0 }
        };

        #endregion

        #region Save System

        /// <summary>Интервал автосохранения (в тиках)</summary>
        public const int AUTO_SAVE_INTERVAL = 60;

        /// <summary>Максимум слотов сохранений</summary>
        public const int MAX_SAVE_SLOTS = 5;

        /// <summary>Расширение файлов сохранения</summary>
        public const string SAVE_FILE_EXTENSION = ".sav";

        #endregion

        // ========================================================================
        // Ai-game4 additions — time/tick/rendering constants preserved from the
        // previous stub. Required by Structs.WorldTime and the Godot Adapter.
        // ========================================================================

        #region Ai-game4 Time / Tick / Rendering

        /// <summary>1 tick = 1 minute of game time.</summary>
        public const int TICKS_PER_HOUR = 60;
        /// <summary>24 hours × 60 minutes.</summary>
        public const int TICKS_PER_DAY = 1440;
        /// <summary>30 days × 1440 ticks.</summary>
        public const int TICKS_PER_MONTH = 43200;
        /// <summary>12 months × 43200 ticks.</summary>
        public const int TICKS_PER_YEAR = 518400;

        /// <summary>Game-world start year (used by WorldTime).</summary>
        public const int START_YEAR = 1864;

        /// <summary>Normal speed: 1 tick per real second.</summary>
        public const int SPEED_NORMAL = 1;
        /// <summary>Fast speed: 5 ticks per real second.</summary>
        public const int SPEED_FAST = 5;
        /// <summary>Quick speed: 15 ticks per real second.</summary>
        public const int SPEED_QUICK = 15;

        /// <summary>Autosave trigger interval (in ticks) — alias of AUTO_SAVE_INTERVAL.</summary>
        public const int AUTOSAVE_INTERVAL_TICKS = AUTO_SAVE_INTERVAL;
        /// <summary>Qi regeneration batch interval (in ticks).</summary>
        public const int QI_REGEN_BATCH_TICKS = 10;

        // ── Медитация / среда (QI_SYSTEM.md §5.2, FORMATION_SYSTEM.md §10.2) ──
        /// <summary>
        /// Базовый множитель среды для медитации («Обычная» местность, ×0.5).
        /// Формации Gathering повышают множитель в зоне действия.
        /// </summary>
        public const float ENVIRONMENT_MULT_NORMAL = 0.5f;

        // ── Tile / rendering ────────────────────────────────────────────────
        /// <summary>Tile edge length in meters (tile is 2×2 m).</summary>
        public const int TILE_SIZE_M = 2;
        /// <summary>Pixels per meter (rendering scaling factor).</summary>
        public const int METERS_TO_PIXELS = 32;
        /// <summary>Tile size in pixels (TILE_SIZE_M * METERS_TO_PIXELS).</summary>
        public const int TILE_PIXELS = 64;
        /// <summary>Pixels-per-unit used by the renderer for tile sprites.</summary>
        public const int TILE_PPU = 32;

        /// <summary>
        /// Default world map dimensions (test polygon V1). Used by PlayerModule
        /// and GameWorldController for clamping player movement bounds when
        /// TileService is not yet available. Replaces previously hardcoded
        /// literal "49" (= MapWidth - 1) and "50" (= MapWidth).
        /// See audit issue #15 (08_15_code_audit.md).
        /// </summary>
        public const int DEFAULT_MAP_WIDTH = 50;
        public const int DEFAULT_MAP_HEIGHT = 50;

        // ── NPC / combat ────────────────────────────────────────────────────
        /// <summary>Maximum simultaneously active NPCs in the world.</summary>
        public const int MAX_ACTIVE_NPCS = 100;
        /// <summary>Aggro detection radius (tiles).</summary>
        public const float AGGRO_RADIUS = 5f;
        /// <summary>Attack reach radius (tiles).</summary>
        public const float ATTACK_RADIUS = 1.5f;
        /// <summary>Default patrol radius (tiles).</summary>
        public const float PATROL_RADIUS = 10f;
        /// <summary>Default NPC move speed (tiles/sec).</summary>
        public const float DEFAULT_MOVE_SPEED = 2.0f;
        /// <summary>Fleeing speed multiplier.</summary>
        public const float FLEE_SPEED_MULT = 1.5f;

        // ── Save file layout (Godot user dir) ───────────────────────────────
        /// <summary>Main save file name.</summary>
        public const string SAVE_MAIN_FILE = "main.sav";
        /// <summary>Per-chunk save subdirectory.</summary>
        public const string SAVE_CHUNKS_DIR = "chunks";
        /// <summary>Per-location save subdirectory.</summary>
        public const string SAVE_LOCATIONS_DIR = "locations";
        /// <summary>Session metadata file name.</summary>
        public const string SAVE_METADATA = "metadata.sav";

        #endregion
    }
}

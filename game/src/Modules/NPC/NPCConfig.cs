#nullable enable
// Создано: 2026-05-09 15:30:00 UTC
// Редактировано: 2026-05-20 18:00:11 UTC — Фаза 0: П#1 CoreQualityMultipliers, П#2 AwakeningTypeWeights, П#4 ConductivityGrowthFactors
// Редактировано: 2026-08-22 — IMPL-5 (Q6): весовые таблицы генерации техник/экипировки
//   (TechniqueGradeWeights, TechniqueGradeMultipliers, EquipmentGradeWeightsByLevel)
//   перенесены в Core.Data.GeneratorTables — Generator-модуль больше не зависит от NPC-модуля.
// Конфигурация модуля NPC.
// BD-48: class (не struct — mutable struct risk).
namespace CultivationGame.Modules.NPC
{
    /// <summary>
    /// Конфигурация модуля NPC.
    /// Устанавливается через NPCModule.SetConfig() из NPCLifetimeScope.
    /// </summary>
    public class NPCConfig
    {
        /// <summary>Радиус обнаружения врагов (агрессия)</summary>
        public float AggroRadius = 5f;

        /// <summary>Радиус атаки (дальности удара)</summary>
        public float AttackRadius = 1.5f;

        /// <summary>Радиус бегства (расстояние для попытки убежать)</summary>
        public float FleeRadius = 8f;

        /// <summary>Радиус патруля (максимальное удаление от точки спавна)</summary>
        public float PatrolRadius = 10f;

        /// <summary>Скорость затухания угрозы в секунду</summary>
        public float ThreatDecayRate = 2f;

        /// <summary>Порог угрозы для перехода в атаку</summary>
        public float ThreatThreshold = 50f;

        /// <summary>Порог HP для бегства (доля от максимума)</summary>
        public float FleeHealthRatio = 0.2f;

        /// <summary>Затухание отношения за один игровой день</summary>
        public int AttitudeDecayPerDay = 1;

        /// <summary>Дней до начала затухания отношений</summary>
        public int AttitudeDecayStartDays = 7;

        /// <summary>Максимальное количество активных NPC</summary>
        public int MaxActiveNPCs = 100;

        /// <summary>Скорость движения NPC по умолчанию (ед/сек)</summary>
        public float DefaultMoveSpeed = 2f;

        /// <summary>Скорость бегства (множитель к DefaultMoveSpeed)</summary>
        public float FleeSpeedMultiplier = 1.5f;

        // === Параметры генерации души (Шаг 1) — Создано: 2026-05-20 ===

        /// <summary>Базовая ёмкость ядра (L1.0)</summary>
        public float BaseCoreCapacity = 1000f;

        /// <summary>Множитель роста ёмкости ядра</summary>
        public float CoreCapacityGrowth = 1.1f;

        /// <summary>Множители качества ядра (7 градаций: Fragmented..Transcendent). ЕДИНЫЕ для игрока и NPC (ПРОТИВОРЕЧИЕ #1). Источник: QiBreakthroughCalculator</summary>
        public float[] CoreQualityMultipliers = { 0.5f, 0.7f, 0.85f, 1.0f, 1.2f, 1.5f, 2.0f };

        /// <summary>Веса качества ядра для Character</summary>
        public float[] CoreQualityWeightsCharacter = { 5f, 15f, 25f, 35f, 14f, 5f, 1f };

        /// <summary>Веса качества ядра для Creature</summary>
        public float[] CoreQualityWeightsCreature = { 20f, 30f, 25f, 20f, 4f, 1f, 0f };

        /// <summary>Веса типа пробуждения (None, Natural, Guided, Artifact, Forced). 5 записей — None вес 0 (ПРОТИВОРЕЧИЕ #2, F1-03 фикс)</summary>
        public float[] AwakeningTypeWeights = { 0f, 20f, 50f, 20f, 10f };

        /// <summary>Веса дельты уровня (-2, -1, 0, +1)</summary>
        public float[] LevelDeltaWeights = { 18f, 36f, 41f, 5f };

        /// <summary>Смещение капа уровня локации: npcLevel ≤ locationLevel + X</summary>
        public float LocationLevelCapOffset = 0.9f;

        // === Параметры проводимости (ПРОТИВОРЕЧИЕ #4) — Создано: 2026-05-20 ===

        /// <summary>levelGrowthFactor проводимости по уровням: L0..L7+ (ПРОТИВОРЕЧИЕ #4 — расширенная формула)</summary>
        public float[] ConductivityGrowthFactors = { 1.0f, 1.2f, 1.5f, 2.0f, 3.0f, 5.0f, 8.0f, 12.0f };

        // === Таймауты AI — Создано: 2026-05-20 ===

        /// <summary>Таймаут бездействия (сек)</summary>
        public float IdleTimeout = 10f;

        /// <summary>Таймаут блуждания (сек)</summary>
        public float WanderTimeout = 15f;

        /// <summary>Таймаут патруля (сек)</summary>
        public float PatrolTimeout = 30f;

        /// <summary>Таймаут бегства (сек)</summary>
        public float FleeTimeout = 5f;

        /// <summary>Дистанция следования (ед)</summary>
        public float FollowDistance = 2f;

        // NOTE (Q6 / IMPL-5, AUDIT-2 M6): весовые таблицы генерации экипировки
        // и техник (EquipmentGradeWeightsByLevel, TechniqueGradeWeights,
        // TechniqueGradeMultipliers) перенесены в Core.Data.GeneratorTables.
        // Generator-модуль больше не зависит от NPC-модуля.
        // NPC-специфичные веса (soul gen) остаются здесь — они используются
        // только SoulGenerator внутри NPC-модуля.
    }
}

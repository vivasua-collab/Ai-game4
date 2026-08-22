#nullable enable
// Создано: 2026-05-09 15:30:00 UTC
// Редактировано: 2026-05-20 18:00:11 UTC — Фаза 1: MaxLifespan (1.B) + BodyParts = List<BodyPart> (П#6)
// Редактировано: 2026-05-20 18:43:21 UTC — Фаза 3: статы STR/AGI/VIT/INT (задача 3.3)
// Редактировано: 2026-05-20 19:11:00 UTC — Фаза 4, задача 4.6: AwakeningAge поле
// Редактировано: 2026-05-22 04:14:49 UTC — Спринт 3 B6: InnateElement
// Редактировано: 2026-05-23 — IMPL-1: Moved from Modules/NPC/Data to Core/Data
//   (fix Core→Modules architecture violation: NPCState references BodyPart which now also lives in Core.Data).
// Runtime-состояние экземпляра NPC.
// Хранит изменяемые данные для каждой NPC-сущности.
// NPCRole и NPCAIState определены в Core/Data/Enums.cs (общие для всего проекта).
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Core.Data
{
    /// <summary>
    /// Runtime-состояние экземпляра NPC.
    /// Создаётся при спавне, уничтожается при деспавне.
    /// Все изменяемые поля — здесь, неизменяемые — в пресете (NPCData).
    ///
    /// NPCRole и NPCAIState — перечисления из CultivationGame.Core (Enums.cs),
    /// используются в NPCContracts.cs и INPCService.cs.
    /// </summary>
    public class NPCState
    {
        // === Идентификация ===
        public string NpcId;
        public string PresetId;
        public string DisplayName;

        // === Классификация ===
        public NPCRole Role;
        public NPCCategory Category;
        public PersonalityTrait Personality;
        public SoulType SoulType;
        public Morphology Morphology;
        public BodyMaterial BodyMaterial;

        // === Культивация ===
        public CultivationLevel CultivationLevel;
        public int SubLevel;
        public CoreQuality CoreQuality;
        public long MaxQi;              // Fix-01: long для значений Ци
        public long CurrentQi;          // Fix-01: long для значений Ци
        public float Conductivity;

        // === Здоровье ===
        public int MaxHealth;
        public int CurrentHealth;

        // === AI-состояние ===
        public NPCAIState AIState;
        public string TargetId;         // Текущая цель (для атаки/преследования)
        public float StateTimer;        // Таймер текущего состояния AI

        // === Отношения ===
        public int AttitudeScore;       // Отношение к игроку (-100..+100)

        // === Флаги ===
        public bool IsAlive;
        public bool IsInCombat;

        // === Принадлежность ===
        public string SectId;
        public string CurrentLocation;

        // === Позиция ===
        public Position2D Position;

        // === Угрозы (sourceId → threatLevel) ===
        public Dictionary<string, float> Threats = new Dictionary<string, float>();

        // === Кэш Ци из QiChangedEvent ===
        public long CachedPlayerQi;     // Fix-01: long для значений Ци
        public int CachedPlayerLevel = 1;

        // === Новые поля пайплайна NPC Assembly — Создано: 2026-05-20 ===

        /// <summary>Хронологический возраст (лет)</summary>
        public int Age;

        /// <summary>Возраст пробуждения (для расчёта latePenalty — задача 4.6)</summary>
        public int AwakeningAge;

        /// <summary>Тип пробуждения ядра</summary>
        public AwakeningType AwakeningType;

        /// <summary>Этап смертного развития</summary>
        public MortalStage MortalStage;

        /// <summary>Плотность Ци: 2^(level-1)</summary>
        public int QiDensity;

        /// <summary>Части тела (Шаг 3)</summary>
        public List<BodyPart> BodyParts = new();

        /// <summary>Идентификаторы изученных техник (Шаг 6)</summary>
        public List<string> TechniqueIds = new();

        /// <summary>Экипировка по слотам (Шаг 5)</summary>
        public Dictionary<EquipmentSlot, string> EquipmentIds = new();

        /// <summary>Инвентарь (Шаг 7)</summary>
        public List<InventorySlot> InventorySlots = new();

        /// <summary>Базовый урон (без оружия)</summary>
        public int BaseDamage;

        /// <summary>Базовая защита (без брони)</summary>
        public int BaseDefense;

        /// <summary>Уровень агрессии (0..1)</summary>
        public float AggressionLevel;

        /// <summary>Идентификатор вида ("human", "wolf", ...)</summary>
        public string SpeciesId;

        /// <summary>Максимальная продолжительность жизни (лет) — из SoulData (задача 1.B)</summary>
        public int MaxLifespan;

        // === Базовые статы (Фаза 3, задача 3.3) ===

        /// <summary>Сила (из SoulData.CalculateStats) — ЗАПРЕТ 3.9: int</summary>
        public int Strength;

        /// <summary>Ловкость (из SoulData.CalculateStats) — ЗАПРЕТ 3.9: int</summary>
        public int Agility;

        /// <summary>Живучесть (из SoulData.CalculateStats) — ЗАПРЕТ 3.9: int</summary>
        public int Vitality;

        /// <summary>Интеллект (из SoulData.CalculateStats) — ЗАПРЕТ 3.9: int</summary>
        public int Intelligence;

        // === Стихия (Спринт 3 B6) ===

        /// <summary>Врождённая стихия NPC (из SoulData.InnateElement). По умолчанию Neutral.</summary>
        public Element InnateElement = Element.Neutral;
    }
}

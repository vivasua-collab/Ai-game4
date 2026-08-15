#nullable enable
// Создано: 2026-05-08 10:07:00 UTC
// Редактировано: 2026-05-08 10:55:19 UTC — исправлен namespace на CultivationGame.Core.Data
// Редактировано: 2026-05-09 16:00:00 UTC — Phase 9: расширение полей NPC (культивация, Ци, тело, AI, отношение, статус)

namespace CultivationGame.Core.Data
{
    /// <summary>
    /// Данные NPC во время выполнения
    /// </summary>
    public class NPCData
    {
        public string NpcId = string.Empty;
        public string PresetId = string.Empty;
        public string DisplayName = string.Empty;
        public NPCCategory Category;
        public PersonalityTrait Personality;
        public Position2D Position;

        // === Природа и культивация ===
        public SoulType SoulType;              // Природа сущности
        public Morphology Morphology;          // Внешняя форма
        public BodyMaterial BodyMaterial;      // Материал тела
        public CultivationLevel CultivationLevel; // Уровень культивации
        public int SubLevel;                   // Подуровень (0-9)
        public CoreQuality CoreQuality;        // Качество ядра

        // === Ци ===
        public long MaxQi;                     // Максимальное Ци
        public long CurrentQi;                 // Текущее Ци
        public float Conductivity;             // Проводимость

        // === Тело ===
        public int MaxHealth;                  // Максимальное HP
        public int CurrentHealth;              // Текущее HP

        // === AI ===
        public NPCRole Role;                   // Роль NPC
        public NPCAIState AIState;             // Текущее AI-состояние
        public string TargetId = string.Empty; // ID текущей цели
        public float StateTimer;               // Таймер состояния

        // === Отношение ===
        public int AttitudeScore;              // Числовое отношение (-100..+100)

        // === Статус ===
        public bool IsAlive = true;            // Жив ли NPC
        public bool IsInCombat;                // В бою ли
        public string SectId = string.Empty;   // ID секты
        public string CurrentLocation = string.Empty; // Текущая локация
    }
}

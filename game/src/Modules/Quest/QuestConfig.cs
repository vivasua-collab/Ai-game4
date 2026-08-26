#nullable enable
// Создано: 2026-05-09 — Phase 12: конфигурация модуля квестов
// BD-48: class (не struct)
// Migrated from Ai-game3 (Unity) to Ai-game4 (Godot).
namespace CultivationGame.Modules.Quest
{
    /// <summary>
    /// Конфигурация модуля квестов.
    /// BD-48: class, потому что содержит вложенные ссылочные типы.
    /// </summary>
    public class QuestConfig
    {
        /// <summary>Максимум активных квестов одновременно</summary>
        public int MaxActiveQuests = 10;

        /// <summary>Разрешить ли автогенерацию квестов</summary>
        public bool EnableAutoGeneration = true;

        /// <summary>Интервал проверки автогенерации (в тиках / игровых минутах)</summary>
        public int AutoGenerateIntervalTicks = 60;

        /// <summary>Шанс автогенерации квеста при каждой проверке (0-1)</summary>
        public float AutoGenerateChance = 0.3f;
    }
}

#nullable enable
// Создано: 2026-05-09 — Phase 12: конфигурация модуля квестов
// BD-48: class (не struct)
// Migrated from Ai-game3 (Unity) to Ai-game4 (Godot).
namespace CultivationGame.Modules.Quest
{
    /// <summary>
    /// Конфигурация модуля квестов.
    /// BD-48: class, потому что содержит вложенные ссылочные типы.
    /// Review этап 7 (P2-4): EnableAutoGeneration/AutoGenerateIntervalTicks/
    /// AutoGenerateChance УДАЛЕНЫ — мёртвая конфигурация (QuestModule.Tick пуст,
    /// QuestService не читал ни одного поля; автогенерация — будущая фаза:
    /// вернём конфиг вместе с реализацией, а не ложным ожиданием).
    /// </summary>
    public class QuestConfig
    {
        /// <summary>Максимум активных квестов одновременно</summary>
        public int MaxActiveQuests = 10;
    }
}

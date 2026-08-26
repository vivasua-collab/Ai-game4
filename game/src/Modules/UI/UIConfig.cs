#nullable enable
// Создано: 2026-05-09 — Phase 14: конфигурация модуля UI
// BD-48: class (не struct)
// Migrated from Ai-game3 (Unity) to Ai-game4 (Godot).
namespace CultivationGame.Modules.UI
{
    /// <summary>
    /// Конфигурация модуля UI.
    /// BD-48: class, потому что содержит ссылочные поля.
    /// </summary>
    public class UIConfig
    {
        /// <summary>Длительность уведомления Toast по умолчанию (секунды)</summary>
        public float DefaultToastDuration = 3f;

        /// <summary>Максимальное количество одновременных Toast</summary>
        public int MaxToastCount = 5;

        /// <summary>Задержка перед показом следующего Toast (секунды)</summary>
        public float ToastQueueDelay = 0.3f;
    }
}

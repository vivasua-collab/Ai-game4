#nullable enable
// Создано: 2026-05-09 — Phase 13: конфигурация модуля взаимодействия
// BD-48: class (не struct)
namespace CultivationGame.Modules.Interaction
{
    /// <summary>
    /// Конфигурация модуля взаимодействия.
    /// BD-48: class, потому что содержит ссылочные поля.
    /// </summary>
    public class InteractionConfig
    {
        /// <summary>Дальность взаимодействия по умолчанию (метры)</summary>
        public float DefaultInteractionRange = 2f;

        /// <summary>Скорость typewriter-эффекта (символов в секунду)</summary>
        public float TypewriterSpeed = 30f;

        /// <summary>Порог обновления кэша ближайшего объекта (метры)</summary>
        public float PositionUpdateThreshold = 0.5f;
    }
}

#nullable enable
// Создано: 2026-05-09 04:35:17 UTC
// Конфигурация модуля Ци.
// BD-48 урок: class вместо struct (mutable struct risk).
// Migrated from Ai-game3 (Unity) to Ai-game4 (Godot).
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Qi
{
    /// <summary>
    /// Конфигурация модуля Ци.
    /// Устанавливается через SetConfig() из QiLifetimeScope.
    /// </summary>
    public class QiConfig
    {
        /// <summary>Идентификатор сущности</summary>
        public string EntityId = "player";

        /// <summary>Начальный уровень культивации (1-10)</summary>
        public int CultivationLevel = 1;

        /// <summary>Начальный под-уровень (0-9)</summary>
        public int SubLevel = 0;

        /// <summary>Качество ядра</summary>
        public CoreQuality CoreQuality = CoreQuality.Normal;

        /// <summary>Начальное Ци (-1 = заполнить до максимума)</summary>
        public long InitialQi = -1;

        /// <summary>Бонус проводимости от перков (0.0 - 2.0)</summary>
        public float ConductivityBonus = 0f;

        /// <summary>Включить пассивную регенерацию</summary>
        public bool EnablePassiveRegen = true;

        /// <summary>Множитель регенерации (от уровня, см. RegenerationMultipliers)</summary>
        public float RegenMultiplier = 1f;
    }
}

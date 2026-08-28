#nullable enable
// Создано: 2026-05-09
// Конфигурация модуля боя.
// BD-48 урок: class, не struct (mutable struct risk).
namespace CultivationGame.Modules.Combat
{
    /// <summary>
    /// Конфигурация модуля боя.
    /// BD-48: class, не struct.
    /// </summary>
    public class CombatConfig
    {
        // C-6 FIX (аудит-3): PlayerEntityId удалён — поле не использовалось
        // после миграции на PlayerIdResolver (канонический ID игрока
        // определяется там; config-алиас "player" дрейфовал от "player_0").

        /// <summary>Включить AI противников</summary>
        public bool EnableAI = true;

        /// <summary>Задержка между ходами AI (секунды)</summary>
        public float AITurnDelay = 1.0f;

        /// <summary>Максимальная длительность боя (секунды, 0 = бесконечно)</summary>
        public float MaxCombatDuration = 0f;

        /// <summary>Включить автоматический лут после боя</summary>
        public bool AutoLootOnVictory = true;

        /// <summary>Множитель урона игрока (для баланса)</summary>
        public float PlayerDamageMultiplier = 1.0f;

        /// <summary>Множитель урона врагов (для баланса)</summary>
        public float EnemyDamageMultiplier = 1.0f;
    }
}

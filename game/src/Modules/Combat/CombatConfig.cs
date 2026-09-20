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
        // Review этап 3 (P0-2): EnableAI/AITurnDelay удалены — конфиги
        // фантомного CombatAIService (legacy), ничего не читали.
        /// <summary>Максимальная длительность боя (секунды, 0 = бесконечно)</summary>
        public float MaxCombatDuration = 0f;

        /// <summary>
        /// R21-2 (attack-speed модель): порог готовности удара в промилле.
        /// Каждый тик боя участнику начисляется скорость атаки (оружие ×
        /// AGI-фактор §8.2); при readiness ≥ порога удар принимается и
        /// вычитает порог (остаток сохраняется — плавные каденции).
        /// ЗАПРЕТ 3.9: int промилле.
        /// </summary>
        public int AttackThresholdPermil = 1000;

        /// <summary>Множитель урона игрока (для баланса)</summary>
        public float PlayerDamageMultiplier = 1.0f;

        /// <summary>Множитель урона врагов (для баланса)</summary>
        public float EnemyDamageMultiplier = 1.0f;
    }
}

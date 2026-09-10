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
        /// Review этап 3 (P0-1): тайм-аут ЧУЖОГО хода (сек игрового времени).
        /// Пассивный не-игрок (не атакует) не блокирует бой навсегда: после
        /// тайм-аута ход возвращается другой стороне. Ход игрока тайм-аута
        /// не имеет. NPC-атака идёт кулдауном 1.6с + каст ~0.5с — 2.5с хватает.
        /// </summary>
        public float EnemyTurnTimeoutSec = 2.5f;

        /// <summary>Множитель урона игрока (для баланса)</summary>
        public float PlayerDamageMultiplier = 1.0f;

        /// <summary>Множитель урона врагов (для баланса)</summary>
        public float EnemyDamageMultiplier = 1.0f;
    }
}

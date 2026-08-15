#nullable enable
// Создано: 2026-05-09 16:17:00 UTC
// Конфигурация модуля игрока.
// BD-48: class (не struct) — избегание риска изменяемой структуры (mutable struct).
// Migrated from Ai-game3 (Unity) to Ai-game4 (Godot).
namespace CultivationGame.Modules.Player
{
    /// <summary>
    /// Конфигурация модуля игрока.
    /// Все настраиваемые параметры вынесены сюда.
    /// BD-48: class (не struct).
    /// </summary>
    public class PlayerConfig
    {
        /// <summary>Идентификатор игрока по умолчанию</summary>
        public string DefaultPlayerId = "player_0";

        /// <summary>Имя игрока по умолчанию</summary>
        public string DefaultPlayerName = "Практик";

        /// <summary>Время перехода в сон (секунды)</summary>
        public float FallAsleepTime = 2f;

        /// <summary>Время пробуждения (секунды)</summary>
        public float WakeUpTime = 1f;

        /// <summary>Минимальная длительность сна для закрепления статов (часы)</summary>
        public float MinSleepHoursForConsolidation = 4f;

        /// <summary>Максимальное закрепление за один сон (доля от виртуальной дельты)</summary>
        public float MaxConsolidationPerSleep = 0.20f;

        /// <summary>Скорость восстановления HP во сне (% в час от максимального)</summary>
        public float SleepHpRecoveryPercentPerHour = 12.5f;

        /// <summary>Скорость восстановления выносливости во сне (% в час)</summary>
        public float SleepStaminaRecoveryPercentPerHour = 100f;

        /// <summary>Расстояние публикации позиции (минимальное смещение для события)</summary>
        /// <remarks>FIX-MOVEMENT: Снижено с 0.5 до 0.01 для плавного визуального движения.</remarks>
        public float PositionUpdateThreshold = 0.01f;

        /// <summary>Максимальное количество слотов техник</summary>
        public int MaxTechniqueSlots = 9;

        /// <summary>Прирост виртуальной дельты за действие</summary>
        public float StatDeltaPerAction = 0.001f;

        /// <summary>Скорость движения (ед/сек)</summary>
        public float MoveSpeed = 3f;

        /// <summary>Множитель скорости бега</summary>
        public float RunSpeedMultiplier = 1.5f;
    }
}

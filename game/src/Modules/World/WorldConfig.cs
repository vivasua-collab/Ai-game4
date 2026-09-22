#nullable enable
using CultivationGame.Core.Data;
// Создано: 2026-05-09 17:30:00 UTC
// Конфигурация модуля World.
// BD-48: class (не struct) — содержит ссылочные типы.
// Migrated from Ai-game3 (Unity) to Ai-game4 (Godot).
namespace CultivationGame.Modules.World
{
    /// <summary>
    /// Конфигурация модуля World.
    /// Параметры для TimeService, LocationService, FactionService, EventService.
    /// BD-48: class (не struct), так как содержит словари.
    ///
    /// R36-a (Фаза 11 / P3-11): удалены мёртвые противоречивые источники
    /// времени — StartYear/StartMonth/StartDay/StartHour (конфликтовали с
    /// каноном 06:00 дня 1, зашитым в GameConstants/TimeService: StartHour=12
    /// никогда не читался), BaseTickRate (не читался), AutoSaveIntervalTicks=60
    /// (конфликтовал с реальным SaveConfig.AutoSaveIntervalMinutes=30).
    /// Канонические источники: GameConstants (календарь), TimeService
    /// (старт мира 06:00 день 1), SaveConfig (каденция автосейва).
    /// </summary>
    public class WorldConfig
    {
        // === Время ===
        /// <summary>Скорость времени по умолчанию при старте модуля.</summary>
        public TimeSpeed DefaultSpeed = TimeSpeed.Normal;

        // === Локации ===
        /// <summary>Начальная локация</summary>
        public string StartLocationId = "start_village";

        /// <summary>Начальный сектор</summary>
        public string StartSectorId = "0_0";

        // === Фракции ===
        /// <summary>Множитель влияния сект на attitude (0.3)</summary>
        public float FactionAttitudeWeight = 0.3f;

        /// <summary>Множитель влияния нации на attitude (0.2)</summary>
        public float NationAttitudeWeight = 0.2f;

        // === Мировые события ===
        /// <summary>Шанс случайного мирового события в тик (0.0001 = ~раз в 10000 тиков)</summary>
        public float RandomEventChancePerTick = 0.0001f;

        /// <summary>Минимальная длительность мирового события в тиках</summary>
        public int MinEventDurationTicks = 60;

        /// <summary>Максимальная длительность мирового события в тиках</summary>
        public int MaxEventDurationTicks = 4800;
    }
}

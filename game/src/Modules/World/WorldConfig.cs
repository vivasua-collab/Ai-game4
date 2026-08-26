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
    /// </summary>
    public class WorldConfig
    {
        // === Время ===
        /// <summary>Начальный год (Э.С.М.)</summary>
        public int StartYear = 1864;

        /// <summary>Начальный месяц (1-12)</summary>
        public int StartMonth = 1;

        /// <summary>Начальный день (1-30)</summary>
        public int StartDay = 1;

        /// <summary>Начальный час (0-23)</summary>
        public int StartHour = 12;

        /// <summary>Скорость времени по умолчанию при старте модуля.</summary>
        public TimeSpeed DefaultSpeed = TimeSpeed.Normal;

        /// <summary>Базовая скорость тиков в секунду при Normal</summary>
        public float BaseTickRate = 1f;

        /// <summary>Интервал автосохранения в тиках</summary>
        public int AutoSaveIntervalTicks = 60;

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

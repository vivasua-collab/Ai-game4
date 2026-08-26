#nullable enable
// Создано: 2026-05-12 — Phase 18A: конфигурация модуля сохранения
// Редактировано: 2026-05-12 — Phase 18A: начальная реализация
// MIGRATION (Ai-game4): `using UnityEngine` removed. Ai-game4 will use
// System.IO.Path.Combine + Godot user data dir (resolved by SaveModule/Adapter)
// instead of Unity's Application.persistentDataPath.
namespace CultivationGame.Modules.Save
{
    /// <summary>
    /// Конфигурация модуля сохранения.
    /// BD-48: class (не struct) — для DI-регистрации.
    /// Все параметры настраиваемые — могут быть переопределены программно
    /// через SaveModuleServices.
    /// </summary>
    public class SaveConfig
    {
        /// <summary>
        /// Интервал автосохранения в игровых минутах.
        /// Каждые N игровых минут срабатывает автосохранение.
        /// По умолчанию: 5 игровых минут.
        /// </summary>
        public int AutoSaveIntervalMinutes = 5;

        /// <summary>
        /// Директория сохранений (относительно пользовательского пути данных).
        /// В Ai-game4 resolved by SaveModule/Adapter via Godot OS.GetUserDataDir().
        /// По умолчанию: "saves".
        /// </summary>
        public string SaveDirectory = "saves";

        /// <summary>
        /// Максимальное количество слотов сохранения.
        /// По умолчанию: 5 (Slot1, Slot2, Slot3, AutoSave, QuickSave).
        /// </summary>
        public int MaxSaveSlots = 5;

        /// <summary>
        /// Включить сжатие Gzip для файлов сохранения.
        /// Phase 19+: пока отключено, заглушка для будущей реализации.
        /// По умолчанию: false.
        /// </summary>
        public bool EnableCompression = false;

        /// <summary>
        /// Версия формата сохранения.
        /// Используется для обратной совместимости при миграции данных.
        /// По умолчанию: 1.
        /// </summary>
        public int SaveVersion = 1;

        /// <summary>
        /// Разрешить быстрое сохранение (QuickSave).
        /// По умолчанию: true.
        /// </summary>
        public bool QuickSaveEnabled = true;
    }
}

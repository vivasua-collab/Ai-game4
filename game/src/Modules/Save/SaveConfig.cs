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
        /// Интервал автосохранения в игровых минутах (1 тик = 1 игровая минута,
        /// Normal-скорость = 1 тик/сек). Гейт: автосейв работает ТОЛЬКО в
        /// активной сессии (SessionState.Playing) — см. SaveModule.Tick (R17).
        /// 0 или меньше — автосейв выключен.
        /// PT3-001 (плейтест-2-рестарт, 25.10): 30 → 300 — указание владельца
        /// «временной интервал 5 минут» (30 игр.мин = 30 сек реального времени
        /// при Normal — владелец наблюдал автосейвы каждые полминуты; 300 игр.мин
        /// = ровно 5 реальных минут при Normal-скорости).
        /// </summary>
        public int AutoSaveIntervalMinutes = 300;

        /// <summary>
        /// PT3-001 (плейтест-2-рестарт): ротация автосейвов — «принцип слотов»
        /// по указанию владельца («максимум 10 автосейвов. Или принцип ротации
        /// или принцип слотов»). Слоты autosave_01..autosave_10 (2 цифры —
        /// легаси-мусор autosave_NNNN старых билдов не конфликтует и чистится
        /// при старте). Полный набор → перезапись САМОГО СТАРОГО по save_meta
        /// (ротация переживает рестарт процесса — состояние не в памяти).
        /// </summary>
        public int AutoSaveRotationSlots = 10;

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

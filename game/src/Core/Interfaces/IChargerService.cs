#nullable enable
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Messaging.Contracts;
// Создано: 2026-05-08 12:52:40 UTC
// Редактировано: 2026-05-08 14:22:17 UTC — аудит CH-04: Tick/Configure добавлены в интерфейс

namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Интерфейс сервиса зарядников Ци.
    /// Управляет слотами камней, буфером Ци, тепловым балансом.
    /// Источник: CHARGER_SYSTEM.md
    /// </summary>
    public interface IChargerService
    {
        /// <summary>Зарядник работоспособен (включён и не перегрет)</summary>
        bool IsOperational { get; }

        /// <summary>Зарядник перегрет</summary>
        bool IsOverheated { get; }

        /// <summary>Уровень тепла (0.0 - 1.0)</summary>
        float HeatLevel { get; }

        /// <summary>Состояние тепла</summary>
        HeatState HeatState { get; }

        /// <summary>Количество слотов</summary>
        int SlotCount { get; }

        /// <summary>Количество активных слотов (с камнями)</summary>
        int ActiveSlotsCount { get; }

        /// <summary>Ци в буфере</summary>
        long BufferQi { get; }

        /// <summary>Ёмкость буфера</summary>
        long BufferCapacity { get; }

        /// <summary>Режим работы зарядника</summary>
        ChargerMode Mode { get; }

        // === Управление режимом ===

        /// <summary>Активировать зарядник</summary>
        void Activate();

        /// <summary>Деактивировать зарядник</summary>
        void Deactivate();

        // === Операции со слотами ===

        /// <summary>Получить состояние слота</summary>
        ChargerSlotState GetSlotState(int slotIndex);

        /// <summary>Попробовать зарядить слот (вставить Ци в камень)</summary>
        bool TryCharge(int slotIndex, float qiAmount);

        /// <summary>Попробовать разрядить слот (извлечь Ци из камня)</summary>
        bool TryDischarge(int slotIndex, float qiAmount);

        // === Операции с Ци ===

        /// <summary>Использовать Ци для техники (ядро → буфер).</summary>
        /// <returns>True если Ци достаточно</returns>
        bool UseQiForTechnique(long qiCost);

        /// <summary>Проверить, достаточно ли Ци для техники</summary>
        bool CanUseTechnique(long qiCost);

        /// <summary>Доступное Ци (ядро практика + буфер с учётом потерь)</summary>
        long GetAvailableQi();

        // === Боевой режим ===

        /// <summary>Войти в боевой режим (медленное остывание)</summary>
        void EnterCombat();

        /// <summary>Выйти из боевого режима</summary>
        void ExitCombat();

        // === Жизненный цикл ===

        /// <summary>
        /// Кадровое обновление зарядника.
        /// Вызывается из ChargerModule.ITickable.Tick().
        /// CH-04: добавлен в интерфейс, чтобы убрать приведение типов в Tick().
        /// Configure() НЕ добавлен — использует ChargerBufferConfig из Modules.Charger,
        /// что создало бы циклическую зависимость Core → Modules.
        /// ChargerModule может вызывать Configure напрямую (один модуль).
        /// </summary>
        void Tick();
    }

    /// <summary>
    /// Режим работы зарядника.
    /// Упрощён до двух режимов: вкл/выкл.
    /// </summary>
    public enum ChargerMode
    {
        Off,    // Выключен (скорость 0, потери 0%)
        On      // Включен (скорость ×1.0, потери 10%)
    }
}

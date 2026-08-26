#nullable enable
using CultivationGame.Core;
using CultivationGame.Core.Data;
// Создано: 2026-05-28 — UI-2: интерфейс сервиса стамины
// ЗАПРЕТ 3.9: целочисленная арифметика, промилле ×1000

namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Сервис управления stamina (выносливостью) игрока.
    /// Отвечает за расход, регенерацию и публикацию событий.
    /// Все промилле-значения: 1000‰ = полная стамина, 0‰ = пустая.
    /// </summary>
    public interface IStaminaService
    {
        /// <summary>Текущая стамина (абсолютное значение)</summary>
        int CurrentStamina { get; }

        /// <summary>Максимальная стамина</summary>
        int MaxStamina { get; }

        /// <summary>Текущая стамина в промилле от максимума (0–1000)</summary>
        int CurrentPromille { get; }

        /// <summary>Истощена ли стамина полностью</summary>
        bool IsExhausted { get; }

        /// <summary>
        /// Расход стамины. Возвращает true если хватало стамины.
        /// Публикует StaminaChangedEvent при изменении.
        /// </summary>
        /// <param name="amountPromille">Расход в промилле от максимума (1–1000)</param>
        bool Spend(int amountPromille);

        /// <summary>
        /// Регенерация стамины за тик.
        /// Публикует StaminaChangedEvent при изменении.
        /// </summary>
        /// <param name="deltaTimeSec">Время в миллисекундах (×1000, int)</param>
        void Regenerate(int deltaTimeMs);

        /// <summary>
        /// Установить текущую стамину напрямую (для загрузки/читов).
        /// </summary>
        void SetCurrent(int current);
    }
}

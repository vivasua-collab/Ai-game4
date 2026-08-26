#nullable enable
using CultivationGame.Core;
using CultivationGame.Core.Data;
// Создано: 2026-05-28 — UI-2: интерфейс сервиса валюты (Духовные Камни)
// ЗАПРЕТ 3.9: целочисленная арифметика

namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Сервис управления валютой (Духовные Камни).
    /// Публикует CurrencyChangedEvent при изменении баланса.
    /// </summary>
    public interface ICurrencyService
    {
        /// <summary>Текущее количество Духовных Камней</summary>
        int SpiritStones { get; }

        /// <summary>
        /// Добавить Духовные Камни.
        /// Публикует CurrencyChangedEvent.
        /// </summary>
        /// <param name="amount">Количество (должно быть > 0)</param>
        void Add(int amount);

        /// <summary>
        /// Потратить Духовные Камни.
        /// Публикует CurrencyChangedEvent при успехе.
        /// </summary>
        /// <param name="amount">Количество (должно быть > 0)</param>
        /// <returns>True если хватало камней</returns>
        bool Spend(int amount);

        /// <summary>
        /// Установить баланс напрямую (для загрузки).
        /// </summary>
        void SetBalance(int spiritStones);
    }
}

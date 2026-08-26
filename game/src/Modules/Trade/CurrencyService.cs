#nullable enable
// Создано: 2026-08-25 — NPC_COMBAT_PREP Phase 4: реализация ICurrencyService.
// Духовные камни игрока (UI-2). Баланс — int (ЗАПРЕТ 3.9).
// CurrencyChangedEvent уже существует в PlayerContracts.cs — публикуем его.
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Trade
{
    /// <summary>
    /// Сервис валюты (Духовные Камни). Стартовый баланс — TradeConfig.StartStones.
    /// Публикует CurrencyChangedEvent при каждом изменении баланса
    /// (UI-лавка и HUD обновляют индикатор по этому событию).
    /// </summary>
    public sealed class CurrencyService : ICurrencyService
    {
        [Inject] private readonly IPublisher<CurrencyChangedEvent> _changedPub = null!;
        [Inject] private readonly TradeConfig _config = null!;

        private int _spiritStones;
        private bool _initialized;

        /// <summary>Текущее количество Духовных Камней.</summary>
        public int SpiritStones
        {
            get
            {
                EnsureInitialized();
                return _spiritStones;
            }
        }

        /// <summary>Ленивая инициализация стартового баланса (до TradeModule.Start).</summary>
        private void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            _spiritStones = _config?.StartStones ?? 50;
        }

        /// <summary>Добавить Духовные Камни. Публикует CurrencyChangedEvent.</summary>
        public void Add(int amount)
        {
            EnsureInitialized();
            if (amount <= 0) return;

            _spiritStones += amount;
            _changedPub.Publish(new CurrencyChangedEvent(_spiritStones, amount));
        }

        /// <summary>
        /// Потратить Духовные Камни. Возвращает false, если не хватало.
        /// Публикует CurrencyChangedEvent только при успехе.
        /// </summary>
        public bool Spend(int amount)
        {
            EnsureInitialized();
            if (amount <= 0) return false;
            if (_spiritStones < amount) return false;

            _spiritStones -= amount;
            _changedPub.Publish(new CurrencyChangedEvent(_spiritStones, -amount));
            return true;
        }

        /// <summary>Установить баланс напрямую (для загрузки сейва).</summary>
        public void SetBalance(int spiritStones)
        {
            _initialized = true;
            int delta = spiritStones - _spiritStones;
            _spiritStones = spiritStones;
            _changedPub.Publish(new CurrencyChangedEvent(_spiritStones, delta));
        }
    }
}

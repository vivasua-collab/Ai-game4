#nullable enable
// Создано: 2026-08-25 — NPC_COMBAT_PREP Phase 4: реализация ICurrencyService.
// Духовные камни игрока (UI-2). Баланс — int (ЗАПРЕТ 3.9).
// CurrencyChangedEvent уже существует в PlayerContracts.cs — публикуем его.
//
// R17 (аудит-0911 TRD-1): кошелёк НЕ сохранялся (класс без ISaveable;
// SetBalance «для загрузки сейва» — 0 вызывов). LoadGame восстанавливал
// инвентарь, но баланс — дефолтные 50 → дюп камней (купил→сейв→лоад:
// предметы на месте И камни вернулись) / потеря заработанного. Теперь:
// блок "currency" + IWorldResettable (ленивая ре-инициализация стартового
// баланса при пересборке мира).
using System;
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
    public sealed class CurrencyService : ICurrencyService, ISaveable, IWorldResettable
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
        /// <remarks>
        /// P2-33 (аудит 09.22 12:00, Фаза 12): насыщающая арифметика —
        /// int-переполнение больше не делает баланс отрицательным
        /// (прежде _spiritStones += amount при MaxValue-крае заворачивало
        /// в минус и уезжало в UI/торговлю/сейв; то же семейство, что и
        /// Qi-арифметика Фазы 9). Копятся до представимого максимума.
        /// </remarks>
        public void Add(int amount)
        {
            EnsureInitialized();
            if (amount <= 0) return;

            // P2-33: long-промежуток + клэмп (saturating add).
            long candidate = (long)_spiritStones + amount;
            _spiritStones = candidate > int.MaxValue ? int.MaxValue : (int)candidate;
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
        /// <remarks>P2-33: минусовой/овермаксимальный баланс невозможен и прямым вызовом.</remarks>
        public void SetBalance(int spiritStones)
        {
            _initialized = true;
            int clamped = Math.Clamp(spiritStones, 0, int.MaxValue);
            int delta = clamped - _spiritStones;
            _spiritStones = clamped;
            _changedPub.Publish(new CurrencyChangedEvent(_spiritStones, delta));
        }

        // ══════════════════════════════════════════════════════════════
        // R17 (TRD-1): ISaveable — блок "currency"
        // ══════════════════════════════════════════════════════════════

        /// <summary>Типизированный state-блок для round-trip десериализации.</summary>
        public sealed class CurrencySaveState
        {
            public int SpiritStones;
        }

        public string SaveKey => "currency";
        public Type StateType => typeof(CurrencySaveState);

        public object CaptureState()
        {
            EnsureInitialized();
            return new CurrencySaveState { SpiritStones = _spiritStones };
        }

        public void RestoreState(object state)
        {
            if (state is not CurrencySaveState data || data == null) return;
            // SetBalance публикует CurrencyChangedEvent → HUD/лавка обновятся.
            SetBalance(Math.Max(0, data.SpiritStones));
            Console.WriteLine($"[CurrencyService] RestoreState: баланс {data.SpiritStones} камней");
        }

        // R17 (E-1): IWorldResettable — пересборка мира = новый практик со
        // стартовым балансом. Сброс ленивой инициализации + событие для HUD.
        public void ResetWorld()
        {
            _initialized = false;
            EnsureInitialized();
            _changedPub.Publish(new CurrencyChangedEvent(_spiritStones, 0));
        }
    }
}

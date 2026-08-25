#nullable enable
// Создано: 2026-08-25 — NPC_COMBAT_PREP Phase 4: точка входа модуля Trade.
// Подписка TradeRequestedEvent (от DialogueService) → ITradeService.OpenTrade.
// Торговля не имеет per-tick работы; Tick используется только headless-хуком
// GODOT_TRADE_DEBUG=1 (паттерн GODOT_COMBAT_SIM / GODOT_FORMATION_TEST):
// после спавна мира открывает лавку первого Merchant-NPC, покупает самый
// дешёвый товар, продаёт его обратно и закрывает лавку — smoke-тест флоу.
using System;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Trade
{
    /// <summary>
    /// Точка входа модуля торговли. Мост DialogueService → TradeService
    /// через шину (EVT-01: модули не знают друг о друге напрямую).
    /// </summary>
    public class TradeModule : IModule
    {
        [Inject] private readonly ITradeService _tradeService = null!;
        [Inject] private readonly INPCService _npcService = null!;
        [Inject] private readonly IItemDatabaseService _itemDb = null!;

        [Inject] private readonly ISubscriber<TradeRequestedEvent> _tradeRequestedSub = null!;

        private IDisposable? _tradeRequestedSubscription;

        // === Headless smoke-тест (GODOT_TRADE_DEBUG=1) ===
        private bool _tradeDebugEnabled;
        private bool _tradeDebugDone;
        // GODOT_TRADE_HOLD=1 — не закрывать лавку после smoke-теста
        // (для скриншотов окна торговли: GODOT_SCREENSHOT_DELAY ~4-6с).
        private bool _tradeDebugHold;
        // Торговец появляется после сборки сцены (GameSession.NewGame → фазы);
        // ждём не дольше 60 тиков (~60 игровых минут), затем сдаёмся.
        private const int TradeDebugTimeoutTicks = 60;

        public string ModuleName => "Trade";

        public void Start()
        {
            _tradeRequestedSubscription?.Dispose();
            _tradeRequestedSubscription = _tradeRequestedSub.Subscribe(OnTradeRequested);

            _tradeDebugEnabled =
                Environment.GetEnvironmentVariable("GODOT_TRADE_DEBUG") == "1";
            _tradeDebugHold =
                Environment.GetEnvironmentVariable("GODOT_TRADE_HOLD") == "1";

            Console.WriteLine("[TradeModule] Started");
        }

        public void Tick(int tickCount)
        {
            // Обычной per-tick работы нет. Headless smoke-тест: ждём Merchant-NPC
            // (сцена собирается в первые секунды), прогоняем buy/sell один раз.
            if (_tradeDebugEnabled && !_tradeDebugDone)
            {
                if (tickCount > TradeDebugTimeoutTicks)
                {
                    Console.WriteLine("[TradeDebug] Торговец так и не появился — smoke-тест пропущен");
                    _tradeDebugDone = true;
                    return;
                }

                string? merchant = FindMerchantNpc();
                if (merchant != null)
                {
                    _tradeDebugDone = true;
                    RunTradeDebug(merchant);
                }
            }
        }

        public void Dispose()
        {
            _tradeRequestedSubscription?.Dispose();
            _tradeRequestedSubscription = null;
        }

        /// <summary>Выбор «Покажи товары» в диалоге → открыть лавку этого NPC.</summary>
        private void OnTradeRequested(in TradeRequestedEvent e)
        {
            if (string.IsNullOrEmpty(e.NpcId)) return;
            _tradeService.OpenTrade(e.NpcId);
        }

        // === Headless smoke-тест ===

        private string? FindMerchantNpc()
        {
            if (_npcService == null) return null;
            foreach (var id in _npcService.GetAllNPCIds())
            {
                var state = _npcService.GetNPCState(id);
                if (state != null && state.IsAlive && state.Role == NPCRole.Merchant)
                    return id;
            }
            return null;
        }

        /// <summary>
        /// Smoke-тест лавки: OpenTrade → TryBuy (самый дешёвый товар) →
        /// TrySell (продать его обратно) → CloseTrade. Каждый шаг логируется.
        /// </summary>
        private void RunTradeDebug(string npcId)
        {
            Console.WriteLine($"[TradeDebug] Открытие лавки: {npcId}");
            _tradeService.OpenTrade(npcId);

            var stock = _tradeService.GetMerchantStock(npcId);
            Console.WriteLine($"[TradeDebug] Ассортимент: {stock.Count} позиций");

            // Самый дешёвый товар в наличии.
            string? cheapestId = null;
            int cheapestPrice = int.MaxValue;
            foreach (var entry in stock)
            {
                if (entry.Count <= 0) continue;
                int price = _tradeService.GetBuyPrice(entry.ItemId);
                if (price > 0 && price < cheapestPrice)
                {
                    cheapestPrice = price;
                    cheapestId = entry.ItemId;
                }
            }

            if (cheapestId != null)
            {
                string name = _itemDb.TryGetItem(cheapestId, out var item) ? item.NameRu : cheapestId;
                bool bought = _tradeService.TryBuy(npcId, cheapestId, 1);
                Console.WriteLine($"[TradeDebug] TryBuy '{name}' ({cheapestId}, {cheapestPrice} камней): {bought}");

                bool sold = _tradeService.TrySell(npcId, cheapestId, 1);
                Console.WriteLine($"[TradeDebug] TrySell '{name}' обратно: {sold}");
            }
            else
            {
                Console.WriteLine("[TradeDebug] Ассортимент пуст — buy/sell пропущены");
            }

            if (_tradeDebugHold)
            {
                Console.WriteLine("[TradeDebug] Лавка оставлена открытой (GODOT_TRADE_HOLD=1)");
                return;
            }

            _tradeService.CloseTrade();
            Console.WriteLine("[TradeDebug] Лавка закрыта — smoke-тест завершён");
        }
    }
}

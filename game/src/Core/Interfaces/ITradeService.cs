#nullable enable
// Создано: 2026-08-25 — NPC_COMBAT_PREP Phase 4: интерфейс сервиса торговли.
// Духовные камни — int (ЗАПРЕТ 3.9: целочисленная арифметика).
using System.Collections.Generic;
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Сервис торговли с NPC-торговцами.
    /// Управляет сессией (OpenTrade/CloseTrade), ассортиментом мерчанта
    /// и сделками покупки/продажи за духовные камни (ICurrencyService).
    ///
    /// События (TradeContracts.cs):
    ///   - TradeRequestedEvent (подписка) — диалог просит открыть лавку;
    ///   - TradeOpenedEvent / TradeClosedEvent (публикация) — жизненный цикл;
    ///   - TradeCompletedEvent / TradeFailedEvent (публикация) — сделки.
    /// </summary>
    public interface ITradeService
    {
        /// <summary>NpcId торговца текущей сессии (null — торговля не открыта).</summary>
        string? ActiveMerchantId { get; }

        /// <summary>true — сессия торговли открыта.</summary>
        bool IsTrading { get; }

        /// <summary>
        /// Открыть торговлю с NPC. Валидирует роль/диспозицию торговца,
        /// при первом обращении генерирует ассортимент (детерминированный
        /// сид от npcId), публикует TradeOpenedEvent.
        /// </summary>
        void OpenTrade(string npcId);

        /// <summary>
        /// Закрыть сессию торговли. Публикует TradeClosedEvent
        /// (единая точка резюма тиков для GameWorldController).
        /// </summary>
        void CloseTrade();

        /// <summary>
        /// Ассортимент торговца (снимок-копия для UI).
        /// Генерируется лениво при первом запросе, если ещё не создан.
        /// </summary>
        IReadOnlyList<MerchantStockEntry> GetMerchantStock(string npcId);

        /// <summary>
        /// Купить предметы у торговца: проверка остатка, стоимости
        /// (SpiritStones), вместимости инвентаря; списание камней,
        /// добавление предметов, публикация TradeCompletedEvent.
        /// </summary>
        bool TryBuy(string npcId, string itemId, int count);

        /// <summary>
        /// Продать предметы торговцу: проверка наличия в инвентаре игрока,
        /// списание предметов, начисление камней (цена продажи),
        /// публикация TradeCompletedEvent.
        /// </summary>
        bool TrySell(string npcId, string itemId, int count);

        /// <summary>
        /// Цена покупки (за 1 шт.): Value × MarkupPermil / 1000, минимум 1.
        /// </summary>
        int GetBuyPrice(string itemId);

        /// <summary>
        /// Цена продажи (за 1 шт.): Value × SellPermil / 1000, минимум 0.
        /// </summary>
        int GetSellPrice(string itemId);
    }
}

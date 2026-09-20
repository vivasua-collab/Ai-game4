#nullable enable
// Создано: 2026-09-20 — R22-1: СТАБ реализации ICurrencyExchangeService.
// Чистая математика котировок (Permil, ЗАПРЕТ 3.9) — без кошелька золота,
// без проводки в TradeService/UI (будущий эпизод по плану аудита R21-C).
// Все числа — из EconomyConstants (канон 2026-09-20: 1 камень = 100 золота).
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Trade
{
    /// <summary>
    /// СТАБ обмена валют: золото ⇄ духовные камни.
    /// Даёт котировки по каноническому курсу (EconomyConstants).
    /// НЕ выполняет сделки: выполнение + гейт «только культиватор-торговец»
    /// + золотой кошелёк — будущие эпизоды (ECONOMY_SYSTEM §7).
    /// </summary>
    public sealed class CurrencyExchangeService : ICurrencyExchangeService
    {
        /// <summary>Базовый курс: золота за 1 камень (=100).</summary>
        public int GoldPerSpiritStone => EconomyConstants.GoldPerSpiritStone;

        /// <summary>
        /// Котировка продажи камней игроком: ×500‰ от курса.
        /// Пример: 10 камней → 10 × 100 × 0.5 = 500 золота.
        /// </summary>
        public ExchangeQuote QuoteStonesToGold(int stones)
        {
            if (stones <= 0)
                return new ExchangeQuote(0, 0, EconomyConstants.StoneSellPermil);

            int gold = Permil.Apply(
                stones * EconomyConstants.GoldPerSpiritStone,
                EconomyConstants.StoneSellPermil);
            return new ExchangeQuote(gold, stones, EconomyConstants.StoneSellPermil);
        }

        /// <summary>
        /// Котировка покупки камней игроком: ×1200‰ от курса.
        /// Пример: 10 камней → 10 × 100 × 1.2 = 1200 золота.
        /// </summary>
        public ExchangeQuote QuoteGoldToStones(int stones)
        {
            if (stones <= 0)
                return new ExchangeQuote(0, 0, EconomyConstants.StoneBuyMarkupPermil);

            int gold = Permil.Apply(
                stones * EconomyConstants.GoldPerSpiritStone,
                EconomyConstants.StoneBuyMarkupPermil);
            return new ExchangeQuote(gold, stones, EconomyConstants.StoneBuyMarkupPermil);
        }

        /// <summary>
        /// Правило «смертным не владеть камнями»: торговец-смертный
        /// (CultivationLevel.None) камнями не торгует — только золотом.
        /// </summary>
        public bool CanMerchantTradeStones(CultivationLevel merchantLevel)
        {
            return EconomyConstants.CanHoldSpiritStones(merchantLevel);
        }
    }
}

#nullable enable
using CultivationGame.Core.Data;
// Создано: 2026-09-20 — R22-1: интерфейс СТАБА обмена валют (репорт 20.09 №5).
// Двухвалютная экономика: золото (смертные) ⇄ духовные камни (культиваторы).
// Курс канонический: 1 камень = 100 золота (EconomyConstants).
// Статус: СТАБ — котировки доступны сразу (чистая математика), проводка
// в торговлю/лавку/UI — будущий эпизод (план аудита R21-C).

namespace CultivationGame.Core.Interfaces
{
    /// <summary>Котировка обмена валют (результат расчёта, immutable).</summary>
    /// <param name="Gold">Итоговое количество золота в котировке (0 при непредставимой котировке).</param>
    /// <param name="Stones">Количество камней в котировке.</param>
    /// <param name="RatePermil">Применённый коэффициент к курсу (‰).</param>
    /// <param name="isRepresentable">
    /// BUG-020 (Этап-6 аудита): false — котировка непредставима в int
    /// (произведение stones × курс × коэффициент выше int.MaxValue).
    /// Явный отказ вместо молча неверного (прежде — отрицательного) числа.
    /// </param>
    public readonly struct ExchangeQuote(int gold, int stones, int ratePermil, bool isRepresentable = true)
    {
        /// <summary>Итоговое количество золота (0 при непредставимой котировке).</summary>
        public int Gold { get; } = gold;

        /// <summary>Количество камней.</summary>
        public int Stones { get; } = stones;

        /// <summary>Применённый коэффициент к базовому курсу (1200‰/500‰).</summary>
        public int RatePermil { get; } = ratePermil;

        /// <summary>
        /// Котировка представима в int (результат достоверен).
        /// false = сделка такого объёма не может быть рассчитана валютой int —
        /// вызывающий должен отказаться (разбить партию или отклонить).
        /// </summary>
        public bool IsRepresentable { get; } = isRepresentable;

        /// <summary>Фабрика непредставимой котировки: Gold=0, IsRepresentable=false.</summary>
        public static ExchangeQuote Unrepresentable(int stones, int ratePermil)
            => new(0, stones, ratePermil, false);
    }

    /// <summary>
    /// СТАБ обмена валют: золото ⇄ духовные камни.
    /// Спецификация: docs/docs_v2/06_player/ECONOMY_SYSTEM.md.
    /// Обмен в игре будет доступен ТОЛЬКО у культиваторов-торговцев
    /// (Role == Merchant И CultivationLevel != None).
    /// </summary>
    public interface ICurrencyExchangeService
    {
        /// <summary>Базовый курс: золота за 1 духовный камень (=100).</summary>
        int GoldPerSpiritStone { get; }

        /// <summary>
        /// Котировка ПРОДАЖИ камней игроком: золото = stones × курс × 500‰.
        /// Смертный торговец такую сделку не примет (CanMerchantTradeStones).
        /// BUG-020: непредставимые объёмы (результат > int.MaxValue) дают
        /// ExchangeQuote.IsRepresentable=false — явный отказ, не неверное число.
        /// </summary>
        /// <param name="stones">Количество продаваемых камней (> 0).</param>
        ExchangeQuote QuoteStonesToGold(int stones);

        /// <summary>
        /// Котировка ПОКУПКИ камней игроком: золото = stones × курс × 1200‰.
        /// BUG-020: непредставимые объёмы — ExchangeQuote.IsRepresentable=false.
        /// </summary>
        /// <param name="stones">Количество покупаемых камней (> 0).</param>
        ExchangeQuote QuoteGoldToStones(int stones);

        /// <summary>
        /// Может ли торговец работать с камнями Ци.
        /// Правило «смертным не владеть камнями»: только культиватор-торговец.
        /// </summary>
        /// <param name="merchantLevel">Уровень культивации торговца.</param>
        bool CanMerchantTradeStones(CultivationLevel merchantLevel);
    }
}

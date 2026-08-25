#nullable enable
// Создано: 2026-08-25 — NPC_COMBAT_PREP Phase 4: делегат регистрации модуля Trade.
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Trade
{
    /// <summary>
    /// Делегат регистрации публичных сервисов модуля Trade.
    /// </summary>
    public static class TradeModuleServices
    {
        public static void Register(IContainerBuilder builder)
        {
            // === Публичные сервисы ===
            // Register<ICurrencyService, CurrencyService> также форвардит
            // конкретный тип CurrencyService на тот же синглтон (Container
            // регистрирует оба ключа одной Registration).
            builder.Register<ICurrencyService, CurrencyService>(Lifetime.Singleton);
            builder.Register<ITradeService, TradeService>(Lifetime.Singleton);

            // === Конфигурация по умолчанию ===
            var defaultConfig = new TradeConfig();
            builder.RegisterInstance(defaultConfig);

            // === Точка входа модуля ===
            builder.Register<TradeModule>(Lifetime.Singleton);
        }
    }
}

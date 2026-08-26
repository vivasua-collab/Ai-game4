#nullable enable
// Создано: 2026-08-25 — NPC_COMBAT_PREP Phase 4: конфигурация модуля Trade.
// BD-48: class (ссылочный тип, инжектится через RegisterInstance).
namespace CultivationGame.Modules.Trade
{
    /// <summary>
    /// Конфигурация модуля торговли.
    /// Все шансы/цены — целочисленные (ЗАПРЕТ 3.9): наценки в промилле.
    /// </summary>
    public class TradeConfig
    {
        // === Ценообразование (промилле от базовой Value предмета) ===

        /// <summary>Наценка покупки: 1200‰ = ×1.2 (торговец продаёт дороже базы).</summary>
        public int MarkupPermil = 1200;

        /// <summary>Коэффициент продажи: 500‰ = ×0.5 (торговец покупает вдвое дешевле).</summary>
        public int SellPermil = 500;

        /// <summary>Стартовый баланс игрока в духовных камнях.</summary>
        public int StartStones = 50;

        // === Ассортимент (кол-во позиций при генерации) ===

        /// <summary>Минимум видов оружия в лавке.</summary>
        public int StockWeaponMin = 1;

        /// <summary>Максимум видов оружия в лавке.</summary>
        public int StockWeaponMax = 2;

        /// <summary>Минимум видов брони в лавке.</summary>
        public int StockArmorMin = 1;

        /// <summary>Максимум видов брони в лавке.</summary>
        public int StockArmorMax = 2;

        /// <summary>Минимум видов расходников в лавке.</summary>
        public int StockConsumableMin = 3;

        /// <summary>Максимум видов расходников в лавке.</summary>
        public int StockConsumableMax = 4;

        /// <summary>Количество стопок материалов в лавке.</summary>
        public int StockMaterialCount = 1;

        /// <summary>Мин. количество в стопке расходника.</summary>
        public int ConsumableStackMin = 2;

        /// <summary>Макс. количество в стопке расходника.</summary>
        public int ConsumableStackMax = 5;

        /// <summary>Мин. количество в стопке материала.</summary>
        public int MaterialStackMin = 3;

        /// <summary>Макс. количество в стопке материала.</summary>
        public int MaterialStackMax = 8;
    }
}

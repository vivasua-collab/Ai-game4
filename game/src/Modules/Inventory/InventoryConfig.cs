#nullable enable
// Создано: 2026-05-09 00:00:00 UTC
// Конфигурация модуля инвентаря.
// BD-48 урок: Config — class, не struct (mutable struct risk).
using System.Collections.Generic;
using CultivationGame.Modules.Inventory.Data;

namespace CultivationGame.Modules.Inventory
{
    /// <summary>
    /// Конфигурация модуля инвентаря.
    /// BD-48: class, не struct.
    /// </summary>
    public class InventoryConfig
    {
        /// <summary>
        /// Максимальный переносимый вес (кг) — 50 per user request 2026-08-22
        /// (владельческий канон; P2-36 отклонён by-user — см. §3.3-примечание
        /// INVENTORY_SYSTEM и чекпоинт 09_22_r36b).
        /// </summary>
        public float MaxCarryWeight = 50f;

        /// <summary>
        /// Максимальный объём рюкзака (литры) — 100 per user request 2026-08-22
        /// (владельческий канон; P2-36 отклонён by-user).
        /// </summary>
        public float MaxCarryVolume = 100f;

        /// <summary>Вместимость духовного хранилища (слоты)</summary>
        public int SpiritStorageCapacity = 20;

        /// <summary>Вместимость кольца хранения (слоты)</summary>
        public int RingStorageCapacity = 10;

        /// <summary>Рецепты крафта по умолчанию (recipeId → рецепт)</summary>
        public Dictionary<string, CraftingRecipe> Recipes = new();
    }
}

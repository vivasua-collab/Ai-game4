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
        /// <summary>Максимальный переносимый вес (кг)</summary>
        public float MaxCarryWeight = 50f;

        /// <summary>Максимальный объём рюкзака (литры)</summary>
        public float MaxCarryVolume = 100f;

        /// <summary>Вместимость духовного хранилища (слоты)</summary>
        public int SpiritStorageCapacity = 20;

        /// <summary>Вместимость кольца хранения (слоты)</summary>
        public int RingStorageCapacity = 10;

        /// <summary>Рецепты крафта по умолчанию (recipeId → рецепт)</summary>
        public Dictionary<string, CraftingRecipe> Recipes = new();
    }
}

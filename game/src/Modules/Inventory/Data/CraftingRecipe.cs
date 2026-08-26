#nullable enable
// Создано: 2026-05-09 00:00:00 UTC
// Данные рецепта крафта — модульная архитектура.
// Заменяет legacy CraftingController.cs (776 LOC) — упрощённая модель.
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Inventory.Data
{
    /// <summary>
    /// Рецепт крафта.
    /// Описывает: из чего → что → сколько.
    /// </summary>
    public class CraftingRecipe
    {
        /// <summary>Уникальный ID рецепта</summary>
        public string RecipeId;

        /// <summary>Название рецепта</summary>
        public string Name;

        /// <summary>ID результата крафта</summary>
        public string ResultItemId;

        /// <summary>Количество результатов</summary>
        public int ResultCount = 1;

        /// <summary>Требуемые материалы (itemId → количество)</summary>
        public Dictionary<string, int> Ingredients = new();

        /// <summary>Минимальный уровень культивации для крафта</summary>
        public int RequiredCultivationLevel = 0;

        /// <summary>Длительность крафта (тики игрового времени)</summary>
        public int CraftTime = 1;

        /// <summary>Является ли рецепт доступным (для условных рецептов)</summary>
        public bool IsAvailable = true;

        /// <summary>
        /// Проверить, достаточно ли ингредиентов в инвентаре.
        /// </summary>
        public bool HasIngredients(IInventoryService inventory)
        {
            foreach (var ingredient in Ingredients)
            {
                if (inventory.GetItemCount(ingredient.Key) < ingredient.Value)
                    return false;
            }
            return true;
        }
    }
}

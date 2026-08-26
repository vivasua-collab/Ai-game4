#nullable enable
// Создано: 2026-05-09 00:00:00 UTC
// Реализация ICraftingService.
// Заменяет legacy CraftingController.cs (776 LOC) — упрощённая модель.
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Modules.Inventory.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Inventory
{
    /// <summary>
    /// Реализация ICraftingService.
    /// Управляет крафтом: проверка рецептов, расход материалов, создание предметов.
    /// </summary>
    public class CraftingService : ICraftingService
    {
        // === Зависимости (DI через конструктор) ===
        private readonly IInventoryService _inventoryService;
        private readonly IPublisher<CraftCompletedEvent> _craftCompletedPub;
        private readonly IPublisher<CraftFailedEvent> _craftFailedPub;

        // === Состояние ===
        private readonly Dictionary<string, CraftingRecipe> _recipes = new();

        // === Конструктор (VContainer) ===
        public CraftingService(
            IInventoryService inventoryService,
            IPublisher<CraftCompletedEvent> craftCompletedPub,
            IPublisher<CraftFailedEvent> craftFailedPub)
        {
            _inventoryService = inventoryService;
            _craftCompletedPub = craftCompletedPub;
            _craftFailedPub = craftFailedPub;
        }

        /// <summary>
        /// Зарегистрировать рецепт крафта.
        /// Вызывается из InventoryModule.Configure().
        /// </summary>
        public void RegisterRecipe(CraftingRecipe recipe)
        {
            if (recipe == null || string.IsNullOrEmpty(recipe.RecipeId)) return;
            _recipes[recipe.RecipeId] = recipe;
        }

        /// <summary>
        /// Зарегистрировать несколько рецептов.
        /// </summary>
        public void RegisterRecipes(Dictionary<string, CraftingRecipe> recipes)
        {
            if (recipes == null) return;
            foreach (var kvp in recipes)
            {
                RegisterRecipe(kvp.Value);
            }
        }

        // === ICraftingService ===

        public bool CanCraft(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId)) return false;

            if (!_recipes.TryGetValue(recipeId, out var recipe))
            {
                return false;
            }

            if (!recipe.IsAvailable) return false;

            // Проверка ингредиентов
            return recipe.HasIngredients(_inventoryService);
        }

        public bool TryCraft(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId)) return false;

            if (!_recipes.TryGetValue(recipeId, out var recipe))
            {
                _craftFailedPub.Publish(new CraftFailedEvent(recipeId, "Рецепт не найден"));
                return false;
            }

            if (!recipe.IsAvailable)
            {
                _craftFailedPub.Publish(new CraftFailedEvent(recipeId, "Рецепт недоступен"));
                return false;
            }

            // Проверка ингредиентов
            if (!recipe.HasIngredients(_inventoryService))
            {
                _craftFailedPub.Publish(new CraftFailedEvent(recipeId, "Недостаточно ингредиентов"));
                return false;
            }

            // Расход ингредиентов
            foreach (var ingredient in recipe.Ingredients)
            {
                if (!_inventoryService.TryRemoveItem(ingredient.Key, ingredient.Value))
                {
                    // Это не должно произойти, т.к. HasIngredients проверил
                    _craftFailedPub.Publish(new CraftFailedEvent(recipeId, $"Не удалось израсходовать {ingredient.Key}"));
                    return false;
                }
            }

            // Добавление результата
            // В будущих фазах: загрузка ItemData по ResultItemId из базы предметов
            // Пока — публикуем событие завершения крафта
            _craftCompletedPub.Publish(new CraftCompletedEvent(recipeId, recipe.ResultItemId, recipe.ResultCount));
            return true;
        }
    }
}

#nullable enable
// Создано: 2026-05-09 00:00:00 UTC
// Реализация ICraftingService.
// Заменяет legacy CraftingController.cs (776 LOC) — упрощённая модель.
using System;
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
        // Review этап 4 (P1-4): валидация результата ДО расхода ингредиентов
        // (транзакция: крафт неизвестного базе предмета не должен уничтожать материалы).
        private readonly IItemDatabaseService? _itemDatabase;

        // === Состояние ===
        private readonly Dictionary<string, CraftingRecipe> _recipes = new();

        // === Конструктор (VContainer) ===
        public CraftingService(
            IInventoryService inventoryService,
            IPublisher<CraftCompletedEvent> craftCompletedPub,
            IPublisher<CraftFailedEvent> craftFailedPub,
            IItemDatabaseService? itemDatabase = null)
        {
            _inventoryService = inventoryService;
            _craftCompletedPub = craftCompletedPub;
            _craftFailedPub = craftFailedPub;
            _itemDatabase = itemDatabase;
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

            // Review этап 4 (P1-4): результат должен быть известен базе ДО
            // расхода ингредиентов — иначе материалы уничтожены, а предмет
            // «потерян» в OnCraftCompleted (не найден в ItemDatabase).
            if (_itemDatabase != null
                && !_itemDatabase.TryGetItem(recipe.ResultItemId, out _))
            {
                _craftFailedPub.Publish(new CraftFailedEvent(recipeId,
                    $"Результат '{recipe.ResultItemId}' не найден в ItemDatabase"));
                return false;
            }

            // Проверка ингредиентов
            if (!recipe.HasIngredients(_inventoryService))
            {
                _craftFailedPub.Publish(new CraftFailedEvent(recipeId, "Недостаточно ингредиентов"));
                return false;
            }

            // Расход ингредиентов.
            // ST9-01 (Этап-9 аудита, 23.10): транзакционный расход. Прежде при
            // неудаче mid-loop ранее списанные ингредиенты НЕ откатывались —
            // частичная потеря материалов. При текущей модели событий путь
            // латентный (ItemRemovedEvent никто не слушает, между HasIngredients
            // и удалениями мутаторов нет), НО любой будущий реактивный подписчик
            // ItemRemovedEvent (синхронно изымающий предметы) делает его
            // достижимым — воспроизведено Stage9InventoryTruthSimDebug A4b
            // (hostile-подписчик: wood 10→7 при отказавшем крафте).
            // Теперь: фиксируем списанное, при любом отказе — полный возврат.
            var removedSoFar = new List<KeyValuePair<string, int>>(recipe.Ingredients.Count);
            foreach (var ingredient in recipe.Ingredients)
            {
                if (!_inventoryService.TryRemoveItem(ingredient.Key, ingredient.Value))
                {
                    // Это не должно произойти, т.к. HasIngredients проверил
                    // (за исключением реактивной мутации между шагами).
                    RollbackRemovedIngredients(removedSoFar, recipeId);
                    _craftFailedPub.Publish(new CraftFailedEvent(recipeId, $"Не удалось израсходовать {ingredient.Key}"));
                    return false;
                }
                removedSoFar.Add(ingredient);
            }

            // Добавление результата
            // В будущих фазах: загрузка ItemData по ResultItemId из базы предметов
            // Пока — публикуем событие завершения крафта
            _craftCompletedPub.Publish(new CraftCompletedEvent(recipeId, recipe.ResultItemId, recipe.ResultCount));
            return true;
        }

        /// <summary>
        /// ST9-01: откат списанных ингредиентов при mid-loop отказе расхода.
        /// Best-effort с громкой диагностикой: предметы только что покинули
        /// инвентарь (объём освободён), TryAddItem обязан вместить их обратно;
        /// теоретический отказ (враждебный мутатор забил объём между шагами)
        /// не должен проходить молча — предмет теряется ТОЛЬКО с ошибкой в лог.
        /// Источник ItemSources.Craft: транзакция крафта, net-zero откат
        /// (QuestProgressTracker различает поступление: craft ≠ harvest).
        /// </summary>
        private void RollbackRemovedIngredients(List<KeyValuePair<string, int>> removed, string recipeId)
        {
            if (removed.Count == 0) return;
            foreach (var r in removed)
            {
                if (_itemDatabase != null && _itemDatabase.TryGetItem(r.Key, out var item) && item != null)
                {
                    if (!_inventoryService.TryAddItem(item, r.Value, out int returned, ItemSources.Craft)
                        || returned < r.Value)
                    {
                        Console.WriteLine($"[CraftingService] ST9-01 ROLLBACK FAIL: '{r.Key}' возвращено " +
                                          $"{returned}/{r.Value} (рецепт '{recipeId}') — ДИАГНОСТИКА: проверь подписчиков ItemRemovedEvent");
                    }
                }
                else
                {
                    Console.WriteLine($"[CraftingService] ST9-01 ROLLBACK FAIL: предмет '{r.Key}' " +
                                      $"не резолвится каталогом (рецепт '{recipeId}') — вернуть невозможно");
                }
            }
            Console.WriteLine($"[CraftingService] ST9-01: откат {removed.Count} ингредиентов рецепта '{recipeId}' (транзакция крафта прервана)");
        }
    }
}

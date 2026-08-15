#nullable enable
using CultivationGame.Core;
using CultivationGame.Core.Data;
// Создано: 2026-05-10 07:36:53 UTC
// Аудит P0-02: вынесен из IInventoryService.cs (1 интерфейс = 1 файл)
namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Интерфейс крафта предметов.
    /// </summary>
    public interface ICraftingService
    {
        bool CanCraft(string recipeId);
        bool TryCraft(string recipeId);
    }
}

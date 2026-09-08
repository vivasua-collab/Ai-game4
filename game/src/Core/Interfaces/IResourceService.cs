#nullable enable
// Создано: 2026-05-08 19:38:52 UTC
// Редактировано: 2026-05-09 03:25:00 UTC — добавлен Harvest в интерфейс (FIX-1: устранение каста в TileMapService)
using CultivationGame.Core.Data;
using CultivationGame.Core.Messaging.Contracts;

using CultivationGame.Core;
namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Интерфейс сервиса ресурсов.
    /// Управление спавном, сбором и подбором ресурсов.
    /// Источник: TILE_SYSTEM.md, plan_02_tile.md
    /// </summary>
    public interface IResourceService
    {
        /// <summary>Попытаться разместить ресурс на тайле</summary>
        bool TrySpawnResource(int x, int y, string resourceId);

        /// <summary>
        /// Review этап 6 (P2-5): команда запроса подбора предмета (БЫЛО
        /// TryPickup(resourceId, out ItemData) — out всегда null! при true:
        /// «успех» не означал выдачу предмета). Команда асинхронна по своей
        /// природе: InventoryModule реагирует на ItemAddRequestEvent.
        /// </summary>
        bool RequestPickup(string itemId);

        /// <summary>Собрать ресурс с тайла. Возвращает результат сбора.</summary>
        HarvestResult Harvest(int x, int y, in GameTile tile);

        /// <summary>Зарегистрировать истощённый ресурс для респауна</summary>
        void RegisterDepletedResource(int x, int y, in GameTile tile);
    }
}

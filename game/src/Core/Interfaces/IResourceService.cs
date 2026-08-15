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

        /// <summary>Попытаться подобрать ресурс как предмет</summary>
        bool TryPickup(string resourceId, out ItemData item);

        /// <summary>Собрать ресурс с тайла. Возвращает результат сбора.</summary>
        HarvestResult Harvest(int x, int y, in GameTile tile);

        /// <summary>Зарегистрировать истощённый ресурс для респауна</summary>
        void RegisterDepletedResource(int x, int y, in GameTile tile);
    }
}

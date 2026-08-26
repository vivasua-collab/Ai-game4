#nullable enable
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Messaging.Contracts;
// Создано: 2026-05-08 19:38:52 UTC
// Редактировано: 2026-05-09 15:54:26 UTC — добавлен using CultivationGame.Core.Messaging для HarvestResult

namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Интерфейс сервиса тайловой карты.
    /// Управление сеткой тайлов, сбор ресурсов, проходимость.
    /// Источник: TILE_SYSTEM.md, plan_02_tile.md
    /// </summary>
    public interface ITileService
    {
        /// <summary>Получить данные тайла по координатам</summary>
        GameTile GetTile(int x, int y);

        /// <summary>Установить данные тайла по координатам</summary>
        void SetTile(int x, int y, in GameTile data);

        /// <summary>Попытаться собрать ресурс с тайла</summary>
        bool TryHarvest(int x, int y, out HarvestResult result);

        /// <summary>Проверить проходимость тайла</summary>
        bool IsWalkable(int x, int y);

        /// <summary>Ширина карты</summary>
        int MapWidth { get; }

        /// <summary>Высота карты</summary>
        int MapHeight { get; }

        /// <summary>
        /// Сгенерировать тайловую карту. Ai-game3 compatibility —
        /// вызывается из TileModule / TileMapGenPhase.
        /// </summary>
        void Generate(int seed, int width, int height, TerrainType baseTerrain);
    }
}

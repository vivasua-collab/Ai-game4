#nullable enable
using System.Collections.Generic;
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Сервис выпавших на землю предметов.
    /// Хранит список предметов, лежащих на земле в мире.
    /// Управляет drop (выпадение) и pickup (подбор).
    /// </summary>
    public interface IGroundItemService
    {
        /// <summary>Количество предметов на земле.</summary>
        int Count { get; }

        /// <summary>
        /// Выбросить предмет на землю в указанной позиции.
        /// Создаёт GroundItem, публикует ItemDroppedEvent.
        /// </summary>
        /// <param name="itemId">ID предмета</param>
        /// <param name="count">Количество</param>
        /// <param name="worldX">Мировая X (пиксели)</param>
        /// <param name="worldY">Мировая Y (пиксели)</param>
        /// <returns>Уникальный ID выпавшего предмета (0 = ошибка)</returns>
        long DropItem(string itemId, int count, float worldX, float worldY);

        /// <summary>
        /// Подобрать ближайший к позиции предмет (в радиусе maxDistance).
        /// Публикует ItemPickedUpEvent + ItemAddRequestEvent.
        /// </summary>
        /// <param name="worldX">Позиция игрока X</param>
        /// <param name="worldY">Позиция игрока Y</param>
        /// <param name="maxDistance">Максимальная дистанция (пиксели)</param>
        /// <returns>true если подобран</returns>
        bool TryPickupNearest(float worldX, float worldY, float maxDistance);

        /// <summary>
        /// Получить все выпавшие предметы (для рендерера).
        /// </summary>
        IReadOnlyList<GroundItem> GetAllGroundItems();
    }

    /// <summary>
    /// Выпавший на землю предмет.
    /// </summary>
    public readonly struct GroundItem
    {
        public readonly long DropId;
        public readonly string ItemId;
        public readonly int Count;
        public readonly float WorldX;
        public readonly float WorldY;

        public GroundItem(long dropId, string itemId, int count, float worldX, float worldY)
        {
            DropId = dropId;
            ItemId = itemId;
            Count = count;
            WorldX = worldX;
            WorldY = worldY;
        }
    }
}

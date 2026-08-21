#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Messaging.Contracts;

/// <summary>
/// Событие: предмет выпал на землю.
/// Публикуется когда инвентарь переполнен по объёму OR игрок выбросил предмет.
/// GroundItemRenderer подписывается и создаёт спрайт.
/// </summary>
public readonly struct ItemDroppedEvent
{
    public readonly long DropId;        // Уникальный ID выпавшего предмета
    public readonly string ItemId;      // ID предмета (для резолва в ItemDatabase)
    public readonly int Count;          // Количество в стопке
    public readonly float WorldX;       // Мировая позиция X (пиксели)
    public readonly float WorldY;       // Мировая позиция Y (пиксели)

    public ItemDroppedEvent(long dropId, string itemId, int count, float worldX, float worldY)
    {
        DropId = dropId;
        ItemId = itemId;
        Count = count;
        WorldX = worldX;
        WorldY = worldY;
    }
}

/// <summary>
/// Событие: предмет поднят с земли.
/// Публикуется когда игрок подбирает выпавший предмет.
/// GroundItemRenderer подписывается и удаляет спрайт.
/// </summary>
public readonly struct ItemPickedUpEvent
{
    public readonly long DropId;
    public readonly string ItemId;
    public readonly int Count;

    public ItemPickedUpEvent(long dropId, string itemId, int count)
    {
        DropId = dropId;
        ItemId = itemId;
        Count = count;
    }
}

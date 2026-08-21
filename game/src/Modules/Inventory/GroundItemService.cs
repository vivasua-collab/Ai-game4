#nullable enable
// Создано: 2026-08-21 — Ground item system for dropped items.
// Хранит предметы, выпавшие на землю (overflow inventory OR player throw).
// Поддерживает drop (создание) и pickup (подбор ближайшего).
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Inventory
{
    /// <summary>
    /// Реализация IGroundItemService.
    /// Хранит список выпавших предметов, управляет drop/pickup.
    /// Публикует ItemDroppedEvent / ItemPickedUpEvent для рендерера.
    /// </summary>
    public class GroundItemService : IGroundItemService
    {
        private readonly IPublisher<ItemDroppedEvent> _droppedPub;
        private readonly IPublisher<ItemPickedUpEvent> _pickedUpPub;
        private readonly IPublisher<ItemAddRequestEvent> _itemAddPub;

        private readonly List<GroundItem> _items = new();
        private long _nextDropId = 1;

        public int Count => _items.Count;

        public GroundItemService(
            IPublisher<ItemDroppedEvent> droppedPub,
            IPublisher<ItemPickedUpEvent> pickedUpPub,
            IPublisher<ItemAddRequestEvent> itemAddPub)
        {
            _droppedPub = droppedPub;
            _pickedUpPub = pickedUpPub;
            _itemAddPub = itemAddPub;
        }

        public long DropItem(string itemId, int count, float worldX, float worldY)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return 0;

            var dropId = _nextDropId++;
            var item = new GroundItem(dropId, itemId, count, worldX, worldY);
            _items.Add(item);

            _droppedPub.Publish(new ItemDroppedEvent(dropId, itemId, count, worldX, worldY));
            return dropId;
        }

        public bool TryPickupNearest(float worldX, float worldY, float maxDistance)
        {
            if (_items.Count == 0) return false;

            // Find nearest ground item.
            int nearestIdx = -1;
            float nearestDistSq = maxDistance * maxDistance;

            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                float dx = item.WorldX - worldX;
                float dy = item.WorldY - worldY;
                float distSq = dx * dx + dy * dy;
                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearestIdx = i;
                }
            }

            if (nearestIdx < 0) return false;

            var picked = _items[nearestIdx];
            _items.RemoveAt(nearestIdx);

            // Publish pickup event (renderer removes sprite).
            _pickedUpPub.Publish(new ItemPickedUpEvent(picked.DropId, picked.ItemId, picked.Count));

            // Publish add-item request (inventory picks up).
            _itemAddPub.Publish(new ItemAddRequestEvent(picked.ItemId, picked.Count, "pickup"));

            return true;
        }

        public IReadOnlyList<GroundItem> GetAllGroundItems() => _items.AsReadOnly();
    }
}

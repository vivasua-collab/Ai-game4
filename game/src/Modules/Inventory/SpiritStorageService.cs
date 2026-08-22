#nullable enable
// Создано: 2026-08-22 — Q9: Spirit Storage implementation.
// INVENTORY_SYSTEM.md §5: духовное хранилище, Qi cost per access.
using System;
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Inventory
{
    /// <summary>
    /// Реализация ISpiritStorageService.
    /// Духовное хранилище — предметы хранятся в "духовном пространстве" культиватора.
    /// Qi cost за каждый доступ (store/retrieve).
    /// Q9: разделено из единого StorageService.
    /// </summary>
    public class SpiritStorageService : ISpiritStorageService
    {
        private readonly IPublisher<ItemAddedEvent> _itemAddedPub;
        private readonly IPublisher<ItemRemovedEvent> _itemRemovedPub;
        private readonly IQiService _qiService;

        private readonly int _capacity;
        private readonly long _accessCost;
        private readonly List<InventorySlot> _storedItems = new();
        private readonly Dictionary<string, int> _itemCountCache = new();

        public int Capacity => _capacity;
        public int UsedSlots => _storedItems.Count;
        public long CurrentQi => _qiService?.CurrentQi ?? 0;

        /// <param name="capacity">Максимальное количество слотов.</param>
        /// <param name="accessCost">Qi cost per store/retrieve operation.</param>
        public SpiritStorageService(
            IPublisher<ItemAddedEvent> itemAddedPub,
            IPublisher<ItemRemovedEvent> itemRemovedPub,
            IQiService qiService,
            int capacity = 20,
            long accessCost = 10)
        {
            _itemAddedPub = itemAddedPub;
            _itemRemovedPub = itemRemovedPub;
            _qiService = qiService;
            _capacity = capacity;
            _accessCost = accessCost;
        }

        public long GetAccessCost() => _accessCost;

        public bool TryStore(ItemData item)
        {
            if (item == null) return false;

            // Check Qi cost
            if (_qiService != null && _qiService.CurrentQi < _accessCost)
            {
                Console.WriteLine($"[SpiritStorage] Not enough Qi: {CurrentQi}/{_accessCost}");
                return false;
            }

            // Check capacity
            if (_storedItems.Count >= _capacity)
            {
                // Try stacking
                if (item.Stackable)
                {
                    for (int i = 0; i < _storedItems.Count; i++)
                    {
                        if (_storedItems[i].ItemId == item.ItemId && _storedItems[i].Count < item.MaxStack)
                        {
                            int newCount = System.Math.Min(_storedItems[i].Count + 1, item.MaxStack);
                            _storedItems[i] = new InventorySlot(item.ItemId, newCount, item.Category, item.Rarity);
                            _itemCountCache[item.ItemId] = newCount;
                            DeductQi();
                            _itemAddedPub.Publish(new ItemAddedEvent(item.ItemId, 1));
                            return true;
                        }
                    }
                }
                return false;
            }

            // Add new slot
            _storedItems.Add(new InventorySlot(item.ItemId, 1, item.Category, item.Rarity));
            _itemCountCache[item.ItemId] = 1;
            DeductQi();
            _itemAddedPub.Publish(new ItemAddedEvent(item.ItemId, 1));
            return true;
        }

        public bool TryRetrieve(string itemId, out ItemData item)
        {
            item = null!;
            if (string.IsNullOrEmpty(itemId)) return false;

            // Check Qi cost
            if (_qiService != null && _qiService.CurrentQi < _accessCost)
            {
                Console.WriteLine($"[SpiritStorage] Not enough Qi for retrieve: {CurrentQi}/{_accessCost}");
                return false;
            }

            for (int i = 0; i < _storedItems.Count; i++)
            {
                if (_storedItems[i].ItemId == itemId)
                {
                    var slot = _storedItems[i];
                    int newCount = slot.Count - 1;

                    if (newCount <= 0)
                    {
                        _storedItems.RemoveAt(i);
                        _itemCountCache.Remove(itemId);
                    }
                    else
                    {
                        _storedItems[i] = new InventorySlot(itemId, newCount, slot.Category, slot.Rarity);
                        _itemCountCache[itemId] = newCount;
                    }

                    DeductQi();
                    _itemRemovedPub.Publish(new ItemRemovedEvent(itemId, 1));
                    return true;
                }
            }

            return false;
        }

        public IReadOnlyList<InventorySlot> GetStoredItems() => _storedItems.AsReadOnly();

        private void DeductQi()
        {
            if (_qiService != null && _accessCost > 0)
            {
                _qiService.TryConsumeQi(_accessCost);
            }
        }
    }
}

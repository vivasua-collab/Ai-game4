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
        // Review этап 4 (P0-1): резолв ItemData при извлечении (раньше out item
        // всегда null — предмет исчезал из хранилища, вызывающий получал ничего).
        private readonly IItemDatabaseService? _itemDatabase;

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
            IItemDatabaseService itemDatabase,
            int capacity = 20,
            long accessCost = 10)
        {
            _itemAddedPub = itemAddedPub;
            _itemRemovedPub = itemRemovedPub;
            _qiService = qiService;
            _itemDatabase = itemDatabase;
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

            // Review этап 4 (P1-2): ВСЕГДА сначала ищем неполный стек того же
            // ItemId (раньше — только при заполненном хранилище: stackable-предмет
            // расходовал слот на каждую единицу, UsedSlots достигал capacity
            // искусственно быстро).
            if (item.Stackable)
            {
                for (int i = 0; i < _storedItems.Count; i++)
                {
                    if (_storedItems[i].ItemId == item.ItemId && _storedItems[i].Count < item.MaxStack)
                    {
                        int newCount = System.Math.Min(_storedItems[i].Count + 1, item.MaxStack);
                        _storedItems[i] = new InventorySlot(item.ItemId, newCount, item.Category, item.Rarity);
                        RecalcCountCache(item.ItemId);
                        DeductQi();
                        _itemAddedPub.Publish(new ItemAddedEvent(item.ItemId, 1));
                        return true;
                    }
                }
            }

            // Check capacity (новый слот — только если подходящего стека нет)
            if (_storedItems.Count >= _capacity)
            {
                return false;
            }

            // Add new slot
            _storedItems.Add(new InventorySlot(item.ItemId, 1, item.Category, item.Rarity));
            RecalcCountCache(item.ItemId);
            DeductQi();
            _itemAddedPub.Publish(new ItemAddedEvent(item.ItemId, 1));
            return true;
        }

        public bool TryRetrieve(string itemId, out ItemData item)
        {
            item = null!;
            if (string.IsNullOrEmpty(itemId)) return false;

            // Review этап 4 (P0-1): резолвим ItemData ДО изменения слота.
            // Если предмет неизвестен базе — ничего не удаляем (раньше:
            // удаляли из хранилища, возвращали true с item = null → потеря).
            if (_itemDatabase == null || !_itemDatabase.TryGetItem(itemId, out var resolved) || resolved == null)
            {
                Console.WriteLine($"[SpiritStorage] Предмет '{itemId}' не найден в ItemDatabase — извлечение отклонено");
                return false;
            }

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
                        RecalcCountCache(itemId);
                    }

                    // Review этап 4 (P0-1): возвращаем РЕАЛЬНЫЙ предмет вызывающему.
                    item = resolved!;
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

        /// <summary>
        /// Review этап 4 (P1-2): кэш = СУММА по всем слотам itemId (раньше
        /// кэшировалось количество одного стека — при нескольких стеках
        /// кэш расходился с фактическим содержимым).
        /// </summary>
        private void RecalcCountCache(string itemId)
        {
            int total = 0;
            for (int i = 0; i < _storedItems.Count; i++)
            {
                if (_storedItems[i].ItemId == itemId) total += _storedItems[i].Count;
            }
            if (total > 0) _itemCountCache[itemId] = total;
            else _itemCountCache.Remove(itemId);
        }
    }
}

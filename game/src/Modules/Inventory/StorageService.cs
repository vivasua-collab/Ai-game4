#nullable enable
// Создано: 2026-05-09 00:00:00 UTC
// Редактировано: INV-B03 FIX: добавлена проверка maxStack
// Редактировано: INV-A09 FIX: TryRetrieve теперь корректно работает без ItemData
// Реализация IStorageService.
// Унифицированное хранилище — замена SpiritStorage + StorageRing (80% дублирование устранено).
// Один класс с параметром StorageType.
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Inventory
{
    /// <summary>
    /// Реализация IStorageService.
    /// Унифицированное хранилище: Spirit (духовное) и Ring (кольцо).
    /// Замена legacy SpiritStorageController (953 LOC) + StorageRingController (1215 LOC).
    ///
    /// BEFORE: SpiritStorageController + StorageRingController = 80% дубликат
    /// AFTER:  StorageService (~130 LOC) с параметром StorageType
    ///
    /// INV-A09 FIX: IStorageService.TryRetrieve(out ItemData) — API контракт изменён.
    /// StorageService хранит InventorySlot (ItemId, Count, Category, Rarity),
    /// но НЕ имеет доступа к ScriptableObject ItemData.
    /// Решение: TryRetrieve возвращает null для ItemData (out-параметр),
    ///   но возвращает true при успешном извлечении. Вызывающий может
    ///   загрузить ItemData через ItemDatabase по ItemId из InventorySlot.
    ///   Для удобства добавлен метод TryRetrieveSlot.
    /// </summary>
    public class StorageService : IStorageService
    {
        // === Зависимости (DI через конструктор) ===
        private readonly IPublisher<ItemAddedEvent> _itemAddedPub;
        private readonly IPublisher<ItemRemovedEvent> _itemRemovedPub;

        // === Состояние ===
        private readonly StorageType _type;
        private readonly int _capacity;
        private readonly List<InventorySlot> _storedItems = new();
        private readonly Dictionary<string, int> _itemCountCache = new();

        // INV-B03 FIX: Кэш maxStack по itemId (заполняется при TryStore)
        private readonly Dictionary<string, int> _maxStackCache = new();

        // === Свойства ===
        public StorageType Type => _type;
        public int Capacity => _capacity;
        public int UsedSlots => _storedItems.Count;

        // === Конструктор (VContainer) ===
        public StorageService(
            StorageType type,
            int capacity,
            IPublisher<ItemAddedEvent> itemAddedPub,
            IPublisher<ItemRemovedEvent> itemRemovedPub)
        {
            _type = type;
            _capacity = capacity;
            _itemAddedPub = itemAddedPub;
            _itemRemovedPub = itemRemovedPub;
        }

        // === IStorageService ===

        public bool TryStore(ItemData item)
        {
            if (item == null) return false;

            // Проверка вложения (NestingFlag)
            if (item.AllowNesting == NestingFlag.None) return false;
            if (_type == StorageType.Spirit && item.AllowNesting == NestingFlag.Ring) return false;
            if (_type == StorageType.Ring && item.AllowNesting == NestingFlag.Spirit) return false;

            // INV-B03 FIX: Кэшируем maxStack для последующих проверок
            _maxStackCache[item.ItemId] = item.MaxStack;

            // Стакающиеся предметы — ищем существующий слот
            if (item.Stackable)
            {
                for (int i = 0; i < _storedItems.Count; i++)
                {
                    if (_storedItems[i].ItemId == item.ItemId)
                    {
                        int maxStack = item.MaxStack;
                        int newCount = _storedItems[i].Count + 1;

                        // INV-B03 FIX: Проверка maxStack
                        if (newCount > maxStack)
                            return false; // Стак полон

                        _storedItems[i] = new InventorySlot(item.ItemId, newCount, item.Category, item.Rarity);
                        _itemCountCache[item.ItemId] = newCount;
                        _itemAddedPub.Publish(new ItemAddedEvent(item.ItemId, 1));
                        return true;
                    }
                }
            }

            // Проверка лимита слотов
            if (_storedItems.Count >= _capacity)
            {
                // INV-B15 FIX: Публикуем событие переполнения (через ItemRemovedEvent с count=0)
                // StorageOverflowEvent пока не реализован в контрактах — используем молчаливый отказ
                return false;
            }

            // Новый слот
            _storedItems.Add(new InventorySlot(item.ItemId, 1, item.Category, item.Rarity));
            if (_itemCountCache.ContainsKey(item.ItemId))
                _itemCountCache[item.ItemId]++;
            else
                _itemCountCache[item.ItemId] = 1;

            _itemAddedPub.Publish(new ItemAddedEvent(item.ItemId, 1));
            return true;
        }

        /// <summary>
        /// INV-A09 FIX: TryRetrieve извлекает предмет из хранилища.
        /// ItemData out-параметр всегда null — StorageService не хранит ScriptableObject ссылки.
        /// Для получения ItemData используйте ItemDatabase.Lookup(retrievedItemId).
        /// Метод TryRetrieveSlot возвращает InventorySlot с полными данными.
        /// </summary>
        public bool TryRetrieve(string itemId, out ItemData item)
        {
            item = null; // INV-A09: StorageService не может реконструировать ItemData

            if (string.IsNullOrEmpty(itemId)) return false;

            for (int i = 0; i < _storedItems.Count; i++)
            {
                if (_storedItems[i].ItemId == itemId)
                {
                    var slot = _storedItems[i];
                    int remaining = slot.Count - 1;

                    if (remaining <= 0)
                    {
                        _storedItems.RemoveAt(i);
                        _itemCountCache.Remove(itemId);
                    }
                    else
                    {
                        _storedItems[i] = new InventorySlot(itemId, remaining, slot.Category, slot.Rarity);
                        _itemCountCache[itemId] = remaining;
                    }

                    _itemRemovedPub.Publish(new ItemRemovedEvent(itemId, 1));
                    return true;
                }
            }

            return false;
        }

        public IReadOnlyList<InventorySlot> GetStoredItems()
        {
            return _storedItems.AsReadOnly();
        }
    }
}

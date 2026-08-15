#nullable enable
// Создано: 2026-05-09 00:00:00 UTC
// Редактировано: 2026-05-09 — EVT-02: подписка на ItemAddRequestEvent (command-событие),
//   реализация IDisposable для управления подписками.
// Редактировано: 2026-05-10 — Phase 17A: NPC-E01 / INV6-E01 / INV6-01 / Q12-E01 fixes
// Редактировано: 2026-05-10 12:00:00 UTC — Phase 18A: реализация ISaveable
// Редактировано: 2026-05-18 17:58:25 UTC — P1-01 FIX: TryRemoveItem мульти-слот + cache fix;
//   P1-04 FIX: CalculateTotalWeight/Volume через IItemDatabaseService
// Редактировано: 2026-05-18 18:39:47 UTC — STR-MODEL: интеграция BackpackService,
//   CanFitItem, HowManyCanFit, GetCurrentWeight/Volume, GetEffectiveMaxWeight/Volume
// Реализация IInventoryService.
// Строчная модель инвентаря (вес + объём, НЕ сетка).
// Заменяет legacy InventoryController.cs (1073 LOC).
using System;
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Core.Events;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Inventory
{
    /// <summary>
    /// Реализация IInventoryService.
    /// Управляет рюкзаком персонажа: добавление, удаление, подсчёт предметов.
    /// Строчная модель: вес + объём (нет сетки).
    ///
    /// STR-MODEL: Интеграция с BackpackService для учёта:
    /// - effectiveMaxWeight = baseMaxWeight + backpack.WeightBonus
    /// - effectiveMaxVolume = baseMaxVolume + backpack.VolumeBonus
    /// - effectiveWeight = rawWeight × (1 - backpack.WeightReduction / 100)
    ///
    /// INV6-E01 FIX: Подписка на ItemAddRequestEvent перенесена в InventoryModule.
    /// </summary>
    public class InventoryService : IInventoryService, ISaveable, IDisposable
    {
        // === Зависимости (DI через конструктор) ===
        private readonly IPublisher<ItemAddedEvent> _itemAddedPub;
        private readonly IPublisher<ItemRemovedEvent> _itemRemovedPub;
        // P1-04 FIX: ItemDatabase для CalculateTotalWeight/Volume
        private readonly IItemDatabaseService _itemDatabase;
        // STR-MODEL: BackpackService для effective weight/volume
        private readonly BackpackService _backpackService;

        // === Состояние ===
        private readonly List<InventorySlot> _slots = new();
        private InventoryConfig _config;
        private bool _isConfigured;

        // === Кэш для быстрого поиска ===
        private readonly Dictionary<string, int> _itemCountCache = new();

        // === Свойства ===
        // Строчная модель: TotalSlots = расчётное число из объёма (1 слот = 1 единица объёма)
        public int TotalSlots => (int)GetEffectiveMaxVolume();
        public int UsedSlots => _slots.Count;

        // === Конструктор (VContainer) ===
        public InventoryService(
            IPublisher<ItemAddedEvent> itemAddedPub,
            IPublisher<ItemRemovedEvent> itemRemovedPub,
            IItemDatabaseService itemDatabase = null,
            BackpackService backpackService = null)
        {
            _itemAddedPub = itemAddedPub;
            _itemRemovedPub = itemRemovedPub;
            _itemDatabase = itemDatabase;
            _backpackService = backpackService;
        }

        /// <summary>
        /// Настроить сервис конфигурацией.
        /// Вызывается из InventoryModule.IStartable.Start().
        /// </summary>
        public void Configure(InventoryConfig config)
        {
            _config = config;
            _isConfigured = true;
        }

        // === IInventoryService — базовые методы ===

        public bool TryAddItem(ItemData item, int count = 1)
        {
            if (item == null) return false;
            if (!_isConfigured) Configure(new InventoryConfig());
            if (count <= 0) return false;

            // STR-MODEL: Проверка лимитов веса и объёма с учётом рюкзака
            if (!CanFitItem(item, count)) return false;

            // Стакающиеся предметы — ищем существующий слот
            if (item.Stackable)
            {
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (_slots[i].ItemId == item.ItemId)
                    {
                        // Проверка maxStack
                        int newCount = _slots[i].Count + count;
                        if (newCount > item.MaxStack)
                        {
                            // Стак заполнен — пробуем добавить остаток в новый слот
                            int canAdd = item.MaxStack - _slots[i].Count;
                            if (canAdd <= 0) continue;

                            _slots[i] = new InventorySlot(item.ItemId, item.MaxStack, item.Category, item.Rarity);
                            _itemCountCache[item.ItemId] = _itemCountCache.TryGetValue(item.ItemId, out var cached)
                                ? cached + canAdd : canAdd;
                            _itemAddedPub.Publish(new ItemAddedEvent(item.ItemId, canAdd));

                            // Рекурсивно добавляем остаток
                            int remaining = count - canAdd;
                            if (remaining > 0)
                                return TryAddItem(item, remaining);
                            return true;
                        }

                        _slots[i] = new InventorySlot(item.ItemId, newCount, item.Category, item.Rarity);
                        _itemCountCache[item.ItemId] = newCount;
                        _itemAddedPub.Publish(new ItemAddedEvent(item.ItemId, count));
                        return true;
                    }
                }

                // Нет существующего слота — создаём новый с учётом maxStack
                int toAdd = Math.Min(count, item.MaxStack);
                var newSlot = new InventorySlot(item.ItemId, toAdd, item.Category, item.Rarity);
                _slots.Add(newSlot);
                if (_itemCountCache.ContainsKey(item.ItemId))
                    _itemCountCache[item.ItemId] += toAdd;
                else
                    _itemCountCache[item.ItemId] = toAdd;

                _itemAddedPub.Publish(new ItemAddedEvent(item.ItemId, toAdd));

                // Если не всё влезло — рекурсивно добавляем остаток
                int remainder = count - toAdd;
                if (remainder > 0)
                    return TryAddItem(item, remainder);
                return true;
            }

            // Нестакающиеся предметы — каждый занимает отдельный слот
            for (int i = 0; i < count; i++)
            {
                var newSlot = new InventorySlot(item.ItemId, 1, item.Category, item.Rarity);
                _slots.Add(newSlot);
                if (_itemCountCache.ContainsKey(item.ItemId))
                    _itemCountCache[item.ItemId]++;
                else
                    _itemCountCache[item.ItemId] = 1;

                _itemAddedPub.Publish(new ItemAddedEvent(item.ItemId, 1));
            }
            return true;
        }

        public bool TryRemoveItem(string itemId, int count = 1)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            if (count <= 0) return false;
            if (!_isConfigured) Configure(new InventoryConfig());

            // Шаг 1 — проверить, достаточно ли предметов в сумме
            int totalAvailable = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].ItemId == itemId)
                    totalAvailable += _slots[i].Count;
            }

            if (totalAvailable < count) return false;

            // Шаг 2 — удаляем из слотов (с конца)
            int remaining = count;
            for (int i = _slots.Count - 1; i >= 0 && remaining > 0; i--)
            {
                if (_slots[i].ItemId != itemId) continue;

                if (_slots[i].Count <= remaining)
                {
                    remaining -= _slots[i].Count;
                    _slots[i] = default(InventorySlot);
                }
                else
                {
                    _slots[i] = new InventorySlot(
                        itemId,
                        _slots[i].Count - remaining,
                        _slots[i].Category,
                        _slots[i].Rarity);
                    remaining = 0;
                }
            }

            // Шаг 3 — обновить кэш
            int newTotal = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].ItemId == itemId)
                    newTotal += _slots[i].Count;
            }
            if (newTotal > 0)
                _itemCountCache[itemId] = newTotal;
            else
                _itemCountCache.Remove(itemId);

            // Шаг 4 — уплотнить
            CompactSlots();

            _itemRemovedPub.Publish(new ItemRemovedEvent(itemId, count));
            return true;
        }

        private void CompactSlots()
        {
            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                if (_slots[i].IsEmpty)
                    _slots.RemoveAt(i);
            }
        }

        public int GetItemCount(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            return _itemCountCache.TryGetValue(itemId, out var count) ? count : 0;
        }

        public IReadOnlyList<InventorySlot> GetAllSlots()
        {
            return _slots.AsReadOnly();
        }

        // === STR-MODEL: методы строчной модели ===

        /// <summary>
        /// STR-MODEL: Проверить, поместится ли предмет в инвентарь.
        /// Учитывает бонусы рюкзака (weightBonus, volumeBonus, weightReduction).
        /// </summary>
        public bool CanFitItem(ItemData item, int count = 1)
        {
            if (item == null || count <= 0) return false;

            float addedWeight = item.Weight * count;
            float addedVolume = item.Volume * count;
            float effectiveMaxWeight = GetEffectiveMaxWeight();
            float effectiveMaxVolume = GetEffectiveMaxVolume();
            float currentWeight = GetCurrentWeight();
            float currentVolume = GetCurrentVolume();

            return currentWeight + addedWeight <= effectiveMaxWeight
                && currentVolume + addedVolume <= effectiveMaxVolume;
        }

        /// <summary>
        /// STR-MODEL: Сколько предметов данного типа поместится.
        /// Ограничено: вес (эффективный макс), объём (эффективный макс).
        /// </summary>
        public int HowManyCanFit(ItemData item)
        {
            if (item == null) return 0;

            float effectiveMaxWeight = GetEffectiveMaxWeight();
            float effectiveMaxVolume = GetEffectiveMaxVolume();
            float currentWeight = GetCurrentWeight();
            float currentVolume = GetCurrentVolume();

            float remainingWeight = effectiveMaxWeight - currentWeight;
            float remainingVolume = effectiveMaxVolume - currentVolume;

            int byWeight = item.Weight > 0 ? (int)Math.Floor(remainingWeight / item.Weight) : int.MaxValue;
            int byVolume = item.Volume > 0 ? (int)Math.Floor(remainingVolume / item.Volume) : int.MaxValue;

            return Math.Max(0, Math.Min(byWeight, byVolume));
        }

        /// <summary>
        /// STR-MODEL: Текущий суммарный вес предметов (кг).
        /// Через ItemDatabase для получения weight каждого предмета.
        /// </summary>
        public float GetCurrentWeight()
        {
            float total = 0f;
            foreach (var slot in _slots)
            {
                if (slot.IsEmpty) continue;
                if (_itemDatabase != null && _itemDatabase.TryGetItem(slot.ItemId, out var item))
                    total += item.Weight * slot.Count;
                else
                    total += 0.5f * slot.Count;
            }
            return total;
        }

        /// <summary>
        /// STR-MODEL: Текущий суммарный объём предметов (л).
        /// Через ItemDatabase для получения volume каждого предмета.
        /// </summary>
        public float GetCurrentVolume()
        {
            float total = 0f;
            foreach (var slot in _slots)
            {
                if (slot.IsEmpty) continue;
                if (_itemDatabase != null && _itemDatabase.TryGetItem(slot.ItemId, out var item))
                    total += item.Volume * slot.Count;
                else
                    total += 1.0f * slot.Count;
            }
            return total;
        }

        /// <summary>
        /// STR-MODEL: Эффективный максимальный вес с учётом рюкзака.
        /// Формула: baseMaxWeight + backpack.WeightBonus
        /// </summary>
        public float GetEffectiveMaxWeight()
        {
            float baseMax = _config?.MaxCarryWeight ?? 50f;
            if (_backpackService != null)
                return _backpackService.GetEffectiveMaxWeight(baseMax);
            return baseMax;
        }

        /// <summary>
        /// STR-MODEL: Эффективный максимальный объём с учётом рюкзака.
        /// Формула: baseMaxVolume + backpack.VolumeBonus
        /// </summary>
        public float GetEffectiveMaxVolume()
        {
            float baseMax = _config?.MaxCarryVolume ?? 100f;
            if (_backpackService != null)
                return _backpackService.GetEffectiveMaxVolume(baseMax);
            return baseMax;
        }

        // === ISaveable ===

        public string SaveKey => "inventory";

        public object CaptureState()
        {
            var slotData = new InventorySlotSaveData[_slots.Count];
            for (int i = 0; i < _slots.Count; i++)
            {
                slotData[i] = new InventorySlotSaveData
                {
                    itemId = _slots[i].ItemId,
                    count = _slots[i].Count,
                    category = (int)_slots[i].Category,
                    rarity = (int)_slots[i].Rarity
                };
            }

            var data = new InventorySaveData
            {
                slots = slotData,
                currentWeight = GetCurrentWeight(),
                currentVolume = GetCurrentVolume()
            };
            return data;
        }

        public void RestoreState(object state)
        {
            if (state is not InventorySaveData data || data == null) return;

            _slots.Clear();
            _itemCountCache.Clear();

            if (data.slots != null)
            {
                foreach (var slotSave in data.slots)
                {
                    if (string.IsNullOrEmpty(slotSave.itemId)) continue;

                    var slot = new InventorySlot(
                        slotSave.itemId,
                        slotSave.count,
                        (ItemCategory)slotSave.category,
                        (ItemRarity)slotSave.rarity);
                    _slots.Add(slot);

                    if (_itemCountCache.ContainsKey(slotSave.itemId))
                        _itemCountCache[slotSave.itemId] += slotSave.count;
                    else
                        _itemCountCache[slotSave.itemId] = slotSave.count;
                }
            }
        }

        // === IDisposable ===

        public void Dispose()
        {
            // Подписка на ItemAddRequestEvent перенесена в InventoryModule.
        }
    }

    // === Сериализуемые структуры для ISaveable ===

    [Serializable]
    public class InventorySaveData
    {
        public InventorySlotSaveData[] slots;
        public float currentWeight;
        public float currentVolume;
    }

    [Serializable]
    public class InventorySlotSaveData
    {
        public string itemId;
        public int count;
        public int category;
        public int rarity;
    }
}

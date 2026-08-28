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
            return TryAddItem(item, count, out _);
        }

        /// <summary>
        /// Try to add items. Returns true if at least 1 added.
        /// addedCount = actual number added (may be less than requested if volume full).
        /// Caller can drop (count - addedCount) on ground if > 0.
        /// </summary>
        public bool TryAddItem(ItemData item, int count, out int addedCount)
        {
            addedCount = 0;
            if (item == null) return false;
            if (!_isConfigured) Configure(new InventoryConfig());
            if (count <= 0) return false;

            // OVERFLOW POLICY: Items ALWAYS enter inventory (even if overweight).
            // This is intentional — player can exceed carry weight, but movement
            // speed drops (handled in GameWorldController.HandleFreeMovement).
            // Storage Ring / Spirit Storage (future) allow offloading excess.
            // Volume limit still enforced (physical space in backpack).
            float addedVolume = item.Volume * count;
            float effectiveMaxVolume = GetEffectiveMaxVolume();
            float currentVolume = GetCurrentVolume();
            if (currentVolume + addedVolume > effectiveMaxVolume)
            {
                // Volume limit reached — try to add as many as fit.
                int canFit = HowManyCanFit(item);
                if (canFit <= 0)
                {
                    Console.WriteLine($"[Inventory] Cannot add {item.ItemId}×{count} — volume full ({currentVolume:F1}/{effectiveMaxVolume:F1})");
                    return false;
                }
                // Add partial, log overflow.
                int originalCount = count;
                count = canFit;
                addedCount = count;
                Console.WriteLine($"[Inventory] Volume limit — adding partial {item.ItemId}×{count}/{originalCount}");
            }
            else
            {
                addedCount = count;
            }

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

                            // Рекурсивно добавляем остаток.
                            // INV-A1 FIX (аудит-4): раньше вызывалась 2-arg перегрузка —
                            // уточнённый addedCount рекурсии отбрасывался, и при сплите
                            // стака + лимите объёма out-addedCount завышал фактическое
                            // число (влияет на дроп-на-землю). Аккумулируем.
                            int remaining = count - canAdd;
                            if (remaining > 0)
                            {
                                TryAddItem(item, remaining, out int recAdded);
                                addedCount = canAdd + recAdded;
                            }
                            else
                            {
                                addedCount = canAdd;
                            }
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

                // Если не всё влезло — рекурсивно добавляем остаток.
                // INV-A1 FIX (аудит-4): аккумулируем уточнённый addedCount
                // рекурсии (раньше отбрасывался 2-arg перегрузкой).
                int remainder = count - toAdd;
                if (remainder > 0)
                {
                    TryAddItem(item, remainder, out int recAdded);
                    addedCount = toAdd + recAdded;
                }
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
        /// NOTE: Weight limit is NOT enforced (overflow allowed — player can be overweight,
        /// movement speed drops). Only volume (physical backpack space) is enforced.
        /// </summary>
        public bool CanFitItem(ItemData item, int count = 1)
        {
            if (item == null || count <= 0) return false;

            float addedVolume = item.Volume * count;
            float effectiveMaxVolume = GetEffectiveMaxVolume();
            float currentVolume = GetCurrentVolume();

            return currentVolume + addedVolume <= effectiveMaxVolume;
        }

        /// <summary>
        /// STR-MODEL: Сколько предметов данного типа поместится.
        /// Limited by VOLUME only (weight overflow allowed).
        /// </summary>
        public int HowManyCanFit(ItemData item)
        {
            if (item == null) return 0;

            float effectiveMaxVolume = GetEffectiveMaxVolume();
            float currentVolume = GetCurrentVolume();

            float remainingVolume = effectiveMaxVolume - currentVolume;

            // Weight NOT checked — overflow allowed.
            int byVolume = item.Volume > 0 ? (int)Math.Floor(remainingVolume / item.Volume) : int.MaxValue;

            return Math.Max(0, byVolume);
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

        /// <summary>True if current weight exceeds effective max (overweight).</summary>
        public bool IsOverweight => GetCurrentWeight() > GetEffectiveMaxWeight();

        /// <summary>
        /// Overload ratio: 0 = at/below max, 1.0 = 2× max, 2.0 = 3× max.
        /// Capped at 3.0 (4× max) to prevent zero-speed lock.
        /// </summary>
        public float OverweightRatio
        {
            get
            {
                float max = GetEffectiveMaxWeight();
                if (max <= 0f) return 0f;
                float current = GetCurrentWeight();
                if (current <= max) return 0f;
                // (current - max) / max gives overload fraction; cap at 3.0
                return Math.Min(3.0f, (current - max) / max);
            }
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

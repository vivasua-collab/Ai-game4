#nullable enable
// Создано: 2026-05-18 18:12:00 UTC
// Редактировано: 2026-05-18 18:39:47 UTC — STR-MODEL: объёмная вместимость
//   TryStore проверяет CurrentVolume + item.Volume ≤ MaxVolume
//   ActivateRing принимает maxVolume параметр
//   RecalculateVolume после добавления/удаления
// Сервис колец хранения.
// Управляет хранилищами, привязанными к экипированным кольцам.
// Помещение/извлечение предметов стоит Qi.
// Формулы из legacy:
//   storeCost  = (10 + tier * 5) + itemWeight * (0.5 + tier * 0.2)
//   retrieveCost = (10 + tier * 5) * 0.5 + itemWeight * (0.5 + tier * 0.2) * 0.5

using System;
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Modules.Inventory.Data;
using CultivationGame.Core.Events;

namespace CultivationGame.Modules.Inventory
{
    /// <summary>
    /// Сервис колец хранения.
    /// Реализует IStorageRingService.
    ///
    /// STR-MODEL: вместимость определяется объёмом (maxVolume, литры),
    /// а не количеством слотов. Предмет добавляется, если
    /// CurrentVolume + item.Volume ≤ MaxVolume.
    ///
    /// Кольца хранения — экипируемые аксессуары (слоты Ring*).
    /// При экипировке кольца активируется его хранилище.
    /// При снятии — деактивируется, но предметы ОСТАЮТСЯ внутри.
    /// </summary>
    public class StorageRingService : IStorageRingService, IDisposable
    {
        // === Зависимости (DI через конструктор) ===
        private readonly ISubscriber<EquipmentChangedEvent> _equipChangedSub;
        private readonly IItemDatabaseService _itemDatabase;
        private readonly IEquipmentService _equipmentService;

        // === Состояние ===
        private readonly Dictionary<string, StorageRingEntry> _rings = new();
        private IDisposable _equipChangedSubscription;

        // === Слоты колец ===
        private static readonly HashSet<EquipmentSlot> RingSlots = new()
        {
            EquipmentSlot.RingLeft1,
            EquipmentSlot.RingLeft2,
            EquipmentSlot.RingRight1,
            EquipmentSlot.RingRight2
        };

        // === Конструктор (VContainer) ===
        public StorageRingService(
            ISubscriber<EquipmentChangedEvent> equipChangedSub,
            IItemDatabaseService itemDatabase,
            IEquipmentService equipmentService)
        {
            _equipChangedSub = equipChangedSub;
            _itemDatabase = itemDatabase;
            _equipmentService = equipmentService;
        }

        /// <summary>
        /// Инициализация: проверить текущие кольца и подписаться на события.
        /// </summary>
        public void Initialize()
        {
            CheckExistingRings();
            _equipChangedSubscription = _equipChangedSub.Subscribe(OnEquipmentChanged);
        }

        // === IStorageRingService ===

        /// <summary>
        /// Поместить предмет в хранилище кольца.
        /// STR-MODEL: проверка по объёму (CurrentVolume + item.Volume ≤ MaxVolume).
        /// </summary>
        public bool TryStore(string ringItemId, ItemData item, out long qiCost)
        {
            qiCost = 0L;

            if (string.IsNullOrEmpty(ringItemId) || item == null) return false;

            if (!_rings.TryGetValue(ringItemId, out var entry))
            {
                Console.WriteLine($"[StorageRingService] Кольцо '{ringItemId}' не зарегистрировано");
                return false;
            }

            if (!entry.IsActive)
            {
                Console.WriteLine($"[StorageRingService] Кольцо '{ringItemId}' неактивно (снято)");
                return false;
            }

            // Проверка: NestingFlag
            if (item.AllowNesting == NestingFlag.None) return false;
            if (item.AllowNesting == NestingFlag.Spirit) return false;

            // STR-MODEL: Проверка по объёму
            float addedVolume = item.Volume;
            if (entry.CurrentVolume + addedVolume > entry.MaxVolume)
            {
                Console.WriteLine($"[StorageRingService] Кольцо '{ringItemId}' переполнено по объёму " +
                    $"({entry.CurrentVolume:F1}+{addedVolume:F1}/{entry.MaxVolume:F1} л)");
                return false;
            }

            // Вычислить Qi стоимость
            qiCost = GetStoreQiCost(entry.Tier, item.Weight);

            // Стакающиеся предметы — ищем существующий слот
            if (item.Stackable)
            {
                for (int i = 0; i < entry.StoredItems.Count; i++)
                {
                    if (entry.StoredItems[i].ItemId == item.ItemId)
                    {
                        int newCount = entry.StoredItems[i].Count + 1;
                        if (newCount > item.MaxStack)
                        {
                            // Стак полон — создаём новый слот если есть объём
                            // (объём уже проверен выше, но проверим с учётом нового слота)
                            entry.StoredItems.Add(new InventorySlot(item.ItemId, 1, item.Category, item.Rarity));
                        }
                        else
                        {
                            entry.StoredItems[i] = new InventorySlot(item.ItemId, newCount, item.Category, item.Rarity);
                        }

                        // STR-MODEL: пересчитать объём
                        entry.RecalculateVolume(_itemDatabase);
                        return true;
                    }
                }
            }

            // Новый слот
            entry.StoredItems.Add(new InventorySlot(item.ItemId, 1, item.Category, item.Rarity));

            // STR-MODEL: пересчитать объём
            entry.RecalculateVolume(_itemDatabase);
            return true;
        }

        /// <summary>
        /// Извлечь предмет из хранилища кольца.
        /// </summary>
        public bool TryRetrieve(string ringItemId, string storedItemId, out ItemData item, out long qiCost)
        {
            item = null;
            qiCost = 0L;

            if (string.IsNullOrEmpty(ringItemId) || string.IsNullOrEmpty(storedItemId)) return false;

            if (!_rings.TryGetValue(ringItemId, out var entry))
            {
                Console.WriteLine($"[StorageRingService] Кольцо '{ringItemId}' не зарегистрировано");
                return false;
            }

            if (!entry.IsActive)
            {
                Console.WriteLine($"[StorageRingService] Кольцо '{ringItemId}' неактивно (снято)");
                return false;
            }

            // Найти предмет в хранилище
            for (int i = 0; i < entry.StoredItems.Count; i++)
            {
                if (entry.StoredItems[i].ItemId != storedItemId) continue;

                var slot = entry.StoredItems[i];
                float itemWeight = 0f;

                if (_itemDatabase != null && _itemDatabase.TryGetItem(storedItemId, out var itemData))
                {
                    item = itemData;
                    itemWeight = itemData.Weight;
                }

                qiCost = GetRetrieveQiCost(entry.Tier, itemWeight);

                int remaining = slot.Count - 1;
                if (remaining <= 0)
                {
                    entry.StoredItems.RemoveAt(i);
                }
                else
                {
                    entry.StoredItems[i] = new InventorySlot(storedItemId, remaining, slot.Category, slot.Rarity);
                }

                // STR-MODEL: пересчитать объём
                entry.RecalculateVolume(_itemDatabase);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Получить содержимое хранилища кольца.
        /// </summary>
        public IReadOnlyList<InventorySlot> GetRingContents(string ringItemId)
        {
            if (string.IsNullOrEmpty(ringItemId)) return Array.Empty<InventorySlot>();
            if (!_rings.TryGetValue(ringItemId, out var entry)) return Array.Empty<InventorySlot>();
            return entry.StoredItems.AsReadOnly();
        }

        /// <summary>
        /// Qi стоимость помещения предмета в кольцо.
        /// Формула: (10 + tier * 5) + itemWeight * (0.5 + tier * 0.2)
        /// ЗАПРЕТ 2: Qi-значения должны быть long.
        /// </summary>
        public long GetStoreQiCost(int ringTier, float itemWeight)
        {
            float baseCost = 10f + ringTier * 5f;
            float weightCost = itemWeight * (0.5f + ringTier * 0.2f);
            return (long)Math.Floor(baseCost + weightCost);
        }

        /// <summary>
        /// Qi стоимость извлечения предмета из кольца.
        /// Формула: (10 + tier * 5) * 0.5 + itemWeight * (0.5 + tier * 0.2) * 0.5
        /// ЗАПРЕТ 2: Qi-значения должны быть long.
        /// </summary>
        public long GetRetrieveQiCost(int ringTier, float itemWeight)
        {
            float baseCost = (10f + ringTier * 5f) * 0.5f;
            float weightCost = itemWeight * (0.5f + ringTier * 0.2f) * 0.5f;
            return (long)Math.Floor(baseCost + weightCost);
        }

        /// <summary>
        /// Активировать хранилище для кольца (при экипировке).
        /// STR-MODEL: принимает maxVolume (литры) вместо capacity (слоты).
        /// </summary>
        public void ActivateRing(string ringItemId, int tier, int capacity)
        {
            // STR-MODEL: вычисляем maxVolume из tier
            // Формула из документации (StorageRingData):
            // T1=5л, T2=15л, T3=30л, T4=60л
            float maxVolume = tier switch
            {
                1 => 5f,
                2 => 15f,
                3 => 30f,
                4 => 60f,
                5 => 100f,
                _ => tier * 10f // Fallback
            };

            ActivateRingWithVolume(ringItemId, tier, maxVolume, capacity);
        }

        /// <summary>
        /// Активировать хранилище для кольца с явным maxVolume.
        /// STR-MODEL: основной метод активации.
        /// </summary>
        public void ActivateRingWithVolume(string ringItemId, int tier, float maxVolume, int capacity = 0)
        {
            if (string.IsNullOrEmpty(ringItemId)) return;

            if (_rings.TryGetValue(ringItemId, out var existing))
            {
                existing.Tier = tier;
                existing.MaxVolume = maxVolume;
                existing.Capacity = capacity;
                existing.IsActive = true;
                existing.RecalculateVolume(_itemDatabase);
                Console.WriteLine($"[StorageRingService] Реактивация кольца '{ringItemId}' " +
                    $"(Tier={tier}, MaxVolume={maxVolume}л, Items={existing.StoredItems.Count})");
            }
            else
            {
                var entry = new StorageRingEntry(ringItemId, tier, maxVolume, capacity);
                _rings[ringItemId] = entry;
                Console.WriteLine($"[StorageRingService] Активация кольца '{ringItemId}' " +
                    $"(Tier={tier}, MaxVolume={maxVolume}л)");
            }
        }

        /// <summary>
        /// Деактивировать хранилище кольца (при снятии).
        /// Предметы ОСТАЮТСЯ внутри — деактивация ≠ очистка.
        /// </summary>
        public void DeactivateRing(string ringItemId)
        {
            if (string.IsNullOrEmpty(ringItemId)) return;

            if (_rings.TryGetValue(ringItemId, out var entry))
            {
                entry.IsActive = false;
                Console.WriteLine($"[StorageRingService] Деактивация кольца '{ringItemId}' " +
                    $"(Items сохранены: {entry.StoredItems.Count})");
            }
        }

        /// <summary>
        /// Проверить, активно ли кольцо.
        /// </summary>
        public bool IsRingActive(string ringItemId)
        {
            if (string.IsNullOrEmpty(ringItemId)) return false;
            return _rings.TryGetValue(ringItemId, out var entry) && entry.IsActive;
        }

        // === Обработчики событий ===

        private void OnEquipmentChanged(in EquipmentChangedEvent e)
        {
            if (!RingSlots.Contains(e.Slot)) return;

            if (!string.IsNullOrEmpty(e.ItemId))
            {
                if (_itemDatabase != null && _itemDatabase.TryGetItem(e.ItemId, out var itemData))
                {
                    if (itemData is EquipmentData equipData && equipData.StorageRingTier > 0)
                    {
                        // STR-MODEL: активировать с maxVolume из EquipmentData
                        float maxVol = equipData.StorageMaxVolume > 0
                            ? equipData.StorageMaxVolume
                            : GetDefaultMaxVolume(equipData.StorageRingTier);
                        ActivateRingWithVolume(e.ItemId, equipData.StorageRingTier, maxVol, equipData.StorageCapacity);
                    }
                }
            }

            if (!string.IsNullOrEmpty(e.OldItemId))
            {
                if (_rings.ContainsKey(e.OldItemId))
                {
                    DeactivateRing(e.OldItemId);
                }
            }
        }

        private void CheckExistingRings()
        {
            foreach (var slot in RingSlots)
            {
                var equipped = _equipmentService.GetEquipped(slot);
                if (equipped != null && equipped.StorageRingTier > 0)
                {
                    float maxVol = equipped.StorageMaxVolume > 0
                        ? equipped.StorageMaxVolume
                        : GetDefaultMaxVolume(equipped.StorageRingTier);
                    ActivateRingWithVolume(equipped.ItemId, equipped.StorageRingTier, maxVol, equipped.StorageCapacity);
                }
            }
        }

        /// <summary>
        /// Стандартный maxVolume по tier (из документации StorageRingData).
        /// T1=5л, T2=15л, T3=30л, T4=60л
        /// </summary>
        private static float GetDefaultMaxVolume(int tier)
        {
            return tier switch
            {
                1 => 5f,
                2 => 15f,
                3 => 30f,
                4 => 60f,
                5 => 100f,
                _ => tier * 10f
            };
        }

        // === IDisposable ===

        public void Dispose()
        {
            _equipChangedSubscription?.Dispose();
            _equipChangedSubscription = null;
        }
    }
}

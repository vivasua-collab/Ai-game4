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
using System.Linq;
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
    /// R35 (Фаза 12 / P1-19, аудит 09.22 12:00): сервис — ISaveable +
    /// IWorldResettable. Прежде фактическое содержимое колец (_rings →
    /// StoredItems) не сохранялось и не сбрасывалось: warm-load УТЕЧКА
    /// (ResetWorld не существовал: EquipmentService.RestoreState
    /// реактивировал кольцо с содержимым прошлого мира), cold-load ПОТЕРЯ
    /// (содержимое вообще не в сейве). Теперь: блок "storage_rings"
    /// (полный снапшот; RestoreOrder — после "equipment") + сброс домена
    /// при пересборке мира. Контракт «реактивация не чистит» сохранён:
    /// LoadGame = ResetWorld (чисто) → equipment-restore реактивирует
    /// ПУСТОЕ кольцо → блок storage_rings восстанавливает содержимое сейва.
    ///
    /// STR-MODEL: вместимость определяется объёмом (maxVolume, литры),
    /// а не количеством слотов. Предмет добавляется, если
    /// CurrentVolume + item.Volume ≤ MaxVolume.
    ///
    /// Кольца хранения — экипируемые аксессуары (слоты Ring*).
    /// При экипировке кольца активируется его хранилище.
    /// При снятии — деактивируется, но предметы ОСТАЮТСЯ внутри.
    /// </summary>
    public class StorageRingService : IStorageRingService, ISaveable, IWorldResettable, IDisposable
    {
        // === Зависимости (DI через конструктор) ===
        private readonly ISubscriber<EquipmentChangedEvent> _equipChangedSub;
        private readonly IItemDatabaseService _itemDatabase;
        private readonly IEquipmentService _equipmentService;
        // Review этап 4 (P1-3): операции с кольцом реально платят Ци (контракт
        // IStorageRingService: qiCost — не только «посчитать», но и списать).
        private readonly IQiService? _qiService;

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
            IEquipmentService equipmentService,
            IQiService? qiService = null)
        {
            _equipChangedSub = equipChangedSub;
            _itemDatabase = itemDatabase;
            _equipmentService = equipmentService;
            _qiService = qiService;
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

            // Review этап 4 (P1-3): проверка баланса ДО изменения содержимого;
            // списание — после успешного мутацирования (транзакция: операция
            // с кольцом либо полностью проходит с оплатой, либо не проходит).
            if (!HasQiFor(qiCost))
            {
                Console.WriteLine($"[StorageRingService] Недостаточно Ци для помещения: {CurrentQi}/{qiCost}");
                return false;
            }

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
                        ChargeQi(qiCost);
                        return true;
                    }
                }
            }

            // Новый слот
            entry.StoredItems.Add(new InventorySlot(item.ItemId, 1, item.Category, item.Rarity));

            // STR-MODEL: пересчитать объём
            entry.RecalculateVolume(_itemDatabase);
            ChargeQi(qiCost);
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

                // Review этап 4 (P0-1-паттерн): предмет должен быть известен базе
                // ДО удаления из кольца (иначе та же потеря: удалли + item=null).
                if (_itemDatabase == null || !_itemDatabase.TryGetItem(storedItemId, out var itemData) || itemData == null)
                {
                    Console.WriteLine($"[StorageRingService] Предмет '{storedItemId}' не найден в ItemDatabase — извлечение отклонено");
                    return false;
                }

                item = itemData;
                itemWeight = itemData.Weight;

                qiCost = GetRetrieveQiCost(entry.Tier, itemWeight);

                // Review этап 4 (P1-3): проверка баланса ДО удаления из кольца.
                if (!HasQiFor(qiCost))
                {
                    Console.WriteLine($"[StorageRingService] Недостаточно Ци для извлечения: {CurrentQi}/{qiCost}");
                    item = null;
                    return false;
                }

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
                ChargeQi(qiCost);
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

        // === Review этап 4 (P1-3): Ци-транзакции ===

        private long CurrentQi => _qiService?.CurrentQi ?? 0;

        /// <summary>Баланса хватает на операцию? (списание — только после мутации)</summary>
        private bool HasQiFor(long cost)
        {
            return _qiService == null || cost <= 0 || _qiService.CurrentQi >= cost;
        }

        /// <summary>Списать Ци за операцию (после успешного изменения содержимого).</summary>
        private void ChargeQi(long cost)
        {
            if (_qiService != null && cost > 0)
            {
                _qiService.TryConsumeQi(cost);
            }
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

        // ════════════════════════════════════════════════════════════
        // R35 (Фаза 12 / P1-19): ISaveable — блок "storage_rings".
        // RestoreOrder: ПОСЛЕ "equipment" (реакция на EquipmentChangedEvent
        // при восстановлении экипировки создаёт пустые записи — блок
        // заполняет их содержимым сейва) и ПОСЛЕ "item_db"
        // (RecalculateVolume резолвит объёмы предметов).
        // ════════════════════════════════════════════════════════════

        /// <summary>Типизированный state-блок для round-trip десериализации.</summary>
        public sealed class StorageRingsSaveState
        {
            public List<RingEntrySave> Rings = new();
        }

        /// <summary>Снапшот одного кольца.</summary>
        public sealed class RingEntrySave
        {
            public string RingItemId = "";
            public int Tier;
            public float MaxVolume;
            public int Capacity;
            public bool IsActive;
            public List<InventorySlot> StoredItems = new();
        }

        public string SaveKey => "storage_rings";
        public Type StateType => typeof(StorageRingsSaveState);

        public object CaptureState()
        {
            var data = new StorageRingsSaveState();
            foreach (var kvp in _rings)
            {
                var entry = kvp.Value;
                var saved = new RingEntrySave
                {
                    RingItemId = entry.RingItemId,
                    Tier = entry.Tier,
                    MaxVolume = entry.MaxVolume,
                    Capacity = entry.Capacity,
                    IsActive = entry.IsActive,
                };
                foreach (var slot in entry.StoredItems)
                    if (!slot.IsEmpty)
                        saved.StoredItems.Add(slot);
                data.Rings.Add(saved);
            }
            return data;
        }

        public void RestoreState(object state)
        {
            if (state is not StorageRingsSaveState data || data == null) return;

            // Полная замена (не merge): LoadGame перед этим сбросил домен
            // (IWorldResettable ниже) — прямые вызовы тоже получают чистый
            // словарь, содержимое прошлого мира не протекает.
            _rings.Clear();

            int restored = 0;
            if (data.Rings != null)
            {
                foreach (var saved in data.Rings)
                {
                    if (saved == null || string.IsNullOrEmpty(saved.RingItemId)) continue;
                    var entry = new StorageRingEntry(saved.RingItemId, saved.Tier,
                        saved.MaxVolume, saved.Capacity)
                    {
                        IsActive = saved.IsActive,
                    };
                    if (saved.StoredItems != null)
                        foreach (var slot in saved.StoredItems)
                            if (slot is { IsEmpty: false })
                                entry.StoredItems.Add(slot);
                    entry.RecalculateVolume(_itemDatabase);
                    _rings[saved.RingItemId] = entry;
                    restored++;
                }
            }

            Console.WriteLine($"[StorageRingService] RestoreState: {restored} колец восстановлено " +
                              $"(предметов: {_rings.Values.Sum(r => r.StoredItems.Count)})");
        }

        // ── R35 (Фаза 12 / P1-19): IWorldResettable ───────────────────
        /// <summary>
        /// Пересборка мира (NewGame/LoadGame — сброс ДО RestoreState):
        /// хранилища колец world-scoped → полностью очищаются. Warm-load:
        /// EquipmentService.RestoreState реактивирует кольца из сейва
        /// (EquipmentChangedEvent) — реактивация в ПУСТОМ словаре создаёт
        /// чистые записи; затем блок storage_rings восстановит содержимое.
        /// </summary>
        public void ResetWorld()
        {
            _rings.Clear();
            Console.WriteLine("[StorageRingService] ResetWorld: хранилища колец очищены (P1-19)");
        }
    }
}

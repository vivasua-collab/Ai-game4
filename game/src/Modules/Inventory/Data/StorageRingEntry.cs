#nullable enable
// Создано: 2026-05-18 18:12:00 UTC
// Редактировано: 2026-05-18 18:39:47 UTC — STR-MODEL: добавлены MaxVolume, CurrentVolume
//   Вместимость кольца теперь определяется объёмом (литры), а не слотами.
// Запись в хранилище кольца.
// Хранит состояние одного кольца: активность, тир, объём, предметы.

using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Inventory.Data
{
    /// <summary>
    /// Запись в хранилище кольца.
    /// Каждое экипированное кольцо хранения имеет свою запись.
    /// Предметы остаются внутри при снятии кольца (деактивация ≠ очистка).
    ///
    /// STR-MODEL: вместимость определяется объёмом (maxVolume, литры),
    /// а не количеством слотов. Предмет добавляется, если
    /// CurrentVolume + item.Volume ≤ MaxVolume.
    /// </summary>
    public class StorageRingEntry
    {
        /// <summary>ID предмета-кольца (itemId из EquipmentData)</summary>
        public string RingItemId;

        /// <summary>Тир кольца (1-5)</summary>
        public int Tier;

        /// <summary>Максимальный объём (литры) — STR-MODEL</summary>
        public float MaxVolume;

        /// <summary>Текущий объём (литры) — STR-MODEL: пересчитывается из StoredItems</summary>
        public float CurrentVolume;

        /// <summary>Активно ли хранилище</summary>
        public bool IsActive;

        /// <summary>Предметы внутри кольца</summary>
        public readonly List<InventorySlot> StoredItems = new();

        // === Обратная совместимость ===
        /// <summary>Вместимость (слоты) — DEPRECATED, оставлен для совместимости</summary>
        public int Capacity;

        /// <summary>
        /// Создать запись кольца хранения (строчная модель).
        /// </summary>
        /// <param name="ringItemId">ID предмета-кольца</param>
        /// <param name="tier">Тир кольца (1-5)</param>
        /// <param name="maxVolume">Максимальный объём (литры)</param>
        /// <param name="capacity">Вместимость в слотах (DEPRECATED, для совместимости)</param>
        public StorageRingEntry(string ringItemId, int tier, float maxVolume, int capacity = 0)
        {
            RingItemId = ringItemId;
            Tier = tier;
            MaxVolume = maxVolume;
            Capacity = capacity;
            CurrentVolume = 0f;
            IsActive = true;
        }

        /// <summary>
        /// Пересчитать текущий объём из StoredItems.
        /// Вызывается после добавления/удаления предметов.
        /// </summary>
        public void RecalculateVolume(IItemDatabaseService itemDatabase)
        {
            CurrentVolume = 0f;
            foreach (var slot in StoredItems)
            {
                if (slot.IsEmpty) continue;
                if (itemDatabase != null && itemDatabase.TryGetItem(slot.ItemId, out var item))
                    CurrentVolume += item.Volume * slot.Count;
                else
                    CurrentVolume += 1.0f * slot.Count; // Fallback
            }
        }
    }
}

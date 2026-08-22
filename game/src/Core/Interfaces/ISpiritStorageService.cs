#nullable enable
// Создано: 2026-08-22 — Q9: Разделение Spirit + Ring storage строго по доке.
// INVENTORY_SYSTEM.md §5: Spirit Storage — духовное хранилище (unlimited, Qi cost per access).
// INVENTORY_SYSTEM.md §6: Storage Ring — кольцо хранения (экипируется, N слотов).
//
// Раньше был единый IStorageService с StorageType { Spirit, Ring }.
// Теперь разделены: ISpiritStorageService (этот файл) + IStorageRingService (уже существует).
using System.Collections.Generic;
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Духовное хранилище (Spirit Storage).
    /// INVENTORY_SYSTEM.md §5: unlimited slots, Qi cost per access.
    /// Предметы хранятся в "духовном пространстве" культиватора.
    /// </summary>
    public interface ISpiritStorageService
    {
        /// <summary>Вместимость (для spirit — можно unlimited, но пока лимит).</summary>
        int Capacity { get; }

        /// <summary>Занято слотов.</summary>
        int UsedSlots { get; }

        /// <summary>Текущий Qi игрока (для проверки стоимости доступа).</summary>
        long CurrentQi { get; }

        /// <summary>
        /// Попытаться поместить предмет в духовное хранилище.
        /// Списывает Qi за доступ (if Qi insufficient → fail).
        /// </summary>
        bool TryStore(ItemData item);

        /// <summary>
        /// Попытаться извлечь предмет.
        /// Списывает Qi за доступ.
        /// </summary>
        bool TryRetrieve(string itemId, out ItemData item);

        /// <summary>Стоимость Qi для одной операции store/retrieve.</summary>
        long GetAccessCost();

        /// <summary>Все предметы в хранилище.</summary>
        IReadOnlyList<InventorySlot> GetStoredItems();
    }
}

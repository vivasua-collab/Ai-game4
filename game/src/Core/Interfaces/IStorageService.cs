#nullable enable
// Создано: 2026-05-10 07:36:53 UTC
// Аудит P0-02: вынесен из IInventoryService.cs (1 интерфейс = 1 файл)
using CultivationGame.Core.Data;

using CultivationGame.Core;
namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Интерфейс хранилища предметов.
    /// Два типа: Spirit (духовное) и Ring (кольцо).
    /// </summary>
    public interface IStorageService
    {
        StorageType Type { get; }
        int Capacity { get; }
        int UsedSlots { get; }
        bool TryStore(ItemData item);
        bool TryRetrieve(string itemId, out ItemData item);
        System.Collections.Generic.IReadOnlyList<InventorySlot> GetStoredItems();
    }
}

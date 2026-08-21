#nullable enable
// Создано: 2026-05-08 10:07:00 UTC
// Редактировано: 2026-05-08 10:55:19 UTC — добавлен using CultivationGame.Core.Data для ItemData
// Редактировано: 2026-05-10 07:36:53 UTC — аудит P0-02: IStorageService, ICraftingService, StorageType
// Редактировано: 2026-05-18 18:39:47 UTC — STR-MODEL: CanFitItem, HowManyCanFit,
//   GetCurrentWeight, GetCurrentVolume, GetEffectiveMaxWeight/Volume
// Интерфейс инвентаря игрока (строчная модель: вес + объём).
using System.Collections.Generic;
using CultivationGame.Core.Data;

using CultivationGame.Core;
namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Интерфейс инвентаря игрока.
    /// Строчная модель: ограничители — вес (кг) и объём (л), НЕ сетка слотов.
    /// </summary>
    public interface IInventoryService
    {
        bool TryAddItem(ItemData item, int count = 1);
        bool TryRemoveItem(string itemId, int count = 1);
        int GetItemCount(string itemId);
        IReadOnlyList<InventorySlot> GetAllSlots();
        int TotalSlots { get; }
        int UsedSlots { get; }

        // === STR-MODEL: методы для работы с весом и объёмом ===

        /// <summary>
        /// Проверить, поместится ли предмет в инвентарь.
        /// Строчная модель: проверка по весу и объёму с учётом рюкзака.
        /// </summary>
        bool CanFitItem(ItemData item, int count = 1);

        /// <summary>
        /// Сколько предметов данного типа поместится в инвентарь.
        /// Ограничено: вес (эффективный макс), объём (эффективный макс).
        /// </summary>
        int HowManyCanFit(ItemData item);

        /// <summary>
        /// Текущий суммарный вес предметов (кг).
        /// </summary>
        float GetCurrentWeight();

        /// <summary>
        /// Текущий суммарный объём предметов (л).
        /// </summary>
        float GetCurrentVolume();

        /// <summary>
        /// Эффективный максимальный вес с учётом бонусов рюкзака.
        /// Формула: baseMaxWeight + backpack.weightBonus
        /// </summary>
        float GetEffectiveMaxWeight();

        /// <summary>
        /// Эффективный максимальный объём с учётом бонусов рюкзака.
        /// Формула: baseMaxVolume + backpack.volumeBonus
        /// </summary>
        float GetEffectiveMaxVolume();

        /// <summary>
        /// Is player carrying more than effective max weight? (overweight)
        /// Items still enter inventory, but movement speed is reduced.
        /// </summary>
        bool IsOverweight { get; }

        /// <summary>
        /// Weight overload ratio: 0 = no overload, 1.0 = 2× max, 2.0 = 3× max, etc.
        /// Used to scale movement speed penalty (0.5 - 1.0 × speed).
        /// </summary>
        float OverweightRatio { get; }
    }
}

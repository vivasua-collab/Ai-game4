#nullable enable
// Создано: 2026-05-08 10:07:00 UTC
// Редактировано: 2026-05-08 10:55:19 UTC — добавлен using CultivationGame.Core.Data для ItemData
// Редактировано: 2026-05-10 07:36:53 UTC — аудит P0-02: IStorageService, ICraftingService, StorageType
// Редактировано: 2026-05-18 18:39:47 UTC — STR-MODEL: CanFitItem, HowManyCanFit,
//   GetCurrentWeight, GetCurrentVolume, GetEffectiveMaxWeight/Volume
// Интерфейс инвентаря игрока (строчная модель: вес + объём).
using System;
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

        // === Кучки: работа с отдельными слотами (2026-09-09) ===

        /// <summary>
        /// Разделить слот на два стака («кучки»): перенести moveCount предметов
        /// из слота slotIndex в НОВЫЙ слот того же типа.
        /// Условия: предмет стакается; 1 ≤ moveCount ≤ slot.Count−1 (оба стака ≥ 1).
        /// Тотал по ItemId не меняется — события не публикуются.
        /// Задача: отделить часть стака (выбросить/отложить под алхимию).
        /// </summary>
        bool TrySplitSlot(int slotIndex, int moveCount);

        /// <summary>
        /// Удалить ровно count предметов из КОНКРЕТНОГО слота slotIndex
        /// (а не со всех слотов, как TryRemoveItem). Слот либо уменьшается,
        /// либо удаляется целиком. Публикует ItemRemovedEvent.
        /// Используется drop-ом кучки в корзину: выбрасывается только
        /// перетащенный стак, остальные кучки того же предмета остаются.
        /// </summary>
        bool TryRemoveFromSlot(int slotIndex, int count);

        // === SlotId-адресность (review R10, 2026-09-09): TOCTOU-защита ===

        /// <summary>
        /// Индекс слота по стабильной идентичности кучки; −1 — SlotId не
        /// найден (кучка исчезла: удалена/слита/инвентарь перезагружен).
        /// UI-действия (drag&drop, split) обязаны разрешать адрес ТОЛЬКО
        /// так — индекс списка дрейфует при мутациях между захватом и
        /// действием.
        /// </summary>
        int FindSlotIndexBySlotId(Guid slotId);

        /// <summary>
        /// SlotId-адресное разделение стака: находит кучку по SlotId,
        /// сверяет expectedItemId (защита от подмены) и делит её.
        /// Отказ (false), если кучка исчезла или ItemId не совпал —
        /// вызывающий НЕ должен применять операцию к другому слоту.
        /// </summary>
        bool TrySplitSlot(Guid slotId, string expectedItemId, int moveCount);

        /// <summary>
        /// SlotId-адресное удаление: находит кучку по SlotId, сверяет
        /// expectedItemId, удаляет ровно count шт. из ЭТОЙ кучки.
        /// Отказ (false) при исчезновении/несовпадении — деструктивный
        /// фолбэк «удалить все слоты предмета» со стороны UI запрещён.
        /// </summary>
        bool TryRemoveFromSlot(Guid slotId, string expectedItemId, int count);

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

#nullable enable
// Создано: 2026-05-18 17:58:25 UTC
// Сервис разрешения itemId → ItemData.
// Загружает предустановленные SO из Resources + регистрирует runtime-сгенерированные.
using CultivationGame.Core.Data;
using System.Collections.Generic;

using CultivationGame.Core;
namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Сервис разрешения itemId → ItemData.
    /// Предустановленные предметы загружаются из Resources/Items.
    /// Runtime-сгенерированные (от генераторов) регистрируются через Register().
    /// </summary>
    public interface IItemDatabaseService
    {
        /// <summary>Получить ItemData по itemId. Возвращает false если не найден.</summary>
        bool TryGetItem(string itemId, out ItemData item);

        /// <summary>Зарегистрировать runtime-сгенерированный ItemData.</summary>
        void Register(ItemData item);

        /// <summary>Зарегистрировать несколько ItemData.</summary>
        void RegisterRange(IEnumerable<ItemData> items);

        /// <summary>Все зарегистрированные предметы.</summary>
        IReadOnlyList<ItemData> GetAllItems();

        /// <summary>Фильтр по категории.</summary>
        IReadOnlyList<ItemData> GetItemsByCategory(ItemCategory category);

        /// <summary>Количество зарегистрированных предметов.</summary>
        int Count { get; }
    }
}

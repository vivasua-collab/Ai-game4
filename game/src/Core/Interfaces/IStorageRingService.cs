#nullable enable
// Создано: 2026-05-18 18:12:00 UTC
// Интерфейс сервиса колец хранения.
// Кольца хранения — экипируемые аксессуары со встроенным хранилищем.
// Помещение/извлечение предметов стоит Qi.

using System.Collections.Generic;
using CultivationGame.Core.Data;

using CultivationGame.Core;
namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Интерфейс сервиса колец хранения.
    /// Управляет хранилищами, привязанными к экипированным кольцам.
    /// Qi cost формулы из legacy: baseQiCost = 10 + tier * 5.
    /// </summary>
    public interface IStorageRingService
    {
        /// <summary>Поместить предмет в хранилище кольца</summary>
        bool TryStore(string ringItemId, ItemData item, out long qiCost);

        /// <summary>Извлечь предмет из хранилища кольца</summary>
        bool TryRetrieve(string ringItemId, string storedItemId, out ItemData item, out long qiCost);

        /// <summary>Получить содержимое хранилища кольца</summary>
        IReadOnlyList<InventorySlot> GetRingContents(string ringItemId);

        /// <summary>Qi стоимость помещения предмета</summary>
        long GetStoreQiCost(int ringTier, float itemWeight);

        /// <summary>Qi стоимость извлечения предмета</summary>
        long GetRetrieveQiCost(int ringTier, float itemWeight);

        /// <summary>Активировать хранилище для кольца (при экипировке)</summary>
        void ActivateRing(string ringItemId, int tier, int capacity);

        /// <summary>Деактивировать хранилище кольца (при снятии)</summary>
        void DeactivateRing(string ringItemId);

        /// <summary>Проверить, активно ли кольцо</summary>
        bool IsRingActive(string ringItemId);
    }
}

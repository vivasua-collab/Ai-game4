#nullable enable
// Создано: 2026-08-25 — NPC_COMBAT_PREP Phase 4: позиция ассортимента торговца.
// Core-модель (engine-agnostic): TradeService наполняет, TradeWindow читает.

using System;

namespace CultivationGame.Core.Data
{
    /// <summary>
    /// Одна позиция ассортимента торговца: предмет + доступное количество.
    /// Ассортимент персистентен в рамках сессии (генерируется при первом
    /// OpenTrade, выкупленное количество не восстанавливается до рестока —
    /// будущие фазы, см. RestockTimestamp).
    /// </summary>
    public class MerchantStockEntry
    {
        /// <summary>Владелец ассортимента (npcId торговца).</summary>
        public string NpcId = string.Empty;

        /// <summary>Идентификатор предмета (itemId из IItemDatabaseService).</summary>
        public string ItemId = string.Empty;

        /// <summary>Доступное количество (уменьшается при покупке).</summary>
        public int Count;

        /// <summary>
        /// Метка времени (игровые секунды) следующего пополнения.
        /// V1 не используется (ресток — будущая фаза), поле зарезервировано,
        /// чтобы не ломать контракт при добавлении.
        /// </summary>
        public float RestockTimestamp;

        /// <summary>Изначальное количество (для будущей статистики продаж).</summary>
        public int InitialCount;
    }
}

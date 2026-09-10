#nullable enable
// Создано: 2026-09-10 — R13 FULL-LOOT: контейнер «труп NPC».
// Доступ к инвентарю мёртвого NPC: при смерти NPC его экипировка, инвентарь
// и духовные камни снимаются в CorpseData (снапшот), который лежит на месте
// смерти до обыска (E) и/или истечения срока (TTL).
//
// АРХИТЕКТУРА (SlotId-адресность, паттерн R10 TOCTOU-фикса): каждый предмет
// трупа адресуется уникальным Guid SlotId. LootWindow берёт предмет ТОЛЬКО
// по адресу — «снимок содержимого → клик по строке X» больше не может
// ошибиться при изменении списка между обновлением UI и кликом.
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Data
{
    /// <summary>
    /// Один предмет в трупе (адресуемый по SlotId).
    /// Source помечает происхождение: "equipment" | "inventory" | "spirit_stones"
    /// — для UI-группировки (экипировка с ролей, карманы, камни).
    /// </summary>
    public readonly struct CorpseItem
    {
        public readonly Guid SlotId;
        public readonly string ItemId;
        public readonly int Count;
        public readonly ItemRarity Rarity;
        public readonly string Source;

        public CorpseItem(Guid slotId, string itemId, int count, ItemRarity rarity, string source)
        {
            SlotId = slotId;
            ItemId = itemId;
            Count = count;
            Rarity = rarity;
            Source = source ?? "";
        }

        public override string ToString() => $"{ItemId}×{Count} ({Source})";
    }

    /// <summary>
    /// Труп NPC — контейнер лута на месте смерти.
    /// Создаётся CorpseService по NPCDeathEvent, опустошается обыском
    /// (TryTakeItem / LootAll через ICorpseService), удаляется при
    /// опустошении либо по TTL (RemoveOldCorpses).
    /// </summary>
    public sealed class CorpseData
    {
        public string CorpseId = "";
        public string NpcId = "";
        public string DisplayName = "";
        public string SpeciesId = "";
        public Position2D Position;          // тайл места смерти
        public string KillerId = "";
        /// <summary>Игровое время смерти (ITimeService.TotalTime, сек).</summary>
        public float DiedAtGameSeconds;
        public int NpcLevel;                 // уровень культивации (для QA/логов)

        /// <summary>Предметы трупа (мутабельно: обыск удаляет элементы).</summary>
        public List<CorpseItem> Items = new();

        public bool IsEmpty => Items.Count == 0;
        public int ItemCount => Items.Count;
    }
}

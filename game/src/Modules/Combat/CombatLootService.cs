#nullable enable
// Создано: 2026-05-09
// Редактировано: 2026-08-22 — IMPL-6 (Q5): Random → ICombatRng (детерминированный бой).
// Редактировано: 2026-05-09 — CMB-A09: вызов IInventoryService.TryAddItem() в GrantLoot
// Редактировано: 2026-05-09 — EVT-02: убрана инъекция IInventoryService,
//   добавление лута через ItemAddRequestEvent вместо прямого вызова
// Редактировано: 2026-05-18 17:58:25 UTC — P0-04 FIX: IItemGeneratorService вместо placeholder IDs
//   P0-05 FIX: убран double-publish ItemAddedEvent; LootEntry из Core/Data
// Редактировано: 2026-05-20 19:11:00 UTC — Фаза 4, задача 4.1: NPC дроп при смерти
// Сервис лута после боя — генерация и выдача предметов.
// Перенесено из legacy Combat/LootGenerator.cs + CombatLootHandler.cs с адаптацией.
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Combat
{
    /// <summary>
    /// Сервис лута после боя.
    /// Генерирует предметы через IItemGeneratorService и добавляет в инвентарь.
    ///
    /// АРХИТЕКТУРА: CombatLootService НЕ инжектит IInventoryService напрямую.
    /// EVT-02: добавление лута через ItemAddRequestEvent (вместо IInventoryService.TryAddItem).
    /// InventoryModule подписывается на ItemAddRequestEvent и обрабатывает внутренне.
    ///
    /// P0-04 FIX: Использует IItemGeneratorService для реальных предметов.
    /// P0-05 FIX: Публикует ТОЛЬКО ItemAddRequestEvent. InventoryService публикует ItemAddedEvent.
    ///
    /// Фаза 4, задача 4.1: GenerateNPCLoot — дроп экипировки, инвентаря и духовных камней NPC.
    /// </summary>
    public class CombatLootService
    {
        // === Зависимости ===
        private readonly IItemGeneratorService _generator;
        private readonly IPublisher<ItemAddRequestEvent> _itemAddRequestPub; // EVT-02: command-событие
        private readonly IItemDatabaseService _itemDatabase; // 4.1: для определения редкости экипировки NPC
        private readonly ICombatRng _rng; // Q5: deterministic RNG

        // === Настройки ===
        private const int MinLootItems = 1;
        private const int MaxLootItems = 3;

        // === Идентификаторы духовных камней (4.1) ===
        private const string SpiritStoneShardId = "spirit_stone_shard";
        private const string SpiritStoneFragmentId = "spirit_stone_fragment";

        // === Конструктор ===
        public CombatLootService(
            IItemGeneratorService generator,
            IPublisher<ItemAddRequestEvent> itemAddRequestPub,
            ICombatRng rng, // IMPL-6 (Q5): deterministic RNG (заменяет static Random)
            IItemDatabaseService itemDatabase = null)
        {
            _generator = generator ?? throw new System.ArgumentNullException(nameof(generator));
            _itemAddRequestPub = itemAddRequestPub;
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _itemDatabase = itemDatabase; // Опциональная зависимость — для редкости экипировки
        }

        /// <summary>
        /// Сгенерировать лут из убитого врага.
        /// P0-04 FIX: Использует IItemGeneratorService для реальных предметов
        /// вместо placeholder IDs вида "loot_enemy_Common_0".
        /// Q5: броски через ICombatRng (детерминированный бой).
        /// </summary>
        public List<LootEntry> GenerateLoot(string enemyId, int enemyLevel)
        {
            var loot = new List<LootEntry>();
            int itemCount = _rng.Next(MinLootItems, MaxLootItems + 1);

            for (int i = 0; i < itemCount; i++)
            {
                // 70% экипировка, 30% расходники
                if (_rng.NextBool(0.7f))
                {
                    var equip = _generator.GenerateRandomEquipment(enemyLevel);
                    loot.Add(new LootEntry(equip.ItemId, 1, equip.Rarity, "combat"));
                }
                else
                {
                    var consumable = _generator.GenerateConsumableForLevel(enemyLevel);
                    loot.Add(new LootEntry(consumable.ItemId, 1, consumable.Rarity, "combat"));
                }
            }

            Console.WriteLine($"[CombatLootService] Лут для {enemyId} (уровень {enemyLevel}): {loot.Count} предметов");
            return loot;
        }

        /// <summary>
        /// Добавить сгенерированный лут в инвентарь.
        /// P0-05 FIX: публикует ТОЛЬКО ItemAddRequestEvent.
        /// InventoryService.TryAddItem() публикует ItemAddedEvent внутри.
        /// Убран double-publish.
        /// </summary>
        public void GrantLoot(List<LootEntry> loot)
        {
            if (loot == null) return;

            foreach (var entry in loot)
            {
                // EVT-02: запрашиваем добавление предмета через событие
                _itemAddRequestPub.Publish(new ItemAddRequestEvent(entry.ItemId, entry.Count, "loot"));
                // P0-05 FIX: НЕ публикуем ItemAddedEvent — InventoryService делает это внутри TryAddItem
            }
        }

        // ================================================================
        // Фаза 4, задача 4.1: NPC дроп при смерти
        // ================================================================

        /// <summary>
        /// Сгенерировать лут из убитого NPC.
        /// Включает: экипировку, инвентарь и духовные камни.
        ///
        /// Вызывающая сторона передаёт данные NPC напрямую —
        /// CombatLootService не зависит от NPC-модуля.
        ///
        /// Духовные камни (детерминированно через SeededRandom):
        ///   Уровень 1-2: 1-3 осколка духового камня (spirit_stone_shard, Rare)
        ///   Уровень 3-4: 2-5 осколков духового камня (spirit_stone_shard, Rare)
        ///   Уровень 5+:  1-2 фрагмента духового камня (spirit_stone_fragment, Epic)
        /// </summary>
        /// <param name="enemyLevel">Уровень NPC — влияет на тип и количество духовных камней</param>
        /// <param name="equipmentIds">Экипировка NPC: слот → itemId</param>
        /// <param name="inventorySlots">Инвентарь NPC: слоты с предметами</param>
        /// <param name="seed">Seed для детерминированной генерации камней (0 = случайный)</param>
        public List<LootEntry> GenerateNPCLoot(
            int enemyLevel,
            Dictionary<EquipmentSlot, string> equipmentIds,
            List<InventorySlot> inventorySlots,
            long seed = 0)
        {
            var loot = new List<LootEntry>();

            // --- 1. Дроп всей экипировки NPC ---
            if (equipmentIds != null)
            {
                foreach (var kvp in equipmentIds)
                {
                    // Пропускаем пустой слот (None или null/empty itemId)
                    if (kvp.Key == EquipmentSlot.None) continue;
                    if (string.IsNullOrEmpty(kvp.Value)) continue;

                    ItemRarity rarity = ResolveRarity(kvp.Value);
                    loot.Add(new LootEntry(kvp.Value, 1, rarity, "combat"));
                }
            }

            // --- 2. Дроп всех предметов инвентаря NPC ---
            if (inventorySlots != null)
            {
                foreach (var slot in inventorySlots)
                {
                    if (slot.IsEmpty) continue;
                    if (slot.Count <= 0) continue;

                    loot.Add(new LootEntry(slot.ItemId, slot.Count, slot.Rarity, "combat"));
                }
            }

            // --- 3. Духовные камни (детерминированно через SeededRandom) ---
            var rng = seed != 0
                ? new SeededRandom(seed)
                : new SeededRandom();

            if (enemyLevel >= 1 && enemyLevel <= 2)
            {
                // Уровень 1-2: 1-3 осколка духового камня
                int count = rng.Next(1, 4); // [1, 4) = 1..3
                loot.Add(new LootEntry(SpiritStoneShardId, count, ItemRarity.Rare, "combat"));
            }
            else if (enemyLevel >= 3 && enemyLevel <= 4)
            {
                // Уровень 3-4: 2-5 осколков духового камня
                int count = rng.Next(2, 6); // [2, 6) = 2..5
                loot.Add(new LootEntry(SpiritStoneShardId, count, ItemRarity.Rare, "combat"));
            }
            else if (enemyLevel >= 5)
            {
                // Уровень 5+: 1-2 фрагмента духового камня
                int count = rng.Next(1, 3); // [1, 3) = 1..2
                loot.Add(new LootEntry(SpiritStoneFragmentId, count, ItemRarity.Epic, "combat"));
            }
            // Уровень < 1: духовных камней нет

            Console.WriteLine($"[CombatLootService] NPC лут (уровень {enemyLevel}): " +
                      $"{loot.Count} записей (экипировка + инвентарь + камни)");
            return loot;
        }

        /// <summary>
        /// Добавить сгенерированный NPC-лут в инвентарь игрока.
        /// Удобный метод-алиас для GrantLoot — делегирует вызов.
        /// Фаза 4, задача 4.1.
        /// </summary>
        public void GrantNPCLoot(List<LootEntry> loot)
        {
            GrantLoot(loot);
        }

        // === Вспомогательные методы (4.1) ===

        /// <summary>
        /// Определить редкость предмета по itemId.
        /// Использует IItemDatabaseService если доступен,
        /// иначе возвращает Common как запасной вариант.
        /// </summary>
        private ItemRarity ResolveRarity(string itemId)
        {
            if (_itemDatabase != null && _itemDatabase.TryGetItem(itemId, out var itemData))
            {
                return itemData.Rarity;
            }

            // Запасной вариант: Common если база данных недоступна
            return ItemRarity.Common;
        }
    }
}

#nullable enable
// Создано: 2026-09-10 — R13 FULL-LOOT: сидер классических предметов лута.
// Регистрирует в IItemDatabaseService «фантомные» ID, которые используются
// генерацией инвентаря NPC (NPCAssemblyService.FillInventory, фаза 4.1) и
// лут-сервисами (CombatLootService, CorpseService) с 2026-05, но НИКОГДА не
// были зарегистрированы в БД: взятие такого предмета в инвентарь молча
// отклонялось InventoryModule («Предмет не найден в ItemDatabase») —
// обнаружено QA GODOT_LOOT_DEBUG (2026-09-10).
//
// Паттерн — QiStoneSeeder (идемпотентный статический сидер).
// Вызов — GeneratorModule.Start() (владелец ItemDatabase).
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Generator
{
    /// <summary>
    /// Сидер классических предметов лута (материалы + духовные камни).
    /// Идемпотентен — повторные вызовы игнорируются.
    /// </summary>
    public static class ClassicLootSeeder
    {
        private static bool _seeded = false;

        /// <summary>Зарегистрировать канонические предметы лута в БД.</summary>
        public static void Seed(IItemDatabaseService database)
        {
            if (_seeded || database == null) return;
            _seeded = true;

            database.Register(new ItemData
            {
                ItemId = "material_iron_scrap",
                NameRu = "Железный лом",
                NameEn = "Iron Scrap",
                Description = "Обломки железа: годятся в дело кузнецу или в обмен на монету.",
                Category = ItemCategory.Material,
                ItemType = "material",
                Rarity = ItemRarity.Common,
                Stackable = true,
                MaxStack = 20,
                Weight = 0.5f,
                Volume = 0.4f,
                Value = 1,
            });

            database.Register(new ItemData
            {
                ItemId = "material_spirit_stone_shard",
                NameRu = "Крошка духовного камня",
                NameEn = "Spirit Stone Grit",
                Description = "Мелкая крошка одухотворённого камня — сырьё для артефакторов.",
                Category = ItemCategory.Material,
                ItemType = "material",
                Rarity = ItemRarity.Uncommon,
                Stackable = true,
                MaxStack = 20,
                Weight = 0.08f,
                Volume = 0.03f,
                Value = 6,
            });

            database.Register(new ItemData
            {
                ItemId = "spirit_stone_shard",
                NameRu = "Осколок духового камня",
                NameEn = "Spirit Stone Shard",
                Description = "Осколок камня, насыщенного Ци. Мелкая ходовая валюта культиваторов.",
                Category = ItemCategory.Material,
                ItemType = "currency",
                Rarity = ItemRarity.Rare,
                Stackable = true,
                MaxStack = 99,
                Weight = 0.05f,
                Volume = 0.02f,
                Value = 12,
            });

            database.Register(new ItemData
            {
                ItemId = "spirit_stone_fragment",
                NameRu = "Фрагмент духового камня",
                NameEn = "Spirit Stone Fragment",
                Description = "Крупный фрагмент камня Ци. Ценная валюта сект и торговцев.",
                Category = ItemCategory.Material,
                ItemType = "currency",
                Rarity = ItemRarity.Epic,
                Stackable = true,
                MaxStack = 99,
                Weight = 0.12f,
                Volume = 0.05f,
                Value = 30,
            });

            Console.WriteLine("[ClassicLootSeeder] Зарегистрировано 4 классических предмета лута " +
                "(material_iron_scrap, material_spirit_stone_shard, spirit_stone_shard, spirit_stone_fragment)");
        }
    }
}

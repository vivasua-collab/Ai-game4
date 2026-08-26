#nullable enable
// Создано: 2026-08-23 — Этап 7 внедрения ЦИ: сидер камней Ци.
// Регистрирует 10 канонических камней (5 размеров × calm/chaotic) в
// IItemDatabaseService. Идемпотентен. Используется InventoryWindow._Ready
// (DEBUG-сид) и CheatPanel.GrantQiStones (выдача игроку).
//
// ItemId формат: qistone_{size}_{type}
//   • qistone_dust_calm      — Пыль Ци (спокойная)            1 024 Ци
//   • qistone_pebble_calm    — Галька Ци (спокойная)          8 192 Ци
//   • qistone_shard_calm     — Осколок Ци (спокойная)        27 648 Ци
//   • qistone_stone_calm     — Камень Ци (спокойный)         65 536 Ци
//   • qistone_boulder_calm   — Глыба Ци (спокойная)         128 000 Ци
//   • qistone_dust_chaotic   — Пыль Хаотичной Ци               1 024 Ци
//   • ... (5 chaotic вариантов)
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Adapter.UI
{
    /// <summary>
    /// Сидер камней Ци — регистрирует канонический набор (5 размеров × 2 типа).
    /// Канон: GENERATORS_SYSTEM.md §10. Плотность 1024 ед/см³.
    ///
    /// Использование:
    ///   QiStoneSeeder.Seed(itemDatabase);     // зарегистрировать в БД
    ///   QiStoneSeeder.CreateRandom(rng);      // получить случайный камень (для CheatPanel)
    /// </summary>
    public static class QiStoneSeeder
    {
        private static bool _seeded = false;

        private static readonly QiStoneSize[] AllSizes =
        {
            QiStoneSize.Dust, QiStoneSize.Pebble, QiStoneSize.Shard,
            QiStoneSize.Stone, QiStoneSize.Boulder,
        };

        private static readonly string[] SizeNameRu =
        {
            "Пыль", "Галька", "Осколок", "Камень", "Глыба",
        };

        private static readonly string[] SizeNameEn =
        {
            "Dust", "Pebble", "Shard", "Stone", "Boulder",
        };

        /// <summary>
        /// Зарегистрировать все 10 канонических камней Ци в IItemDatabaseService.
        /// Идемпотентно — повторные вызовы игнорируются.
        /// </summary>
        public static void Seed(IItemDatabaseService database)
        {
            if (_seeded || database == null) return;
            _seeded = true;

            foreach (var size in AllSizes)
            {
                // calm (безопасный)
                database.Register(Create(size, isChaotic: false));
                // chaotic (опасный)
                database.Register(Create(size, isChaotic: true));
            }
        }

        /// <summary>
        /// Создать камень Ци с заданными параметрами (не регистрирует в БД).
        /// ItemId формат: qistone_{size}_{type}.
        /// </summary>
        public static QiStoneData Create(QiStoneSize size, bool isChaotic)
        {
            int volumeCm3 = QiStoneData.CanonicalVolumeCm3(size);
            long qiAmount = QiStoneData.CanonicalQiAmount(size);

            int sizeIdx = (int)size;
            string sizeToken = SizeNameEn[sizeIdx].ToLowerInvariant();
            string typeToken = isChaotic ? "chaotic" : "calm";
            string itemId = $"qistone_{sizeToken}_{typeToken}";

            string nameRu = isChaotic
                ? $"{SizeNameRu[sizeIdx]} Хаотичного Ци"
                : $"{SizeNameRu[sizeIdx]} Ци";
            string nameEn = isChaotic
                ? $"Chaotic Qi {SizeNameEn[sizeIdx]}"
                : $"Qi {SizeNameEn[sizeIdx]}";

            string desc = isChaotic
                ? $"Хаотичный кристалл Ци. Объём: {qiAmount} ед. ОПАСЕН: −10% HP при использовании (риск 10%)."
                : $"Спокойный кристалл Ци. Объём: {qiAmount} ед. Безопасен для поглощения.";

            // Редкость: больше камень → реже (для чит-выдачи: dust=Common, boulder=Legendary).
            ItemRarity rarity = size switch
            {
                QiStoneSize.Dust    => ItemRarity.Common,
                QiStoneSize.Pebble  => ItemRarity.Uncommon,
                QiStoneSize.Shard   => ItemRarity.Rare,
                QiStoneSize.Stone   => ItemRarity.Epic,
                QiStoneSize.Boulder => ItemRarity.Legendary,
                _ => ItemRarity.Common,
            };

            // Физические параметры: камень Ци ~2.6 г/см³ (кристалл). Вес в кг.
            float weightKg = volumeCm3 * 0.0026f;
            // Объём в литрах для инвентаря (1 л = 1000 см³).
            float volumeL = volumeCm3 / 1000f;
            // Минимум 0.001 л чтобы не «исчез» в инвентаре.
            if (volumeL < 0.001f) volumeL = 0.001f;

            // Стоимость: 1 духовный камень = 1024 Ци (калькуляция по объёму).
            int value = (int)(qiAmount / 1024);
            if (value < 1) value = 1;

            // Chaotic немного дороже (опасный товар).
            if (isChaotic) value = (int)(value * 1.2);

            return new QiStoneData
            {
                ItemId = itemId,
                NameRu = nameRu,
                NameEn = nameEn,
                Description = desc,
                Category = ItemCategory.QiStone,
                ItemType = "QiStone",
                Rarity = rarity,
                Stackable = true,
                MaxStack = 50,
                Weight = weightKg,
                Volume = volumeL,
                Value = value,
                HasDurability = false,
                Size = size,
                IsChaotic = isChaotic,
                QiAmount = qiAmount,
                QiRemaining = qiAmount,
                VolumeCm3 = volumeCm3,
            };
        }

        /// <summary>
        /// Получить список всех 10 ItemId камней Ци.
        /// </summary>
        public static IReadOnlyList<string> AllItemIds()
        {
            var ids = new List<string>(10);
            foreach (var size in AllSizes)
            {
                int idx = (int)size;
                string sizeToken = SizeNameEn[idx].ToLowerInvariant();
                ids.Add($"qistone_{sizeToken}_calm");
                ids.Add($"qistone_{sizeToken}_chaotic");
            }
            return ids;
        }
    }
}

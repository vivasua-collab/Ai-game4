#nullable enable
// Создано: 2026-08-25 — NPC_COMBAT_PREP Phase 4: реализация ITradeService.
// Лавка торговца: генерация ассортимента (детерминированный сид от npcId),
// сделки покупки/продажи за духовные камни, события жизненного цикла.
//
// Архитектура (EVT-01): кросс-модульное взаимодействие — через EventBus:
//   - TradeRequestedEvent (подписка через TradeModule) → OpenTrade
//   - TradeOpenedEvent / TradeClosedEvent / TradeCompletedEvent /
//     TradeFailedEvent (публикация) → UI (TradeWindow) и GameWorldController.
// Сервисы игрока (инвентарь, валюта, база предметов, генераторы) инжектятся
// напрямую — прецедент: BeltService инжектит сервисы того же контейнера.
//
// ЗАПРЕТ 3.9: все цены/балансы — int, наценки — промилле (Permil.Apply).
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Trade
{
    /// <summary>
    /// Сервис торговли с NPC-торговцами. Ассортимент персистентен per-merchant
    /// в рамках сессии: генерируется при первом OpenTrade(npcId) из
    /// детерминированного сида (FNV-1a от npcId), выкупленное не
    /// восстанавливается (ресток — будущая фаза, см. MerchantStockEntry).
    /// </summary>
    public sealed class TradeService : ITradeService
    {
        // === Зависимости ===
        [Inject] private readonly IInventoryService _inventory = null!;
        [Inject] private readonly IItemDatabaseService _itemDb = null!;
        [Inject] private readonly IEquipmentGenerator _equipmentGenerator = null!;
        [Inject] private readonly IItemGeneratorService _itemGenerator = null!;
        [Inject] private readonly INPCService _npcService = null!;
        [Inject] private readonly ICurrencyService _currency = null!;
        [Inject] private readonly TradeConfig _config = null!;

        // === EventBus ===
        [Inject] private readonly IPublisher<TradeOpenedEvent> _openedPub = null!;
        [Inject] private readonly IPublisher<TradeClosedEvent> _closedPub = null!;
        [Inject] private readonly IPublisher<TradeCompletedEvent> _completedPub = null!;
        [Inject] private readonly IPublisher<TradeFailedEvent> _failedPub = null!;

        // === Состояние ===
        private readonly Dictionary<string, List<MerchantStockEntry>> _stockByMerchant = new();
        private string? _activeMerchantId;

        /// <summary>ItemId материала для стопки в лавке (регистрируется при отсутствии).</summary>
        private const string MaterialItemId = "material_iron_ore";

        /// <inheritdoc/>
        public string? ActiveMerchantId => _activeMerchantId;

        /// <inheritdoc/>
        public bool IsTrading => _activeMerchantId != null;

        // === Жизненный цикл сессии ===

        /// <inheritdoc/>
        public void OpenTrade(string npcId)
        {
            if (string.IsNullOrEmpty(npcId))
            {
                Fail("Торговец не найден");
                return;
            }

            // Уже торгуем с этим же NPC — не переоткрываем (идемпотентность).
            if (IsTrading && _activeMerchantId == npcId) return;

            // Переход к другому торговцу — закрыть текущую сессию.
            if (IsTrading) CloseTrade();

            // Валидация: только Merchant (роль или диспозиция).
            var state = _npcService?.GetNPCState(npcId);
            if (state == null || !state.IsAlive
                || (state.Role != NPCRole.Merchant && state.Disposition != NPCDisposition.Merchant))
            {
                Console.WriteLine($"[Trade] OpenTrade отклонён — '{npcId}' не торговец");
                Fail("Этот NPC не торгует");
                return;
            }

            var stock = GetOrCreateStock(npcId);
            _activeMerchantId = npcId;

            Console.WriteLine($"[Trade] merchant stock: {stock.Count} items ({npcId})");
            _openedPub.Publish(new TradeOpenedEvent(npcId));
        }

        /// <inheritdoc/>
        public void CloseTrade()
        {
            if (!IsTrading) return;

            _activeMerchantId = null;
            _closedPub.Publish(new TradeClosedEvent());
        }

        // === Ассортимент ===

        /// <inheritdoc/>
        public IReadOnlyList<MerchantStockEntry> GetMerchantStock(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return Array.Empty<MerchantStockEntry>();
            var stock = GetOrCreateStock(npcId);
            // Снимок списка (shallow) — UI не мутирует сервисные коллекции.
            return new List<MerchantStockEntry>(stock);
        }

        private List<MerchantStockEntry> GetOrCreateStock(string npcId)
        {
            if (_stockByMerchant.TryGetValue(npcId, out var existing))
                return existing;

            var stock = GenerateStock(npcId);
            _stockByMerchant[npcId] = stock;
            return stock;
        }

        /// <summary>
        /// Сгенерировать ассортимент лавки. Детерминированно для npcId:
        /// сид = FNV-1a(npcId), генераторы принимают дочерние сиды
        /// (простые множители) — одинаковый npcId даёт одинаковую лавку.
        /// </summary>
        private List<MerchantStockEntry> GenerateStock(string npcId)
        {
            var stock = new List<MerchantStockEntry>();
            long seed = Fnv1a(npcId);
            var rng = new SeededRandom(seed);

            // === Оружие (levels 1-3, «Матрёшка») ===
            int weaponCount = rng.Next(_config.StockWeaponMin, _config.StockWeaponMax + 1);
            for (int i = 0; i < weaponCount; i++)
            {
                int level = rng.Next(1, 4); // 1-3
                var weapon = _equipmentGenerator.GenerateWeapon(level, null, seed + 101 + i * 7919);
                if (weapon != null)
                    stock.Add(NewEntry(npcId, weapon.ItemId, 1));
            }

            // === Броня (levels 1-3) ===
            int armorCount = rng.Next(_config.StockArmorMin, _config.StockArmorMax + 1);
            for (int i = 0; i < armorCount; i++)
            {
                int level = rng.Next(1, 4);
                var armor = _equipmentGenerator.GenerateArmor(level, null, seed + 211 + i * 7919);
                if (armor != null)
                    stock.Add(NewEntry(npcId, armor.ItemId, 1));
            }

            // === Расходники (heal/qi пилюли через ItemGeneratorService) ===
            int consumableCount = rng.Next(_config.StockConsumableMin, _config.StockConsumableMax + 1);
            for (int i = 0; i < consumableCount; i++)
            {
                int level = rng.Next(1, 4);
                var consumable = _itemGenerator.GenerateConsumableForLevel(level, seed + 307 + i * 6271);
                if (consumable == null) continue;

                // Разнообразие: нечётные позиции — пилюли Ци (qi_restore),
                // чётные оставляем heal («Лекарство уровня N» от генератора).
                if (i % 2 == 1)
                {
                    consumable.NameRu = $"Пилюля Ци уровня {level}";
                    consumable.Effects = new List<ItemEffect>
                    {
                        new ItemEffect { EffectType = "qi_restore", Value = 20 + level * 10 },
                    };
                }

                int stack = rng.Next(_config.ConsumableStackMin, _config.ConsumableStackMax + 1);
                stock.Add(NewEntry(npcId, consumable.ItemId, stack));
            }

            // === Материалы (регистрируем канонический предмет при отсутствии) ===
            for (int i = 0; i < _config.StockMaterialCount; i++)
            {
                var material = EnsureMaterialItem();
                int stack = rng.Next(_config.MaterialStackMin, _config.MaterialStackMax + 1);
                stock.Add(NewEntry(npcId, material.ItemId, stack));
            }

            Console.WriteLine($"[Trade] Сгенерирован ассортимент для {npcId}: " +
                              $"{weaponCount} оруж. + {armorCount} брони + {consumableCount} расходн. + " +
                              $"{_config.StockMaterialCount} матер. (seed={seed})");
            return stock;
        }

        private MerchantStockEntry NewEntry(string npcId, string itemId, int count)
        {
            return new MerchantStockEntry
            {
                NpcId = npcId,
                ItemId = itemId,
                Count = count,
                InitialCount = count,
            };
        }

        /// <summary>Материал лавки: «Железная руда». Регистрируется только если ещё нет в БД.</summary>
        private ItemData EnsureMaterialItem()
        {
            if (_itemDb.TryGetItem(MaterialItemId, out var existing))
                return existing;

            var item = new ItemData
            {
                ItemId = MaterialItemId,
                NameRu = "Железная руда",
                NameEn = "Iron Ore",
                Description = "Материал для крафта. Торговец продаёт её оптом.",
                Category = ItemCategory.Material,
                ItemType = "Material",
                Rarity = ItemRarity.Common,
                Stackable = true,
                MaxStack = 100,
                Weight = 1.5f,
                Volume = 1.0f,
                Value = 5,
                HasDurability = false,
            };
            _itemDb.Register(item);
            return item;
        }

        // === Сделки ===

        /// <inheritdoc/>
        public bool TryBuy(string npcId, string itemId, int count)
        {
            if (!IsTrading || _activeMerchantId != npcId)
            {
                Fail("Торговля не открыта");
                return false;
            }
            if (string.IsNullOrEmpty(itemId) || count <= 0) return false;

            if (!_itemDb.TryGetItem(itemId, out var item))
            {
                Fail("Предмет неизвестен");
                return false;
            }

            // Остаток в лавке: покупаем не больше, чем есть.
            var entry = FindEntry(npcId, itemId);
            int available = entry?.Count ?? 0;
            if (available <= 0)
            {
                Fail("Товара нет в наличии");
                return false;
            }
            int buyCount = Math.Min(count, available);

            // Вместимость инвентаря (объём): частичная покупка не нужна —
            // режем счёт до того, сколько физически влезет.
            int canFit = _inventory.HowManyCanFit(item);
            if (canFit <= 0)
            {
                Fail("Инвентарь полон");
                return false;
            }
            buyCount = Math.Min(buyCount, canFit);

            int unitPrice = GetBuyPrice(itemId);
            int total = unitPrice * buyCount;

            // Достаточно ли камней (строгая проверка на всю партию).
            if (_currency.SpiritStones < total)
            {
                Fail("Не хватает духовных камней");
                return false;
            }

            if (!_currency.Spend(total))
            {
                Fail("Не хватает духовных камней");
                return false;
            }

            if (!_inventory.TryAddItem(item, buyCount))
            {
                // Редкий краевой случай (гонка с вместимостью) — полный возврат.
                _currency.Add(total);
                Fail("Не удалось положить предмет в инвентарь");
                return false;
            }

            entry!.Count -= buyCount;

            Console.WriteLine($"[Trade] Buy: {itemId}×{buyCount} за {total} камней " +
                              $"(баланс: {_currency.SpiritStones})");
            _completedPub.Publish(new TradeCompletedEvent(npcId, itemId, buyCount, true, total));
            return true;
        }

        /// <inheritdoc/>
        public bool TrySell(string npcId, string itemId, int count)
        {
            if (!IsTrading || _activeMerchantId != npcId)
            {
                Fail("Торговля не открыта");
                return false;
            }
            if (string.IsNullOrEmpty(itemId) || count <= 0) return false;

            if (!_itemDb.TryGetItem(itemId, out _))
            {
                Fail("Предмет неизвестен");
                return false;
            }

            // Продаём не больше, чем есть в инвентаре.
            int have = _inventory.GetItemCount(itemId);
            if (have <= 0)
            {
                Fail("Предмета нет в инвентаре");
                return false;
            }
            int sellCount = Math.Min(count, have);

            if (!_inventory.TryRemoveItem(itemId, sellCount))
            {
                Fail("Не удалось изъять предмет");
                return false;
            }

            int total = GetSellPrice(itemId) * sellCount;
            if (total > 0)
                _currency.Add(total);

            Console.WriteLine($"[Trade] Sell: {itemId}×{sellCount} за {total} камней " +
                              $"(баланс: {_currency.SpiritStones})");
            _completedPub.Publish(new TradeCompletedEvent(npcId, itemId, sellCount, false, total));
            return true;
        }

        // === Ценообразование (ЗАПРЕТ 3.9: промилле) ===

        /// <inheritdoc/>
        public int GetBuyPrice(string itemId)
        {
            if (!_itemDb.TryGetItem(itemId, out var item)) return 0;
            // Value × MarkupPermil / 1000, минимум 1 (торговец не отдаёт даром).
            return Math.Max(1, Permil.Apply(item.Value, _config.MarkupPermil));
        }

        /// <inheritdoc/>
        public int GetSellPrice(string itemId)
        {
            if (!_itemDb.TryGetItem(itemId, out var item)) return 0;
            // Value × SellPermil / 1000, минимум 0.
            return Math.Max(0, Permil.Apply(item.Value, _config.SellPermil));
        }

        // === Вспомогательные ===

        private MerchantStockEntry? FindEntry(string npcId, string itemId)
        {
            if (!_stockByMerchant.TryGetValue(npcId, out var stock)) return null;
            foreach (var entry in stock)
            {
                if (entry.ItemId == itemId) return entry;
            }
            return null;
        }

        private void Fail(string reason)
        {
            Console.WriteLine($"[Trade] Fail: {reason}");
            _failedPub.Publish(new TradeFailedEvent(reason));
        }

        /// <summary>
        /// FNV-1a 32-bit — детерминированный хеш строки (string.GetHashCode
        /// рандомизирован per-process в .NET, для сидов не подходит).
        /// </summary>
        private static long Fnv1a(string text)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char c in text)
                {
                    hash ^= c;
                    hash *= 16777619u;
                }
                // Избегаем нулевого сида (SeededRandom миксует, но перестрахуемся).
                return hash == 0 ? 1L : hash;
            }
        }
    }
}

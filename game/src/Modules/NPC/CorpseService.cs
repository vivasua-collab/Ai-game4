#nullable enable
// Создано: 2026-09-10 — R13 FULL-LOOT: сервис трупов NPC.
// Подписывается на NPCDeathEvent → снапшот экипировки + инвентаря + духовных
// камней погибшего в CorpseData (контейнер лута на месте смерти).
//
// ЗАМЕНА ЭТАПА 3 (2026-08-22): ранее NPCModule.OnNPCDeathForLoot ронял на землю
// 1-2 СЛУЧАЙНЫХ предмета из EquipmentGenerator (полнота лута не соблюдалась:
// реальная экипировка/инвентарь NPC исчезали). Теперь — ЧЕСТНЫЙ full loot:
// труп содержит ровно то, что было на NPC при жизни.
//
// Духовные камни — та же таблица, что CombatLootService.GenerateNPCLoot
// (фаза 4.1): L1-2: 1-3 осколка; L3-4: 2-5 осколков; L5+: 1-2 фрагмента.
//
// АРХИТЕКТУРА (EVT-02): выдача лута — ItemAddRequestEvent (command-событие),
// InventoryModule обрабатывает внутренне; переполнение инвентаря игрока
// уходит на землю (overflow-путь InventoryModule). CorpseService НЕ инжектит
// IInventoryService. SlotId-адресность — паттерн R10 (TOCTOU-защита).
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.NPC
{
    /// <summary>
    /// Реализация ICorpseService (R13 FULL-LOOT).
    /// </summary>
    public class CorpseService : ICorpseService, IDisposable
    {
        // === Зависимости ===
        private readonly NPCService _npcService;
        private readonly ITimeService _timeService;
        private readonly ISubscriber<NPCDeathEvent> _npcDeathSub;
        private readonly IPublisher<CorpseCreatedEvent> _corpseCreatedPub;
        private readonly IPublisher<CorpseRemovedEvent> _corpseRemovedPub;
        private readonly IPublisher<CorpseLootedEvent> _corpseLootedPub;
        private readonly IPublisher<ItemAddRequestEvent> _itemAddPub;
        private readonly IItemDatabaseService? _itemDatabase; // редкость экипировки (опционально)

        // === Идентификаторы духовных камней (таблица фазы 4.1) ===
        private const string SpiritStoneShardId = "spirit_stone_shard";
        private const string SpiritStoneFragmentId = "spirit_stone_fragment";

        // === Состояние ===
        private readonly List<CorpseData> _corpses = new();
        private long _nextCorpseSeq = 1;
        private IDisposable? _npcDeathSubscription;

        public CorpseService(
            NPCService npcService,
            ITimeService timeService,
            ISubscriber<NPCDeathEvent> npcDeathSub,
            IPublisher<CorpseCreatedEvent> corpseCreatedPub,
            IPublisher<CorpseRemovedEvent> corpseRemovedPub,
            IPublisher<CorpseLootedEvent> corpseLootedPub,
            IPublisher<ItemAddRequestEvent> itemAddPub,
            IItemDatabaseService? itemDatabase = null)
        {
            _npcService = npcService ?? throw new ArgumentNullException(nameof(npcService));
            _timeService = timeService;
            _npcDeathSub = npcDeathSub;
            _corpseCreatedPub = corpseCreatedPub;
            _corpseRemovedPub = corpseRemovedPub;
            _corpseLootedPub = corpseLootedPub;
            _itemAddPub = itemAddPub;
            _itemDatabase = itemDatabase;
        }

        /// <summary>Подписка на смерти NPC. Вызывается из NPCModule.Start().</summary>
        public void Initialize()
        {
            _npcDeathSubscription?.Dispose();
            _npcDeathSubscription = _npcDeathSub?.Subscribe(OnNPCDeath);
        }

        // === ICorpseService ===

        public int CorpseCount => _corpses.Count;

        public IReadOnlyList<CorpseData> GetAllCorpses() => _corpses.AsReadOnly();

        public CorpseData? GetCorpse(string corpseId)
        {
            if (string.IsNullOrEmpty(corpseId)) return null;
            for (int i = 0; i < _corpses.Count; i++)
                if (_corpses[i].CorpseId == corpseId) return _corpses[i];
            return null;
        }

        public CorpseData? FindNearestCorpse(Position2D position, float rangeTiles)
        {
            CorpseData? best = null;
            int bestDist = int.MaxValue;
            int range = (int)Math.Ceiling(rangeTiles);

            for (int i = 0; i < _corpses.Count; i++)
            {
                var c = _corpses[i];
                int dx = Math.Abs(c.Position.X - position.X);
                int dy = Math.Abs(c.Position.Y - position.Y);
                int dist = Math.Max(dx, dy);
                if (dist > range) continue;
                if (dist < bestDist) { bestDist = dist; best = c; }
            }
            return best;
        }

        public bool TryTakeItem(string corpseId, Guid slotId)
        {
            var corpse = GetCorpse(corpseId);
            if (corpse == null)
            {
                Console.WriteLine($"[CorpseService] TryTakeItem: труп '{corpseId}' не найден");
                return false;
            }

            // Адресный поиск (R10-паттерн): снимаем ровно тот SlotId, что
            // запросил UI — независимо от перерисовок списка между кадрами.
            int idx = -1;
            for (int i = 0; i < corpse.Items.Count; i++)
                if (corpse.Items[i].SlotId == slotId) { idx = i; break; }

            if (idx < 0)
            {
                Console.WriteLine($"[CorpseService] TryTakeItem: слот {slotId} в '{corpseId}' не найден (уже обыскан?)");
                return false;
            }

            var item = corpse.Items[idx];
            corpse.Items.RemoveAt(idx);

            // EVT-02: command-событие → InventoryModule.TryAddItem (overflow → земля).
            _itemAddPub?.Publish(new ItemAddRequestEvent(item.ItemId, item.Count, "loot"));
            _corpseLootedPub?.Publish(new CorpseLootedEvent(corpse.CorpseId, 1));
            Console.WriteLine($"[CorpseService] {corpse.DisplayName}: взято {item.ItemId}×{item.Count} (осталось {corpse.ItemCount})");

            if (corpse.IsEmpty)
                RemoveInternal(corpse, "looted");
            return true;
        }

        public int LootAll(string corpseId)
        {
            var corpse = GetCorpse(corpseId);
            if (corpse == null)
            {
                Console.WriteLine($"[CorpseService] LootAll: труп '{corpseId}' не найден");
                return 0;
            }

            int taken = corpse.Items.Count;
            if (taken == 0) return 0;

            // Каждая запись — отдельный ItemAddRequestEvent (стак не
            // разбивается: InventoryService сам стакнет однотипные).
            for (int i = 0; i < corpse.Items.Count; i++)
            {
                var item = corpse.Items[i];
                _itemAddPub?.Publish(new ItemAddRequestEvent(item.ItemId, item.Count, "loot"));
            }
            corpse.Items.Clear();

            _corpseLootedPub?.Publish(new CorpseLootedEvent(corpse.CorpseId, taken));
            Console.WriteLine($"[CorpseService] FULL LOOT: {corpse.DisplayName} — {taken} записей → инвентарь игрока");

            RemoveInternal(corpse, "looted");
            return taken;
        }

        public void RemoveCorpse(string corpseId)
        {
            var corpse = GetCorpse(corpseId);
            if (corpse == null) return;
            RemoveInternal(corpse, "manual");
        }

        public int RemoveOldCorpses(float maxAgeGameSeconds)
        {
            // TTL по игровому времени (DiedAtGameSeconds; при недоступном
            // ITimeService сравнение с текущим временем не выполняется —
            // трупы живут до обыска).
            float now = _timeService?.TotalTime ?? float.MinValue;
            if (now == float.MinValue) return 0;

            int removed = 0;
            for (int i = _corpses.Count - 1; i >= 0; i--)
            {
                if (now - _corpses[i].DiedAtGameSeconds > maxAgeGameSeconds)
                {
                    RemoveInternal(_corpses[i], "expired");
                    removed++;
                }
            }
            return removed;
        }

        public void Dispose()
        {
            _npcDeathSubscription?.Dispose();
            _npcDeathSubscription = null;
            _corpses.Clear();
        }

        // === Обработчик смерти NPC ===

        private void OnNPCDeath(in NPCDeathEvent e)
        {
            var state = _npcService.GetNPCState(e.NpcId);
            if (state == null)
            {
                // NPC мог быть деспавнут/не найден — труп не создаём.
                Console.WriteLine($"[CorpseService] NPCDeathEvent: состояние '{e.NpcId}' не найдено — труп не создан");
                return;
            }

            // Зашита от «фальшивых» смертей: реальный флоу (NPCCombatAdapter,
            // OnYearChanged) ставит IsAlive=false ДО публикации. Если NPC
            // жив — событие не соответствует состоянию (QA-публикация/баг) —
            // труп для живого не создаём (иначе: full loot живого NPC).
            if (state.IsAlive)
            {
                Console.WriteLine($"[CorpseService] NPCDeathEvent для ЖИВОГО '{e.NpcId}' — труп не создан " +
                    "(IsAlive=true; событие без реальной смерти)");
                return;
            }

            // Дедуп: повторное событие смерти того же NPC (kill-feed QA-паттерн,
            // ретраи) — труп уже существует, второй не создаём.
            for (int i = 0; i < _corpses.Count; i++)
            {
                if (_corpses[i].NpcId == e.NpcId)
                {
                    Console.WriteLine($"[CorpseService] Дедуп: труп для '{e.NpcId}' уже существует ({_corpses[i].CorpseId})");
                    return;
                }
            }

            var corpse = new CorpseData
            {
                CorpseId = $"corpse_{_nextCorpseSeq++}",
                NpcId = e.NpcId,
                DisplayName = state.DisplayName,
                SpeciesId = state.SpeciesId ?? "unknown",
                Position = state.Position,
                KillerId = e.KillerId ?? "",
                DiedAtGameSeconds = _timeService?.TotalTime ?? 0f,
                NpcLevel = (int)state.CultivationLevel,
                Items = BuildCorpseItems(state, e.NpcId),
            };

            _corpses.Add(corpse);

            _corpseCreatedPub?.Publish(new CorpseCreatedEvent(
                corpse.CorpseId, corpse.NpcId, corpse.DisplayName,
                corpse.Position.X, corpse.Position.Y, corpse.ItemCount));

            Console.WriteLine($"[CorpseService] Труп '{corpse.DisplayName}' ({corpse.CorpseId}) " +
                      $"на ({corpse.Position.X},{corpse.Position.Y}): {corpse.ItemCount} записей лута");
        }

        // === Сбор содержимого трупа ===

        /// <summary>
        /// Снапшот содержимого NPC: экипировка (все надетые слоты) +
        /// инвентарь (все стаками как есть) + духовные камни (таблица 4.1).
        /// Негуманоиды (звери) без экипировки/инвентаря дают только камни.
        ///
        /// Защита от «фантомных» ID (паттерн P1-5): предмет, неизвестный
        /// ItemDatabase, в труп НЕ попадает (взятие было бы отклонено
        /// InventoryModule — предмет потерялся бы молча). Предупреждение в лог —
        /// генератор обязан регистрировать всё, что выдаёт.
        /// </summary>
        private List<CorpseItem> BuildCorpseItems(NPCState state, string npcId)
        {
            var items = new List<CorpseItem>();

            int skippedUnknown = 0;

            bool IsKnown(string itemId)
            {
                if (_itemDatabase != null && !_itemDatabase.TryGetItem(itemId, out _))
                {
                    Console.WriteLine($"[CorpseService] ⚠ Предмет '{itemId}' неизвестен ItemDatabase — " +
                        "не попадает в труп (генератор не зарегистрировал его)");
                    skippedUnknown++;
                    return false;
                }
                return true;
            }

            // --- 1. Экипировка ---
            if (state.EquipmentIds != null)
            {
                foreach (var kvp in state.EquipmentIds)
                {
                    if (kvp.Key == EquipmentSlot.None) continue;
                    if (string.IsNullOrEmpty(kvp.Value)) continue;
                    if (!IsKnown(kvp.Value)) continue;

                    items.Add(new CorpseItem(
                        Guid.NewGuid(), kvp.Value, 1, ResolveRarity(kvp.Value), "equipment"));
                }
            }

            // --- 2. Инвентарь ---
            if (state.InventorySlots != null)
            {
                foreach (var slot in state.InventorySlots)
                {
                    if (slot.IsEmpty || slot.Count <= 0) continue;
                    if (!IsKnown(slot.ItemId)) continue;

                    items.Add(new CorpseItem(
                        Guid.NewGuid(), slot.ItemId, slot.Count, slot.Rarity, "inventory"));
                }
            }

            // --- 3. Духовные камни (детерминированно от npcId — таблица 4.1) ---
            // (зарегистрированы ClassicLootSeeder — проверка не нужна)
            int level = (int)state.CultivationLevel;
            var rng = new SeededRandom(npcId.GetHashCode());

            if (level >= 1 && level <= 2)
            {
                int count = rng.Next(1, 4); // 1..3 осколков
                items.Add(new CorpseItem(Guid.NewGuid(), SpiritStoneShardId, count, ItemRarity.Rare, "spirit_stones"));
            }
            else if (level >= 3 && level <= 4)
            {
                int count = rng.Next(2, 6); // 2..5 осколков
                items.Add(new CorpseItem(Guid.NewGuid(), SpiritStoneShardId, count, ItemRarity.Rare, "spirit_stones"));
            }
            else if (level >= 5)
            {
                int count = rng.Next(1, 3); // 1..2 фрагмента
                items.Add(new CorpseItem(Guid.NewGuid(), SpiritStoneFragmentId, count, ItemRarity.Epic, "spirit_stones"));
            }

            if (skippedUnknown > 0)
                Console.WriteLine($"[CorpseService] ⚠ {npcId}: {skippedUnknown} фантомных предметов пропущено");

            return items;
        }

        private ItemRarity ResolveRarity(string itemId)
        {
            if (_itemDatabase != null && _itemDatabase.TryGetItem(itemId, out var itemData))
                return itemData.Rarity;
            return ItemRarity.Common;
        }

        // === Внутренние ===

        private void RemoveInternal(CorpseData corpse, string reason)
        {
            _corpses.Remove(corpse);
            _corpseRemovedPub?.Publish(new CorpseRemovedEvent(corpse.CorpseId, reason));
            Console.WriteLine($"[CorpseService] Труп '{corpse.CorpseId}' убран ({reason})");
        }
    }
}

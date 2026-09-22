#nullable enable
// Создано: 2026-05-18 17:58:25 UTC
// Сервис базы данных предметов. Загружает предустановленные ScriptableObject-ы
// из Resources/Items и позволяет регистрировать runtime-сгенерированные предметы.
//
// R17 (аудит-0911 G-2/G-3): каталог НЕ переживал cold-load — сеятели
// (QiStoneSeeder/материалы/стрелы/экипировка стартового набора, генерация
// NPC-экипировки) живут в фазах с SkipOnLoad=true → в новом процессе после
// LoadGame ВСЕ предметы кроме 4 ClassicLoot — фантомы (TryGetItem=null,
// вес fallback 0.5, использование/экипировка невозможны). Теперь: блок
// "item_db" — типодискриминированный снапшот всех зарегистрированных
// предметов + счётчики ID генераторов (восстанавливается ПЕРВЫМ после
// мира/времени — контракт RestoreOrder в SaveDataAggregator).
using System;
using System.Collections.Generic;
using System.Text.Json;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Modules.Save;

namespace CultivationGame.Modules.Generator
{
    /// <summary>
    /// Реализация IItemDatabaseService.
    /// Хранит словарь предметов по ID и индекс по категориям.
    /// Предустановленные предметы загружаются из Resources/Items при Initialize().
    /// Runtime-сгенерированные предметы регистрируются через Register().
    ///
    /// R35 (Фаза 12 / P2-31/P2-32, аудит 09.22 12:00):
    /// • Register/RestoreState удаляют заменяемую запись из category-index
    ///   по СТАРОЙ категории (прежде — по новой: замена Consumable→Material
    ///   оставляла stale-запись в Consumable);
    /// • RestoreState очищает каталог ПЕРЕД восстановлением (прежде — merge:
    ///   runtime-предметы прошлого мира переживали Load);
    /// • IWorldResettable: ResetWorld = полная очистка + сброс счётчиков
    ///   генераторов + ре-сид канонического контента (ClassicLoot — как у
    ///   свежего процесса; GeneratorModule.Start сеет его один раз при буте).
    /// </summary>
    public class ItemDatabaseService : IItemDatabaseService, ISaveable, IWorldResettable
    {
        // === Основной словарь: itemId → ItemData ===
        private readonly Dictionary<string, ItemData> _itemsById = new Dictionary<string, ItemData>();

        // === Индекс по категориям: ItemCategory → список ItemData ===
        private readonly Dictionary<ItemCategory, List<ItemData>> _itemsByCategory =
            new Dictionary<ItemCategory, List<ItemData>>();

        // === Кэш-список всех предметов (обновляется при мутации) ===
        private List<ItemData> _allItemsCache = new List<ItemData>();

        // === Флаг: кэш требует перестроения ===
        private bool _cacheDirty = true;

        /// <inheritdoc/>
        public int Count => _itemsById.Count;

        /// <summary>
        /// Загрузить предустановленные предметы from the items catalogue.
        /// Ai-game4: Resources.LoadAll is Unity-only — replaced with a no-op
        /// stub. Catalogue will be populated via Register() by the GeneratorModule.
        /// </summary>
        public void Initialize()
        {
            // Ai-game4: no Resources API. Pre-built items are registered via Register().
            Console.WriteLine("[ItemDatabase] Initialized (no Resources catalogue — populate via Register())");
        }

        /// <inheritdoc/>
        public bool TryGetItem(string itemId, out ItemData item)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                item = null;
                return false;
            }

            return _itemsById.TryGetValue(itemId, out item);
        }

        /// <inheritdoc/>
        public void Register(ItemData item)
        {
            if (item == null)
            {
                Console.WriteLine("[ItemDatabase] Попытка зарегистрировать null-предмет");
                return;
            }

            if (string.IsNullOrEmpty(item.ItemId))
            {
                Console.WriteLine("[ItemDatabase] Попытка зарегистрировать предмет с пустым itemId");
                return;
            }

            if (_itemsById.TryGetValue(item.ItemId, out var oldItem))
            {
                Console.WriteLine($"[ItemDatabase] Предмет с itemId={item.ItemId} уже зарегистрирован — замена");
                // R35 (P2-31): удалить старую запись из индекса категорий по
                // СТАРОЙ категории (прежде использовалась категория нового
                // объекта → stale-запись оставалась в старом списке).
                RemoveFromCategoryIndex(oldItem.ItemId, oldItem.Category);
            }

            RegisterInternal(item);
            Console.WriteLine($"[ItemDatabase] Зарегистрирован предмет: {item.ItemId} ({item.Category})");
        }

        /// <inheritdoc/>
        public void RegisterRange(IEnumerable<ItemData> items)
        {
            if (items == null)
            {
                Console.WriteLine("[ItemDatabase] RegisterRange: передана null-коллекция");
                return;
            }

            foreach (var item in items)
            {
                Register(item);
            }
        }

        /// <inheritdoc/>
        public IReadOnlyList<ItemData> GetAllItems()
        {
            RebuildCacheIfNeeded();
            return _allItemsCache;
        }

        /// <inheritdoc/>
        public IReadOnlyList<ItemData> GetItemsByCategory(ItemCategory category)
        {
            if (_itemsByCategory.TryGetValue(category, out var list))
            {
                // Возвращаем копию, чтобы внешняя мутация не сломала индекс
                return new List<ItemData>(list);
            }

            return Array.Empty<ItemData>();
        }

        // === Внутренние методы ===

        /// <summary>
        /// Внутренняя регистрация без проверок дублирования (уже выполнены).
        /// </summary>
        private void RegisterInternal(ItemData item)
        {
            _itemsById[item.ItemId] = item;

            // Добавляем в индекс категорий
            if (!_itemsByCategory.ContainsKey(item.Category))
            {
                _itemsByCategory[item.Category] = new List<ItemData>();
            }

            _itemsByCategory[item.Category].Add(item);

            // Помечаем кэш как грязный
            _cacheDirty = true;
        }

        /// <summary>
        /// Удалить предмет из индекса категорий по itemId.
        /// Используется при замене существующего предмета.
        /// </summary>
        private void RemoveFromCategoryIndex(string itemId, ItemCategory category)
        {
            if (_itemsByCategory.TryGetValue(category, out var list))
            {
                // Ищем по itemId, так как ссылка может быть другой
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i] != null && list[i].ItemId == itemId)
                    {
                        list.RemoveAt(i);
                        break;
                    }
                }

                // Удаляем пустые списки категорий
                if (list.Count == 0)
                {
                    _itemsByCategory.Remove(category);
                }
            }
        }

        /// <summary>
        /// Перестроить кэш-список всех предметов, если он устарел.
        /// </summary>
        private void RebuildCacheIfNeeded()
        {
            if (!_cacheDirty) return;

            _allItemsCache = new List<ItemData>(_itemsById.Values);
            _cacheDirty = false;
        }

        // ════════════════════════════════════════════════════════════
        // R17 (G-2/G-3): ISaveable — блок "item_db"
        // ════════════════════════════════════════════════════════════

        /// <summary>Типизированный state-блок для round-trip десериализации.</summary>
        public sealed class ItemDbSaveState
        {
            /// <summary>Предметы каталога (type-дискриминированные записи).</summary>
            public List<ItemDbEntry> Items = new();

            /// <summary>R17 (G-3): счётчик ID EquipmentGenerator (static).</summary>
            public int EquipmentIdCounter;

            /// <summary>R17 (G-3): счётчик генерации ItemGeneratorService (static).</summary>
            public long ItemGenerationCounter;
        }

        /// <summary>
        /// Одна запись каталога: тип-дискриминатор (ключ карты KnownTypes)
        /// + JSON предмета конкретного типа. Полиморфная сериализация без
        /// атрибутов на Core-моделях (ItemData/EquipmentData/QiStoneData —
        /// engine-agnostic, System.Text.Json-атрибуты там не разводим).
        /// </summary>
        public sealed class ItemDbEntry
        {
            public string TypeKey = "";
            public string Json = "";
        }

        /// <summary>Карта известных типов: дискриминатор → конкретный тип ItemData.</summary>
        private static readonly Dictionary<string, Type> KnownTypes = new()
        {
            ["item"] = typeof(ItemData),
            ["equipment"] = typeof(EquipmentData),
            ["qi_stone"] = typeof(QiStoneData),
        };

        public string SaveKey => "item_db";
        public Type StateType => typeof(ItemDbSaveState);

        public object CaptureState()
        {
            var data = new ItemDbSaveState
            {
                EquipmentIdCounter = EquipmentGenerator.GetIdCounter(),
                ItemGenerationCounter = ItemGeneratorService.GetGenerationCounter(),
            };

            foreach (var item in _itemsById.Values)
            {
                if (item == null || string.IsNullOrEmpty(item.ItemId)) continue;

                string typeKey = "item";
                if (item is EquipmentData) typeKey = "equipment";
                else if (item is QiStoneData) typeKey = "qi_stone";

                data.Items.Add(new ItemDbEntry
                {
                    TypeKey = typeKey,
                    Json = JsonSerializer.Serialize(item, item.GetType(), SaveJson.Options),
                });
            }
            return data;
        }

        public void RestoreState(object state)
        {
            if (state is not ItemDbSaveState data || data == null) return;

            // R35 (P2-32): каталог ЗАМЕНЯЕТСЯ, не мержится. Прежде RestoreState
            // добавлял/заменял поверх живого каталога: runtime-предметы прошлого
            // мира (тёплая LoadGame/NewGame) переживали загрузку и вместе с
            // восстановленными счётчиками создавали риск коллизий новых
            // генераций со старыми ID (связка P1-16). Холодный Load: каталог и
            // так пуст; тёплый: GameSession уже сбросил домены (ResetWorld ниже)
            // — чистим и для прямых вызовов без фазы сброса.
            _itemsById.Clear();
            _itemsByCategory.Clear();
            _cacheDirty = true;

            int restored = 0;
            int skipped = 0;
            if (data.Items != null)
            {
                foreach (var entry in data.Items)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.Json)) continue;
                    if (!KnownTypes.TryGetValue(entry.TypeKey ?? "", out var itemType))
                    {
                        skipped++;
                        continue;
                    }

                    try
                    {
                        if (JsonSerializer.Deserialize(entry.Json, itemType, SaveJson.Options) is ItemData item
                            && !string.IsNullOrEmpty(item.ItemId))
                        {
                            // Тихая регистрация: сотни строк лога на каталог не нужны.
                            // R35 (P2-31): заменяемая запись удаляется по СТАРОЙ
                            // категории (каталог только что очищен — ветка защитная).
                            if (_itemsById.TryGetValue(item.ItemId, out var oldItem))
                                RemoveFromCategoryIndex(oldItem.ItemId, oldItem.Category);
                            RegisterInternal(item);
                            restored++;
                        }
                        else skipped++;
                    }
                    catch (JsonException)
                    {
                        skipped++;
                    }
                }
            }

            // R17 (G-3): счётчики ID — новые предметы не коллидируют с
            // восстановленными из сейва.
            EquipmentGenerator.SetIdCounter(data.EquipmentIdCounter);
            ItemGeneratorService.SetGenerationCounter(data.ItemGenerationCounter);

            Console.WriteLine($"[ItemDatabase] RestoreState: {restored} предметов восстановлено" +
                              (skipped > 0 ? $", {skipped} пропущено" : "") +
                              $"; счётчики ID: eq={data.EquipmentIdCounter}, gen={data.ItemGenerationCounter}");
        }

        // ── R35 (Фаза 12 / P2-32): IWorldResettable ─────────────────────
        /// <summary>
        /// Пересборка мира (NewGame ИЛИ LoadGame — сброс выполняет
        /// WorldDomainResetPhase/GameSession ДО RestoreState):
        /// • каталог полностью очищается (runtime-предметы прошлого мира
        ///   не переживают границу миров);
        /// • счётчики генераторов — в 0 (новый мир начинает нумерацию заново;
        ///   коллизий со старыми ID нет — каталог пуст);
        /// • канонический контент (ClassicLoot: материалы/камни Ци) — ре-сеится:
        ///   GeneratorModule.Start() сеет его один раз при буте, фаза сброса
        ///   возвращает каталог в эквивалент «свежего процесса».
        /// LoadGame-путь: после сброса блок item_db RestoreState накладывает
        /// сохранённый каталог поверх чистого.
        /// </summary>
        public void ResetWorld()
        {
            _itemsById.Clear();
            _itemsByCategory.Clear();
            _cacheDirty = true;
            EquipmentGenerator.SetIdCounter(0);
            ItemGeneratorService.SetGenerationCounter(0);
            // P2-32: гвард сидера — process-scoped; сбрасываем, чтобы канон
            // вернулся в очищенный каталог (boot сеет его один раз).
            ClassicLootSeeder.ResetForNewWorld();
            ClassicLootSeeder.Seed(this);
            Console.WriteLine($"[ItemDatabase] ResetWorld: каталог очищен, счётчики ID обнулены, " +
                              $"канон ClassicLoot ре-сеен ({Count} предметов)");
        }
    }
}

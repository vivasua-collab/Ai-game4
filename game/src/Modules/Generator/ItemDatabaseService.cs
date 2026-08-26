#nullable enable
// Создано: 2026-05-18 17:58:25 UTC
// Сервис базы данных предметов. Загружает предустановленные ScriptableObject-ы
// из Resources/Items и позволяет регистрировать runtime-сгенерированные предметы.
using System;
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Generator
{
    /// <summary>
    /// Реализация IItemDatabaseService.
    /// Хранит словарь предметов по ID и индекс по категориям.
    /// Предустановленные предметы загружаются из Resources/Items при Initialize().
    /// Runtime-сгенерированные предметы регистрируются через Register().
    /// </summary>
    public class ItemDatabaseService : IItemDatabaseService
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

            if (_itemsById.ContainsKey(item.ItemId))
            {
                Console.WriteLine($"[ItemDatabase] Предмет с itemId={item.ItemId} уже зарегистрирован — замена");
                // Удаляем старую запись из индекса категорий
                RemoveFromCategoryIndex(item.ItemId, item.Category);
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
    }
}

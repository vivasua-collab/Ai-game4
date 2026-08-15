#nullable enable
// Создано: 2026-05-19 09:22:25 UTC
// Редактировано: 2026-05-19 09:22:25 UTC — этап 1.7: добавлен HardnessTier в ObjectInfo
// Единая таблица характеристик объектов на тайлах.
// Устраняет дублирование HP/ResourceMax/ResourceId в TileGeneratorService,
// ResourceService.ResourceConfigs, TileMapService.GetDestructibleHP.
// Источник: 05_19_audit_location_v1.md — GAP-RES-01, 05_19_plan_location_phase1.md — этап 1.1

using System.Collections.Generic;

namespace CultivationGame.Core.Data
{
    /// <summary>
    /// Параметры объекта по умолчанию.
    /// Единый источник истины — устраняет дублирование в
    /// TileGeneratorService, ResourceService, TileMapService.
    /// Источник: TILE_SYSTEM.md §2, LOCATION_MAP_SYSTEM.md §3
    /// </summary>
    public readonly struct ObjectInfo
    {
        public readonly ObjectType Type;
        public readonly ObjectCategory Category;
        public readonly string ResourceId;       // Идентификатор ресурса (пустой = нет ресурса)
        public readonly string ItemId;           // Идентификатор предмета при сборе
        public readonly float ResourceMax;       // Максимальное количество ресурса
        public readonly int HarvestAmount;       // Количество за один сбор
        public readonly float DestructibleHP;    // HP разрушаемого объекта (0 = не разрушаемое)
        public readonly int RespawnDays;         // Дней до респауна (0 = не респаунится)
        public readonly bool IsPassable;         // Проходимый объект (кусты, tall_grass)
        public readonly float MoveCostModifier;  // Модификатор стоимости движения (1.0 = нет влияния)
        public readonly HardnessTier HardnessTier; // Тир прочности (для проверки инструмента)

        public ObjectInfo(
            ObjectType type,
            ObjectCategory category,
            string resourceId,
            string itemId,
            float resourceMax,
            int harvestAmount,
            float destructibleHP,
            int respawnDays,
            bool isPassable = false,
            float moveCostModifier = 1.0f,
            HardnessTier hardnessTier = HardnessTier.None)
        {
            Type = type;
            Category = category;
            ResourceId = resourceId;
            ItemId = itemId;
            ResourceMax = resourceMax;
            HarvestAmount = harvestAmount;
            DestructibleHP = destructibleHP;
            RespawnDays = respawnDays;
            IsPassable = isPassable;
            MoveCostModifier = moveCostModifier;
            HardnessTier = hardnessTier;
        }
    }

    /// <summary>
    /// Статическая таблица характеристик объектов.
    /// Единый источник истины для всех сервисов тайловой системы.
    ///
    /// HP-значения из ResourceService.ResourceConfigs (корректные):
    ///   Tree_Oak=100, Tree_Pine=120, Tree_Birch=80
    /// НЕ из TileGeneratorService (устаревшие: 80/100/60).
    ///
    /// IsPassable: Bush/Bush_Berry проходимы с замедлением ×1.5
    ///   (TILE_SYSTEM.md §2 — кусты = Passable, moveCost ×1.5).
    ///
    /// MoveCostModifier: 1.0 = нет влияния, 1.5 = замедление 50%, 0.7 = бонус 30%
    ///
    /// HardnessTier: определяет минимальный тир инструмента для разрушения.
    ///   Organic=1 (дерево), Stone=2 (камень), Metal=3 (руда), Spiritual=4, Void=5
    ///   Источник: LOCATION_MAP_SYSTEM.md §3, EQUIPMENT_SYSTEM.md §3
    /// </summary>
    public static class ObjectDefaults
    {
        private static readonly Dictionary<ObjectType, ObjectInfo> Entries = new()
        {
            // === Растительность (100-199) ===
            { ObjectType.Tree_Oak, new(
                ObjectType.Tree_Oak, ObjectCategory.Vegetation,
                "wood_oak", "material_wood", 50f, 5, 100f, 7,
                isPassable: false, moveCostModifier: 1.0f,
                hardnessTier: HardnessTier.Organic) },

            { ObjectType.Tree_Pine, new(
                ObjectType.Tree_Pine, ObjectCategory.Vegetation,
                "wood_pine", "material_wood", 60f, 5, 120f, 7,
                isPassable: false, moveCostModifier: 1.0f,
                hardnessTier: HardnessTier.Organic) },

            { ObjectType.Tree_Birch, new(
                ObjectType.Tree_Birch, ObjectCategory.Vegetation,
                "wood_birch", "material_wood", 40f, 4, 80f, 5,
                isPassable: false, moveCostModifier: 1.0f,
                hardnessTier: HardnessTier.Organic) },

            { ObjectType.Bush, new(
                ObjectType.Bush, ObjectCategory.Vegetation,
                "", "", 0f, 0, 0f, 0,
                isPassable: true, moveCostModifier: 1.5f,
                hardnessTier: HardnessTier.None) },

            { ObjectType.Bush_Berry, new(
                ObjectType.Bush_Berry, ObjectCategory.Vegetation,
                "berry", "consumable_berry", 10f, 3, 0f, 3,
                isPassable: true, moveCostModifier: 1.5f,
                hardnessTier: HardnessTier.None) },

            // === Камни (200-299) ===
            { ObjectType.Rock_Small, new(
                ObjectType.Rock_Small, ObjectCategory.Rock,
                "stone_small", "material_stone", 20f, 3, 50f, 14,
                isPassable: false, moveCostModifier: 1.0f,
                hardnessTier: HardnessTier.Stone) },

            { ObjectType.Rock_Medium, new(
                ObjectType.Rock_Medium, ObjectCategory.Rock,
                "stone_medium", "material_stone", 40f, 5, 100f, 14,
                isPassable: false, moveCostModifier: 1.0f,
                hardnessTier: HardnessTier.Stone) },

            { ObjectType.Rock_Large, new(
                ObjectType.Rock_Large, ObjectCategory.Rock,
                "", "", 0f, 0, 200f, 0,
                isPassable: false, moveCostModifier: 1.0f,
                hardnessTier: HardnessTier.Stone) },

            // === Интерактивные (500-599) ===
            { ObjectType.Chest, new(
                ObjectType.Chest, ObjectCategory.Interactive,
                "", "", 0f, 0, 0f, 0,
                isPassable: false, moveCostModifier: 1.0f,
                hardnessTier: HardnessTier.None) },

            { ObjectType.OreVein, new(
                ObjectType.OreVein, ObjectCategory.Interactive,
                "ore_iron", "material_iron_ore", 30f, 3, 150f, 30,
                isPassable: false, moveCostModifier: 1.0f,
                hardnessTier: HardnessTier.Metal) },

            { ObjectType.Herb, new(
                ObjectType.Herb, ObjectCategory.Interactive,
                "herb_medicinal", "consumable_herb", 5f, 1, 0f, 2,
                isPassable: false, moveCostModifier: 1.0f,
                hardnessTier: HardnessTier.None) },
        };

        /// <summary>
        /// Получить полную информацию об объекте по типу.
        /// Возвращает default(ObjectInfo), если тип не найден.
        /// </summary>
        public static bool TryGet(ObjectType type, out ObjectInfo info)
        {
            return Entries.TryGetValue(type, out info);
        }

        /// <summary>
        /// Получить информацию об объекте. Если не найден — возвращает default.
        /// </summary>
        public static ObjectInfo Get(ObjectType type)
        {
            return TryGet(type, out var info) ? info : default;
        }

        /// <summary>
        /// HP разрушаемого объекта по типу.
        /// </summary>
        public static float GetHP(ObjectType type)
        {
            var info = Get(type);
            return info.DestructibleHP;
        }

        /// <summary>
        /// Максимальное количество ресурса по типу объекта.
        /// </summary>
        public static float GetResourceMax(ObjectType type)
        {
            var info = Get(type);
            return info.ResourceMax;
        }

        /// <summary>
        /// Идентификатор ресурса по типу объекта.
        /// </summary>
        public static string GetResourceId(ObjectType type)
        {
            var info = Get(type);
            return info.ResourceId;
        }

        /// <summary>
        /// Является ли объект проходимым (кусты и т.п.).
        /// Источник: TILE_SYSTEM.md §2 — Passable объекты.
        /// </summary>
        public static bool IsPassable(ObjectType type)
        {
            var info = Get(type);
            return info.IsPassable;
        }

        /// <summary>
        /// Модификатор стоимости движения от объекта.
        /// 1.0 = нет влияния, 1.5 = замедление, 0.7 = бонус.
        /// Источник: TILE_SYSTEM.md §2 — bush ×1.5, road ×0.7.
        /// </summary>
        public static float GetMoveCostModifier(ObjectType type)
        {
            var info = Get(type);
            return info.MoveCostModifier;
        }

        /// <summary>
        /// Количество ресурса за один сбор по типу объекта.
        /// </summary>
        public static int GetHarvestAmount(ObjectType type)
        {
            var info = Get(type);
            return info.HarvestAmount;
        }

        /// <summary>
        /// Идентификатор предмета при сборе по типу объекта.
        /// </summary>
        public static string GetItemId(ObjectType type)
        {
            var info = Get(type);
            return info.ItemId;
        }

        /// <summary>
        /// Дней до респауна по типу объекта. 0 = не респаунится.
        /// </summary>
        public static int GetRespawnDays(ObjectType type)
        {
            var info = Get(type);
            return info.RespawnDays;
        }

        /// <summary>
        /// Тир прочности объекта.
        /// Определяет минимальный тир инструмента для разрушения.
        /// Organic=1 (дерево), Stone=2 (камень), Metal=3 (руда).
        /// </summary>
        public static HardnessTier GetHardnessTier(ObjectType type)
        {
            var info = Get(type);
            return info.HardnessTier;
        }
    }
}

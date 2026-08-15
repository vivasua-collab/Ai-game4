#nullable enable
// Создано: 2026-05-08 19:38:52 UTC
// Редактировано: 2026-05-19 09:22:25 UTC — этап 1.4-1.8: Road, IsWalkable, EffectiveMoveCost, HardnessTier
// Единая структура данных тайла — единственный источник истины.
// В Core для использования в ITileService (Core → Modules недопустимо).
// Устраняет тройное дублирование Legacy (TileData + GameTile + TerrainTile/ObjectTile).
// Источник: plan_02_tile.md — Новая модель данных

using System;

namespace CultivationGame.Core.Data
{
    /// <summary>
    /// Единая структура данных тайла.
    /// Заменяет: TileData (ScriptableObject), GameTile (runtime), TerrainTile/ObjectTile (Unity Tilemap).
    /// Все данные хранятся в этой структуре — единственный источник истины.
    /// </summary>
    [Serializable]
    public struct GameTile
    {
        // === Координаты ===
        public int X;
        public int Y;

        // === Поверхность ===
        public TerrainType Terrain;  // Stratum 1: surface (moveCost, walkability)
        public BiomeType Biome;      // Stratum 0: biome (color, Qi only)
        public float MoveCost;
        public TileFlags Flags;

        // === Объект ===
        public ObjectType Object;
        public ObjectCategory ObjectCategory;

        // === Ресурсы ===
        public bool IsHarvestable;
        public float ResourceAmount;     // Текущее количество ресурса
        public float ResourceMax;        // Максимальное количество ресурса
        public string ResourceId;        // Идентификатор ресурса

        // === Разрушаемость ===
        public bool IsDestructible;
        public float DestructibleHP;     // Текущее HP разрушаемого объекта
        public float DestructibleMaxHP;  // Максимальное HP разрушаемого объекта
        public HardnessTier HardnessTier; // Тир прочности (для проверки инструмента при разрушении)

        // === Проходимость (вычисляемое свойство) ===

        /// <summary>
        /// Можно ли пройти по тайлу.
        /// Зависит от Terrain (Void, Water_Deep, Lava = непроходимо)
        /// и Object (непроходимые объекты блокируют, проходимые — нет).
        /// Проходимые объекты: Bush, Bush_Berry (определяется через ObjectDefaults.IsPassable).
        /// Источник: TILE_SYSTEM.md §2 — Impassable/Passable категории.
        /// </summary>
        public bool IsWalkable => (Flags & TileFlags.Passable) != 0
                                  && Terrain != TerrainType.Void
                                  && Terrain != TerrainType.Water_Deep
                                  && Terrain != TerrainType.Lava
                                  && (Object == ObjectType.None || ObjectDefaults.IsPassable(Object));

        /// <summary>
        /// Итоговая стоимость движения с учётом поверхности и объекта.
        /// Terrain.MoveCost × Object.MoveCostModifier.
        /// Bush: ×1.5 (замедление 50%), Road: ×0.7 (бонус 30%).
        /// Возвращает 0 для непроходимых тайлов.
        /// Источник: TILE_SYSTEM.md §2 — Множители стоимости движения.
        /// </summary>
        public float EffectiveMoveCost
        {
            get
            {
                float terrainCost = MoveCost;
                if (terrainCost <= 0f) return 0f;  // Непроходимо

                float objectModifier = Object != ObjectType.None
                    ? ObjectDefaults.GetMoveCostModifier(Object)
                    : 1.0f;
                return terrainCost * objectModifier;
            }
        }

        // === Фабричные методы ===

        /// <summary>
        /// Создать пустой тайл с поверхностью.
        /// </summary>
        public static GameTile CreateTerrain(int x, int y, TerrainType terrain)
        {
            var tile = new GameTile
            {
                X = x,
                Y = y,
                Terrain = terrain,
                Biome = BiomeType.Grassland,  // default, will be set by Generate
                MoveCost = GetTerrainMoveCost(terrain),
                Flags = GetTerrainFlags(terrain),
                Object = ObjectType.None,
                ObjectCategory = ObjectCategory.None,
                IsHarvestable = false,
                ResourceAmount = 0f,
                ResourceMax = 0f,
                ResourceId = string.Empty,
                IsDestructible = false,
                DestructibleHP = 0f,
                DestructibleMaxHP = 0f,
                HardnessTier = HardnessTier.None
            };
            return tile;
        }

        /// <summary>
        /// Создать тайл с объектом (дерево, камень, руда и т.д.).
        /// </summary>
        public static GameTile CreateWithObject(int x, int y, TerrainType terrain, ObjectType obj,
            float resourceMax = 0f, string resourceId = "", float hp = 0f)
        {
            var tile = CreateTerrain(x, y, terrain);
            tile.Object = obj;
            tile.ObjectCategory = GetObjectCategory(obj);

            // Объект делает тайл непроходимым (кроме проходимых объектов)
            // Определяется через ObjectDefaults.IsPassable
            if (obj != ObjectType.None && !ObjectDefaults.IsPassable(obj))
            {
                tile.Flags &= ~TileFlags.Passable;
            }

            // Ресурс
            if (resourceMax > 0)
            {
                tile.IsHarvestable = true;
                tile.ResourceAmount = resourceMax;
                tile.ResourceMax = resourceMax;
                tile.ResourceId = resourceId;
                tile.Flags |= TileFlags.Harvestable;
            }

            // Разрушаемость
            if (hp > 0)
            {
                tile.IsDestructible = true;
                tile.DestructibleHP = hp;
                tile.DestructibleMaxHP = hp;
                tile.HardnessTier = ObjectDefaults.GetHardnessTier(obj);
            }

            return tile;
        }

        // === Статические таблицы ===

        /// <summary>
        /// Стоимость движения по типу местности.
        /// Источник: TILE_SYSTEM.md §2 — Множители стоимости движения
        /// </summary>
        public static float GetTerrainMoveCost(TerrainType terrain)
        {
            return terrain switch
            {
                TerrainType.Grass => 1.0f,
                TerrainType.Dirt => 1.0f,
                TerrainType.Stone => 1.0f,
                TerrainType.Water_Shallow => 2.0f,
                TerrainType.Water_Deep => 0f,    // Непроходимо
                TerrainType.Sand => 1.2f,
                TerrainType.Snow => 1.5f,
                TerrainType.Ice => 1.5f,          // + скольжение (особая механика)
                TerrainType.Lava => 0f,           // Непроходимо
                TerrainType.Void => 0f,           // Непроходимо
                TerrainType.Road => 0.7f,          // Бонус скорости (TILE_SYSTEM.md §2)
                _ => 1.0f
            };
        }

        /// <summary>
        /// Флаги по типу местности.
        /// </summary>
        public static TileFlags GetTerrainFlags(TerrainType terrain)
        {
            return terrain switch
            {
                TerrainType.Grass => TileFlags.Passable,
                TerrainType.Dirt => TileFlags.Passable,
                TerrainType.Stone => TileFlags.Passable,
                TerrainType.Water_Shallow => TileFlags.Passable | TileFlags.Swimable,
                TerrainType.Water_Deep => TileFlags.Swimable,
                TerrainType.Sand => TileFlags.Passable,
                TerrainType.Snow => TileFlags.Passable,
                TerrainType.Ice => TileFlags.Passable,
                TerrainType.Lava => TileFlags.Dangerous,
                TerrainType.Void => TileFlags.None,
                TerrainType.Road => TileFlags.Passable,
                _ => TileFlags.Passable
            };
        }

        /// <summary>
        /// Категория объекта по типу.
        /// </summary>
        public static ObjectCategory GetObjectCategory(ObjectType obj)
        {
            int val = (int)obj;
            if (val >= 100 && val < 200) return ObjectCategory.Vegetation;
            if (val >= 200 && val < 300) return ObjectCategory.Rock;
            if (val >= 500 && val < 600) return ObjectCategory.Interactive;
            if (val >= 400 && val < 500) return ObjectCategory.Building;
            return ObjectCategory.None;
        }
    }
}

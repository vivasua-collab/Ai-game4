#nullable enable
// Создано: 2026-05-09 00:00:00 UTC
// Сервис материалов — работа с материалами для крафта и экипировки.
// Заменяет legacy MaterialSystem.cs (548 LOC) — упрощённая модель.
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Inventory
{
    /// <summary>
    /// Сервис материалов.
    /// Управляет базой материалов, их свойствами и тиром.
    ///
    /// В текущей фазе — справочник материалов с базовыми параметрами.
    /// В будущих фазах — загрузка из ScriptableObject / JSON.
    /// </summary>
    public class MaterialService
    {
        // === Состояние ===
        private readonly Dictionary<string, MaterialInfo> _materials = new();

        /// <summary>
        /// Зарегистрировать материал.
        /// </summary>
        public void RegisterMaterial(string materialId, MaterialCategory category, int tier,
            float hardness, float weightMultiplier, float valueMultiplier)
        {
            _materials[materialId] = new MaterialInfo
            {
                MaterialId = materialId,
                Category = category,
                Tier = tier,
                Hardness = hardness,
                WeightMultiplier = weightMultiplier,
                ValueMultiplier = valueMultiplier
            };
        }

        /// <summary>
        /// Получить информацию о материале по ID.
        /// </summary>
        public MaterialInfo GetMaterial(string materialId)
        {
            if (string.IsNullOrEmpty(materialId)) return null;
            return _materials.TryGetValue(materialId, out var info) ? info : null;
        }

        /// <summary>
        /// Получить все материалы указанного тира.
        /// </summary>
        public List<MaterialInfo> GetMaterialsByTier(int tier)
        {
            var result = new List<MaterialInfo>();
            foreach (var kvp in _materials)
            {
                if (kvp.Value.Tier == tier) result.Add(kvp.Value);
            }
            return result;
        }

        /// <summary>
        /// Получить все материалы указанной категории.
        /// </summary>
        public List<MaterialInfo> GetMaterialsByCategory(MaterialCategory category)
        {
            var result = new List<MaterialInfo>();
            foreach (var kvp in _materials)
            {
                if (kvp.Value.Category == category) result.Add(kvp.Value);
            }
            return result;
        }

        /// <summary>
        /// Инициализация материалов по умолчанию.
        /// Тир 1-5, базовые материалы.
        /// </summary>
        public void InitializeDefaults()
        {
            // Тир 1: Обычные
            RegisterMaterial("iron", MaterialCategory.Metal, 1, 4f, 1.0f, 1.0f);
            RegisterMaterial("leather", MaterialCategory.Leather, 1, 2f, 0.5f, 0.8f);
            RegisterMaterial("cloth", MaterialCategory.Cloth, 1, 1f, 0.2f, 0.5f);
            RegisterMaterial("wood", MaterialCategory.Wood, 1, 2f, 0.4f, 0.3f);

            // Тир 2: Качественные
            RegisterMaterial("steel", MaterialCategory.Metal, 2, 6f, 1.2f, 2.0f);
            RegisterMaterial("silk", MaterialCategory.Cloth, 2, 1.5f, 0.1f, 1.5f);

            // Тир 3: Духовные
            RegisterMaterial("spirit_iron", MaterialCategory.Metal, 3, 8f, 0.8f, 5.0f);
            RegisterMaterial("jade", MaterialCategory.Crystal, 3, 7f, 1.5f, 4.0f);

            // Тир 4: Небесные
            RegisterMaterial("star_metal", MaterialCategory.Metal, 4, 10f, 1.0f, 15.0f);
            RegisterMaterial("dragon_bone", MaterialCategory.Bone, 4, 12f, 0.6f, 20.0f);

            // Тир 5: Первородные
            RegisterMaterial("void_matter", MaterialCategory.Void, 5, 15f, 0.1f, 100.0f);
        }
    }

    /// <summary>
    /// Информация о материале.
    /// </summary>
    public class MaterialInfo
    {
        public string MaterialId;
        public MaterialCategory Category;
        public int Tier;
        public float Hardness;
        public float WeightMultiplier;
        public float ValueMultiplier;
    }
}

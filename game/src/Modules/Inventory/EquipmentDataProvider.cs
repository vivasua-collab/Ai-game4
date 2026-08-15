#nullable enable
// Создано: 2026-05-20 18:43:21 UTC
// Редактировано: 2026-05-22 09:51:00 UTC — Спринт 6 C5: +GetArmorCoverage()
// Редактировано: 2026-05-22 13:08:27 UTC — P2-6.2 FIX: ArmorCoverage default 100→0; P2-6.3 FIX: HasEntity проверяет кэши
// Реализация IEquipmentDataProvider — хранилище данных экипировки per-entity.
// Отдельный класс от EquipmentService (EquipmentService обслуживает игрока, EquipmentDataProvider — NPC).
// Конструктор без зависимостей (чистое хранилище данных).
// Регистрируется в VContainer как Singleton: IEquipmentDataProvider → EquipmentDataProvider.
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Inventory
{
    /// <summary>
    /// Реализация IEquipmentDataProvider — хранилище данных экипировки per-entity.
    /// Отдельный класс от EquipmentService (EquipmentService обслуживает игрока, EquipmentDataProvider — NPC).
    /// Конструктор без зависимостей (чистое хранилище данных).
    ///
    /// Временное ограничение: EquipmentData (ScriptableObject) не может быть создан
    /// из строки ID без IItemDatabaseService. Поэтому:
    /// - SetEquipment хранит только строковые ID экипировки
    /// - GetEquipped возвращает null (пока нет IItemDatabaseService)
    /// - GetTotalArmor/GetTotalDamage используют предрассчитанные кэши,
    ///   которые устанавливаются через SetTotalArmor/SetTotalDamage из NPCAssemblyService
    ///
    /// TODO: После внедрения IItemDatabaseService — резолвить ID → EquipmentData
    /// </summary>
    public class EquipmentDataProvider : IEquipmentDataProvider
    {
        // === Хранилище per-entity ===

        /// <summary>
        /// Маппинг экипировки: entityId → (слот → ID предмета).
        /// Хранит строковые ID, не EquipmentData (нужен IItemDatabaseService для резолва).
        /// </summary>
        private readonly Dictionary<string, Dictionary<EquipmentSlot, string>> _entityEquipmentIds = new();

        /// <summary>
        /// Кэш суммарной брони: entityId → totalArmor.
        /// Устанавливается из NPCAssemblyService.SetTotalArmor().
        /// </summary>
        private readonly Dictionary<string, float> _cachedTotalArmor = new();

        /// <summary>
        /// Кэш суммарного урона: entityId → totalDamage.
        /// Устанавливается из NPCAssemblyService.SetTotalDamage().
        /// </summary>
        private readonly Dictionary<string, float> _cachedTotalDamage = new();

        // === Конструктор (без зависимостей) ===

        /// <summary>
        /// Конструктор без зависимостей — чистое хранилище данных.
        /// VContainer автоматически вызовет при регистрации.
        /// </summary>
        public EquipmentDataProvider()
        {
        }

        // === IEquipmentDataProvider ===

        /// <summary>
        /// Получить экипированный предмет в слоте.
        /// ВНИМАНИЕ: В текущей реализации возвращает null, так как
        /// EquipmentData (ScriptableObject) не может быть создан из строки ID.
        /// TODO: После внедрения IItemDatabaseService — резолвить ID → EquipmentData.
        /// </summary>
        public EquipmentData GetEquipped(string entityId, EquipmentSlot slot)
        {
            // Пока нет IItemDatabaseService, невозможно резолвить string ID → EquipmentData
            // Возвращаем null — подписчики должны обрабатывать null-значения
            return null;
        }

        /// <summary>
        /// Получить суммарную броню сущности.
        /// Возвращает предрассчитанное значение из кэша (0 по умолчанию).
        /// Значение устанавливается через SetTotalArmor() из NPCAssemblyService.
        /// </summary>
        public float GetTotalArmor(string entityId)
        {
            if (entityId == null) return 0f;
            return _cachedTotalArmor.TryGetValue(entityId, out float armor) ? armor : 0f;
        }

        /// <summary>
        /// Получить суммарный урон сущности.
        /// Возвращает предрассчитанное значение из кэша (0 по умолчанию).
        /// Значение устанавливается через SetTotalDamage() из NPCAssemblyService.
        /// </summary>
        public float GetTotalDamage(string entityId)
        {
            if (entityId == null) return 0f;
            return _cachedTotalDamage.TryGetValue(entityId, out float damage) ? damage : 0f;
        }

        /// <summary>
        /// Установить экипировку для сущности (при создании NPC).
        /// Хранит строковые ID экипировки по слотам.
        /// Не резолвит ID → EquipmentData (нужен IItemDatabaseService).
        /// </summary>
        public void SetEquipment(string entityId, Dictionary<EquipmentSlot, string> equipmentIds)
        {
            if (entityId == null) return;

            if (equipmentIds == null)
            {
                _entityEquipmentIds.Remove(entityId);
                return;
            }

            // Копируем словарь, чтобы внешние мутации не влияли на хранилище
            var copy = new Dictionary<EquipmentSlot, string>(equipmentIds);
            _entityEquipmentIds[entityId] = copy;
        }

        /// <summary>
        /// Проверить существование сущности в провайдере.
        /// P2-6.3 FIX: проверяет также кэши armor/damage, т.к. сущность
        /// может быть создана через SetTotalArmor/SetTotalDamage без equipment IDs.
        /// </summary>
        public bool HasEntity(string entityId)
        {
            return entityId != null &&
                (_entityEquipmentIds.ContainsKey(entityId) ||
                 _cachedTotalArmor.ContainsKey(entityId) ||
                 _cachedTotalDamage.ContainsKey(entityId) ||
                 _cachedArmorCoverage.ContainsKey(entityId));
        }

        /// <summary>
        /// Удалить сущность из провайдера (при деспавне NPC).
        /// Очищает все данные: экипировку, кэш брони и урона.
        /// </summary>
        public void RemoveEntity(string entityId)
        {
            if (entityId == null) return;

            _entityEquipmentIds.Remove(entityId);
            _cachedTotalArmor.Remove(entityId);
            _cachedTotalDamage.Remove(entityId);
            _cachedArmorCoverage.Remove(entityId);
        }

        /// <summary>
        /// Инвалидировать кэш брони для сущности.
        /// Сбрасывает кэшированное значение брони в 0.
        /// </summary>
        public void InvalidateCache(string entityId)
        {
            if (entityId == null) return;
            _cachedTotalArmor.Remove(entityId);
            _cachedTotalDamage.Remove(entityId);
            _cachedArmorCoverage.Remove(entityId);
        }

        // === Дополнительные методы для NPCAssemblyService ===

        /// <summary>
        /// Установить предрассчитанную суммарную броню для сущности.
        /// Вызывается из NPCAssemblyService после расчёта всех параметров NPC.
        /// </summary>
        public void SetTotalArmor(string entityId, float armor)
        {
            if (entityId == null) return;
            _cachedTotalArmor[entityId] = armor;
        }

        /// <summary>
        /// Установить предрассчитанный суммарный урон для сущности.
        /// Вызывается из NPCAssemblyService после расчёта всех параметров NPC.
        /// </summary>
        public void SetTotalDamage(string entityId, float damage)
        {
            if (entityId == null) return;
            _cachedTotalDamage[entityId] = damage;
        }

        /// <summary>
        /// Получить ID экипированного предмета в слоте (строковый ID).
        /// Используется, когда нужен только ID, а не полные данные EquipmentData.
        /// Возвращает null, если сущность или слот не найдены.
        /// </summary>
        public string GetEquippedItemId(string entityId, EquipmentSlot slot)
        {
            if (entityId == null) return null;
            if (!_entityEquipmentIds.TryGetValue(entityId, out var slots)) return null;
            return slots.TryGetValue(slot, out var itemId) ? itemId : null;
        }

        // === Спринт 6 C5: Coverage ===

        /// <summary>
        /// Кэш покрытия брони: entityId → coveragePercent (0-100).
        /// Устанавливается через SetArmorCoverage() из NPCAssemblyService.
        /// </summary>
        private readonly Dictionary<string, int> _cachedArmorCoverage = new();

        /// <summary>
        /// Получить средний процент покрытия брони для сущности.
        /// Спринт 6 C5: Возвращает 0-100 (процент покрытия).
        /// 0 = нет покрытия (броня никогда не покрывает), 100 = полное покрытие.
        /// Используется для coverage roll в DamageService.
        /// </summary>
        public int GetArmorCoverage(string entityId)
        {
            if (entityId == null) return 0;
            return _cachedArmorCoverage.TryGetValue(entityId, out int coverage) ? coverage : 0;
            // P2-6.2 FIX: default = 0 (нет покрытия), было 100 (полное покрытие)
            // Сущности без явного coverage НЕ получают бесплатную броню
        }

        /// <summary>
        /// Установить покрытие брони для сущности.
        /// Вызывается из NPCAssemblyService после расчёта параметров NPC.
        /// </summary>
        public void SetArmorCoverage(string entityId, int coverage)
        {
            if (entityId == null) return;
            _cachedArmorCoverage[entityId] = coverage;
        }
    }
}

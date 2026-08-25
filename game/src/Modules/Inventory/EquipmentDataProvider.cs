#nullable enable
// Создано: 2026-05-20 18:43:21 UTC
// Редактировано: 2026-05-22 09:51:00 UTC — Спринт 6 C5: +GetArmorCoverage()
// Редактировано: 2026-05-22 13:08:27 UTC — P2-6.2 FIX: ArmorCoverage default 100→0; P2-6.3 FIX: HasEntity проверяет кэши
// Редактировано: 2026-08-25 — NPC_COMBAT_PREP Phase 8: резолв ID → EquipmentData через
//   IItemDatabaseService (TODO закрыт — база предметов внедрена), прямые данные игрока
//   (SetEquipmentData), агрегаты боевых статов в промилле (dodge/block/parry/crit/penetration).
// Реализация IEquipmentDataProvider — хранилище данных экипировки per-entity.
// Отдельный класс от EquipmentService (EquipmentService обслуживает игрока, EquipmentDataProvider — все сущности).
// Регистрируется как Singleton: IEquipmentDataProvider → EquipmentDataProvider.
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Inventory
{
    /// <summary>
    /// Реализация IEquipmentDataProvider — хранилище данных экипировки per-entity.
    /// Отдельный класс от EquipmentService (EquipmentService обслуживает игрока, EquipmentDataProvider — NPC).
    ///
    /// Два источника данных (приоритет у прямого кэша):
    /// 1. Прямой кэш EquipmentData (игрок — EquipmentService пушит через SetEquipmentData).
    /// 2. Строковые ID + резолв через IItemDatabaseService (NPC — SetEquipment из NPCSpawnerService).
    /// GetTotalArmor/GetTotalDamage по-прежнему используют предрассчитанные кэши
    /// (SetTotalArmor/SetTotalDamage из NPCSpawnerService), но автоматически
    /// пересчитываются из данных, если прямой кэш есть, а суммарные кэши не заданы.
    /// </summary>
    public class EquipmentDataProvider : IEquipmentDataProvider
    {
        // === Зависимости ===

        private readonly IItemDatabaseService? _itemDatabase;

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

        /// <summary>
        /// Прямой кэш EquipmentData: entityId → (слот → предмет).
        /// Путь игрока: EquipmentService пушит полные объекты (Phase 8).
        /// </summary>
        private readonly Dictionary<string, Dictionary<EquipmentSlot, EquipmentData>> _entityEquipmentData = new();

        // === Конструктор ===

        /// <summary>
        /// Конструктор с IItemDatabaseService (для резолва ID → EquipmentData).
        /// DI (жаднейший конструктор) подставит базу предметов; база регистрируется
        /// в GeneratorModule до первого Resolve — порядок модулей не важен.
        /// </summary>
        public EquipmentDataProvider(IItemDatabaseService? itemDatabase = null)
        {
            _itemDatabase = itemDatabase;
        }

        // === IEquipmentDataProvider ===

        /// <summary>
        /// Получить экипированный предмет в слоте.
        /// Phase 8: сначала прямой кэш данных (игрок), затем резолв ID через
        /// IItemDatabaseService (NPC). null — если ничего не надето или ID не зарегистрирован.
        /// </summary>
        public EquipmentData GetEquipped(string entityId, EquipmentSlot slot)
        {
            if (entityId == null) return null;

            // 1. Прямой кэш (игрок).
            if (_entityEquipmentData.TryGetValue(entityId, out var direct)
                && direct.TryGetValue(slot, out var directItem))
                return directItem;

            // 2. Резолв ID через базу предметов (NPC).
            string itemId = GetEquippedItemId(entityId, slot);
            if (itemId == null || _itemDatabase == null) return null;
            if (!_itemDatabase.TryGetItem(itemId, out var item)) return null;
            return item as EquipmentData;
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
                 _entityEquipmentData.ContainsKey(entityId) ||
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
            _entityEquipmentData.Remove(entityId);
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

        // === NPC_COMBAT_PREP Phase 8: агрегаты боевых статов (промилле) ===

        /// <summary>
        /// Суммарный модификатор уклонения от экипировки (промилле, знак сохраняется).
        /// Источник — EquipmentData.DodgeBonus всех надетых предметов (генератор
        /// «Матрёшка» пишет отрицательные значения для тяжёлой брони).
        /// </summary>
        public int GetDodgeBonusPermil(string entityId)
        {
            float sum = 0f;
            foreach (var item in EnumerateEquipment(entityId))
                sum += item.DodgeBonus;
            return (int)System.MathF.Round(sum * 10f); // % → промилле
        }

        /// <summary>
        /// Плоский бонус блока от экипировки (промилле).
        /// Источник — StatBonus "blockChance" (EQUIPMENT_SYSTEM.md §7.1 Defense).
        /// </summary>
        public int GetBlockBonusPermil(string entityId)
        {
            return SumFlatStatBonusPermil(entityId, "blockChance");
        }

        /// <summary>
        /// Плоский бонус парирования от экипировки (промилле).
        /// Источник — StatBonus "parryChance" (дата-driven; 0, пока контент
        /// не выдаёт такие бонусы).
        /// </summary>
        public int GetParryBonusPermil(string entityId)
        {
            return SumFlatStatBonusPermil(entityId, "parryChance");
        }

        /// <summary>
        /// Плоский бонус крит-шанса от экипировки атакующего (промилле).
        /// Источник — StatBonus "critChance" (EQUIPMENT_SYSTEM.md §7.1 Combat).
        /// </summary>
        public int GetCritBonusPermil(string entityId)
        {
            return SumFlatStatBonusPermil(entityId, "critChance");
        }

        /// <summary>
        /// Пробитие оружия основной руки (ед. брони).
        /// COMBAT_SYSTEM.md §11.5: penetration = weapon.penetration + STR×0.5 + techniquePenetration.
        /// </summary>
        public int GetWeaponPenetration(string entityId)
        {
            var weapon = GetEquipped(entityId, EquipmentSlot.WeaponMain);
            return weapon?.Penetration ?? 0;
        }

        /// <summary>
        /// Установить экипировку сущности напрямую (полные EquipmentData).
        /// Путь игрока (Phase 8): EquipmentService пушит свой словарь после
        /// каждого equip/unequip. Пересчитывает суммарные кэши урона/брони.
        /// </summary>
        public void SetEquipmentData(string entityId, Dictionary<EquipmentSlot, EquipmentData> equipment)
        {
            if (entityId == null) return;

            if (equipment == null)
            {
                _entityEquipmentData.Remove(entityId);
                return;
            }

            _entityEquipmentData[entityId] = new Dictionary<EquipmentSlot, EquipmentData>(equipment);

            // Пересчёт суммарных кэшей из агрегатора (консистентность с NPC-путём).
            float totalArmor = 0f, totalDamage = 0f;
            foreach (var kvp in equipment)
            {
                if (kvp.Value == null) continue;
                totalArmor += kvp.Value.Defense;
                totalDamage += kvp.Value.Damage;
            }
            _cachedTotalArmor[entityId] = totalArmor;
            _cachedTotalDamage[entityId] = totalDamage;
        }

        // === Вспомогательные ===

        /// <summary>
        /// Перечислить все надетые предметы сущности (прямой кэш + резолв ID).
        /// </summary>
        private IEnumerable<EquipmentData> EnumerateEquipment(string entityId)
        {
            if (entityId == null) yield break;

            // 1. Прямой кэш (игрок).
            if (_entityEquipmentData.TryGetValue(entityId, out var direct))
            {
                foreach (var kvp in direct)
                    if (kvp.Value != null)
                        yield return kvp.Value;
                yield break; // прямой кэш полный — ID не нужны
            }

            // 2. Резолв ID (NPC).
            if (!_entityEquipmentIds.TryGetValue(entityId, out var slots)) yield break;
            if (_itemDatabase == null) yield break;

            foreach (var kvp in slots)
            {
                if (kvp.Value == null) continue;
                if (_itemDatabase.TryGetItem(kvp.Value, out var item) && item is EquipmentData eq)
                    yield return eq;
            }
        }

        /// <summary>
        /// Сумма плоских StatBonus с именем statName по всей экипировке (промилле).
        /// StatBonus.Value трактуется как % (1% = 10 промилле, ЗАПРЕТ 3.9 на границе боя).
        /// </summary>
        private int SumFlatStatBonusPermil(string entityId, string statName)
        {
            float sum = 0f;
            foreach (var item in EnumerateEquipment(entityId))
            {
                if (item.StatBonuses == null) continue;
                foreach (var bonus in item.StatBonuses)
                {
                    if (bonus != null && bonus.StatName == statName)
                        sum += bonus.Value;
                }
            }
            return (int)System.MathF.Round(sum * 10f);
        }
    }
}

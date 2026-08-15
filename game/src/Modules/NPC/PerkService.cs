#nullable enable
// Создано: 2026-05-20 19:11:00 UTC
// Фаза 4, задача 4.2 — сервис перков NPC для бонусов проводимости.
// Перки реализованы как постоянные баффы (duration=float.MaxValue) через IBuffService.
// Проводимость обновляется через IQiDataProvider при изменении перков.
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Modules.NPC.Data;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.NPC
{
    /// <summary>
    /// Сервис перков NPC.
    /// Управляет наложением/снятием перков, делегируя в IBuffService для баффов.
    /// Рассчитывает суммарный бонус проводимости и обновляет IQiDataProvider.
    ///
    /// Архитектура:
    /// - PerkService.ApplyPerk → BuffService.ApplyBuff(duration=float.MaxValue) + обновление проводимости
    /// - PerkService.RemovePerk → BuffService.RemoveBuff + пересчёт проводимости
    /// - Внутренний справочник _perkRegistry содержит конфигурацию всех перков
    /// - Внутренний справочник _entityPerks отслеживает перки per-entity
    /// - _entityBaseConductivity хранит базовую проводимость (до перков) для пересчёта
    /// </summary>
    public sealed class PerkService : IPerkService
    {
        // === Зависимости (DI через конструктор) ===
        private readonly IBuffService _buffService;
        private readonly IQiDataProvider _qiDataProvider;

        // === Внутреннее состояние ===

        /// <summary>Перки сущности: entityId → список активных PerkType</summary>
        private readonly Dictionary<string, List<PerkType>> _entityPerks = new();

        /// <summary>Базовая проводимость сущности (до перков): entityId → float</summary>
        private readonly Dictionary<string, float> _entityBaseConductivity = new();

        /// <summary>Справочник всех перков: PerkType → PerkData</summary>
        private static readonly Dictionary<PerkType, PerkData> PerkRegistry = new()
        {
            {
                PerkType.GoldenBody, new PerkData
                {
                    Type = PerkType.GoldenBody,
                    Id = "perk_golden_body",
                    NameRu = "Золотое качество тела",
                    Description = "Тело совершенствуется до золотого стандарта, увеличивая проводимость меридиан на 30%.",
                    ConductivityBonus = 0.30f
                }
            },
            {
                PerkType.MeridianTempering, new PerkData
                {
                    Type = PerkType.MeridianTempering,
                    Id = "perk_meridian_tempering",
                    NameRu = "Закалка меридиан",
                    Description = "Меридианы закалены духовным огнём, повышая проводимость на 15%.",
                    ConductivityBonus = 0.15f
                }
            },
            {
                PerkType.CelestialChannels, new PerkData
                {
                    Type = PerkType.CelestialChannels,
                    Id = "perk_celestial_channels",
                    NameRu = "Небесные каналы",
                    Description = "Меридианы соединены с небесными каналами, увеличивая проводимость на 20%.",
                    ConductivityBonus = 0.20f
                }
            }
        };

        // === Конструктор (VContainer) ===

        public PerkService(IBuffService buffService, IQiDataProvider qiDataProvider)
        {
            _buffService = buffService;
            _qiDataProvider = qiDataProvider;
        }

        // === IPerkService: Управление перками ===

        /// <summary>
        /// Применить перк к сущности.
        /// Делегирует в BuffService.ApplyBuff с duration=float.MaxValue (бесконечный бафф).
        /// Обновляет проводимость через IQiDataProvider.
        /// </summary>
        public bool ApplyPerk(string entityId, PerkType perkType)
        {
            if (string.IsNullOrEmpty(entityId) || perkType == PerkType.None)
                return false;

            // Проверяем, есть ли уже такой перк
            if (HasPerk(entityId, perkType))
                return false;

            // Получаем данные перка из справочника
            if (!PerkRegistry.TryGetValue(perkType, out var perkData))
                return false;

            // Сохраняем базовую проводимость при первом перке
            SaveBaseConductivityIfNeeded(entityId);

            // Применяем перк как постоянный бафф через BuffService
            bool applied = _buffService.ApplyBuff(entityId, perkData.Id, float.MaxValue, perkData.ConductivityBonus);
            if (!applied)
                return false;

            // Регистрируем перк во внутреннем справочнике
            var perks = GetOrCreatePerkList(entityId);
            perks.Add(perkType);

            // Обновляем проводимость в QiDataProvider
            UpdateConductivity(entityId);

            return true;
        }

        /// <summary>
        /// Снять перк с сущности.
        /// Удаляет бафф через BuffService и пересчитывает проводимость.
        /// </summary>
        public bool RemovePerk(string entityId, PerkType perkType)
        {
            if (string.IsNullOrEmpty(entityId) || perkType == PerkType.None)
                return false;

            if (!PerkRegistry.TryGetValue(perkType, out var perkData))
                return false;

            // Удаляем бафф через BuffService
            bool removed = _buffService.RemoveBuff(entityId, perkData.Id);
            if (!removed)
                return false;

            // Убираем перк из внутреннего справочника
            if (_entityPerks.TryGetValue(entityId, out var perks))
            {
                perks.Remove(perkType);
                if (perks.Count == 0)
                {
                    _entityPerks.Remove(entityId);
                    _entityBaseConductivity.Remove(entityId);
                }
            }

            // Пересчитываем проводимость
            UpdateConductivity(entityId);

            return true;
        }

        /// <summary>
        /// Проверить наличие перка у сущности.
        /// Проверяет внутренний справочник (не BuffService, чтобы избежать коллизий ID).
        /// </summary>
        public bool HasPerk(string entityId, PerkType perkType)
        {
            if (string.IsNullOrEmpty(entityId) || perkType == PerkType.None)
                return false;

            return _entityPerks.TryGetValue(entityId, out var perks) && perks.Contains(perkType);
        }

        /// <summary>
        /// Получить суммарный бонус проводимости от всех перков сущности.
        /// Возвращает значение в диапазоне 0..N (например, 0.65 = +65% проводимости).
        /// </summary>
        public float GetTotalConductivityBonus(string entityId)
        {
            if (string.IsNullOrEmpty(entityId))
                return 0f;

            if (!_entityPerks.TryGetValue(entityId, out var perks))
                return 0f;

            float total = 0f;
            for (int i = 0; i < perks.Count; i++)
            {
                if (PerkRegistry.TryGetValue(perks[i], out var data))
                    total += data.ConductivityBonus;
            }
            return total;
        }

        /// <summary>
        /// Получить все перки сущности.
        /// Возвращает копию списка (безопасна для итерации).
        /// </summary>
        public List<PerkType> GetAllPerksForEntity(string entityId)
        {
            if (string.IsNullOrEmpty(entityId) || !_entityPerks.TryGetValue(entityId, out var perks))
                return new List<PerkType>();

            return new List<PerkType>(perks);
        }

        // === Внутренние методы ===

        /// <summary>
        /// Получить или создать список перков для сущности.
        /// </summary>
        private List<PerkType> GetOrCreatePerkList(string entityId)
        {
            if (!_entityPerks.TryGetValue(entityId, out var list))
            {
                list = new List<PerkType>();
                _entityPerks[entityId] = list;
            }
            return list;
        }

        /// <summary>
        /// Сохранить базовую проводимость сущности при первом применении перка.
        /// Базовая проводимость используется для пересчёта при добавлении/удалении перков.
        /// </summary>
        private void SaveBaseConductivityIfNeeded(string entityId)
        {
            if (_entityBaseConductivity.ContainsKey(entityId))
                return;

            float baseConductivity = _qiDataProvider.GetConductivity(entityId);
            _entityBaseConductivity[entityId] = baseConductivity;
        }

        /// <summary>
        /// Обновить проводимость сущности в QiDataProvider.
        /// Формула: effectiveConductivity = baseConductivity × (1 + totalPerkBonus)
        /// </summary>
        private void UpdateConductivity(string entityId)
        {
            if (!_qiDataProvider.HasEntity(entityId))
                return;

            // Базовая проводимость
            float baseConductivity = _entityBaseConductivity.TryGetValue(entityId, out var bc)
                ? bc
                : _qiDataProvider.GetConductivity(entityId);

            // Суммарный бонус от перков
            float totalBonus = GetTotalConductivityBonus(entityId);

            // Итоговая проводимость = базовая × (1 + бонус)
            float effectiveConductivity = baseConductivity * (1f + totalBonus);

            // Обновляем QiDataProvider (сохраняя текущие значения currentQi и maxQi)
            long currentQi = _qiDataProvider.GetCurrentQi(entityId);
            long maxQi = _qiDataProvider.GetMaxQi(entityId);
            _qiDataProvider.SetQiState(entityId, currentQi, maxQi, effectiveConductivity);
        }
    }
}

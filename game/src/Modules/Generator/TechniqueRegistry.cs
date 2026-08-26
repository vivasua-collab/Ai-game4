#nullable enable
// Создано: 2026-05-20 18:18 UTC
// Реестр сгенерированных техник — хранилище для боевого поиска.
// Источник: docs/NPC_ASSEMBLY_PIPELINE.md §6
using System.Collections.Generic;
using System.Linq;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Generator
{
    /// <summary>
    /// Простой реестр для хранения сгенерированных техник по ID.
    /// Используется для боевого поиска: CombatService запрашивает
    /// TechniqueData по techniqueId.
    ///
    /// Зарегистрирован как Singleton в DI через GeneratorModuleServices.
    /// </summary>
    public sealed class TechniqueRegistry
    {
        private readonly Dictionary<string, TechniqueData> _techniques = new();

        /// <summary>
        /// Зарегистрировать технику в реестре.
        /// Если техника с таким ID уже существует, она заменяется.
        /// </summary>
        /// <param name="technique">Данные техники для регистрации</param>
        public void Register(TechniqueData technique)
        {
            if (technique == null || string.IsNullOrEmpty(technique.TechniqueId))
                return;
            _techniques[technique.TechniqueId] = technique;
        }

        /// <summary>
        /// Получить технику по идентификатору.
        /// </summary>
        /// <param name="techniqueId">Идентификатор техники</param>
        /// <returns>TechniqueData или null, если не найдена</returns>
        public TechniqueData Get(string techniqueId)
        {
            if (string.IsNullOrEmpty(techniqueId))
                return null;
            _techniques.TryGetValue(techniqueId, out var data);
            return data;
        }

        /// <summary>
        /// Проверить существование техники в реестре.
        /// </summary>
        /// <param name="techniqueId">Идентификатор техники</param>
        /// <returns>true, если техника зарегистрирована</returns>
        public bool Exists(string techniqueId)
        {
            return !string.IsNullOrEmpty(techniqueId) && _techniques.ContainsKey(techniqueId);
        }

        /// <summary>
        /// Очистить реестр (используется при перегенерации или сбросе).
        /// </summary>
        public void Clear()
        {
            _techniques.Clear();
        }

        /// <summary>
        /// Получить все зарегистрированные техники (для дедупликации).
        /// </summary>
        public IReadOnlyCollection<TechniqueData> GetAll()
        {
            return _techniques.Values.ToList();
        }

        /// <summary>
        /// Удалить технику по идентификатору (для очистки дублей).
        /// </summary>
        public bool Remove(string techniqueId)
        {
            if (string.IsNullOrEmpty(techniqueId)) return false;
            return _techniques.Remove(techniqueId);
        }

        /// <summary>Количество зарегистрированных техник.</summary>
        public int Count => _techniques.Count;
    }
}

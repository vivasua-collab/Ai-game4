#nullable enable
// Создано: 2026-05-20 18:18 UTC
// Интерфейс генератора техник — для DI и тестирования.
// Источник: docs/NPC_ASSEMBLY_PIPELINE.md §6
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Интерфейс генератора техник.
    /// Детерминированная генерация TechniqueData на основе
    /// уровня культивации, роли NPC и seed.
    /// </summary>
    public interface ITechniqueGeneratorService
    {
        /// <summary>
        /// Сгенерировать одну технику.
        /// </summary>
        /// <param name="cultivationLevel">Уровень культивации NPC (1-10)</param>
        /// <param name="roleId">Роль NPC (определяет тип техники)</param>
        /// <param name="seed">Seed для детерминированной генерации</param>
        /// <returns>Сгенерированная TechniqueData</returns>
        TechniqueData Generate(int cultivationLevel, NPCRole roleId, long seed);

        /// <summary>
        /// Сгенерировать несколько техник.
        /// </summary>
        /// <param name="cultivationLevel">Уровень культивации NPC (1-10)</param>
        /// <param name="roleId">Роль NPC</param>
        /// <param name="count">Количество техник для генерации</param>
        /// <param name="seed">Базовый seed (каждая техника получает seed + i)</param>
        /// <returns>Список сгенерированных техник</returns>
        List<TechniqueData> GenerateMultiple(int cultivationLevel, NPCRole roleId, int count, long seed);
    }
}

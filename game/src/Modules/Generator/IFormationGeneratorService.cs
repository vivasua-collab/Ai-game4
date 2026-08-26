#nullable enable
// Этап 4 внедрения ЦИ (2026-08-23): IFormationGeneratorService.
// Живёт в модуле (НЕ Core): возвращает FormationData из Modules.Formation.Data,
// Core не может ссылаться на типы модулей (layering).
// Детерминированная генерация (GENERATORS_SYSTEM.md §9, FORMATION_SYSTEM.md §3-8).
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Generator
{
    /// <summary>
    /// Интерфейс генератора формаций.
    /// Все формулы — FormationCalculator (contourQi, capacity, drain).
    /// </summary>
    public interface IFormationGeneratorService
    {
        /// <summary>
        /// Сгенерировать формацию со случайными типом/размером/формой/стихией.
        /// Размер Heavy — только для level ≥ 6 (FORMATION_SYSTEM §4).
        /// </summary>
        FormationData Generate(int level, long seed);

        /// <summary>
        /// Сгенерировать формацию заданного типа и размера.
        /// Форма и стихия — детерминированный рандом по seed.
        /// </summary>
        FormationData GenerateSpecified(FormationType type, FormationSize size, int level, long seed);
    }
}

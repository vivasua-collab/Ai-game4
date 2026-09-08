#nullable enable
// Создано: 2026-05-20 18:18 UTC
// Интерфейс генератора техник — для DI и тестирования.
// Источник: docs/NPC_ASSEMBLY_PIPELINE.md §6
// Редактировано: 2026-08-23 — Этап 1 внедрения ЦИ: +GenerateSpecified (явный тип
// для выдачи тест-набора техник игроку).
// Редактировано: 2026-09-08 (ревью-1 P1-1): +BuildSpecified — генерация с ЯВНЫМ
// грейдом и БЕЗ авто-регистрации в реестре (для пайплайнов с валидацией/дедупом:
// PreGenTechniquePhase). Семантика семейства методов:
//   Generate*/GenerateSpecified — «сгенерировать + ЗАРЕГИСТРИРОВАТЬ» (NPC-пайплайн,
//     выдача игроку — техника сразу доступна боевому поиску по id);
//   BuildSpecified — «только построить объект» (регистрация — ответственность
//     вызывающего, ПОСЛЕ VerificationService.FilterValid и дедупликации).
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

        /// <summary>
        /// Сгенерировать технику заданного типа (этап 1 внедрения ЦИ).
        /// Для выдачи тест-набора игроку: тип фиксируется, остальное (подтип,
        /// стихия, грейд, мастерство) — детерминированный рандом по seed.
        /// Cultivation — пассивная техника (qiCost=0, BaseDamage=0).
        /// ЗАРЕГИСТРИРОВАН в TechniqueRegistry (боевой поиск по id работает).
        /// </summary>
        /// <param name="type">Тип техники (явно заданный)</param>
        /// <param name="level">Уровень техники (1..9, валидируется по cultivationLevel)</param>
        /// <param name="cultivationLevel">Уровень культивации практика (1-10)</param>
        /// <param name="seed">Seed для детерминированной генерации</param>
        TechniqueData GenerateSpecified(TechniqueType type, int level, int cultivationLevel, long seed);

        /// <summary>
        /// 2026-09-08 (ревью-1 P1-1): построить технику заданного типа И ГРЕЙДА,
        /// БЕЗ регистрации в TechniqueRegistry. Для пакетной пред-генерации:
        /// вызывающий валидирует (VerificationService.FilterValid),
        /// дедуплицирует (DeduplicationService) и регистрирует сам — только
        /// валидные уникальные объекты попадают в реестр.
        /// Грейд фиксируется явно (Common/Refined/Perfect/Transcendent);
        /// для Cultivation передавайте Common (§11.5 пассив — грейд не
        /// влияет на статы, но выбирается для консистентности id/имён).
        /// </summary>
        /// <param name="type">Тип техники (явно заданный)</param>
        /// <param name="grade">Грейд техники (явно заданный — НЕ случайный)</param>
        /// <param name="level">Уровень техники (1..9, валидируется по cultivationLevel)</param>
        /// <param name="cultivationLevel">Уровень культивации практика (1-10)</param>
        /// <param name="seed">Seed для детерминированной генерации</param>
        TechniqueData BuildSpecified(TechniqueType type, TechniqueGrade grade, int level, int cultivationLevel, long seed);
    }
}

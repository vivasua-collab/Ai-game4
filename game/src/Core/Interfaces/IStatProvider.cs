#nullable enable
// Создано: 2026-05-21 19:25:59 UTC
// Редактировано: 2026-05-22 04:14:49 UTC — Спринт 3 B6: GetElement/GetMaterial для стихий/материала цели
// Редактировано: 2026-05-22 11:30:00 UTC — Спринт 8 C10: GetMorphology для таблиц попадания
// Спринт 2 B3: Единый интерфейс доступа к статам для CombatService.
// Решает проблему: StatService хранит статы игрока, NPCState хранит статы NPC,
// но CombatService не имеет единого способа получить статы.
// IStatProvider — адаптер внутри Combat-модуля (ИСКЛЮЧЕНИЕ из EVT-01).
using CultivationGame.Core;

using CultivationGame.Core.Data;
namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Единый интерфейс доступа к статам сущностей для боевой системы.
    /// Спринт 2 B3: CombatService не может напрямую читать ни IStatService
    /// (только игрок), ни NPCState (только NPC). IStatProvider скрывает
    /// источник данных за единым интерфейсом.
    ///
    /// ИСКЛЮЧЕНИЕ из EVT-01: это адаптер ВНУТРИ Combat-модуля,
    /// а не кросс-модульная инъекция. Делегирует в IStatService/NPCService.
    ///
    /// ЗАПРЕТ 3.9: Все статы — int. Финальный результат кастуется к int.
    ///
    /// Спринт 3 B6: добавлены GetElement/GetMaterial для стихийных множителей.
    /// </summary>
    public interface IStatProvider
    {
        /// <summary>
        /// Получить значение стата сущности.
        /// Для игрока — делегирует в IStatService.GetStat().
        /// Для NPC — читает из NPCState (Strength/Agility/Vitality/Intelligence).
        /// </summary>
        /// <param name="entityId">Идентификатор сущности (игрок или NPC)</param>
        /// <param name="type">Тип стата</param>
        /// <returns>Значение стата (int — ЗАПРЕТ 3.9)</returns>
        int GetStat(string entityId, StatType type);

        /// <summary>
        /// Получить врождённую стихию сущности.
        /// Для игрока — Element.Neutral.
        /// Для NPC — NPCState.InnateElement (из SoulData).
        /// Спринт 3 B6: для стихийных множителей в DamageService.
        /// </summary>
        Element GetElement(string entityId);

        /// <summary>
        /// Получить материал тела сущности.
        /// Для игрока — BodyMaterial.Organic.
        /// Для NPC — NPCState.BodyMaterial (из SpeciesData.Material).
        /// Спринт 3 B6: для материального снижения в DefenseProcessor.
        /// </summary>
        BodyMaterial GetMaterial(string entityId);

        /// <summary>
        /// Получить морфологию тела сущности.
        /// Для игрока — Morphology.Humanoid.
        /// Для NPC — NPCState.Morphology (из SpeciesData.Morphology).
        /// Спринт 8 C10: для выбора таблицы попадания по частям тела.
        /// </summary>
        Morphology GetMorphology(string entityId);
    }
}

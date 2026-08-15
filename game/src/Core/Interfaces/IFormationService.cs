#nullable enable
// Создано: 2026-05-09
// Редактировано: 2026-05-22 07:55:00 UTC — Аудит CRIT-1: +GetFormationBonusPermil() для ЗАПРЕТ 3.9
// Интерфейс сервиса формаций.
// Управление жизненным циклом формаций: прорисовка, наполнение, активация, деактивация.
using System.Collections.Generic;

using CultivationGame.Core;
using CultivationGame.Core.Data;
namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Интерфейс сервиса формаций.
    /// Реализация: Modules.Formation.FormationService.
    ///
    /// АРХИТЕКТУРА (EVT-01): Formation модуль НЕ инжектит интерфейсы других модулей.
    /// Все кросс-модульные взаимодействия — через MessagePipe:
    /// - QiChangedEvent → кэш состояния Ци (вместо IQiService)
    /// - QiConsumeRequestEvent → команда расхода Ци
    /// - CombatEndedEvent → автодеактивация
    /// - ITickable.Tick() → обработка утечки Ци (FMT-A01: до реализации TimeModule)
    /// </summary>
    public interface IFormationService
    {
        /// <summary>Активна ли формация</summary>
        bool IsFormationActive { get; }

        /// <summary>Идентификатор активной формации (null если неактивна)</summary>
        string ActiveFormationId { get; }

        /// <summary>Текущая стадия формации</summary>
        FormationStage CurrentStage { get; }

        /// <summary>
        /// Начать прорисовку контура формации.
        /// Этап 1: Затрата contourQi от создателя.
        /// Публикует QiConsumeRequestEvent для расхода Ци.
        /// </summary>
        /// <param name="formationId">Идентификатор формации (из FormationData)</param>
        /// <param name="casterId">Идентификатор создателя</param>
        /// <returns>true если прорисовка начата</returns>
        bool StartDrawing(string formationId, string casterId);

        /// <summary>
        /// Начать наполнение формации Ци.
        /// Этап 2: Внесение Ци участниками через FormationContributeQiRequestEvent.
        /// Автоматически вызывается после завершения прорисовки.
        /// </summary>
        /// <returns>true если наполнение начато</returns>
        bool StartFilling();

        /// <summary>
        /// Внести Ци в формацию (от участника).
        /// Публикует QiConsumeRequestEvent для списания Ци с участника.
        /// </summary>
        /// <param name="contributorId">Идентификатор вносящего</param>
        /// <param name="amount">Количество Ци</param>
        /// <returns>Фактически внесённое количество</returns>
        long ContributeQi(string contributorId, long amount);

        /// <summary>
        /// Активировать формацию.
        /// Этап 3: Переход из Filling → Active при 100% заполнении.
        /// </summary>
        /// <returns>true если формация активирована</returns>
        bool ActivateFormation();

        /// <summary>
        /// Деактивировать формацию.
        /// Возвращает формацию в состояние None.
        /// </summary>
        /// <returns>true если формация деактивирована</returns>
        bool DeactivateFormation();

        /// <summary>
        /// Получить бонус формации для указанной характеристики.
        /// Используется в пайплайне урона (Слой 3b).
        /// </summary>
        /// <param name="stat">Тип характеристики</param>
        /// <returns>Модификатор (0 если формация неактивна)</returns>
        float GetFormationBonus(StatType stat);

        /// <summary>
        /// Получить бонус формации в промилле (ЗАПРЕТ 3.9).
        /// 0 = нет бонуса, 200 = +20%, -150 = -15%.
        /// Аудит CRIT-1: для integer math в боевом пайплайне.
        /// </summary>
        int GetFormationBonusPermil(StatType stat);

        /// <summary>Текущее Ци в пуле формации</summary>
        long QiPoolCurrent { get; }

        /// <summary>Максимальная ёмкость пула формации</summary>
        long QiPoolMax { get; }

        /// <summary>Количество участников формации</summary>
        int ParticipantCount { get; }

        /// <summary>Идентификатор создателя формации</summary>
        string CasterId { get; }

        /// <summary>
        /// Получить данные всех активных эффектов формации.
        /// </summary>
        IReadOnlyList<FormationEffectData> GetActiveEffects();
    }

    /// <summary>
    /// Данные эффекта формации (readonly struct — нулевая GC).
    /// </summary>
    public readonly struct FormationEffectData
    {
        public readonly FormationEffectType EffectType;
        public readonly StatType TargetStat;
        public readonly float Value;
        public readonly ControlType ControlType;
        public readonly string TargetTag; // "ally" или "enemy"

        public FormationEffectData(
            FormationEffectType effectType,
            StatType targetStat,
            float value,
            ControlType controlType = ControlType.None,
            string targetTag = "ally")
        {
            EffectType = effectType;
            TargetStat = targetStat;
            Value = value;
            ControlType = controlType;
            TargetTag = targetTag;
        }
    }
}

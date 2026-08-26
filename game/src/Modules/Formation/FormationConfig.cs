#nullable enable
// Создано: 2026-05-09
// Конфигурация модуля формаций.
// BD-48: class (не struct — mutable struct risk).
namespace CultivationGame.Modules.Formation
{
    /// <summary>
    /// Конфигурация модуля формаций.
    /// Устанавливается через FormationModule.SetConfig() из FormationLifetimeScope.
    /// </summary>
    public class FormationConfig
    {
        /// <summary>Идентификатор формации по умолчанию (из FormationData)</summary>
        public string DefaultFormationId = "basic_barrier";

        /// <summary>Идентификатор создателя по умолчанию</summary>
        public string DefaultCasterId = "player";

        /// <summary>Среда по умолчанию для множителя заполнения</summary>
        public string DefaultEnvironment = "normal";

        /// <summary>Автоматическая деактивация при окончании боя</summary>
        public bool AutoDeactivateOnCombatEnd = true;

        /// <summary>Автоматическая деактивация при истощении пула Ци</summary>
        public bool AutoDeactivateOnDepleted = true;

        /// <summary>Включить обработку утечки Ци (в тиках)</summary>
        public bool EnableDrain = true;

        /// <summary>Множитель скорости утечки (для отладки)</summary>
        public float DrainSpeedMultiplier = 1.0f;
    }
}

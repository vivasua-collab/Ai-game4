#nullable enable
// Создано: 2026-05-09
// Эффекты формации — БЕЗ статического изменяемого состояния.
// Каждый экземпляр FormationEffects привязан к конкретной формации.
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Modules.Formation.Data;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Formation
{
    /// <summary>
    /// Эффекты формации.
    /// Применяет баффы/дебаффи/урон/исцеление/контроль к сущностям в зоне действия.
    ///
    /// КЛЮЧЕВОЕ ОТЛИЧИЕ ОТ LEGACY: Никакого статического состояния!
    /// Legacy FormationEffects использовал static Dictionary для сохранения состояний RigidBody.
    /// Новая реализация — instance-based, каждый экземпляр привязан к формации.
    /// </summary>
    public class FormationEffects
    {
        // === Состояние (instance-based, NOT static) ===
        private readonly List<FormationEffectData> _activeEffects = new List<FormationEffectData>();
        private readonly Dictionary<StatType, float> _statBonuses = new Dictionary<StatType, float>();
        private bool _isActive;

        /// <summary>Активны ли эффекты формации</summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// Инициализировать эффекты из данных формации.
        /// </summary>
        public void Initialize(FormationData formationData)
        {
            _activeEffects.Clear();
            _statBonuses.Clear();

            foreach (var entry in formationData.Effects)
            {
                var effectData = new FormationEffectData(
                    entry.EffectType,
                    entry.TargetStat,
                    entry.Value,
                    entry.ControlType,
                    entry.TargetTag);

                _activeEffects.Add(effectData);

                // Агрегируем бонусы по статам (только для Buff/Debuff)
                if (entry.EffectType == FormationEffectType.Buff ||
                    entry.EffectType == FormationEffectType.Debuff)
                {
                    float sign = entry.EffectType == FormationEffectType.Buff ? 1f : -1f;
                    if (_statBonuses.ContainsKey(entry.TargetStat))
                        _statBonuses[entry.TargetStat] += sign * entry.Value;
                    else
                        _statBonuses[entry.TargetStat] = sign * entry.Value;
                }
            }
        }

        /// <summary>
        /// Активировать эффекты формации.
        /// В будущих фазах: применение баффов через MessagePipe.
        /// </summary>
        public void Activate()
        {
            _isActive = true;
            // В будущих фазах: публикация BuffAppliedEvent для каждого эффекта
        }

        /// <summary>
        /// Деактивировать эффекты формации.
        /// В будущих фазах: снятие баффов через MessagePipe.
        /// </summary>
        public void Deactivate()
        {
            _isActive = false;
            // В будущих фазах: публикация BuffRemovedEvent для каждого эффекта
        }

        /// <summary>
        /// Получить бонус формации для указанной характеристики.
        /// Используется в пайплайне урона (Слой 3b).
        /// Возвращает 0 если формация неактивна.
        /// </summary>
        /// <param name="stat">Тип характеристики</param>
        /// <returns>Модификатор (0 если неактивна или нет бонуса)</returns>
        public float GetFormationBonus(StatType stat)
        {
            if (!_isActive) return 0f;
            if (_statBonuses.TryGetValue(stat, out float bonus))
                return bonus;
            return 0f;
        }

        /// <summary>
        /// Получить все активные эффекты.
        /// </summary>
        public IReadOnlyList<FormationEffectData> GetActiveEffects()
        {
            return _activeEffects.AsReadOnly();
        }

        /// <summary>
        /// Сбросить все эффекты.
        /// </summary>
        public void Reset()
        {
            _isActive = false;
            _activeEffects.Clear();
            _statBonuses.Clear();
        }
    }
}

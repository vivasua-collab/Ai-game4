#nullable enable
// Создано: 2026-05-09
// Редактировано: 2026-10-20 — АУДИТ-1020 (BUG-008): агрегация бонусов по
//   (StatType, TargetTag) — TargetTag больше не теряется (прежде бонусы
//   формации действовали на ЛЮБЫЕ сущности на всей карте).
// Эффекты формации — БЕЗ статического изменяемого состояния.
// Каждый экземпляр FormationEffects привязан к конкретной формации.
using System.Collections.Generic;
using CultivationGame.Core;
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

        // АУДИТ-1020 (BUG-008): агрегация по (StatType, TargetTag).
        // Прежний словарь по StatType терял TargetTag → DamageService тянул
        // бонус формации для ЛЮБОЙ сущности (барьер игрока «защищал» врагов
        // по всей карте, Amplification усиливал вражеский урон).
        private readonly Dictionary<(StatType Stat, string Tag), float> _statBonusesByTag =
            new Dictionary<(StatType, string), float>();

        // АУДИТ-1020 (BUG-008): LEGACY-словарь по StatType — сумма по всем тегам,
        // прежняя семантика до бага. Оставлен для старых QA-вызовов
        // GetFormationBonus(stat); боевые потребители перешли на
        // tag-aware GetFormationBonus(stat, targetTag).
        private readonly Dictionary<StatType, float> _statBonusesLegacy = new Dictionary<StatType, float>();

        private bool _isActive;

        /// <summary>Активны ли эффекты формации</summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// Инициализировать эффекты из данных формации.
        /// </summary>
        public void Initialize(FormationData formationData)
        {
            _activeEffects.Clear();
            _statBonusesByTag.Clear();
            _statBonusesLegacy.Clear();

            foreach (var entry in formationData.Effects)
            {
                var effectData = new FormationEffectData(
                    entry.EffectType,
                    entry.TargetStat,
                    entry.Value,
                    entry.ControlType,
                    entry.TargetTag);

                _activeEffects.Add(effectData);

                // Агрегируем бонусы по статам (только для Buff/Debuff).
                // VER-001 (10-19-fix-b, аудит form-s7): + Shield — барьерные
                // формации (Barrier-тип, FormationGeneratorService.BuildEffects)
                // пишут эффект как Shield/Defense, но агрегировались только
                // Buff/Debuff → GetFormationBonusPermil(Defense) всегда 0, а
                // потребитель пайплайна урона СУЩЕСТВУЕТ (DamageService MED-2
                // тянет GetFormationBonusPermil(StatType.Defense)) — эффект
                // барьера был «мёртв». Shield агрегируется со знаком «+» по
                // своему TargetStat (генератор: Defense 0.2..0.6 → 200..600‰).
                if (entry.EffectType == FormationEffectType.Buff ||
                    entry.EffectType == FormationEffectType.Debuff ||
                    entry.EffectType == FormationEffectType.Shield)
                {
                    float sign = entry.EffectType == FormationEffectType.Debuff ? -1f : 1f;

                    // АУДИТ-1020 (BUG-008): tag-aware агрегация — ключ
                    // (StatType, TargetTag). Tag пустой → «ally» (дефолт
                    // FormationEffectData/генератора).
                    string tag = string.IsNullOrEmpty(entry.TargetTag) ? "ally" : entry.TargetTag;
                    var key = (entry.TargetStat, tag);
                    if (_statBonusesByTag.ContainsKey(key))
                        _statBonusesByTag[key] += sign * entry.Value;
                    else
                        _statBonusesByTag[key] = sign * entry.Value;

                    // LEGACY: сумма по всем тегам (прежняя семантика;
                    // см. комментарий у _statBonusesLegacy).
                    if (_statBonusesLegacy.ContainsKey(entry.TargetStat))
                        _statBonusesLegacy[entry.TargetStat] += sign * entry.Value;
                    else
                        _statBonusesLegacy[entry.TargetStat] = sign * entry.Value;
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
        /// LEGACY: бонус формации для характеристики по ЛЮБОМУ тегу —
        /// СУММА бонусов всех тегов (прежняя семантика до АУДИТ-1020/BUG-008,
        /// когда TargetTag терялся при агрегации).
        /// Боевые потребители используют tag-aware перегрузку ниже; этот метод
        /// оставлен для старых QA-вызовов FormationShotSimDebug (4b/4d).
        /// Возвращает 0 если формация неактивна.
        /// </summary>
        /// <param name="stat">Тип характеристики</param>
        /// <returns>Модификатор (0 если неактивна или нет бонуса)</returns>
        public float GetFormationBonus(StatType stat)
        {
            if (!_isActive) return 0f;
            if (_statBonusesLegacy.TryGetValue(stat, out float bonus))
                return bonus;
            return 0f;
        }

        /// <summary>
        /// АУДИТ-1020 (BUG-008): бонус формации для характеристики по тегу
        /// цели ("ally"/"enemy") — TargetTag учитывается.
        /// Возвращает 0 если формация неактивна или тег не имеет бонуса
        /// (барьер с Shield/ally не даёт Defense врагам).
        /// </summary>
        /// <param name="stat">Тип характеристики</param>
        /// <param name="targetTag">Тег стороны цели ("ally" или "enemy")</param>
        /// <returns>Модификатор (0 если неактивна или нет бонуса для тега)</returns>
        public float GetFormationBonus(StatType stat, string targetTag)
        {
            if (!_isActive) return 0f;
            if (string.IsNullOrEmpty(targetTag)) return 0f; // без тега — нет зонального бонуса
            if (_statBonusesByTag.TryGetValue((stat, targetTag), out float bonus))
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
            _statBonusesByTag.Clear();
            _statBonusesLegacy.Clear();
        }
    }
}

#nullable enable
// Создано: 2026-05-09
// Редактировано: 2026-05-24 05:45:00 UTC — FIX CS1061: LearnedTechnique +CastTime/ArmorPenetration/BaseDamage/Element/IsUltimate; LearnTechnique расширён
// Редактировано: 2026-05-09 — CMB-A06: QiCost изменён с float на long (Fix-01)
// Редактировано: 2026-05-09 — EVT-01: убрана инъекция IQiService,
//   кросс-модульное общение через QiChangedEvent + QiConsumeRequestEvent
// Редактировано: 2026-08-23 — Этап 1-2 внедрения ЦИ:
//   слотовая модель TECHNIQUE_SYSTEM.md §12 (Cultivation 1, Combat 3+(L-1),
//   Curse 1, Formation 1), LearnTechnique(TechniqueData), выбор активной
//   техники, рост мастерства при использовании (§10), публикация
//   TechniqueLearnedEvent/TechniqueSelectionChangedEvent.
// Сервис управления техниками — изучение, применение, отслеживание кулдаунов.
using System;
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Combat
{
    /// <summary>
    /// Сервис управления техниками.
    /// Отслеживает изученные техники, кулдауны и стоимость Ци.
    ///
    /// АРХИТЕКТУРА: TechniqueService НЕ инжектит IBodyService, IQiService.
    /// Межмодульное общение — через EventBus (TechniqueUsedEvent, QiConsumeRequestEvent).
    ///
    /// СЛОТЫ (TECHNIQUE_SYSTEM.md §12):
    ///   Cultivation 1 | Combat 3+(L-1) | Curse 1 | Formation 1.
    ///   Все активные типы (Defense/Support/Healing/Movement/Sensory/Poison)
    ///   занимают слоты пула Combat.
    ///
    /// CMB-A06: LearnedTechnique.QiCost — long (Fix-01: все Qi-значения long).
    /// EVT-01: кэш Qi из QiChangedEvent вместо инъекции IQiService.
    /// </summary>
    public class TechniqueService : IDisposable
    {
        // === Зависимости ===
        private readonly IPublisher<TechniqueUsedEvent> _techniqueUsedPub;
        private readonly IPublisher<TechniqueLearnedEvent> _techniqueLearnedPub;
        private readonly IPublisher<TechniqueForgottenEvent> _techniqueForgottenPub;
        private readonly IPublisher<TechniqueSelectionChangedEvent> _techniqueSelectionPub;

        // EVT-01: подписки на кросс-модульные события (вместо инъекции IQiService)
        private readonly ISubscriber<QiChangedEvent> _qiChangedSub;
        private readonly IPublisher<QiConsumeRequestEvent> _qiConsumeRequestPub;

        // EVT-01: кэш состояния из событий
        private int _cachedCultivationLevel = 1;
        private string _cachedEntityId = "";
        private long _cachedCurrentQi;

        // IDisposable для подписок
        private IDisposable _qiChangedSubscription;

        // === Состояние ===
        private readonly Dictionary<string, LearnedTechnique> _learnedTechniques = new();
        private readonly Dictionary<string, float> _cooldowns = new();
        private readonly List<string> _orderedIds = new(); // порядок изучения (для UI/цикла выбора)
        private string? _selectedTechniqueId;
        private int _usedCapacity;

        // === Конструктор ===
        public TechniqueService(
            IPublisher<TechniqueUsedEvent> techniqueUsedPub,
            IPublisher<TechniqueLearnedEvent> techniqueLearnedPub,
            IPublisher<TechniqueForgottenEvent> techniqueForgottenPub,
            IPublisher<TechniqueSelectionChangedEvent> techniqueSelectionPub,
            ISubscriber<QiChangedEvent> qiChangedSub,
            IPublisher<QiConsumeRequestEvent> qiConsumeRequestPub)
        {
            _techniqueUsedPub = techniqueUsedPub;
            _techniqueLearnedPub = techniqueLearnedPub;
            _techniqueForgottenPub = techniqueForgottenPub;
            _techniqueSelectionPub = techniqueSelectionPub;
            _qiChangedSub = qiChangedSub;
            _qiConsumeRequestPub = qiConsumeRequestPub;

            // EVT-01: подписка на кэш состояния Ци
            _qiChangedSubscription = _qiChangedSub.Subscribe((in QiChangedEvent e) => {
                _cachedCurrentQi = e.Current;
                _cachedCultivationLevel = e.CultivationLevel;
                _cachedEntityId = e.EntityId;
            });
        }

        // === Выбор активной техники ===

        /// <summary>
        /// Этап 5: внешний бонус урона техник (пермил, ЗАПРЕТ 3.9).
        /// Источник — активная формация Amplification в зоне игрока.
        /// 0 = нет бонуса; 1300 = +30%. Применяется в CombatService.GetTechniqueDamage.
        /// </summary>
        public int ExternalDamageBonusPermil;

        /// <summary>ID выбранной техники (null — не выбрана).</summary>
        public string? SelectedTechniqueId => _selectedTechniqueId;

        /// <summary>Выбранная техника (null — не выбрана).</summary>
        public LearnedTechnique? SelectedTechnique
            => _selectedTechniqueId != null && _learnedTechniques.TryGetValue(_selectedTechniqueId, out var t) ? t : null;

        /// <summary>Выбрать активную технику (для каста по R/клику). null — сброс.</summary>
        public void SelectTechnique(string? techniqueId)
        {
            if (techniqueId != null && !_learnedTechniques.ContainsKey(techniqueId)) return;
            _selectedTechniqueId = techniqueId;
            _techniqueSelectionPub.Publish(new TechniqueSelectionChangedEvent(techniqueId));
        }

        /// <summary>Циклический выбор следующей кастуемой техники (Q).</summary>
        public void CycleSelection()
        {
            if (_orderedIds.Count == 0) return;
            int idx = _selectedTechniqueId != null ? _orderedIds.IndexOf(_selectedTechniqueId) : -1;
            string next = _orderedIds[(idx + 1) % _orderedIds.Count];
            SelectTechnique(next);
        }

        // === Слоты (TECHNIQUE_SYSTEM.md §12) ===

        /// <summary>Категория слота для типа техники.</summary>
        public static TechniqueType SlotCategory(TechniqueType type)
        {
            return type switch
            {
                TechniqueType.Cultivation => TechniqueType.Cultivation,
                TechniqueType.Curse => TechniqueType.Curse,
                TechniqueType.Formation => TechniqueType.Formation,
                // Defense/Support/Healing/Movement/Sensory/Poison/Combat → пул Combat
                _ => TechniqueType.Combat
            };
        }

        /// <summary>Ёмкость слотов категории для уровня культивации (§12).</summary>
        public static int SlotCapacity(TechniqueType category, int cultivationLevel)
        {
            int level = Math.Max(1, cultivationLevel);
            return category switch
            {
                TechniqueType.Cultivation => 1,
                TechniqueType.Curse => 1,
                TechniqueType.Formation => 1,
                TechniqueType.Combat => 3 + (level - 1),
                _ => 3 + (level - 1)
            };
        }

        /// <summary>Сколько техник категории занято.</summary>
        public int UsedSlots(TechniqueType type)
        {
            var category = SlotCategory(type);
            int used = 0;
            foreach (var t in _learnedTechniques.Values)
                if (SlotCategory(t.Type) == category) used++;
            return used;
        }

        /// <summary>Сколько свободных слотов для категории типа.</summary>
        public int FreeSlots(TechniqueType type)
        {
            return SlotCapacity(SlotCategory(type), _cachedCultivationLevel) - UsedSlots(type);
        }

        // === Изучение ===

        /// <summary>
        /// Изучить сгенерированную технику (TechniqueData из TechniqueGeneratorService).
        /// Проверяет: свободный слот категории, уровень резонанса (§8.1: L_техники ≤ L_практика
        /// и ≥ max(1, L_практика − 4)).
        /// </summary>
        public bool LearnTechnique(TechniqueData data)
        {
            if (data == null || string.IsNullOrEmpty(data.TechniqueId)) return false;
            if (_learnedTechniques.ContainsKey(data.TechniqueId)) return false;

            // Ограничение уровня (TECHNIQUE_SYSTEM.md §8.1 — Резонанс Ци)
            int minL = Math.Max(1, _cachedCultivationLevel - 4);
            if (data.Level > _cachedCultivationLevel || data.Level < minL) return false;

            // Слот категории
            if (FreeSlots(data.Type) <= 0) return false;

            _learnedTechniques[data.TechniqueId] = new LearnedTechnique
            {
                TechniqueId = data.TechniqueId,
                Name = data.NameRu,
                Type = data.Type,
                Grade = data.Grade,
                Subtype = data.Subtype,
                Level = data.Level,
                Element = data.Element,
                QiCost = data.QiCost,
                Cooldown = data.Cooldown,
                CastTime = data.CastTime,
                Range = data.Range,
                ArmorPenetration = data.ArmorPenetration,
                BaseDamage = data.BaseDamage,
                IsUltimate = data.IsUltimate,
                Mastery = data.Mastery
            };
            _orderedIds.Add(data.TechniqueId);

            // Авто-выбор первой изученной техники
            if (_selectedTechniqueId == null) SelectTechnique(data.TechniqueId);

            _techniqueLearnedPub.Publish(new TechniqueLearnedEvent(
                data.TechniqueId, data.NameRu, data.Type, data.Grade));
            return true;
        }

        /// <summary>
        /// Забыть технику (освободить слот). Тест-режим/читы.
        /// </summary>
        public bool ForgetTechnique(string techniqueId)
        {
            if (!_learnedTechniques.Remove(techniqueId)) return false;
            _orderedIds.Remove(techniqueId);
            _cooldowns.Remove(techniqueId);
            if (_selectedTechniqueId == techniqueId)
            {
                _selectedTechniqueId = _orderedIds.Count > 0 ? _orderedIds[0] : null;
                _techniqueSelectionPub.Publish(new TechniqueSelectionChangedEvent(_selectedTechniqueId));
            }
            _techniqueForgottenPub.Publish(new TechniqueForgottenEvent(techniqueId));
            return true;
        }

        /// <summary>Забыть все техники (тест-режим/читы).</summary>
        public void ForgetAll()
        {
            foreach (var id in _orderedIds.ToArray())
                ForgetTechnique(id);
        }

        /// <summary>
        /// Изучить технику (legacy-сигнатура по компонентам).
        /// CMB-A06: qiCost — long (Fix-01).
        /// </summary>
        public bool LearnTechnique(string techniqueId, TechniqueType type, TechniqueGrade grade,
            CombatSubtype subtype, long qiCost, float cooldown,
            float castTime = 0.5f, int armorPenetration = 0, int baseDamage = 0,
            Element element = Element.Neutral, bool isUltimate = false)
        {
            var data = new TechniqueData
            {
                TechniqueId = techniqueId,
                NameRu = techniqueId,
                Type = type,
                Grade = grade,
                Subtype = subtype,
                Level = _cachedCultivationLevel,
                Element = element,
                QiCost = qiCost,
                Cooldown = cooldown,
                CastTime = castTime,
                ArmorPenetration = armorPenetration,
                BaseDamage = baseDamage,
                IsUltimate = isUltimate
            };
            return LearnTechnique(data);
        }

        // === Использование ===

        /// <summary>
        /// Использовать технику (legacy — мгновенный расход Ци).
        /// CMB-A06: QiCost — long, каст не нужен.
        /// EVT-01: проверка Ци из кэша + QiConsumeRequestEvent вместо IQiService.TryConsumeQi.
        /// Этап 1: рост мастерства при использовании (§10: mastery = min(100, +0.01)).
        /// </summary>
        public bool UseTechnique(string techniqueId)
        {
            if (!_learnedTechniques.TryGetValue(techniqueId, out var tech))
                return false;

            // Проверка кулдауна
            if (_cooldowns.TryGetValue(techniqueId, out var remaining) && remaining > 0)
                return false;

            // EVT-01: проверка Ци из кэша (best-effort: кэш актуален в рамках кадра)
            if (_cachedCurrentQi < tech.QiCost)
                return false;

            // EVT-01: запрашиваем расход Ци через событие
            _qiConsumeRequestPub.Publish(new QiConsumeRequestEvent(tech.QiCost, "TechniqueService"));

            // Установка кулдауна
            if (tech.Cooldown > 0)
                _cooldowns[techniqueId] = tech.Cooldown;

            // Рост мастерства (TECHNIQUE_SYSTEM.md §5.1 шаг 5)
            tech.Mastery = MathF.Min(100f, tech.Mastery + 0.01f);

            // Публикация события
            // Фаза 9D: QiCost float→int (ЗАПРЕТ 3.9)
            _techniqueUsedPub.Publish(new TechniqueUsedEvent(
                _cachedEntityId, techniqueId, (int)tech.QiCost));

            return true;
        }

        /// <summary>
        /// Stage 0 (2026-08-25, GLM-5.3): завершение использования техники ПОСЛЕ зарядки.
        /// Вызывается PlayerTechniqueCaster.OnChargeCompleted после того, как
        /// TechniqueChargeService дренировал Ци тиками (расход уже учтён).
        /// Делает ТОЛЬКО: установку кулдауна, рост мастерства, публикацию TechniqueUsedEvent.
        /// НЕ списывает Ци (уже сделано сервисом зарядки).
        /// </summary>
        public bool CompleteUse(string techniqueId)
        {
            if (!_learnedTechniques.TryGetValue(techniqueId, out var tech))
                return false;

            // Кулдаун (на случай повторного вызова — но PlayerTechniqueCaster гарантирует путь)
            if (_cooldowns.TryGetValue(techniqueId, out var remaining) && remaining > 0)
                return false;

            // Установка кулдауна
            if (tech.Cooldown > 0)
                _cooldowns[techniqueId] = tech.Cooldown;

            // Рост мастерства (TECHNIQUE_SYSTEM.md §5.1 шаг 5)
            tech.Mastery = MathF.Min(100f, tech.Mastery + 0.01f);

            // Публикация события
            _techniqueUsedPub.Publish(new TechniqueUsedEvent(
                _cachedEntityId, techniqueId, (int)tech.QiCost));

            return true;
        }

        /// <summary>
        /// Обновить кулдауны (вызывается из CombatModule.Tick).
        /// </summary>
        public void UpdateCooldowns(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            List<string> expired = null;
            foreach (var kvp in _cooldowns)
            {
                float remaining = kvp.Value - deltaTime;
                if (remaining <= 0f)
                {
                    expired ??= new List<string>();
                    expired.Add(kvp.Key);
                }
                else
                {
                    _cooldowns[kvp.Key] = remaining;
                }
            }

            if (expired != null)
            {
                foreach (var key in expired)
                    _cooldowns.Remove(key);
            }
        }

        /// <summary>
        /// Получить оставшееся время кулдауна.
        /// </summary>
        public float GetCooldown(string techniqueId)
        {
            return _cooldowns.TryGetValue(techniqueId, out var remaining) ? remaining : 0f;
        }

        /// <summary>
        /// Проверить, изучена ли техника.
        /// </summary>
        public bool IsLearned(string techniqueId)
        {
            return _learnedTechniques.ContainsKey(techniqueId);
        }

        /// <summary>
        /// Получить изученную технику.
        /// </summary>
        public LearnedTechnique GetTechnique(string techniqueId)
        {
            return _learnedTechniques.TryGetValue(techniqueId, out var tech) ? tech : null;
        }

        /// <summary>
        /// Получить все изученные техники.
        /// </summary>
        public IReadOnlyDictionary<string, LearnedTechnique> GetAllTechniques()
        {
            return _learnedTechniques;
        }

        /// <summary>Идентификаторы изученных техник в порядке изучения (для UI).</summary>
        public IReadOnlyList<string> GetOrderedIds()
        {
            return _orderedIds;
        }

        public void Dispose()
        {
            _qiChangedSubscription?.Dispose();
            _qiChangedSubscription = null;
        }
    }

    /// <summary>
    /// Данные изученной техники.
    /// CMB-A06: QiCost — long (Fix-01: все Qi-значения long).
    /// FIX CS1061: добавлены CastTime, ArmorPenetration, BaseDamage, Element, IsUltimate —
    /// поля из TechniqueData, необходимые CombatService.
    /// Этап 1: +Name, +Level, +Range, +Mastery (живой рост при использовании).
    /// </summary>
    public class LearnedTechnique
    {
        public string TechniqueId;
        public string Name = "";
        public TechniqueType Type;
        public TechniqueGrade Grade;
        public CombatSubtype Subtype;
        public int Level;                 // Уровень техники (1..9)
        public long QiCost;               // CMB-A06: long вместо float (Fix-01)
        public float Cooldown;
        public int CapacityCost;
        public float CastTime;            // FIX CS1061: время каста (из TechniqueData)
        public float Range;               // Дальность (метры, из TechniqueData)
        public int ArmorPenetration;      // FIX CS1061: пробитие брони (из TechniqueData, C6)
        public int BaseDamage;            // FIX CS1061: базовый урон (из TechniqueData, P1-6.1: int)
        public Element Element;           // FIX CS1061: стихия (из TechniqueData, B5)
        public bool IsUltimate;           // FIX CS1061: Ultimate-техника (из TechniqueData)
        public float Mastery;             // Этап 1: мастерство 0..100 (растёт при использовании)
    }
}

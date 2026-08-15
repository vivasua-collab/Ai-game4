#nullable enable
// Создано: 2026-05-09
// Редактировано: 2026-05-24 05:45:00 UTC — FIX CS1061: LearnedTechnique +CastTime/ArmorPenetration/BaseDamage/Element/IsUltimate; LearnTechnique расширён
// Редактировано: 2026-05-09 — CMB-A06: QiCost изменён с float на long (Fix-01)
// Редактировано: 2026-05-09 — EVT-01: убрана инъекция IQiService,
//   кросс-модульное общение через QiChangedEvent + QiConsumeRequestEvent
// Сервис управления техниками — изучение, применение, отслеживание кулдаунов.
// Перенесено из legacy Combat/TechniqueController.cs с адаптацией.
using System;
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Combat
{
    /// <summary>
    /// Сервис управления техниками.
    /// Отслеживает изученные техники, кулдауны и стоимость Ци.
    ///
    /// АРХИТЕКТУРА: TechniqueService НЕ инжектит IBodyService, IQiService.
    /// Межмодульное общение — через MessagePipe (TechniqueUsedEvent, QiConsumeRequestEvent).
    ///
    /// CMB-A06: LearnedTechnique.QiCost — long (Fix-01: все Qi-значения long).
    /// EVT-01: кэш Qi из QiChangedEvent вместо инъекции IQiService.
    /// </summary>
    public class TechniqueService : IDisposable
    {
        // === Зависимости ===
        private readonly IPublisher<TechniqueUsedEvent> _techniqueUsedPub;

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
        private int _usedCapacity;

        // === Конструктор ===
        public TechniqueService(
            IPublisher<TechniqueUsedEvent> techniqueUsedPub,
            ISubscriber<QiChangedEvent> qiChangedSub,
            IPublisher<QiConsumeRequestEvent> qiConsumeRequestPub)
        {
            _techniqueUsedPub = techniqueUsedPub;
            _qiChangedSub = qiChangedSub;
            _qiConsumeRequestPub = qiConsumeRequestPub;

            // EVT-01: подписка на кэш состояния Ци
            _qiChangedSubscription = _qiChangedSub.Subscribe((in QiChangedEvent e) => {
                _cachedCurrentQi = e.Current;
                _cachedCultivationLevel = e.CultivationLevel;
                _cachedEntityId = e.EntityId;
            });
        }

        /// <summary>
        /// Изучить технику.
        /// CMB-A06: qiCost — long (Fix-01).
        /// FIX CS1061: добавлены castTime, armorPenetration, baseDamage, element, isUltimate.
        /// </summary>
        public bool LearnTechnique(string techniqueId, TechniqueType type, TechniqueGrade grade,
            CombatSubtype subtype, long qiCost, float cooldown,
            float castTime = 0.5f, int armorPenetration = 0, int baseDamage = 0,
            Element element = Element.Neutral, bool isUltimate = false)
        {
            if (string.IsNullOrEmpty(techniqueId)) return false;
            if (_learnedTechniques.ContainsKey(techniqueId)) return false;

            // Проверка ёмкости
            int cost = TechniqueCapacity.CalculateCost(type, grade, subtype);
            int capacity = TechniqueCapacity.CalculateCapacity(type, _cachedCultivationLevel); // EVT-01: из кэша

            if (!TechniqueCapacity.CanLearn(_usedCapacity, capacity, cost))
                return false;

            _learnedTechniques[techniqueId] = new LearnedTechnique
            {
                TechniqueId = techniqueId,
                Type = type,
                Grade = grade,
                Subtype = subtype,
                QiCost = qiCost, // CMB-A06: long вместо float
                Cooldown = cooldown,
                CapacityCost = cost,
                CastTime = castTime,               // FIX CS1061
                ArmorPenetration = armorPenetration, // FIX CS1061
                BaseDamage = baseDamage,             // FIX CS1061
                Element = element,                   // FIX CS1061
                IsUltimate = isUltimate              // FIX CS1061
            };

            _usedCapacity += cost;
            return true;
        }

        /// <summary>
        /// Использовать технику.
        /// CMB-A06: QiCost — long, каст не нужен.
        /// EVT-01: проверка Ци из кэша + QiConsumeRequestEvent вместо IQiService.TryConsumeQi.
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

            // Публикация события
            // Фаза 9D: QiCost float→int (ЗАПРЕТ 3.9)
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
    /// </summary>
    public class LearnedTechnique
    {
        public string TechniqueId;
        public TechniqueType Type;
        public TechniqueGrade Grade;
        public CombatSubtype Subtype;
        public long QiCost;              // CMB-A06: long вместо float (Fix-01)
        public float Cooldown;
        public int CapacityCost;
        public float CastTime;            // FIX CS1061: время каста (из TechniqueData)
        public int ArmorPenetration;      // FIX CS1061: пробитие брони (из TechniqueData, C6)
        public int BaseDamage;            // FIX CS1061: базовый урон (из TechniqueData, P1-6.1: int)
        public Element Element;           // FIX CS1061: стихия (из TechniqueData, B5)
        public bool IsUltimate;           // FIX CS1061: Ultimate-техника (из TechniqueData)
    }
}

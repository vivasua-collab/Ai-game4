#nullable enable
// Создано: 2026-05-09
// Редактировано: 2026-08-22 — IMPL-6 (Q5): Random.Shared → ICombatRng (детерминированный бой).
// Редактировано: 2026-05-09 — CMB-C02: использование Aggressiveness в UpdateAI
// AI противников — управление поведением NPC в бою.
// Перенесено из legacy Combat/CombatAI.cs с адаптацией.
using System;
using CultivationGame.Core.Data;
using CultivationGame.Modules.Combat.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Combat
{
    /// <summary>
    /// Сервис AI противников.
    /// Управляет поведением NPC в бою на основе личности и текущей ситуации.
    ///
    /// АРХИТЕКТУРА: CombatAIService НЕ инжектит IBodyService / IInventoryService.
    /// Взаимодействие через ICombatService.ExecuteAttack/ExecuteDefense.
    ///
    /// CMB-C02: Aggressiveness теперь влияет на выбор действия.
    /// Высокая агрессивность → шанс атаки увеличивается, защиты уменьшается.
    ///
    /// IMPL-6 (Q5): все броски рандома идут через инжектированный
    /// <see cref="ICombatRng"/> — бои воспроизводимы при том же seed.
    /// </summary>
    public class CombatAIService
    {
        // === Зависимости ===
        private readonly ICombatService _combatService;
        private readonly ICombatRng _rng; // Q5: deterministic RNG

        // === Состояние ===
        private AIPersonality _personality;
        private float _actionTimer;
        private string _entityId;
        private bool _isActive;

        // === Конструктор ===
        public CombatAIService(ICombatService combatService, ICombatRng rng)
        {
            _combatService = combatService;
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        /// <summary>
        /// Инициализировать AI с личностью и идентификатором сущности.
        /// </summary>
        public void Initialize(string entityId, AIPersonality personality)
        {
            _entityId = entityId ?? "enemy";
            _personality = personality ?? AIPersonality.CreateBalanced();
            _actionTimer = 0f;
            _isActive = true;
        }

        /// <summary>
        /// Обновить AI за один кадр.
        /// Вызывается из CombatModule.Tick() только в EnemyTurn.
        /// CMB-C02: Aggressiveness влияет на вероятность атаки vs защиты.
        /// </summary>
        public AIAction UpdateAI(float deltaTime, float hpRatio)
        {
            if (!_isActive || _personality == null) return AIAction.None;

            _actionTimer += deltaTime;

            // Проверка: пора ли действовать?
            if (_actionTimer < _personality.ActionDelay) return AIAction.Wait;

            _actionTimer = 0f;

            // Проверка: нужно ли сбежать?
            if (hpRatio <= _personality.FleeThreshold)
            {
                return AIAction.Flee;
            }

            // CMB-C02: Выбор действия на основе личности
            // Aggressiveness определяет базовый шанс атаки
            // Остаток делится между защитой и техниками
            // Q5: ICombatRng.NextFloat вместо (float)Random.Shared.NextDouble.
            float roll = _rng.NextFloat();

            // Агрессивность масштабирует шанс атаки
            float attackChance = _personality.Aggressiveness;
            float defenseChance = _personality.DefenseTendency * (1f - _personality.Aggressiveness);
            float techniqueChance = _personality.TechniquePreference * (1f - _personality.Aggressiveness);

            // Нормализация: сумма может не равняться 1.0
            float total = attackChance + defenseChance + techniqueChance;
            if (total > 0f)
            {
                attackChance /= total;
                defenseChance /= total;
                techniqueChance /= total;
            }

            // Защита
            if (roll < defenseChance)
            {
                return AIAction.Defend;
            }

            // Техника
            if (roll < defenseChance + techniqueChance)
            {
                return AIAction.UseTechnique;
            }

            // Базовая атака (агрессивный выбор)
            return AIAction.Attack;
        }

        /// <summary>
        /// Выбрать подтип защиты.
        /// </summary>
        public DefenseSubtype ChooseDefense()
        {
            if (_personality == null) return DefenseSubtype.Block;

            // Q5: ICombatRng.NextFloat вместо (float)Random.Shared.NextDouble.
            float roll = _rng.NextFloat();
            if (roll < 0.3f) return DefenseSubtype.Dodge;
            if (roll < 0.6f) return DefenseSubtype.Block;
            if (roll < 0.8f) return DefenseSubtype.Parry;
            return DefenseSubtype.Shield;
        }

        /// <summary>
        /// Деактивировать AI (конец боя).
        /// </summary>
        public void Deactivate()
        {
            _isActive = false;
            _actionTimer = 0f;
        }
    }

    /// <summary>
    /// Возможные действия AI.
    /// </summary>
    public enum AIAction
    {
        None,           // Нет действия
        Wait,           // Ожидание (кулдаун)
        Attack,         // Базовая атака
        UseTechnique,   // Использование техники
        Defend,         // Защита
        Flee            // Бегство
    }
}

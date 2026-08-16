#nullable enable
// Создано: 2026-05-09 15:30:00 UTC
// Редактировано: 2026-05-09 15:55:29 UTC — NPC-A07/NPC-C01: устранена GC-аллокация в ProcessThreatDecay
// Редактировано: 2026-05-09 15:55:29 UTC — NPC-B04: SetAIState проверяет IsAlive
// Упрощённый Behaviour Tree для AI NPC.
// Обрабатывает решения: Idle, Wandering, Patrolling, Fleeing, Attacking, Following.
// EVT-01: Подписки на DamageAppliedEvent, BodyPartSeveredEvent, PlayerPositionChangedEvent.
// Hub-and-Spoke: НЕ инжектит сервисы других модулей.
using System;
using Vector2 = CultivationGame.Core.Data.Position2D;
using System.Collections.Generic;
using CultivationGame.Core;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Modules.NPC.Data;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.NPC
{
    /// <summary>
    /// Сервис AI NPC — упрощённый Behaviour Tree.
    /// Каждый тик оценивает ситуацию и решает следующее состояние для каждого NPC.
    ///
    /// Факторы решения:
    /// - Текущее AIState + StateTimer (таймаут состояния)
    /// - Уровень угроз (Threats — затухают со временем)
    /// - Доля здоровья (HealthRatio)
    /// - Черты личности PersonalityTrait (влияют на веса)
    /// - Близость игрока
    ///
    /// АРХИТЕКТУРА (EVT-01): Подписки на кросс-модульные события:
    /// - DamageAppliedEvent → добавление угрозы, потенциально Flee
    /// - BodyPartSeveredEvent → принудительное бегство
    /// - PlayerPositionChangedEvent → обновление осведомлённости
    /// </summary>
    public class NPCAIService : IDisposable
    {
        // === Зависимости ===
        private readonly NPCService _npcService;
        private readonly NPCConfig _config;
        private readonly ITimeService _timeService;

        // === MessagePipe: подписки ===
        private readonly ISubscriber<DamageAppliedEvent> _damageAppliedSub;
        private readonly ISubscriber<BodyPartSeveredEvent> _bodyPartSeveredSub;
        private readonly ISubscriber<PlayerPositionChangedEvent> _playerPosChangedSub;
        private IDisposable _damageAppliedSubscription;
        private IDisposable _bodyPartSeveredSubscription;
        private IDisposable _playerPosChangedSubscription;

        // === Состояние ===
        private Vector2 _playerPosition;

        // NPC-A07/NPC-C01: переиспользуемый буфер для затухания угроз (устранение GC-аллокации)
        private readonly List<string> _threatKeysBuffer = new List<string>();

        // === Конструктор (VContainer) ===
        public NPCAIService(
            NPCService npcService,
            NPCConfig config,
            ITimeService timeService,
            ISubscriber<DamageAppliedEvent> damageAppliedSub,
            ISubscriber<BodyPartSeveredEvent> bodyPartSeveredSub,
            ISubscriber<PlayerPositionChangedEvent> playerPosChangedSub)
        {
            _npcService = npcService;
            _config = config;
            _timeService = timeService;
            _damageAppliedSub = damageAppliedSub;
            _bodyPartSeveredSub = bodyPartSeveredSub;
            _playerPosChangedSub = playerPosChangedSub;
        }

        /// <summary>
        /// Инициализация: подписки на кросс-модульные события.
        /// </summary>
        public void Initialize()
        {
            _damageAppliedSubscription = _damageAppliedSub.Subscribe(OnDamageApplied);
            _bodyPartSeveredSubscription = _bodyPartSeveredSub.Subscribe(OnBodyPartSevered);
            _playerPosChangedSubscription = _playerPosChangedSub.Subscribe(OnPlayerPositionChanged);
        }

        /// <summary>
        /// Тик AI — обработать всех живых NPC.
        /// Вызывается из NPCModule.Tick().
        /// BD-42: Использует ITimeService.DeltaTime.
        /// </summary>
        public void Tick()
        {
            // BD-42: deltaTime через ITimeService
            float deltaTime = _timeService.DeltaTime;

            foreach (var state in _npcService.GetAllStates())
            {
                if (!state.IsAlive) continue;

                // Обновляем таймер состояния
                state.StateTimer += deltaTime;

                // Затухание угроз
                ProcessThreatDecay(state, deltaTime);

                // Оценка и принятие решения
                EvaluateAndDecide(state);
            }
        }

        // === Принятие решений ===

        /// <summary>
        /// Оценить ситуацию и принять решение о следующем AI-состоянии.
        /// Упрощённый Behaviour Tree с весами на основе PersonalityTrait.
        /// </summary>
        private void EvaluateAndDecide(NPCState state)
        {
            // В бою — не меняем AIState (управляет CombatAdapter)
            if (state.IsInCombat) return;

            // Проверка: нужно ли сбежать (мало здоровья)
            float healthRatio = state.MaxHealth > 0
                ? (float)state.CurrentHealth / state.MaxHealth
                : 0f;

            if (healthRatio <= _config.FleeHealthRatio && state.AIState != NPCAIState.Fleeing)
            {
                _npcService.SetAIState(state.NpcId, NPCAIState.Fleeing);
                return;
            }

            // Проверка: высокая угроза → атака
            float maxThreat = GetMaxThreat(state);
            if (maxThreat >= _config.ThreatThreshold && state.AIState != NPCAIState.Attacking)
            {
                _npcService.SetAIState(state.NpcId, NPCAIState.Attacking);
                return;
            }

            // Проверка: таймаут текущего состояния
            float stateTimeout = GetStateTimeout(state.AIState);
            if (state.StateTimer < stateTimeout) return;

            // Выбор следующего состояния на основе личности и ситуации
            NPCAIState nextState = DecideNextState(state, healthRatio);
            _npcService.SetAIState(state.NpcId, nextState);
        }

        /// <summary>
        /// Взвешенный случайный выбор следующего состояния.
        /// Веса зависят от PersonalityTrait и текущей ситуации.
        /// </summary>
        private NPCAIState DecideNextState(NPCState state, float healthRatio)
        {
            // Базовые веса для каждого состояния
            float idleWeight = 1f;
            float wanderWeight = 2f;
            float patrolWeight = 1f;
            float fleeWeight = 0f;
            float attackWeight = 0f;
            float followWeight = 0f;

            // Модификаторы от личности
            if ((state.Personality & PersonalityTrait.Aggressive) != 0)
            {
                attackWeight += 3f;
                wanderWeight += 1f;
            }

            if ((state.Personality & PersonalityTrait.Cautious) != 0)
            {
                fleeWeight += 2f;
                idleWeight += 1f;
                attackWeight -= 1f;
            }

            if ((state.Personality & PersonalityTrait.Pacifist) != 0)
            {
                attackWeight -= 2f;
                fleeWeight += 1f;
                idleWeight += 1f;
            }

            if ((state.Personality & PersonalityTrait.Curious) != 0)
            {
                wanderWeight += 2f;
                followWeight += 1f;
            }

            if ((state.Personality & PersonalityTrait.Ambitious) != 0)
            {
                attackWeight += 1f;
                patrolWeight += 1f;
            }

            if ((state.Personality & PersonalityTrait.Vengeful) != 0)
            {
                // Мстительность увеличивает атаку при наличии угроз
                float totalThreat = GetTotalThreat(state);
                attackWeight += totalThreat * 0.05f;
            }

            // Модификаторы от ситуации
            if (healthRatio < 0.5f) fleeWeight += 2f;
            if (healthRatio < 0.3f) fleeWeight += 3f;

            float playerDist = Vector2.Distance(state.Position, _playerPosition);
            if (playerDist < _config.AggroRadius)
            {
                // Игрок рядом — зависит от отношения
                if ((state.Personality & PersonalityTrait.Aggressive) != 0)
                    attackWeight += 2f;
                else if ((state.Personality & PersonalityTrait.Pacifist) != 0)
                    fleeWeight += 1f;
            }

            if (playerDist < _config.AggroRadius * 2f)
            {
                followWeight += 0.5f;
            }

            // Клэмп весов (не отрицательные)
            idleWeight = Math.Max(0f, idleWeight);
            wanderWeight = Math.Max(0f, wanderWeight);
            patrolWeight = Math.Max(0f, patrolWeight);
            fleeWeight = Math.Max(0f, fleeWeight);
            attackWeight = Math.Max(0f, attackWeight);
            followWeight = Math.Max(0f, followWeight);

            // Взвешенная случайная выборка
            float total = idleWeight + wanderWeight + patrolWeight + fleeWeight + attackWeight + followWeight;
            if (total <= 0f) return NPCAIState.Idle;

            float roll = (float)Random.Shared.NextDouble() * total;
            float cumulative = 0f;

            cumulative += idleWeight;
            if (roll < cumulative) return NPCAIState.Idle;

            cumulative += wanderWeight;
            if (roll < cumulative) return NPCAIState.Wandering;

            cumulative += patrolWeight;
            if (roll < cumulative) return NPCAIState.Patrolling;

            cumulative += fleeWeight;
            if (roll < cumulative) return NPCAIState.Fleeing;

            cumulative += attackWeight;
            if (roll < cumulative) return NPCAIState.Attacking;

            return NPCAIState.Following;
        }

        // === Обработка угроз ===

        /// <summary>
        /// Затухание угроз со временем.
        /// Уменьшает уровень каждой угрозы на ThreatDecayRate * deltaTime.
        /// </summary>
        private void ProcessThreatDecay(NPCState state, float deltaTime)
        {
            // NPC-A07/NPC-C01: переиспользуем буфер вместо аллокации нового List каждый тик
            // ФИКС: сначала собираем snapshot ключей, потом модифицируем словарь.
            // Раньше state.Threats[kvp.Key] = newThreat модифицировал словарь
            // во время foreach — InvalidOperationException.
            _threatKeysBuffer.Clear();

            // Шаг 1: snapshot ключей (без модификации словаря)
            foreach (var kvp in state.Threats)
                _threatKeysBuffer.Add(kvp.Key);

            // Шаг 2: безопасная модификация по snapshot
            for (int i = 0; i < _threatKeysBuffer.Count; i++)
            {
                string key = _threatKeysBuffer[i];
                float currentThreat = state.Threats[key];
                float newThreat = currentThreat - _config.ThreatDecayRate * deltaTime;
                if (newThreat <= 0f)
                    state.Threats.Remove(key);
                else
                    state.Threats[key] = newThreat;
            }
        }

        /// <summary>
        /// Получить максимальный уровень угрозы.
        /// </summary>
        private float GetMaxThreat(NPCState state)
        {
            float maxThreat = 0f;
            foreach (var kvp in state.Threats)
            {
                if (kvp.Value > maxThreat) maxThreat = kvp.Value;
            }
            return maxThreat;
        }

        /// <summary>
        /// Получить суммарный уровень угроз.
        /// </summary>
        private float GetTotalThreat(NPCState state)
        {
            float total = 0f;
            foreach (var kvp in state.Threats)
                total += kvp.Value;
            return total;
        }

        /// <summary>
        /// Таймаут для AI-состояния (сколько NPC остаётся в нём).
        /// </summary>
        private float GetStateTimeout(NPCAIState state)
        {
            switch (state)
            {
                case NPCAIState.Idle: return 3f;
                case NPCAIState.Wandering: return 5f;
                case NPCAIState.Patrolling: return 8f;
                case NPCAIState.Fleeing: return 4f;
                case NPCAIState.Attacking: return 2f;
                case NPCAIState.Following: return 6f;
                case NPCAIState.Talking: return 10f;
                case NPCAIState.Trading: return 10f;
                case NPCAIState.Resting: return 15f;
                default: return 3f;
            }
        }

        // === Обработчики кросс-модульных событий ===

        /// <summary>
        /// Обработчик DamageAppliedEvent — добавить угрозу, потенциально Flee.
        /// </summary>
        private void OnDamageApplied(in DamageAppliedEvent e)
        {
            var state = _npcService.GetNPCState(e.TargetId);
            if (state == null || !state.IsAlive) return;

            // Добавляем угрозу от источника урона
            float threatLevel = e.Damage * 2f; // Урон конвертируется в угрозу
            if (state.Threats.ContainsKey(e.SourceId))
                state.Threats[e.SourceId] += threatLevel;
            else
                state.Threats[e.SourceId] = threatLevel;

            // Обновляем целевой идентификатор
            state.TargetId = e.SourceId;
        }

        /// <summary>
        /// Обработчик BodyPartSeveredEvent — принудительное бегство.
        /// </summary>
        private void OnBodyPartSevered(in BodyPartSeveredEvent e)
        {
            var state = _npcService.GetNPCState(e.EntityId);
            if (state == null || !state.IsAlive) return;

            // Отрубленная часть — максимальная угроза, принудительное бегство
            state.Threats["sever_unknown"] = 100f;
            _npcService.SetAIState(state.NpcId, NPCAIState.Fleeing);
        }

        /// <summary>
        /// Обработчик PlayerPositionChangedEvent — обновление позиции игрока.
        /// </summary>
        private void OnPlayerPositionChanged(in PlayerPositionChangedEvent e)
        {
            _playerPosition = new Vector2((int)e.X, (int)e.Y);
        }

        public void Dispose()
        {
            _damageAppliedSubscription?.Dispose();
            _damageAppliedSubscription = null;
            _bodyPartSeveredSubscription?.Dispose();
            _bodyPartSeveredSubscription = null;
            _playerPosChangedSubscription?.Dispose();
            _playerPosChangedSubscription = null;
        }
    }
}

#nullable enable
// Создано: 2026-05-09 15:30:00 UTC
// Редактировано: 2026-05-09 15:55:29 UTC — NPC-B05: кэш позиции игрока через PlayerPositionChangedEvent
//   для корректной работы Attacking/Following/Fleeing когда цель — игрок
// Упрощённая система движения NPC.
// Обрабатывает перемещение на основе AIState.
// BD-42: Использует ITimeService.DeltaTime вместо UnityEngine.Time.deltaTime.
using System;
using Vector2 = CultivationGame.Core.Data.Position2D;
using CultivationGame.Core;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Modules.NPC.Data;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.NPC
{
    /// <summary>
    /// Сервис движения NPC — упрощённая навигация.
    /// Обрабатывает перемещение на основе текущего AIState:
    /// - Idle: без движения
    /// - Wandering: случайное блуждание в радиусе патруля
    /// - Patrolling: движение между точками патруля
    /// - Fleeing: движение от цели
    /// - Attacking: движение к цели
    /// - Following: движение к цели с поддержкой дистанции
    ///
    /// BD-42: Использует ITimeService.DeltaTime.
    /// NPC-B05: Кэширует позицию игрока через PlayerPositionChangedEvent
    /// для движения к/от игрока (TargetId может быть не-NPC).
    /// </summary>
    public class NPCMovementService : IDisposable
    {
        // === Зависимости ===
        private readonly NPCService _npcService;
        private readonly NPCConfig _config;
        private readonly ITimeService _timeService;

        // === MessagePipe: подписки ===
        private readonly ISubscriber<PlayerPositionChangedEvent> _playerPosChangedSub;
        private IDisposable _playerPosChangedSubscription;

        // === Внутреннее состояние ===
        // Точка спавна для каждого NPC (центр патруля)
        private readonly System.Collections.Generic.Dictionary<string, Vector2> _spawnPositions
            = new System.Collections.Generic.Dictionary<string, Vector2>();

        // Цель странствия для каждого NPC
        private readonly System.Collections.Generic.Dictionary<string, Vector2> _wanderTargets
            = new System.Collections.Generic.Dictionary<string, Vector2>();

        // NPC-B05: кэш позиции игрока для движения к/от игрока
        private Vector2 _playerPosition;

        // === Конструктор (VContainer) ===
        public NPCMovementService(
            NPCService npcService,
            NPCConfig config,
            ITimeService timeService,
            ISubscriber<PlayerPositionChangedEvent> playerPosChangedSub)
        {
            _npcService = npcService;
            _config = config;
            _timeService = timeService;
            _playerPosChangedSub = playerPosChangedSub;
        }

        /// <summary>
        /// Инициализация: подписка на PlayerPositionChangedEvent.
        /// NPC-B05: кэшируем позицию игрока для Attacking/Following/Fleeing.
        /// Вызывается из NPCModule.Start().
        /// </summary>
        public void Initialize()
        {
            _playerPosChangedSubscription = _playerPosChangedSub.Subscribe(OnPlayerPositionChanged);
        }

        private void OnPlayerPositionChanged(in PlayerPositionChangedEvent e)
        {
            _playerPosition = new Vector2((int)e.X, (int)e.Y);
        }

        // === Публичный API ===

        /// <summary>
        /// Зарегистрировать точку спавна NPC (центр зоны патруля).
        /// Вызывается при спавне NPC.
        /// </summary>
        public void RegisterSpawnPosition(string npcId, Vector2 position)
        {
            _spawnPositions[npcId] = position;
        }

        /// <summary>
        /// Удалить точку спавна NPC. Вызывается при деспавне.
        /// </summary>
        public void UnregisterSpawnPosition(string npcId)
        {
            _spawnPositions.Remove(npcId);
            _wanderTargets.Remove(npcId);
        }

        /// <summary>
        /// Обработать движение всех NPC за один кадр.
        /// Вызывается из NPCModule.Tick().
        /// BD-42: Использует ITimeService.DeltaTime.
        /// </summary>
        public void ProcessMovement()
        {
            // BD-42: deltaTime через ITimeService
            float deltaTime = _timeService.DeltaTime;

            foreach (var state in _npcService.GetAllStates())
            {
                if (!state.IsAlive) continue;

                ProcessNPCMovement(state, deltaTime);
            }
        }

        // === Обработка движения по AIState ===

        /// <summary>
        /// Обработать движение одного NPC на основе AIState.
        /// </summary>
        private void ProcessNPCMovement(NPCState state, float deltaTime)
        {
            switch (state.AIState)
            {
                case NPCAIState.Idle:
                    // Без движения
                    break;

                case NPCAIState.Wandering:
                    ProcessWandering(state, deltaTime);
                    break;

                case NPCAIState.Patrolling:
                    ProcessPatrolling(state, deltaTime);
                    break;

                case NPCAIState.Fleeing:
                    ProcessFleeing(state, deltaTime);
                    break;

                case NPCAIState.Attacking:
                    ProcessAttacking(state, deltaTime);
                    break;

                case NPCAIState.Following:
                    ProcessFollowing(state, deltaTime);
                    break;

                case NPCAIState.Talking:
                case NPCAIState.Trading:
                case NPCAIState.Meditating:
                case NPCAIState.Cultivating:
                case NPCAIState.Working:
                case NPCAIState.Searching:
                case NPCAIState.Guarding:
                case NPCAIState.Defending:
                    // Без движения (NPC занят)
                    break;

                case NPCAIState.Resting:
                    // Без движения (NPC отдыхает)
                    break;
            }
        }

        /// <summary>
        /// Случайное блуждание в радиусе патруля от точки спавна.
        /// </summary>
        private void ProcessWandering(NPCState state, float deltaTime)
        {
            // Получаем или генерируем цель блуждания
            if (!_wanderTargets.TryGetValue(state.NpcId, out var target)
                || Vector2.Distance(state.Position, target) < 0.5f)
            {
                // Генерация новой случайной цели в радиусе патруля
                target = GenerateRandomPointInRadius(state.NpcId, _config.PatrolRadius);
                _wanderTargets[state.NpcId] = target;
            }

            // Движение к цели
            MoveToward(state, target, _config.DefaultMoveSpeed, deltaTime);
        }

        /// <summary>
        /// Патрулирование — движение между точками патруля.
        /// Упрощённая версия: движение по квадрату вокруг точки спавна.
        /// </summary>
        private void ProcessPatrolling(NPCState state, float deltaTime)
        {
            // Упрощённое патрулирование: движение к случайной точке в радиусе
            if (!_wanderTargets.TryGetValue(state.NpcId, out var target)
                || Vector2.Distance(state.Position, target) < 0.5f)
            {
                target = GenerateRandomPointInRadius(state.NpcId, _config.PatrolRadius);
                _wanderTargets[state.NpcId] = target;
            }

            MoveToward(state, target, _config.DefaultMoveSpeed, deltaTime);
        }

        /// <summary>
        /// Бегство — движение от цели (цель в Threats или TargetId).
        /// Скорость увеличена множителем FleeSpeedMultiplier.
        /// </summary>
        private void ProcessFleeing(NPCState state, float deltaTime)
        {
            Vector2 pos = state.Position;
            Vector2 fleeFrom = GetThreatPosition(state);
            if (fleeFrom == Vector2.zero) fleeFrom = pos; // Нет угрозы — стоим

            // Направление от угрозы
            Vector2 direction = (pos - fleeFrom).normalized;
            if (direction == Vector2.zero) direction = RandomDirection();

            Vector2 fleeTarget = pos + direction * _config.FleeRadius;
            float fleeSpeed = _config.DefaultMoveSpeed * _config.FleeSpeedMultiplier;

            MoveToward(state, fleeTarget, fleeSpeed, deltaTime);
        }

        /// <summary>
        /// Атака — движение к цели (TargetId).
        /// NPC-B05: Цель может быть игроком (не NPC) — используем кэш позиции.
        /// Остановка при достижении AttackRadius.
        /// </summary>
        private void ProcessAttacking(NPCState state, float deltaTime)
        {
            if (string.IsNullOrEmpty(state.TargetId)) return;

            // NPC-B05: пробуем получить позицию цели — сначала как NPC, затем как игрока
            Vector2 targetPos = GetTargetPosition(state.TargetId);
            if (targetPos == Vector2.zero && _playerPosition == Vector2.zero) return;

            float distance = Vector2.Distance(state.Position, targetPos);

            // Уже в радиусе атаки — не двигаемся
            if (distance <= _config.AttackRadius) return;

            // Движение к цели
            MoveToward(state, targetPos, _config.DefaultMoveSpeed * 1.2f, deltaTime);
        }

        /// <summary>
        /// Следование — движение к цели с поддержкой дистанции.
        /// NPC-B05: Цель может быть игроком (не NPC) — используем кэш позиции.
        /// </summary>
        private void ProcessFollowing(NPCState state, float deltaTime)
        {
            if (string.IsNullOrEmpty(state.TargetId)) return;

            // NPC-B05: пробуем получить позицию цели — сначала как NPC, затем как игрока
            Vector2 targetPos = GetTargetPosition(state.TargetId);
            if (targetPos == Vector2.zero && _playerPosition == Vector2.zero) return;

            float distance = Vector2.Distance(state.Position, targetPos);
            float followDistance = 2f; // Дистанция следования

            // Уже на нужной дистанции — не двигаемся
            if (distance <= followDistance) return;

            // Движение к цели
            MoveToward(state, targetPos, _config.DefaultMoveSpeed, deltaTime);
        }

        // === Утилиты движения ===

        /// <summary>
        /// Получить позицию цели по ID.
        /// NPC-B05: Сначала пробуем как NPC, затем используем кэш позиции игрока.
        /// </summary>
        private Vector2 GetTargetPosition(string targetId)
        {
            var targetState = _npcService.GetNPCState(targetId);
            if (targetState != null)
                return targetState.Position;

            // Цель — не NPC (вероятно, игрок), используем кэш
            return _playerPosition;
        }

        /// <summary>
        /// Движение NPC к целевой точке с заданной скоростью.
        /// Обновляет Position в NPCState и через NPCService.
        /// </summary>
        private void MoveToward(NPCState state, Vector2 target, float speed, float deltaTime)
        {
            Vector2 pos = state.Position;
            Vector2 direction = (target - pos).normalized;
            float step = speed * deltaTime;

            // Ограничиваем шаг до расстояния до цели
            float distance = Vector2.Distance(pos, target);
            if (step > distance) step = distance;

            state.Position = pos + direction * step;
            _npcService.UpdatePosition(state.NpcId, state.Position);
        }

        /// <summary>
        /// Генерация случайной точки в радиусе от точки спавна.
        /// </summary>
        private Vector2 GenerateRandomPointInRadius(string npcId, float radius)
        {
            Vector2 center = _spawnPositions.TryGetValue(npcId, out var spawn)
                ? spawn
                : Vector2.zero;

            float angle = (float)(Random.Shared.NextDouble() * 360.0) * ((float)Math.PI / 180f);
            float dist = (float)(Random.Shared.NextDouble() * (radius - 1.0) + 1.0);
            return center + new Vector2((int)(MathF.Cos(angle) * dist), (int)(MathF.Sin(angle) * dist));
        }

        /// <summary>
        /// Получить позицию максимальной угрозы для NPC.
        /// NPC-B05: учитываем кэш позиции игрока.
        /// </summary>
        private Vector2 GetThreatPosition(NPCState state)
        {
            // Ищем источник максимальной угрозы
            string maxThreatSource = null;
            float maxThreat = 0f;

            foreach (var kvp in state.Threats)
            {
                if (kvp.Value > maxThreat)
                {
                    maxThreat = kvp.Value;
                    maxThreatSource = kvp.Key;
                }
            }

            if (maxThreatSource == null) return Vector2.zero;

            // Пытаемся получить позицию источника угрозы
            return GetTargetPosition(maxThreatSource);
        }

        /// <summary>
        /// Случайное направление (для бегства без чёткой цели).
        /// </summary>
        private Vector2 RandomDirection()
        {
            float angle = (float)(Random.Shared.NextDouble() * 360.0) * ((float)Math.PI / 180f);
            return new Vector2((int)MathF.Cos(angle), (int)MathF.Sin(angle));
        }

        public void Dispose()
        {
            _playerPosChangedSubscription?.Dispose();
            _playerPosChangedSubscription = null;
            _spawnPositions.Clear();
            _wanderTargets.Clear();
        }
    }
}

#nullable enable
// Создано: 2026-05-09 15:30:00 UTC
// Редактировано: 2026-05-09 15:55:29 UTC — NPC-B05: кэш позиции игрока через PlayerPositionChangedEvent
//   для корректной работы Attacking/Following/Fleeing когда цель — игрок
// Упрощённая система движения NPC.
// Обрабатывает перемещение на основе AIState.
// BD-42: Использует ITimeService.DeltaTime вместо UnityEngine.Time.deltaTime.
using System;
using System.Collections.Generic;
using Vector2 = CultivationGame.Core.Data.Position2D;
using CultivationGame.Core;
using CultivationGame.Core.Messaging.Contracts;
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
    /// - Attacking: движение к цели (R16: дальнобойные — kiting, см. ниже)
    /// - Following: движение к цели с поддержкой дистанции
    ///
    /// BD-42: Использует ITimeService.DeltaTime.
    /// NPC-B05: Кэширует позицию игрока через PlayerPositionChangedEvent
    /// для движения к/от игрока (TargetId может быть не-NPC).
    ///
    /// R16 (2026-09-10): KITTING ДЛЯ ДАЛЬНОБОЙНЫХ NPC — санкционированная
    /// инъекция IEquipmentDataProvider (паттерн NPCModule.ProcessNpcAttacks/
    /// NPCSpriteRenderer: чтение оружия NPC без событий). Лучник не лезет
    /// в melee: dist < KiteMinRange → отход, dist ≤ AttackRange оружия →
    /// стоит и стреляет, дальше — сближается (раньше ВСЕГДА сближался до 1.5,
    /// дальнее оружие не имело смысла в ИИ).
    /// </summary>
    public class NPCMovementService : IDisposable
    {
        // === Зависимости ===
        private readonly NPCService _npcService;
        private readonly NPCConfig _config;
        private readonly ITimeService _timeService;
        // GROUP-SPAWN: групповой сервис — overlay над индивидуальным AI.
        // Если NPC состоит в активной группе с CurrentGroupTarget, движение
        // к этой цели имеет приоритет над wander/patrol/attack индивидуального AI.
        private readonly INPCGroupService? _groupService;
        // R16: экипировка NPC — дальность оружия для кайтинга (санкционированная
        // инъекция чтения, тот же паттерн что NPCModule._equipmentDataProvider).
        private readonly IEquipmentDataProvider? _equipmentProvider;
        // ST13-001 (Этап-13, NPC_AI_SYSTEM §6): тайлы для проходимости и A*.
        // Паттерн — тот же санкционированный что у AnimalService (_tileService).
        private readonly ITileService? _tileService;
        private readonly NPCPathfinder? _pathfinder;

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
            ISubscriber<PlayerPositionChangedEvent> playerPosChangedSub,
            // GROUP-SPAWN: опциональная зависимость (null-tolerant для тестов).
            // DI регистрирует INPCGroupService как singleton в NPCModuleServices.
            INPCGroupService? groupService = null,
            // R16: экипировка для кайтинга лучников (опционально — тот же
            // null-tolerant паттерн, DI подставит зарегистрированный провайдер).
            IEquipmentDataProvider? equipmentProvider = null,
            // ST13-001: тайлы мира — A* pathfinding + проходимость.
            ITileService? tileService = null)
        {
            _npcService = npcService;
            _config = config;
            _timeService = timeService;
            _playerPosChangedSub = playerPosChangedSub;
            _groupService = groupService;
            _equipmentProvider = equipmentProvider;
            _tileService = tileService;
            _pathfinder = tileService != null ? new NPCPathfinder(tileService) : null;
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
            _pathStates.Remove(npcId);
        }

        /// <summary>
        /// Обработать движение всех NPC за один кадр.
        /// Вызывается из NPCModule.Tick().
        /// BD-42: Использует ITimeService.DeltaTime.
        /// P1-3 (аудит 09.22): zero-GC — персистентный буфер снапшота
        /// (CopyStatesTo) вместо аллоцирующего GetAllStates().
        /// </summary>
        private readonly List<NPCState> _moveStates = new();

        public void ProcessMovement()
        {
            // BD-42: deltaTime через ITimeService
            float deltaTime = _timeService.DeltaTime;

            _npcService.CopyStatesTo(_moveStates);
            for (int i = 0; i < _moveStates.Count; i++)
            {
                var state = _moveStates[i];
                if (!state.IsAlive) continue;

                ProcessNPCMovement(state, deltaTime);
            }
        }

        // === Обработка движения по AIState ===

        /// <summary>
        /// Обработать движение одного NPC на основе AIState.
        /// ST13-002 (Этап-13, GROUP_SYSTEM §6 — канон приоритетов):
        ///   1. Бой (индивидуальный): Attacking/Fleeing — групповая задача
        ///      НЕ подавляет боевые состояния (прежде overlay выполнялся до
        ///      switch: участник патруля в бою продолжал идти к waypoint,
        ///      игнорируя преследование/бегство).
        ///   2. Групповая задача: если NPC в группе (не в бою) и группа
        ///      активна с CurrentGroupTarget — движение к ней.
        ///   3. Индивидуальное поведение: wander/patrol/прочее.
        /// После завершения боя (AIState → не-боевое) следующий же тик
        /// возвращает NPC к групповой задаче (§6: «при завершении боя NPC
        /// возвращается к групповой задаче»).
        /// GROUP-SPAWN overlay — LIGHT: followers пока без formation offsets.
        /// </summary>
        private void ProcessNPCMovement(NPCState state, float deltaTime)
        {
            // === ST13-002: групповой overlay — ТОЛЬКО вне боевых состояний ===
            bool combatMotion = state.AIState is NPCAIState.Attacking or NPCAIState.Fleeing;
            if (!combatMotion && _groupService != null)
            {
                var group = _groupService.GetGroupByMember(state.NpcId);
                if (group != null && group.IsActive && group.CurrentGroupTarget.HasValue)
                {
                    MoveTowardSmart(state, group.CurrentGroupTarget.Value, _config.DefaultMoveSpeed, deltaTime);
                    return; // Skip individual AI logic for this tick.
                }
            }

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

            // Движение к цели (ST13-001: через Smart — обход препятствий)
            MoveTowardSmart(state, target, _config.DefaultMoveSpeed, deltaTime);
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

            MoveTowardSmart(state, target, _config.DefaultMoveSpeed, deltaTime);
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

            MoveTowardSmart(state, fleeTarget, fleeSpeed, deltaTime);
        }

        /// <summary>
        /// R16: минимальная дистанция кайтинга — ближе лучник отходит
        /// (melee-зона: 3 тайла — вне досягаемости ближнего удара).
        /// </summary>
        private const float KiteMinRange = 3f;

        /// <summary>
        /// Атака — движение к цели (TargetId).
        /// NPC-B05: Цель может быть игроком (не NPC) — используем кэш позиции.
        /// R16: KITING — дальнобойное оружие (AttackRange > 2, фаза 9A)
        /// держит дистанцию: отход от melee-зоны, стрельба с дистанции оружия.
        /// Ближнее оружие — как раньше: сближение до AttackRadius.
        /// </summary>
        private void ProcessAttacking(NPCState state, float deltaTime)
        {
            if (string.IsNullOrEmpty(state.TargetId)) return;

            // NPC-B05: пробуем получить позицию цели — сначала как NPC, затем как игрока
            Vector2 targetPos = GetTargetPosition(state.TargetId, state.Position);
            if (targetPos == Vector2.zero && _playerPosition == Vector2.zero) return;

            float distance = Vector2.Distance(state.Position, targetPos);

            // R16: дальнобойный NPC — kiting (см. комментарий класса).
            var weapon = _equipmentProvider?.GetEquipped(state.NpcId, EquipmentSlot.WeaponMain);
            if (weapon != null && weapon.AttackRange > 2)
            {
                if (distance < KiteMinRange)
                {
                    // Слишком близко — отход от цели (стрелять в упор нельзя,
                    // melee-противник бьёт).
                    Vector2 pos = state.Position;
                    Vector2 dir = (pos - targetPos).normalized;
                    if (dir == Vector2.zero) dir = RandomDirection();
                    Vector2 retreatTarget = pos + dir * 2f;
                    MoveTowardSmart(state, retreatTarget, _config.DefaultMoveSpeed, deltaTime);
                    return;
                }
                if (distance <= weapon.AttackRange) return; // в зоне обстрела — стоим
                // Дальше дальности оружия — сближаемся (в зону обстрела).
                // R20 (баг №2): ChaseSpeedMultiplier (1.6) вместо хардкода 1.2 —
                // погоня 4.8 тайла/с обгоняет игрока (2.8): «медленно убежать» не выйдет.
                MoveTowardSmart(state, targetPos, _config.DefaultMoveSpeed * _config.ChaseSpeedMultiplier, deltaTime);
                return;
            }

            // Ближний бой: уже в радиусе атаки — не двигаемся
            if (distance <= _config.AttackRadius) return;

            // Движение к цели (R20: ChaseSpeedMultiplier — см. выше).
            MoveTowardSmart(state, targetPos, _config.DefaultMoveSpeed * _config.ChaseSpeedMultiplier, deltaTime);
        }

        /// <summary>
        /// Следование — движение к цели с поддержкой дистанции.
        /// NPC-B05: Цель может быть игроком (не NPC) — используем кэш позиции.
        /// </summary>
        private void ProcessFollowing(NPCState state, float deltaTime)
        {
            if (string.IsNullOrEmpty(state.TargetId)) return;

            // NPC-B05: пробуем получить позицию цели — сначала как NPC, затем как игрока
            Vector2 targetPos = GetTargetPosition(state.TargetId, state.Position);
            if (targetPos == Vector2.zero && _playerPosition == Vector2.zero) return;

            float distance = Vector2.Distance(state.Position, targetPos);
            float followDistance = 2f; // Дистанция следования

            // Уже на нужной дистанции — не двигаемся
            if (distance <= followDistance) return;

            // Движение к цели
            MoveTowardSmart(state, targetPos, _config.DefaultMoveSpeed, deltaTime);
        }

        // === ST13-001: умное движение с учётом препятствий ===

        /// <summary>Кэш маршрута одного NPC (A*-вейпоинты).</summary>
        private sealed class PathState
        {
            internal readonly List<Vector2> Waypoints = new();
            internal int Index;
            internal Vector2 Goal;
            internal bool HasPath;
            internal bool NoPath;
            internal float RetryAt;
        }

        private readonly Dictionary<string, PathState> _pathStates = new();
        private float _pathClock;

        private PathState GetPathState(string npcId)
        {
            if (!_pathStates.TryGetValue(npcId, out var ps))
            {
                ps = new PathState();
                _pathStates[npcId] = ps;
            }
            return ps;
        }

        /// <summary>
        /// ST13-001 (NPC_AI_SYSTEM §6): движение к цели с учётом проходимости.
        /// Быстрый путь: прямая чиста (supercover-Bresenham) — прямое движение
        /// (наиболее частый случай, O(дистанции), без A*). Прямая перекрыта —
        /// A*-маршрут по вейпоинтам; цель изменилась / следующий вейпоинт стал
        /// непроходим (препятствие появилось) — re-path (§6.2). Пути нет —
        /// жадный шаг (диаг→гор→верт, паттерн AnimalService); всё заблокировано
        /// — стоим (нет ни телепорта, ни вечного цикла); периодическая
        ///retry-повторка ловит исчезновение препятствия.
        /// Без тайлов (тесты без мира) — прежнее прямое MoveToward.
        /// </summary>
        private void MoveTowardSmart(NPCState state, Vector2 target, float speed, float deltaTime)
        {
            if (_pathfinder == null || !_pathfinder.IsAvailable)
            {
                MoveToward(state, target, speed, deltaTime);
                return;
            }
            _pathClock += deltaTime;

            var tileNow = new Vector2((int)MathF.Round(state.Position.X), (int)MathF.Round(state.Position.Y));
            var tileGoal = new Vector2((int)MathF.Round(target.X), (int)MathF.Round(target.Y));

            // Быстрый путь: прямая чиста (или цель в том же тайле).
            if (tileNow == tileGoal || _pathfinder.LineWalkable(tileNow, tileGoal))
            {
                MoveToward(state, target, speed, deltaTime);
                return;
            }

            var ps = GetPathState(state.NpcId);

            // Re-plan: цели нет пути (retry-таймер) / цель сместилась /
            // следующий вейпоинт стал непроходим (препятствие появилось).
            bool needReplan = ps.NoPath
                ? _pathClock >= ps.RetryAt
                : !ps.HasPath
                    || ps.Goal != tileGoal
                    || (ps.Index < ps.Waypoints.Count
                        && !_pathfinder.IsWalkableTile(ps.Waypoints[ps.Index]));
            if (needReplan)
            {
                ps.Waypoints.Clear();
                ps.Index = 0;
                var path = _pathfinder.FindPath(tileNow, tileGoal);
                if (path == null)
                {
                    ps.NoPath = true;
                    ps.HasPath = false;
                    ps.RetryAt = _pathClock + 0.5f;
                }
                else
                {
                    ps.NoPath = false;
                    ps.HasPath = true;
                    ps.Goal = tileGoal;
                    for (int i = 0; i < path.Count; i++)
                        ps.Waypoints.Add(path[i]);
                }
            }

            if (ps.NoPath)
            {
                GreedyStep(state, target, speed, deltaTime);
                return;
            }

            // Следование по вейпоинтам (достижение — продвижение индекса).
            while (ps.Index < ps.Waypoints.Count
                && Vector2.Distance(state.Position, ps.Waypoints[ps.Index]) < 0.35f)
                ps.Index++;
            if (ps.Index >= ps.Waypoints.Count)
            {
                // Хвост маршрута пройден — финишный отрезок напрямую
                // (он же — следование за подвижной целью внутри последнего тайла).
                MoveToward(state, target, speed, deltaTime);
                return;
            }
            MoveToward(state, ps.Waypoints[ps.Index], speed, deltaTime);
        }

        /// <summary>
        /// Пути нет: жадный шаг в сторону цели (диагональ → горизонталь →
        /// вертикаль — паттерн AnimalService.StepTowardsTarget). Всё
        /// заблокировано — без движения (нет зависания/телепорта).
        /// </summary>
        private void GreedyStep(NPCState state, Vector2 target, float speed, float deltaTime)
        {
            if (_pathfinder == null) return;
            var here = new Vector2((int)MathF.Round(state.Position.X), (int)MathF.Round(state.Position.Y));
            var goal = new Vector2((int)MathF.Round(target.X), (int)MathF.Round(target.Y));
            int dx = Math.Sign(goal.X - here.X);
            int dy = Math.Sign(goal.Y - here.Y);
            Vector2? step = null;
            if (dx != 0 && dy != 0 && _pathfinder.IsWalkableTile(new Vector2(here.X + dx, here.Y + dy)))
                step = new Vector2(here.X + dx, here.Y + dy);
            else if (dx != 0 && _pathfinder.IsWalkableTile(new Vector2(here.X + dx, here.Y)))
                step = new Vector2(here.X + dx, here.Y);
            else if (dy != 0 && _pathfinder.IsWalkableTile(new Vector2(here.X, here.Y + dy)))
                step = new Vector2(here.X, here.Y + dy);
            if (step == null) return; // окружён — стоим
            MoveToward(state, step.Value, speed, deltaTime);
        }

        // === Утилиты движения ===

        /// <summary>
        /// Получить позицию цели по ID.
        /// NPC-B05 + AUDIT-0921 B4: цель-NPC (в т.ч. умершая/удалённая из
        /// реестра) — её позиция; ИГРОК — только по явному резолву
        /// PlayerIdResolver (кэш позиции). Прежний фоллбек «любой
        /// неизвестный ID = игрок» отправлял NPC с мёртвой/исчезнувшей
        /// NPC-целью преследовать ИГРОКА — поведение «призрачной погони».
        /// Неизвестный не-игрок → fallbackOwnPosition (стоим на месте).
        /// </summary>
        private Vector2 GetTargetPosition(string targetId, Vector2 fallbackOwnPosition)
        {
            var targetState = _npcService.GetNPCState(targetId);
            if (targetState != null)
                return targetState.Position;

            // Явная проверка: только резолвенный игрок → кэш позиции игрока
            if (Core.Helpers.PlayerIdResolver.IsPlayer(targetId))
                return _playerPosition;

            // Неизвестный не-игрок: честный «нет цели» — не выдумываем игрока
            return fallbackOwnPosition;
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
        /// ST13-001: цель блуждания обязана быть ПРОХОДИМЫМ тайлом (до 8
        /// попыток; не нашли — текущая позиция NPC — стоим, путь не строим).
        /// </summary>
        private Vector2 GenerateRandomPointInRadius(string npcId, float radius)
        {
            // R14-аудит (P2-2): якорь не зарегистрирован (NPC восстановлен из
            // сейва — RestoreState пишет позицию, но не якорь блуждания) →
            // блуждаем вокруг ТЕКУЩЕЙ позиции, а не (0,0): раньше всё
            // восстановленное население дрейфовало к углу карты.
            Vector2 center;
            if (_spawnPositions.TryGetValue(npcId, out var spawn))
            {
                center = spawn;
            }
            else
            {
                var restored = _npcService.GetNPCState(npcId);
                center = restored != null ? restored.Position : Vector2.zero;
            }

            // ST13-001: до 8 попыток найти проходимый тайл в радиусе.
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float angle = (float)(Random.Shared.NextDouble() * 360.0) * ((float)Math.PI / 180f);
                float dist = (float)(Random.Shared.NextDouble() * (radius - 1.0) + 1.0);
                var candidate = center + new Vector2((int)(MathF.Cos(angle) * dist), (int)(MathF.Sin(angle) * dist));
                if (_pathfinder == null || _pathfinder.IsWalkableTile(candidate))
                    return candidate;
            }
            var st = _npcService.GetNPCState(npcId);
            return st != null ? st.Position : center;
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
            // (AUDIT-0921 B4: неизвестный не-игрок → позиция самого NPC — стоим)
            return GetTargetPosition(maxThreatSource, state.Position);
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
            _pathStates.Clear();
        }
    }
}

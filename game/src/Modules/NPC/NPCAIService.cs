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
    /// - R16: CombatStartedEvent → ответ NPC на бой (Attacking/Fleeing)
    ///
    /// R16 (2026-09-10) «доработка боевой системы»: боевые переходы ДО гейта
    /// IsInCombat (раньше NPC в бою не менял состояние вообще):
    /// - месть: удар/начало боя → Attacking (или Fleeing по личности);
    /// - бегство: HP ≤ FleeHealthRatio работает и В БОЮ (NPC_AI_SYSTEM §4.2)
    ///   с публикацией CombatDisengageEvent (бой завершается стадией Flee);
    /// - leash: цель-игрок дальше AggroRadius×3 → выход из боя (aggro-drop).
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
        // R16: старт боя → месть участника-NPC.
        private readonly ISubscriber<CombatStartedEvent> _combatStartedSub;
        // R16: публикация выхода NPC из боя (бегство/leash).
        private readonly IPublisher<CombatDisengageEvent> _combatDisengagePub;
        private IDisposable _damageAppliedSubscription;
        private IDisposable _bodyPartSeveredSubscription;
        private IDisposable _playerPosChangedSubscription;
        private IDisposable _combatStartedSubscription;

        // === Состояние ===
        private Vector2 _playerPosition;

        // NPC-A07/NPC-C01: переиспользуемый буфер для затухания угроз (устранение GC-аллокации)
        private readonly List<string> _threatKeysBuffer = new List<string>();

        /// <summary>
        /// R16: радиус привязи (leash) — дистанция до цели-игрока, после которой
        /// NPC выходит из боя (aggro-drop). AggroRadius × 3 = 15 тайлов по умолчанию
        /// (5 агро + запас на преследование; убежать от NPC — реальная стратегия).
        /// </summary>
        private float LeashRadius => _config.AggroRadius * 3f;

        // === Конструктор (VContainer) ===
        public NPCAIService(
            NPCService npcService,
            NPCConfig config,
            ITimeService timeService,
            ISubscriber<DamageAppliedEvent> damageAppliedSub,
            ISubscriber<BodyPartSeveredEvent> bodyPartSeveredSub,
            ISubscriber<PlayerPositionChangedEvent> playerPosChangedSub,
            // R16: месть на старт боя + публикация disengage.
            ISubscriber<CombatStartedEvent> combatStartedSub,
            IPublisher<CombatDisengageEvent> combatDisengagePub)
        {
            _npcService = npcService;
            _config = config;
            _timeService = timeService;
            _damageAppliedSub = damageAppliedSub;
            _bodyPartSeveredSub = bodyPartSeveredSub;
            _playerPosChangedSub = playerPosChangedSub;
            _combatStartedSub = combatStartedSub;
            _combatDisengagePub = combatDisengagePub;
        }

        /// <summary>
        /// Инициализация: подписки на кросс-модульные события.
        /// </summary>
        public void Initialize()
        {
            _damageAppliedSubscription = _damageAppliedSub.Subscribe(OnDamageApplied);
            _bodyPartSeveredSubscription = _bodyPartSeveredSub.Subscribe(OnBodyPartSevered);
            _playerPosChangedSubscription = _playerPosChangedSub.Subscribe(OnPlayerPositionChanged);
            // R16: старт боя → участник-NPC отвечает (Attacking/Fleeing).
            _combatStartedSubscription = _combatStartedSub.Subscribe(OnCombatStartedForRetaliation);
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
        /// Player entity id (PlayerService._data.Id). StatProviderAdapter
        /// treats any non-NPC id as the player, so the exact value only
        /// matters for threat targeting.
        /// </summary>
        private const string PlayerId = "player_0";

        /// <summary>
        /// Диспозиционный ИИ (2026-08-22, физический прототип):
        /// - Hostile: игрок в AggroRadius → угроза выше порога (агро).
        /// - Friendly: враг (Hostile NPC) в бою с игроком в радиусе → угроза врагу.
        /// - Neutral/Merchant: ничего (мирные, блуждание/торговля).
        /// </summary>
        private void ProcessDisposition(NPCState state)
        {
            switch (state.Disposition)
            {
                case NPCDisposition.Hostile:
                {
                    float distToPlayer = Vector2.Distance(state.Position, _playerPosition);
                    if (distToPlayer <= _config.AggroRadius)
                    {
                        // Мгновенный агро: threat выше порога (50).
                        state.Threats[PlayerId] = System.MathF.Max(
                            state.Threats.TryGetValue(PlayerId, out var t) ? t : 0f,
                            _config.ThreatThreshold + 10f);
                    }
                    break;
                }

                case NPCDisposition.Friendly:
                {
                    // Защита игрока: ищем Hostile NPC в бою с игроком рядом с нами.
                    float distToPlayer = Vector2.Distance(state.Position, _playerPosition);
                    if (distToPlayer > _config.AggroRadius * 2f) break;

                    foreach (var other in _npcService.GetAllStates())
                    {
                        if (other.NpcId == state.NpcId || !other.IsAlive) continue;
                        if (other.Disposition != NPCDisposition.Hostile) continue;
                        if (other.TargetId != PlayerId && other.TargetId != "player") continue;

                        float dist = Vector2.Distance(state.Position, other.Position);
                        if (dist <= _config.AggroRadius * 2f)
                        {
                            state.Threats[other.NpcId] = _config.ThreatThreshold + 10f;
                            break; // одна цель за проход
                        }
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// AUDIT-0911 NPC-7 FIX: раньше возвращал любой ключ Threats с макс.
        /// значением — включая ФАНТОМНЫЕ («sever_unknown» от ампутации;
        /// «dot:{buffId}» от DoT-тиков: BodyModule публикует DamageAppliedEvent
        /// с SourceId="dot:…"). После таймаута Fleeing NPC выбирал фантом как
        /// TargetId → движение «не-NPC = игрок» → NPC преследовал и БИЛ игрока
        /// без всякого агро. Теперь ключ обязан быть реальной живой целью:
        /// игрок или живой NPC (DoT-урон от яда не должен провоцировать атаку
        /// на игрока — месть от REAL ударов уже работает через DamageApplied).
        /// </summary>
        private string? GetTopThreatId(NPCState state)
        {
            string? topId = null;
            float max = 0f;
            foreach (var kvp in state.Threats)
            {
                if (kvp.Value <= max) continue;

                // Валидация цели: игрок или ЖИВОЙ NPC.
                if (CultivationGame.Core.Helpers.PlayerIdResolver.IsPlayer(kvp.Key))
                {
                    // игрок — валидная цель (если жив)
                }
                else
                {
                    var threatState = _npcService.GetNPCState(kvp.Key);
                    if (threatState == null || !threatState.IsAlive) continue; // фантом/мёртвый — пропускаем
                }

                max = kvp.Value;
                topId = kvp.Key;
            }
            return topId;
        }

        /// <summary>
        /// Оценить ситуацию и принять решение о следующем AI-состоянии.
        /// Упрощённый Behaviour Tree с весами на основе PersonalityTrait.
        ///
        /// R16: ПОРЯДОК ПРОВЕРОК ИЗМЕНЁН — боевые переходы ДО гейта IsInCombat.
        /// Раньше гейт `if (IsInCombat) return` стоял первым: NPC в бою не
        /// менял состояние ВООБЩЕ — не бежал при HP<20% (спека §4.2), не
        /// выпускал игрока из боя (leash), а ответ на АТАКУ ИГРОКА не работал
        /// вовсе (CombatStartedEvent ставил IsInCombat, но AIState оставался
        /// Idle → ProcessNpcAttacks не видел Attacking → «манекен»).
        /// </summary>
        private void EvaluateAndDecide(NPCState state)
        {
            // === R16: бегство при малом HP — ДО гейта боя (§4.2: работает и в бою) ===
            float healthRatio = state.MaxHealth > 0
                ? (float)state.CurrentHealth / state.MaxHealth
                : 0f;

            if (healthRatio <= _config.FleeHealthRatio && state.AIState != NPCAIState.Fleeing)
            {
                // В активном боя — сообщить CombatService (бой завершится Flee).
                if (state.IsInCombat) PublishDisengage(state, "бегство: HP ниже порога");
                _npcService.SetAIState(state.NpcId, NPCAIState.Fleeing);
                return;
            }

            // === R16: leash (aggro-drop) — цель-игрок слишком далеко ===
            // NPC в бою с игроком, дистанция > LeashRadius → выход из боя:
            // игрок может спастись бегством (раньше преследование было вечным).
            if (state.IsInCombat
                && !string.IsNullOrEmpty(state.TargetId)
                && Core.Helpers.PlayerIdResolver.IsPlayer(state.TargetId))
            {
                float distToPlayer = Vector2.Distance(state.Position, _playerPosition);
                if (distToPlayer > LeashRadius)
                {
                    PublishDisengage(state, "leash: цель вне радиуса привязи");
                    _npcService.SetAIState(state.NpcId, NPCAIState.Wandering);
                    return;
                }
            }

            // В бою — не меняем AIState (боевое поведение управляется парой
            // Attacking + ProcessNpcAttacks; месть/бегство/leash выше уже отработали).
            if (state.IsInCombat) return;

            // === Диспозиционный ИИ (2026-08-22, физический прототип) ===
            // Hostile: игрок в радиусе обнаружения → мгновенная угроза.
            // Friendly: враг атакует игрока рядом → защита (угроза врагу).
            ProcessDisposition(state);

            // Торговец стоит на месте (лавка) — не блуждает и не патрулирует.
            if (state.Disposition == NPCDisposition.Merchant
                && state.AIState is not (NPCAIState.Idle or NPCAIState.Fleeing))
            {
                _npcService.SetAIState(state.NpcId, NPCAIState.Idle);
                return;
            }

            // (R16: проверка бегства при малом HP перенесена ВЫШЕ гейта боя —
            // см. начало EvaluateAndDecide; здесь оставлена только атака.)

            // Проверка: высокая угроза → атака
            // R16: НИЗКОЕ HP — не пере-агримся (раньше: раненый NPC выходил из
            // боя по бегству, тут же входил через угрозу, снова бежал…
            // flip-flop «агро↔бегство» каждые ~1.5с, вечный цикл стартов боя).
            float maxThreat = GetMaxThreat(state);
            if (maxThreat >= _config.ThreatThreshold
                && state.AIState != NPCAIState.Attacking
                && healthRatio > _config.FleeHealthRatio)
            {
                // Назначаем цель = источник максимальной угрозы (до этого
                // TargetId никто не заполнял — Attacking двигался в никуда).
                string? topThreat = GetTopThreatId(state);
                if (!string.IsNullOrEmpty(topThreat))
                {
                    state.TargetId = topThreat;
                    _npcService.SetAIState(state.NpcId, NPCAIState.Attacking);
                    return;
                }
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
        /// R16: + МЕСТЬ — получает урон вне Attacking → переходит в Attacking
        /// (или Fleeing по личности). Раньше цель запоминалась, но AIState не
        /// менялся: NPC «запоминал обидчика», но никогда не атаковал его.
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

            // R16: месть — удар по NPC = мгновенный ответ (Attacking) или
            // бегство (миролюбивый). Порог угроз не нужен: сам факт удара.
            RetaliateOrFlee(state, e.SourceId);
        }

        // === R16: боевые переходы (месть/бегство/leash) ===

        /// <summary>
        /// R16: старт боя → участник-NPC отвечает (NPC_AI_SYSTEM §4.2).
        /// Покрывает ГЛАВНЫЙ дефект: игрок начинает бой (Space) →
        /// CombatStartedEvent → NPCCombatAdapter ставит IsInCombat, но AIState
        /// оставался Idle → NPC не отвечал («манекен»). Теперь каждый
        /// участник-NPC решает: Attacking (боец) или Fleeing (миролюбивый).
        /// </summary>
        private void OnCombatStartedForRetaliation(in CombatStartedEvent e)
        {
            var target = _npcService.GetNPCState(e.TargetId);
            if (target != null && target.IsAlive)
                RetaliateOrFlee(target, e.InstigatorId);

            // Инициатор-NPC и так в Attacking (путь угрозы), но при
            // программном старте боя (QA/будущие системы) — тоже решает.
            var instigator = _npcService.GetNPCState(e.InstigatorId);
            if (instigator != null && instigator.IsAlive)
                RetaliateOrFlee(instigator, e.TargetId);
        }

        /// <summary>
        /// R16: месть или бегство при агрессии против NPC.
        /// Эвристика личности (NPC_AI_SYSTEM §5.2):
        /// - низкое HP (≤ FleeHealthRatio) → сразу бегство (иначе входил в бой,
        ///   чтобы тут же выйти — flip-flop со стартами боя);
        /// - боец по роли (Guard/Enemy/Monster) или Aggressive/Vengeful → Attacking;
        /// - Pacifist/Cautious (не боец по роли) → Fleeing;
        /// - прочие (пассиры/культиваторы) → защищаются (Attacking).
        /// Не трогает уже Attacking/Fleeing (идемпотентность повторных ударов).
        /// </summary>
        private void RetaliateOrFlee(NPCState state, string sourceId)
        {
            if (state.AIState is NPCAIState.Attacking or NPCAIState.Fleeing) return;

            float healthRatio = state.MaxHealth > 0
                ? (float)state.CurrentHealth / state.MaxHealth
                : 0f;

            if (healthRatio <= _config.FleeHealthRatio || ShouldFleeInsteadOfFight(state))
            {
                _npcService.SetAIState(state.NpcId, NPCAIState.Fleeing);
            }
            else
            {
                state.TargetId = sourceId;
                _npcService.SetAIState(state.NpcId, NPCAIState.Attacking);
            }
        }

        /// <summary>
        /// R16: миролюбивый ли NPC (убегает вместо ответа).
        /// Роли-бойцы дерутся всегда; Aggressive/Vengeful — тоже; Pacifist/
        /// Cautious гражданские — бегут (RimWorld-подобное поведение).
        /// </summary>
        private static bool ShouldFleeInsteadOfFight(NPCState state)
        {
            if (state.Role is NPCRole.Guard or NPCRole.Enemy or NPCRole.Monster) return false;
            if ((state.Personality & PersonalityTrait.Aggressive) != 0) return false;
            if ((state.Personality & PersonalityTrait.Vengeful) != 0) return false;
            if ((state.Personality & PersonalityTrait.Pacifist) != 0) return true;
            if ((state.Personality & PersonalityTrait.Cautious) != 0) return true;
            return false; // по умолчанию — защищается
        }

        /// <summary>
        /// R16: публикация выхода NPC из боя. Очистка угроз + событие →
        /// CombatModule завершает CombatService-бой (Flee), NPCCombatAdapter
        /// сбрасывает IsInCombat/TargetId участникам.
        /// </summary>
        private void PublishDisengage(NPCState state, string reason)
        {
            state.Threats.Clear();
            _combatDisengagePub.Publish(new CombatDisengageEvent(state.NpcId, reason));
            Console.WriteLine($"[NPCAIService] {state.NpcId} покидает бой: {reason}");
        }

        /// <summary>
        /// Обработчик BodyPartSeveredEvent — принудительное бегство.
        /// AUDIT-0911 NPC-7 FIX: фантомная угроза «sever_unknown» УДАЛЕНА —
        /// событие не знает атакующего, а ключ-фантом позже выбирался
        /// топ-угрозой → Attacking на несуществующую цель → фолбэк движения
        /// «не-NPC = игрок» → безагровая атака игрока. Бегство (смысл
        /// обработчика) сохранено; угрозу от РЕАЛЬНОГО атакующего вносит
        /// OnDamageApplied (месть R16).
        /// </summary>
        private void OnBodyPartSevered(in BodyPartSeveredEvent e)
        {
            var state = _npcService.GetNPCState(e.EntityId);
            if (state == null || !state.IsAlive) return;

            // Отрубленная часть — принудительное бегство
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
            _combatStartedSubscription?.Dispose();
            _combatStartedSubscription = null;
        }
    }
}

#nullable enable
// Создано: 2026-05-09
// Редактировано: 2026-05-25 06:00:00 UTC — A3-2/A3-3/A3-4/A3-5 FIX: ExecuteAttack overload с targetId/isRanged, авто-начало боя, переключение цели
// Редактировано: 2026-05-25 07:01:36 UTC — ЗАПРЕТ 3.9: _cachedConductivity float → _cachedConductivityPermil int, cast speed integer math
// Редактировано: 2026-05-09 — CMB-A04: вызов IQiBufferService.Activate() для Shield
// Редактировано: 2026-05-09 — CMB-A10: baseDamage из техники вместо хардкода
// Редактировано: 2026-05-09 — CMB-C04: штрафы при QiDepleted
// Редактировано: 2026-05-09 — CMB-C06: исправлена логика победителя при Flee
// Редактировано: 2026-05-09 — EVT-01: убрана инъекция IQiService/IQiBufferService,
//   кросс-модульное общение ТОЛЬКО через MessagePipe (Hub-and-Spoke)
// Редактировано: 2026-05-10 — Phase 17C: C7-E01 FIX: TechniqueUsedEvent.QiCost = qiCost вместо baseDamage
// Редактировано: 2026-05-20 18:43:21 UTC — Фаза 3: IQiDataProvider для уровня NPC (задача 3.I)
// Редактировано: 2026-05-21 18:35:52 UTC — Спринт 1 A3: BaseDamage вместо TechniqueCapacity.CalculateCost
// Редактировано: 2026-05-21 19:25:59 UTC — Спринт 2: B5 Element из TechniqueData, B4 Potency из TechniqueChargeService, B3 IStatProvider, B2 BuffService
// Редактировано: 2026-05-22 07:55:00 UTC — Аудит CRIT-1: PotencyPermil вместо float potency
// Редактировано: 2026-05-22 09:51:00 UTC — Спринт 5: C1/C2/C3 статы для шансов (dodge/crit/block/parry)
// Редактировано: 2026-05-22 09:51:00 UTC — Спринт 6: C4 WeaponDamageCalculator, C6 Penetration
// Редактировано: 2026-05-22 11:30:00 UTC — Спринт 7 C8: Element в DamageAppliedEvent
// Редактировано: 2026-05-22 11:30:00 UTC — Спринт 8 C10: TargetMorphology для таблиц попадания
// Редактировано: 2026-05-22 13:48:17 UTC — Этап 2.2: удалён мёртвый float GetTechniquePotency; Этап 2.3: conductivity хардкод→IQiDataProvider
// Редактировано: 2026-05-22 13:50:00 UTC — Этап 3.1: рефакторинг дублирования P1-8.1
// Редактировано: 2026-05-22 13:55:00 UTC — Этап 3.5: P2-4.1 FIX: IsPlayerTarget + P2-7.3 FIX: AttackSubtype в DamageRequest
// Реализация ICombatService — управление ходом боя.
// Заменяет legacy CombatManager.cs (925 LOC) — God Object разделён.
// CombatManager → CombatService + DamageService + CombatAIService + TechniqueService + CombatLootService.
using System;
using CultivationGame.Core;
using CultivationGame.Core.Helpers;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Combat
{
    /// <summary>
    /// Реализация ICombatService.
    /// Управляет ходом боя: начало, конец, ходы, атаки, защита.
    ///
    /// АРХИТЕКТУРА: CombatService НЕ инжектит IBodyService, IQiService, IQiBufferService напрямую.
    /// Взаимодействие через MessagePipe события (Hub-and-Spoke):
    /// - QiChangedEvent → кэш _cachedCurrentQi, _cachedCultivationLevel
    /// - QiBufferStateChangedEvent → кэш _cachedBufferIsActive
    /// - QiConsumeRequestEvent → запрос расхода Ци (вместо IQiService.TryConsumeQi)
    /// - QiBufferActivateRequestEvent → запрос активации буфера (вместо IQiBufferService.Activate)
    /// - QiBufferDeactivateRequestEvent → запрос деактивации буфера (вместо IQiBufferService.Deactivate)
    ///
    /// Уроки:
    /// - CH-32/33: VContainer нуждается в реализации интерфейсов
    /// - INV-04: sibling scopes НЕ видят регистрации друг друга
    /// - QI-C01: кросс-модульная подписка через MessagePipe
    /// - EVT-01: полная независимость модулей через событийную модель
    /// </summary>
    public class CombatService : ICombatService, IDisposable
    {
        // === Зависимости (DI через конструктор) ===
        private readonly IDamageService _damageService;
        private readonly TechniqueService _techniqueService; // CMB-A10: для данных техники
        private readonly TechniqueChargeService _techniqueChargeService; // Спринт 2 B4: для potency
        private readonly IStatProvider _statProvider; // Спринт 3 B1/B6: статы + стихия + материал
        private readonly IEquipmentDataProvider _equipmentDataProvider; // Спринт 3 B7: NPC BaseDamage
        private readonly IPublisher<CombatStartedEvent> _combatStartedPub;
        private readonly IPublisher<CombatEndedEvent> _combatEndedPub;
        private readonly IPublisher<TechniqueUsedEvent> _techniqueUsedPub;
        private readonly IPublisher<EnemyKilledEvent> _enemyKilledPub;
        private readonly IPublisher<AttackRejectedEvent> _attackRejectedPub; // C-5 (аудит-3): событие отклонения атаки
        private readonly ISubscriber<QiDepletedEvent> _qiDepletedSub;
        private readonly IQiDataProvider _qiDataProvider; // Фаза 3 (3.I): уровни NPC

        // EVT-01: подписки на кросс-модульные события (вместо инъекции IQiService/IQiBufferService)
        private readonly ISubscriber<QiChangedEvent> _qiChangedSub;
        private readonly ISubscriber<QiBufferStateChangedEvent> _qiBufferStateChangedSub;
        private readonly IPublisher<QiConsumeRequestEvent> _qiConsumeRequestPub;
        private readonly IPublisher<QiBufferActivateRequestEvent> _qiBufferActivateReqPub;
        private readonly IPublisher<QiBufferDeactivateRequestEvent> _qiBufferDeactivateReqPub;

        // EVT-01: кэш состояния из событий
        private long _cachedCurrentQi;
        private int _cachedCultivationLevel = 1;
        private int _cachedConductivityPermil = 2780; // ЗАПРЕТ 3.9: проводимость в промилле (2.78 → 2780‰)
        private bool _cachedBufferIsActive;

        // === Состояние боя ===
        private bool _isInCombat;
        private CombatStage _currentStage;
        private string _currentTargetId;
        private string _instigatorId;
        private float _combatTimer;
        private CombatConfig _config;
        private DefenseSubtype _lastPlayerDefense = DefenseSubtype.None; // Последняя защита игрока

        // Спринт 8 C11: время каста техник
        private PendingTechnique _pendingTechnique;
        private bool _isCasting;

        // Stage 0 (2026-08-25, GLM-5.3): potency последней атаки (от зарядки игрока).
        // 1000 = базовая (NPC/без зарядки); >1000 = заряженная игроком (множитель урона).
        private int _lastAttackPotencyPermil = GameConstants.POTENCY_BASE_PERMIL;

        // Phase 8 ч.2 (2026-09-03): ranged-флаг текущей атаки (паттерн
        // _lastAttackPotencyPermil — мгновенный путь; pending несёт свой).
        // true → подтип RangedProjectile + урон дальнобойного оружия (§4.2).
        private bool _lastAttackIsRanged;

        // IDisposable для подписок
        private IDisposable _qiDepletedSubscription;
        private IDisposable _qiChangedSubscription;
        private IDisposable _qiBufferStateChangedSubscription;
        private IDisposable _damageAppliedSubscription; // C11: прерывание каста при получении урона

        // === ICombatService Properties ===

        public bool IsInCombat => _isInCombat;
        public CombatStage CurrentStage => _currentStage;
        public string CurrentTargetId => _currentTargetId;

        // === Конструктор (VContainer) ===

        public CombatService(
            IDamageService damageService,
            TechniqueService techniqueService,
            TechniqueChargeService techniqueChargeService, // Спринт 2 B4
            IStatProvider statProvider, // Спринт 3 B1/B6
            IEquipmentDataProvider equipmentDataProvider, // Спринт 3 B7
            IPublisher<CombatStartedEvent> combatStartedPub,
            IPublisher<CombatEndedEvent> combatEndedPub,
            IPublisher<TechniqueUsedEvent> techniqueUsedPub,
            IPublisher<EnemyKilledEvent> enemyKilledPub,
            IPublisher<AttackRejectedEvent> attackRejectedPub, // C-5 (аудит-3): отклонение при _isCasting
            ISubscriber<QiDepletedEvent> qiDepletedSub,
            ISubscriber<QiChangedEvent> qiChangedSub,
            ISubscriber<QiBufferStateChangedEvent> qiBufferStateChangedSub,
            IPublisher<QiConsumeRequestEvent> qiConsumeRequestPub,
            IPublisher<QiBufferActivateRequestEvent> qiBufferActivateReqPub,
            IPublisher<QiBufferDeactivateRequestEvent> qiBufferDeactivateReqPub,
            IQiDataProvider qiDataProvider) // Фаза 3 (3.I)
        {
            _damageService = damageService;
            _techniqueService = techniqueService;
            _techniqueChargeService = techniqueChargeService; // Спринт 2 B4
            _statProvider = statProvider; // Спринт 3 B1/B6
            _equipmentDataProvider = equipmentDataProvider; // Спринт 3 B7
            _combatStartedPub = combatStartedPub;
            _combatEndedPub = combatEndedPub;
            _techniqueUsedPub = techniqueUsedPub;
            _enemyKilledPub = enemyKilledPub;
            _attackRejectedPub = attackRejectedPub; // C-5 (аудит-3)
            _qiDepletedSub = qiDepletedSub;
            _qiChangedSub = qiChangedSub;
            _qiBufferStateChangedSub = qiBufferStateChangedSub;
            _qiConsumeRequestPub = qiConsumeRequestPub;
            _qiBufferActivateReqPub = qiBufferActivateReqPub;
            _qiBufferDeactivateReqPub = qiBufferDeactivateReqPub;
            _qiDataProvider = qiDataProvider; // Фаза 3 (3.I)

            // EVT-01: подписка на кэш состояния Ци
            _qiChangedSubscription = _qiChangedSub.Subscribe((in QiChangedEvent e) => {
                _cachedCurrentQi = e.Current;
                _cachedCultivationLevel = e.CultivationLevel;
                _cachedConductivityPermil = Permil.FromFloat(e.Conductivity); // ЗАПРЕТ 3.9: проводимость → промилле
            });

            // EVT-01: подписка на кэш состояния буфера
            _qiBufferStateChangedSubscription = _qiBufferStateChangedSub.Subscribe((in QiBufferStateChangedEvent e) => {
                _cachedBufferIsActive = e.IsActive;
            });
        }

        /// <summary>
        /// Спринт 8 C11: Подписка на DamageAppliedEvent для прерывания каста.
        /// Вызывается из CombatModule.Start() после настройки.
        /// </summary>
        public void SubscribeToDamageApplied(ISubscriber<DamageAppliedEvent> damageAppliedSub)
        {
            _damageAppliedSubscription = damageAppliedSub.Subscribe(OnDamageAppliedForCastInterrupt);
        }

        /// <summary>
        /// Спринт 8 C11: Прерывание каста при получении урона кастующим.
        /// </summary>
        private void OnDamageAppliedForCastInterrupt(in DamageAppliedEvent e)
        {
            if (!_isCasting) return;
            // Если атакующий получает урон во время каста — прервать
            if (e.TargetId == _pendingTechnique.AttackerId)
            {
                _isCasting = false;
                _pendingTechnique = default;
            }
        }

        /// <summary>
        /// Настроить сервис конфигурацией.
        /// Вызывается из CombatModule.IStartable.Start().
        /// </summary>
        public void Configure(CombatConfig config)
        {
            _config = config;
        }

        // === ICombatService ===

        public void StartCombat(string instigatorId, string targetId)
        {
            if (_isInCombat) return; // Уже в бою

            _isInCombat = true;
            _instigatorId = instigatorId;
            _currentTargetId = targetId;
            _currentStage = CombatStage.Initiative;
            _combatTimer = 0f;
            _lastPlayerDefense = DefenseSubtype.None;

            // Подписка на QiDepletedEvent — для прерывания техник
            _qiDepletedSubscription = _qiDepletedSub.Subscribe(OnQiDepleted);

            // Определяем инициативу: кто ходит первым
            // Упрощённая версия: игрок всегда первый
            _currentStage = CombatStage.PlayerTurn;

            _combatStartedPub.Publish(new CombatStartedEvent(instigatorId, targetId));
        }

        public void EndCombat()
        {
            if (!_isInCombat) return;

            // CMB-C06: исправлена логика определения победителя
            // 2026-08-26 (аудит-3 C-1): Victory/Defeat теперь ИГРОКО-ЦЕНТРИЧНЫ
            // (Victory = победил игрок; было «instigator победил», что давало
            // winner=NPC в бою, который инициировал NPC и проиграл его же).
            // Для NPC-vs-NPC (игрока в бою нет) — прежняя инстагаторская схема.
            string winnerId;
            string loserId;
            bool victory;

            bool instigatorIsPlayer = PlayerIdResolver.IsPlayer(_instigatorId);
            bool targetIsPlayer = PlayerIdResolver.IsPlayer(_currentTargetId);

            switch (_currentStage)
            {
                case CombatStage.Victory:
                    if (instigatorIsPlayer || targetIsPlayer)
                    {
                        // В бою есть игрок: Victory = игрок победил.
                        winnerId = instigatorIsPlayer ? _instigatorId : _currentTargetId;
                        loserId = instigatorIsPlayer ? _currentTargetId : _instigatorId;
                        victory = true;
                    }
                    else
                    {
                        // NPC vs NPC: победил инициатор (прежняя семантика).
                        winnerId = _instigatorId;
                        loserId = _currentTargetId;
                        victory = true;
                    }
                    break;
                case CombatStage.Defeat:
                    if (instigatorIsPlayer || targetIsPlayer)
                    {
                        // Игрок проиграл: победитель — не-игрок.
                        winnerId = instigatorIsPlayer ? _currentTargetId : _instigatorId;
                        loserId = instigatorIsPlayer ? _instigatorId : _currentTargetId;
                        victory = false;
                    }
                    else
                    {
                        winnerId = _currentTargetId;
                        loserId = _instigatorId;
                        victory = false;
                    }
                    break;
                case CombatStage.Flee:
                    // При побеге — никто не победил
                    winnerId = null;
                    loserId = null;
                    victory = false;
                    break;
                default:
                    // Если бой прерван не через стадию — ничья
                    winnerId = null;
                    loserId = null;
                    victory = false;
                    break;
            }

            _combatEndedPub.Publish(new CombatEndedEvent(winnerId, loserId, victory));

            // Сброс состояния
            _isInCombat = false;
            _currentStage = CombatStage.None;
            _currentTargetId = null;
            _instigatorId = null;
            _combatTimer = 0f;
            _lastPlayerDefense = DefenseSubtype.None;

            // Освобождаем подписку
            _qiDepletedSubscription?.Dispose();
            _qiDepletedSubscription = null;
        }

        /// <summary>
        /// Обратная совместимость — ExecuteAttack без targetId/isRanged.
        /// Делегирует в полную сигнатуру с defaults (null, false).
        /// </summary>
        public void ExecuteAttack(string attackerId, string techniqueId)
        {
            ExecuteAttack(attackerId, techniqueId, null, false);
        }

        /// <summary>
        /// A3-5 FIX: Полная сигнатура ExecuteAttack с TargetId и IsRanged.
        /// A3-2 FIX: Авто-начало боя при наличии TargetId.
        /// A3-3 FIX: Переключение цели при указанном TargetId.
        /// A3-4 FIX: IsRanged доступен для будущих расширений пайплайна.
        ///
        /// Stage 0 (2026-08-25, GLM-5.3): + potencyPermil (по умолчанию 1000).
        /// Если potencyPermil > 1000 — атака игрока после зарядки, пропуск pending-таймера
        /// (зарядка УЖЕ была временем каста). BuildAndExecuteDamageRequest применяет potency.
        /// </summary>
        public void ExecuteAttack(string attackerId, string techniqueId, string targetId, bool isRanged, int potencyPermil = 1000, bool isCharged = false)
        {
            // A3-2 FIX: Авто-начало боя при наличии цели
            if (!_isInCombat)
            {
                if (!string.IsNullOrEmpty(targetId))
                {
                    StartCombat(attackerId, targetId);
                }
                else
                {
                    return; // Вне боя без цели — ничего не делать
                }
            }

            // A3-3 FIX: Переключение цели при указанном TargetId
            if (!string.IsNullOrEmpty(targetId) && targetId != _currentTargetId)
            {
                _currentTargetId = targetId;
            }

            // A3-4 FIX: IsRanged сохраняем для будущих расширений (пока влияет на выбор AttackType)
            // TODO: Использовать isRanged для выбора CombatSubtype в BuildAndExecuteDamageRequest

            // Спринт 8 C11: Уже кастует — нельзя начать новый
            // C-5 FIX (аудит-3): раньше тихий return — игрок не понимал, почему
            // атака не прошла. Публикуем событие отклонения (для UI-тоста,
            // паттерн EquipmentBlockedEvent).
            if (_isCasting)
            {
                _attackRejectedPub.Publish(new AttackRejectedEvent(
                    attackerId,
                    techniqueId,
                    $"Каст уже идёт: {_pendingTechnique.TechniqueId}"));
                return;
            }

            // Stage 0: если атака уже заряжена (isCharged или potency > 1000) —
            // пропустить pending-таймер (зарядка TechniqueChargeService была временем каста).
            if (isCharged || potencyPermil > GameConstants.POTENCY_BASE_PERMIL)
            {
                _lastAttackPotencyPermil = potencyPermil;
                _lastAttackIsRanged = isRanged;
                ApplyTechniqueImmediately(attackerId, techniqueId);
                return;
            }

            // C11: Проверка времени каста техники
            var techForCastTime = _techniqueService.GetTechnique(techniqueId);
            // P2-8.1 FIX: используем CastTime вместо Cooldown
            float castTime = techForCastTime?.CastTime ?? 0.5f;

            // C11: Скорость каста из проводимости
            // ЗАПРЕТ 3.9: проводимость в промилле, castSpeed в промилле
            int conductivityPermil;
            if (_qiDataProvider.HasEntity(attackerId))
                conductivityPermil = Permil.FromFloat(_qiDataProvider.GetConductivity(attackerId));
            else
                conductivityPermil = _cachedConductivityPermil; // Игрок — из кэша QiChangedEvent

            // ЗАПРЕТ 3.9: castSpeedPermil = 1000 + conductivityPermil/100
            // Пример: conductivity=2780‰ → castSpeed=1000+27=1027‰ (×1.027)
            int castSpeedPermil = 1000 + conductivityPermil / 100;
            // effectiveCastTime = castTime × 1000 / castSpeedPermil (float для Unity Time API)
            float effectiveCastTime = Math.Max(0.1f, castTime * 1000f / castSpeedPermil);

            // Запоминаем potency для BuildAndExecuteDamageRequest (после pending-таймера)
            _lastAttackPotencyPermil = potencyPermil;
            // Phase 8 ч.2: ranged-флаг тоже переживает pending-таймер
            // (иначе выстрел из лука после 0.5с «натяжения» забывал бы подтип).
            _lastAttackIsRanged = isRanged;

            if (effectiveCastTime > 0.15f)
            {
                // M1 (2026-09-03): резолвим defender СЕЙЧАС и запоминаем в pending —
                // смена _currentTargetId другими атакующими во время каста больше
                // не перенаправляет выстрел (npc не бьёт сам себя).
                string castTargetId = attackerId == _instigatorId ? _currentTargetId : _instigatorId;
                // Отложенное применение — установить PendingTechnique
                _pendingTechnique = new PendingTechnique
                {
                    AttackerId = attackerId,
                    TechniqueId = techniqueId,
                    TargetId = castTargetId,
                    PotencyPermil = potencyPermil,
                    IsRanged = isRanged,
                    RemainingCastTime = effectiveCastTime,
                    TotalCastTime = effectiveCastTime
                };
                _isCasting = true;

                // Публикация события начала каста (для UI анимации)
                // _castStartedPub.Publish(new TechniqueCastStartedEvent(...));

                // Переход хода — кастующий пропускает ход
                if (_currentStage == CombatStage.PlayerTurn)
                    _currentStage = CombatStage.EnemyTurn;
                return;
            }

            // Мгновенное применение (effectiveCastTime <= 0.15с)
            // Редактировано: 2026-05-22 13:50:00 UTC — Этап 3.1: рефакторинг дублирования P1-8.1
            BuildAndExecuteDamageRequest(attackerId, techniqueId);
        }

        /// <summary>
        /// Спринт 8 C11: Применить технику мгновенно (после каста или если CastTime ≈ 0).
        /// Делегирует основную логику из ExecuteAttack, но без повторной проверки боя.
        /// M1 (2026-09-03): + explicitDefenderId/explicitPotencyPermil — pending-каст
        /// передаёт запомненные цель и potency (per-attacker), мгновенный путь
        /// оставляет null → прежний резолв из текущего состояния боя.
        /// Phase 8 ч.2 (2026-09-03): + explicitIsRanged — pending-каст передаёт
        /// запомненный ranged-флаг каста.
        /// </summary>
        private void ApplyTechniqueImmediately(string attackerId, string techniqueId,
            string explicitDefenderId = null, int? explicitPotencyPermil = null,
            bool? explicitIsRanged = null)
        {
            // Делегируем общую логику в BuildAndExecuteDamageRequest
            // Редактировано: 2026-05-22 13:50:00 UTC — Этап 3.1: рефакторинг дублирования P1-8.1
            BuildAndExecuteDamageRequest(attackerId, techniqueId, explicitDefenderId, explicitPotencyPermil, explicitIsRanged);
        }

        /// <summary>
        /// Общая логика построения и выполнения запроса урона.
        /// Редактировано: 2026-05-22 13:50:00 UTC — Этап 3.1: рефакторинг дублирования P1-8.1
        /// Извлечена из ExecuteAttack и ApplyTechniqueImmediately для устранения дублирования P1-8.1.
        /// Выполняет: получение данных техники, уровней, статов, построение DamageRequest,
        /// расчёт урона, публикацию события, проверку фатальности, переход хода.
        /// </summary>
        private void BuildAndExecuteDamageRequest(string attackerId, string techniqueId,
            string explicitDefenderId = null, int? explicitPotencyPermil = null,
            bool? explicitIsRanged = null)
        {
            // CMB-A10: получаем урон из данных техники вместо хардкода
            int baseDamage = GetTechniqueDamage(techniqueId);
            DamageType damageType = GetTechniqueDamageType(techniqueId);
            Element element = GetTechniqueElement(techniqueId);
            AttackType attackType = GetTechniqueAttackType(techniqueId);
            TechniqueGrade grade = GetTechniqueGrade(techniqueId);
            // Stage 0: potency из зарядки игрока (не dormant lookup).
            // M1: pending-каст подставляет potency СВОЕГО кастера (per-attacker);
            // мгновенный путь — глобальный _lastAttackPotencyPermil как раньше.
            int potencyPermil = explicitPotencyPermil ?? _lastAttackPotencyPermil;

            // Phase 8 ч.2 (2026-09-03): ranged-флаг атаки (лук/арбалет).
            // pending-каст несёт флаг каста; мгновенный путь — глобальный флаг.
            // Базовая атака луком: подтип RangedProjectile + AttackType.Ranged
            // (INT scaling §4.2); стрела — Physical урон (материя, не Ци-снаряд;
            // GetTechniqueDamageType(null) уже возвращает Physical — не трогаем).
            bool isRanged = explicitIsRanged ?? _lastAttackIsRanged;
            if (isRanged && _techniqueService.GetTechnique(techniqueId) == null)
                attackType = AttackType.Ranged;

            // M1 (2026-09-03): pending-каст уже знает свою цель (запомнена на старте).
            // Мгновенный путь — прежний резолв из текущего состояния боя.
            // Баг был: инстагатор кастует → другой атакующий переключает
            // _currentTargetId на кастера → каст срабатывает: defender = сам кастер.
            string defenderId = explicitDefenderId;
            if (string.IsNullOrEmpty(defenderId))
            {
                defenderId = attackerId == _instigatorId ? _currentTargetId : _instigatorId;
            }

            int attackerLevel;
            int defenderLevel;

            // Проверяем, является ли атакующий NPC
            if (_qiDataProvider.HasEntity(attackerId))
            {
                attackerLevel = _qiDataProvider.GetCultivationLevel(attackerId);
            }
            else
            {
                attackerLevel = _cachedCultivationLevel; // Игрок — из кэша QiChangedEvent
            }

            // Проверяем, является ли защитник NPC
            if (_qiDataProvider.HasEntity(defenderId))
            {
                defenderLevel = _qiDataProvider.GetCultivationLevel(defenderId);
            }
            else
            {
                defenderLevel = _cachedCultivationLevel; // Игрок — из кэша
            }

            // 2026-08-26 (аудит-3 C-1): игрок определяется по PlayerIdResolver
            // ("player"/"player_0"), а НЕ по инстагатору. Раньше при инициировании
            // боя NPC (волк/бандит атакует первым: instigator=NPC) флаги
            // ИНВЕРТИРОВАЛИСЬ: isPlayerTarget=false для игрока → qi-щит читался
            // из per-entity провайдера вместо кэша; isPlayerAttacker=true для NPC
            // → защита игрока игнорировалась.
            bool isPlayerAttacker = PlayerIdResolver.IsPlayer(attackerId);

            // CMB-A05/A07/A08: передаём данные через DamageRequest
            // Спринт 3 B1: статы атакующего/защищающегося
            int attackerSTR = _statProvider.GetStat(attackerId, StatType.Strength);
            int attackerAGI = _statProvider.GetStat(attackerId, StatType.Agility);
            int attackerINT = _statProvider.GetStat(attackerId, StatType.Intelligence);
            int defenderAGI = _statProvider.GetStat(defenderId, StatType.Agility);
            int defenderSTR = _statProvider.GetStat(defenderId, StatType.Strength); // P2-5.2: STR защищающегося для блока

            // Спринт 3 B6: DefenderElement и DefenderMaterial из IStatProvider
            Element defenderElement = _statProvider.GetElement(defenderId);
            BodyMaterial defenderMaterial = _statProvider.GetMaterial(defenderId);

            // Спринт 8 C10: TargetMorphology для таблицы попадания
            Morphology targetMorphology = _statProvider.GetMorphology(defenderId);

            // Спринт 5 C1/C2/C3: статы для шансов попадания
            int attackerLuck = _statProvider.GetStat(attackerId, StatType.Luck);

            // NPC_COMBAT_PREP Phase 8: wiring боевых статов экипировки (5 TODO закрыты).
            // techniqueCritBonus — плоский крит-бонус экипировки атакующего
            // (EQUIPMENT_SYSTEM.md §7.1 "critChance"); вклад самой TechniqueData
            // добавится после расширения модели техник.
            int techniqueCritBonus = _equipmentDataProvider.GetCritBonusPermil(attackerId);

            // Спринт 5 C1: штраф уклонения от брони — DodgeBonus брони защитника
            // (тяжёлая броня даёт отрицательный DodgeBonus → положительный штраф).
            // COMBAT_SYSTEM.md §7.1: dodgeChance = 5% + (AGI-10)×0.5% - armorDodgePenalty.
            int armorDodgePenalty = -_equipmentDataProvider.GetDodgeBonusPermil(defenderId);

            // Спринт 5 C3: блок и парирование — StatBonus "blockChance"/"parryChance"
            // экипировки защитника (EQUIPMENT_SYSTEM.md §7.1 Defense).
            int shieldBlock = _equipmentDataProvider.GetBlockBonusPermil(defenderId);
            int weaponParryBonus = _equipmentDataProvider.GetParryBonusPermil(defenderId);

            // Получаем данные техники для penetration и weapon damage
            var tech = _techniqueService.GetTechnique(techniqueId);

            // Спринт 6 C6: пробитие брони
            // penetration = weapon.Penetration + attackerSTR × 0.5 + techniquePenetration (§11.5)
            int penetration = attackerSTR / 2; // attackerSTR × 0.5
            // P2-6.1 FIX: подключаем ArmorPenetration из TechniqueData
            if (tech != null)
                penetration += tech.ArmorPenetration;
            // Phase 8: + weapon.Penetration — пробитие оружия основной руки атакующего
            penetration += _equipmentDataProvider.GetWeaponPenetration(attackerId);

            // Спринт 6 C4 + Phase 8: урон оружия для melee_weapon-техник И базовой
            // атаки с оружием (раньше удар игрока всегда давал 10 — кулак,
            // экипированное оружие не влияло на урон).
            // Phase 8 ч.2 (2026-09-03): + ranged-ветка — урон дальнобойного
            // оружия (лук) по §4.2 (AGI 2.5% + INT 5%), НЕ по melee-формуле STR/AGI.
            var equippedWeapon = _equipmentDataProvider.GetEquipped(attackerId, EquipmentSlot.WeaponMain);
            bool hasWeapon = equippedWeapon != null;
            // Фаза 9A: AttackRange ≤ 2 — ближнее, > 2 — дальнобойное (лук = 18)
            bool isRangedWeapon = isRanged && equippedWeapon != null && equippedWeapon.AttackRange > 2;
            bool useWeaponDamage = (tech != null && tech.Subtype == CombatSubtype.MeleeWeapon)
                                   || (tech == null && hasWeapon && !isRangedWeapon)
                                   || isRangedWeapon;
            if (useWeaponDamage)
            {
                int weaponDamage = _equipmentDataProvider.HasEntity(attackerId)
                    ? (int)_equipmentDataProvider.GetTotalDamage(attackerId)
                    : 0;
                if (weaponDamage > 0)
                {
                    // Phase 8 ч.2: дальнобойное оружие — своя формула (§4.2),
                    // меч — прежняя melee-формула (STR/AGI, §4.1).
                    int weaponBonus = isRangedWeapon
                        ? WeaponDamageCalculator.CalculateRangedWeaponDamage(
                            weaponDamage, attackerAGI, attackerINT)
                        : WeaponDamageCalculator.CalculateMeleeWeaponDamage(
                            weaponDamage, attackerSTR, attackerAGI);
                    // Заменить BaseDamage на урон оружия, если он больше
                    baseDamage = Math.Max(baseDamage, weaponBonus);
                }
            }

            // Спринт 3 B7: NPC BaseDamage как аддитивный бонус — только для безоружных
            // атак NPC (Phase 8: с оружием урон уже учтён выше через WeaponDamageCalculator).
            // P2-4.1-STYLE FIX: строковая проверка "player" заменена на isPlayerAttacker —
            // боевой ID игрока "player_0" раньше попадал в NPC-ветку и после
            // синхронизации экипировки игрока в провайдер дал бы двойной учёт урона.
            // BaseDamage уже зарегистрирован в EquipmentDataProvider.SetTotalDamage()
            if (_equipmentDataProvider.HasEntity(attackerId) && !isPlayerAttacker && !hasWeapon)
            {
                int npcBaseDamage = (int)_equipmentDataProvider.GetTotalDamage(attackerId);
                if (npcBaseDamage > 0 && (tech == null || tech.Subtype != CombatSubtype.MeleeWeapon))
                {
                    // NPC BaseDamage — 50% добавляется к урону техники
                    // Для MeleeWeapon уже учтено через WeaponDamageCalculator
                    baseDamage += npcBaseDamage / 2;
                }
            }

            // P2-4.1 FIX + аудит-3 C-1: цель-игрок — по PlayerIdResolver,
            // не по инстагатору (инстагатором может быть NPC).
            bool isPlayerTarget = PlayerIdResolver.IsPlayer(defenderId);

            // P2-7.3 FIX: передаём подтип атаки для различения slashing/piercing от blunt
            // M2 (2026-09-03): basic_attack с оружием в главной руке теперь MeleeWeapon
            // (раньше всегда MeleeStrike — вооружённый удар шёл как «безоружный»:
            // бонус урона считался, но подтип врал в последствиях: кровотечение
            // slashing/piercing не триггерилось для оружия).
            // Phase 8 ч.2 (2026-09-03): ranged-атака с луком → RangedProjectile
            // (закрыт TODO NPC_COMBAT_PREP Phase 8 ч.2). Подтип задаёт INT-scaling
            // и piercing-последствия (стрела колет — кровотечение, P2-7.3).
            // Ammo (расход стрел) и ProjectileRenderer (визуал трассера) —
            // следующие итерации (см. docs_v2 COMBAT_SYSTEM.md §4.2).
            CombatSubtype attackSubtype = tech?.Subtype
                ?? (isRangedWeapon ? CombatSubtype.RangedProjectile
                : (hasWeapon ? CombatSubtype.MeleeWeapon : CombatSubtype.MeleeStrike));

            var request = new DamageRequest(
                attackerId,
                defenderId,
                baseDamage, damageType, element, defenderElement,
                attackType, grade, potencyPermil, // CRIT-1: промилле вместо float
                attackerLevel, defenderLevel,
                isPlayerAttacker ? DefenseSubtype.None : _lastPlayerDefense,
                defenderMaterial,
                attackerSTR, attackerAGI, attackerINT, defenderAGI, // B1
                armorDodgePenalty, attackerLuck, techniqueCritBonus, // C1/C2
                shieldBlock, weaponParryBonus,                      // C3
                penetration,                                         // C6
                targetMorphology,                                    // C10: морфология для таблицы попадания
                defenderSTR,                                         // P2-5.2: STR защищающегося для блока
                isPlayerTarget,                                      // P2-4.1: флаг «цель — игрок»
                attackSubtype                                        // P2-7.3: подтип атаки (для кровотечения)
            );

            // Единый пайплайн урона
            var result = _damageService.CalculateDamage(request);

            // Публикуем событие использования техники
            // C7-E01 FIX: QiCost = стоимость Ци техники, а не baseDamage
            // Фаза 9D: QiCost float→int (ЗАПРЕТ 3.9)
            int qiCost = GetTechniqueQiCost(techniqueId);
            _techniqueUsedPub.Publish(new TechniqueUsedEvent(attackerId, techniqueId, qiCost));

            // Проверяем результат боя
            if (result.IsFatal)
            {
                // 2026-08-26 (аудит-3 C-1): ВИКТИМ-ЦЕНТРИЧНАЯ логика (была
                // «attackerId == _instigatorId» = игрок): при гибели игрока от
                // руки NPC-инстагатора старый код публиковал EnemyKilledEvent(игрок)
                // (лут/квесты дропались НА игрока) и ставил Victory.
                if (isPlayerTarget)
                {
                    // Игрок погиб — поражение, EnemyKilledEvent НЕ публикуется.
                    _currentStage = CombatStage.Defeat;
                }
                else
                {
                    // Жертва — NPC: если убил игрок — EnemyKilledEvent (лут/квесты).
                    if (isPlayerAttacker)
                        _enemyKilledPub.Publish(new EnemyKilledEvent(defenderId));
                    _currentStage = CombatStage.Victory;
                }
                EndCombat();
                return;
            }

            // Переход хода
            if (_currentStage == CombatStage.PlayerTurn)
            {
                _currentStage = CombatStage.EnemyTurn;
            }
            else if (_currentStage == CombatStage.EnemyTurn)
            {
                _currentStage = CombatStage.PlayerTurn;
            }
        }

        public void ExecuteDefense(string defenderId, DefenseSubtype defenseType)
        {
            if (!_isInCombat) return;

            // Запоминаем выбранную защиту для использования в пайплайне урона
            // 2026-08-26 (аудит-3 C-1): PlayerIdResolver вместо
            // «defenderId == _instigatorId || == _config?.PlayerEntityId»
            // (config-алиас "player" не покрывал канонический "player_0",
            // которым атакует NPC AI).
            if (PlayerIdResolver.IsPlayer(defenderId))
            {
                _lastPlayerDefense = defenseType;
            }

            // CMB-A04: активация QiBuffer в режиме щита (если Shield)
            if (defenseType == DefenseSubtype.Shield)
            {
                long shieldQi = _cachedCurrentQi / 4; // 25% Ци на щит (EVT-01: из кэша)
                if (shieldQi >= GameConstants.MIN_QI_FOR_BUFFER)
                {
                    // P0-X1 FIX: передаём EntityId в QiConsumeRequestEvent для корректного списания Ци
                    _qiConsumeRequestPub.Publish(new QiConsumeRequestEvent(shieldQi, "Combat", defenderId));
                    // EVT-01: запрашиваем активацию буфера через событие вместо _qiBufferService.Activate
                    _qiBufferActivateReqPub.Publish(new QiBufferActivateRequestEvent(shieldQi, QiBufferMode.Shield));
                }
            }

            // Переход хода после защиты
            if (_currentStage == CombatStage.PlayerTurn)
            {
                _currentStage = CombatStage.EnemyTurn;
            }
        }

        // === Обработчики событий ===

        /// <summary>
        /// Обработчик QiDepletedEvent — прерывание текущей техники.
        /// CMB-C04: при исчерпании Ци — боевые штрафы.
        /// </summary>
        private void OnQiDepleted(in QiDepletedEvent e)
        {
            if (!_isInCombat) return;

            // При исчерпании Ци:
            // - Деактивируем QiBuffer (если активен) — через событие (EVT-01)
            // - Сбрасываем защиту до None (нет Ци для щита)
            // - Публикуем штраф через событие (будущие фазы: оглушение, замедление)
            if (_cachedBufferIsActive) // EVT-01: из кэша вместо _qiBufferService.IsActive
            {
                // EVT-01: запрашиваем деактивацию буфера через событие вместо _qiBufferService.Deactivate
                _qiBufferDeactivateReqPub.Publish(new QiBufferDeactivateRequestEvent());
            }
            _lastPlayerDefense = DefenseSubtype.None;
        }

        /// <summary>
        /// Обновление таймера боя (вызывается из CombatModule.Tick).
        /// Спринт 8 C11: обновление таймера каста.
        /// </summary>
        public void UpdateTimer(float deltaTime)
        {
            if (!_isInCombat) return;
            _combatTimer += deltaTime;

            // Проверка таймаута боя
            if (_config != null && _config.MaxCombatDuration > 0 && _combatTimer >= _config.MaxCombatDuration)
            {
                _currentStage = CombatStage.Flee;
                EndCombat();
                return;
            }

            // Спринт 8 C11: Обновление таймера каста
            if (_isCasting)
            {
                _pendingTechnique.RemainingCastTime -= deltaTime;
                if (_pendingTechnique.RemainingCastTime <= 0f)
                {
                    string attackerId = _pendingTechnique.AttackerId;
                    string techniqueId = _pendingTechnique.TechniqueId;
                    string pendingTargetId = _pendingTechnique.TargetId;   // M1: цель на момент старта каста
                    int pendingPotencyPermil = _pendingTechnique.PotencyPermil; // M1: potency кастера
                    bool pendingIsRanged = _pendingTechnique.IsRanged;     // Phase 8 ч.2: ranged-флаг каста
                    _isCasting = false;
                    _pendingTechnique = default;
                    ApplyTechniqueImmediately(attackerId, techniqueId, pendingTargetId, pendingPotencyPermil, pendingIsRanged);
                }
            }
        }

        // === Вспомогательные методы для получения данных техники ===

        /// <summary>
        /// Получить базовый урон техники.
        /// CMB-A10: если техника изучена — берём из данных, иначе — базовый урон оружия/кулака.
        /// Спринт 1 A3 FIX: используем TechniqueData.BaseDamage вместо TechniqueCapacity.CalculateCost().
        /// CalculateCost возвращает стоимость ёмкости (50-200), а не урон.
        /// BaseDamage = CapacityCost × gradeMultiplier — это реальный урон техники.
        /// </summary>
        private int GetTechniqueDamage(string techniqueId)
        {
            var tech = _techniqueService.GetTechnique(techniqueId);
            if (tech != null)
            {
                // A3 FIX: BaseDamage уже содержит capacity × gradeMultiplier
                // Старый код: TechniqueCapacity.CalculateCost() → стоимость ёмкости (50-200),
                // что не является уроном. BaseDamage рассчитывается при генерации техники.
                int baseDamage = tech.BaseDamage > 0 ? tech.BaseDamage : 10; // P1-6.1: BaseDamage уже int
                // Этап 5 внедрения ЦИ: бонус формации Amplification (пермил, ЗАПРЕТ 3.9).
                int bonusPermil = _techniqueService.ExternalDamageBonusPermil;
                if (bonusPermil != 0)
                {
                    baseDamage = baseDamage * bonusPermil / 1000;
                }
                return baseDamage;
            }
            // Базовый урон кулака (нет техники)
            return 10;
        }

        /// <summary>
        /// Получить тип урона техники.
        /// Спринт 4 C6: Qi-урон для Ranged-подтипов и Combat-техник с элементом.
        /// MeleeStrike/MeleeWeapon → Physical. Ranged* → Qi. Defense* → Physical.
        /// </summary>
        private DamageType GetTechniqueDamageType(string techniqueId)
        {
            var tech = _techniqueService.GetTechnique(techniqueId);
            if (tech != null)
            {
                // C6: дальнобойные техники — Qi урон (снаряды, лучи, AoE)
                // Ближний бой — Physical
                return tech.Subtype switch
                {
                    CombatSubtype.RangedProjectile => DamageType.Qi,
                    CombatSubtype.RangedBeam => DamageType.Qi,
                    CombatSubtype.RangedAoe => DamageType.Qi,
                    CombatSubtype.MeleeStrike => DamageType.Physical,
                    CombatSubtype.MeleeWeapon => DamageType.Physical,
                    CombatSubtype.DefenseBlock => DamageType.Physical,
                    CombatSubtype.DefenseShield => DamageType.Physical,
                    CombatSubtype.DefenseDodge => DamageType.Physical,
                    // Для Combat-типа без подтипа — Qi если есть стихия, иначе Physical
                    _ => tech.Type == TechniqueType.Combat && tech.Element != Element.Neutral
                        ? DamageType.Qi
                        : DamageType.Physical
                };
            }
            return DamageType.Physical;
        }

        /// <summary>
        /// Получить стихию техники.
        /// Спринт 2 B5 FIX: читаем из TechniqueData.Element вместо хардкода Neutral.
        /// Без этого ВСЕ атаки были Neutral — стихийная система мертва.
        /// </summary>
        private Element GetTechniqueElement(string techniqueId)
        {
            var tech = _techniqueService.GetTechnique(techniqueId);
            return tech?.Element ?? Element.Neutral;
        }

        /// <summary>
        /// Получить тип атаки техники (для stat scaling и подавления уровнем).
        /// Спринт 3 B1: использует CombatSubtype для определения AttackType.
        /// MeleeStrike → STR scaling, MeleeWeapon → AGI scaling, Ranged* → INT scaling.
        /// </summary>
        private AttackType GetTechniqueAttackType(string techniqueId)
        {
            var tech = _techniqueService.GetTechnique(techniqueId);
            if (tech != null)
            {
                // Ultimate-техника — отдельный тип
                if (tech.IsUltimate) return AttackType.Ultimate;

                // Спринт 3 B1: маппинг CombatSubtype → AttackType для stat scaling
                return tech.Subtype switch
                {
                    CombatSubtype.MeleeStrike => AttackType.MeleeStrike,
                    CombatSubtype.MeleeWeapon => AttackType.MeleeWeapon,
                    CombatSubtype.RangedProjectile => AttackType.Ranged,
                    CombatSubtype.RangedBeam => AttackType.Ranged,
                    CombatSubtype.RangedAoe => AttackType.Ranged,
                    _ => tech.Type == TechniqueType.Combat ? AttackType.Technique : AttackType.Normal
                };
            }
            return AttackType.MeleeStrike; // Без техники — безоружная атака (STR scaling)
        }

        /// <summary>
        /// Получить грейд техники.
        /// </summary>
        private TechniqueGrade GetTechniqueGrade(string techniqueId)
        {
            var tech = _techniqueService.GetTechnique(techniqueId);
            return tech?.Grade ?? TechniqueGrade.Common;
        }

        /// <summary>
        /// Получить стоимость Ци техники.
        /// C7-E01 FIX: используем стоимость Ци из данных техники вместо baseDamage.
        /// </summary>
        // Фаза 9D: float→int (ЗАПРЕТ 3.9)
        private int GetTechniqueQiCost(string techniqueId)
        {
            var tech = _techniqueService.GetTechnique(techniqueId);
            if (tech != null)
            {
                return (int)tech.QiCost; // CMB-A06: QiCost — long, кастуем в int
            }
            // Базовая атака — 0 Ци
            return 0;
        }

        public void Dispose()
        {
            _qiDepletedSubscription?.Dispose();
            _qiDepletedSubscription = null;
            _qiChangedSubscription?.Dispose();
            _qiChangedSubscription = null;
            _qiBufferStateChangedSubscription?.Dispose();
            _qiBufferStateChangedSubscription = null;
            _damageAppliedSubscription?.Dispose();
            _damageAppliedSubscription = null;
        }

        /// <summary>
        /// Спринт 8 C11: Данные отложенной техники (время каста).
        /// M1 (2026-09-03): + TargetId/PotencyPermil — pending запоминает цель и
        /// силу КАСТЕРА на момент старта. Раньше цель резолвилась из
        /// _currentTargetId/_instigatorId В МОМЕНТ срабатывания: если за время
        /// каста цель боя переключалась (A3-3: другой атакующий указал TargetId),
        /// инстагатор-кастер получал defenderId = сам себя (npc→npc self-hit).
        /// Potency был глобальным (_lastAttackPotencyPermil): заряженная атака
        /// игрока во время чужого каста усиливала чужой pending-выстрел.
        /// </summary>
        private struct PendingTechnique
        {
            public string AttackerId;
            public string TechniqueId;
            public string TargetId;        // M1: defender на момент старта каста
            public int PotencyPermil;      // M1: potency кастера на момент старта
            public bool IsRanged;          // Phase 8 ч.2: ranged-флаг каста (лук)
            public float RemainingCastTime;
            public float TotalCastTime;
        }
    }
}

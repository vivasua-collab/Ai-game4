#nullable enable
// Создано: 2026-05-09 15:30:00 UTC
// Редактировано: 2026-05-20 18:43:21 UTC — Фаза 3: двойной урон исправлен (задача 3.L)
// Редактировано: 2026-05-21 08:15:33 UTC — Волна 2.3: CurrentHealth обновляется из IBodyDataProvider
// Адаптер боя NPC — мост между NPC-модулем и боевой системой через MessagePipe.
// EVT-01: Все кросс-модульные взаимодействия — через MessagePipe.
// Hub-and-Spoke: НЕ инжектит ICombatService напрямую.
// ПРОТИВОРЕЧИЕ #3: NPCCombatAdapter НЕ списывает HP напрямую — только через BodyParts.
using System;
using CultivationGame.Core;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;

namespace CultivationGame.Modules.NPC
{
    /// <summary>
    /// Адаптер боя NPC — мост между NPC-модулем и боевой системой.
    ///
    /// Подписки (только MessagePipe):
    /// - CombatStartedEvent → пометить NPC как IsInCombat
    /// - CombatEndedEvent → очистить IsInCombat, обновить отношение
    ///
    /// Публикации:
    /// - CombatStartedEvent → когда NPC решает атаковать
    /// - NPCDamagedEvent → когда NPC получает урон
    ///
    /// АРХИТЕКТУРА: Адаптер НЕ инжектит ICombatService.
    /// Все взаимодействия через MessagePipe (Hub-and-Spoke).
    /// </summary>
    public class NPCCombatAdapter : IDisposable
    {
        // === Зависимости ===
        private readonly NPCService _npcService;
        private readonly NPCConfig _config;
        private readonly IBodyDataProvider _bodyDataProvider; // Волна 2.3: для обновления CurrentHealth

        // === MessagePipe: паблишеры ===
        private readonly IPublisher<NPCDamagedEvent> _npcDamagedPub;
        private readonly IPublisher<NPCDeathEvent> _npcDeathPub;

        // === MessagePipe: подписки ===
        private readonly ISubscriber<CombatStartedEvent> _combatStartedSub;
        private readonly ISubscriber<CombatEndedEvent> _combatEndedSub;
        private readonly ISubscriber<DamageAppliedEvent> _damageAppliedSub;
        // R16: выход NPC из боя по своей инициативе (бегство/leash).
        private readonly ISubscriber<CombatDisengageEvent> _combatDisengageSub;
        private IDisposable _combatStartedSubscription;
        private IDisposable _combatEndedSubscription;
        private IDisposable _damageAppliedSubscription;
        private IDisposable _combatDisengageSubscription;

        // === Конструктор (VContainer) ===
        public NPCCombatAdapter(
            NPCService npcService,
            NPCConfig config,
            IBodyDataProvider bodyDataProvider,
            IPublisher<NPCDamagedEvent> npcDamagedPub,
            IPublisher<NPCDeathEvent> npcDeathPub,
            ISubscriber<CombatStartedEvent> combatStartedSub,
            ISubscriber<CombatEndedEvent> combatEndedSub,
            ISubscriber<DamageAppliedEvent> damageAppliedSub,
            // R16: disengage-подписка.
            ISubscriber<CombatDisengageEvent> combatDisengageSub)
        {
            _npcService = npcService;
            _config = config;
            _bodyDataProvider = bodyDataProvider;
            _npcDamagedPub = npcDamagedPub;
            _npcDeathPub = npcDeathPub;
            _combatStartedSub = combatStartedSub;
            _combatEndedSub = combatEndedSub;
            _damageAppliedSub = damageAppliedSub;
            _combatDisengageSub = combatDisengageSub;
        }

        /// <summary>
        /// Инициализация: подписки на боевые события.
        /// </summary>
        public void Initialize()
        {
            _combatStartedSubscription = _combatStartedSub.Subscribe(OnCombatStarted);
            _combatEndedSubscription = _combatEndedSub.Subscribe(OnCombatEnded);
            _damageAppliedSubscription = _damageAppliedSub.Subscribe(OnDamageApplied);
            // R16: сброс состояния при выходе NPC из боя.
            _combatDisengageSubscription = _combatDisengageSub.Subscribe(OnCombatDisengage);
        }

        // === Публичный API ===

        /// <summary>
        /// Review этап 3 (P2-5): MarkNpcCombatStarted (было StartAttack — имя
        /// вводило в заблуждение: метод НЕ запускает расчёт боя в CombatService,
        /// а только помечает NPC как находящегося в бою). Реальный удар идёт
        /// ОТДЕЛЬНЫМ путём: NPCModule.ProcessNpcAttacks → AttackIntentEvent →
        /// CombatModule → CombatService (первый интент и стартует
        /// CombatService-бой).
        /// Вызывается NPCModule.OnAIStateChanged при переходе в Attacking.
        ///
        /// R16 (2026-09-10): фантомный publish CombatStartedEvent УДАЛЁН —
        /// событие публикует ТОЛЬКО CombatService.StartCombat (при первом
        /// принятом интенте). Раньше адаптер публиковал событие напрямую
        /// (двойной publish при реальном старте; «бой» в UI до фактического
        /// боя). Теперь — прямая пометка состояний обоим NPC-участникам.
        /// Ответ NPC на начало боя — NPCAIService.OnCombatStartedForRetaliation
        /// (подписан на НАСТОЯЩЕЕ событие CombatService).
        /// </summary>
        public void MarkNpcCombatStarted(string npcId, string targetId)
        {
            var state = _npcService.GetNPCState(npcId);
            if (state == null || !state.IsAlive) return;
            if (!state.IsInCombat)
            {
                state.IsInCombat = true;
                state.TargetId = targetId;
            }

            // NPC-vs-NPC: пометить и цель (если она NPC).
            var targetState = _npcService.GetNPCState(targetId);
            if (targetState != null && targetState.IsAlive && !targetState.IsInCombat)
            {
                targetState.IsInCombat = true;
                targetState.TargetId = npcId;
            }
        }

        /// <summary>
        /// R16-аудит (P3-1): мёртвый метод ApplyDamage удалён — ни одного
        /// вызователя в game/src (урон NPC идёт через DamageAppliedEvent →
        /// OnDamageApplied → NPCDamagedEvent, см. ниже).
        /// </summary>

        // === Обработчики кросс-модульных событий ===

        /// <summary>
        /// Обработчик CombatStartedEvent — пометить NPC как IsInCombat.
        /// </summary>
        private void OnCombatStarted(in CombatStartedEvent e)
        {
            // Проверяем, является ли NPC участником боя
            var npcState = _npcService.GetNPCState(e.InstigatorId);
            if (npcState != null)
            {
                npcState.IsInCombat = true;
                npcState.TargetId = e.TargetId;
                // Не меняем AIState — CombatAdapter не управляет AI
            }

            // Проверяем цель (может быть NPC)
            var targetState = _npcService.GetNPCState(e.TargetId);
            if (targetState != null)
            {
                targetState.IsInCombat = true;
                targetState.TargetId = e.InstigatorId;
            }
        }

        /// <summary>
        /// Обработчик CombatEndedEvent — очистить IsInCombat, обновить отношение.
        /// R16: null-гварды Winner/Loser — Flee-окончание (бегство/leash NPC)
        /// публикует CombatEndedEvent(null, null) — раньше падало
        /// ArgumentNullException'ом в GetNPCState (спящий баг: Flee-стадия
        /// была недостижима при MaxCombatDuration=0).
        /// </summary>
        private void OnCombatEnded(in CombatEndedEvent e)
        {
            // Победитель
            var winnerState = e.WinnerId != null ? _npcService.GetNPCState(e.WinnerId) : null;
            if (winnerState != null)
            {
                winnerState.IsInCombat = false;
                winnerState.TargetId = null;

                // Ухудшение отношения к проигравшему
                if (e.LoserId != null)
                    _npcService.ModifyAttitude(e.WinnerId!, e.LoserId, -10);
            }

            // Проигравший
            var loserState = e.LoserId != null ? _npcService.GetNPCState(e.LoserId) : null;
            if (loserState != null)
            {
                loserState.IsInCombat = false;
                loserState.TargetId = null;

                // Сильное ухудшение отношения к победителю
                if (e.WinnerId != null)
                    _npcService.ModifyAttitude(e.LoserId!, e.WinnerId, -20);
            }
        }

        /// <summary>
        /// R16: NPC покинул бой по своей инициативе (бегство HP&lt;20% / leash).
        /// CombatService-бой завершается CombatModule-ом (AbandonCombat →
        /// CombatEndedEvent(Flee) несёт null победителя — по нему адаптер
        /// никого не чистит), поэтому сброс IsInCombat/TargetId делаем здесь,
        /// для обеих NPC-сторон (дисэнгейджер + его оппонент-NPC).
        /// </summary>
        private void OnCombatDisengage(in CombatDisengageEvent e)
        {
            var state = _npcService.GetNPCState(e.NpcId);
            if (state == null) return;

            string? opponentId = state.TargetId;
            state.IsInCombat = false;
            state.TargetId = null;

            // NPC-vs-NPC: оппонент тоже выходит из навязанного боя.
            var opponentState = opponentId != null ? _npcService.GetNPCState(opponentId) : null;
            if (opponentState != null && opponentState.IsAlive)
            {
                opponentState.IsInCombat = false;
                opponentState.TargetId = null;
            }
        }

        /// <summary>
        /// Обработчик DamageAppliedEvent — уведомление NPC о полученном уроне.
        /// ПРОТИВОРЕЧИЕ #3: урон уже применён через BodyService.
        /// НЕ вызываем ApplyDamage повторно — только публикуем NPCDamagedEvent.
        /// </summary>
        private void OnDamageApplied(in DamageAppliedEvent e)
        {
            var state = _npcService.GetNPCState(e.TargetId);
            if (state == null || !state.IsAlive) return;

            // ПРОТИВОРЕЧИЕ #3: урон уже применён через BodyService.BodyParts
            // Публикуем NPCDamagedEvent как уведомление (без повторного вычитания HP)

            // Волна 2.3: Обновляем CurrentHealth из BodyParts
            // Без этого CurrentHealth навсегда = MaxHealth, NPC бессмертен логически
            if (_bodyDataProvider.HasEntity(e.TargetId))
                state.CurrentHealth = _bodyDataProvider.GetCurrentHealth(e.TargetId);

            float healthRatio = state.MaxHealth > 0
                ? (float)state.CurrentHealth / state.MaxHealth
                : 0f;
            _npcDamagedPub.Publish(new NPCDamagedEvent(e.TargetId, e.SourceId, e.Damage, healthRatio));

            // Проверка смерти: 2026-09-11 (аудит боя) — ЕДИНОЕ правило тел
            // (BODY_SYSTEM): смерть = жизненно важная часть уничтожена
            // (Head/Heart RedHP ≤ 0 — IsEntityAlive) ИЛИ полный дренаж HP.
            // Раньше ждали только CurrentHealth ≤ 0 — при per-part floor
            // (урон в мёртвую часть теряется) сумма практически не обнуляется
            // → NPC «бессмертен» в честном бою, труп не создавался.
            bool bodyDead = _bodyDataProvider.HasEntity(e.TargetId)
                && (!_bodyDataProvider.IsEntityAlive(e.TargetId)
                    || _bodyDataProvider.GetCurrentHealth(e.TargetId) <= 0);
            if (bodyDead && state.IsAlive)
            {
                state.IsAlive = false;
                state.IsInCombat = false;
                state.AIState = NPCAIState.Idle;
                _npcDeathPub.Publish(new NPCDeathEvent(e.TargetId, e.SourceId));
            }
        }

        public void Dispose()
        {
            _combatStartedSubscription?.Dispose();
            _combatStartedSubscription = null;
            _combatEndedSubscription?.Dispose();
            _combatEndedSubscription = null;
            _damageAppliedSubscription?.Dispose();
            _damageAppliedSubscription = null;
            _combatDisengageSubscription?.Dispose();
            _combatDisengageSubscription = null;
        }
    }
}

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
        private readonly IPublisher<CombatStartedEvent> _combatStartedPub;
        private readonly IPublisher<NPCDamagedEvent> _npcDamagedPub;
        private readonly IPublisher<NPCDeathEvent> _npcDeathPub;

        // === MessagePipe: подписки ===
        private readonly ISubscriber<CombatStartedEvent> _combatStartedSub;
        private readonly ISubscriber<CombatEndedEvent> _combatEndedSub;
        private readonly ISubscriber<DamageAppliedEvent> _damageAppliedSub;
        private IDisposable _combatStartedSubscription;
        private IDisposable _combatEndedSubscription;
        private IDisposable _damageAppliedSubscription;

        // === Конструктор (VContainer) ===
        public NPCCombatAdapter(
            NPCService npcService,
            NPCConfig config,
            IBodyDataProvider bodyDataProvider,
            IPublisher<CombatStartedEvent> combatStartedPub,
            IPublisher<NPCDamagedEvent> npcDamagedPub,
            IPublisher<NPCDeathEvent> npcDeathPub,
            ISubscriber<CombatStartedEvent> combatStartedSub,
            ISubscriber<CombatEndedEvent> combatEndedSub,
            ISubscriber<DamageAppliedEvent> damageAppliedSub)
        {
            _npcService = npcService;
            _config = config;
            _bodyDataProvider = bodyDataProvider;
            _combatStartedPub = combatStartedPub;
            _npcDamagedPub = npcDamagedPub;
            _npcDeathPub = npcDeathPub;
            _combatStartedSub = combatStartedSub;
            _combatEndedSub = combatEndedSub;
            _damageAppliedSub = damageAppliedSub;
        }

        /// <summary>
        /// Инициализация: подписки на боевые события.
        /// </summary>
        public void Initialize()
        {
            _combatStartedSubscription = _combatStartedSub.Subscribe(OnCombatStarted);
            _combatEndedSubscription = _combatEndedSub.Subscribe(OnCombatEnded);
            _damageAppliedSubscription = _damageAppliedSub.Subscribe(OnDamageApplied);
        }

        // === Публичный API ===

        /// <summary>
        /// NPC начинает атаку — публикует CombatStartedEvent.
        /// Вызывается NPCAIService при переходе в Attacking.
        /// </summary>
        public void StartAttack(string npcId, string targetId)
        {
            var state = _npcService.GetNPCState(npcId);
            if (state == null || !state.IsAlive) return;
            if (state.IsInCombat) return;

            // Публикуем событие начала боя
            _combatStartedPub.Publish(new CombatStartedEvent(npcId, targetId));
        }

        /// <summary>
        /// NPC получает урон — уведомление (НЕ вычитает HP напрямую).
        /// ПРОТИВОРЕЧИЕ #3: единая система через BodyParts.
        /// HP = Σ(BodyParts.RedHP) — всегда пересчитывается из body parts.
        /// Урон проходит через BodyService → DamageAppliedEvent → BodyParts.
        /// </summary>
        public void ApplyDamage(string npcId, string sourceId, int damage)
        {
            var state = _npcService.GetNPCState(npcId);
            if (state == null || !state.IsAlive) return;

            // ПРОТИВОРЕЧИЕ #3: НЕ вычитаем HP напрямую!
            // Урон проходит через BodyService → DamageAppliedEvent → BodyParts
            // NPCHealth пересчитывается из BodyParts через IBodyDataProvider.GetCurrentHealth()

            // Публикуем событие получения урона (уведомление)
            // CurrentHealth пересчитывается из IBodyDataProvider.GetCurrentHealth()
            float healthRatio = state.MaxHealth > 0
                ? (float)state.CurrentHealth / state.MaxHealth
                : 0f;

            _npcDamagedPub.Publish(new NPCDamagedEvent(npcId, sourceId, damage, healthRatio));

            // Проверка смерти — через кэшированное HP
            // Временно: оставляем проверку через CurrentHealth (будет обновляться через событие)
        }

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
        /// </summary>
        private void OnCombatEnded(in CombatEndedEvent e)
        {
            // Победитель
            var winnerState = _npcService.GetNPCState(e.WinnerId);
            if (winnerState != null)
            {
                winnerState.IsInCombat = false;
                winnerState.TargetId = null;

                // Ухудшение отношения к проигравшему
                if (e.LoserId != null)
                    _npcService.ModifyAttitude(e.WinnerId, e.LoserId, -10);
            }

            // Проигравший
            var loserState = _npcService.GetNPCState(e.LoserId);
            if (loserState != null)
            {
                loserState.IsInCombat = false;
                loserState.TargetId = null;

                // Сильное ухудшение отношения к победителю
                if (e.WinnerId != null)
                    _npcService.ModifyAttitude(e.LoserId, e.WinnerId, -20);
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

            // Проверка смерти: CurrentHealth должен обновляться из BodyParts
            if (state.CurrentHealth <= 0 && state.IsAlive)
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
        }
    }
}

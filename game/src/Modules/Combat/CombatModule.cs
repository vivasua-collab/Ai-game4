#nullable enable
// Создано: 2026-05-09
// Точка входа модуля боя.
// IStartable — инициализация сервисов, ITickable — обновление AI и таймеров.
// Migrated from Ai-game3 (Unity+VContainer+MessagePipe) to Ai-game4 (Godot+DI+EventBus) 2026-08-15:
//   - using MessagePipe → using CultivationGame.Core.Events
//   - using VContainer/VContainer.Unity → using CultivationGame.Core.DI / CultivationGame.Core.Interfaces
//   - Handler signature: void OnXxx(XxxEvent e) → void OnXxx(in XxxEvent e)
//   - IStartable.Start() / ITickable.Tick() → IModule.Start() / IModule.Tick(int)
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Combat;

/// <summary>
/// Точка входа модуля боя.
/// Инициализирует сервисы конфигурацией и подписывается на события.
///
/// Review этап 3 (P0-2): фантомный CombatAIService (жёсткий ID "enemy" без
/// тела/Ци/статов) УДАЛЁН из runtime-цикла. Единственный источник NPC-атак —
/// NPCModule.ProcessNpcAttacks (реальные NPC с динамическими ID).
/// </summary>
public class CombatModule : IModule
{
    // === Зависимости (DI через интерфейсы Core) ===
    [Inject] private readonly ICombatService _combatService = null!;
    [Inject] private readonly CombatService _combatServiceImpl = null!;
    [Inject] private readonly IDamageService _damageService = null!;
    [Inject] private readonly TechniqueService _techniqueService = null!;
    [Inject] private readonly TechniqueChargeService _techniqueChargeService = null!;
    [Inject] private readonly CombatLootService _combatLootService = null!;
    [Inject] private readonly ITimeService _timeService = null!;

    // Phase 8 ч.3 (2026-09-03): гейт дальнего боя (LOS + расход стрел)
    // и публикация отклонений (тот же контракт C-5, что и у CombatService).
    // Review этап 3 (P1-3): проверка наличия стрел ДО боя; списание — только
    // ПОСЛЕ принятия атаки CombatService (см. OnAttackIntent).
    [Inject] private readonly CombatRangeGateService _rangeGate = null!;
    [Inject] private readonly IPublisher<AttackRejectedEvent> _attackRejectedPub = null!;

    // Подписка на события
    [Inject] private readonly ISubscriber<EnemyKilledEvent> _enemyKilledSub = null!;
    [Inject] private readonly ISubscriber<CombatEndedEvent> _combatEndedSub = null!;
    [Inject] private readonly ISubscriber<EquipmentChangedEvent> _equipmentChangedSub = null!;
    [Inject] private readonly ISubscriber<BuffAppliedEvent> _buffAppliedSub = null!;
    [Inject] private readonly ISubscriber<BuffRemovedEvent> _buffRemovedSub = null!;
    [Inject] private readonly ISubscriber<DamageAppliedEvent> _damageAppliedSub = null!;
    [Inject] private readonly ISubscriber<AttackIntentEvent> _attackIntentSub = null!;

    // === Состояние ===
    // IMPL-3: Config injected via DI (replaces obsolete SetConfig()).
    [Inject] private readonly CombatConfig _config = null!;
    private bool _isConfigured;
    private IDisposable? _enemyKilledSubscription;
    private IDisposable? _combatEndedSubscription;
    private IDisposable? _equipmentChangedSubscription;
    private IDisposable? _buffAppliedSubscription;
    private IDisposable? _buffRemovedSubscription;
    private IDisposable? _attackIntentSubscription;

    public string ModuleName => "Combat";

    public void Start()
    {
        // IMPL-3: Config injected via DI. Flag still used by Tick/handlers.
        _isConfigured = true;

        // === Конфигурация сервисов ===
        _combatServiceImpl.Configure(_config);

        // Review этап 3 (P0-2): Инициализация фантомного AI("enemy") удалена —
        // NPC-атаки идут только через NPCModule.ProcessNpcAttacks (реальные ID).

        // === Подписка на кросс-модульные события ===
        _enemyKilledSubscription = _enemyKilledSub.Subscribe(OnEnemyKilled);
        _combatEndedSubscription = _combatEndedSub.Subscribe(OnCombatEnded);

        _equipmentChangedSubscription = _equipmentChangedSub.Subscribe(OnEquipmentChanged);
        _buffAppliedSubscription = _buffAppliedSub.Subscribe(OnBuffApplied);
        _buffRemovedSubscription = _buffRemovedSub.Subscribe(OnBuffRemoved);

        // Спринт 8 C11: подписка CombatService на DamageAppliedEvent (прерывание каста)
        _combatServiceImpl.SubscribeToDamageApplied(_damageAppliedSub);

        // Фаза 9D: подписка на AttackIntentEvent — боевой мост
        _attackIntentSubscription = _attackIntentSub.Subscribe(OnAttackIntent);
    }

    public void Tick(int tickCount)
    {
        if (!_isConfigured) return;

        float delta = _timeService.DeltaTime;

        _combatServiceImpl.UpdateTimer(delta);

        // Обновление кулдаунов техник
        _techniqueService.UpdateCooldowns(delta);

        // Stage 0 (2026-08-25, GLM-5.3): обновление активных зарядок техник
        // (модель заполнения TECHNIQUE_SYSTEM §5.3). Зарядка тиками по проводимости.
        _techniqueChargeService.UpdateCharges(delta);

        // Review этап 3 (P0-2): блок «AI-ход (только в EnemyTurn)» удалён —
        // фантомный CombatAIService с жёстким ID "enemy" создавал атаки
        // сущности без тела/Ци/экипировки и конкурировал с реальными NPC.
        // Чужой ход двигается тайм-аутом EnemyTurnTimeoutSec в UpdateTimer.
    }

    /// <summary>
    /// Обработчик EnemyKilledEvent — генерация лута.
    /// EventBus handler signature: void OnXxx(in XxxEvent e).
    /// </summary>
    private void OnEnemyKilled(in EnemyKilledEvent e)
    {
        if (!_isConfigured || _config == null || !_config.AutoLootOnVictory) return;

        var loot = _combatLootService.GenerateLoot(e.EnemyId, 1);
        _combatLootService.GrantLoot(loot);
    }

    /// <summary>
    /// Обработчик CombatEndedEvent — сброс.
    /// Review этап 3 (P0-2): деактивация фантомного AI удалена вместе с ним.
    /// </summary>
    private void OnCombatEnded(in CombatEndedEvent e)
    {
    }

    /// <summary>
    /// Обработчик EquipmentChangedEvent — обновление данных брони.
    /// </summary>
    private void OnEquipmentChanged(in EquipmentChangedEvent e)
    {
        // В будущих фазах: пересчёт брони для пайплайна урона
    }

    /// <summary>
    /// Обработчик BuffAppliedEvent — обновление модификаторов.
    /// </summary>
    private void OnBuffApplied(in BuffAppliedEvent e)
    {
        // В будущих фазах: пересчёт боевых модификаторов
    }

    /// <summary>
    /// Обработчик BuffRemovedEvent — обновление модификаторов.
    /// </summary>
    private void OnBuffRemoved(in BuffRemovedEvent e)
    {
        // В будущих фазах: пересчёт боевых модификаторов
    }

    /// <summary>
    /// Фаза 9D: Обработчик AttackIntentEvent — боевой мост.
    /// Phase 8 ч.3 (2026-09-03): для ranged-интентов — авторитетный гейт
    /// (LOS + стрелы) ДО StartCombat/ExecuteAttack: выстрел сквозь камень
    /// не должен начинать бой и тратить стрелу. Паттерн отклонения — C-5
    /// (AttackRejectedEvent, тост только для игрока — GameWorldController).
    /// Review этап 3 (P1-3): стрела списывается ТОЛЬКО после принятия атаки
    /// CombatService (Accepted) — раньше списывалась до гейтов ExecuteAttack
    /// и терялась при отклонении (каст/не тот ход/не участник).
    /// </summary>
    private void OnAttackIntent(in AttackIntentEvent e)
    {
        if (!_isConfigured) return;

        // Phase 8 ч.3: гейт дальнего боя. Каст в процессе → пропускаем
        // гейт (стрелу НЕ тратим), ExecuteAttack сам отклонит по C-5.
        // Пустой TargetId (легаси авто-выбор) — гейт не нужен: цель
        // резолвится внутри ExecuteAttack, расход не списываем.
        if (e.IsRanged && !string.IsNullOrEmpty(e.TargetId) && !_combatServiceImpl.IsCasting)
        {
            if (!_rangeGate.HasLineOfSight(e.AttackerId, e.TargetId))
            {
                _attackRejectedPub.Publish(new AttackRejectedEvent(
                    e.AttackerId, e.TechniqueId,
                    "нет линии огня — препятствие на пути стрелы"));
                return;
            }
            // Review этап 3 (P1-3): проверяем НАЛИЧИЕ стрел — без списания.
            if (!_rangeGate.HasRangedAmmo(e.AttackerId))
            {
                _attackRejectedPub.Publish(new AttackRejectedEvent(
                    e.AttackerId, e.TechniqueId,
                    "нет стрел — колчан пуст"));
                return;
            }
        }

        if (!_combatService.IsInCombat && !string.IsNullOrEmpty(e.TargetId))
        {
            _combatService.StartCombat(e.AttackerId, e.TargetId);
        }

        AttackAcceptance acceptance = _combatService.ExecuteAttack(
            e.AttackerId, e.TechniqueId, e.TargetId, e.IsRanged, e.PotencyPermil, e.IsCharged);

        // Review этап 3 (P1-3): списание стрелы — ТОЛЬКО после принятия атаки
        // (единая authoritative точка; NPC — безлимит как раньше).
        if (acceptance == AttackAcceptance.Accepted && e.IsRanged)
        {
            _rangeGate.TryConsumeRangedAmmo(e.AttackerId);
        }
    }

    public void Dispose()
    {
        _enemyKilledSubscription?.Dispose();
        _enemyKilledSubscription = null;
        _combatEndedSubscription?.Dispose();
        _combatEndedSubscription = null;
        _equipmentChangedSubscription?.Dispose();
        _equipmentChangedSubscription = null;
        _buffAppliedSubscription?.Dispose();
        _buffAppliedSubscription = null;
        _buffRemovedSubscription?.Dispose();
        _buffRemovedSubscription = null;
        _attackIntentSubscription?.Dispose();
        _attackIntentSubscription = null;
    }
}

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
using CultivationGame.Modules.Combat.Data;

namespace CultivationGame.Modules.Combat;

/// <summary>
/// Точка входа модуля боя.
/// Инициализирует сервисы конфигурацией и подписывается на события.
/// </summary>
public class CombatModule : IModule
{
    // === Зависимости (DI через интерфейсы Core) ===
    [Inject] private readonly ICombatService _combatService = null!;
    [Inject] private readonly CombatService _combatServiceImpl = null!;
    [Inject] private readonly IDamageService _damageService = null!;
    [Inject] private readonly TechniqueService _techniqueService = null!;
    [Inject] private readonly TechniqueChargeService _techniqueChargeService = null!;
    [Inject] private readonly CombatAIService _combatAIService = null!;
    [Inject] private readonly CombatLootService _combatLootService = null!;
    [Inject] private readonly ITimeService _timeService = null!;

    // P2-8.3 FIX: IBodyDataProvider для обновления _cachedEnemyHpRatio
    [Inject] private readonly IBodyDataProvider _bodyDataProvider = null!;

    // Подписка на события
    [Inject] private readonly ISubscriber<EnemyKilledEvent> _enemyKilledSub = null!;
    [Inject] private readonly ISubscriber<CombatEndedEvent> _combatEndedSub = null!;
    [Inject] private readonly ISubscriber<EquipmentChangedEvent> _equipmentChangedSub = null!;
    [Inject] private readonly ISubscriber<BuffAppliedEvent> _buffAppliedSub = null!;
    [Inject] private readonly ISubscriber<BuffRemovedEvent> _buffRemovedSub = null!;
    [Inject] private readonly ISubscriber<DamageAppliedEvent> _damageAppliedSub = null!;
    [Inject] private readonly ISubscriber<AttackIntentEvent> _attackIntentSub = null!;

    // === Состояние ===
    private CombatConfig? _config;
    private bool _isConfigured;
    private IDisposable? _enemyKilledSubscription;
    private IDisposable? _combatEndedSubscription;
    private IDisposable? _equipmentChangedSubscription;
    private IDisposable? _buffAppliedSubscription;
    private IDisposable? _buffRemovedSubscription;
    private IDisposable? _damageAppliedForHpSubscription;
    private IDisposable? _attackIntentSubscription;

    // CMB-C01: кэш HP ratio из событий (вместо хардкода 0.5f)
    private float _cachedEnemyHpRatio = 1.0f;

    public string ModuleName => "Combat";

    /// <summary>
    /// Установить конфигурацию модуля.
    /// </summary>
    public void SetConfig(CombatConfig config)
    {
        _config = config;
        _isConfigured = true;
    }

    public void Start()
    {
        // === Конфигурация сервисов ===
        if (_isConfigured && _config != null)
        {
            _combatServiceImpl.Configure(_config);

            // Инициализация AI
            _combatAIService.Initialize("enemy", AIPersonality.CreateBalanced());
        }

        // === Подписка на кросс-модульные события ===
        _enemyKilledSubscription = _enemyKilledSub.Subscribe(OnEnemyKilled);
        _combatEndedSubscription = _combatEndedSub.Subscribe(OnCombatEnded);

        _equipmentChangedSubscription = _equipmentChangedSub.Subscribe(OnEquipmentChanged);
        _buffAppliedSubscription = _buffAppliedSub.Subscribe(OnBuffApplied);
        _buffRemovedSubscription = _buffRemovedSub.Subscribe(OnBuffRemoved);

        // Спринт 8 C11: подписка CombatService на DamageAppliedEvent (прерывание каста)
        _combatServiceImpl.SubscribeToDamageApplied(_damageAppliedSub);

        // P2-8.3 FIX: подписка на DamageAppliedEvent для обновления _cachedEnemyHpRatio
        _damageAppliedForHpSubscription = _damageAppliedSub.Subscribe(OnDamageAppliedForHpRatio);

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

        // AI-ход (только в EnemyTurn)
        if (_combatService.IsInCombat && _combatService.CurrentStage == CombatStage.EnemyTurn)
        {
            var action = _combatAIService.UpdateAI(delta, _cachedEnemyHpRatio);
            ExecuteAIAction(action);
        }
    }

    /// <summary>
    /// Выполнить действие AI.
    /// </summary>
    private void ExecuteAIAction(AIAction action)
    {
        switch (action)
        {
            case AIAction.Attack:
                _combatService.ExecuteAttack("enemy", "basic_attack");
                break;
            case AIAction.UseTechnique:
                _combatService.ExecuteAttack("enemy", "technique_npc");
                break;
            case AIAction.Defend:
                var defense = _combatAIService.ChooseDefense();
                _combatService.ExecuteDefense("enemy", defense);
                break;
            case AIAction.Flee:
                // В будущих фазах: логика побега
                break;
        }
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
    /// Обработчик CombatEndedEvent — деактивация AI.
    /// </summary>
    private void OnCombatEnded(in CombatEndedEvent e)
    {
        _combatAIService.Deactivate();
        _cachedEnemyHpRatio = 1.0f;
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
    /// P2-8.3 FIX: Обработчик DamageAppliedEvent — обновление кэша HP ratio врага.
    /// </summary>
    private void OnDamageAppliedForHpRatio(in DamageAppliedEvent e)
    {
        if (!_combatService.IsInCombat) return;
        if (e.TargetId != _combatService.CurrentTargetId) return;

        int currentHP = _bodyDataProvider.GetCurrentHealth(e.TargetId);
        int maxHP = _bodyDataProvider.GetMaxHealth(e.TargetId);

        if (maxHP > 0)
        {
            _cachedEnemyHpRatio = (float)currentHP / maxHP;
        }
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
    /// </summary>
    private void OnAttackIntent(in AttackIntentEvent e)
    {
        if (!_isConfigured) return;

        if (!_combatService.IsInCombat && !string.IsNullOrEmpty(e.TargetId))
        {
            _combatService.StartCombat(e.AttackerId, e.TargetId);
        }

        _combatService.ExecuteAttack(e.AttackerId, e.TechniqueId, e.TargetId, e.IsRanged);
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
        _damageAppliedForHpSubscription?.Dispose();
        _damageAppliedForHpSubscription = null;
        _attackIntentSubscription?.Dispose();
        _attackIntentSubscription = null;
    }
}

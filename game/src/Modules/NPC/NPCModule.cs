#nullable enable
// Создано: 2026-05-09 15:30:00 UTC
// Точка входа модуля NPC.
// Migrated from Ai-game3 (Unity+VContainer+MessagePipe) to Ai-game4 (Godot+DI+EventBus) 2026-08-15:
//   - using MessagePipe → using CultivationGame.Core.Events
//   - using VContainer/VContainer.Unity → using CultivationGame.Core.DI / CultivationGame.Core.Interfaces
//   - Handler signature: void OnXxx(XxxEvent e) → void OnXxx(in XxxEvent e)
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.NPC;

/// <summary>
/// Точка входа модуля NPC.
/// Инициализирует сервисы конфигурацией и обрабатывает тики.
/// </summary>
public class NPCModule : IModule
{
    [Inject] private readonly NPCService _npcServiceImpl = null!;
    [Inject] private readonly NPCAIService _aiService = null!;
    [Inject] private readonly NPCMovementService _movementService = null!;
    [Inject] private readonly NPCCombatAdapter _combatAdapter = null!;
    [Inject] private readonly NPCRelationshipService _relationshipService = null!;
    [Inject] private readonly NPCSpawnerService _spawnerService = null!;
    [Inject] private readonly NPCQiRegenService _qiRegenService = null!;
    [Inject] private readonly NPCVisualService _visualService = null!;

    [Inject] private readonly ISubscriber<NPCAIStateChangedEvent> _aiStateChangedSub = null!;
    private IDisposable? _aiStateChangedSubscription;

    [Inject] private readonly ISubscriber<YearChangedEvent> _yearChangedSub = null!;
    private IDisposable? _yearChangedSubscription;

    [Inject] private readonly IPublisher<NPCDeathEvent> _npcDeathPub = null!;

    // IMPL-3: Config injected via DI (replaces obsolete SetConfig()).
    [Inject] private readonly NPCConfig _config = null!;
    private bool _isConfigured;

    public string ModuleName => "NPC";

    public void Start()
    {
        // IMPL-3: Config injected via DI. Inner services also receive it via constructor injection.
        _isConfigured = true;

        _npcServiceImpl.Initialize();
        _relationshipService.Initialize();
        _aiService.Initialize();
        _combatAdapter.Initialize();
        _movementService.Initialize();
        _spawnerService.Initialize();
        _qiRegenService.Initialize();
        _visualService.Initialize();

        _aiStateChangedSubscription = _aiStateChangedSub.Subscribe(OnAIStateChanged);
        _yearChangedSubscription = _yearChangedSub.Subscribe(OnYearChanged);
    }

    public void Tick(int tickCount)
    {
        if (!_isConfigured) return;

        _aiService.Tick();
        _movementService.ProcessMovement();
        _visualService.UpdateVisualPositions();
    }

    public void Dispose()
    {
        _npcServiceImpl.Dispose();
        _relationshipService?.Dispose();
        _aiService?.Dispose();
        _combatAdapter?.Dispose();
        _movementService?.Dispose();
        _spawnerService?.Dispose();
        _qiRegenService?.Dispose();
        _visualService?.Dispose();

        _aiStateChangedSubscription?.Dispose();
        _aiStateChangedSubscription = null;
        _yearChangedSubscription?.Dispose();
        _yearChangedSubscription = null;
    }

    /// <summary>
    /// NPC-E01 FIX: Обработка смены AI-состояния.
    /// EventBus handler signature: void OnXxx(in XxxEvent e).
    /// </summary>
    private void OnAIStateChanged(in NPCAIStateChangedEvent e)
    {
        if (e.NewState == NPCAIState.Attacking)
        {
            var targetId = _npcServiceImpl.GetNPCState(e.NpcId)?.TargetId;
            if (!string.IsNullOrEmpty(targetId))
                _combatAdapter.StartAttack(e.NpcId, targetId);
        }
    }

    /// <summary>
    /// Задача 2.6: Обработчик смены года — NPC стареют.
    /// </summary>
    private void OnYearChanged(in YearChangedEvent e)
    {
        foreach (var state in _npcServiceImpl.GetAllStates())
        {
            if (!state.IsAlive) continue;

            state.Age++;

            if (state.MaxLifespan > 0 && state.Age >= state.MaxLifespan)
            {
                state.IsAlive = false;
                state.CurrentHealth = 0;
                _npcDeathPub.Publish(new NPCDeathEvent(state.NpcId, "old_age"));
            }
        }
    }
}

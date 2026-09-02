#nullable enable
// Создано: 2026-05-09 15:30:00 UTC
// Точка входа модуля NPC.
// Migrated from Ai-game3 (Unity+VContainer+MessagePipe) to Ai-game4 (Godot+DI+EventBus) 2026-08-15:
//   - using MessagePipe → using CultivationGame.Core.Events
//   - using VContainer/VContainer.Unity → using CultivationGame.Core.DI / CultivationGame.Core.Interfaces
//   - Handler signature: void OnXxx(XxxEvent e) → void OnXxx(in XxxEvent e)
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using Vector2 = CultivationGame.Core.Data.Position2D;

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

    // GROUP-SPAWN: групповой сервис — надстройка над индивидуальным AI.
    // Tick() обновляет CurrentGroupTarget для участников групп; NPCMovementService
    // читает это поле как overlay (приоритет над индивидуальным AI).
    [Inject] private readonly INPCGroupService _groupService = null!;

    [Inject] private readonly ISubscriber<NPCAIStateChangedEvent> _aiStateChangedSub = null!;
    private IDisposable? _aiStateChangedSubscription;

    [Inject] private readonly ISubscriber<YearChangedEvent> _yearChangedSub = null!;
    [Inject] private readonly IPublisher<AttackIntentEvent> _attackIntentPub = null!;
    [Inject] private readonly ISubscriber<NPCDeathEvent> _npcDeathSub = null!;
    private IDisposable? _npcDeathSubscription;
    [Inject] private readonly IEquipmentGenerator _equipmentGenerator = null!;
    [Inject] private readonly IGroundItemService _groundItems = null!;
    [Inject] private readonly ITimeService _timeService = null!;
    private IDisposable? _yearChangedSubscription;

    [Inject] private readonly IPublisher<NPCDeathEvent> _npcDeathPub = null!;

    // IMPL-3: Config injected via DI (replaces obsolete SetConfig()).
    [Inject] private readonly NPCConfig _config = null!;
    private bool _isConfigured;

    // M2 (2026-09-03): кэш позиции игрока для проверки дистанции NPC→игрок
    // в ProcessNpcAttacks (паттерн NPC-B05 из NPCMovementService).
    [Inject] private readonly ISubscriber<PlayerPositionChangedEvent> _playerPosSub = null!;
    private IDisposable? _playerPosSubscription;
    private Vector2 _playerPosition = Vector2.Zero;

    public string ModuleName => "NPC";

    /// NPC attack cooldown (seconds of game time).
    private const float NpcAttackCooldownSec = 1.6f;

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
        _npcDeathSubscription = _npcDeathSub.Subscribe(OnNPCDeathForLoot);
        // M2: позиция игрока → кэш (дистанция атаки NPC→игрок).
        _playerPosSubscription = _playerPosSub.Subscribe(OnPlayerPositionChanged);
    }

    /// <summary>
    /// NPC attack loop (2026-08-22, физический прототип): NPC в состоянии
    /// Attacking с целью в радиусе атаки публикует AttackIntentEvent с
    /// кулдауном. CombatModule выполняет полный damage pipeline.
    /// </summary>
    private readonly Dictionary<string, float> _npcAttackTimers = new();

    public void Tick(int tickCount)
    {
        if (!_isConfigured) return;

        _aiService.Tick();
        // GROUP-SPAWN: обновляем цели групп (CurrentGroupTarget для участников).
        // Вызывается после AI (чтобы видеть смену состояний NPC) и до движения
        // (чтобы NPCMovementService видел актуальные групповые цели).
        _groupService?.Tick(tickCount);
        _movementService.ProcessMovement();
        _visualService.UpdateVisualPositions();
        ProcessNpcAttacks();
    }

    private void ProcessNpcAttacks()
    {
        if (_npcServiceImpl == null || _attackIntentPub == null) return;
        float now = _timeService?.TotalTime ?? 0f;

        foreach (var state in _npcServiceImpl.GetAllStates())
        {
            if (!state.IsAlive || state.AIState != NPCAIState.Attacking) continue;
            if (string.IsNullOrEmpty(state.TargetId)) continue;

            var target = _npcServiceImpl.GetNPCState(state.TargetId);
            /// Цель — NPC или игрок: дистанция по тайлам (Position2D).
            int dx, dy;
            if (target != null)
            {
                dx = System.Math.Abs(target.Position.X - state.Position.X);
                dy = System.Math.Abs(target.Position.Y - state.Position.Y);
            }
            else
            {
                // M2 (2026-09-03): цель — игрок: дистанция из кэша
                // PlayerPositionChangedEvent (паттерн NPC-B05). РАНЬШЕ
                // dx=dy=0 «всегда рядом» — NPC в Attacking бил игрока с
                // ЛЮБОЙ дистанции (застрял у препятствия/aggro издалека).
                dx = System.Math.Abs((int)_playerPosition.X - state.Position.X);
                dy = System.Math.Abs((int)_playerPosition.Y - state.Position.Y);
            }
            int dist = System.Math.Max(dx, dy);
            if (dist > 2) continue; /// вне физической досягаемости

            if (!_npcAttackTimers.TryGetValue(state.NpcId, out var last)) last = -999f;
            if (now - last < NpcAttackCooldownSec) continue;

            _npcAttackTimers[state.NpcId] = now;
            _attackIntentPub.Publish(new AttackIntentEvent(
                state.NpcId, state.TargetId, "npc_strike", false));
        }
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

        _npcDeathSubscription?.Dispose();
        _npcDeathSubscription = null;
        _aiStateChangedSubscription?.Dispose();
        _aiStateChangedSubscription = null;
        _yearChangedSubscription?.Dispose();
        _yearChangedSubscription = null;
        _playerPosSubscription?.Dispose();
        _playerPosSubscription = null;
    }

    /// <summary>
    /// Этап 3 (2026-08-22): смерть NPC → лут из EquipmentGenerator падает
    /// на землю у места смерти (1-2 предмета, подбор — E).
    /// </summary>
    private void OnNPCDeathForLoot(in NPCDeathEvent e)
    {
        if (_equipmentGenerator == null || _groundItems == null) return;

        var state = _npcServiceImpl.GetNPCState(e.NpcId);
        float px = (state?.Position.X ?? 25) * GameConstants.TILE_PIXELS + GameConstants.TILE_PIXELS / 2f;
        float py = (state?.Position.Y ?? 25) * GameConstants.TILE_PIXELS + GameConstants.TILE_PIXELS / 2f;

        int level = 1 + (int)(state?.SubLevel ?? 0);
        try
        {
            int drops = (e.NpcId.GetHashCode() & 1) == 0 ? 1 : 2;
            for (int i = 0; i < drops; i++)
            {
                var item = _equipmentGenerator.GenerateRandom(
                    System.Math.Clamp(level + i, 1, 9), seed: e.NpcId.GetHashCode() + i);
                _groundItems.DropItem(item.ItemId, 1, px + i * 20f - 10f, py + i * 12f - 6f);
            }
            Console.WriteLine($"[NPCLoot] {e.NpcId} dropped {drops} item(s) at ({px:F0},{py:F0})");
        }
        catch (System.Exception ex)
        {
            Console.WriteLine($"[NPCLoot] failed for {e.NpcId}: {ex.Message}");
        }
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
    /// M2 (2026-09-03): кэш позиции игрока (тайлы) для проверки дистанции
    /// атаки NPC→игрок в ProcessNpcAttacks.
    /// </summary>
    private void OnPlayerPositionChanged(in PlayerPositionChangedEvent e)
    {
        _playerPosition = new Vector2((int)e.X, (int)e.Y);
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

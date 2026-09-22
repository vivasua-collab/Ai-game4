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
// R17 (аудит-0911 E-1): + IWorldResettable — сброс NPC-домена входит в
// единый контракт world-scoped сброса (ResolveAll<IWorldResettable> в
// WorldDomainResetPhase/GameSession.LoadGame) вместо точечного вызова
// из NpcDomainResetPhase/LoadGame.
public class NPCModule : IModule, IWorldResettable
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
    [Inject] private readonly ITimeService _timeService = null!;
    private IDisposable? _yearChangedSubscription;

    [Inject] private readonly IPublisher<NPCDeathEvent> _npcDeathPub = null!; // R20: old_age-ветка удалена; publisher оставлен для будущих осознанных смертей (болезни/проклятия)

    // R13 FULL-LOOT (2026-09-10): трупы-контейнеры лута. CorpseService
    // подписан на NPCDeathEvent сам (Initialize ниже) — снапшот экипировки/
    // инвентаря/камней в CorpseData на месте смерти. Замена Этапа 3: раньше
    // OnNPCDeathForLoot ронял 1-2 СЛУЧАЙНЫХ предмета (нечестно — реальный
    // инвентарь NPC исчезал); теперь полный лут лежит в трупе до обыска (E).
    [Inject] private readonly CorpseService _corpseService = null!;

    // R13 «NPC спаун через генерацию»: генератор состава населения.
    // R14 (2026-09-10): внутрисессионное восполнение УДАЛЕНО — пока игрок
    // в локации, новые NPC не приходят; только ивенты (TrySpawnEventNpc:
    // караван/набег) — точка входа для будущего event-pipeline.
    [Inject] private readonly NPCSpawnCompositionService _spawnComposition = null!;

    // IMPL-3: Config injected via DI (replaces obsolete SetConfig()).
    [Inject] private readonly NPCConfig _config = null!;
    private bool _isConfigured;

    // M2 (2026-09-03): кэш позиции игрока для проверки дистанции NPC→игрок
    // в ProcessNpcAttacks (паттерн NPC-B05 из NPCMovementService).
    [Inject] private readonly ISubscriber<PlayerPositionChangedEvent> _playerPosSub = null!;
    private IDisposable? _playerPosSubscription;
    private Vector2 _playerPosition = Vector2.Zero;

    // Phase 8 ч.2 (2026-09-03): дальнобойные NPC (лук/арбалет) бьют
    // с дистанции оружия вместо melee-гейта dist>2.
    [Inject] private readonly IEquipmentDataProvider _equipmentDataProvider = null!;
    // R21-2 (attack-speed): готовность удара NPC — из CombatService
    // (куладаун 1.6с удалён: темп задаёт оружие NPC × AGI).
    [Inject] private readonly ICombatService? _combatServiceForReadiness = null;

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
        // R13: подписка CorpseService на NPCDeathEvent (трупы-контейнеры).
        _corpseService?.Initialize();
        // M2: позиция игрока → кэш (дистанция атаки NPC→игрок).
        _playerPosSubscription = _playerPosSub.Subscribe(OnPlayerPositionChanged);
    }

    /// <summary>
    /// NPC attack loop (2026-08-22, физический прототип): NPC в состоянии
    /// Attacking с целью в радиусе атаки публикует AttackIntentEvent с
    /// кулдауном. CombatModule выполняет полный damage pipeline.
    /// </summary>
    private readonly Dictionary<string, float> _npcAttackTimers = new(); // R21-2: не используется (readiness) — оставлен для сейв-стабильности поля
    // P1-3: буфер снапшота состояний для attack-loop (каждый игровой тик).
    private readonly List<NPCState> _attackStates = new();

    /// <summary>
    /// R13-аудит (P2-4) + R14-аудит (P2-1): сброс NPC-домена при пересборке
    /// мира в том же процессе. R17: вызывается через IWorldResettable из
    /// WorldDomainResetPhase (фаза 0, NewGame — до спавн-фаз 6/7/8) и
    /// GameSession.LoadGame (до RestoreState из сейва — фазы идут ПОСЛЕ
    /// восстановления). Чистит: реестр NPC + per-entity провайдеры + баффы +
    /// якоря блуждания + отношения (полный путь DespawnNPC), группы.
    /// Трупы: P1-4 (аудит 09.22) — CorpseService теперь сам IWorldResettable
    /// (ResolveAll сбрасывает его напрямую), явный вызов здесь УДАЛЁН —
    /// один владелец контракта, двойной сброс не нужен.
    /// </summary>
    public void ResetWorld()
    {
        _spawnerService.ResetWorld();
        _groupService?.ResetWorld();
    }

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

        // R14: поддержание населения в тиках УДАЛЕНО — в сессии (игрок в
        // локации) новые NPC не генерируются; естественное восстановление —
        // при (пере)сборке локации (TRANSITION_SYSTEM §5.3 «таймер памяти»).
        // Единственная внутрисессионная точка входа — TrySpawnEventNpc
        // (караван/набег), вызывается event-pipeline, не тиками.

        // R13: TTL-очистка трупов (старые тела исчезают через 1 игровой день).
        _corpseService?.RemoveOldCorpses(CorpseTtlGameSeconds);
    }

    /// <summary>Время жизни трупа (игровые секунды) до естественного исчезновения.</summary>
    private const float CorpseTtlGameSeconds = 1440f; // 1 игровой день (тик = 1 мин → 1440 тиков)

    private void ProcessNpcAttacks()
    {
        if (_npcServiceImpl == null || _attackIntentPub == null) return;
        float now = _timeService?.TotalTime ?? 0f;

        // P1-3 (аудит 09.22): zero-GC hot path — персистентный буфер
        // снапшота вместо аллоцирующего GetAllStates() на каждом тике.
        _npcServiceImpl.CopyStatesTo(_attackStates);
        for (int i = 0; i < _attackStates.Count; i++)
        {
            var state = _attackStates[i];
            if (!state.IsAlive || state.AIState != NPCAIState.Attacking) continue;
            if (string.IsNullOrEmpty(state.TargetId)) continue;

            var target = _npcServiceImpl.GetNPCState(state.TargetId);

            // AUDIT-0921 B4 (P1): цель — МЁРТВЫЙ NPC → атакующий зависал в
            // Attacking навсегда (удары по трупу отклонялись пайплайном,
            // AIState/TargetId никто не сбрасывал). Труп — не противник:
            // сброс цели + Wandering; новый выбор (месть/угрозы) — обычным
            // путём EvaluateAndDecide. Игрок-цель не трогаем (его смерть —
            // отдельный контур PlayerService).
            if (target != null && !target.IsAlive)
            {
                state.TargetId = null;
                _npcServiceImpl.SetAIState(state.NpcId, NPCAIState.Wandering);
                continue;
            }

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

            // Phase 8 ч.2 (2026-09-03): дальность атаки — из экипированного
            // оружия NPC (фаза 9A: AttackRange ≤ 2 = ближнее, > 2 = лук/арбалет).
            // Раньше жёсткий гейт dist > 2: лучник с луком (range 18) мог
            // бить только «в упор» — дальнее оружие не имело смысла.
            var npcWeapon = _equipmentDataProvider?.GetEquipped(state.NpcId, EquipmentSlot.WeaponMain);
            bool npcIsRanged = npcWeapon != null && npcWeapon.AttackRange > 2;
            int maxAttackRange = npcIsRanged ? npcWeapon!.AttackRange : 2;
            if (dist > maxAttackRange) continue; /// вне досягаемости оружия

            // R21-2 (attack-speed): NPC шлёт интент при ГОТОВНОСТИ удара
            // (CombatService.IsAttackReady — оружие NPC задаёт темп:
            // кинжал 1430‰/тик, меч 1100, двуруч ~825 × AGI). Куладаун 1.6с
            // и жёсткое чередование ходов удалены — удары независимы.
            if (_combatServiceForReadiness != null
                && !_combatServiceForReadiness.IsAttackReady(state.NpcId)) continue;

            _npcAttackTimers[state.NpcId] = now; // R21-2: только для диагностики
            // Phase 8 ч.2: isRanged → CombatService резолвит RangedProjectile + §4.2
            _attackIntentPub.Publish(new AttackIntentEvent(
                state.NpcId, state.TargetId, "npc_strike", npcIsRanged));
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
        _corpseService?.Dispose();

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
    /// ЗАМЕНЕНО (R13, 2026-09-10): CorpseService создаёт на месте смерти
    /// труп-контейнер с ПОЛНЫМ содержимым NPC (экипировка + инвентарь +
    /// духовные камни) — обыск через E (LootWindow), full loot — кнопкой
    /// «Забрать всё». Честная полнота лута вместо случайных 1-2 предметов.
    /// Метод удалён; событие NPCDeathEvent обрабатывает CorpseService.
    /// </summary>

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
                // Review этап 3 (P2-5): MarkNpcCombatStarted (было StartAttack) —
                // помечает NPC бойцом; удар пойдёт через ProcessNpcAttacks.
                _combatAdapter.MarkNpcCombatStarted(e.NpcId, targetId);
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
    /// R20 (баг №3, запрос 09_09_22_40): смерть от старости УДАЛЕНА —
    /// правило пользователя: «NPC на карте не должны умирать самостоятельно,
    /// только при смерти в бою или от проклятий и ядов». Возраст продолжает
    /// расти (будущие системы — ветвость/болезни — должны быть ОСОЗНАННЫМИ
    /// событиями с репортом, не тихой смертью на тике календаря).
    /// Потребители «✝ ушёл из мира (старость)» (KillFeed/EventLog) сохранены.
    /// </summary>
    private void OnYearChanged(in YearChangedEvent e)
    {
        foreach (var state in _npcServiceImpl.GetAllStates())
        {
            if (!state.IsAlive) continue;
            state.Age++;
        }
    }
}

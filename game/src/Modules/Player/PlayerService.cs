#nullable enable
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Helpers;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Player;

/// <summary>
/// PlayerService — thin facade over the player's CharacterData, position
/// and lifecycle (sleep, stance, death). Implements <see cref="IPlayerService"/>.
///
/// ARCHITECTURE: cross-module interactions go through EventBus only.
/// No direct injection of IQiService/IBodyService/ICombatService. Qi level
/// is cached via <see cref="QiChangedEvent"/> subscription.
/// </summary>
public sealed class PlayerService : IPlayerService, IDisposable
{
    [Inject] private readonly IPublisher<PlayerDeathEvent> _deathPub = null!;
    [Inject] private readonly IPublisher<PlayerReviveEvent> _revivePub = null!;
    [Inject] private readonly IPublisher<PlayerPositionChangedEvent> _positionPub = null!;
    [Inject] private readonly ISubscriber<QiChangedEvent> _qiChangedSub = null!;
    [Inject] private readonly ISubscriber<BodyCriticalEvent> _bodyCriticalSub = null!;
    [Inject] private readonly IBodyService _bodyService = null!;

    private readonly CharacterData _data = new();
    private readonly List<string> _assignedTechniques = new();
    private bool _spawned;
    // AUDIT-0911 BOD-2: честный флаг «смерть объявлена» — вместо гейта
    // `!IsAlive` в Die() (при vital-смерти IsAlive уже false → PlayerDeathEvent
    // никогда бы не опубликовался; флаг сбрасывается Revive()).
    private bool _deathAnnounced;

    // Cached cultivation info mirrored from QiChangedEvent (zero polling).
    private CultivationLevel _cachedCultivationLevel = CultivationLevel.None;
    private long _cachedCurrentQi;

    private IDisposable? _qiChangedToken;
    private IDisposable? _bodyCriticalToken;

    /// <summary>Internal — exposed for module-internal spawn logic.</summary>
    public bool IsSpawned => _spawned;

    /// <summary>Internal — direct access to underlying CharacterData.</summary>
    public CharacterData Data => _data;

    // === IPlayerService ===

    public string PlayerId => _data.Id;
    public Position2D Position => _data.Position;
    /// <summary>
    /// IsAlive delegates to BodyService (Q4: единая HP система).
    /// AUDIT-0911 BOD-2/PLR-1 FIX: единое правило тел (как для NPC/зверей
    /// D8-фикс): vital Head/Heart RedHP ≤ 0 = мёртв. Раньше — «сердце не
    /// ампутировано» (сердце НЕ ампутируется по дизайну тел — IsVital без
    /// порога ампутации) → IsAlive был ВСЕГДА true → игрок «жил» с
    /// разрушенной головой. Fallback _data.Health — до инициализации тела.
    /// </summary>
    public bool IsAlive
    {
        get
        {
            if (_bodyService != null && !string.IsNullOrEmpty(_bodyService.EntityId))
            {
                return _bodyService.IsEntityAlive(_bodyService.EntityId);
            }
            // Fallback before BodyService initialized.
            return _data.Health > 0f;
        }
    }
    public bool IsSleeping => SleepState != PlayerSleepState.Awake;
    public PlayerSleepState SleepState { get; private set; } = PlayerSleepState.Awake;
    public PlayerStance Stance { get; private set; } = PlayerStance.Normal;
    public CultivationLevel CultivationLevel => _cachedCultivationLevel;
    public long GetCurrentQi() => _cachedCurrentQi;

    public IReadOnlyList<string> GetAssignedTechniques() => _assignedTechniques;

    public void StartSleep(float hours)
    {
        if (!IsAlive) return;
        if (SleepState != PlayerSleepState.Awake) return;
        SleepState = PlayerSleepState.FallingAsleep;
        Console.WriteLine($"[PlayerService] StartSleep({hours}h)");
    }

    public void WakeUp()
    {
        if (SleepState == PlayerSleepState.Awake) return;
        SleepState = PlayerSleepState.Awake;
        Console.WriteLine("[PlayerService] WakeUp");
    }

    public void SetPosition(Position2D position)
    {
        if (!_spawned) return;
        var old = _data.Position;
        _data.Position = position;
        if (old != position)
        {
            _positionPub.Publish(new PlayerPositionChangedEvent(position.X, position.Y));
        }
    }

    public void Tick(float deltaTime)
    {
        if (!_spawned || !IsAlive) return;
        // V1: sleep state machine — FallingAsleep → Sleeping after a tick.
        if (SleepState == PlayerSleepState.FallingAsleep)
        {
            SleepState = PlayerSleepState.Sleeping;
        }
    }

    // === Module-internal helpers ===

    /// <summary>Spawn the player avatar. Called by PlayerSpawnPhase / PlayerModule.Start.</summary>
    /// <remarks>
    /// Idempotent: if the player is already spawned, this is a no-op.
    /// Prevents double-spawn leak when both PlayerModule.Start() and
    /// PlayerSpawnPhase.ExecuteAsync() call Spawn (audit issue #3).
    /// </remarks>
    public void Spawn(Position2D position)
    {
        if (_spawned)
        {
            Console.WriteLine($"[PlayerService] Spawn ignored — already spawned @ {_data.Position}");
            return;
        }

        _data.Id = "player_0";
        _data.Name = "Практик";
        _data.Position = position;
        _data.Health = 100f;  // Fallback only; IsAlive delegates to BodyService.
        _data.CultivationLevel = 1;
        _data.CurrentQi = 0;
        _data.Age = 16;
        _spawned = true;
        _qiChangedToken = _qiChangedSub.Subscribe(OnQiChanged);
        _bodyCriticalToken = _bodyCriticalSub.Subscribe(OnBodyCritical);
        // Publish initial position so NPC AI/movement knows where player is.
        _positionPub.Publish(new PlayerPositionChangedEvent(position.X, position.Y));
        Console.WriteLine($"[PlayerService] Player spawned @ {position}, hp delegated to BodyService");
    }

    /// <summary>Snap player to a tile (used by PlayerModule.Tick for tile-grid movement).</summary>
    public void MoveTo(int x, int y) => SetPosition(new Position2D(x, y));

    public void SetFacing(Direction dir) => _data.Facing = dir;

    public void Die(string cause)
    {
        // AUDIT-0911 BOD-2: флаг вместо `!IsAlive` — при vital-смерти
        // (Head RedHP→0) IsAlive УЖЕ false, старый гейт глотал PlayerDeathEvent.
        if (_deathAnnounced) return;
        _deathAnnounced = true;
        _data.Health = 0f;
        Stance = PlayerStance.Normal;
        if (SleepState != PlayerSleepState.Awake) WakeUp();
        _deathPub.Publish(new PlayerDeathEvent(cause));
        Console.WriteLine($"[PlayerService] Player died: {cause}");
    }

    public void Revive()
    {
        _deathAnnounced = false;
        _data.Health = 100f;
        Stance = PlayerStance.Normal;
        _revivePub.Publish(new PlayerReviveEvent());
    }

    private void OnQiChanged(in QiChangedEvent e)
    {
        _cachedCurrentQi = e.Current;
        _cachedCultivationLevel = (CultivationLevel)e.CultivationLevel;
    }

    /// <summary>
    /// Handle BodyCriticalEvent — vital part disabled/severed → player dies.
    /// Q4: единая HP система, смерть через BodyService events.
    /// AUDIT-0911 BOD-2/PLR-1 FIX: раньше только Heart+Disabled —
    /// разрушение Head (RedHP→0) игрока НЕ убивало (в отличие от NPC/зверей
    /// после D8-фикса). Единое правило тел: vital Head|Heart × Disabled|Severed.
    /// </summary>
    private void OnBodyCritical(in BodyCriticalEvent e)
    {
        // B1: нормализация ID игрока через PlayerIdResolver.
        // Раньше: hardcoded проверка e.EntityId != _data.Id && e.EntityId != "player"
        // (исторические алиасы "player_0" и "player" — см. PlayerIdResolver).
        if (!PlayerIdResolver.AreSameEntity(e.EntityId, _data.Id)) return;

        // Vital-часть уничтожена/ампутирована = смерть (единое правило тел).
        bool vitalDestroyed = (e.Part == BodyPartType.Heart || e.Part == BodyPartType.Head)
            && (e.State == BodyPartState.Disabled || e.State == BodyPartState.Severed);
        if (vitalDestroyed)
        {
            string partName = e.Part == BodyPartType.Heart ? "Сердце остановлено" : "Голова разрушена";
            Die($"{partName} ({e.HealthRatio:P0} HP)");
        }
    }

    public void Dispose()
    {
        _qiChangedToken?.Dispose();
        _qiChangedToken = null;
        _bodyCriticalToken?.Dispose();
        _bodyCriticalToken = null;
    }
}

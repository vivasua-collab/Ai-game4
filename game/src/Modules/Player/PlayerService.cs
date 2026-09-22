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
public sealed class PlayerService : IPlayerService, ISaveable, IWorldResettable, IDisposable
{
    [Inject] private readonly IPublisher<PlayerDeathEvent> _deathPub = null!;
    [Inject] private readonly IPublisher<PlayerReviveEvent> _revivePub = null!;
    [Inject] private readonly IPublisher<PlayerPositionChangedEvent> _positionPub = null!;
    [Inject] private readonly ISubscriber<QiChangedEvent> _qiChangedSub = null!;
    [Inject] private readonly ISubscriber<BodyCriticalEvent> _bodyCriticalSub = null!;
    [Inject] private readonly IBodyService _bodyService = null!;
    // R35 (Фаза 14 / P1-20): закрепление дельт при пробуждении. StatService —
    // сервис ТОГО ЖЕ модуля (Player) — инъекция через Core-интерфейс
    // корректна (архитектурный запрет касается IQi/IBody/ICombat).
    [Inject] private readonly IStatService? _statService = null;

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

    // === R35 (Фаза 14 / P1-20): сон — реальные часы =====================
    // Прежде StartSleep(hours) принимал часы и НИГДЕ их не хранил; состояние
    // FallingAsleep→Sleeping переключалось в Tick, но PlayerService.Tick
    // никто не вызывал, пробуждения/закрепления не существовало. Теперь:
    // считаем ФАКТИЧЕСКИ проспанные минуты (1 тик = 1 игровая минута,
    // наращивает PlayerModule.Tick), авто-пробуждение по плану, пробуждение
    // (авто или WakeUp) → StatService.ConsolidateSleep(фактические часы).
    private float _plannedSleepHours;
    private int _sleptMinutes;

    public void StartSleep(float hours)
    {
        if (!IsAlive) return;
        if (SleepState != PlayerSleepState.Awake) return;
        _plannedSleepHours = hours > 0 ? hours : 8f;
        _sleptMinutes = 0;
        SleepState = PlayerSleepState.FallingAsleep;
        Console.WriteLine($"[PlayerService] StartSleep({_plannedSleepHours}h)");
    }

    public void WakeUp()
    {
        if (SleepState == PlayerSleepState.Awake) return;
        SleepState = PlayerSleepState.Awake;

        // P1-20: закрепление дельт по канону §6 (минимум 4ч — внутри
        // ConsolidateSleep; меньше — дельта просто сохраняется).
        float sleptHours = _sleptMinutes / 60f;
        if (_statService != null && sleptHours > 0f)
        {
            _statService.ConsolidateSleep(sleptHours);
            Console.WriteLine($"[PlayerService] WakeUp: сон {sleptHours:0.#}ч — дельты закреплены (§6)");
        }
        else
        {
            Console.WriteLine($"[PlayerService] WakeUp (сон {sleptHours:0.#}ч — без закрепления)");
        }
        _plannedSleepHours = 0f;
        _sleptMinutes = 0;
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

        // R35 (P1-20): счёт фактических минут сна (1 тик = 1 игровая минута;
        // Tick вызывается из PlayerModule.Tick). Авто-пробуждение — по
        // запланированным часам (WakeUp сам делает ConsolidateSleep).
        if (SleepState == PlayerSleepState.Sleeping && _plannedSleepHours > 0f)
        {
            _sleptMinutes++;
            if (_sleptMinutes >= _plannedSleepHours * 60f)
            {
                Console.WriteLine($"[PlayerService] Авто-пробуждение: проспано {_sleptMinutes} мин (план {_plannedSleepHours}ч)");
                WakeUp();
            }
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

        // R35 (Фаза 14 / P2-44) ФИКС: оживить ТЕЛО. IsAlive делегирован
        // BodyService (vital Head/Heart RedHP ≤ 0 = мёртв) — прежде Revive
        // менял только _deathAnnounced/_data.Health и «оживал» при фактически
        // мёртвом теле: событие ревайва при IsAlive=false. Vital-части
        // восстанавливаются: Disabled/разрушенная — HealPart (до максимума,
        // событие BodyPartHealedEvent → UI/дебаффы); SEVERED (ампутированная
        // при смертельном уроне) — ReattachPart с её же Max-значениями
        // (событие BodyPartReattachedEvent → SeveredDebuffSystem снимет
        // дебаффы ампутации).
        if (_bodyService != null && !_bodyService.IsEntityAlive(_bodyService.EntityId))
        {
            RestoreVitalPart(BodyPartType.Head);
            RestoreVitalPart(BodyPartType.Heart);
        }

        _revivePub.Publish(new PlayerReviveEvent());
    }

    /// <summary>P2-44: восстановить vital-часть (HealPart или Reattach при ампутации).</summary>
    private void RestoreVitalPart(BodyPartType type)
    {
        if (_bodyService == null) return;
        if (_bodyService.IsPartSevered(type))
        {
            foreach (var p in _bodyService.GetAllParts())
            {
                if (p.Type != type) continue;
                _bodyService.ReattachPart(type, System.Math.Max(1, p.MaxRedHP), System.Math.Max(0, p.MaxBlackHP));
                break;
            }
        }
        else
        {
            _bodyService.HealPart(type, int.MaxValue / 4);
        }
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

    // ══════════════════════════════════════════════════════════════
    // R17 (аудит-0911 E-3): ISaveable — блок "player" (позиция + состояние
    // аватара). Раньше позиция игрока НЕ сохранялась: cold-load ставил
    // игрока на (25,25) — фолбэк PlayerModule.Start, а не место из сейва.
    // ══════════════════════════════════════════════════════════════

    /// <summary>Типизированный state-блок для round-trip десериализации.</summary>
    public sealed class PlayerSaveState
    {
        public bool Spawned;
        public int PosX;
        public int PosY;
        public int Facing;
        public int SleepState;
        public int Stance;
        public bool DeathAnnounced;
    }

    public string SaveKey => "player";
    public Type StateType => typeof(PlayerSaveState);

    public object CaptureState()
    {
        return new PlayerSaveState
        {
            Spawned = _spawned,
            PosX = _data.Position.X,
            PosY = _data.Position.Y,
            Facing = (int)_data.Facing,
            SleepState = (int)SleepState,
            Stance = (int)Stance,
            DeathAnnounced = _deathAnnounced,
        };
    }

    public void RestoreState(object state)
    {
        if (state is not PlayerSaveState data || data == null) return;
        if (!data.Spawned) return; // игрока не было в сейве — не трогаем

        // Сброс мира уже прошёл (IWorldResettable до RestoreState):
        // Spawn заново подписывает QiChanged/BodyCritical и публикует
        // начальную позицию (NPC AI/движение узнают, где игрок).
        var pos = new Position2D(data.PosX, data.PosY);
        if (!_spawned)
        {
            Spawn(pos);
        }
        else
        {
            SetPosition(pos);
        }

        if (Enum.IsDefined(typeof(Direction), data.Facing))
            _data.Facing = (Direction)data.Facing;

        if (Enum.IsDefined(typeof(PlayerSleepState), data.SleepState))
            SleepState = (PlayerSleepState)data.SleepState;
        if (Enum.IsDefined(typeof(PlayerStance), data.Stance))
            Stance = (PlayerStance)data.Stance;
        _deathAnnounced = data.DeathAnnounced;

        Console.WriteLine($"[PlayerService] RestoreState: позиция ({data.PosX},{data.PosY}), " +
                          $"sleep={SleepState}, stance={Stance}, dead={_deathAnnounced}");
    }

    // R17 (E-1): IWorldResettable — пересборка мира = аватар не заспавнен
    // (PlayerSpawnPhase заспавнит заново; LoadGame-путь вернёт из сейва).
    public void ResetWorld()
    {
        _qiChangedToken?.Dispose();
        _qiChangedToken = null;
        _bodyCriticalToken?.Dispose();
        _bodyCriticalToken = null;

        _spawned = false;
        _deathAnnounced = false;
        SleepState = PlayerSleepState.Awake;
        Stance = PlayerStance.Normal;
        _assignedTechniques.Clear();
        _cachedCultivationLevel = CultivationLevel.None;
        _cachedCurrentQi = 0;

        _data.Id = string.Empty;
        _data.Name = string.Empty;
        _data.Position = default;
        _data.Health = 100f;
        _data.CultivationLevel = 1;
        _data.CurrentQi = 0;
        _data.Age = 16;
        _data.Facing = Direction.South;
    }
}

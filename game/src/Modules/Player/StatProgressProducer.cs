#nullable enable
// Создано: 2026-09-22 — R35 (Фаза 14 / P1-20, внешний аудит 09.22 12:00):
// недостающие звенья конвейера развития характеристик. Аудитор: «действие →
// virtual delta → сон → threshold → +1 stat» — в коде фактические
// отсутствовали ПРОДЮСЕРЫ дельт (до) и закрепление при сне (после).
//
// Продюсеры по канону STAT_THRESHOLD_SYSTEM.md §5.1/§5.2 (таблицы
// канонические и обязательны к реализации):
//   • Удар в бою             → STR +0.001
//   • Уклонение              → AGI +0.001
//   • Блок                   → STR +0.001
//   • Получение урона        → VIT +0.001
//   • Использование техники  → INT +0.001
//   • Медитация              → INT +0.01/мин (per-tick, из PlayerModule.Tick)
//
// Правила:
//   • учитывается ТОЛЬКО участие игрока (PlayerIdResolver: "player"/"player_0")
//     — события NPC↔NPC дельт не дают;
//   • инварианты дельт (не-отрицательность, первичные статы, капы 10/10/15/10)
//     держит StatService.AddVirtualDelta (P2-49);
//   • hot-path без аллокаций: подписки полевые, обработчики без new;
//   • тренировки §5.2 (кроме медитации) пока не имеют механики/UI в игре —
//     продюсеры добавятся вместе с ними (зафиксировано в чекпоинте R35).
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.Events;
using CultivationGame.Core.Helpers;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Player;

/// <summary>
/// Продюсер виртуальных дельт характеристик из боевых/медитативных действий
/// игрока (P1-20, Фаза 14 аудита 09.22 12:00). Подписывается на
/// DamageAppliedEvent / TechniqueUsedEvent / MeditationStateChangedEvent;
/// медитационный прирост — per-tick из <see cref="PlayerModule.Tick"/>
/// (1 тик = 1 игровая минута).
/// </summary>
public sealed class StatProgressProducer : IDisposable
{
    // §5.1: приросты боевых действий.
    private const float StrikeDelta = 0.001f;   // удар
    private const float DodgeDelta = 0.001f;    // уклонение
    private const float BlockDelta = 0.001f;    // блок
    private const float DamageTakenDelta = 0.001f; // получение урона
    private const float TechniqueDelta = 0.001f;   // использование техники
    // §5.2: медитация (за игровую минуту).
    private const float MeditationDeltaPerMinute = 0.01f;

    private readonly IStatService _stats;
    private readonly IPlayerService _player;
    private readonly ISubscriber<DamageAppliedEvent> _damageSub;
    private readonly ISubscriber<TechniqueUsedEvent> _techniqueSub;
    private readonly ISubscriber<MeditationStateChangedEvent> _meditationSub;

    private IDisposable? _damageToken;
    private IDisposable? _techniqueToken;
    private IDisposable? _meditationToken;

    // Состояние медитации (зеркало MeditationStateChangedEvent) — per-tick
    // прирост INT наращивается из PlayerModule.Tick.
    private bool _meditationActive;

    public StatProgressProducer(
        IStatService stats,
        IPlayerService player,
        ISubscriber<DamageAppliedEvent> damageSub,
        ISubscriber<TechniqueUsedEvent> techniqueSub,
        ISubscriber<MeditationStateChangedEvent> meditationSub)
    {
        _stats = stats;
        _player = player;
        _damageSub = damageSub;
        _techniqueSub = techniqueSub;
        _meditationSub = meditationSub;
    }

    /// <summary>Подписки (вызывается из PlayerModule.Start — идемпотентно).</summary>
    public void Start()
    {
        if (_damageToken != null) return;
        _damageToken = _damageSub.Subscribe(OnDamageApplied);
        _techniqueToken = _techniqueSub.Subscribe(OnTechniqueUsed);
        _meditationToken = _meditationSub.Subscribe(OnMeditationChanged);
        Console.WriteLine("[StatProgressProducer] Запущен: продюсеры дельт §5.1/§5.2 (удар/уклонение/блок/урон/техника/медитация)");
    }

    /// <summary>
    /// Per-tick прирост медитации (1 тик = 1 игровая минута). Вызывается из
    /// PlayerModule.Tick ТОЛЬКО при активной медитации.
    /// </summary>
    public void TickMeditation()
    {
        if (!_meditationActive) return;
        _stats.AddVirtualDelta(StatType.Intelligence, MeditationDeltaPerMinute);
    }

    /// <summary>Медитация активна? (для PlayerModule — гейт per-tick вызова).</summary>
    public bool IsMeditating => _meditationActive;

    private void OnDamageApplied(in DamageAppliedEvent e)
    {
        // §5.1: только участие ИГРОКА (NPC↔NPC-бой дельт не даёт).
        bool playerIsSource = PlayerIdResolver.AreSameEntity(e.SourceId, _player.PlayerId);
        bool playerIsTarget = PlayerIdResolver.AreSameEntity(e.TargetId, _player.PlayerId);

        if (playerIsSource && (e.Result is CombatAttackResult.Hit or CombatAttackResult.CriticalHit
                               or CombatAttackResult.Kill))
        {
            // Удар в бою (попадание) → STR.
            _stats.AddVirtualDelta(StatType.Strength, StrikeDelta);
        }

        if (playerIsTarget)
        {
            switch (e.Result)
            {
                case CombatAttackResult.Dodge:
                    // Уклонение → AGI.
                    _stats.AddVirtualDelta(StatType.Agility, DodgeDelta);
                    break;
                case CombatAttackResult.Block:
                case CombatAttackResult.Parry:
                    // Блок/парирование (защитное действие) → STR.
                    _stats.AddVirtualDelta(StatType.Strength, BlockDelta);
                    break;
                case CombatAttackResult.Hit:
                case CombatAttackResult.CriticalHit:
                case CombatAttackResult.Kill:
                    // Получение урона → VIT.
                    _stats.AddVirtualDelta(StatType.Vitality, DamageTakenDelta);
                    break;
            }
        }
    }

    private void OnTechniqueUsed(in TechniqueUsedEvent e)
    {
        // §5.1: использование техники → INT (только игрок).
        if (!PlayerIdResolver.AreSameEntity(e.UserId, _player.PlayerId)) return;
        _stats.AddVirtualDelta(StatType.Intelligence, TechniqueDelta);
    }

    private void OnMeditationChanged(in MeditationStateChangedEvent e)
    {
        _meditationActive = e.IsActive;
    }

    public void Dispose()
    {
        _damageToken?.Dispose(); _damageToken = null;
        _techniqueToken?.Dispose(); _techniqueToken = null;
        _meditationToken?.Dispose(); _meditationToken = null;
    }
}

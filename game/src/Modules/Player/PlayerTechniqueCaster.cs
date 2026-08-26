#nullable enable
// Stage 0+1 (2026-08-25, GLM-5.3): каст техник игрока через модель заполнения
// + вариант В (аура держит одну).
//
// Поток:
//   1. TechniqueCastRequestedEvent (Z / клик в панели T):
//      - если аура удерживает технику → Release + FireTechnique (второе нажатие)
//      - иначе → валидация + TechniqueChargeService.StartCharge (первое нажатие)
//   2. TechniqueChargeService.UpdateCharges (тик CombatModule) дренирует Ци по
//      проводимости (chargeRate = conductivity × COMBAT_CHANNEL_MULT × masteryBonus).
//   3. При ChargedQi ≥ QiCost → TechniqueChargeCompletedEvent → OnChargeCompleted:
//      - аура свободна → AuraHoldService.Hold (park, ждёт второго нажатия)
//      - аура занята → FireTechnique немедленно (вариант В: «остальные срабатывают сразу»)
//   4. FireTechnique: TechniqueService.CompleteUse (кулдаун+мастерство) + эффект по типу
//      (Combat → AttackIntentEvent с potency; Healing → лечение; Defense → щит; Movement → рывок; ...).
//
// Источник: checkpoints/08_25_technique_hold_analysis.md (план, подтверждён 2026-08-25).
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Modules.Combat;
using CultivationGame.Modules.Formation;
using CultivationGame.Modules.Generator;

namespace CultivationGame.Modules.Player;

/// <summary>
/// Кастер техник игрока (Stage 0+1 — модель заполнения + аура-задержка).
/// Владеет логикой: запуск зарядки, выпуск заряженной техники, удержание в ауре.
/// </summary>
public sealed class PlayerTechniqueCaster : IDisposable
{
    [Inject] private readonly IPlayerService _player = null!;
    [Inject] private readonly INPCService _npcs = null!;
    [Inject] private readonly IBodyService _body = null!;
    [Inject] private readonly TechniqueService _techniques = null!;
    [Inject] private readonly TechniqueChargeService _chargeService = null!;
    [Inject] private readonly AuraHoldService _aura = null!;
    [Inject] private readonly IFormationService _formations = null!;
    [Inject] private readonly IFormationGeneratorService _formationGenerator = null!;
    [Inject] private readonly IPublisher<AttackIntentEvent> _attackIntentPub = null!;
    [Inject] private readonly IPublisher<QiBufferActivateRequestEvent> _qiBufferActivatePub = null!;
    [Inject] private readonly IPublisher<TechniqueCastResultEvent> _castResultPub = null!;
    [Inject] private readonly ISubscriber<TechniqueCastRequestedEvent> _castRequestSub = null!;
    [Inject] private readonly ISubscriber<TechniqueChargeCompletedEvent> _chargeCompletedSub = null!;
    [Inject] private readonly ISubscriber<FormationActivatedEvent> _formationActivatedSub = null!;

    private const int DashDistanceTiles = 3;
    private const float MinAttackRangeTiles = 2f;

    private static readonly FormationType[] FormationTypePool =
    {
        FormationType.Barrier, FormationType.Amplification,
        FormationType.Suppression, FormationType.Gathering
    };

    private IDisposable? _castRequestToken;
    private IDisposable? _chargeCompletedToken;
    private IDisposable? _formationActivatedToken;
    private readonly Random _formationRng = new();

    public void Start()
    {
        _castRequestToken = _castRequestSub.Subscribe(OnCastRequested);
        _chargeCompletedToken = _chargeCompletedSub.Subscribe(OnChargeCompleted);
        _formationActivatedToken = _formationActivatedSub.Subscribe(OnFormationActivated);
    }

    public void Tick(float deltaTime) { /* нет кадровых задач в самом кастере */ }

    /// <summary>Barrier-формация активирована → Ци-буфер игрока (этап 5, без изменений).</summary>
    private void OnFormationActivated(in FormationActivatedEvent e)
    {
        if (e.CasterId != _player.PlayerId) return;
        if (e.Type != FormationType.Barrier) return;
        long shield = Math.Min(2000, Math.Max(200, _formations.QiPoolMax / 10));
        _qiBufferActivatePub.Publish(new QiBufferActivateRequestEvent(shield, QiBufferMode.Shield));
    }

    // === Входная точка: нажатие Z / клик в панели ===

    private void OnCastRequested(in TechniqueCastRequestedEvent e)
    {
        int mouseX = e.TargetMouseX;
        int mouseY = e.TargetMouseY;

        // Stage 1 (вариант В): если аура удерживает технику → ВЫПУСК (второе нажатие)
        var held = _aura.Current;
        if (held != null)
        {
            var heldTech = _techniques.GetTechnique(held.TechniqueId);
            if (heldTech != null)
            {
                FireTechnique(heldTech, held.PotencyPermil, mouseX, mouseY);
                _aura.Release(); // снимаем с ауры (fire уже применил эффект)
                return;
            }
            // Техника больше не изучена — рассеять
            _aura.Dissipate("technique_forgotten");
        }

        var tech = _techniques.GetTechnique(e.TechniqueId);
        if (tech == null)
        {
            PublishFail(e.TechniqueId, "Техника не изучена");
            return;
        }
        if (tech.Type == TechniqueType.Cultivation)
        {
            PublishFail(e.TechniqueId, "Пассивная техника — работает при медитации");
            return;
        }

        // Stage 0: для Combat — ранняя валидация цели (чтобы не тратить Ци впустую)
        if (tech.Type == TechniqueType.Combat)
        {
            var target = FindTargetInRange(tech);
            if (target == null)
            {
                PublishFail(e.TechniqueId, "Нет цели в радиусе");
                return;
            }
        }

        // Запускаем зарядку (валидация кулдауна/Ци внутри StartCharge)
        bool started = _chargeService.StartCharge(_player.PlayerId, e.TechniqueId, mouseX, mouseY);
        if (!started)
        {
            string reason = _techniques.GetCooldown(e.TechniqueId) > 0
                ? "Перезарядка"
                : (_techniques.FreeSlots(tech.Type) < 0 ? "Нет слота" : "Недостаточно Ци или проводимость");
            PublishFail(e.TechniqueId, reason);
        }
        // Успешный старт → дальнейшее происходит в OnChargeCompleted по тикум
    }

    // === Завершение зарядки (от TechniqueChargeService) ===

    private void OnChargeCompleted(in TechniqueChargeCompletedEvent e)
    {
        if (e.EntityId != _player.PlayerId) return;

        var tech = _techniques.GetTechnique(e.TechniqueId);
        if (tech == null)
        {
            // Техника удалена во время зарядки — игнор
            return;
        }

        // Stage 1 (вариант В): аура свободна → подвязка (park, ждёт второго нажатия);
        // аура занята → немедленный выпуск («остальные срабатывают сразу»).
        if (_aura.IsEmpty)
        {
            bool held = _aura.Hold(e.TechniqueId, e.PotencyPermil, e.ChargedQi, tech.QiCost, tech.Element);
            if (held)
            {
                // Паркуется в ауре — тост/визуал подскажет игроку
                _castResultPub.Publish(new TechniqueCastResultEvent(
                    e.TechniqueId, true, "В ауре",
                    e.TargetMouseX, e.TargetMouseY, e.TargetMouseX, e.TargetMouseY,
                    tech.Type, tech.Element, 2 /* Self/aura visual */));
                return;
            }
        }

        // Аура занята (или Hold провалился) → немедленный выпуск
        FireTechnique(tech, e.PotencyPermil, e.TargetMouseX, e.TargetMouseY);
    }

    // === Применение эффекта техники (бывший switch в OnCastRequested) ===

    /// <summary>
    /// Выпустить технику: CompleteUse (кулдаун+мастерство) + эффект по типу.
    /// potencyPermil: 1000 базовая; >1000 — заряженная (пока всегда 1000 на Stage 0).
    /// </summary>
    private void FireTechnique(LearnedTechnique tech, int potencyPermil, int mouseX, int mouseY)
    {
        int playerX = _player.Position.X;
        int playerY = _player.Position.Y;

        // CompleteUse: кулдаун + мастерство + TechniqueUsedEvent (БЕЗ расхода Ци —
        // уже списано тиками в TechniqueChargeService).
        if (!_techniques.CompleteUse(tech.TechniqueId))
        {
            PublishFail(tech.TechniqueId, _techniques.GetCooldown(tech.TechniqueId) > 0
                ? "Перезарядка" : "Применение невозможно");
            return;
        }

        // Этап 5: бонус урона от активной формации Amplification (пермил, ЗАПРЕТ 3.9)
        _techniques.ExternalDamageBonusPermil = GetAmplificationBonusPermil();

        switch (tech.Type)
        {
            case TechniqueType.Combat:
            {
                var target = FindTargetInRange(tech);
                if (target == null)
                {
                    PublishFail(tech.TechniqueId, "Цель исчезла");
                    return;
                }
                bool isRanged = tech.Subtype is CombatSubtype.RangedProjectile
                                             or CombatSubtype.RangedBeam
                                             or CombatSubtype.RangedAoe;
                _attackIntentPub.Publish(new AttackIntentEvent(
                    _player.PlayerId, target, tech.TechniqueId, isRanged, potencyPermil, isCharged: true));
                PublishSuccess(tech, playerX, playerY, target);
                return;
            }

            case TechniqueType.Healing:
            {
                if (!_hasDamagedParts())
                {
                    PublishFail(tech.TechniqueId, "Тело не ранено");
                    return;
                }
                // potency применяется к лечению (Stage 0: 1000 = ×1.0)
                int healAmount = tech.BaseDamage > 0 ? tech.BaseDamage : 10;
                healAmount = healAmount * potencyPermil / 1000;
                HealDamagedParts(healAmount);
                PublishSuccess(tech, playerX, playerY, null, visualKind: 3);
                return;
            }

            case TechniqueType.Defense:
            {
                // Щит: инвестируем остаточную Ци в буфер (CHARGER_SYSTEM §5.2)
                long invest = Math.Max(50, tech.QiCost);
                invest = invest * potencyPermil / 1000;
                _qiBufferActivatePub.Publish(new QiBufferActivateRequestEvent(invest, QiBufferMode.Shield));
                PublishSuccess(tech, playerX, playerY, null, visualKind: 4);
                return;
            }

            case TechniqueType.Movement:
            {
                int dirX = Math.Sign(mouseX / 1000 - playerX * GameConstants.TILE_PIXELS);
                int dirY = Math.Sign(mouseY / 1000 - playerY * GameConstants.TILE_PIXELS);
                if (dirX == 0 && dirY == 0) { dirX = 1; }
                _player.SetPosition(new Position2D(playerX + dirX * DashDistanceTiles,
                                                   playerY + dirY * DashDistanceTiles));
                PublishSuccess(tech, playerX, playerY, null, visualKind: 2);
                return;
            }

            case TechniqueType.Sensory:
            case TechniqueType.Support:
            case TechniqueType.Curse:
            {
                // Схематично: расход Ци (в зарядке) + тост + визуал ауры
                PublishSuccess(tech, playerX, playerY, null, visualKind: 2);
                return;
            }

            case TechniqueType.Formation:
            {
                if (_formations.CurrentStage != Core.Data.FormationStage.None)
                {
                    PublishFail(tech.TechniqueId, "Формация уже создаётся");
                    return;
                }
                var type = FormationTypePool[_formationRng.Next(FormationTypePool.Length)];
                var data = _formationGenerator.GenerateSpecified(
                    type, FormationSize.Small, tech.Level, _formationRng.NextInt64());
                bool started = _formations.StartDrawing(data.Id, _player.PlayerId,
                    _player.Position.X, _player.Position.Y);
                if (!started)
                {
                    PublishFail(tech.TechniqueId, "Не хватает Ци на контур или уровень мал");
                    return;
                }
                PublishSuccess(tech, playerX, playerY, null, visualKind: 2);
                return;
            }

            default:
                PublishFail(tech.TechniqueId, "Тип техники не поддерживается");
                return;
        }
    }

    /// <summary>Бонус урона от активной формации Amplification, если игрок в зоне (этап 5).</summary>
    private int GetAmplificationBonusPermil()
    {
        if (_formations is not Modules.Formation.FormationService svc) return 1000;
        if (!svc.IsFormationActive || svc.CurrentFormation is not { } f) return 1000;
        if (f.FormationType != FormationType.Amplification) return 1000;
        var pos = _player.Position;
        float radiusTiles = f.EffectRadiusMeters / (float)GameConstants.TILE_SIZE_M;
        int dist = Math.Max(Math.Abs(pos.X - svc.PositionX), Math.Abs(pos.Y - svc.PositionY));
        if (dist > radiusTiles) return 1000;
        return 1000 + svc.GetFormationBonusPermil(StatType.Damage);
    }

    /// <summary>Ближайший живой NPC в радиусе техники (Chebyshev, тайлы).</summary>
    private string? FindTargetInRange(LearnedTechnique tech)
    {
        float rangeTiles = Math.Max(MinAttackRangeTiles, tech.Range / GameConstants.TILE_SIZE_M);
        var nearby = _npcs.GetNearbyNPCIds(_player.Position, rangeTiles);
        if (nearby == null || nearby.Count == 0) return null;
        string? best = null;
        int bestDist = int.MaxValue;
        var pos = _player.Position;
        foreach (var id in nearby)
        {
            if (!_npcs.IsAlive(id)) continue;
            var npc = _npcs.GetNPC(id);
            if (npc == null) continue;
            int dist = Math.Max(Math.Abs(npc.Position.X - pos.X), Math.Abs(npc.Position.Y - pos.Y));
            if (dist < bestDist) { bestDist = dist; best = id; }
        }
        return best;
    }

    private bool _hasDamagedParts()
    {
        var parts = _body.GetAllParts();
        if (parts == null) return false;
        foreach (var p in parts)
            if (p.CurrentRedHP < p.MaxRedHP) return true;
        return false;
    }

    /// <summary>Лечение: BaseDamage распределяется по самым раненым частям.</summary>
    private void HealDamagedParts(int amount)
    {
        var parts = _body.GetAllParts();
        if (parts == null) return;
        while (amount > 0)
        {
            BodyPartType? worst = null;
            int worstMissing = 0;
            foreach (var p in parts)
            {
                int missing = p.MaxRedHP - p.CurrentRedHP;
                if (missing > worstMissing) { worstMissing = missing; worst = p.Type; }
            }
            if (worst == null) break;
            int heal = Math.Min(amount, worstMissing);
            _body.HealPart(worst.Value, heal);
            amount -= heal;
        }
    }

    private void PublishSuccess(LearnedTechnique tech, int px, int py, string? targetId, int visualKind = 0)
    {
        int tx = px, ty = py;
        if (targetId != null)
        {
            var npc = _npcs.GetNPC(targetId);
            if (npc != null) { tx = npc.Position.X; ty = npc.Position.Y; }
        }
        if (visualKind == 0)
            visualKind = tech.Subtype is CombatSubtype.RangedAoe ? 1 : 0;

        _castResultPub.Publish(new TechniqueCastResultEvent(
            tech.TechniqueId, true, "",
            px * GameConstants.TILE_PIXELS * 1000, py * GameConstants.TILE_PIXELS * 1000,
            tx * GameConstants.TILE_PIXELS * 1000, ty * GameConstants.TILE_PIXELS * 1000,
            tech.Type, tech.Element, visualKind));
    }

    private void PublishFail(string techniqueId, string reason)
    {
        _castResultPub.Publish(new TechniqueCastResultEvent(
            techniqueId, false, reason, 0, 0, 0, 0,
            TechniqueType.Combat, Element.Neutral, 0));
    }

    public void Dispose()
    {
        _castRequestToken?.Dispose();
        _castRequestToken = null;
        _chargeCompletedToken?.Dispose();
        _chargeCompletedToken = null;
        _formationActivatedToken?.Dispose();
        _formationActivatedToken = null;
    }
}

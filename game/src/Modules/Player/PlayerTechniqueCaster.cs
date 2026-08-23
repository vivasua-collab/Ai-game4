#nullable enable
// Этап 2 внедрения ЦИ (2026-08-23): PlayerTechniqueCaster — каст техник игрока.
// Подписан на TechniqueCastRequestedEvent (от Adapter: клавиша Z / клик в панели).
// Пайплайн: проверка Ци+кулдаун (TechniqueService.UseTechnique — расход Ци,
// рост мастерства) → эффект по типу техники:
//   Combat   → AttackIntentEvent(цель = ближайший NPC в Range) → боевой конвейер
//   Healing  → лечение раненых частей тела (IBodyService.HealPart)
//   Defense  → активация Ци-буфера (QiBufferActivateRequestEvent, режим щита)
//   Movement → рывок (dash) на 3 тайла в сторону курсора (IPlayerService.SetPosition)
//   Sensory  → обнаружение NPC в радиусе (тост)
//   Support/Curse → схематично (тост + визуал, этап 3)
//   Formation → этап 5 (пока отказ с сообщением)
//   Cultivation → пассивная, каст невозможен
// Результат публикуется TechniqueCastResultEvent (тосты + визуал этапа 3).
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
/// Кастер техник игрока. Владеет логикой применения техник по типам.
/// АРХИТЕКТУРА: кросс-модульное общение через EventBus; санкционированные
/// прямые инъекции (как в PlayerCombatAdapter): IPlayerService, INPCService,
/// IBodyService (модуль игрока работает с игроком), IFormationService +
/// IFormationGeneratorService (этап 5: формации).
/// </summary>
public sealed class PlayerTechniqueCaster : IDisposable
{
    [Inject] private readonly IPlayerService _player = null!;
    [Inject] private readonly INPCService _npcs = null!;
    [Inject] private readonly IBodyService _body = null!;
    [Inject] private readonly TechniqueService _techniques = null!;
    [Inject] private readonly IFormationService _formations = null!;
    [Inject] private readonly IFormationGeneratorService _formationGenerator = null!;
    [Inject] private readonly IPublisher<AttackIntentEvent> _attackIntentPub = null!;
    [Inject] private readonly IPublisher<QiBufferActivateRequestEvent> _qiBufferActivatePub = null!;
    [Inject] private readonly IPublisher<TechniqueCastResultEvent> _castResultPub = null!;
    [Inject] private readonly ISubscriber<TechniqueCastRequestedEvent> _castRequestSub = null!;
    [Inject] private readonly ISubscriber<FormationActivatedEvent> _formationActivatedSub = null!;

    /// <summary>Дальность dash техник Movement (тайлы).</summary>
    private const int DashDistanceTiles = 3;
    /// <summary>Радиус обнаружения техник Sensory (тайлы).</summary>
    private const int SensoryRadiusTiles = 10;
    /// <summary>Минимальная дальность атаки в тайлах (Range/2м, но не меньше 2).</summary>
    private const float MinAttackRangeTiles = 2f;

    /// <summary>Типы формаций для случайной генерации Formation-техникой (этап 5).</summary>
    private static readonly FormationType[] FormationTypePool =
    {
        FormationType.Barrier, FormationType.Amplification,
        FormationType.Suppression, FormationType.Gathering
    };

    private IDisposable? _castRequestToken;
    private IDisposable? _formationActivatedToken;
    private readonly Random _formationRng = new();

    public void Start()
    {
        _castRequestToken = _castRequestSub.Subscribe(OnCastRequested);
        // Этап 5: Barrier-формация при активации даёт игроку Ци-щит.
        _formationActivatedToken = _formationActivatedSub.Subscribe(OnFormationActivated);
    }

    public void Tick(float deltaTime) { /* нет кадровых задач */ }

    /// <summary>Barrier-формация активирована → Ци-буфер игрока (схематично, этап 5).</summary>
    private void OnFormationActivated(in FormationActivatedEvent e)
    {
        if (e.CasterId != _player.PlayerId) return;
        if (e.Type != FormationType.Barrier) return;
        // Поглощение урона за счёт Ци (FORMATION_SYSTEM §12.1: барьер поглощает урон).
        // Схематично: буфер = 10% ёмкости формации (кап 2000).
        long shield = Math.Min(2000, Math.Max(200, _formations.QiPoolMax / 10));
        _qiBufferActivatePub.Publish(new QiBufferActivateRequestEvent(shield, QiBufferMode.Shield));
    }

    private void OnCastRequested(in TechniqueCastRequestedEvent e)
    {
        var tech = _techniques.GetTechnique(e.TechniqueId);
        if (tech == null)
        {
            PublishFail(e.TechniqueId, "Техника не изучена");
            return;
        }

        // Cultivation — пассивная (работает сама при медитации).
        if (tech.Type == TechniqueType.Cultivation)
        {
            PublishFail(e.TechniqueId, "Пассивная техника — работает при медитации");
            return;
        }

        // Расход Ци + кулдаун + мастерство (QiConsumeRequestEvent внутри).
        // Проверки цели делаем ДО расхода — чтобы не жечь Ци впустую.
        int playerX = _player.Position.X;
        int playerY = _player.Position.Y;

        switch (tech.Type)
        {
            case TechniqueType.Combat:
            {
                var target = FindTargetInRange(tech);
                if (target == null)
                {
                    PublishFail(e.TechniqueId, "Нет цели в радиусе");
                    return;
                }
                bool isRanged = tech.Subtype is CombatSubtype.RangedProjectile
                                             or CombatSubtype.RangedBeam
                                             or CombatSubtype.RangedAoe;
                if (!_techniques.UseTechnique(e.TechniqueId))
                {
                    PublishFail(e.TechniqueId, _techniques.GetCooldown(e.TechniqueId) > 0
                        ? "Перезарядка" : "Недостаточно Ци");
                    return;
                }
                // Этап 5: формация Amplification в зоне → пермил-бонус урона
                // (CombatService.GetTechniqueDamage применяет; ЗАПРЕТ 3.9).
                _techniques.ExternalDamageBonusPermil = GetAmplificationBonusPermil();
                _attackIntentPub.Publish(new AttackIntentEvent(
                    _player.PlayerId, target, e.TechniqueId, isRanged));
                PublishSuccess(tech, playerX, playerY, target);
                return;
            }

            case TechniqueType.Healing:
            {
                if (!_hasDamagedParts())
                {
                    PublishFail(e.TechniqueId, "Тело не ранено");
                    return;
                }
                if (!_techniques.UseTechnique(e.TechniqueId))
                {
                    PublishFail(e.TechniqueId, _techniques.GetCooldown(e.TechniqueId) > 0
                        ? "Перезарядка" : "Недостаточно Ци");
                    return;
                }
                HealDamagedParts(tech.BaseDamage > 0 ? tech.BaseDamage : 10);
                PublishSuccess(tech, playerX, playerY, null, visualKind: 3);
                return;
            }

            case TechniqueType.Defense:
            {
                if (!_techniques.UseTechnique(e.TechniqueId))
                {
                    PublishFail(e.TechniqueId, _techniques.GetCooldown(e.TechniqueId) > 0
                        ? "Перезарядка" : "Недостаточно Ци");
                    return;
                }
                // Щит: инвестируем остаточную Ци в буфер (CHARGER_SYSTEM §5.2:
                // ядро — первичный источник; буфер поглощает урон).
                long invest = Math.Max(50, tech.QiCost);
                _qiBufferActivatePub.Publish(new QiBufferActivateRequestEvent(invest, QiBufferMode.Shield));
                PublishSuccess(tech, playerX, playerY, null, visualKind: 4);
                return;
            }

            case TechniqueType.Movement:
            {
                if (!_techniques.UseTechnique(e.TechniqueId))
                {
                    PublishFail(e.TechniqueId, _techniques.GetCooldown(e.TechniqueId) > 0
                        ? "Перезарядка" : "Недостаточно Ци");
                    return;
                }
                int dirX = Math.Sign(e.TargetMouseX / 1000 - playerX * GameConstants.TILE_PIXELS);
                int dirY = Math.Sign(e.TargetMouseY / 1000 - playerY * GameConstants.TILE_PIXELS);
                if (dirX == 0 && dirY == 0) { dirX = 1; }
                _player.SetPosition(new Position2D(playerX + dirX * DashDistanceTiles,
                                                   playerY + dirY * DashDistanceTiles));
                PublishSuccess(tech, playerX, playerY, null, visualKind: 2);
                return;
            }

            case TechniqueType.Sensory:
            {
                if (!_techniques.UseTechnique(e.TechniqueId))
                {
                    PublishFail(e.TechniqueId, _techniques.GetCooldown(e.TechniqueId) > 0
                        ? "Перезарядка" : "Недостаточно Ци");
                    return;
                }
                // Схематично: список живых NPC в радиусе (тост формирует Adapter по событию).
                PublishSuccess(tech, playerX, playerY, null, visualKind: 2);
                return;
            }

            case TechniqueType.Support:
            case TechniqueType.Curse:
            {
                // Схематично (этап 2): расход Ци + тост + визуал ауры.
                // Реальные баффы/дебаффы — через BuffService в следующей итерации.
                if (!_techniques.UseTechnique(e.TechniqueId))
                {
                    PublishFail(e.TechniqueId, _techniques.GetCooldown(e.TechniqueId) > 0
                        ? "Перезарядка" : "Недостаточно Ци");
                    return;
                }
                PublishSuccess(tech, playerX, playerY, null, visualKind: 2);
                return;
            }

            case TechniqueType.Formation:
            {
                // Этап 5 внедрения ЦИ: создание формации (вариант А, без ядра).
                // 1) Генерация формации (тип случайный из пула, Small, уровень техники).
                // 2) StartDrawing в позиции игрока: расход contourQi (QiConsumeRequestEvent).
                // 3) Автонаполнение: FormationModule.AutoFillTick (conductivity/сек) → Active.
                if (_formations.CurrentStage != Core.Data.FormationStage.None)
                {
                    PublishFail(e.TechniqueId, "Формация уже создаётся");
                    return;
                }

                var type = FormationTypePool[_formationRng.Next(FormationTypePool.Length)];
                var data = _formationGenerator.GenerateSpecified(
                    type, FormationSize.Small, tech.Level, _formationRng.NextInt64());

                bool started = _formations.StartDrawing(data.Id, _player.PlayerId,
                    _player.Position.X, _player.Position.Y);
                if (!started)
                {
                    PublishFail(e.TechniqueId, "Не хватает Ци на контур или уровень мал");
                    return;
                }

                PublishSuccess(tech, playerX, playerY, null, visualKind: 2);
                return;
            }

            default:
                PublishFail(e.TechniqueId, "Тип техники не поддерживается");
                return;
        }
    }

    /// <summary>
    /// Этап 5: бонус урона от активной формации Amplification, если игрок в зоне.
    /// Возвращает пермил (1000 = +0%, 1300 = +30%); 1000 — формации нет.
    /// </summary>
    private int GetAmplificationBonusPermil()
    {
        if (_formations is not Modules.Formation.FormationService svc) return 1000;
        if (!svc.IsFormationActive || svc.CurrentFormation is not { } f) return 1000;
        if (f.FormationType != FormationType.Amplification) return 1000;

        // Игрок в радиусе? (Chebyshev в тайлах против EffectRadiusMeters / TILE_SIZE_M)
        var pos = _player.Position;
        float radiusTiles = f.EffectRadiusMeters / (float)GameConstants.TILE_SIZE_M;
        int dist = Math.Max(Math.Abs(pos.X - svc.PositionX), Math.Abs(pos.Y - svc.PositionY));
        if (dist > radiusTiles) return 1000;

        return 1000 + svc.GetFormationBonusPermil(StatType.Damage);
    }

    /// <summary>Ближайший живой NPC в радиусе техники (Chebyshev, тайлы).</summary>
    private string? FindTargetInRange(LearnedTechnique tech)
    {        // Range хранится в метрах; 1 тайл = 2 м (TILE_SIZE_M), минимум 2 тайла.
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
        // Простая политика: лечим по очереди самые раненые (min CurrentRedHP ratio).
        // HealPart сам клампит по MaxRedHP.
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
        _formationActivatedToken?.Dispose();
        _formationActivatedToken = null;
    }
}

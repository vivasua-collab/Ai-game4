#nullable enable
// Создано: 2026-09-21 — R27 «TargetingService» (план
//   checkpoints/plans/2026-09-20_r23_multicombat_aoe_design.md §3.1).
//
// ЗАЧЕМ: выбор цели игрока БЕЗ атаки (Tab-цикл). Прежде таргетинг был
// неявным: каждая система сама искала «ближайшего» (PlayerCombatAdapter.
// FindNearestTarget для Space, PlayerTechniqueCaster.FindTargetInRange
// для техник) — игрок не мог выбрать, КОГО бить в толпе (мультибой R24-C
// сделал толпу реальной). Теперь: Tab → цикл по целям в радиусе →
// PlayerTargetChangedEvent (рамка-подсветка на рендере) → атаки игрока
// ПРЕДПОЧИТАЮТ выбранную цель (в радиусе и живую; иначе прежний
// «ближайший»).
//
// ПОТОК:
//   InputAdapter (Tab, action "cycle_target") → PlayerInputService
//   → PlayerCombatAdapter.Tick → TargetingService.CycleTarget()
//   → PlayerTargetChangedEvent → NPCSpriteRenderer/AnimalSpriteRenderer
//     (подсветка), Space/Z-атаки (SelectedTarget).
//
// АРХИТЕКТУРА: engine-agnostic (Modules/Player), кандидаты — NPC ∪ звери
// (D1-паттерн: волки живут в AnimalService), Чебышёв-дистанция, БЕЗ float
// (ЗАПРЕТ 3.9). Смерть/исчезновение цели — ленивая валидация при
// чтении (Get/Cycle): без подписок на чужие события — цель «протухает»
// сама. NPC-ретаргет по угрозе уже есть (NPCAIService.Threats) — этот
// сервис ТОЛЬКО про ручной выбор игрока.
using System;
using System.Collections.Generic;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Helpers;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Player;

/// <summary>
/// R27: выбор цели игрока (Tab-цикл по целям в радиусе).
/// </summary>
public sealed class TargetingService
{
    /// <summary>
    /// Радиус циклирования (тайлы, Чебышёв): «видимый бой» вокруг игрока.
    /// Шире melee (2.5): Tab работает и для дальних техник/лука.
    /// </summary>
    public const float CycleRadiusTiles = 12f;

    [Inject] private readonly IPlayerService? _player = null;
    [Inject] private readonly INPCService? _npcs = null;
    [Inject] private readonly IAnimalService? _animals = null;
    [Inject] private readonly IPublisher<PlayerTargetChangedEvent>? _targetChangedPub = null;

    /// <summary>Выбранная цель (null/empty = не выбрана). Ленивая валидация.</summary>
    public string CurrentTargetId { get; private set; } = string.Empty;

    /// <summary>
    /// Tab-цикл: следующая цель по кругу (сортировка по Чебышёв-дистанции
    /// от игрока; текущая цель в списке → следующая за ней, конец списка →
    /// первая; цели нет/протухла → первая). Публикует
    /// PlayerTargetChangedEvent. Пустой список — тихий сброс.
    /// </summary>
    public void CycleTarget()
    {
        var targets = CollectTargets();
        if (targets.Count == 0)
        {
            if (!string.IsNullOrEmpty(CurrentTargetId))
            {
                CurrentTargetId = string.Empty;
                PublishChange(string.Empty, 0, 0, 0);
            }
            return;
        }

        // Позиция текущей (валидной) цели в списке → следующая по кругу
        int index = targets.FindIndex(t => PlayerIdResolver.AreSameEntity(t.Id, CurrentTargetId));
        int next = index < 0 ? 0 : (index + 1) % targets.Count;

        CurrentTargetId = targets[next].Id;
        PublishChange(targets[next].Id, targets[next].Pos.X, targets[next].Pos.Y,
            targets[next].Dist);
    }

    /// <summary>
    /// Выбранная цель, если она ЖИВА и в радиусе (rangeTiles, Чебышёв).
    /// «Протухшая» цель (умерла/ушла) — авто-сброс + событие.
    /// </summary>
    public bool TryGetSelectedTargetInRange(float rangeTiles, out string targetId, out Position2D pos)
    {
        targetId = string.Empty;
        pos = default;

        if (string.IsNullOrEmpty(CurrentTargetId)) return false;
        if (_player == null) return false;

        if (!TryResolveTarget(CurrentTargetId, out pos)) // умерла/исчезла
        {
            CurrentTargetId = string.Empty;
            PublishChange(string.Empty, 0, 0, 0);
            return false;
        }

        var p = _player.Position;
        int dist = Math.Max(Math.Abs(pos.X - p.X), Math.Abs(pos.Y - p.Y));
        if (dist > Math.Max(1, (int)Math.Ceiling(rangeTiles)))
        {
            return false; // вне радиуса действия, но остаётся «выбранной»
        }

        targetId = CurrentTargetId;
        return true;
    }

    /// <summary>Явный сброс выбора (например, цель убита игроком).</summary>
    public void ClearTarget(string entityId)
    {
        if (string.IsNullOrEmpty(entityId)
            || !PlayerIdResolver.AreSameEntity(entityId, CurrentTargetId)) return;
        CurrentTargetId = string.Empty;
        PublishChange(string.Empty, 0, 0, 0);
    }

    private void PublishChange(string id, int x, int y, int dist)
        => _targetChangedPub?.Publish(new PlayerTargetChangedEvent(id, x, y, dist));

    /// <summary>
    /// Живые кандидаты (NPC ∪ звери) в радиусе циклирования от игрока,
    /// сортировка по дистанции (стабильный порядок цикла).
    /// </summary>
    private List<(string Id, Position2D Pos, int Dist)> CollectTargets()
    {
        var result = new List<(string Id, Position2D Pos, int Dist)>();
        if (_player == null) return result;

        var p = _player.Position;

        if (_npcs != null)
        {
            var nearby = _npcs.GetNearbyNPCIds(p, CycleRadiusTiles);
            if (nearby != null)
            {
                foreach (var id in nearby)
                {
                    if (!_npcs.IsAlive(id)) continue;
                    var npc = _npcs.GetNPC(id);
                    if (npc == null) continue;
                    int dist = Math.Max(Math.Abs(npc.Position.X - p.X), Math.Abs(npc.Position.Y - p.Y));
                    result.Add((id, npc.Position, dist));
                }
            }
        }

        if (_animals != null)
        {
            foreach (var animal in _animals.GetAliveAnimalsInRange(p, CycleRadiusTiles))
            {
                if (!animal.IsValid) continue;
                int dist = Math.Max(Math.Abs(animal.Position.X - p.X), Math.Abs(animal.Position.Y - p.Y));
                result.Add((animal.EntityId, animal.Position, dist));
            }
        }

        result.Sort((a, b) => a.Dist.CompareTo(b.Dist));
        return result;
    }

    /// <summary>Жива ли сущность и где (NPC/зверь; игрок — не цель).</summary>
    private bool TryResolveTarget(string entityId, out Position2D pos)
    {
        pos = default;
        if (string.IsNullOrEmpty(entityId)) return false;

        var npc = _npcs?.GetNPC(entityId);
        if (npc != null)
        {
            if (_npcs != null && !_npcs.IsAlive(entityId)) return false;
            pos = npc.Position;
            return true;
        }

        var animal = _animals?.TryGetAnimal(entityId);
        if (animal.HasValue && animal.Value.IsValid)
        {
            pos = animal.Value.Position;
            return true;
        }
        return false;
    }
}

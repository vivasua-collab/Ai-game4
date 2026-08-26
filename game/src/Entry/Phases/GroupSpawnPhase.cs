#nullable enable
// Создано: 2026-08-22 — GROUP-SPAWN: групповой спавн NPC и животных.
// Phase 7 — спавн 3-5 групп NPC на большой карте (500×500):
//   * 1 волчья стая (3-5 волков, HuntingPack, species="wolf")
//   * 1 патруль стражи (2-3 NPC Guard, Patrol, faction="town_guard")
//   * 1 торговый караван (1 Merchant + 2 Guard, TradeCaravan, faction="merchants")
//   * 1 стадо оленей (3-5 оленей, Patrol, species="deer")
// На малой карте (50×50): 1 волчья стая + 1 патруль стражи.
//
// Группа — это overlay над индивидуальным AI: NPCGroupService.Tick() обновляет
// CurrentGroupTarget для участников, NPCMovementService читает это поле
// (приоритет над wander/patrol). Для животных overlay пока не применяется —
// они продолжают блуждать через AnimalService.Tick(), но числятся в группе
// (для будущего расширения: стая волков атакует вместе, стадо оленей бежит
// вместе при угрозе).
//
// Источник: task GROUP-SPAWN
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Modules.NPC;

namespace CultivationGame.Entry.Phases;

/// <summary>
/// Phase 7 — групповой спавн: волчья стая, патруль стражи, торговый караван,
/// стадо оленей. Создаёт NPCGroup через INPCGroupService и заполняет её
/// участниками через NPCSpawnerService (для NPC) или AnimalService (для
/// животных). Каждой группе назначается patrol route вокруг точки спавна,
/// чтобы NPCGroupService.Tick() мог выставлять CurrentGroupTarget.
/// </summary>
public sealed class GroupSpawnPhase : AbstractSceneAssemblyPhase
{
    public override string PhaseName => "GroupSpawn";
    public override int PhaseOrder => 7;

    [Inject] private readonly INPCGroupService _groupService = null!;
    [Inject] private readonly INPCSpawnerService _spawner = null!;
    [Inject] private readonly AnimalService _animalService = null!;
    [Inject] private readonly ITileService _tiles = null!;
    [Inject] private readonly IGameSession _session = null!;

    // Prime offset — independent RNG stream from animals (7919), NPCs (104729),
    // terrain. Ensures group spawn coordinates don't correlate with individual
    // spawns even when location seed is reused.
    private const int GroupSeedOffset = 200003;

    private const int MaxSpawnAttempts = 200;
    private const int MinDistanceFromPlayer = 8;
    private const int MaxGroupSpacingAttempts = 30;

    // Track centres of already-placed groups so new groups don't overlap.
    private readonly List<Position2D> _placedGroupCentres = new();

    public override Task ExecuteAsync()
    {
        // 2026-08-26 (аудит-2 B-6): сброс перед каждым прогоном сборки — фаза
        // синглтон, а RunAssembly вызывается и на NewGame, и на Load
        // (GameSession:91/131): без сброса spacing-чек гонялся по устаревшим
        // центрам прошлой сборки.
        _placedGroupCentres.Clear();

        var locId = _session.Data?.WorldId ?? LocationCatalog.TestPolygon.Id;
        var loc = LocationCatalog.Find(locId) ?? LocationCatalog.TestPolygon;
        var rng = new SeededRandom(loc.Seed + GroupSeedOffset);

        // Use ACTUAL tile-map dimensions (honours GODOT_MAP_SIZE override)
        // rather than catalog dimensions — this ensures group placement stays
        // in-bounds on the large-world perf-test path.
        int mapW = _tiles.MapWidth > 0 ? _tiles.MapWidth : loc.Width;
        int mapH = _tiles.MapHeight > 0 ? _tiles.MapHeight : loc.Height;

        bool large = mapW >= 200 && mapH >= 200;
        int groupsCreated = 0;

        if (large)
        {
            // Large world (≥200×200, typically 500×500): spawn 4 diverse groups.
            groupsCreated += SpawnWolfPack(rng, loc, mapW, mapH);
            groupsCreated += SpawnGuardPatrol(rng, loc, mapW, mapH);
            groupsCreated += SpawnTradeCaravan(rng, loc, mapW, mapH);
            groupsCreated += SpawnDeerHerd(rng, loc, mapW, mapH);
        }
        else
        {
            // Small map (50×50): 1 wolf pack (3 wolves) + 1 guard patrol (2 guards).
            groupsCreated += SpawnWolfPack(rng, loc, mapW, mapH, count: 3);
            groupsCreated += SpawnGuardPatrol(rng, loc, mapW, mapH, count: 2);
        }

        Console.WriteLine(
            $"[Phase {PhaseOrder}] {PhaseName} complete — {groupsCreated} groups on '{loc.Id}' ({mapW}×{mapH})");
        return Task.CompletedTask;
    }

    // === Group spawners ===

    /// <summary>
    /// Волчья стая: 3-5 волков, HuntingPack task, species="wolf".
    /// Волки спавнятся через AnimalService (как обычные животные) и добавляются
    /// в группу для будущего координированного поведения (фланговая атака).
    /// </summary>
    private int SpawnWolfPack(SeededRandom rng, LocationData loc, int mapW, int mapH, int? count = null)
    {
        int n = count ?? rng.Next(3, 6);  // 3-5 wolves
        var center = FindGroupCenter(rng, mapW, mapH, minSpacing: 20);
        if (center is null)
        {
            Console.WriteLine("[GroupSpawn] Wolf pack — no walkable centre found, skipped");
            return 0;
        }

        var groupId = _groupService.CreateGroup(GroupTaskType.HuntingPack, faction: "", species: "wolf");
        var route = BuildPatrolRoute(center.Value, radius: 8, points: 4, rng, mapW, mapH);
        _groupService.SetPatrolRoute(groupId, route);

        for (int i = 0; i < n; i++)
        {
            var pos = FindWalkableNear(center.Value, rng, mapW, mapH, maxOffset: 2) ?? center.Value;
            try
            {
                var animal = _animalService.SpawnAnimal("wolf", pos);
                _groupService.AddMember(groupId, animal.EntityId,
                    i == 0 ? GroupRole.Leader : GroupRole.Follower);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GroupSpawn] Wolf spawn failed at {pos}: {ex.Message}");
            }
        }
        Console.WriteLine($"[GroupSpawn] Wolf pack '{groupId}' — {n} wolves at {center.Value}");
        return 1;
    }

    /// <summary>
    /// Патруль стражи: 2-3 NPC Guard, Patrol task, faction="town_guard".
    /// Получает patrol route вокруг точки спавна; лидер ведёт, последователи
    /// следуют за лидером (v1: все движутся к CurrentGroupTarget).
    /// </summary>
    private int SpawnGuardPatrol(SeededRandom rng, LocationData loc, int mapW, int mapH, int? count = null)
    {
        int n = count ?? rng.Next(2, 4);  // 2-3 guards
        var center = FindGroupCenter(rng, mapW, mapH, minSpacing: 20);
        if (center is null)
        {
            Console.WriteLine("[GroupSpawn] Guard patrol — no walkable centre found, skipped");
            return 0;
        }

        var groupId = _groupService.CreateGroup(GroupTaskType.Patrol, faction: "town_guard");
        var route = BuildPatrolRoute(center.Value, radius: 10, points: 4, rng, mapW, mapH);
        _groupService.SetPatrolRoute(groupId, route);

        for (int i = 0; i < n; i++)
        {
            var pos = FindWalkableNear(center.Value, rng, mapW, mapH, maxOffset: 2) ?? center.Value;
            long seed = loc.Seed + GroupSeedOffset + (long)NPCRole.Guard + i;
            string npcId = _spawner.SpawnNPC("human", NPCRole.Guard, 2, pos, seed);
            if (!string.IsNullOrEmpty(npcId))
                _groupService.AddMember(groupId, npcId,
                    i == 0 ? GroupRole.Leader : GroupRole.Follower);
        }
        Console.WriteLine($"[GroupSpawn] Guard patrol '{groupId}' — {n} guards at {center.Value}");
        return 1;
    }

    /// <summary>
    /// Торговый караван: 1 Merchant (лидер) + 2 Guard (охрана),
    /// TradeCaravan task, faction="merchants". Patrol route шире (15 тайлов,
    /// 3 waypoints) — изображает движение между городами.
    /// </summary>
    private int SpawnTradeCaravan(SeededRandom rng, LocationData loc, int mapW, int mapH)
    {
        var center = FindGroupCenter(rng, mapW, mapH, minSpacing: 20);
        if (center is null)
        {
            Console.WriteLine("[GroupSpawn] Trade caravan — no walkable centre found, skipped");
            return 0;
        }

        var groupId = _groupService.CreateGroup(GroupTaskType.TradeCaravan, faction: "merchants");
        var route = BuildPatrolRoute(center.Value, radius: 15, points: 3, rng, mapW, mapH);
        _groupService.SetPatrolRoute(groupId, route);

        // 1 merchant as the leader.
        var merchantPos = FindWalkableNear(center.Value, rng, mapW, mapH, maxOffset: 1) ?? center.Value;
        long merchantSeed = loc.Seed + GroupSeedOffset + (long)NPCRole.Merchant;
        string merchantId = _spawner.SpawnNPC("human", NPCRole.Merchant, 1, merchantPos, merchantSeed);
        if (!string.IsNullOrEmpty(merchantId))
            _groupService.AddMember(groupId, merchantId, GroupRole.Leader);

        // 2 guards as followers.
        for (int i = 0; i < 2; i++)
        {
            var pos = FindWalkableNear(center.Value, rng, mapW, mapH, maxOffset: 2) ?? center.Value;
            long seed = loc.Seed + GroupSeedOffset + (long)NPCRole.Guard + 100 + i;
            string npcId = _spawner.SpawnNPC("human", NPCRole.Guard, 1, pos, seed);
            if (!string.IsNullOrEmpty(npcId))
                _groupService.AddMember(groupId, npcId, GroupRole.Follower);
        }
        Console.WriteLine($"[GroupSpawn] Trade caravan '{groupId}' — 1 merchant + 2 guards at {center.Value}");
        return 1;
    }

    /// <summary>
    /// Стадо оленей: 3-5 оленей, Patrol task, species="deer". Спавнится через
    /// AnimalService; patrol route компактнее (radius 6, 3 waypoints) —
    /// изображает мирное пастбище.
    /// </summary>
    private int SpawnDeerHerd(SeededRandom rng, LocationData loc, int mapW, int mapH)
    {
        int n = rng.Next(3, 6);  // 3-5 deer
        var center = FindGroupCenter(rng, mapW, mapH, minSpacing: 20);
        if (center is null)
        {
            Console.WriteLine("[GroupSpawn] Deer herd — no walkable centre found, skipped");
            return 0;
        }

        var groupId = _groupService.CreateGroup(GroupTaskType.Patrol, species: "deer");
        var route = BuildPatrolRoute(center.Value, radius: 6, points: 3, rng, mapW, mapH);
        _groupService.SetPatrolRoute(groupId, route);

        for (int i = 0; i < n; i++)
        {
            var pos = FindWalkableNear(center.Value, rng, mapW, mapH, maxOffset: 3) ?? center.Value;
            try
            {
                var animal = _animalService.SpawnAnimal("deer", pos);
                _groupService.AddMember(groupId, animal.EntityId,
                    i == 0 ? GroupRole.Leader : GroupRole.Follower);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GroupSpawn] Deer spawn failed at {pos}: {ex.Message}");
            }
        }
        Console.WriteLine($"[GroupSpawn] Deer herd '{groupId}' — {n} deer at {center.Value}");
        return 1;
    }

    // === Helpers ===

    /// <summary>
    /// Найти walkable-точку для центра группы: далеко от игрока и от других
    /// уже размещённых групп (минимум minSpacing тайлов по Чебышеву).
    /// </summary>
    private Position2D? FindGroupCenter(SeededRandom rng, int mapW, int mapH, int minSpacing)
    {
        int cx = mapW / 2, cy = mapH / 2;
        for (int attempt = 0; attempt < MaxSpawnAttempts; attempt++)
        {
            int x = rng.Next(1, mapW - 1);
            int y = rng.Next(1, mapH - 1);
            if (!_tiles.IsWalkable(x, y)) continue;

            int distToPlayer = Math.Max(Math.Abs(x - cx), Math.Abs(y - cy));
            if (distToPlayer < MinDistanceFromPlayer) continue;

            // Check spacing against previously placed groups.
            bool tooClose = false;
            foreach (var placed in _placedGroupCentres)
            {
                int dist = Math.Max(Math.Abs(x - placed.X), Math.Abs(y - placed.Y));
                if (dist < minSpacing) { tooClose = true; break; }
            }
            if (tooClose) continue;

            _placedGroupCentres.Add(new Position2D(x, y));
            return new Position2D(x, y);
        }

        // Fallback: relax spacing constraint (last attempt).
        for (int attempt = 0; attempt < MaxGroupSpacingAttempts; attempt++)
        {
            int x = rng.Next(1, mapW - 1);
            int y = rng.Next(1, mapH - 1);
            if (!_tiles.IsWalkable(x, y)) continue;
            int distToPlayer = Math.Max(Math.Abs(x - cx), Math.Abs(y - cy));
            if (distToPlayer < MinDistanceFromPlayer) continue;
            _placedGroupCentres.Add(new Position2D(x, y));
            return new Position2D(x, y);
        }
        return null;
    }

    /// <summary>
    /// Найти walkable-точку рядом с заданным центром (для размещения участников
    /// группы компактным кластером). Возвращает null если все попытки провалились.
    /// </summary>
    private Position2D? FindWalkableNear(Position2D center, SeededRandom rng, int mapW, int mapH, int maxOffset)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            int dx = rng.Next(-maxOffset, maxOffset + 1);
            int dy = rng.Next(-maxOffset, maxOffset + 1);
            int x = center.X + dx;
            int y = center.Y + dy;
            if (x < 1 || y < 1 || x >= mapW - 1 || y >= mapH - 1) continue;
            if (!_tiles.IsWalkable(x, y)) continue;
            return new Position2D(x, y);
        }
        return null;
    }

    /// <summary>
    /// Построить patrol route из N точек по кругу радиусом R вокруг центра.
    /// Точки, попавшие на non-walkable тайлы, заменяются на сам центр.
    /// </summary>
    private List<Position2D> BuildPatrolRoute(Position2D center, int radius, int points,
        SeededRandom rng, int mapW, int mapH)
    {
        var route = new List<Position2D>(points);
        for (int i = 0; i < points; i++)
        {
            int angle = (360 / points) * i;
            double rad = angle * Math.PI / 180.0;
            int x = center.X + (int)Math.Round(Math.Cos(rad) * radius);
            int y = center.Y + (int)Math.Round(Math.Sin(rad) * radius);
            x = Math.Clamp(x, 1, mapW - 2);
            y = Math.Clamp(y, 1, mapH - 2);

            // If the chosen tile isn't walkable, fall back to the centre.
            if (!_tiles.IsWalkable(x, y))
            {
                x = center.X;
                y = center.Y;
            }
            route.Add(new Position2D(x, y));
        }
        return route;
    }
}

#nullable enable
// Создано: 2026-08-22 — NPC_COMBAT_PREP Phase 1: спавн людей-NPC на тестовой карте.
// Phase 6 — spawns 4 human NPCs (Merchant / Cultivator / Guard / Passerby)
// through the full NPCAssemblyService pipeline via NPCSpawnerService.
// Источник: docs/docs_v2/09_workflow/NPC_COMBAT_PREP.md §Phase 1
using System;
using System.Threading;
using System.Threading.Tasks;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Modules.NPC;

namespace CultivationGame.Entry.Phases;

/// <summary>
/// Phase 6 — spawns human NPCs at deterministic walkable positions on the
/// active location. Delegates to <see cref="INPCSpawnerService.SpawnNPC"/>
/// (full assembly pipeline: soul → body → qi → equipment → personality).
/// Seeds derive from the location seed so spawns are deterministic.
/// </summary>
public sealed class HumanNPCSpawnPhase : AbstractSceneAssemblyPhase
{
    public override string PhaseName => "HumanNPCSpawn";
    public override int PhaseOrder => 6;

    [Inject] private readonly INPCSpawnerService _spawner = null!;
    [Inject] private readonly ITileService _tiles = null!;
    [Inject] private readonly IGameSession _session = null!;

    // Prime offset — independent RNG stream from animals (7919) and terrain.
    private const int NpcSeedOffset = 104729;
    private const int MaxSpawnAttempts = 200;
    private const int MinDistanceFromPlayer = 5;

    private static readonly (NPCRole Role, int Level)[] SpawnRoles =
    {
        (NPCRole.Merchant,   1),
        (NPCRole.Cultivator, 3),
        (NPCRole.Guard,      2),
        (NPCRole.Passerby,   0),
    };

    public override Task ExecuteAsync()
    {
        var locId = _session.Data?.WorldId ?? LocationCatalog.TestPolygon.Id;
        var loc = LocationCatalog.Find(locId) ?? LocationCatalog.TestPolygon;

        var rng = new SeededRandom(loc.Seed + NpcSeedOffset);
        int spawned = 0;

        foreach (var (role, level) in SpawnRoles)
        {
            var pos = FindWalkablePosition(rng, loc.Width, loc.Height);
            if (pos is null)
            {
                Console.WriteLine($"[HumanNPCSpawn] No walkable tile for {role} — skipped");
                continue;
            }

            long seed = loc.Seed + NpcSeedOffset + (long)role;
            string npcId = _spawner.SpawnNPC("human", role, level, pos.Value, seed);
            if (!string.IsNullOrEmpty(npcId))
            {
                spawned++;
                Console.WriteLine($"[HumanNPCSpawn] Spawned {role} #{npcId} at ({pos.Value.X}, {pos.Value.Y})");
            }
        }

        Console.WriteLine(
            $"[Phase {PhaseOrder}] {PhaseName} complete — {spawned}/{SpawnRoles.Length} NPCs on '{loc.Id}'");
        return Task.CompletedTask;
    }

    private Position2D? FindWalkablePosition(SeededRandom rng, int width, int height)
    {
        int cx = width / 2, cy = height / 2;
        for (int attempt = 0; attempt < MaxSpawnAttempts; attempt++)
        {
            int x = rng.Next(1, width - 1);
            int y = rng.Next(1, height - 1);
            if (!_tiles.IsWalkable(x, y)) continue;

            int dist = Math.Max(Math.Abs(x - cx), Math.Abs(y - cy));
            if (dist < MinDistanceFromPlayer) continue;

            return new Position2D(x, y);
        }
        return null;
    }
}

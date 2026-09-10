#nullable enable
// Создано: 2026-08-22 — NPC_COMBAT_PREP Phase 1: спавн людей-NPC на тестовой карте.
// Phase 6 — spawns 4 human NPCs (Merchant / Cultivator / Guard / Passerby)
// through the full NPCAssemblyService pipeline via NPCSpawnerService.
// Источник: docs/docs_v2/09_workflow/NPC_COMBAT_PREP.md §Phase 1
// РЕДАКТИРОВАНО (R13, 2026-09-10): состав населения — ГЕНЕРАЦИЯ вместо
// хардкод-массива. NPCSpawnCompositionService.GenerateStartup(loc) выводит
// роли/уровни из типа локации + DangerLevel + сида (детерминированно);
// поддержание популяции (респаун при выбивании) — ReinforcementTick из
// NPCModule.Tick.
using System;
using System.Threading;
using System.Threading.Tasks;
using CultivationGame.Core.DI;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;
using CultivationGame.Modules.NPC;

namespace CultivationGame.Entry.Phases;

/// <summary>
/// Phase 7 — spawns human NPCs at deterministic walkable positions on the
/// active location. Delegates to <see cref="INPCSpawnerService.SpawnNPC"/>
/// (full assembly pipeline: soul → body → qi → equipment → personality).
/// Seeds derive from the location seed so spawns are deterministic.
/// R13: состав (роли/уровни/число) генерируется NPCSpawnCompositionService
/// по типу локации и уровню опасности — вместо фиксированного массива.
/// </summary>
public sealed class HumanNPCSpawnPhase : AbstractSceneAssemblyPhase
{
    public override string PhaseName => "HumanNPCSpawn";
    // 2026-09-08 (ревью-1 P2-1): 6 → 7 — сдвиг из-за уникализации AnimalSpawn(6).
    public override int PhaseOrder => 7;

    [Inject] private readonly INPCSpawnerService _spawner = null!;
    [Inject] private readonly ITileService _tiles = null!;
    [Inject] private readonly IGameSession _session = null!;
    [Inject] private readonly CultivationGame.Modules.Interaction.DialogueService _dialogues = null!;
    // R13: генератор состава населения («NPC спаун через генерацию»).
    [Inject] private readonly NPCSpawnCompositionService _composition = null!;

    // Prime offset — independent RNG stream from animals (7919) and terrain.
    private const int NpcSeedOffset = 104729;
    private const int MaxSpawnAttempts = 200;
    private const int MinDistanceFromPlayer = 5;

    public override Task ExecuteAsync()
    {
        var locId = _session.Data?.WorldId ?? LocationCatalog.TestPolygon.Id;
        var loc = LocationCatalog.Find(locId) ?? LocationCatalog.TestPolygon;

        // R13: Reset (re-assembly safety) + генерация состава населения.
        // ДЕТЕРМИНИЗМ: один сид локации → один состав (QA-воспроизводимость).
        _composition?.Reset();
        var requests = _composition?.GenerateStartup(loc)
            ?? new System.Collections.Generic.List<SpawnRequest>();

        var rng = new SeededRandom(loc.Seed + NpcSeedOffset);
        int spawned = 0;

        foreach (var request in requests)
        {
            var pos = FindWalkablePosition(rng, loc.Width, loc.Height);
            if (pos is null)
            {
                Console.WriteLine($"[HumanNPCSpawn] No walkable tile for {request.Role} — skipped");
                continue;
            }

            long seed = loc.Seed + NpcSeedOffset + (long)request.Role * 31 + spawned;
            string npcId = _spawner.SpawnNPC(request.SpeciesId, request.Role, request.Level, pos.Value, seed);
            if (!string.IsNullOrEmpty(npcId))
            {
                spawned++;
                // Phase 2: bind a role dialogue so E-key interaction opens chat.
                // Enemy (бандиты) — без диалога: только бой.
                string? dialogueId = DialogueIdForRole(request.Role);
                if (dialogueId != null)
                    _dialogues?.MapNpcDialogue(npcId, dialogueId);
                Console.WriteLine($"[HumanNPCSpawn] Spawned {request.Role} L{request.Level} " +
                          $"#{npcId} at ({pos.Value.X}, {pos.Value.Y})");
            }
        }

        Console.WriteLine(
            $"[Phase {PhaseOrder}] {PhaseName} complete — {spawned}/{requests.Count} generated NPCs on '{loc.Id}'");
        return Task.CompletedTask;
    }

    private static string? DialogueIdForRole(NPCRole role) => role switch
    {
        NPCRole.Merchant   => "dialogue_merchant",
        NPCRole.Cultivator => "dialogue_cultivator",
        NPCRole.Guard      => "dialogue_guard",
        NPCRole.Elder      => "dialogue_elder",
        NPCRole.Passerby   => "dialogue_passerby",
        _                  => null, // Enemy/Monster — только бой
    };

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

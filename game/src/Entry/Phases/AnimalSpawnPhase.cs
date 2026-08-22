#nullable enable
// Создано: 2026-08-22 — Phase C (BODY-IMPL-PLAN): спавн простых животных.
// Phase 5 — spawns 3-5 wandering animals (wolf / deer / rabbit) on the
// test polygon at game start. Replaces the stub NPCSpawnPhase.
// Источник: checkpoints/08_22_body_impl_plan.md Phase C
using System;
using System.Threading;
using System.Threading.Tasks;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Modules.NPC;

namespace CultivationGame.Entry.Phases;

/// <summary>
/// Phase 5 — spawns 3-5 wandering animals at random walkable positions
/// on the active location's tile map. Delegates the spawn loop to
/// <see cref="AnimalService.SpawnForLocation"/>; clears any fallback
/// animals spawned by <c>AnimalService.Start()</c> first so re-assembly
/// (e.g. NewGame → RunAssembly) doesn't double-spawn.
/// </summary>
public sealed class AnimalSpawnPhase : AbstractSceneAssemblyPhase
{
    public override string PhaseName => "AnimalSpawn";
    public override int PhaseOrder => 5;

    [Inject] private readonly AnimalService _animalService = null!;
    [Inject] private readonly IGameSession _session = null!;

    public override Task ExecuteAsync()
    {
        var locId = _session.Data?.WorldId ?? LocationCatalog.TestPolygon.Id;
        var loc = LocationCatalog.Find(locId) ?? LocationCatalog.TestPolygon;

        // Clear fallback animals (spawned by AnimalService.Start() for
        // direct-load case) before spawning the proper set for this location.
        _animalService.ClearAnimals();

        int spawned = _animalService.SpawnForLocation(loc.Width, loc.Height, loc.Seed, loc.Id);

        Console.WriteLine(
            $"[Phase {PhaseOrder}] {PhaseName} complete — {spawned} animals on '{loc.Id}' ({loc.Width}×{loc.Height})");
        return Task.CompletedTask;
    }
}

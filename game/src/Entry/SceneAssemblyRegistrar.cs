#nullable enable
using CultivationGame.Core.DI;
using CultivationGame.Entry.Phases;

namespace CultivationGame.Entry;

/// <summary>
/// Registers the <see cref="SceneOrchestrator"/> and the scene-assembly
/// phases with the root DI container. Called from <see cref="GameLifetimeScope"/>.
/// </summary>
/// <remarks>
/// Phases are registered as singletons so the orchestrator can discover
/// them via <c>IResolver.ResolveAll&lt;ISceneAssemblyPhase&gt;()</c> on
/// first assembly run.
/// </remarks>
public static class SceneAssemblyRegistrar
{
    public static void Register(IContainerBuilder builder)
    {
        // Orchestrator
        builder.Register<SceneOrchestrator>(Lifetime.Singleton);

        // Phases in execution order (orchestrator sorts by PhaseOrder at
        // runtime, so registration order here is purely cosmetic).
        builder.Register<CoreValidationPhase>(Lifetime.Singleton);
        builder.Register<TileMapGenPhase>(Lifetime.Singleton);
        builder.Register<WorldInitPhase>(Lifetime.Singleton);
        builder.Register<PlayerSpawnPhase>(Lifetime.Singleton);
        // Phase 5 — Phase C (BODY-IMPL-PLAN): spawn simple wandering animals
        // (wolf/deer/rabbit) on the test polygon. Replaces the v1 stub
        // NPCSpawnPhase which logged "No NPCs in test polygon".
        builder.Register<AnimalSpawnPhase>(Lifetime.Singleton);
        // NPC_COMBAT_PREP Phase 1 — spawn human NPCs (merchant/cultivator/
        // guard/passerby) through the full assembly pipeline.
        builder.Register<HumanNPCSpawnPhase>(Lifetime.Singleton);
        // GROUP-SPAWN Phase 7 — spawns NPC groups (wolf pack, guard patrol,
        // trade caravan, deer herd) on the large world. Each group is created
        // via INPCGroupService and populated with members spawned through
        // NPCSpawnerService (NPCs) or AnimalService (animals).
        builder.Register<GroupSpawnPhase>(Lifetime.Singleton);
        builder.Register<FormationInitPhase>(Lifetime.Singleton);
        builder.Register<ChargerInitPhase>(Lifetime.Singleton);
        builder.Register<QuestInitPhase>(Lifetime.Singleton);
        builder.Register<UIInitPhase>(Lifetime.Singleton);
        builder.Register<FinalizePhase>(Lifetime.Singleton);
    }
}

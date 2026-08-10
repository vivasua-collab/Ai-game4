#nullable enable
using CultivationGame.Core.DI;
using CultivationGame.Entry.Phases;

namespace CultivationGame.Entry;

/// <summary>
/// Registers the <see cref="SceneOrchestrator"/> and the 10 scene-assembly
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

        // 10 phases in execution order (orchestrator sorts by PhaseOrder at
        // runtime, so registration order here is purely cosmetic).
        builder.Register<CoreValidationPhase>(Lifetime.Singleton);
        builder.Register<TileMapGenPhase>(Lifetime.Singleton);
        builder.Register<WorldInitPhase>(Lifetime.Singleton);
        builder.Register<PlayerSpawnPhase>(Lifetime.Singleton);
        builder.Register<NPCSpawnPhase>(Lifetime.Singleton);
        builder.Register<FormationInitPhase>(Lifetime.Singleton);
        builder.Register<ChargerInitPhase>(Lifetime.Singleton);
        builder.Register<QuestInitPhase>(Lifetime.Singleton);
        builder.Register<UIInitPhase>(Lifetime.Singleton);
        builder.Register<FinalizePhase>(Lifetime.Singleton);
    }
}

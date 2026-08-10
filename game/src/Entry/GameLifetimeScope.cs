#nullable enable
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Modules.Body;
using CultivationGame.Modules.Buff;
using CultivationGame.Modules.Charger;
using CultivationGame.Modules.Combat;
using CultivationGame.Modules.Formation;
using CultivationGame.Modules.Generator;
using CultivationGame.Modules.Interaction;
using CultivationGame.Modules.Inventory;
using CultivationGame.Modules.NPC;
using CultivationGame.Modules.Player;
using CultivationGame.Modules.Qi;
using CultivationGame.Modules.Quest;
using CultivationGame.Modules.Save;
using CultivationGame.Modules.Tile;
using CultivationGame.Modules.UI;
using CultivationGame.Modules.World;

namespace CultivationGame.Entry;

/// <summary>
/// Root DI configurator. Builds the entire application container by
/// composing the 16 module <c>XxxModuleServices.Register</c> entry points,
/// the scene-assembly pipeline, and the session/entry-point singletons.
/// </summary>
/// <remarks>
/// <para><b>Circular IResolver note:</b> the <see cref="IResolver"/>
/// interface is NOT explicitly registered here. The <c>Container</c>
/// implementation authored by the Core layer implements both
/// <c>IContainerBuilder</c> and <c>IResolver</c>; <c>builder.Build()</c>
/// returns the container itself as <see cref="IResolver"/>, and the
/// container special-cases <c>Resolve&lt;IResolver&gt;()</c> to return
/// <c>this</c>. Services that need <see cref="IResolver"/> (e.g.
/// <see cref="SceneOrchestrator"/>, <see cref="GameEntryPoint"/>) have
/// <c>[Inject] IResolver</c> fields that are filled post-build.</para>
/// <para><b>Module registration order</b> follows DI_AND_EVENTBUS §1.2
/// (adjusted per task brief): World, Tile, Body, Qi, Buff, Charger,
/// Inventory, Combat, Formation, NPC, Player, Quest, Interaction, UI,
/// Save, Generator. Order matters where module constructors depend on
/// interfaces registered by earlier modules (resolved lazily by the
/// container, but registration order can affect startable iteration).</para>
/// </remarks>
public static class GameLifetimeScope
{
    /// <summary>
    /// Build and return the root resolver. Call once at process startup
    /// (from the adapter's bootstrap node) and retain the result for the
    /// lifetime of the application.
    /// </summary>
    public static IResolver Build()
    {
        var builder = new ContainerBuilder();

        // 1. EventBus (publisher/subscriber factories resolve against this).
        var eventBus = new EventBus();
        builder.RegisterInstance(eventBus);

        // 2. 16 module services — registration order per DI_AND_EVENTBUS §1.2.
        WorldModuleServices.Register(builder);
        TileModuleServices.Register(builder);
        BodyModuleServices.Register(builder);
        QiModuleServices.Register(builder);
        BuffModuleServices.Register(builder);
        ChargerModuleServices.Register(builder);
        InventoryModuleServices.Register(builder);
        CombatModuleServices.Register(builder);
        FormationModuleServices.Register(builder);
        NPCModuleServices.Register(builder);
        PlayerModuleServices.Register(builder);
        QuestModuleServices.Register(builder);
        InteractionModuleServices.Register(builder);
        UIModuleServices.Register(builder);
        SaveModuleServices.Register(builder);
        GeneratorModuleServices.Register(builder);

        // 3. Scene-assembly pipeline (orchestrator + 10 phases).
        SceneAssemblyRegistrar.Register(builder);

        // 4. Session + entry point.
        builder.Register<IGameSession, GameSession>(Lifetime.Singleton);
        builder.Register<GameEntryPoint>(Lifetime.Singleton);

        // 5. Build — the returned container is itself the IResolver.
        return builder.Build();
    }
}

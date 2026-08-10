#nullable enable
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Generator;

/// <summary>
/// Generator module — UTILITY module (no Module.cs, no Tick).
/// Just registers the GeneratorService so other modules (combat loot, NPC
/// equipment, technique drops) can resolve IGeneratorService and roll items.
/// </summary>
public static class GeneratorModuleServices
{
    public static void Register(IContainerBuilder builder)
    {
        builder.Register<GeneratorConfig>(Lifetime.Singleton);
        builder.Register<GeneratorService>(Lifetime.Singleton);
        builder.Register<IGeneratorService, GeneratorService>(Lifetime.Singleton);
        // No GeneratorModule class — utility module, no tick, no Start.
    }
}

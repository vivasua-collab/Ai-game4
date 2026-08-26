#nullable enable
// Создано: 2026-05-10 — Phase 17B: делегат регистрации модуля
// Migrated from Ai-game3 (Unity+VContainer+MessagePipe) to Ai-game4 (Godot+DI+EventBus) 2026-08-15:
//   - Register(IContainerBuilder, MessagePipeOptions) → Register(IContainerBuilder)
//   - builder.Register<X>.As<I>().AsSelf() → builder.Register<I, X>()
//   - RegisterBuildCallback removed (no equivalent; SetConfig called explicitly by Entry phase)
//   - BodyLifetimeScope merged here (deleted)
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Body;

/// <summary>
/// Делегат регистрации публичных сервисов модуля Body.
/// Заменяет stub-регистрации.
/// </summary>
public static class BodyModuleServices
{
    /// <summary>
    /// Зарегистрировать все публичные сервисы модуля Body.
    /// </summary>
    public static void Register(IContainerBuilder builder)
    {
        // === Публичные сервисы ===
        // Phase 18A FIX: единый singleton для IBodyService + ISaveable
        // (в VContainer: Register<BodyService>().As<IBodyService>().As<ISaveable>().AsSelf())
        // Our DI: register concrete BodyService as singleton, then expose it as IBodyService via separate registration.
        // NOTE: Our minimal DI doesn't support multi-interface As<>(). Register the same instance under multiple
        // interfaces by using RegisterInstance after first resolution — for v1, register as IBodyService only.
        builder.Register<IBodyService, BodyService>(Lifetime.Singleton);
        builder.Register<IBodyDataProvider, BodyService>(Lifetime.Singleton);
        builder.Register<ISaveable, BodyService>(Lifetime.Singleton);

        // === Точка входа модуля ===
        builder.Register<BodyModule>(Lifetime.Singleton);

        // === П.23 Этап 7: Дебаффы от потери частей тела ===
        builder.Register<SeveredDebuffSystem>(Lifetime.Singleton);

        // === Внутренние сервисы модуля Body ===
        builder.Register<IBodyFactory, BodyFactory>(Lifetime.Singleton);
        builder.Register<BodyTemplateProvider>(Lifetime.Singleton);
        builder.Register<SpeciesRegistry>(Lifetime.Singleton);

        // === Система врождённых усилений тела (Task 2.3) ===
        builder.Register<BodyEnhancementSystem>(Lifetime.Singleton);

        // === Конфигурация по умолчанию (гуманоид-практик) ===
        // TODO P2-03 (V3): параметризовать EntityId для NPC-тел вместо хардкода "player"
        var defaultConfig = new BodyConfig
        {
            EntityId = "player",
            Morphology = Morphology.Humanoid,
            Material = BodyMaterial.Organic,
            Size = SizeClass.Medium,
            Vitality = 10f
        };
        builder.RegisterInstance(defaultConfig);
    }
}

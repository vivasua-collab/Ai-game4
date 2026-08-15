#nullable enable
// Создано: 2026-05-10 — Phase 17B: делегат регистрации модуля
// Migrated from Ai-game3 (Unity+VContainer+MessagePipe) to Ai-game4 (Godot+DI+EventBus) 2026-08-15.
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Formation;

/// <summary>
/// Делегат регистрации публичных сервисов модуля Formation.
/// </summary>
public static class FormationModuleServices
{
    public static void Register(IContainerBuilder builder)
    {
        builder.Register<IFormationService, FormationService>(Lifetime.Singleton);
        builder.Register<FormationModule>(Lifetime.Singleton);

        // Конфигурация по умолчанию
        var defaultConfig = new FormationConfig
        {
            DefaultCasterId = "player",
        };
        builder.RegisterInstance(defaultConfig);
    }
}

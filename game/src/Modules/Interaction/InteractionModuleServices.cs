#nullable enable
// Создано: 2026-05-10 — Phase 17B: делегат регистрации модуля
// Migrated from Ai-game3 (Unity+VContainer+MessagePipe) to Ai-game4 (Godot+DI+EventBus) 2026-08-15.
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Interaction;

/// <summary>
/// Делегат регистрации публичных сервисов модуля Interaction.
/// </summary>
public static class InteractionModuleServices
{
    public static void Register(IContainerBuilder builder)
    {
        // === Конфигурация по умолчанию ===
        var config = new InteractionConfig();
        builder.RegisterInstance(config);

        // === Внутренние сервисы ===
        builder.Register<DialogueTypewriter>(Lifetime.Singleton);

        // === Публичные сервисы ===
        builder.Register<IInteractionService, InteractionService>(Lifetime.Singleton);
        builder.Register<IDialogueService, DialogueService>(Lifetime.Singleton);

        // === Точка входа модуля ===
        builder.Register<InteractionModule>(Lifetime.Singleton);
    }
}

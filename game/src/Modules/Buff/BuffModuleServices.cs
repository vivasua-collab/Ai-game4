#nullable enable
// Создано: 2026-05-10 — Phase 17B: делегат регистрации модуля
// Migrated from Ai-game3 (Unity+VContainer+MessagePipe) to Ai-game4 (Godot+DI+EventBus) 2026-08-15:
//   - Register(IContainerBuilder, MessagePipeOptions) → Register(IContainerBuilder)
//   - builder.Register<X>.As<I>().AsSelf() → builder.Register<I, X>()
//   - RegisterBuildCallback removed (no equivalent)
//   - BuffLifetimeScope merged here (deleted)
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Buff;

/// <summary>
/// Делегат регистрации публичных сервисов модуля Buff.
/// </summary>
public static class BuffModuleServices
{
    /// <summary>
    /// Зарегистрировать все публичные сервисы модуля Buff.
    /// </summary>
    public static void Register(IContainerBuilder builder)
    {
        // === Внутренние сервисы ===
        builder.Register<BuffTickProcessor>(Lifetime.Singleton);

        // === Публичные сервисы ===
        builder.Register<IBuffService, BuffService>(Lifetime.Singleton);

        // === Точка входа модуля ===
        builder.Register<BuffModule>(Lifetime.Singleton);

        // === Конфигурация по умолчанию ===
        var defaultConfig = new BuffConfig
        {
            DefaultTickInterval = 1f,
            MaxBuffsPerEntity = 20
        };
        builder.RegisterInstance(defaultConfig);
    }
}

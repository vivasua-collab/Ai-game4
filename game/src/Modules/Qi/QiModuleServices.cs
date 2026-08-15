#nullable enable
// Создано: 2026-05-10 — Phase 17B: делегат регистрации модуля
// Migrated from Ai-game3 (Unity+VContainer+MessagePipe) to Ai-game4 (Godot+DI+EventBus) 2026-08-15:
//   - Register(IContainerBuilder, MessagePipeOptions) → Register(IContainerBuilder)
//   - builder.Register<X>.As<I>().AsSelf() → builder.Register<I, X>()
//   - RegisterBuildCallback removed (no equivalent; SetConfig called explicitly by Entry phase)
//   - QiLifetimeScope merged here (deleted)
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Qi;

/// <summary>
/// Делегат регистрации публичных сервисов модуля Qi.
/// Вызывается из Entry phase для регистрации реальных сервисов
/// вместо stub-сервисов.
/// </summary>
public static class QiModuleServices
{
    /// <summary>
    /// Зарегистрировать все публичные сервисы модуля Qi.
    /// Заменяет stub-регистрации.
    /// </summary>
    public static void Register(IContainerBuilder builder)
    {
        // === Публичные сервисы ===
        builder.Register<IQiService, QiService>(Lifetime.Singleton);
        builder.Register<IQiBufferService, QiBufferService>(Lifetime.Singleton);
        builder.Register<IQiDataProvider, QiDataProvider>(Lifetime.Singleton);

        // === Точка входа модуля ===
        builder.Register<QiModule>(Lifetime.Singleton);

        // === Конфигурация по умолчанию (практик L1.0, нормальное ядро) ===
        var defaultConfig = new QiConfig
        {
            EntityId = "player",
            CultivationLevel = 1,
            SubLevel = 0,
            CoreQuality = CoreQuality.Normal,
            InitialQi = -1, // Заполнить до максимума
            ConductivityBonus = 0f,
            EnablePassiveRegen = true,
            RegenMultiplier = 1f
        };
        builder.RegisterInstance(defaultConfig);
    }
}

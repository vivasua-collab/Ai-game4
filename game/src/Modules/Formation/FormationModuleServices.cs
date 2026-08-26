#nullable enable
// Создано: 2026-05-10 — Phase 17B: делегат регистрации модуля
// Редактировано: 2026-08-23 — Этап 4 внедрения ЦИ: +FormationRegistry (реестр
// генерируемых формаций, читается FormationService.FindFormationData).
// Migrated from Ai-game3 (Unity+VContainer+MessagePipe) to Ai-game4 (Godot+DI+EventBus) 2026-08-15.
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Data; // FormationData (аудит-1 A-2: перенесён в Core.Data)

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
        // Этап 4 внедрения ЦИ: реестр формаций (генератор пишет, сервис читает).
        builder.Register<FormationRegistry>(Lifetime.Singleton);

        // Конфигурация по умолчанию
        var defaultConfig = new FormationConfig
        {
            DefaultCasterId = "player",
        };
        builder.RegisterInstance(defaultConfig);
    }
}

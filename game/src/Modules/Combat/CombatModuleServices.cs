#nullable enable
// Создано: 2026-05-10 — Phase 17B: делегат регистрации модуля
// Migrated from Ai-game3 (Unity+VContainer+MessagePipe) to Ai-game4 (Godot+DI+EventBus) 2026-08-15.
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Combat;

/// <summary>
/// Делегат регистрации публичных сервисов модуля Combat.
/// </summary>
public static class CombatModuleServices
{
    /// <summary>
    /// Зарегистрировать все публичные сервисы модуля Combat.
    /// </summary>
    public static void Register(IContainerBuilder builder)
    {
        // === Публичные сервисы ===
        builder.Register<ICombatService, CombatService>(Lifetime.Singleton);
        builder.Register<IDamageService, DamageService>(Lifetime.Singleton);

        // === Внутренние сервисы ===
        builder.Register<IStatProvider, StatProviderAdapter>(Lifetime.Singleton);

        // Техники
        builder.Register<TechniqueService>(Lifetime.Singleton);
        builder.Register<TechniqueChargeService>(Lifetime.Singleton);

        // AI
        builder.Register<CombatAIService>(Lifetime.Singleton);

        // Лут
        builder.Register<CombatLootService>(Lifetime.Singleton);

        // Спринт 7 C7: CombatConsequencesService
        builder.Register<CombatConsequencesService>(Lifetime.Singleton);

        // Спринт 7 C8: ElementalEffectService
        builder.Register<ElementalEffectService>(Lifetime.Singleton);

        // === Точка входа модуля ===
        builder.Register<CombatModule>(Lifetime.Singleton);

        // === Конфигурация по умолчанию ===
        var defaultConfig = new CombatConfig
        {
            PlayerEntityId = "player",
            EnableAI = true,
            AITurnDelay = 1.0f,
            MaxCombatDuration = 0f,
            AutoLootOnVictory = true,
            PlayerDamageMultiplier = 1.0f,
            EnemyDamageMultiplier = 1.0f,
        };
        builder.RegisterInstance(defaultConfig);
    }
}

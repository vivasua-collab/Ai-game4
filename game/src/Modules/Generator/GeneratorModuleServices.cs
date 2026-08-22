#nullable enable
// Создано: 2026-05-18 17:58:25 UTC
// Делегат регистрации публичных сервисов модуля Generator.
// Migrated from Ai-game3 (Unity+VContainer) to Ai-game4 (Godot+DI) 2026-08-15.
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Generator;

/// <summary>
/// Делегат регистрации публичных сервисов модуля Generator.
/// </summary>
public static class GeneratorModuleServices
{
    public static void Register(IContainerBuilder builder)
    {
        // === База данных предметов ===
        builder.Register<IItemDatabaseService, ItemDatabaseService>(Lifetime.Singleton);

        // === Генератор предметов ===
        builder.Register<IItemGeneratorService, ItemGeneratorService>(Lifetime.Singleton);
        // 2026-08-22: генератор экипировки «Матрёшка» (EQUIPMENT_SYSTEM.md §2)
        builder.Register<IEquipmentGenerator, EquipmentGenerator>(Lifetime.Singleton);

        // === Реестр техник ===
        builder.Register<TechniqueRegistry>(Lifetime.Singleton);

        // === Генератор техник ===
        builder.Register<ITechniqueGeneratorService, TechniqueGeneratorService>(Lifetime.Singleton);

        // === Точка входа модуля ===
        builder.Register<GeneratorModule>(Lifetime.Singleton);
    }
}

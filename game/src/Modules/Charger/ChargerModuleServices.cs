#nullable enable
// Создано: 2026-05-10 — Phase 17B: делегат регистрации модуля
// Migrated from Ai-game3 (Unity+VContainer+MessagePipe) to Ai-game4 (Godot+DI+EventBus) 2026-08-15.
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Charger;

/// <summary>
/// Делегат регистрации публичных сервисов модуля Charger.
/// </summary>
public static class ChargerModuleServices
{
    /// <summary>
    /// Зарегистрировать все публичные сервисы модуля Charger.
    /// </summary>
    public static void Register(IContainerBuilder builder)
    {
        // === Публичные сервисы ===
        builder.Register<IChargerService, ChargerService>(Lifetime.Singleton);
        builder.Register<ISaveable, ChargerService>(Lifetime.Singleton);

        // === Точка входа модуля ===
        builder.Register<ChargerModule>(Lifetime.Singleton);

        // === Конфигурация по умолчанию (пояс-накопитель) ===
        // В реальной игре конфигурация загружается из JSON / SaveData
        var bufferConfig = new ChargerBufferConfig
        {
            Capacity = 500,
            Conductivity = 10f,
            EfficiencyLoss = GameConstants.CHARGER_EFFICIENCY_LOSS
        };

        var slotConfigs = new List<ChargerSlotConfig>();
        for (int i = 0; i < 3; i++)
        {
            slotConfigs.Add(new ChargerSlotConfig
            {
                Index = i,
                MinQualityRequired = QiStoneQuality.Common,
                MaxSizeAllowed = QiStoneSize.Huge,
                IsActive = true,
                IsSealed = false,
                AbsorptionBonus = 0f,
                QiRetention = 0.95f
            });
        }
        builder.RegisterInstance(bufferConfig);
        builder.RegisterInstance(slotConfigs);
    }
}

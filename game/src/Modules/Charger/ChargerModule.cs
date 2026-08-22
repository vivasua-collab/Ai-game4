#nullable enable
// Создано: 2026-05-08 12:52:40 UTC
// Точка входа модуля зарядников Ци.
// IStartable — инициализация, ITickable — кадровое обновление
// Migrated from Ai-game3 (Unity+VContainer+MessagePipe) to Ai-game4 (Godot+DI+EventBus) 2026-08-15.
using System;
using System.Collections.Generic;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Charger;

/// <summary>
/// Точка входа модуля Charger.
/// Инициализирует сервис зарядника и запускает кадровое обновление.
/// </summary>
public class ChargerModule : IModule
{
    [Inject] private readonly IChargerService _chargerService = null!;
    [Inject] private readonly ChargerService _chargerServiceImpl = null!;

    // IMPL-3: Configs injected via DI (replaces obsolete SetConfig()).
    [Inject] private readonly ChargerBufferConfig _bufferConfig = null!;
    [Inject] private readonly List<ChargerSlotConfig> _slotConfigs = null!;

    public string ModuleName => "Charger";

    public void Start()
    {
        // Phase 17C: прямая инъекция вместо concrete-cast
        _chargerServiceImpl.Configure(_bufferConfig, _slotConfigs);

        // Автоактивация при старте
        _chargerService.Activate();
    }

    public void Tick(int tickCount)
    {
        // CH-04: Tick через интерфейс — без приведения типов
        _chargerService.Tick();
    }

    public void Dispose()
    {
        // Services own their subscriptions and dispose themselves.
    }
}

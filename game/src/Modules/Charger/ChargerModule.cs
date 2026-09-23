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

    // R37-c (баг-репорт 23.09): мост — подписка на экипировку + первичная
    // синхронизация режима. Домен НЕ активен без надетого зарядника
    // (прежде Activate() безусловно — камни качались даже без зарядника).
    [Inject] private readonly ChargerItemBridge _bridge = null!;

    public string ModuleName => "Charger";

        // P1-10 (аудит 09.22, Фазы 7/10): идемпотентный Start — повторный вызов
    // (прямой вызов вне GameEntryPoint / повторная инстанциация сцены) не
    // дублирует подписки/инициализацию. Флаг — только при УСПЕШНОМ завершении
    // (провал остаётся ретраимым). Основной слой защиты — GameEntryPoint
    // (_startedModules, resume from failure); это страховка от прямых вызовов.
    private bool _startCompleted;

    public void Start()
    {
        if (_startCompleted) return;

        // Phase 17C: прямая инъекция вместо concrete-cast
        _chargerServiceImpl.Configure(_bufferConfig, _slotConfigs);

        // R37-c: режим домена синхронизирует мост (гейт по надетому
        // заряднику). Activate() безусловно — УБРАН. После загрузки сейва
        // RestoreState восстановит сохранённый режим, а событие
        // EquipmentChangedEvent (EquipmentService.RestoreState публикует)
        // досинхронизирует мост.
        _bridge.Initialize();
    
        _startCompleted = true;
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

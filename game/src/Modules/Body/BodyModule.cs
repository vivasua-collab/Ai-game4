#nullable enable
// Создано: 2026-05-08 15:54:00 UTC
// Точка входа модуля Body.
// IStartable — инициализация, ITickable — кадровая регенерация.
// Migrated from Ai-game3 (Unity+VContainer+MessagePipe) to Ai-game4 (Godot+DI+EventBus) 2026-08-15.
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Body;

/// <summary>
/// Конфигурация модуля тела.
/// P2-02 (V3) FIX: init-only свойства вместо мутабельных полей.
/// Устанавливается через SetConfig() из BodyModuleServices.Register().
/// </summary>
public class BodyConfig
{
    public string EntityId { get; init; } = "player";
    public Morphology Morphology { get; init; } = Morphology.Humanoid;
    public BodyMaterial Material { get; init; } = BodyMaterial.Organic;
    public SizeClass Size { get; init; } = SizeClass.Medium;
    public float Vitality { get; init; } = 10f;
}

/// <summary>
/// Точка входа модуля Body.
/// Инициализирует BodyService конфигурацией и запускает регенерацию.
/// BD-42: Использует ITimeService.DeltaTime вместо UnityEngine.Time.deltaTime.
/// </summary>
public class BodyModule : IModule
{
    [Inject] private readonly IBodyService _bodyService = null!;
    // BD-42: Инжекция ITimeService (урок CH-32/33 из Фазы 1)
    [Inject] private readonly ITimeService _timeService = null!;

    // B5-E02: Подписка на тики периодических эффектов (DoT/HoT)
    [Inject] private readonly ISubscriber<BuffTickedEvent> _buffTickedSub = null!;
    private IDisposable? _buffTickedSubscription;

    // П.24: Подписка на изменение Vitality → пересчёт HP
    [Inject] private readonly ISubscriber<StatChangedEvent> _statChangedSub = null!;
    private IDisposable? _statChangedSubscription;

    private bool _isConfigured;
    private BodyConfig? _config;

    public string ModuleName => "Body";

    /// <summary>
    /// Установить конфигурацию тела.
    /// Вызывается из BodyModuleServices.Register() до Start().
    /// </summary>
    public void SetConfig(BodyConfig config)
    {
        _config = config;
        _isConfigured = true;
    }

    public void Start()
    {
        // Инициализация через интерфейс IBodyService (P1-06 FIX)
        if (_isConfigured && _config != null)
        {
            _bodyService.Initialize(
                _config.EntityId,
                _config.Morphology,
                _config.Material,
                _config.Size,
                _config.Vitality);
        }

        // B5-E02: Подписка на BuffTickedEvent — обработка тиков HoT/DoT
        _buffTickedSubscription = _buffTickedSub.Subscribe(OnBuffTicked);

        // П.24: Подписка на StatChangedEvent (VIT) — пересчёт HP
        _statChangedSubscription = _statChangedSub.Subscribe(OnStatChanged);
    }

    public void Tick(int tickCount)
    {
        // BD-42: Регенерация через ITimeService.DeltaTime (не UnityEngine.Time)
        // Корректно работает при паузе (ITimeService может возвращать 0)
        _bodyService.ProcessRegeneration(_timeService.DeltaTime);
    }

    /// <summary>
    /// B5-E02: Обработчик тика периодического эффекта (DoT/HoT).
    /// HoT (HealthRegen) — применяет исцеление к торсу через BodyService.
    /// DoT (Poison/Burn/Bleed/Freeze) — логирование, урон проходит через CombatPipeline.
    /// EventBus handler signature: void OnXxx(in XxxEvent e).
    /// </summary>
    private void OnBuffTicked(in BuffTickedEvent e)
    {
        // Определяем тип эффекта по BuffType
        switch (e.Type)
        {
            case BuffType.HealthRegen:
                // HoT: исцеляем торс (основная часть тела)
                // TODO P1-08 (V3): распределение HoT по повреждённым частям
                // Сейчас лечит только Torso — при ампутированных частях HoT не помогает им
                _bodyService.HealPart(BodyPartType.Torso, (int)e.TickValue);
                break;

            case BuffType.Poison:
            case BuffType.Burn:
            case BuffType.Bleed:
            case BuffType.Freeze:
                // DoT: урон должен проходить через CombatPipeline
                // Пока логируем — полная реализация в будущих фазах
                Console.WriteLine(
                    $"[BodyModule] DoT тик: {e.BuffId} → {e.EntityId}, урон={e.TickValue}");
                break;

            default:
                // Прочие периодические эффекты (QiRestoration, StaminaRegen и т.д.)
                // Обработка в соответствующих модулях
                Console.WriteLine(
                    $"[BodyModule] Тик баффа: {e.BuffId} → {e.EntityId}, значение={e.TickValue}");
                break;
        }
    }

    /// <summary>
    /// B5-E02: Освобождение подписок.
    /// </summary>
    public void Dispose()
    {
        _buffTickedSubscription?.Dispose();
        _buffTickedSubscription = null;
        _statChangedSubscription?.Dispose();
        _statChangedSubscription = null;
    }

    /// <summary>
    /// П.24: Обработчик изменения характеристики.
    /// При изменении Vitality — пересчитать HP всех частей тела.
    /// </summary>
    private void OnStatChanged(in StatChangedEvent e)
    {
        if (e.StatType != StatType.Vitality) return;
        // P1-08 FIX: guard для пустого EntityId (до Initialize)
        if (string.IsNullOrEmpty(_bodyService.EntityId)) return;
        if (e.EntityId != _bodyService.EntityId) return;

        // Делегируем пересчёт BodyService
        _bodyService.RecalculateHPFromVitality(e.OldValue, e.NewValue);
    }
}

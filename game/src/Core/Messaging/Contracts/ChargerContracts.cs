#nullable enable
namespace CultivationGame.Core.Messaging.Contracts;

// Created: 2026-05-08 (Ai-game3) — migrated 2026-08-15.
// Qi-charger contracts: slot state, heat/overheat, buffer changes.

/// <summary>
/// Событие: изменение состояния слота зарядника.
/// Публикуется при вставке/извлечении камня, изменении Ци в камне.
/// </summary>
public readonly struct ChargerStateChangedEvent
{
    public readonly int SlotIndex;
    public readonly ChargerSlotState OldState;
    public readonly ChargerSlotState NewState;
    public readonly float CurrentQi;
    public readonly float MaxQi;

    public ChargerStateChangedEvent(
        int slotIndex,
        ChargerSlotState oldState,
        ChargerSlotState newState,
        float currentQi,
        float maxQi)
    {
        SlotIndex = slotIndex;
        OldState = oldState;
        NewState = newState;
        CurrentQi = currentQi;
        MaxQi = maxQi;
    }
}

/// <summary>
/// Событие: перегрев зарядника.
/// Публикуется при достижении порога перегрева.
/// </summary>
public readonly struct ChargerOverheatedEvent
{
    public readonly float HeatLevel;
    public readonly float CooldownSeconds;

    public ChargerOverheatedEvent(float heatLevel, float cooldownSeconds)
    {
        HeatLevel = heatLevel;
        CooldownSeconds = cooldownSeconds;
    }
}

/// <summary>
/// Событие: остывание завершено.
/// Публикуется когда зарядник выходит из перегрева.
/// </summary>
public readonly struct ChargerCooledDownEvent
{
    public readonly float HeatLevel;

    public ChargerCooledDownEvent(float heatLevel)
    {
        HeatLevel = heatLevel;
    }
}

/// <summary>
/// Событие: изменение уровня тепла зарядника.
/// Публикуется при каждом изменении тепла (для UI).
/// </summary>
public readonly struct ChargerHeatChangedEvent
{
    public readonly float HeatPercent;
    public readonly HeatState State;

    public ChargerHeatChangedEvent(float heatPercent, HeatState state)
    {
        HeatPercent = heatPercent;
        State = state;
    }
}

/// <summary>
/// Событие: изменение Ци в буфере зарядника.
/// </summary>
public readonly struct ChargerBufferChangedEvent
{
    public readonly long CurrentQi;
    public readonly long Capacity;

    public ChargerBufferChangedEvent(long currentQi, long capacity)
    {
        CurrentQi = currentQi;
        Capacity = capacity;
    }
}

/// <summary>
/// Состояние слота зарядника.
/// Используется в IChargerService и ChargerStateChangedEvent.
/// </summary>
public enum ChargerSlotState
{
    Empty,      // Слот пуст
    Active,     // Камень вставлен, работает
    Depleted,   // Камень истощён (Qi = 0)
    Sealed,     // Слот запечатан
    Inactive    // Слот деактивирован
}

/// <summary>
/// Состояние теплового баланса зарядника.
/// </summary>
public enum HeatState
{
    Cool,       // 0-30% — нормальная работа
    Warm,       // 31-60% — повышенная температура
    Hot,        // 61-90% — высокая температура
    Critical,   // 91-99% — критическая температура
    Overheated  // 100% — перегрев (блокировка)
}

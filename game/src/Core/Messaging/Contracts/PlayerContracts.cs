#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Messaging.Contracts;

// Created: 2026-05-08 (Ai-game3) — migrated 2026-08-15.
// Player lifecycle: death, revive, sleep, position, stamina, currency.
// ЗАПРЕТ 3.9: all fractional values ×1000 (per-mille), int arithmetic.

public readonly struct PlayerDeathEvent
{
    public readonly string Cause;
    public PlayerDeathEvent(string cause) { Cause = cause; }
}

public readonly struct PlayerReviveEvent { }

public readonly struct PlayerSleepEvent
{
    public readonly float Hours;
    public readonly bool IsStarting;
    public PlayerSleepEvent(float hours, bool isStarting) { Hours = hours; IsStarting = isStarting; }
}

public readonly struct PlayerPositionChangedEvent
{
    public readonly float X;
    public readonly float Y;
    public PlayerPositionChangedEvent(float x, float y) { X = x; Y = y; }
}

/// <summary>
/// Публикуется PlayerModule при перемещении игрока на новый тайл.
/// Несёт порядковый номер тика и старую/новую позиции (для интерполяции
/// рендера и валидации движения).
/// </summary>
public readonly struct PlayerMovedEvent
{
    public readonly int TickCount;
    public readonly Position2D OldPosition;
    public readonly Position2D NewPosition;

    public PlayerMovedEvent(int tickCount, Position2D oldPosition, Position2D newPosition)
    {
        TickCount = tickCount;
        OldPosition = oldPosition;
        NewPosition = newPosition;
    }
}

// --- UI-2: Stamina ---

/// <summary>
/// Публикуется при изменении стамины игрока.
/// Все значения в промилле (‰): 1000‰ = полная стамина, 0‰ = пустая.
/// ЗАПРЕТ 3.9: целочисленная арифметика, без float/double/decimal.
/// </summary>
public readonly struct StaminaChangedEvent
{
    /// <summary>Текущая стамина в промилле от максимума (0–1000)</summary>
    public readonly int CurrentPromille;

    /// <summary>Максимальная стамина (абсолютное значение, единицы стамины)</summary>
    public readonly int MaxStamina;

    /// <summary>Текущая стамина (абсолютное значение)</summary>
    public readonly int CurrentStamina;

    public StaminaChangedEvent(int currentPromille, int currentStamina, int maxStamina)
    {
        CurrentPromille = currentPromille;
        CurrentStamina = currentStamina;
        MaxStamina = maxStamina;
    }
}

// --- UI-2: Currency ---

/// <summary>
/// Публикуется при изменении количества Духовных Камней.
/// </summary>
public readonly struct CurrencyChangedEvent
{
    /// <summary>Новое количество Духовных Камней</summary>
    public readonly int SpiritStones;

    /// <summary>Изменение (положительное — получение, отрицательное — трата)</summary>
    public readonly int Delta;

    public CurrencyChangedEvent(int spiritStones, int delta)
    {
        SpiritStones = spiritStones;
        Delta = delta;
    }
}

// --- R27 (2026-09-21): выбор цели игрока (TargetingService) ---

/// <summary>
/// R27 (план R23 §3.1): игрок переключил выбранную цель (Tab-цикл).
/// Публикуется TargetingService.CycleTarget. Потребители:
/// NPCSpriteRenderer/AnimalSpriteRenderer (рамка-подсветка выбранной),
/// HUD (имя цели), PlayerCombatAdapter/PlayerTechniqueCaster (атаки
/// предпочитают выбранную цель). TargetId пуст — выбор сброшен (цель
/// умерла/вышла из радиуса). Позиция — для VFX-подсветки без повторного
/// резолва (int-геометрия, ЗАПРЕТ 3.9).
/// </summary>
public readonly struct PlayerTargetChangedEvent
{
    /// <summary>ID выбранной цели (пусто = сброс)</summary>
    public readonly string TargetId;

    /// <summary>Позиция цели в тайлах (для рамки-подсветки)</summary>
    public readonly int TargetX;
    public readonly int TargetY;

    /// <summary>Чебышёв-дистанция до игрока (для UI)</summary>
    public readonly int DistanceTiles;

    public PlayerTargetChangedEvent(string targetId, int targetX, int targetY, int distanceTiles)
    {
        TargetId = targetId ?? string.Empty;
        TargetX = targetX;
        TargetY = targetY;
        DistanceTiles = distanceTiles;
    }
}

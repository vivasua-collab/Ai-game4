#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Messaging.Contracts;

// Created: 2026-05-08 (Ai-game3) — migrated 2026-08-15.
// Tile + resource contracts: tile changes, harvest, depletion, generation, respawn.
// HarvestResult lives here (used in ITileService.TryHarvest).

// === ВСПОМОГАТЕЛЬНЫЕ СТРУКТУРЫ ===

/// <summary>
/// Результат попытки сбора ресурса с тайла.
/// readonly struct — нулевая GC-аллокация.
/// </summary>
public readonly struct HarvestResult
{
    /// <summary>Идентификатор предмета, полученного при сборе</summary>
    public readonly string ItemId;

    /// <summary>Количество полученного предмета</summary>
    public readonly int Amount;

    /// <summary>Остаток ресурса на тайле</summary>
    public readonly float ResourceRemaining;

    /// <summary>Ресурс исчерпан (ResourceRemaining ≤ 0)</summary>
    public readonly bool Depleted;

    public HarvestResult(string itemId, int amount, float resourceRemaining, bool depleted)
    {
        ItemId = itemId;
        Amount = amount;
        ResourceRemaining = resourceRemaining;
        Depleted = depleted;
    }

    /// <summary>Пустой результат — сбор невозможен.</summary>
    public static HarvestResult Empty => new(string.Empty, 0, 0f, false);
}

// === ИСХОДЯЩИЕ СОБЫТИЯ ===

/// <summary>
/// Событие: изменение тайла.
/// Публикуется при SetTile, разрушении объекта, изменении ресурса.
/// </summary>
public readonly struct TileChangedEvent
{
    public readonly int X;
    public readonly int Y;
    public readonly GameTile OldTile;
    public readonly GameTile NewTile;

    public TileChangedEvent(int x, int y, in GameTile oldTile, in GameTile newTile)
    {
        X = x; Y = y; OldTile = oldTile; NewTile = newTile;
    }
}

/// <summary>
/// Событие: собран ресурс с тайла.
/// </summary>
public readonly struct ResourceHarvestedEvent
{
    public readonly int X;
    public readonly int Y;
    public readonly string ResourceId;
    public readonly string ItemId;
    public readonly int Amount;
    public readonly float Remaining;

    public ResourceHarvestedEvent(int x, int y, string resourceId, string itemId, int amount, float remaining)
    {
        X = x; Y = y; ResourceId = resourceId; ItemId = itemId;
        Amount = amount; Remaining = remaining;
    }
}

/// <summary>
/// Событие: ресурс на тайле исчерпан.
/// </summary>
public readonly struct ResourceDepletedEvent
{
    public readonly int X;
    public readonly int Y;
    public readonly string ResourceId;

    public ResourceDepletedEvent(int x, int y, string resourceId)
    {
        X = x; Y = y; ResourceId = resourceId;
    }
}

/// <summary>
/// Событие: карта сгенерирована.
/// </summary>
public readonly struct TileMapGeneratedEvent
{
    public readonly int Width;
    public readonly int Height;
    public readonly int Seed;

    public TileMapGeneratedEvent(int width, int height, int seed)
    {
        Width = width; Height = height; Seed = seed;
    }
}

/// <summary>
/// Событие: ресурс респаунился на тайле.
/// Публикуется ResourceService при респауне, TileMapService подписывается и обновляет тайл.
/// </summary>
public readonly struct ResourceRespawnedEvent
{
    public readonly int X;
    public readonly int Y;
    public readonly ObjectType OriginalObject;
    public readonly float ResourceMax;
    public readonly string ResourceId;

    public ResourceRespawnedEvent(int x, int y, ObjectType originalObject, float resourceMax, string resourceId)
    {
        X = x; Y = y; OriginalObject = originalObject;
        ResourceMax = resourceMax; ResourceId = resourceId;
    }
}

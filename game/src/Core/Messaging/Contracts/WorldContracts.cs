#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Messaging.Contracts;

public readonly struct LocationChangedEvent
{
    public readonly string OldLocationId;
    public readonly string NewLocationId;
    public LocationChangedEvent(string oldLocationId, string newLocationId)
    {
        OldLocationId = oldLocationId;
        NewLocationId = newLocationId;
    }
}

public readonly struct TileChangedEvent
{
    public readonly int X;
    public readonly int Y;
    public readonly TerrainType NewTerrain;
    public TileChangedEvent(int x, int y, TerrainType newTerrain)
    {
        X = x; Y = y; NewTerrain = newTerrain;
    }
}

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

public readonly struct LocationLoadedEvent
{
    public readonly string LocationId;
    public LocationLoadedEvent(string locationId) { LocationId = locationId; }
}

#nullable enable
using System;
using System.Collections.Generic;
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Interfaces;

/// <summary>
/// World / location management. The currently active location is loaded
/// into a scene; world map lists all reachable locations.
/// </summary>
public interface IWorldService
{
    LocationData? CurrentLocation { get; }
    void SetActiveLocation(string locationId);
    IReadOnlyList<LocationData> GetAvailableLocations();

    event Action<LocationData>? OnLocationChanged;
}

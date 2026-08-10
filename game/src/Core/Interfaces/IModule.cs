#nullable enable
using System;

namespace CultivationGame.Core.Interfaces;

/// <summary>Lifecycle hook invoked once after the scene assembly finishes.</summary>
public interface IStartable
{
    void Start();
}

/// <summary>Per-tick update hook. <paramref name="tickCount"/> is the global tick index.</summary>
public interface ITickable
{
    void Tick(int tickCount);
}

/// <summary>Marker for modules that own disposable resources.</summary>
public interface IDisposableModule : IDisposable
{
}

/// <summary>
/// Base contract for all 16 game modules. Modules are registered in the DI
/// container as singletons and ticked by the game loop.
/// </summary>
public interface IModule : IStartable, ITickable, IDisposableModule
{
    /// <summary>Stable module name (e.g. "Charger", "Combat"). Used for diagnostics.</summary>
    string ModuleName { get; }
}

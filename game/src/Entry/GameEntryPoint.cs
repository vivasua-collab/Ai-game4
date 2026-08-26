#nullable enable
using System;
using System.Collections.Generic;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Entry;

/// <summary>
/// Main game-loop driver. Pure C#; called by the Godot Adapter bootstrap
/// node (<c>GameBoot</c>) once per process.
/// </summary>
/// <remarks>
/// <para>Implements <see cref="IStartable"/> and <see cref="ITickable"/>
/// so the adapter can drive both startup and the fixed-tick simulation
/// loop through a single resolved instance.</para>
/// <para><b>Start:</b> collects every <see cref="IStartable"/> and
/// <see cref="ITickable"/> from the container (excluding self to avoid
/// recursion) and calls <c>Start()</c> on each in registration order
/// (modules first, then Entry services, then <c>GameEntryPoint</c> last
/// via the external <c>Start()</c> call from the adapter).</para>
/// <para><b>Tick:</b> forwards the tick to every collected
/// <see cref="ITickable"/> in order. Guarded by a re-entrancy flag in
/// case the adapter calls <c>Tick</c> from a re-entrant context.</para>
/// </remarks>
public sealed class GameEntryPoint : IStartable, ITickable
{
    [Inject] private readonly IResolver _resolver = null!;
    [Inject] private readonly IGameSession _session = null!;

    private readonly List<IStartable> _startables = new();
    private readonly List<ITickable> _tickables = new();
    private bool _initialized;
    private bool _ticking;

    /// <summary>
    /// Drives startup. Resolves all <see cref="IStartable"/> /
    /// <see cref="ITickable"/> (self excluded) and starts them.
    /// Safe to call from the adapter after the container is built.
    /// </summary>
    public void Start()
    {
        if (_initialized)
        {
            Console.WriteLine("[GameEntryPoint] Start() ignored — already initialised");
            return;
        }

        // Collect startables (exclude self to prevent recursion).
        var startables = _resolver.ResolveAll<IStartable>();
        foreach (var s in startables)
        {
            if (!ReferenceEquals(s, this)) _startables.Add(s);
        }

        // Collect tickables (exclude self; GameEntryPoint.Tick is driven
        // externally by the adapter and forwards to the list).
        var tickables = _resolver.ResolveAll<ITickable>();
        foreach (var t in tickables)
        {
            if (!ReferenceEquals(t, this)) _tickables.Add(t);
        }

        // Mark initialised BEFORE invoking Start() so any startable that
        // queries GameEntryPoint via DI sees the initialised state.
        _initialized = true;

        foreach (var s in _startables)
        {
            try
            {
                s.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameEntryPoint] Startable {s.GetType().Name} threw: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Console.WriteLine(
            $"[GameEntryPoint] Started. {_startables.Count} startables, {_tickables.Count} tickables, session={_session.GetType().Name}");
    }

    /// <summary>
    /// Forward a fixed tick to every collected <see cref="ITickable"/>.
    /// Re-entrancy-guarded: if Tick is invoked while already ticking
    /// (e.g. a startable triggers a tick during Start), the nested call
    /// is a no-op.
    /// </summary>
    /// <param name="tickCount">Monotonic tick counter (1 tick = 1 game minute).</param>
    public void Tick(int tickCount)
    {
        if (!_initialized || _ticking) return;
        _ticking = true;
        try
        {
            for (int i = 0; i < _tickables.Count; i++)
            {
                try
                {
                    _tickables[i].Tick(tickCount);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GameEntryPoint] Tickable {_tickables[i].GetType().Name} threw at tick {tickCount}: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        finally
        {
            _ticking = false;
        }
    }
}

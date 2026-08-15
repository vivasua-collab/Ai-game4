#nullable enable
using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Modules.Player;

/// <summary>
/// Player module — owns PlayerService + PlayerInputService + StatService.
/// CRITICAL (PLR-E06): ResetFrameFlags() is called LAST in Tick(), after all
/// other modules have consumed the input frame. This module is registered
/// last in the GameLifetimeScope for the same reason.
/// </summary>
public sealed class PlayerModule : IModule
{
    public string ModuleName => "Player";

    [Inject] private readonly IPlayerService _playerService = null!;
    [Inject] private readonly IPlayerInputService _playerInputService = null!;
    [Inject] private readonly IStatService _statService = null!;
    [Inject] private readonly IPublisher<PlayerMovedEvent> _positionPublisher = null!;

    private int _tickCount;

    public void Start()
    {
        // Spawn player at centre of test polygon (25, 25) if not already spawned.
        if (_playerService is PlayerService ps && !ps.IsSpawned)
        {
            ps.Spawn(new Position2D(25, 25));
        }
        Console.WriteLine($"[PlayerModule] Started — stat svc wired={_statService != null}");
    }

    public void Tick(int tickCount)
    {
        _tickCount = tickCount;
        // Read input — free 8-direction movement (includes diagonals).
        // MoveDirection is in per-mille: X and Y can be -1000..+1000.
        var dir = _playerInputService.MoveDirection;
        if (dir != Position2D.Zero)
        {
            int oldX = _playerService.Position.X;
            int oldY = _playerService.Position.Y;

            // Determine movement delta: each axis can be -1, 0, or +1.
            // Threshold 350 (≈35% of full deflection) to avoid accidental drift.
            int dx = 0, dy = 0;
            if (System.Math.Abs(dir.X) >= 350) dx = System.Math.Sign(dir.X);
            if (System.Math.Abs(dir.Y) >= 350) dy = System.Math.Sign(dir.Y);

            if (dx != 0 || dy != 0)
            {
                // Movement multiplier: 2 tiles per tick on Normal (doubled from 1).
                // Run (Shift) adds +1 extra tile (3 total on Normal).
                int steps = _playerInputService.RunHeld ? 3 : 2;
                int newX = oldX;
                int newY = oldY;
                for (int i = 0; i < steps; i++)
                {
                    newX = Math.Clamp(newX + dx, 0, 49);
                    newY = Math.Clamp(newY + dy, 0, 49);
                }
                if (newX != oldX || newY != oldY)
                {
                    _playerService.SetPosition(new Position2D(newX, newY));
                    _positionPublisher.Publish(new PlayerMovedEvent(
                        _tickCount,
                        new Position2D(oldX, oldY),
                        new Position2D(newX, newY)));
                }
            }
        }

        // PLR-E06: ResetFrameFlags() LAST.
        _playerInputService.ResetFrameFlags();
    }

    public void Dispose()
    {
        Console.WriteLine("[PlayerModule] Disposed");
    }
}

/// <summary>
/// DI registration helper for the Player module. Called by GameLifetimeScope.
/// </summary>
public static class PlayerModuleServices
{
    public static void Register(IContainerBuilder builder)
    {
        builder.Register<PlayerConfig>(Lifetime.Singleton);
        builder.Register<PlayerService>(Lifetime.Singleton);
        builder.Register<PlayerInputService>(Lifetime.Singleton);
        builder.Register<StatService>(Lifetime.Singleton);
        builder.Register<IPlayerService, PlayerService>(Lifetime.Singleton);
        builder.Register<IPlayerInputService, PlayerInputService>(Lifetime.Singleton);
        builder.Register<IStatService, StatService>(Lifetime.Singleton);
        builder.Register<PlayerModule>(Lifetime.Singleton);
    }
}

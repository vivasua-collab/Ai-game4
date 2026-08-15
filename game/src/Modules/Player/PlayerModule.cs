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
        // Spawn player at origin if not already spawned by SceneOrchestrator.
        if (_playerService is PlayerService ps && !ps.IsSpawned)
        {
            ps.Spawn(Position2D.Zero);
        }
        Console.WriteLine($"[PlayerModule] Started — stat svc wired={_statService != null}");
    }

    public void Tick(int tickCount)
    {
        _tickCount = tickCount;
        // Read input and apply movement (V1: simple axis move via MoveDirection)
        var dir = _playerInputService.MoveDirection;
        if (dir != Position2D.Zero)
        {
            int oldX = _playerService.Position.X;
            int oldY = _playerService.Position.Y;
            // V1: snap to tile (1 tile per tick in dominant axis)
            int dx = 0, dy = 0;
            // MoveDirection is in per-mille (1000 = 1.0). ≥500 = dominant axis.
            if (System.Math.Abs(dir.X) >= 500) dx = System.Math.Sign(dir.X);
            if (System.Math.Abs(dir.Y) >= 500) dy = System.Math.Sign(dir.Y);
            if (dx != 0 || dy != 0)
            {
                int newX = oldX + dx;
                int newY = oldY + dy;
                // Clamp to world bounds (test polygon 50×50).
                // TODO: replace with dynamic bounds from IWorldService when available.
                newX = Math.Clamp(newX, 0, 49);
                newY = Math.Clamp(newY, 0, 49);
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

        // PLR-E06: ResetFrameFlags() LAST — must be after all other consumers
        // have read this frame's input. PlayerModule is registered last in
        // GameLifetimeScope so its Tick runs last in the module-tick order.
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

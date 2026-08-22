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
    [Inject] private readonly ITileService _tileService = null!;
    [Inject] private readonly IPublisher<PlayerMovedEvent> _positionPublisher = null!;

    private int _tickCount;
    private Position2D? _mouseDestination;  // null = keyboard movement, non-null = mouse-click movement

    public void Start()
    {
        // Spawn player at centre of test polygon (25, 25) if not already spawned.
        if (_playerService is PlayerService ps && !ps.IsSpawned)
        {
            ps.Spawn(new Position2D(25, 25));
        }
        Console.WriteLine($"[PlayerModule] Started — stat svc wired={_statService != null}");
    }

    /// <summary>
    /// Max valid X coordinate (MapWidth - 1). Falls back to the default
    /// constant when the tile service has not generated the grid yet.
    /// Replaces previously hardcoded literal "49" (audit issue #15).
    /// </summary>
    private int MaxX => _tileService != null && _tileService.MapWidth > 0
        ? _tileService.MapWidth - 1
        : GameConstants.DEFAULT_MAP_WIDTH - 1;

    /// <summary>Max valid Y coordinate. See <see cref="MaxX"/>.</summary>
    private int MaxY => _tileService != null && _tileService.MapHeight > 0
        ? _tileService.MapHeight - 1
        : GameConstants.DEFAULT_MAP_HEIGHT - 1;

    public void Tick(int tickCount)
    {
        _tickCount = tickCount;

        // Movement is now handled by GameWorldController.HandleFreeMovement()
        // (pixel-based, continuous). PlayerModule.Tick() no longer moves the player.
        // Old tick-based movement (HandleKeyboardMovement / HandleMouseMovement)
        // is disabled to prevent double-movement conflict.
        //
        // PlayerModule still handles: stat updates, Qi regen, buff ticks, etc.
        // (to be implemented in future modules)

        // PLR-E06: ResetFrameFlags() is NO LONGER called here.
        // It is now called from GameWorldController._PhysicsProcess AFTER
        // HandleStickyInput() and HandleMouseClick() have read the flags.
        // Previously, PlayerModule.Tick() ran inside GameBoot._PhysicsProcess
        // (via GameEntryPoint.Tick) which runs BEFORE the main scene's
        // _PhysicsProcess — causing flags to be cleared before Adapter reads them.
    }

    private void HandleKeyboardMovement()
    {
        var dir = _playerInputService.MoveDirection;
        if (dir == Position2D.Zero) return;

        // Keyboard input active — clear any mouse destination.
        _mouseDestination = null;

        int oldX = _playerService.Position.X;
        int oldY = _playerService.Position.Y;

        // Each axis independently: -1, 0, or +1.
        // Threshold 200 (was 350 — lowered for better diagonal sensitivity).
        int dx = 0, dy = 0;
        if (System.Math.Abs(dir.X) >= 200) dx = System.Math.Sign(dir.X);
        if (System.Math.Abs(dir.Y) >= 200) dy = System.Math.Sign(dir.Y);

        // Debug: log diagonal detection.
        if (dx != 0 && dy != 0)
        {
            Console.WriteLine($"[PlayerModule] Diagonal move: dx={dx} dy={dy} (dir={dir.X},{dir.Y})");
        }

        if (dx == 0 && dy == 0) return;

        // Movement: 2 tiles per tick normal, 3 with Run.
        // Diagonal correction: diagonal moves cover √2 ≈ 1.41× distance per step.
        // To equalize speed, reduce steps by ~30% when moving diagonally.
        int baseSteps = _playerInputService.RunHeld ? 3 : 2;
        bool isDiagonal = dx != 0 && dy != 0;
        // Diagonal: 2 steps → 1 step (floor), 3 steps → 2 steps.
        int steps = isDiagonal ? Math.Max(1, (int)(baseSteps / 1.41f)) : baseSteps;

        int maxX = MaxX;
        int maxY = MaxY;
        int newX = oldX;
        int newY = oldY;
        for (int i = 0; i < steps; i++)
        {
            newX = Math.Clamp(newX + dx, 0, maxX);
            newY = Math.Clamp(newY + dy, 0, maxY);
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

    /// <summary>
    /// Mouse-click movement: move towards destination tile, 2 tiles per tick.
    /// Each tick, compute direction from current position to destination.
    /// Clears destination when reached.
    /// </summary>
    private void HandleMouseMovement()
    {
        var dest = _mouseDestination.Value;
        int oldX = _playerService.Position.X;
        int oldY = _playerService.Position.Y;

        int dx = dest.X - oldX;
        int dy = dest.Y - oldY;

        // Reached destination?
        if (dx == 0 && dy == 0)
        {
            _mouseDestination = null;
            return;
        }

        // Normalize to -1, 0, +1 per axis (Chebyshev distance = diagonal movement).
        int stepX = System.Math.Clamp(dx, -1, 1);
        int stepY = System.Math.Clamp(dy, -1, 1);

        // Movement speed: 2 tiles per tick (3 with Run).
        int steps = _playerInputService.RunHeld ? 3 : 2;
        int maxX = MaxX;
        int maxY = MaxY;
        int newX = oldX;
        int newY = oldY;
        for (int i = 0; i < steps; i++)
        {
            if (newX != dest.X) newX += stepX;
            if (newY != dest.Y) newY += stepY;
            newX = Math.Clamp(newX, 0, maxX);
            newY = Math.Clamp(newY, 0, maxY);
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

    /// <summary>Set destination for mouse-click movement. Called by Adapter.</summary>
    public void SetMouseDestination(int tileX, int tileY)
    {
        tileX = Math.Clamp(tileX, 0, MaxX);
        tileY = Math.Clamp(tileY, 0, MaxY);
        _mouseDestination = new Position2D(tileX, tileY);
        Console.WriteLine($"[PlayerModule] Mouse destination set: ({tileX}, {tileY})");
    }

    /// <summary>Clear mouse destination (e.g., when keyboard input starts).</summary>
    public void ClearMouseDestination()
    {
        _mouseDestination = null;
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
        // NPC_COMBAT_PREP Phase 6: player combat bridge (attack intent with
        // resolved NPC target). Ticked from the Adapter scene controller.
        builder.Register<PlayerCombatAdapter>(Lifetime.Singleton);
        builder.Register<IStatService, StatService>(Lifetime.Singleton);
        builder.Register<PlayerModule>(Lifetime.Singleton);
    }
}

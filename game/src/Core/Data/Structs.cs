#nullable enable
using System;
using System.Collections.Generic;

namespace CultivationGame.Core.Data;

// ── Position2D ──────────────────────────────────────────────────────────
/// <summary>
/// Integer 2D position in tile coordinates. Value semantics, zero GC.
/// </summary>
public readonly struct Position2D : IEquatable<Position2D>
{
    public readonly int X;
    public readonly int Y;

    public Position2D(int x, int y)
    {
        X = x;
        Y = y;
    }

    public static Position2D Zero => default;

    /// <summary>Chebyshev distance (tile-grid movement).</summary>
    public int DistanceTo(Position2D other)
    {
        int dx = X - other.X;
        int dy = Y - other.Y;
        return Math.Max(Math.Abs(dx), Math.Abs(dy));
    }

    public Position2D WithOffset(int dx, int dy) => new Position2D(X + dx, Y + dy);

    public bool Equals(Position2D other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is Position2D p && Equals(p);
    public override int GetHashCode() => unchecked(X * 397 ^ Y);
    public static bool operator ==(Position2D a, Position2D b) => a.Equals(b);
    public static bool operator !=(Position2D a, Position2D b) => !a.Equals(b);
    public override string ToString() => $"({X}, {Y})";
}

// ── Vector2f ────────────────────────────────────────────────────────────
/// <summary>
/// Float 2D vector. Used for normalized move direction, world-space
/// mouse position etc.
/// </summary>
public readonly struct Vector2f : IEquatable<Vector2f>
{
    public readonly float X;
    public readonly float Y;

    public Vector2f(float x, float y)
    {
        X = x;
        Y = y;
    }

    public static Vector2f Zero => default;

    public float Length => MathF.Sqrt(X * X + Y * Y);

    public Vector2f Normalized()
    {
        float len = Length;
        if (len < 1e-6f) return Zero;
        return new Vector2f(X / len, Y / len);
    }

    public bool Equals(Vector2f other) => X.Equals(other.X) && Y.Equals(other.Y);
    public override bool Equals(object? obj) => obj is Vector2f v && Equals(v);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public static bool operator ==(Vector2f a, Vector2f b) => a.Equals(b);
    public static bool operator !=(Vector2f a, Vector2f b) => !a.Equals(b);
    public override string ToString() => $"({X:F2}, {Y:F2})";
}

// ── WorldTime ───────────────────────────────────────────────────────────
/// <summary>
/// Game time stored as total minutes since the epoch (1 tick = 1 minute).
/// Value semantics, immutable. Date components derived via properties.
/// </summary>
public readonly struct WorldTime : IEquatable<WorldTime>
{
    public readonly int TotalMinutes;

    public WorldTime(int totalMinutes)
    {
        TotalMinutes = totalMinutes;
    }

    public WorldTime(int year, int month, int day, int hour, int minute)
    {
        int yearsFromStart = year - GameConstants.START_YEAR;
        int totalMinutes = yearsFromStart
            * GameConstants.MONTHS_PER_YEAR
            * GameConstants.DAYS_PER_MONTH
            * GameConstants.HOURS_PER_DAY
            * GameConstants.TICKS_PER_HOUR;
        totalMinutes += (month - 1) * GameConstants.DAYS_PER_MONTH * GameConstants.HOURS_PER_DAY * GameConstants.TICKS_PER_HOUR;
        totalMinutes += (day - 1) * GameConstants.HOURS_PER_DAY * GameConstants.TICKS_PER_HOUR;
        totalMinutes += hour * GameConstants.TICKS_PER_HOUR;
        totalMinutes += minute;
        TotalMinutes = totalMinutes;
    }

    public int Minute => TotalMinutes % 60;
    public int Hour => (TotalMinutes / 60) % 24;
    public int Day => ((TotalMinutes / 60 / 24) % GameConstants.DAYS_PER_MONTH) + 1;
    public int Month => ((TotalMinutes / 60 / 24 / GameConstants.DAYS_PER_MONTH) % GameConstants.MONTHS_PER_YEAR) + 1;
    public int Year => TotalMinutes / 60 / 24 / GameConstants.DAYS_PER_MONTH / GameConstants.MONTHS_PER_YEAR + GameConstants.START_YEAR;

    /// <summary>Warm = months 1..9, Cold = months 10..12.</summary>
    public Season Season => Month <= 9 ? Season.Warm : Season.Cold;

    public TimeOfDay TimeOfDay
    {
        get
        {
            return Hour switch
            {
                < 5 => TimeOfDay.Night,
                < 7 => TimeOfDay.Dawn,
                < 11 => TimeOfDay.Morning,
                < 17 => TimeOfDay.Day,
                < 19 => TimeOfDay.Evening,
                < 21 => TimeOfDay.Dusk,
                _ => TimeOfDay.Night,
            };
        }
    }

    public WorldTime AddMinutes(int minutes) => new WorldTime(TotalMinutes + minutes);

    public bool Equals(WorldTime other) => TotalMinutes == other.TotalMinutes;
    public override bool Equals(object? obj) => obj is WorldTime w && Equals(w);
    public override int GetHashCode() => TotalMinutes;
    public static bool operator ==(WorldTime a, WorldTime b) => a.Equals(b);
    public static bool operator !=(WorldTime a, WorldTime b) => !a.Equals(b);
    public override string ToString() => $"{Year}-{Month:D2}-{Day:D2} {Hour:D2}:{Minute:D2}";
}

// ── StatBonus ───────────────────────────────────────────────────────────
public readonly struct StatBonus : IEquatable<StatBonus>
{
    public readonly StatType Stat;
    public readonly float Value;

    public StatBonus(StatType stat, float value)
    {
        Stat = stat;
        Value = value;
    }

    public bool Equals(StatBonus other) => Stat == other.Stat && Value.Equals(other.Value);
    public override bool Equals(object? obj) => obj is StatBonus s && Equals(s);
    public override int GetHashCode() => HashCode.Combine((int)Stat, Value);
    public static bool operator ==(StatBonus a, StatBonus b) => a.Equals(b);
    public static bool operator !=(StatBonus a, StatBonus b) => !a.Equals(b);
}

// ── InventorySlot ───────────────────────────────────────────────────────
public readonly struct InventorySlot : IEquatable<InventorySlot>
{
    public readonly string ItemId;
    public readonly int Count;
    public readonly float Weight;
    public readonly float Volume;

    public InventorySlot(string itemId, int count, float weight, float volume)
    {
        ItemId = itemId;
        Count = count;
        Weight = weight;
        Volume = volume;
    }

    public bool IsEmpty => Count <= 0 || string.IsNullOrEmpty(ItemId);

    public bool Equals(InventorySlot other)
        => Count == other.Count
            && Weight.Equals(other.Weight)
            && Volume.Equals(other.Volume)
            && ItemId == other.ItemId;
    public override bool Equals(object? obj) => obj is InventorySlot s && Equals(s);
    public override int GetHashCode() => HashCode.Combine(ItemId, Count, Weight, Volume);
    public static bool operator ==(InventorySlot a, InventorySlot b) => a.Equals(b);
    public static bool operator !=(InventorySlot a, InventorySlot b) => !a.Equals(b);
}

// ── LootEntry ───────────────────────────────────────────────────────────
public readonly struct LootEntry : IEquatable<LootEntry>
{
    public readonly string ItemId;
    public readonly float Chance;
    public readonly int MinCount;
    public readonly int MaxCount;

    public LootEntry(string itemId, float chance, int minCount, int maxCount)
    {
        ItemId = itemId;
        Chance = chance;
        MinCount = minCount;
        MaxCount = maxCount;
    }

    public bool Equals(LootEntry other)
        => ItemId == other.ItemId
            && Chance.Equals(other.Chance)
            && MinCount == other.MinCount
            && MaxCount == other.MaxCount;
    public override bool Equals(object? obj) => obj is LootEntry e && Equals(e);
    public override int GetHashCode() => HashCode.Combine(ItemId, Chance, MinCount, MaxCount);
    public static bool operator ==(LootEntry a, LootEntry b) => a.Equals(b);
    public static bool operator !=(LootEntry a, LootEntry b) => !a.Equals(b);
}

// ── TileCoord ───────────────────────────────────────────────────────────
public readonly struct TileCoord : IEquatable<TileCoord>
{
    public readonly int X;
    public readonly int Y;
    public readonly int Z;

    public TileCoord(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public bool Equals(TileCoord other) => X == other.X && Y == other.Y && Z == other.Z;
    public override bool Equals(object? obj) => obj is TileCoord c && Equals(c);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    public static bool operator ==(TileCoord a, TileCoord b) => a.Equals(b);
    public static bool operator !=(TileCoord a, TileCoord b) => !a.Equals(b);
    public override string ToString() => $"({X}, {Y}, {Z})";
}

// ── Rect2i ──────────────────────────────────────────────────────────────
public readonly struct Rect2i : IEquatable<Rect2i>
{
    public readonly int X;
    public readonly int Y;
    public readonly int Width;
    public readonly int Height;

    public Rect2i(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public int Left => X;
    public int Right => X + Width;
    public int Top => Y;
    public int Bottom => Y + Height;

    public bool Contains(int x, int y) => x >= X && x < X + Width && y >= Y && y < Y + Height;
    public bool Contains(Position2D p) => Contains(p.X, p.Y);

    public bool Equals(Rect2i other)
        => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
    public override bool Equals(object? obj) => obj is Rect2i r && Equals(r);
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
    public static bool operator ==(Rect2i a, Rect2i b) => a.Equals(b);
    public static bool operator !=(Rect2i a, Rect2i b) => !a.Equals(b);
    public override string ToString() => $"[{X},{Y} {Width}x{Height}]";
}

// ── InputFrameData ──────────────────────────────────────────────────────
/// <summary>
/// Canonical input frame data passed from the engine Adapter to the
/// Application layer each tick. Zero-allocation: the only reference type
/// is <see cref="StickyKeys"/> (string set) — see HOTKEYS §5.1.
/// </summary>
public readonly struct InputFrameData : IEquatable<InputFrameData>
{
    /// <summary>Normalized WASD/arrows direction (zero if no input).</summary>
    public readonly Vector2f MoveDirection;
    public readonly bool IsRun;
    public readonly bool IsLmbPressed;
    public readonly bool IsRmbPressed;
    /// <summary>Seconds RMB has been held (for context-menu trigger ≥0.3s).</summary>
    public readonly float RmbHoldDuration;
    /// <summary>Mouse position in world coordinates (per-mille space).</summary>
    public readonly Vector2f MouseWorldPos;
    public readonly bool IsOverUI;
    /// <summary>Selected hotbar slot 1..9 or null.</summary>
    public readonly int? HotbarSlot;
    /// <summary>One-shot pressed keys for this frame (e.g. "E", "F5").</summary>
    public readonly IReadOnlySet<string> StickyKeys;
    /// <summary>Frame counter for sticky-flag validation.</summary>
    public readonly long Frame;

    public InputFrameData(
        Vector2f moveDirection,
        bool isRun,
        bool isLmbPressed,
        bool isRmbPressed,
        float rmbHoldDuration,
        Vector2f mouseWorldPos,
        bool isOverUI,
        int? hotbarSlot,
        IReadOnlySet<string> stickyKeys,
        long frame)
    {
        MoveDirection = moveDirection;
        IsRun = isRun;
        IsLmbPressed = isLmbPressed;
        IsRmbPressed = isRmbPressed;
        RmbHoldDuration = rmbHoldDuration;
        MouseWorldPos = mouseWorldPos;
        IsOverUI = isOverUI;
        HotbarSlot = hotbarSlot;
        StickyKeys = stickyKeys;
        Frame = frame;
    }

    /// <summary>Convenience: was a sticky key pressed this frame?</summary>
    public bool IsSticky(string key) => StickyKeys.Contains(key);

    public bool Equals(InputFrameData other)
        => Frame == other.Frame
            && IsRun == other.IsRun
            && IsLmbPressed == other.IsLmbPressed
            && IsRmbPressed == other.IsRmbPressed
            && RmbHoldDuration.Equals(other.RmbHoldDuration)
            && IsOverUI == other.IsOverUI
            && HotbarSlot == other.HotbarSlot
            && MoveDirection == other.MoveDirection
            && MouseWorldPos == other.MouseWorldPos;
    public override bool Equals(object? obj) => obj is InputFrameData d && Equals(d);
    public override int GetHashCode() => HashCode.Combine(Frame, MoveDirection, IsRun, HotbarSlot);
    public static bool operator ==(InputFrameData a, InputFrameData b) => a.Equals(b);
    public static bool operator !=(InputFrameData a, InputFrameData b) => !a.Equals(b);
}

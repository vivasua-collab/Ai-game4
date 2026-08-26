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
    /// <summary>Ai-game3 legacy alias for <see cref="Zero"/>.</summary>
    public static Position2D zero => default;

    /// <summary>Chebyshev distance (tile-grid movement).</summary>
    public int DistanceTo(Position2D other)
    {
        int dx = X - other.X;
        int dy = Y - other.Y;
        return Math.Max(Math.Abs(dx), Math.Abs(dy));
    }

    /// <summary>Ai-game3 legacy: Euclidean distance as float.</summary>
    public float Distance(Position2D other)
    {
        int dx = X - other.X;
        int dy = Y - other.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Squared Euclidean distance (zero-alloc).</summary>
    public int SqrMagnitude
    {
        get { return X * X + Y * Y; }
    }

    /// <summary>Euclidean magnitude as float.</summary>
    public float Magnitude => MathF.Sqrt(SqrMagnitude);

    public Position2D WithOffset(int dx, int dy) => new Position2D(X + dx, Y + dy);

    // === Ai-game3 compatibility: float-based helpers ===
    // NPCMovementService and other migrated code expects Position2D to behave
    // like a float vector (Unity Vector2). These members bridge the gap by
    // delegating to Vector2f for float math.

    /// <summary>Normalized direction as float vector (Ai-game3 lowercase alias).</summary>
    public Vector2f normalized
    {
        get
        {
            int sq = SqrMagnitude;
            if (sq == 0) return Vector2f.Zero;
            float inv = 1f / MathF.Sqrt(sq);
            return new Vector2f(X * inv, Y * inv);
        }
    }

    /// <summary>Static distance helper (Ai-game3 compatibility: Vector2.Distance(a, b)).</summary>
    public static float Distance(Position2D a, Position2D b) => a.Distance(b);

    /// <summary>Scale by float — returns Vector2f (Ai-game3 compatibility).</summary>
    public static Vector2f operator *(Position2D a, float s) => new Vector2f(a.X * s, a.Y * s);

    /// <summary>Add a float vector — returns Position2D (rounded to int tile coords).</summary>
    public static Position2D operator +(Position2D a, Vector2f b)
        => new Position2D((int)(a.X + b.X), (int)(a.Y + b.Y));

    /// <summary>Subtract a float vector — returns Position2D.</summary>
    public static Position2D operator -(Position2D a, Vector2f b)
        => new Position2D((int)(a.X - b.X), (int)(a.Y - b.Y));

    /// <summary>
    /// Implicit conversion from Vector2f to Position2D (rounds to nearest int).
    /// Ai-game3 compatibility — migrated code mixes Position2D and Vector2f freely.
    /// </summary>
    public static implicit operator Position2D(Vector2f v)
        => new Position2D((int)v.X, (int)v.Y);

    public bool Equals(Position2D other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is Position2D p && Equals(p);
    public override int GetHashCode() => unchecked(X * 397 ^ Y);
    public static bool operator ==(Position2D a, Position2D b) => a.Equals(b);
    public static bool operator !=(Position2D a, Position2D b) => !a.Equals(b);
    public static Position2D operator +(Position2D a, Position2D b) => new Position2D(a.X + b.X, a.Y + b.Y);
    public static Position2D operator -(Position2D a, Position2D b) => new Position2D(a.X - b.X, a.Y - b.Y);
    public static Position2D operator *(Position2D a, int s) => new Position2D(a.X * s, a.Y * s);
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
                < 21 => TimeOfDay.Twilight,
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
// NOTE: StatBonus moved to its own file (StatBonus.cs) — migrated from Ai-game3
// as a [Serializable] class with { string StatName; float Value; bool IsPercentage; }.

// ── InventorySlot ───────────────────────────────────────────────────────
public readonly struct InventorySlot : IEquatable<InventorySlot>
{
    public readonly string ItemId;
    public readonly int Count;
    public readonly float Weight;
    public readonly float Volume;
    public readonly ItemCategory Category;
    public readonly ItemRarity Rarity;

    public InventorySlot(string itemId, int count, float weight, float volume)
    {
        ItemId = itemId;
        Count = count;
        Weight = weight;
        Volume = volume;
        Category = ItemCategory.Misc;
        Rarity = ItemRarity.Common;
    }

    public InventorySlot(string itemId, int count, ItemCategory category, ItemRarity rarity)
    {
        ItemId = itemId;
        Count = count;
        Weight = 0f;
        Volume = 0f;
        Category = category;
        Rarity = rarity;
    }

    public bool IsEmpty => Count <= 0 || string.IsNullOrEmpty(ItemId);

    public bool Equals(InventorySlot other)
        => Count == other.Count
            && Weight.Equals(other.Weight)
            && Volume.Equals(other.Volume)
            && Category == other.Category
            && Rarity == other.Rarity
            && ItemId == other.ItemId;
    public override bool Equals(object? obj) => obj is InventorySlot s && Equals(s);
    public override int GetHashCode() => HashCode.Combine(ItemId, Count, Weight, Volume);
    public static bool operator ==(InventorySlot a, InventorySlot b) => a.Equals(b);
    public static bool operator !=(InventorySlot a, InventorySlot b) => !a.Equals(b);
}

// ── LootEntry ───────────────────────────────────────────────────────────
/// <summary>
/// Запись лута: предмет + количество + редкость + источник.
/// Унифицированная замена legacy Combat.LootEntry и CombatLootService.LootEntry.
/// Ai-game3 compatibility signature.
/// </summary>
public readonly struct LootEntry : IEquatable<LootEntry>
{
    public readonly string ItemId;
    public readonly int Count;
    public readonly ItemRarity Rarity;
    public readonly string Source;

    public LootEntry(string itemId, int count, ItemRarity rarity, string source = "")
    {
        ItemId = itemId;
        Count = count;
        Rarity = rarity;
        Source = source;
    }

    public bool Equals(LootEntry other)
        => ItemId == other.ItemId
            && Count == other.Count
            && Rarity == other.Rarity
            && Source == other.Source;
    public override bool Equals(object? obj) => obj is LootEntry e && Equals(e);
    public override int GetHashCode() => HashCode.Combine(ItemId, Count, Rarity, Source);
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

// ── SaveSlot ───────────────────────────────────────────────────────────
/// <summary>
/// Identifies a save slot. Combines a slot name (player-chosen or auto)
/// with the slot type so the SaveService can route to the correct file.
/// </summary>
public readonly struct SaveSlot : IEquatable<SaveSlot>
{
    public readonly string Name;
    public readonly SaveSlotType Type;

    public SaveSlot(string name, SaveSlotType type = SaveSlotType.Manual)
    {
        Name = name ?? string.Empty;
        Type = type;
    }

    public string FileName => Type switch
    {
        SaveSlotType.AutoSave => "autosave.sav",
        SaveSlotType.QuickSave => "quicksave.sav",
        _ => string.IsNullOrEmpty(Name) ? "default.sav" : $"{Name}.sav",
    };

    public bool Equals(SaveSlot other) => Name == other.Name && Type == other.Type;
    public override bool Equals(object? obj) => obj is SaveSlot s && Equals(s);
    public override int GetHashCode() => HashCode.Combine(Name, Type);
    public static bool operator ==(SaveSlot a, SaveSlot b) => a.Equals(b);
    public static bool operator !=(SaveSlot a, SaveSlot b) => !a.Equals(b);
    public override string ToString() => $"{Type}:{Name}";
}

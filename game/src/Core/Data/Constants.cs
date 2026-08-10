#nullable enable
namespace CultivationGame.Core.Data;

/// <summary>
/// Game-wide constants. Single source of truth for tuning values that
/// never change at runtime (engine-agnostic).
/// </summary>
public static class GameConstants
{
    // ── Time / tick ──────────────────────────────────────────────────────
    /// <summary>1 tick = 1 minute of game time.</summary>
    public const int TICKS_PER_MINUTE = 1;
    public const int TICKS_PER_HOUR = 60;
    public const int TICKS_PER_DAY = 1440;
    public const int TICKS_PER_MONTH = 43200;
    public const int TICKS_PER_YEAR = 518400;

    public const int START_YEAR = 1864;
    public const int DAYS_PER_MONTH = 30;
    public const int MONTHS_PER_YEAR = 12;
    public const int HOURS_PER_DAY = 24;

    /// <summary>Normal speed: 1 tick per real second.</summary>
    public const int SPEED_NORMAL = 1;
    /// <summary>Fast speed: 5 ticks per real second.</summary>
    public const int SPEED_FAST = 5;
    /// <summary>Quick speed: 15 ticks per real second.</summary>
    public const int SPEED_QUICK = 15;

    /// <summary>Autosave trigger interval (in ticks).</summary>
    public const int AUTOSAVE_INTERVAL_TICKS = 60;
    /// <summary>Qi regeneration batch interval (in ticks).</summary>
    public const int QI_REGEN_BATCH_TICKS = 10;

    // ── Tile / rendering ────────────────────────────────────────────────
    /// <summary>Tile edge length in meters (tile is 2×2 m).</summary>
    public const int TILE_SIZE_M = 2;
    /// <summary>Pixels per meter (rendering scaling factor).</summary>
    public const int METERS_TO_PIXELS = 32;
    /// <summary>Tile size in pixels (TILE_SIZE_M * METERS_TO_PIXELS).</summary>
    public const int TILE_PIXELS = 64;
    /// <summary>Pixels-per-unit used by the renderer for tile sprites.</summary>
    public const int TILE_PPU = 32;

    // ── NPC / combat ────────────────────────────────────────────────────
    public const int MAX_ACTIVE_NPCS = 100;
    public const float AGGRO_RADIUS = 5f;
    public const float ATTACK_RADIUS = 1.5f;
    public const float PATROL_RADIUS = 10f;

    public const float DEFAULT_MOVE_SPEED = 2.0f;
    public const float FLEE_SPEED_MULT = 1.5f;

    // ── Save files ──────────────────────────────────────────────────────
    public const string SAVE_MAIN_FILE = "main.sav";
    public const string SAVE_CHUNKS_DIR = "chunks";
    public const string SAVE_LOCATIONS_DIR = "locations";
    public const string SAVE_METADATA = "metadata.sav";
}

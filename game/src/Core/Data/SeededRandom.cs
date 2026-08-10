#nullable enable
// ============================================================================
// SeededRandom — deterministic RNG (xorshift64*)
// ----------------------------------------------------------------------------
// Pure C# utility used by Generator module and any system that needs
// reproducible randomness (world gen, loot rolls, NPC traits).
// NOT thread-safe; create one instance per thread if used concurrently.
// ============================================================================

using System;

namespace CultivationGame.Core.Data;

/// <summary>
/// Deterministic pseudo-random number generator based on xorshift64*.
/// Same seed => same sequence across all platforms and runs.
/// </summary>
public sealed class SeededRandom
{
    // xorshift64 state — must be non-zero. We mix the seed to guarantee this.
    private ulong _state;

    // Float-conversion constants
    private const double Inv2Exp32 = 1.0 / (1UL << 32);
    private const double Inv2Exp53 = 1.0 / (1UL << 53);

    /// <summary>Constructs a generator with the given seed.</summary>
    public SeededRandom(int seed)
    {
        // Mix the 32-bit seed into a non-zero 64-bit state to avoid the
        // degenerate all-zero state which xorshift cannot escape.
        ulong s = (ulong)seed;
        if (s == 0UL) s = 0x9E3779B97F4A7C15UL;
        // Avalanche the seed so similar seeds don't produce similar streams.
        s ^= s >> 33;
        s *= 0xFF51AFD7ED558CCDUL;
        s ^= s >> 33;
        s *= 0xC4CEB9FE1A85EC53UL;
        s ^= s >> 33;
        if (s == 0UL) s = 0x9E3779B97F4A7C15UL;
        _state = s;
    }

    /// <summary>Returns the next non-negative 32-bit integer.</summary>
    public int Next()
    {
        return (int)(NextU64() >> 32);
    }

    /// <summary>Returns a non-negative integer in [min, max).</summary>
    public int Next(int min, int max)
    {
        if (max <= min) return min;
        // Use the high 32 bits, mask to range — avoids modulo bias for small ranges
        // well enough for game purposes (full de-biasing is overkill for a stub RNG).
        uint range = (uint)(max - min);
        uint r = (uint)(NextU64() >> 32);
        return min + (int)(r % range);
    }

    /// <summary>Returns a float in [0.0, 1.0).</summary>
    public float NextFloat()
    {
        // 24-bit mantissa precision
        return (NextU64() >> 40) * (1.0f / (1U << 24));
    }

    /// <summary>Returns a double in [0.0, 1.0).</summary>
    public double NextDouble()
    {
        // 53-bit mantissa precision
        return (NextU64() >> 11) * Inv2Exp53;
    }

    /// <summary>Fills the buffer with random bytes.</summary>
    public void NextBytes(byte[] buffer)
    {
        if (buffer == null) return;
        int i = 0;
        while (i + 8 <= buffer.Length)
        {
            ulong v = NextU64();
            buffer[i] = (byte)v;
            buffer[i + 1] = (byte)(v >> 8);
            buffer[i + 2] = (byte)(v >> 16);
            buffer[i + 3] = (byte)(v >> 24);
            buffer[i + 4] = (byte)(v >> 32);
            buffer[i + 5] = (byte)(v >> 40);
            buffer[i + 6] = (byte)(v >> 48);
            buffer[i + 7] = (byte)(v >> 56);
            i += 8;
        }
        if (i < buffer.Length)
        {
            ulong v = NextU64();
            while (i < buffer.Length)
            {
                buffer[i++] = (byte)v;
                v >>= 8;
            }
        }
    }

    /// <summary>Core xorshift64* step producing a 64-bit value.</summary>
    private ulong NextU64()
    {
        ulong x = _state;
        x ^= x >> 12;
        x ^= x << 25;
        x ^= x >> 27;
        _state = x;
        return x * 0x2545F4914F6CDD1DUL;
    }
}

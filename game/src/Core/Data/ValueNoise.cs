#nullable enable
using System;

namespace CultivationGame.Core.Data;

/// <summary>
/// Simple 2D value noise implementation — engine-agnostic (pure C#).
/// Inspired by Godot's FastNoiseLite (Simplex Smooth) but without Godot dependency.
///
/// Features:
///   - Deterministic (seeded)
///   - Fractal Brownian Motion (fBm) with configurable octaves
///   - Domain warping for organic coastlines
///   - Zero allocations on hot path
///
/// Based on: https://docs.godotengine.org/en/stable/classes/class_fastnoiselite.html
/// Technique: value noise with smoothstep interpolation.
/// </summary>
public sealed class ValueNoise
{
    private readonly int _seed;
    private readonly int _octaves;
    private readonly float _frequency;
    private readonly float _lacunarity;   // frequency multiplier per octave
    private readonly float _persistence;  // amplitude multiplier per octave

    /// <param name="seed">Deterministic seed.</param>
    /// <param name="octaves">Number of fBm octaves (4 = good detail).</param>
    /// <param name="frequency">Base frequency (0.01-0.05 typical for terrain).</param>
    /// <param name="lacunarity">Frequency multiplier per octave (2.0 standard).</param>
    /// <param name="persistence">Amplitude multiplier per octave (0.5 standard).</param>
    public ValueNoise(int seed, int octaves = 4, float frequency = 0.015f,
                      float lacunarity = 2.0f, float persistence = 0.5f)
    {
        _seed = seed;
        _octaves = octaves;
        _frequency = frequency;
        _lacunarity = lacunarity;
        _persistence = persistence;
    }

    /// <summary>
    /// Sample 2D noise at (x, y). Returns value in range [0, 1].
    /// Uses fBm (fractal Brownian motion) — sum of octaves.
    /// </summary>
    public float Sample(float x, float y)
    {
        float sum = 0f;
        float amplitude = 1f;
        float frequency = _frequency;
        float maxAmplitude = 0f;

        for (int i = 0; i < _octaves; i++)
        {
            sum += SingleNoise(x * frequency, y * frequency, _seed + i * 1000) * amplitude;
            maxAmplitude += amplitude;
            amplitude *= _persistence;
            frequency *= _lacunarity;
        }

        return sum / maxAmplitude;  // normalize to [0, 1]
    }

    /// <summary>
    /// Domain-warped sample — adds organic distortion to coastlines.
    /// Uses two noise samples to offset the input coordinates.
    /// </summary>
    public float SampleWarped(float x, float y, float warpStrength = 0.5f)
    {
        float wx = Sample(x + 0.1f, y + 0.1f) * warpStrength;
        float wy = Sample(x + 0.7f, y + 0.3f) * warpStrength;
        return Sample(x + wx, y + wy);
    }

    /// <summary>
    /// Single-octave value noise with smoothstep interpolation.
    /// Returns value in [0, 1].
    /// </summary>
    private static float SingleNoise(float x, float y, int seed)
    {
        // Integer grid coordinates.
        int x0 = (int)Math.Floor(x);
        int y0 = (int)Math.Floor(y);
        int x1 = x0 + 1;
        int y1 = y0 + 1;

        // Fractional part.
        float sx = x - x0;
        float sy = y - y0;

        // Smoothstep (Ken Perlin's improved fade).
        float u = Fade(sx);
        float v = Fade(sy);

        // Four corners of the grid cell.
        float n00 = Hash(x0, y0, seed);
        float n10 = Hash(x1, y0, seed);
        float n01 = Hash(x0, y1, seed);
        float n11 = Hash(x1, y1, seed);

        // Bilinear interpolation with smoothstep.
        float nx0 = Lerp(n00, n10, u);
        float nx1 = Lerp(n01, n11, u);
        return Lerp(nx0, nx1, v);
    }

    /// <summary>Ken Perlin's fade function: 6t^5 - 15t^4 + 10t^3.</summary>
    private static float Fade(float t)
    {
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    /// <summary>
    /// Hash function — returns pseudo-random value in [0, 1] for integer coords.
    /// Uses a simple but well-distributed hash.
    /// </summary>
    private static float Hash(int x, int y, int seed)
    {
        // Combine coordinates + seed into a single hash.
        int h = seed;
        h = (h * 374761393) ^ x;
        h = (h * 668265263) ^ y;
        h = (h ^ (h >> 13)) * 1274126177;
        h = h ^ (h >> 16);

        // Map to [0, 1] — use unsigned right shift semantics.
        return (h & 0x00FFFFFF) / (float)0x01000000;
    }
}

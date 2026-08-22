#nullable enable
// Создано: 2026-08-22 UTC — IMPL-6 (Q5): default ICombatRng implementation.
// ============================================================================
// CombatRng — deterministic RNG for combat (wraps Core.Data.SeededRandom).
// ----------------------------------------------------------------------------
// AUDIT-2 M7 / Q5: replaces direct `Random.Shared` calls inside the Combat
// module. Constructed once with a fixed seed and registered as a singleton in
// <see cref="CombatModuleServices"/>; combat services receive it via
// constructor injection. Same seed + same call sequence ⇒ same value stream.
// ============================================================================

using System;
using CultivationGame.Core.Data;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Modules.Combat;

/// <summary>
/// Default <see cref="ICombatRng"/> implementation backed by
/// <see cref="SeededRandom"/> (xorshift64*). Deterministic across processes,
/// platforms, and runs given the same seed.
/// </summary>
public sealed class CombatRng : ICombatRng
{
    private readonly SeededRandom _rng;

    /// <summary>
    /// Construct a combat RNG with the given 64-bit seed.
    /// </summary>
    /// <param name="seed">Deterministic seed. Same seed ⇒ same combat stream.</param>
    public CombatRng(long seed)
    {
        _rng = new SeededRandom(seed);
    }

    /// <inheritdoc/>
    public int Next(int min, int max) => _rng.Next(min, max);

    /// <inheritdoc/>
    public float NextFloat() => _rng.NextFloat();

    /// <inheritdoc/>
    public bool NextBool(float probability) => _rng.NextBool(probability);
}

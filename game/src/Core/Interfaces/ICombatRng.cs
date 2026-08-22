#nullable enable
// Создано: 2026-08-22 UTC — IMPL-6 (Q5): injectable deterministic RNG for combat.
// ============================================================================
// ICombatRng — deterministic RNG contract for the Combat module.
// ----------------------------------------------------------------------------
// AUDIT-2 M7 / Q5: combat previously called `Random.Shared` directly, which is
// process-wide non-deterministic — the same playthrough produced different
// results across runs even with identical input. For save/load replay and
// deterministic testing, all combat randomness now flows through this
// interface. The default implementation (<c>CombatRng</c>) wraps
// <c>Core.Data.SeededRandom</c> so a fixed seed yields a reproducible combat
// stream. Tests / save-replay can substitute their own implementation.
// ============================================================================

namespace CultivationGame.Core.Interfaces;

/// <summary>
/// Deterministic RNG for the Combat module (Q5: injectable SeededRandom).
/// All combat randomness — damage rolls, dodge/block/parry checks, loot counts,
/// AI action selection, elemental procs — goes through this interface so the
/// outcome of a fight is reproducible from a seed.
/// </summary>
/// <remarks>
/// Implementations MUST be deterministic: the same seed combined with the same
/// call sequence MUST produce the same value stream. <see cref="Core.Data.SeededRandom"/>
/// (xorshift64*) is the canonical implementation. Implementations are NOT
/// required to be thread-safe; combat runs on the main thread.
/// </remarks>
public interface ICombatRng
{
    /// <summary>
    /// Returns a non-negative integer in [<paramref name="min"/>, <paramref name="max"/>).
    /// Mirrors <c>SeededRandom.Next(int, int)</c> semantics.
    /// </summary>
    int Next(int min, int max);

    /// <summary>
    /// Returns a float in [0.0, 1.0).
    /// Mirrors <c>SeededRandom.NextFloat()</c> semantics.
    /// </summary>
    float NextFloat();

    /// <summary>
    /// Returns <c>true</c> with the given probability (0.0 - 1.0).
    /// <paramref name="probability"/> &lt;= 0 always returns <c>false</c>;
    /// <paramref name="probability"/> &gt;= 1 always returns <c>true</c>.
    /// </summary>
    bool NextBool(float probability);
}

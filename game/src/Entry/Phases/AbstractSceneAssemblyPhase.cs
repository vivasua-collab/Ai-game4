#nullable enable
using System.Threading;
using System.Threading.Tasks;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Entry.Phases;

/// <summary>
/// Base class for all scene-assembly phases. Provides shared DI access
/// to the resolver (for cross-cutting lookups) and enforces the
/// <see cref="ISceneAssemblyPhase"/> contract.
/// </summary>
/// <remarks>
/// Concrete phases derive from this class, override <see cref="PhaseName"/>,
/// <see cref="PhaseOrder"/> and <see cref="ExecuteAsync"/>, and declare
/// their own <c>[Inject]</c> service dependencies.
/// </remarks>
public abstract class AbstractSceneAssemblyPhase : ISceneAssemblyPhase
{
    /// <inheritdoc />
    public abstract string PhaseName { get; }

    /// <inheritdoc />
    public abstract int PhaseOrder { get; }

    /// <summary>
    /// Container resolver, available for phases that need to perform
    /// cross-cutting lookups (e.g. <see cref="CoreValidationPhase"/>).
    /// Populated by the DI container post-build.
    /// </summary>
    [Inject] protected readonly IResolver _resolver = null!;

    /// <inheritdoc />
    public abstract Task ExecuteAsync(CancellationToken ct = default);
}

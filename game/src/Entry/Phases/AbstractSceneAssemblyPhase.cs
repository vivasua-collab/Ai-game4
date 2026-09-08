#nullable enable
using System.Threading.Tasks;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Entry.Phases;

/// <summary>
/// Base class for all scene-assembly phases. Provides shared DI access
/// to the resolver (for cross-cutting lookups) and the common
/// <see cref="ISceneAssemblyPhase"/> plumbing (State, BlockReason,
/// MarkAsSkipped, Reset, SkipOnLoad, CanExecute).
/// </summary>
/// <remarks>
/// Concrete phases derive from this class, override <see cref="PhaseName"/>,
/// <see cref="PhaseOrder"/> and <see cref="ExecuteAsync"/>, and declare
/// their own <c>[Inject]</c> service dependencies. <see cref="Order"/> is
/// exposed via <see cref="PhaseOrder"/> so derived classes only need to
/// override one property.
/// </remarks>
public abstract class AbstractSceneAssemblyPhase : ISceneAssemblyPhase
{
    /// <inheritdoc />
    public abstract string PhaseName { get; }

    /// <summary>
    /// Phase execution order (0 = first). Derived classes override this;
    /// the interface-level <see cref="Order"/> property delegates to it.
    /// </summary>
    public abstract int PhaseOrder { get; }

    /// <inheritdoc />
    public int Order => PhaseOrder;

    /// <inheritdoc />
    public SceneAssemblyPhaseState State { get; protected set; } = SceneAssemblyPhaseState.Pending;

    /// <inheritdoc />
    public string BlockReason { get; protected set; } = string.Empty;

    /// <summary>
    /// Container resolver, available for phases that need to perform
    /// cross-cutting lookups (e.g. <see cref="CoreValidationPhase"/>).
    /// Populated by the DI container post-build.
    /// </summary>
    [Inject] protected readonly IResolver _resolver = null!;

    /// <summary>
    /// Whether this phase should be skipped when loading a save.
    /// Default <c>true</c> — most phases are generative and skipped on
    /// Load. Override to <c>false</c> for wiring-only phases.
    /// </summary>
    public virtual bool SkipOnLoad => true;

    /// <inheritdoc />
    public virtual bool CanExecute() => State == SceneAssemblyPhaseState.Pending;

    /// <inheritdoc />
    public void MarkAsSkipped(string reason)
    {
        State = SceneAssemblyPhaseState.Skipped;
        BlockReason = reason ?? "skipped";
    }

    /// <summary>
    /// 2026-09-08 (ревью-1): вызывается оркестратором перед ExecuteAsync.
    /// </summary>
    public void MarkAsRunning()
    {
        State = SceneAssemblyPhaseState.Running;
    }

    /// <summary>
    /// 2026-09-08 (ревью-1): вызывается оркестратором после успешного ExecuteAsync.
    /// </summary>
    public void MarkAsCompleted()
    {
        State = SceneAssemblyPhaseState.Completed;
        BlockReason = string.Empty;
    }

    /// <summary>
    /// 2026-09-08 (ревью-1): вызывается оркестратором при исключении в ExecuteAsync.
    /// </summary>
    public void MarkAsFailed(string error)
    {
        State = SceneAssemblyPhaseState.Failed;
        BlockReason = string.IsNullOrEmpty(error) ? "failed" : error;
    }

    /// <inheritdoc />
    public void Reset()
    {
        State = SceneAssemblyPhaseState.Pending;
        BlockReason = string.Empty;
    }

    /// <inheritdoc />
    public abstract Task ExecuteAsync();
}

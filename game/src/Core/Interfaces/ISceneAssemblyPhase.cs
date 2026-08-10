#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace CultivationGame.Core.Interfaces;

/// <summary>
/// One of the 10 sequential scene assembly phases. The orchestrator
/// awaits each phase; failures produce <c>SceneAssemblyFailedEvent</c>.
/// </summary>
public interface ISceneAssemblyPhase
{
    string PhaseName { get; }
    int PhaseOrder { get; }
    Task ExecuteAsync(CancellationToken ct = default);
}

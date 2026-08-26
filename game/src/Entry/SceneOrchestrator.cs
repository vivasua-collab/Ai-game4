#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CultivationGame.Core.DI;
using CultivationGame.Core.Events;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.Messaging.Contracts;

namespace CultivationGame.Entry;

/// <summary>
/// Orchestrates the 10-phase scene assembly pipeline.
/// Phases are discovered from the DI container (<see cref="IResolver"/>
/// resolves every <see cref="ISceneAssemblyPhase"/>) on first run, or
/// can be registered explicitly via <see cref="RegisterPhase"/> (useful
/// for tests).
/// </summary>
/// <remarks>
/// Lifecycle of a single assembly run:
/// <list type="number">
///   <item><description>Publish <see cref="SceneInitializingEvent"/>.</description></item>
///   <item><description>For each phase (sorted by <see cref="ISceneAssemblyPhase.PhaseOrder"/>):
///     publish <see cref="ScenePhaseStartedEvent"/>, await <c>ExecuteAsync</c>,
///     publish <see cref="ScenePhaseCompletedEvent"/>.</description></item>
///   <item><description>On exception: publish <see cref="SceneAssemblyFailedEvent"/>
///     and rethrow so the caller (<c>GameSession</c>) can transition state.</description></item>
///   <item><description>On success: publish <see cref="SceneReadyEvent"/>.</description></item>
/// </list>
/// </remarks>
public sealed class SceneOrchestrator
{
    [Inject] private readonly IResolver _resolver = null!;
    [Inject] private readonly IPublisher<SceneInitializingEvent> _initPub = null!;
    [Inject] private readonly IPublisher<ScenePhaseStartedEvent> _startedPub = null!;
    [Inject] private readonly IPublisher<ScenePhaseCompletedEvent> _completedPub = null!;
    [Inject] private readonly IPublisher<SceneAssemblyFailedEvent> _failedPub = null!;
    [Inject] private readonly IPublisher<SceneReadyEvent> _readyPub = null!;

    // 2026-08-26 (аудит-1 A-1): не readonly — переназначается при стабильной
    // пересортировке OrderBy при регистрации фаз.
    private List<ISceneAssemblyPhase> _phases = new();
    private bool _autoLoaded;

    /// <summary>
    /// Explicitly register a phase. Phases added this way supplement (and
    /// are merged with) any phases auto-discovered from the container.
    /// 2026-08-26 (аудит-1 A-1): сортировка OrderBy — СТАБИЛЬНАЯ (List.Sort
    /// нестабилен: при равных Order порядок фаз не определён). При равных
    /// Order сохраняется порядок регистрации.
    /// </summary>
    public void RegisterPhase(ISceneAssemblyPhase phase)
    {
        if (phase is null) throw new ArgumentNullException(nameof(phase));
        _phases.Add(phase);
        _phases = _phases.OrderBy(p => p.Order).ToList();
    }

    /// <summary>
    /// Run the full assembly pipeline. Idempotent w.r.t. phase discovery:
    /// the first call auto-loads phases from the container if none have
    /// been registered explicitly.
    /// </summary>
    public async Task RunAssembly(CancellationToken ct = default)
    {
        EnsurePhasesLoaded();

        _initPub.Publish(new SceneInitializingEvent(_phases.Count));

        var totalSw = Stopwatch.StartNew();
        foreach (var phase in _phases)
        {
            ct.ThrowIfCancellationRequested();

            _startedPub.Publish(new ScenePhaseStartedEvent(phase.PhaseName, phase.Order));
            var phaseSw = Stopwatch.StartNew();
            try
            {
                await phase.ExecuteAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _failedPub.Publish(new SceneAssemblyFailedEvent(phase.PhaseName, ex.ToString()));
                Console.WriteLine(
                    $"[SceneOrchestrator] Phase '{phase.PhaseName}' (#{phase.Order}) failed: {ex.GetType().Name}: {ex.Message}");
                throw;
            }
            phaseSw.Stop();

            _completedPub.Publish(new ScenePhaseCompletedEvent(phase.PhaseName, phase.Order, phaseSw.ElapsedMilliseconds));
        }
        totalSw.Stop();

        _readyPub.Publish(new SceneReadyEvent(_phases.Count, 0, totalSw.ElapsedMilliseconds));
        Console.WriteLine($"[SceneOrchestrator] Assembly complete — {_phases.Count} phases, {totalSw.ElapsedMilliseconds} ms");
    }

    private void EnsurePhasesLoaded()
    {
        if (_autoLoaded) return;
        _autoLoaded = true;

        // Auto-discover any phases registered in the container that haven't
        // been added explicitly via RegisterPhase.
        var discovered = _resolver.ResolveAll<ISceneAssemblyPhase>();
        foreach (var phase in discovered)
        {
            if (!_phases.Contains(phase))
            {
                _phases.Add(phase);
            }
        }
        // Стабильная сортировка (аудит-1 A-1): равные Order → порядок регистрации.
        _phases = _phases.OrderBy(p => p.Order).ToList();
    }

    private static int ComparePhaseOrder(ISceneAssemblyPhase a, ISceneAssemblyPhase b)
        => a.Order.CompareTo(b.Order);
}

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
/// Orchestrates the 15-phase scene assembly pipeline.
/// Phases are discovered from the DI container (<see cref="IResolver"/>
/// resolves every <see cref="ISceneAssemblyPhase"/>) on first run, or
/// can be registered explicitly via <see cref="RegisterPhase"/> (useful
/// for tests).
/// </summary>
/// <remarks>
/// Lifecycle of a single assembly run (2026-09-08, ревью-1 — контракт
/// ISceneAssemblyPhase теперь РАБОТАЕТ, lifecycle принадлежит оркестратору):
/// <list type="number">
///   <item><description>Publish <see cref="SceneInitializingEvent"/>.</description></item>
///   <item><description>Reset() всех фаз (фазы — DI-синглтоны на весь процесс:
///     повторная сборка в том же процессе должна начинаться с чистого
///     State=Pending, иначе CanExecute() отсечёт все фазы).</description></item>
///   <item><description>For each phase (stable sort by Order):
///     <list type="bullet">
///       <item>LoadGame-режим + SkipOnLoad → MarkAsSkipped (генеративные фазы
///         восстанавливаются из сейва);</item>
///       <item>CanExecute() == false → MarkAsSkipped(BlockReason);</item>
///       <item>иначе MarkAsRunning → publish <see cref="ScenePhaseStartedEvent"/> →
///         await ExecuteAsync → MarkAsCompleted + publish
///         <see cref="ScenePhaseCompletedEvent"/>, либо MarkAsFailed +
///         publish <see cref="SceneAssemblyFailedEvent"/> + rethrow.</item>
///     </list></description></item>
///   <item><description>On exception: publish <see cref="SceneAssemblyFailedEvent"/>
///     and rethrow so the caller (<c>GameSession</c>) can transition state.</description></item>
///   <item><description>On success: publish <see cref="SceneReadyEvent"/> с реальными
///     числами completed/skipped (раньше PhasesSkipped всегда был 0).</description></item>
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
    /// 2026-09-08 (ревью-1): равных Order быть НЕ ДОЛЖНО — см. фиксы
    /// уникальной нумерации в фазах (AnimalSpawn 5→6 и сдвиг последующих).
    /// </summary>
    public void RegisterPhase(ISceneAssemblyPhase phase)
    {
        if (phase is null) throw new ArgumentNullException(nameof(phase));
        _phases.Add(phase);
        _phases = _phases.OrderBy(p => p.Order).ToList();
    }

    /// <summary>
    /// Run the full assembly pipeline in the given mode. Idempotent w.r.t.
    /// phase discovery: the first call auto-loads phases from the container
    /// if none have been registered explicitly. Every call resets phase
    /// state first (phases are process-lifetime DI singletons).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="mode">NewGame — run everything; LoadGame — skip
    /// generative phases (<see cref="ISceneAssemblyPhase.SkipOnLoad"/>),
    /// их состояние восстанавливается из сейва вызывающей стороной.</param>
    public async Task RunAssembly(CancellationToken ct = default,
        SceneAssemblyMode mode = SceneAssemblyMode.NewGame)
    {
        EnsurePhasesLoaded();

        // 2026-09-08 (ревью-1): фазы — синглтоны DI. Без Reset повторная
        // сборка (возврат в меню → NewGame/LoadGame) наткнётся на
        // CanExecute()==false у всех фаз со State=Completed/Skipped.
        foreach (var phase in _phases)
            phase.Reset();

        _initPub.Publish(new SceneInitializingEvent(_phases.Count));

        int completed = 0;
        int skipped = 0;
        var totalSw = Stopwatch.StartNew();
        foreach (var phase in _phases)
        {
            ct.ThrowIfCancellationRequested();

            // 1) Load-режим: генеративные фазы пропускаются — их состояние
            //    восстанавливается из сейва (Phase 18C контракт SkipOnLoad).
            if (mode == SceneAssemblyMode.LoadGame && phase.SkipOnLoad)
            {
                phase.MarkAsSkipped("LoadGame: генеративная фаза (SkipOnLoad) — состояние из сейва");
                skipped++;
                Console.WriteLine(
                    $"[SceneOrchestrator] Phase '{phase.PhaseName}' (#{phase.Order}) skipped — load-mode (SkipOnLoad)");
                continue;
            }

            // 2) Гейт готовности фазы (контракт CanExecute).
            if (!phase.CanExecute())
            {
                phase.MarkAsSkipped(string.IsNullOrEmpty(phase.BlockReason)
                    ? "CanExecute() == false"
                    : phase.BlockReason);
                skipped++;
                Console.WriteLine(
                    $"[SceneOrchestrator] Phase '{phase.PhaseName}' (#{phase.Order}) skipped — {phase.BlockReason}");
                continue;
            }

            // 3) Исполнение с полным lifecycle: Running → Completed/Failed.
            phase.MarkAsRunning();
            _startedPub.Publish(new ScenePhaseStartedEvent(phase.PhaseName, phase.Order));
            var phaseSw = Stopwatch.StartNew();
            try
            {
                await phase.ExecuteAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                phase.MarkAsFailed($"{ex.GetType().Name}: {ex.Message}");
                _failedPub.Publish(new SceneAssemblyFailedEvent(phase.PhaseName, ex.ToString()));
                Console.WriteLine(
                    $"[SceneOrchestrator] Phase '{phase.PhaseName}' (#{phase.Order}) failed: {ex.GetType().Name}: {ex.Message}");
                throw;
            }
            phaseSw.Stop();
            phase.MarkAsCompleted();
            completed++;

            _completedPub.Publish(new ScenePhaseCompletedEvent(phase.PhaseName, phase.Order, phaseSw.ElapsedMilliseconds));
        }
        totalSw.Stop();

        // 2026-09-08 (ревью-1): PhasesSkipped — реальное число (раньше всегда 0).
        _readyPub.Publish(new SceneReadyEvent(completed, skipped, totalSw.ElapsedMilliseconds));
        Console.WriteLine(
            $"[SceneOrchestrator] Assembly complete — mode={mode}: {completed} completed, {skipped} skipped, {totalSw.ElapsedMilliseconds} ms");
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
}

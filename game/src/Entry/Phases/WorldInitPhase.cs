#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Entry.Phases;

/// <summary>
/// Phase 3 — activates the test-polygon location on the world service
/// and sets the time speed to Normal.
/// </summary>
public sealed class WorldInitPhase : AbstractSceneAssemblyPhase
{
    public override string PhaseName => "WorldInit";
    public override int PhaseOrder => 3;

    [Inject] private readonly IWorldService _world = null!;
    [Inject] private readonly ITimeService _time = null!;
    [Inject] private readonly IGameSession _session = null!;
    // 2026-09-08 (ревью-1 P1-1): реестр техник — world-scoped каталог. Фазы —
    // DI-синглтоны на весь процесс: при повторной сборке мира (возврат в меню →
    // NewGame) в реестре остались бы техники прошлого мира (stale NPC-id и
    // партии прошлой пред-генерации). Сброс ЗДЕСЬ (фаза 3) — до спавна NPC (7),
    // который регистрирует свои техники, и до PreGen (13).
    [Inject] private readonly CultivationGame.Modules.Generator.TechniqueRegistry _techniqueRegistry = null!;

    public override Task ExecuteAsync()
    {
        var locId = _session.Data?.WorldId ?? LocationCatalog.TestPolygon.Id;
        _world.SetActiveLocation(locId);
        _time.Speed = TimeSpeed.Normal;
        _techniqueRegistry.Clear();
        Console.WriteLine(
            $"[Phase {PhaseOrder}] {PhaseName} complete — location={locId}, speed=Normal, techniqueRegistry cleared (was world-scoped reset)");
        return Task.CompletedTask;
    }
}

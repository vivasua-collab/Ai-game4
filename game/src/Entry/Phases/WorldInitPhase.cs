#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using CultivationGame.Core.Data;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Entry.Phases;

/// <summary>
/// Phase 3 — activates the location from session data on the world service
/// and sets the time speed to the configured default.
/// ST17-005 (Этап-17): прежде фаза ХАРДКОДИЛА TimeSpeed.Normal и молча
/// затирала WorldConfig.DefaultSpeed, выставленный WorldModule.Start() —
/// конфиг «не влиял на поведение» (кандидат аудита «настройки,
/// перезаписываемые поздними фазами»). Дефолтные значения равны (Normal),
/// поведение по умолчанию НЕ изменено — но источник истины теперь конфиг.
/// </summary>
public sealed class WorldInitPhase : AbstractSceneAssemblyPhase
{
    public override string PhaseName => "WorldInit";
    public override int PhaseOrder => 3;

    [Inject] private readonly IWorldService _world = null!;
    [Inject] private readonly ITimeService _time = null!;
    [Inject] private readonly IGameSession _session = null!;
    // ST17-005: источник истины стартовой скорости — WorldConfig
    // (тот же конфиг, что читает WorldModule.Start).
    [Inject] private readonly Modules.World.WorldConfig _config = null!;
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
        // ST17-005: конфиг, а не хардкод — DefaultSpeed=Normal (дефолт),
        // поведение неизменно; изменение конфига теперь реально действует.
        _time.Speed = _config.DefaultSpeed; // ST17-005: конфиг — источник истины (Normal по умолчанию)
        _techniqueRegistry.Clear();
        Console.WriteLine(
            $"[Phase {PhaseOrder}] {PhaseName} complete — location={locId}, speed=Normal, techniqueRegistry cleared (was world-scoped reset)");
        return Task.CompletedTask;
    }
}

#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;

namespace CultivationGame.Entry.Phases;

/// <summary>
/// Phase 9 — UI initialisation. Asks the UI service to show the HUD view.
/// </summary>
public sealed class UIInitPhase : AbstractSceneAssemblyPhase
{
    public override string PhaseName => "UIInit";
    // 2026-08-26 (аудит-1 A-1): 9 → 11 — уникальные порядки после перенумерации.
    public override int PhaseOrder => 11;

    [Inject] private readonly IUIService _ui = null!;

    public override Task ExecuteAsync()
    {
        _ui.ShowView("HUD");
        Console.WriteLine($"[Phase {PhaseOrder}] {PhaseName} complete — HUD shown");
        return Task.CompletedTask;
    }
}

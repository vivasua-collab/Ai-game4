#nullable enable
// Создано: 2026-09-08 — ревью-1 P1-3: headless-верификация ПОВТОРНОЙ сборки
// сцены в одном процессе (GODOT_REASSEMBLY_DEBUG=1).
//
// Проверяет контракт lifecycle оркестратора, который раньше не работал:
//   1. RESET: фазы — DI-синглтоны; после 1-й сборки их State=Completed.
//      Без Reset() в RunAssembly CanExecute()==false у всех фаз → вторая
//      сборка «пропустила» бы 15/15 фаз. Ожидание: все 15 Completed.
//   2. WORLD-SCOPED СБРОС РЕЕСТРА (WorldInitPhase): без Clear() реестр
//      техник накапливал бы техники прошлого мира (~x2). Ожидание: размер
//      после 2-й сборки ≈ размеру 1-й (не удвоился).
//   3. Повторный прогон не бросает исключений (GameSession сам ловит и
//      откатывает state — здесь зовём оркестратор напрямую).
//
// Запуск: GODOT_NEWGAME=1 GODOT_REASSEMBLY_DEBUG=1 \
//   godot --headless --path . scenes/MainMenu.tscn
// Паттерн следует GODOT_KILLFEED_DEBUG (env-хук, DI-инъекция из GameBoot).
using Godot;
using System.Linq;
using CultivationGame.Core.Interfaces;
using CultivationGame.Core.DI;
using CultivationGame.Entry;
using CultivationGame.Adapter.Di;

namespace CultivationGame.Adapter.Scene;

/// <summary>
/// Headless-верификация повторной сборки мира (Reset фаз + сброс реестра).
/// Итог: [ReAssemblySim] VERDICT: PASS/FAIL.
/// </summary>
public partial class ReAssemblySimDebug : Node
{
    private SceneOrchestrator? _orchestrator;
    private Modules.Generator.TechniqueRegistry? _registry;

    public override async void _Ready()
    {
        var container = Scene.GameBoot.Container;
        if (container != null)
        {
            ContainerAdapter.InjectProperties(this, container);
            _orchestrator = container.Resolve<SceneOrchestrator>();
            _registry = container.Resolve<Modules.Generator.TechniqueRegistry>();
        }

        GD.Print($"[ReAssemblySim] diag: orchestrator={_orchestrator != null} registry={_registry != null}");
        if (_orchestrator == null || _registry == null)
        {
            GD.Print("[ReAssemblySim] VERDICT: FAIL — DI resolution failed");
            return;
        }

        // Даём первой сборке и хукам времени (TradeUX/Dialogue) утихнуть.
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);

        int registryBefore = _registry.Count;
        var resolver = container!.Resolve<Core.DI.IResolver>();
        var phases = resolver.ResolveAll<ISceneAssemblyPhase>().OrderBy(p => p.Order).ToList();
        int totalPhases = phases.Count;

        // === ВТОРАЯ сборка в том же процессе (как «меню → NewGame» повторно) ===
        try
        {
            _orchestrator.RunAssembly(System.Threading.CancellationToken.None,
                SceneAssemblyMode.NewGame).GetAwaiter().GetResult();
        }
        catch (System.Exception ex)
        {
            GD.Print($"[ReAssemblySim] VERDICT: FAIL — повторная сборка бросила {ex.GetType().Name}: {ex.Message}");
            return;
        }

        // Проверка 1: все фазы выполнились (Reset отработал, никто не Skipped).
        int completed = phases.Count(p => p.State == SceneAssemblyPhaseState.Completed);
        int skipped = phases.Count(p => p.State == SceneAssemblyPhaseState.Skipped);
        int failed = phases.Count(p => p.State == SceneAssemblyPhaseState.Failed);

        // Проверка 2: реестр НЕ удвоился (world-scoped Clear в WorldInitPhase).
        int registryAfter = _registry.Count;
        bool registrySane = registryAfter <= (registryBefore + registryBefore / 2 + 10);

        GD.Print($"[ReAssemblySim] phases: total={totalPhases} completed={completed} " +
                 $"skipped={skipped} failed={failed}; registry: before={registryBefore} after={registryAfter}");

        bool pass = completed == totalPhases && totalPhases > 0
                    && skipped == 0 && failed == 0 && registrySane;
        GD.Print(pass
            ? "[ReAssemblySim] VERDICT: PASS — Reset фаз работает (все фазы переисполнены), реестр world-scoped (не удвоился)"
            : "[ReAssemblySim] VERDICT: FAIL — см. значения выше (skipped>0 = Reset не отработал; registry x2 = Clear не отработал)");
    }
}

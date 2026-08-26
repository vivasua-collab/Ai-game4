#nullable enable
using System;
using System.Threading.Tasks;

namespace CultivationGame.Entry.Phases;

/// <summary>
/// Финальная фаза сборки сцены (порядок 14 — ПОСЛЕЛЕДНЯЯ).
/// Логирует завершение; авторитетный <c>SceneReadyEvent</c> публикует
/// <see cref="SceneOrchestrator"/> ПОСЛЕ возврата этой фазы (с реальным
/// числом фаз и таймингом).
/// 2026-08-26 (аудит-1 A-1): порядок 10→14, дубль-публикация удалена.
/// </summary>
public sealed class FinalizePhase : AbstractSceneAssemblyPhase
{
    public override string PhaseName => "Finalize";
    // 2026-08-26 (аудит-1 A-1): 10 → 14 — финализация теперь ПОСЛЕДНЯЯ фаза.
    // Раньше порядок 10 выполнялся до PreGenTechnique(44)/TechniqueGrant(45),
    // т.е. «Scene assembly complete» логировался до выдачи техник.
    public override int PhaseOrder => 14;

    public override Task ExecuteAsync()
    {
        // 2026-08-26 (аудит-1 A-1): дубль-публикация SceneReadyEvent(1,0,0)
        // УДАЛЕНА — авторитетный publishes делает SceneOrchestrator ПОСЛЕ всех
        // фаз (с реальным числом фаз). Подписчиков на ранний publish не было.
        Console.WriteLine($"[Phase {PhaseOrder}] {PhaseName} complete — Scene assembly complete");
        return Task.CompletedTask;
    }
}

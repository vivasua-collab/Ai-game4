#nullable enable
namespace CultivationGame.Core.Messaging.Contracts;

// Created: 2026-05-09 (Ai-game3) — migrated 2026-08-15.
// Scene assembly lifecycle events published by the orchestrator (SceneOrchestrator).

/// <summary>
/// Событие: начало сборки сцены.
/// Публикуется оркестратором перед запуском первой фазы.
/// </summary>
public readonly struct SceneInitializingEvent
{
    /// <summary>Общее количество фаз для выполнения</summary>
    public readonly int TotalPhases;

    public SceneInitializingEvent(int totalPhases)
    {
        TotalPhases = totalPhases;
    }
}

/// <summary>
/// Событие: сцена полностью собрана и готова к игре.
/// Публикуется оркестратором после успешного выполнения всех фаз.
/// </summary>
public readonly struct SceneReadyEvent
{
    /// <summary>Количество успешно выполненных фаз</summary>
    public readonly int PhasesCompleted;

    /// <summary>Количество пропущенных фаз</summary>
    public readonly int PhasesSkipped;

    /// <summary>Общее время сборки (мс)</summary>
    public readonly float TotalTimeMs;

    public SceneReadyEvent(int phasesCompleted, int phasesSkipped, float totalTimeMs)
    {
        PhasesCompleted = phasesCompleted;
        PhasesSkipped = phasesSkipped;
        TotalTimeMs = totalTimeMs;
    }
}

/// <summary>
/// Событие: ошибка при сборке сцены.
/// Публикуется оркестратором при падении любой фазы.
/// </summary>
public readonly struct SceneAssemblyFailedEvent
{
    /// <summary>Имя упавшей фазы</summary>
    public readonly string PhaseName;

    /// <summary>Описание ошибки</summary>
    public readonly string Error;

    public SceneAssemblyFailedEvent(string phaseName, string error)
    {
        PhaseName = phaseName;
        Error = error;
    }
}

/// <summary>
/// Событие: сборка сцены завершена с ошибками.
/// Публикуется оркестратором при ContinueOnError=true и наличии failed-фаз.
/// Q16-E01 FIX: позволяет подписчикам узнать о завершении даже при ошибках.
/// </summary>
public readonly struct SceneAssemblyCompletedWithErrorsEvent
{
    /// <summary>Количество успешно выполненных фаз</summary>
    public readonly int PhasesCompleted;

    /// <summary>Количество пропущенных фаз</summary>
    public readonly int PhasesSkipped;

    /// <summary>Количество ошибок</summary>
    public readonly int PhasesFailed;

    /// <summary>Общее время сборки (мс)</summary>
    public readonly float TotalTimeMs;

    public SceneAssemblyCompletedWithErrorsEvent(int phasesCompleted, int phasesSkipped, int phasesFailed, float totalTimeMs)
    {
        PhasesCompleted = phasesCompleted;
        PhasesSkipped = phasesSkipped;
        PhasesFailed = phasesFailed;
        TotalTimeMs = totalTimeMs;
    }
}

/// <summary>
/// Событие: фаза сборки начала выполнение.
/// Публикуется перед ExecuteAsync() каждой фазы.
/// </summary>
public readonly struct ScenePhaseStartedEvent
{
    /// <summary>Имя фазы</summary>
    public readonly string PhaseName;

    /// <summary>Порядковый номер фазы</summary>
    public readonly int Order;

    public ScenePhaseStartedEvent(string phaseName, int order)
    {
        PhaseName = phaseName;
        Order = order;
    }
}

/// <summary>
/// Событие: фаза сборки завершена.
/// Публикуется после успешного ExecuteAsync() фазы.
/// </summary>
public readonly struct ScenePhaseCompletedEvent
{
    /// <summary>Имя фазы</summary>
    public readonly string PhaseName;

    /// <summary>Порядковый номер фазы</summary>
    public readonly int Order;

    /// <summary>Время выполнения (мс)</summary>
    public readonly float ElapsedMs;

    public ScenePhaseCompletedEvent(string phaseName, int order, float elapsedMs)
    {
        PhaseName = phaseName;
        Order = order;
        ElapsedMs = elapsedMs;
    }
}

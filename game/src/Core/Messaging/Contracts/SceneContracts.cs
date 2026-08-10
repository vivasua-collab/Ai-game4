#nullable enable
namespace CultivationGame.Core.Messaging.Contracts;

public readonly struct SceneInitializingEvent
{
    public readonly int TotalPhases;
    public SceneInitializingEvent(int totalPhases) { TotalPhases = totalPhases; }
}

public readonly struct ScenePhaseStartedEvent
{
    public readonly string PhaseName;
    public readonly int Order;
    public ScenePhaseStartedEvent(string phaseName, int order) { PhaseName = phaseName; Order = order; }
}

public readonly struct ScenePhaseCompletedEvent
{
    public readonly string PhaseName;
    public readonly int Order;
    public readonly long DurationMs;
    public ScenePhaseCompletedEvent(string phaseName, int order, long durationMs)
    {
        PhaseName = phaseName;
        Order = order;
        DurationMs = durationMs;
    }
}

public readonly struct SceneReadyEvent
{
    public readonly long TotalDurationMs;
    public SceneReadyEvent(long totalDurationMs) { TotalDurationMs = totalDurationMs; }
}

public readonly struct SceneAssemblyFailedEvent
{
    public readonly string PhaseName;
    public readonly string Exception;
    public SceneAssemblyFailedEvent(string phaseName, string exception)
    {
        PhaseName = phaseName;
        Exception = exception;
    }
}

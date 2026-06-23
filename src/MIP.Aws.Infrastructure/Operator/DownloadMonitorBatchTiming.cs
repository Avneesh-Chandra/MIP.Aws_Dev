namespace MIP.Aws.Infrastructure.Operator;

/// <summary>Shared batch wait and email timing for daily and operator PDF download batches.</summary>
internal static class DownloadMonitorBatchTiming
{
    internal static readonly TimeSpan BatchWaitGracePeriod = TimeSpan.FromMinutes(50);
    private static readonly TimeSpan PerSourceExecutionBudget = TimeSpan.FromMinutes(40);
    internal static readonly TimeSpan DeferredEmailRetryInterval = TimeSpan.FromMinutes(15);
    internal static readonly TimeSpan MaxBatchLifecycle = TimeSpan.FromHours(4);
    internal static readonly int MaxDeferredEmailAttempts = 16;

    /// <summary>
    /// How long the batch orchestrator waits for all sources to reach a terminal state before deferring email.
    /// Covers stagger scheduling plus serialized Playwright/portal downloads.
    /// </summary>
    public static TimeSpan ResolveOrchestratorWaitTimeout(int sourceCount, int intervalMinutes)
    {
        var interval = Math.Clamp(intervalMinutes, 1, 60);
        var stagger = TimeSpan.FromMinutes(Math.Max(0, sourceCount - 1) * interval);
        var execution = PerSourceExecutionBudget * Math.Max(sourceCount, 1);
        return stagger + BatchWaitGracePeriod + execution;
    }

    public static int ResolveOrchestratorDisableConcurrentTimeoutSeconds(int sourceCount, int intervalMinutes) =>
        (int)Math.Ceiling(ResolveOrchestratorWaitTimeout(sourceCount, intervalMinutes).TotalSeconds)
        + (int)DeferredEmailRetryInterval.TotalSeconds;
}

using MIP.Aws.Application.Configuration;

namespace MIP.Aws.Infrastructure.Operator;

/// <summary>Shared batch wait and email timing for daily and operator PDF download batches.</summary>
internal static class DownloadMonitorBatchTiming
{
    internal static readonly TimeSpan PerSourceExecutionBudget = TimeSpan.FromMinutes(4);
    internal static readonly TimeSpan BatchWaitGracePeriod = TimeSpan.FromMinutes(2);
    internal static readonly TimeSpan EmailSendBuffer = TimeSpan.FromMinutes(2);
    internal static readonly TimeSpan DeferredEmailRetryInterval = TimeSpan.FromMinutes(2);
    internal static readonly TimeSpan MaxBatchLifecycle = TimeSpan.FromHours(2);
    internal static readonly int MaxDeferredEmailAttempts = 8;

    /// <summary>
    /// How long the batch orchestrator waits for all sources to reach a terminal state before deferring email.
    /// Sized for ~30 minute batches: stagger + serialized Playwright downloads + short grace.
    /// </summary>
    public static TimeSpan ResolveOrchestratorWaitTimeout(
        int sourceCount,
        int intervalMinutes,
        int maxBatchDurationMinutes = 30)
    {
        var interval = Math.Clamp(intervalMinutes, 0, 60);
        var count = Math.Max(sourceCount, 1);
        var stagger = TimeSpan.FromMinutes(Math.Max(0, count - 1) * interval);
        var execution = PerSourceExecutionBudget * count;
        var budget = stagger + BatchWaitGracePeriod + execution + EmailSendBuffer;
        var cap = TimeSpan.FromMinutes(Math.Clamp(maxBatchDurationMinutes, 15, 120)) - EmailSendBuffer;
        return budget < cap ? budget : cap;
    }

    public static TimeSpan ResolveOrchestratorWaitTimeout(PdfEditionSchedulerOptions options, int sourceCount) =>
        ResolveOrchestratorWaitTimeout(sourceCount, options.StaggerIntervalMinutes, options.MaxBatchDurationMinutes);

    public static TimeSpan ResolveStaggerWindow(int sourceCount, int intervalMinutes) =>
        TimeSpan.FromMinutes(Math.Max(0, sourceCount - 1) * Math.Clamp(intervalMinutes, 0, 60))
        + BatchWaitGracePeriod
        + PerSourceExecutionBudget;

    public static int ResolveOrchestratorDisableConcurrentTimeoutSeconds(
        int sourceCount,
        int intervalMinutes,
        int maxBatchDurationMinutes = 30) =>
        (int)Math.Ceiling(ResolveOrchestratorWaitTimeout(sourceCount, intervalMinutes, maxBatchDurationMinutes).TotalSeconds)
        + (int)DeferredEmailRetryInterval.TotalSeconds * 2
        + 600;
}

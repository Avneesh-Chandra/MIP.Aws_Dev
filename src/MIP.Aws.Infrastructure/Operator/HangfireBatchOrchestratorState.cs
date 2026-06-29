using Hangfire;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using MIP.Aws.Application.Configuration;

namespace MIP.Aws.Infrastructure.Operator;

internal static class HangfireBatchOrchestratorState
{
    private static readonly string[] OrchestratorQueues =
    [
        HangfireQueueOptions.Names.Critical,
        HangfireQueueOptions.Names.Downloads,
        HangfireQueueOptions.Names.Default
    ];

    public static bool IsBatchOrchestratorJobProcessing(string? hangfireJobId)
    {
        if (string.IsNullOrWhiteSpace(hangfireJobId) || JobStorage.Current is null)
        {
            return false;
        }

        try
        {
            var monitor = JobStorage.Current.GetMonitoringApi();
            foreach (var (jobId, _) in monitor.ProcessingJobs(0, 200))
            {
                if (string.Equals(jobId, hangfireJobId, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        catch
        {
            // If Hangfire monitoring is unavailable, do not block email enqueue.
        }

        return false;
    }

    /// <summary>
    /// True when the batch orchestrator is enqueued, scheduled, or actively processing.
    /// Deleted or finished jobs return false so the download monitor can close orphaned batches.
    /// </summary>
    public static bool IsBatchOrchestratorJobAlive(string? hangfireJobId)
    {
        if (string.IsNullOrWhiteSpace(hangfireJobId) || JobStorage.Current is null)
        {
            return false;
        }

        if (IsBatchOrchestratorJobProcessing(hangfireJobId))
        {
            return true;
        }

        try
        {
            var monitor = JobStorage.Current.GetMonitoringApi();
            foreach (var (jobId, _) in monitor.ScheduledJobs(0, 500))
            {
                if (string.Equals(jobId, hangfireJobId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            foreach (var queue in OrchestratorQueues)
            {
                try
                {
                    foreach (var (jobId, _) in monitor.EnqueuedJobs(queue, 0, 200))
                    {
                        if (string.Equals(jobId, hangfireJobId, StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                }
                catch
                {
                    // optional queue
                }
            }
        }
        catch
        {
            // If Hangfire monitoring is unavailable, treat as not alive.
        }

        return false;
    }
}

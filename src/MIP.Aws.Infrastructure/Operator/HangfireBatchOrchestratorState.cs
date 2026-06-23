using Hangfire;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;

namespace MIP.Aws.Infrastructure.Operator;

internal static class HangfireBatchOrchestratorState
{
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
}

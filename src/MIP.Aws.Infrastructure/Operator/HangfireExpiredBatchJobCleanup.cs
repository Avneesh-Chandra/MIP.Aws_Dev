using Hangfire;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure.Operator;

/// <summary>Cancels operator PDF batch orchestrator jobs left processing after deploy or timeout.</summary>
internal static class HangfireExpiredBatchJobCleanup
{
    private static readonly TimeSpan MaxOperatorBatchProcessingAge = TimeSpan.FromMinutes(70);

    public static void TryCancelExpiredOperatorBatchJobs(ILogger logger)
    {
        if (JobStorage.Current is null)
        {
            return;
        }

        IMonitoringApi monitor;
        try
        {
            monitor = JobStorage.Current.GetMonitoringApi();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not open Hangfire monitoring API for batch cleanup.");
            return;
        }

        IList<KeyValuePair<string, ProcessingJobDto>> processing;
        try
        {
            processing = monitor.ProcessingJobs(0, 200);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not list Hangfire processing jobs for batch cleanup.");
            return;
        }

        foreach (var (jobId, dto) in processing)
        {
            if (!IsOperatorPdfBatchJob(dto))
            {
                continue;
            }

            var startedAt = dto.StartedAt ?? DateTime.UtcNow;
            var age = DateTime.UtcNow - startedAt;
            if (age <= MaxOperatorBatchProcessingAge)
            {
                continue;
            }

            try
            {
                if (BackgroundJob.Delete(jobId))
                {
                    logger.LogWarning(
                        "Deleted expired operator PDF batch Hangfire job {JobId} (processing {AgeMinutes:F0} min).",
                        jobId,
                        age.TotalMinutes);
                }
                else
                {
                    logger.LogWarning(
                        "Could not delete expired operator PDF batch Hangfire job {JobId} (processing {AgeMinutes:F0} min).",
                        jobId,
                        age.TotalMinutes);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Error deleting expired operator PDF batch Hangfire job {JobId}.",
                    jobId);
            }
        }
    }

    private static bool IsOperatorPdfBatchJob(ProcessingJobDto dto)
    {
        var typeName = dto.Job?.Type?.Name ?? string.Empty;
        var methodName = dto.Job?.Method?.Name ?? string.Empty;
        return typeName.Contains("DownloadMonitorScheduledJobs", StringComparison.Ordinal)
               && methodName.Contains("ExecuteOperatorPdfBatch", StringComparison.Ordinal);
    }
}

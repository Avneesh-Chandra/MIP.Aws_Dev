using Hangfire;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Infrastructure.Jobs;
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
                var batchStartedAt = TryGetBatchStartedAt(dto);
                if (BackgroundJob.Delete(jobId))
                {
                    logger.LogWarning(
                        "Deleted expired operator PDF batch Hangfire job {JobId} (processing {AgeMinutes:F0} min).",
                        jobId,
                        age.TotalMinutes);

                    if (batchStartedAt is not null)
                    {
                        BackgroundJob.Schedule<DownloadMonitorScheduledJobs>(
                            HangfireQueueOptions.Names.Email,
                            j => j.SendCompletedBatchStatusEmailWhenReadyAsync(batchStartedAt.Value, 0),
                            DownloadMonitorBatchTiming.DeferredEmailRetryInterval);
                    }
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

    private static DateTimeOffset? TryGetBatchStartedAt(ProcessingJobDto dto)
    {
        var args = dto.Job?.Args;
        if (args is null || args.Count == 0)
        {
            return null;
        }

        return args[0] switch
        {
            DateTimeOffset batchStart => batchStart,
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
            string s when DateTimeOffset.TryParse(s, out var parsed) => parsed,
            _ => null
        };
    }
}

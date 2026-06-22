using Hangfire;
using Hangfire.Common;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using MIP.Aws.Application.Configuration;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure.Operator;

/// <summary>Removes queued or scheduled per-source download Hangfire jobs for operator abort/reset.</summary>
internal static class HangfireOperatorDownloadJobCleanup
{
    private static readonly string[] DownloadQueueNames =
    [
        HangfireQueueOptions.Names.Critical,
        HangfireQueueOptions.Names.Downloads,
        HangfireQueueOptions.Names.Default
    ];

    public static int TryRemoveMonitoredSourceDownloadJobs(
        IReadOnlySet<Guid> monitoredSourceIds,
        ILogger logger)
    {
        if (monitoredSourceIds.Count == 0 || JobStorage.Current is null)
        {
            return 0;
        }

        IMonitoringApi monitor;
        try
        {
            monitor = JobStorage.Current.GetMonitoringApi();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not open Hangfire monitoring API for download job cleanup.");
            return 0;
        }

        var removed = 0;
        removed += RemoveScheduledJobs(monitor.ScheduledJobs(0, 500), monitoredSourceIds, logger);
        removed += RemoveProcessingJobs(monitor.ProcessingJobs(0, 200), monitoredSourceIds, logger);

        foreach (var queue in DownloadQueueNames)
        {
            try
            {
                removed += RemoveEnqueuedJobs(monitor.EnqueuedJobs(queue, 0, 200), monitoredSourceIds, logger, queue);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not list Hangfire enqueued jobs for queue {Queue}.", queue);
            }
        }

        return removed;
    }

    public static int CountMonitoredSourceDownloadJobs(IReadOnlySet<Guid> monitoredSourceIds)
    {
        if (monitoredSourceIds.Count == 0 || JobStorage.Current is null)
        {
            return 0;
        }

        try
        {
            var monitor = JobStorage.Current.GetMonitoringApi();
            var count = 0;
            count += CountScheduledJobs(monitor.ScheduledJobs(0, 500), monitoredSourceIds);
            count += CountProcessingJobs(monitor.ProcessingJobs(0, 200), monitoredSourceIds);
            foreach (var queue in DownloadQueueNames)
            {
                try
                {
                    count += CountEnqueuedJobs(monitor.EnqueuedJobs(queue, 0, 200), monitoredSourceIds);
                }
                catch
                {
                    // optional queue
                }
            }

            return count;
        }
        catch
        {
            return 0;
        }
    }

    private static int CountScheduledJobs(JobList<ScheduledJobDto> jobs, IReadOnlySet<Guid> monitoredSourceIds) =>
        jobs.Count(x => TryGetMonitoredSourceId(x.Value.Job, monitoredSourceIds, out _));

    private static int CountEnqueuedJobs(JobList<EnqueuedJobDto> jobs, IReadOnlySet<Guid> monitoredSourceIds) =>
        jobs.Count(x => TryGetMonitoredSourceId(x.Value.Job, monitoredSourceIds, out _));

    private static int CountProcessingJobs(
        IList<KeyValuePair<string, ProcessingJobDto>> jobs,
        IReadOnlySet<Guid> monitoredSourceIds) =>
        jobs.Count(x => TryGetMonitoredSourceId(x.Value.Job, monitoredSourceIds, out _));

    public static bool TryDeleteOperatorBatchOrchestrator(string? hangfireJobId, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(hangfireJobId) || JobStorage.Current is null)
        {
            return false;
        }

        try
        {
            if (BackgroundJob.Delete(hangfireJobId))
            {
                logger.LogWarning("Deleted operator PDF batch orchestrator Hangfire job {JobId}.", hangfireJobId);
                return true;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not delete operator PDF batch orchestrator Hangfire job {JobId}.", hangfireJobId);
        }

        return false;
    }

    private static int RemoveScheduledJobs(
        JobList<ScheduledJobDto> jobs,
        IReadOnlySet<Guid> monitoredSourceIds,
        ILogger logger)
    {
        var removed = 0;
        foreach (var (jobId, dto) in jobs)
        {
            if (!TryGetMonitoredSourceId(dto.Job, monitoredSourceIds, out var sourceId))
            {
                continue;
            }

            if (TryDelete(jobId, "scheduled", sourceId, logger))
            {
                removed++;
            }
        }

        return removed;
    }

    private static int RemoveEnqueuedJobs(
        JobList<EnqueuedJobDto> jobs,
        IReadOnlySet<Guid> monitoredSourceIds,
        ILogger logger,
        string queue)
    {
        var removed = 0;
        foreach (var (jobId, dto) in jobs)
        {
            if (!TryGetMonitoredSourceId(dto.Job, monitoredSourceIds, out var sourceId))
            {
                continue;
            }

            if (TryDelete(jobId, $"enqueued:{queue}", sourceId, logger))
            {
                removed++;
            }
        }

        return removed;
    }

    private static int RemoveProcessingJobs(
        IList<KeyValuePair<string, ProcessingJobDto>> jobs,
        IReadOnlySet<Guid> monitoredSourceIds,
        ILogger logger)
    {
        var removed = 0;
        foreach (var (jobId, dto) in jobs)
        {
            if (!TryGetMonitoredSourceId(dto.Job, monitoredSourceIds, out var sourceId))
            {
                continue;
            }

            if (TryDelete(jobId, "processing", sourceId, logger))
            {
                removed++;
            }
        }

        return removed;
    }

    private static bool TryDelete(string jobId, string bucket, Guid sourceId, ILogger logger)
    {
        try
        {
            if (BackgroundJob.Delete(jobId))
            {
                logger.LogInformation(
                    "Removed {Bucket} Hangfire download job {JobId} for monitored source {SourceId}.",
                    bucket,
                    jobId,
                    sourceId);
                return true;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Could not remove {Bucket} Hangfire download job {JobId} for source {SourceId}.",
                bucket,
                jobId,
                sourceId);
        }

        return false;
    }

    private static bool TryGetMonitoredSourceId(
        Job? job,
        IReadOnlySet<Guid> monitoredSourceIds,
        out Guid sourceId)
    {
        sourceId = Guid.Empty;
        if (job is null)
        {
            return false;
        }

        var typeName = job.Type?.Name ?? string.Empty;
        var methodName = job.Method?.Name ?? string.Empty;
        var isPdf = typeName.Contains("PdfEditionJobs", StringComparison.Ordinal)
                    && methodName.Contains("DiscoverAndDownloadTodayPdf", StringComparison.Ordinal);
        var isPortal = typeName.Contains("NewsIngestionJobs", StringComparison.Ordinal)
                       && methodName.Contains("DownloadSource", StringComparison.Ordinal);
        if (!isPdf && !isPortal)
        {
            return false;
        }

        if (job.Args is not { Count: > 0 })
        {
            return false;
        }

        sourceId = job.Args[0] switch
        {
            Guid id => id,
            string s when Guid.TryParse(s, out var parsed) => parsed,
            _ => Guid.Empty
        };

        return sourceId != Guid.Empty && monitoredSourceIds.Contains(sourceId);
    }
}

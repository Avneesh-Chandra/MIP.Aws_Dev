using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Operator;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Infrastructure.Jobs;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Operator;

public sealed class DownloadMonitorBatchRunService(
    IServiceScopeFactory scopeFactory,
    IMemoryCache cache,
    IOptions<PdfEditionSchedulerOptions> schedulerOptions,
    ILogger<DownloadMonitorBatchRunService> logger) : IDownloadMonitorBatchRunService
{
    private const string ActiveBatchCacheKey = "download-monitor-batch:active";
    private static readonly TimeSpan BatchCacheTtl = TimeSpan.FromHours(8);
    private static readonly TimeSpan SchedulerGracePeriod = TimeSpan.FromMinutes(2);

    public async Task<DownloadMonitorBatchRunResult> StartBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var existing = cache.Get<BatchCacheEntry>(ActiveBatchCacheKey);
        if (existing is not null)
        {
            var existingProgress = await BuildProgressAsync(db, existing, cancellationToken).ConfigureAwait(false);
            if (existingProgress.IsActive)
            {
                throw new InvalidOperationException(
                    "A PDF download batch is already running. Wait for it to finish or refresh progress.");
            }
        }

        var sources = await LoadMonitoredSourcesAsync(db, cancellationToken).ConfigureAwait(false);
        if (sources.Count == 0)
        {
            throw new InvalidOperationException("No enabled download-monitor sources are configured.");
        }

        var startedAt = DateTimeOffset.UtcNow;
        var hangfireJobId = BackgroundJob.Enqueue<DownloadMonitorScheduledJobs>(
            j => j.ExecuteOperatorPdfBatchAsync(startedAt));

        var entry = new BatchCacheEntry(startedAt, sources.Count, hangfireJobId);
        cache.Set(ActiveBatchCacheKey, entry, BatchCacheTtl);

        logger.LogInformation(
            "Operator started PDF download batch {HangfireJobId} for {Count} source(s) at {StartedAt:u}.",
            hangfireJobId,
            sources.Count,
            startedAt);

        return new DownloadMonitorBatchRunResult(startedAt, sources.Count, hangfireJobId);
    }

    public async Task<DownloadMonitorBatchProgressResult?> GetProgressAsync(
        DateTimeOffset? batchStartedAt,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var entry = ResolveBatchEntry(batchStartedAt);
        if (entry is null)
        {
            return null;
        }

        return await BuildProgressAsync(db, entry, cancellationToken).ConfigureAwait(false);
    }

    private BatchCacheEntry? ResolveBatchEntry(DateTimeOffset? batchStartedAt)
    {
        if (batchStartedAt is DateTimeOffset explicitStart)
        {
            var cached = cache.Get<BatchCacheEntry>(ActiveBatchCacheKey);
            if (cached is not null && cached.StartedAt == explicitStart)
            {
                return cached;
            }

            return new BatchCacheEntry(explicitStart, 0, string.Empty);
        }

        return cache.Get<BatchCacheEntry>(ActiveBatchCacheKey);
    }

    private async Task<DownloadMonitorBatchProgressResult> BuildProgressAsync(
        IApplicationDbContext db,
        BatchCacheEntry entry,
        CancellationToken cancellationToken)
    {
        var sources = await LoadMonitoredSourcesAsync(db, cancellationToken).ConfigureAwait(false);
        var total = entry.TotalSources > 0 ? entry.TotalSources : sources.Count;
        var sourceIds = sources.Select(s => s.Id).ToHashSet();

        var jobs = await db.DownloadJobs.AsNoTracking()
            .Where(j => !j.IsDeleted
                        && j.Trigger == DownloadJobTrigger.Scheduled
                        && j.CreatedAt >= entry.StartedAt
                        && sourceIds.Contains(j.NewsSourceId))
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var latestJobBySource = new Dictionary<Guid, DownloadJob>();
        foreach (var job in jobs)
        {
            latestJobBySource.TryAdd(job.NewsSourceId, job);
        }

        var activities = new List<DownloadMonitorBatchActivityResult>();
        var successCount = 0;
        var failedCount = 0;
        var inProgressCount = 0;
        var waitingCount = 0;
        var autoRecoveryCount = 0;

        var interval = Math.Clamp(schedulerOptions.Value.StaggerIntervalMinutes, 1, 60);
        var staggerWindow = TimeSpan.FromMinutes(Math.Max(0, total - 1) * interval) + SchedulerGracePeriod;
        var withinStaggerWindow = DateTimeOffset.UtcNow - entry.StartedAt <= staggerWindow;

        foreach (var source in sources)
        {
            if (!latestJobBySource.TryGetValue(source.Id, out var job))
            {
                if (withinStaggerWindow)
                {
                    waitingCount++;
                    activities.Add(new DownloadMonitorBatchActivityResult(
                        source.Name,
                        "Waiting for scheduled download slot",
                        "Waiting"));
                }
                else
                {
                    failedCount++;
                    activities.Add(new DownloadMonitorBatchActivityResult(
                        source.Name,
                        "Scheduled download did not start",
                        "Failed"));
                }

                continue;
            }

            var (state, activity) = DescribeJob(job);
            activities.Add(new DownloadMonitorBatchActivityResult(source.Name, activity, state));

            switch (state)
            {
                case "Success":
                    successCount++;
                    break;
                case "Failed":
                    failedCount++;
                    break;
                case "AutoRecovery":
                    autoRecoveryCount++;
                    inProgressCount++;
                    break;
                case "InProgress":
                    inProgressCount++;
                    break;
                default:
                    if (withinStaggerWindow)
                    {
                        waitingCount++;
                    }
                    else
                    {
                        failedCount++;
                    }

                    break;
            }
        }

        var completedCount = successCount + failedCount;
        var isComplete = inProgressCount == 0 && autoRecoveryCount == 0 && waitingCount == 0;
        var isActive = !isComplete;
        var percent = total == 0
            ? 100
            : Math.Round(completedCount * 100.0 / total, 1);
        if (!isComplete && completedCount > 0)
        {
            percent = Math.Min(percent, 99);
        }

        var currentPhase = ResolvePhase(inProgressCount, autoRecoveryCount, waitingCount, isComplete, withinStaggerWindow);
        var statusSummary = BuildSummary(
            completedCount,
            total,
            successCount,
            failedCount,
            inProgressCount,
            autoRecoveryCount,
            waitingCount,
            isComplete);

        return new DownloadMonitorBatchProgressResult(
            entry.StartedAt,
            total,
            completedCount,
            successCount,
            failedCount,
            inProgressCount,
            waitingCount,
            autoRecoveryCount,
            percent,
            isActive,
            isComplete,
            currentPhase,
            statusSummary,
            activities.OrderBy(a => a.State switch
            {
                "AutoRecovery" => 0,
                "InProgress" => 1,
                "Waiting" => 2,
                "Failed" => 3,
                _ => 4
            }).ThenBy(a => a.SourceName).ToList());
    }

    private static string ResolvePhase(
        int inProgressCount,
        int autoRecoveryCount,
        int waitingCount,
        bool isComplete,
        bool withinStaggerWindow)
    {
        if (isComplete)
        {
            return "Complete";
        }

        if (autoRecoveryCount > 0)
        {
            return "Auto AI recovery";
        }

        if (inProgressCount > 0)
        {
            return "Downloading";
        }

        if (waitingCount > 0 && withinStaggerWindow)
        {
            return "Scheduling";
        }

        return waitingCount > 0 ? "Downloading" : "Complete";
    }

    private static string BuildSummary(
        int completed,
        int total,
        int success,
        int failed,
        int inProgress,
        int autoRecovery,
        int waiting,
        bool isComplete)
    {
        if (isComplete)
        {
            return $"Batch complete — {success} succeeded, {failed} failed (of {total} sources).";
        }

        var parts = new List<string> { $"{completed}/{total} finished" };
        if (inProgress > 0)
        {
            parts.Add($"{inProgress} in progress");
        }

        if (autoRecovery > 0)
        {
            parts.Add($"{autoRecovery} in auto recovery");
        }

        if (waiting > 0)
        {
            parts.Add($"{waiting} waiting");
        }

        return string.Join(" · ", parts);
    }

    private static (string State, string Activity) DescribeJob(DownloadJob job) => job.Status switch
    {
        DownloadJobStatus.Succeeded or DownloadJobStatus.SuccessWithAutoAiRecovery =>
            ("Success", job.Status == DownloadJobStatus.SuccessWithAutoAiRecovery
                ? "Recovered via automatic AI"
                : "Download succeeded"),
        DownloadJobStatus.Failed or DownloadJobStatus.FailedAfterAutoAiRecovery
            or DownloadJobStatus.ManualInterventionRequired or DownloadJobStatus.AutoAiRecoverySkipped
            or DownloadJobStatus.Cancelled =>
            ("Failed", MapFailureActivity(job.Status)),
        DownloadJobStatus.AutoAiRecoveryAnalyzing =>
            ("AutoRecovery", "Analyzing failure with AI"),
        DownloadJobStatus.AutoAiRecoveryApplying =>
            ("AutoRecovery", "Applying AI-suggested fix"),
        DownloadJobStatus.AutoAiRecoveryRetrying =>
            ("AutoRecovery", "Retrying download after fix"),
        DownloadJobStatus.Running =>
            ("InProgress", "Downloading edition"),
        DownloadJobStatus.Pending =>
            ("InProgress", "Queued for download"),
        _ => ("InProgress", "Processing")
    };

    private static string MapFailureActivity(DownloadJobStatus status) => status switch
    {
        DownloadJobStatus.FailedAfterAutoAiRecovery => "Failed after automatic AI recovery",
        DownloadJobStatus.ManualInterventionRequired => "Manual intervention required",
        DownloadJobStatus.AutoAiRecoverySkipped => "Auto recovery skipped",
        DownloadJobStatus.Cancelled => "Cancelled",
        _ => "Download failed"
    };

    private static async Task<List<NewsSource>> LoadMonitoredSourcesAsync(
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var all = await db.NewsSources.AsNoTracking()
            .Where(s => !s.IsDeleted && s.IsEnabled)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return all.Where(PdfManagementSourceRules.IsPdfDownloadMonitoredSource).ToList();
    }

    private sealed record BatchCacheEntry(DateTimeOffset StartedAt, int TotalSources, string HangfireJobId);
}

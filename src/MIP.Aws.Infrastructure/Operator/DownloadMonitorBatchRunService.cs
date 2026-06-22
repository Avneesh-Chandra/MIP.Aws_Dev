using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Abstractions.Operator;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Infrastructure.Jobs;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Operator;

public sealed class DownloadMonitorBatchRunService(
    IServiceScopeFactory scopeFactory,
    IOptions<PdfEditionSchedulerOptions> schedulerOptions,
    ILogger<DownloadMonitorBatchRunService> logger) : IDownloadMonitorBatchRunService
{
    private static readonly TimeSpan BatchRetention = TimeSpan.FromHours(8);
    private static readonly TimeSpan BatchWaitGracePeriod = TimeSpan.FromMinutes(50);
    private static readonly TimeSpan InferredBatchWindow = TimeSpan.FromHours(3);

    public async Task<DownloadMonitorBatchRunResult> StartBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var recoveryOrchestrator = scope.ServiceProvider.GetRequiredService<ISourceRecoveryOrchestrator>();
        var autoAiEnqueue = scope.ServiceProvider.GetRequiredService<IAutoAiDownloadRecoveryEnqueueService>();

        var existing = await FindLatestBatchEntryAsync(db, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            var existingProgress = await BuildProgressAsync(
                    db,
                    recoveryOrchestrator,
                    autoAiEnqueue,
                    existing,
                    cancellationToken)
                .ConfigureAwait(false);
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

        await DownloadMonitorBatchRunPersistence.PersistAsync(
                db,
                startedAt,
                sources.Count,
                hangfireJobId,
                logger,
                cancellationToken)
            .ConfigureAwait(false);

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
        var recoveryOrchestrator = scope.ServiceProvider.GetRequiredService<ISourceRecoveryOrchestrator>();

        var entry = await ResolveBatchEntryAsync(db, batchStartedAt, cancellationToken).ConfigureAwait(false);
        if (entry is null)
        {
            return null;
        }

        var autoAiEnqueue = scope.ServiceProvider.GetRequiredService<IAutoAiDownloadRecoveryEnqueueService>();
        return await BuildProgressAsync(db, recoveryOrchestrator, autoAiEnqueue, entry, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<BatchEntry?> FindLatestBatchEntryAsync(
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        try
        {
            var cutoff = DateTimeOffset.UtcNow.Subtract(BatchRetention);
            var latest = await db.DownloadMonitorBatchRuns.AsNoTracking()
                .Where(b => !b.IsDeleted && b.StartedAt >= cutoff)
                .OrderByDescending(b => b.StartedAt)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (latest is not null)
            {
                return new BatchEntry(latest.StartedAt, latest.TotalSources, latest.HangfireJobId);
            }
        }
        catch (Exception ex) when (DownloadMonitorBatchRunPersistence.IsMissingBatchRunsTable(ex))
        {
            // Table not migrated yet — fall through to job inference.
        }

        return await TryInferLatestBatchFromJobsAsync(db, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<BatchEntry?> TryInferLatestBatchFromJobsAsync(
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.Subtract(BatchRetention);
        var sources = await LoadMonitoredSourcesAsync(db, cancellationToken).ConfigureAwait(false);
        if (sources.Count == 0)
        {
            return null;
        }

        var sourceIds = sources.Select(s => s.Id).ToHashSet();
        var jobs = await db.DownloadJobs.AsNoTracking()
            .Where(j => !j.IsDeleted && j.CreatedAt >= cutoff && sourceIds.Contains(j.NewsSourceId))
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (jobs.Count == 0)
        {
            return null;
        }

        var latestActivity = jobs[0].CreatedAt;
        var earliestInBatch = jobs
            .Where(j => latestActivity - j.CreatedAt <= InferredBatchWindow)
            .Min(j => j.CreatedAt);

        return new BatchEntry(earliestInBatch.AddMinutes(-1), sources.Count, string.Empty);
    }

    private static async Task<BatchEntry?> ResolveBatchEntryAsync(
        IApplicationDbContext db,
        DateTimeOffset? batchStartedAt,
        CancellationToken cancellationToken)
    {
        if (batchStartedAt is DateTimeOffset explicitStart)
        {
            try
            {
                var row = await db.DownloadMonitorBatchRuns.AsNoTracking()
                    .Where(b => !b.IsDeleted && b.StartedAt == explicitStart)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (row is not null)
                {
                    return new BatchEntry(row.StartedAt, row.TotalSources, row.HangfireJobId);
                }
            }
            catch (Exception ex) when (DownloadMonitorBatchRunPersistence.IsMissingBatchRunsTable(ex))
            {
                // Table not migrated yet.
            }

            return new BatchEntry(explicitStart, 0, string.Empty);
        }

        return await FindLatestBatchEntryAsync(db, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DownloadMonitorBatchProgressResult> BuildProgressAsync(
        IApplicationDbContext db,
        ISourceRecoveryOrchestrator recoveryOrchestrator,
        IAutoAiDownloadRecoveryEnqueueService autoAiEnqueue,
        BatchEntry entry,
        CancellationToken cancellationToken)
    {
        await recoveryOrchestrator.ReconcileAllAsync(cancellationToken).ConfigureAwait(false);

        var sources = await LoadMonitoredSourcesAsync(db, cancellationToken).ConfigureAwait(false);
        var total = entry.TotalSources > 0 ? entry.TotalSources : sources.Count;
        var sourceIds = sources.Select(s => s.Id).ToHashSet();

        var jobs = await db.DownloadJobs.AsNoTracking()
            .Where(j => !j.IsDeleted
                        && j.CreatedAt >= entry.StartedAt.AddMinutes(-1)
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

        foreach (var source in sources)
        {
            string state;
            string activity;

            if (latestJobBySource.TryGetValue(source.Id, out var job))
            {
                (state, activity) = DescribeJob(job);
            }
            else if (await DownloadMonitorBatchOutcomeHelper.HasSuccessfulPdfEditionSinceBatchAsync(
                             db,
                             source.Id,
                             entry.StartedAt,
                             cancellationToken)
                         .ConfigureAwait(false))
            {
                state = "Success";
                activity = "Today's edition already downloaded";
            }
            else
            {
                waitingCount++;
                activities.Add(new DownloadMonitorBatchActivityResult(
                    source.Name,
                    "Waiting for scheduled download slot",
                    "Waiting"));
                continue;
            }

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
                    waitingCount++;
                    break;
            }
        }

        var completedCount = successCount + failedCount;
        var interval = Math.Clamp(schedulerOptions.Value.StaggerIntervalMinutes, 1, 60);
        var staggerWindow = TimeSpan.FromMinutes(Math.Max(0, total - 1) * interval) + BatchWaitGracePeriod;
        var withinStaggerWindow = DateTimeOffset.UtcNow - entry.StartedAt <= staggerWindow;
        var isComplete = inProgressCount == 0 && (completedCount >= total || !withinStaggerWindow);
        var isActive = !isComplete && (inProgressCount > 0 || (waitingCount > 0 && withinStaggerWindow));
        var percent = total == 0
            ? 100
            : Math.Round((completedCount + (isComplete && waitingCount > 0 ? waitingCount : 0)) * 100.0 / total, 1);
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
            entry.HangfireJobId,
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

    private sealed record BatchEntry(DateTimeOffset StartedAt, int TotalSources, string HangfireJobId);
}

using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Infrastructure.Intelligence.Recovery;
using MIP.Aws.Infrastructure.Jobs;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure.Operator;

/// <summary>Heals download jobs left in non-terminal states after worker restarts or aborted Hangfire runs.</summary>
internal static class DownloadJobReconciliation
{
    private static readonly TimeSpan StaleAutoRecoveryAnalyzingThreshold = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan StaleAutoRecoveryApplyingThreshold = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan StaleAutoRecoveryRetryingThreshold = TimeSpan.FromMinutes(25);

    public static async Task ReconcileStaleJobsAsync(
        IApplicationDbContext db,
        ISourceRecoveryOrchestrator recoveryOrchestrator,
        IAutoAiDownloadRecoveryEnqueueService? autoAiEnqueue,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        await ReconcileRunningAndPendingAsync(db, recoveryOrchestrator, autoAiEnqueue, logger, cancellationToken)
            .ConfigureAwait(false);
        await ReconcileStaleAutoRecoveryJobsAsync(db, logger, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ReconcileRunningAndPendingAsync(
        IApplicationDbContext db,
        ISourceRecoveryOrchestrator recoveryOrchestrator,
        IAutoAiDownloadRecoveryEnqueueService? autoAiEnqueue,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var runningJobs = await db.DownloadJobs
            .Where(j => !j.IsDeleted
                        && (j.Status == DownloadJobStatus.Running || j.Status == DownloadJobStatus.Pending))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (runningJobs.Count == 0)
        {
            return;
        }

        var sourceIds = runningJobs.Select(j => j.NewsSourceId).Distinct().ToList();
        var sourcesById = await db.NewsSources.AsNoTracking()
            .Where(s => sourceIds.Contains(s.Id) && !s.IsDeleted)
            .ToDictionaryAsync(s => s.Id, cancellationToken)
            .ConfigureAwait(false);

        var staleJobs = runningJobs
            .Where(j =>
            {
                sourcesById.TryGetValue(j.NewsSourceId, out var source);
                return DownloadJobRunningTiming.IsRunningJobStale(j, source);
            })
            .ToList();

        if (staleJobs.Count == 0)
        {
            return;
        }

        var staleJobIds = staleJobs.Select(j => j.Id).ToList();
        var downloadedPdfJobIds = await db.PdfEditionDownloads.AsNoTracking()
            .Where(p => !p.IsDeleted
                        && p.DownloadJobId != null
                        && staleJobIds.Contains(p.DownloadJobId.Value)
                        && p.Status == PdfEditionStatus.Downloaded)
            .Select(p => p.DownloadJobId!.Value)
            .ToHashSetAsync(cancellationToken)
            .ConfigureAwait(false);

        var editionDate = DateOnly.FromDateTime(now.UtcDateTime);
        var dayStart = new DateTimeOffset(editionDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);
        var sourcesWithDownloadedEditionToday = await db.PdfEditionDownloads.AsNoTracking()
            .Where(p => !p.IsDeleted
                        && sourceIds.Contains(p.NewsSourceId)
                        && p.Status == PdfEditionStatus.Downloaded
                        && (p.EditionDate == editionDate
                            || (p.DownloadedAt >= dayStart && p.DownloadedAt < dayEnd)))
            .Select(p => p.NewsSourceId)
            .ToHashSetAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var job in staleJobs)
        {
            if (downloadedPdfJobIds.Contains(job.Id))
            {
                job.Status = DownloadJobStatus.Succeeded;
                job.ErrorMessage = null;
            }
            else if (sourcesWithDownloadedEditionToday.Contains(job.NewsSourceId))
            {
                job.Status = DownloadJobStatus.Failed;
                job.ErrorMessage ??= "Download attempt was superseded; today's edition was already acquired.";
            }
            else
            {
                job.Status = DownloadJobStatus.Failed;
                job.ErrorMessage ??= "Download was interrupted or timed out.";
            }

            job.CompletedAt ??= DateTimeOffset.UtcNow;
            job.ModifiedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogWarning("Reconciled {Count} stale download job(s) stuck in Running/Pending.", staleJobs.Count);

        foreach (var job in staleJobs.Where(IsRecoveryDownloadJob))
        {
            try
            {
                await recoveryOrchestrator.FinalizeAttemptAsync(ExtractRecoveryAttemptId(job), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not finalize recovery attempt for stale job {JobId}.", job.Id);
            }
        }

        foreach (var job in staleJobs.Where(j =>
                     j.Status == DownloadJobStatus.Failed
                     && !sourcesWithDownloadedEditionToday.Contains(j.NewsSourceId)))
        {
            if (autoAiEnqueue is not null)
            {
                try
                {
                    await autoAiEnqueue.TryEnqueueAfterFailureAsync(job, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not enqueue auto AI recovery for reconciled job {JobId}.", job.Id);
                }
            }

            if (sourcesById.TryGetValue(job.NewsSourceId, out var source)
                && source.PdfDiscoveryEnabled
                && source.SourceType is NewsSourceType.PublicHtml or NewsSourceType.PublicPdf)
            {
                BackgroundJob.Enqueue<PdfEditionJobs>(j => j.DiscoverAndDownloadTodayPdfAsync(job.NewsSourceId));
                logger.LogInformation(
                    "Re-queued PDF edition download for {Source} after stale job {JobId} was reconciled.",
                    source.Name,
                    job.Id);
            }
        }
    }

    private static async Task ReconcileStaleAutoRecoveryJobsAsync(
        IApplicationDbContext db,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var jobs = await db.DownloadJobs
            .Where(j => !j.IsDeleted
                        && (j.Status == DownloadJobStatus.AutoAiRecoveryAnalyzing
                            || j.Status == DownloadJobStatus.AutoAiRecoveryApplying
                            || j.Status == DownloadJobStatus.AutoAiRecoveryRetrying))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var staleJobs = jobs.Where(j => IsStaleAutoRecoveryJob(j, now)).ToList();
        if (staleJobs.Count == 0)
        {
            return;
        }

        var runIds = staleJobs
            .Where(j => j.AutoAiRecoveryRunId is not null)
            .Select(j => j.AutoAiRecoveryRunId!.Value)
            .ToHashSet();

        var runs = runIds.Count == 0
            ? []
            : await db.AutoAiRecoveryRuns
                .Where(r => runIds.Contains(r.Id) && !r.IsDeleted)
                .ToDictionaryAsync(r => r.Id, cancellationToken)
                .ConfigureAwait(false);

        foreach (var job in staleJobs)
        {
            var message = job.Status switch
            {
                DownloadJobStatus.AutoAiRecoveryAnalyzing => "Auto AI recovery analysis timed out.",
                DownloadJobStatus.AutoAiRecoveryApplying => "Auto AI recovery apply step timed out or was interrupted.",
                DownloadJobStatus.AutoAiRecoveryRetrying => "Auto AI recovery retry timed out.",
                _ => "Auto AI recovery timed out."
            };

            job.Status = DownloadJobStatus.FailedAfterAutoAiRecovery;
            job.ErrorMessage ??= message;
            job.CompletedAt ??= now;
            job.ModifiedAt = now;

            if (job.AutoAiRecoveryRunId is Guid runId && runs.TryGetValue(runId, out var run) && run.CompletedAt is null)
            {
                run.Status = AutoAiRecoveryRunStatus.CompletedFailure;
                run.ResultSummary = message;
                run.CompletedAt = now;
                AutoAiRecoveryTimelineWriter.AddStep(run, "Timed out", message, succeeded: false);
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogWarning(
            "Reconciled {Count} stale download job(s) stuck in auto AI recovery.",
            staleJobs.Count);
    }

    private static bool IsStaleAutoRecoveryJob(DownloadJob job, DateTimeOffset now)
    {
        var anchor = job.ModifiedAt ?? job.StartedAt ?? job.CreatedAt;
        var threshold = job.Status switch
        {
            DownloadJobStatus.AutoAiRecoveryApplying => StaleAutoRecoveryApplyingThreshold,
            DownloadJobStatus.AutoAiRecoveryAnalyzing => StaleAutoRecoveryAnalyzingThreshold,
            DownloadJobStatus.AutoAiRecoveryRetrying => StaleAutoRecoveryRetryingThreshold,
            _ => StaleAutoRecoveryAnalyzingThreshold
        };

        return now - anchor > threshold;
    }

    private static bool IsRecoveryDownloadJob(DownloadJob job) =>
        !string.IsNullOrWhiteSpace(job.CorrelationId)
        && job.CorrelationId.StartsWith("recovery:", StringComparison.OrdinalIgnoreCase);

    private static Guid ExtractRecoveryAttemptId(DownloadJob job)
    {
        if (string.IsNullOrWhiteSpace(job.CorrelationId)
            || !job.CorrelationId.StartsWith("recovery:", StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParse(job.CorrelationId["recovery:".Length..], out var attemptId))
        {
            throw new InvalidOperationException("Download job is not linked to a recovery attempt.");
        }

        return attemptId;
    }
}

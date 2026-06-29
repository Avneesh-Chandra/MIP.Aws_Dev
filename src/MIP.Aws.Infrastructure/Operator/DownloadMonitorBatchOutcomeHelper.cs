using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.AutoAiRecovery;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Infrastructure.Operator;

public static class DownloadMonitorBatchOutcomeHelper
{
    public static bool IsAutoRecoveryInProgressStatus(DownloadJobStatus status) =>
        status is DownloadJobStatus.AutoAiRecoveryAnalyzing
            or DownloadJobStatus.AutoAiRecoveryApplying
            or DownloadJobStatus.AutoAiRecoveryRetrying;

    public static bool IsTerminalDownloadStatus(DownloadJobStatus status) =>
        status is DownloadJobStatus.Succeeded
            or DownloadJobStatus.SuccessWithAutoAiRecovery
            or DownloadJobStatus.Failed
            or DownloadJobStatus.FailedAfterAutoAiRecovery
            or DownloadJobStatus.ManualInterventionRequired
            or DownloadJobStatus.AutoAiRecoverySkipped
            or DownloadJobStatus.Cancelled;

    public static bool IsSuccessfulDownloadStatus(DownloadJobStatus status) =>
        status is DownloadJobStatus.Succeeded or DownloadJobStatus.SuccessWithAutoAiRecovery;

    /// <summary>
    /// True when the source has a final outcome for this batch, including after required auto AI recovery finishes.
    /// </summary>
    public static async Task<bool> IsSourceSettledAsync(
        IApplicationDbContext db,
        Guid sourceId,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        if (await HasTodaysDownloadedEditionAsync(db, sourceId, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (await HasSuccessfulPdfEditionSinceBatchAsync(db, sourceId, batchStartedAt, cancellationToken)
                .ConfigureAwait(false))
        {
            return true;
        }

        if (await HasIncompleteAutoRecoveryRunSinceBatchAsync(db, sourceId, batchStartedAt, cancellationToken)
                .ConfigureAwait(false))
        {
            return false;
        }

        var latestJob = await GetLatestJobSinceBatchAsync(db, sourceId, batchStartedAt, cancellationToken)
            .ConfigureAwait(false);

        if (latestJob is null)
        {
            return false;
        }

        if (IsAutoRecoveryInProgressStatus(latestJob.Status))
        {
            return false;
        }

        if (latestJob.Status == DownloadJobStatus.Failed
            && !await IsRequiredAutoRecoveryCompleteAsync(db, latestJob, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        return IsTerminalDownloadStatus(latestJob.Status);
    }

    /// <summary>All monitored sources settled, including any in-flight or pending auto AI recovery finishes.</summary>
    public static async Task<bool> IsBatchReadyForStatusEmailAsync(
        IApplicationDbContext db,
        IReadOnlyList<Guid> sourceIds,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        foreach (var sourceId in sourceIds)
        {
            if (!await IsSourceSettledAsync(db, sourceId, batchStartedAt, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }

        return sourceIds.Count > 0;
    }

    /// <summary>
    /// True when every source has finished its batch download attempt (success, failure, or entered auto-recovery).
    /// Does not wait for auto-recovery to complete.
    /// </summary>
    public static async Task<bool> IsBatchReadyForInitialStatusEmailAsync(
        IApplicationDbContext db,
        IReadOnlyList<Guid> sourceIds,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        foreach (var sourceId in sourceIds)
        {
            if (!await IsBatchDownloadPhaseCompleteForSourceAsync(
                    db,
                    sourceId,
                    batchStartedAt,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                return false;
            }
        }

        return sourceIds.Count > 0;
    }

    public static async Task<bool> IsBatchDownloadPhaseCompleteForSourceAsync(
        IApplicationDbContext db,
        Guid sourceId,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        if (await HasTodaysDownloadedEditionAsync(db, sourceId, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (await HasSuccessfulPdfEditionSinceBatchAsync(db, sourceId, batchStartedAt, cancellationToken)
                .ConfigureAwait(false))
        {
            return true;
        }

        var latestJob = await GetLatestJobSinceBatchAsync(db, sourceId, batchStartedAt, cancellationToken)
            .ConfigureAwait(false);

        if (latestJob is null)
        {
            return false;
        }

        if (latestJob.Status is DownloadJobStatus.Pending or DownloadJobStatus.Running)
        {
            return false;
        }

        return IsAutoRecoveryInProgressStatus(latestJob.Status) || IsTerminalDownloadStatus(latestJob.Status);
    }

    public static async Task<bool> HasPendingAutoRecoveryForBatchAsync(
        IApplicationDbContext db,
        IReadOnlyList<Guid> sourceIds,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        if (sourceIds.Count == 0)
        {
            return false;
        }

        var notBefore = batchStartedAt.AddMinutes(-1);

        return await db.AutoAiRecoveryRuns.AsNoTracking()
            .AnyAsync(
                r => !r.IsDeleted
                     && sourceIds.Contains(r.NewsSourceId)
                     && r.CreatedAt >= notBefore
                     && r.CompletedAt == null,
                cancellationToken)
            .ConfigureAwait(false)
            || await db.SourceRecoveryAttempts.AsNoTracking()
                .AnyAsync(
                    a => !a.IsDeleted
                         && sourceIds.Contains(a.NewsSourceId)
                         && a.CreatedAt >= notBefore
                         && a.Status == SourceRecoveryAttemptStatus.RetryEnqueued,
                    cancellationToken)
                .ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<PendingBatchRecoverySource>> GetPendingAutoRecoverySourcesForBatchAsync(
        IApplicationDbContext db,
        IReadOnlyList<Guid> sourceIds,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        if (sourceIds.Count == 0)
        {
            return [];
        }

        var notBefore = batchStartedAt.AddMinutes(-1);
        var runs = await db.AutoAiRecoveryRuns.AsNoTracking()
            .Where(r => !r.IsDeleted
                        && sourceIds.Contains(r.NewsSourceId)
                        && r.CreatedAt >= notBefore
                        && r.CompletedAt == null)
            .Join(
                db.NewsSources.AsNoTracking().Where(s => !s.IsDeleted),
                r => r.NewsSourceId,
                s => s.Id,
                (r, s) => new PendingBatchRecoverySource(s.Id, s.Name, r.Status, r.FailedDownloadJobId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return runs;
    }

    public static async Task<DateTimeOffset?> ResolveBatchForRecoveryFollowUpAsync(
        IApplicationDbContext db,
        Guid failedDownloadJobId,
        CancellationToken cancellationToken)
    {
        var job = await db.DownloadJobs.AsNoTracking()
            .Where(j => !j.IsDeleted && j.Id == failedDownloadJobId)
            .Select(j => new { j.CreatedAt })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (job is null)
        {
            return null;
        }

        var batch = await db.DownloadMonitorBatchRuns.AsNoTracking()
            .Where(b => !b.IsDeleted
                        && b.StartedAt <= job.CreatedAt
                        && b.StatusEmailSentAt != null
                        && b.RecoveryFollowUpEmailSentAt == null)
            .OrderByDescending(b => b.StartedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return batch?.StartedAt;
    }

    public static async Task<bool> WaitForBatchDownloadPhaseCompleteAsync(
        IApplicationDbContext db,
        IReadOnlyList<Guid> sourceIds,
        DateTimeOffset batchStartedAt,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await IsBatchReadyForInitialStatusEmailAsync(db, sourceIds, batchStartedAt, cancellationToken)
                    .ConfigureAwait(false))
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    public sealed record PendingBatchRecoverySource(
        Guid SourceId,
        string SourceName,
        AutoAiRecoveryRunStatus Status,
        Guid FailedDownloadJobId);

    public static async Task<bool> IsRequiredAutoRecoveryCompleteAsync(
        IApplicationDbContext db,
        DownloadJob failedJob,
        CancellationToken cancellationToken)
    {
        if (failedJob.Status != DownloadJobStatus.Failed)
        {
            return true;
        }

        if (!AutoAiRecoveryEligibility.IsJobEligibleForAutoRecovery(failedJob))
        {
            return true;
        }

        var settings = await GetEffectiveAutoRecoverySettingsAsync(db, cancellationToken).ConfigureAwait(false);
        if (!settings.Enabled
            || !AutoAiRecoveryEligibility.ShouldRunForTrigger(failedJob.Trigger, settings))
        {
            return true;
        }

        var source = await db.NewsSources.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == failedJob.NewsSourceId && !s.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        if (source is null || !AutoAiRecoveryEligibility.IsSourceTypeAllowed(source, settings))
        {
            return true;
        }

        var run = await db.AutoAiRecoveryRuns.AsNoTracking()
            .Where(r => !r.IsDeleted && r.FailedDownloadJobId == failedJob.Id)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (run is null)
        {
            var failedAt = failedJob.CompletedAt ?? failedJob.CreatedAt;
            if (DateTimeOffset.UtcNow - failedAt > TimeSpan.FromMinutes(20))
            {
                return true;
            }

            return false;
        }

        return run.CompletedAt is not null;
    }

    private static async Task<bool> HasIncompleteAutoRecoveryRunSinceBatchAsync(
        IApplicationDbContext db,
        Guid sourceId,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        var notBefore = batchStartedAt.AddMinutes(-1);

        return await db.AutoAiRecoveryRuns.AsNoTracking()
            .AnyAsync(
                r => !r.IsDeleted
                     && r.NewsSourceId == sourceId
                     && r.CreatedAt >= notBefore
                     && r.CompletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<AutoAiDownloadRecoveryOptions> GetEffectiveAutoRecoverySettingsAsync(
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var row = await db.AutoAiDownloadRecoverySettings.AsNoTracking()
            .Where(s => !s.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AutoAiDownloadRecoveryOptions
        {
            Enabled = row?.Enabled ?? true,
            RunAfterScheduledFailure = row?.RunAfterScheduledFailure ?? true,
            RunAfterManualFailure = row?.RunAfterManualFailure ?? true
        };
    }

    public static async Task<bool> IsSourceSuccessfulAsync(
        IApplicationDbContext db,
        Guid sourceId,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        if (await HasTodaysDownloadedEditionAsync(db, sourceId, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (await HasSuccessfulPdfEditionSinceBatchAsync(db, sourceId, batchStartedAt, cancellationToken)
                .ConfigureAwait(false))
        {
            return true;
        }

        var latestJob = await GetLatestJobSinceBatchAsync(db, sourceId, batchStartedAt, cancellationToken)
            .ConfigureAwait(false);

        return latestJob is not null && IsSuccessfulDownloadStatus(latestJob.Status);
    }

    public sealed record BatchEditionSatisfactionSnapshot(
        HashSet<Guid> TodaysEditionSourceIds,
        HashSet<Guid> EditionSinceBatchSourceIds)
    {
        public bool HasTodaysEdition(Guid sourceId) => TodaysEditionSourceIds.Contains(sourceId);

        public bool HasEditionSinceBatch(Guid sourceId) => EditionSinceBatchSourceIds.Contains(sourceId);
    }

    public static async Task<BatchEditionSatisfactionSnapshot> LoadBatchEditionSatisfactionAsync(
        IApplicationDbContext db,
        IReadOnlyCollection<Guid> sourceIds,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        if (sourceIds.Count == 0)
        {
            return new BatchEditionSatisfactionSnapshot([], []);
        }

        var editionDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var dayStart = new DateTimeOffset(editionDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);
        var notBefore = batchStartedAt.AddMinutes(-1);

        var todaysEditionSourceIds = await db.PdfEditionDownloads.AsNoTracking()
            .Where(x => !x.IsDeleted
                        && sourceIds.Contains(x.NewsSourceId)
                        && (x.Status == PdfEditionStatus.Downloaded
                            || x.Status == PdfEditionStatus.SkippedDuplicate
                            || x.Status == PdfEditionStatus.Validated)
                        && (x.EditionDate == editionDate
                            || (x.DownloadedAt >= dayStart && x.DownloadedAt < dayEnd)))
            .Select(x => x.NewsSourceId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var succeededJobSourceIds = await db.DownloadJobs.AsNoTracking()
            .Where(j => !j.IsDeleted
                        && sourceIds.Contains(j.NewsSourceId)
                        && (j.Status == DownloadJobStatus.Succeeded
                            || j.Status == DownloadJobStatus.SuccessWithAutoAiRecovery)
                        && j.CompletedAt >= dayStart
                        && j.CompletedAt < dayEnd
                        && db.DownloadedFiles.Any(f => !f.IsDeleted && f.DownloadJobId == j.Id))
            .Select(j => j.NewsSourceId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var editionSinceBatchSourceIds = await db.PdfEditionDownloads.AsNoTracking()
            .Where(x => !x.IsDeleted
                        && sourceIds.Contains(x.NewsSourceId)
                        && x.EditionDate == editionDate
                        && (x.Status == PdfEditionStatus.Downloaded
                            || x.Status == PdfEditionStatus.SkippedDuplicate)
                        && x.CreatedAt >= notBefore)
            .Select(x => x.NewsSourceId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var todays = todaysEditionSourceIds
            .Concat(succeededJobSourceIds)
            .ToHashSet();
        return new BatchEditionSatisfactionSnapshot(todays, editionSinceBatchSourceIds.ToHashSet());
    }

    /// <summary>True when today's edition is already stored (any earlier batch today counts as satisfied).</summary>
    public static async Task<bool> HasTodaysDownloadedEditionAsync(
        IApplicationDbContext db,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        var editionDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var dayStart = new DateTimeOffset(editionDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);

        var hasPdfEdition = await db.PdfEditionDownloads.AsNoTracking()
            .AnyAsync(
                x => !x.IsDeleted
                     && x.NewsSourceId == sourceId
                     && (x.Status == PdfEditionStatus.Downloaded
                         || x.Status == PdfEditionStatus.SkippedDuplicate
                         || x.Status == PdfEditionStatus.Validated)
                     && (x.EditionDate == editionDate
                         || (x.DownloadedAt >= dayStart && x.DownloadedAt < dayEnd)),
                cancellationToken)
            .ConfigureAwait(false);

        if (hasPdfEdition)
        {
            return true;
        }

        return await db.DownloadJobs.AsNoTracking()
            .AnyAsync(
                j => !j.IsDeleted
                     && j.NewsSourceId == sourceId
                     && (j.Status == DownloadJobStatus.Succeeded
                         || j.Status == DownloadJobStatus.SuccessWithAutoAiRecovery)
                     && j.CompletedAt >= dayStart
                     && j.CompletedAt < dayEnd
                     && db.DownloadedFiles.Any(f => !f.IsDeleted && f.DownloadJobId == j.Id),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<bool> HasSuccessfulPdfEditionSinceBatchAsync(
        IApplicationDbContext db,
        Guid sourceId,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        var notBefore = batchStartedAt.AddMinutes(-1);
        var editionDate = DateOnly.FromDateTime(DateTime.UtcNow);

        return await db.PdfEditionDownloads.AsNoTracking()
            .AnyAsync(
                x => !x.IsDeleted
                     && x.NewsSourceId == sourceId
                     && x.EditionDate == editionDate
                     && (x.Status == PdfEditionStatus.Downloaded
                         || x.Status == PdfEditionStatus.SkippedDuplicate)
                     && x.CreatedAt >= notBefore,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<DownloadJob?> GetLatestJobSinceBatchAsync(
        IApplicationDbContext db,
        Guid sourceId,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        var notBefore = batchStartedAt.AddMinutes(-1);

        return await db.DownloadJobs.AsNoTracking()
            .Where(j => !j.IsDeleted
                        && j.NewsSourceId == sourceId
                        && j.CreatedAt >= notBefore)
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<bool> WaitForSourcesSettledAsync(
        IApplicationDbContext db,
        IReadOnlyList<Guid> sourceIds,
        DateTimeOffset batchStartedAt,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var allSettled = true;
            foreach (var sourceId in sourceIds)
            {
                if (!await IsSourceSettledAsync(db, sourceId, batchStartedAt, cancellationToken).ConfigureAwait(false))
                {
                    allSettled = false;
                    break;
                }
            }

            if (allSettled)
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        }

        return false;
    }
}

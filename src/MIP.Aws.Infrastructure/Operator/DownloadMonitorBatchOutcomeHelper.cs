using MIP.Aws.Application.Abstractions;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Infrastructure.Operator;

public static class DownloadMonitorBatchOutcomeHelper
{
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

        var latestJob = await GetLatestJobSinceBatchAsync(db, sourceId, batchStartedAt, cancellationToken)
            .ConfigureAwait(false);

        return latestJob is not null && IsTerminalDownloadStatus(latestJob.Status);
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

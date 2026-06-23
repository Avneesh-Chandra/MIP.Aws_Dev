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
        if (await HasSuccessfulPdfEditionSinceBatchAsync(db, sourceId, batchStartedAt, cancellationToken)
                .ConfigureAwait(false))
        {
            return true;
        }

        var latestJob = await GetLatestJobSinceBatchAsync(db, sourceId, batchStartedAt, cancellationToken)
            .ConfigureAwait(false);

        return latestJob is not null && IsSuccessfulDownloadStatus(latestJob.Status);
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

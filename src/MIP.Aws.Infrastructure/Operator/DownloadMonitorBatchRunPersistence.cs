using MIP.Aws.Application.Abstractions;
using MIP.Aws.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure.Operator;

internal static class DownloadMonitorBatchRunPersistence
{
    internal static async Task PersistAsync(
        IApplicationDbContext db,
        DateTimeOffset startedAt,
        int totalSources,
        string hangfireJobId,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var batchRun = new DownloadMonitorBatchRun
        {
            Id = Guid.NewGuid(),
            StartedAt = startedAt,
            TotalSources = totalSources,
            HangfireJobId = hangfireJobId,
            CreatedAt = startedAt
        };
        db.DownloadMonitorBatchRuns.Add(batchRun);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsBatchRunsSchemaNotReady(ex))
        {
            logger.LogWarning(
                ex,
                "DownloadMonitorBatchRuns schema is not up to date; batch {HangfireJobId} will run but progress metadata was not persisted. Apply pending EF migrations.",
                hangfireJobId);
        }
    }

    internal static async Task TryMarkAbortedAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var abortedAt = DateTimeOffset.UtcNow;
        try
        {
            var updated = await db.DownloadMonitorBatchRuns
                .Where(b => !b.IsDeleted && b.StartedAt == batchStartedAt && b.AbortedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(b => b.AbortedAt, abortedAt),
                    cancellationToken)
                .ConfigureAwait(false);

            if (updated == 0)
            {
                var row = await db.DownloadMonitorBatchRuns
                    .Where(b => !b.IsDeleted && b.StartedAt == batchStartedAt)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (row is not null && row.AbortedAt is null)
                {
                    row.AbortedAt = abortedAt;
                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (IsBatchRunsSchemaNotReady(ex))
        {
            logger.LogDebug(
                ex,
                "Could not persist AbortedAt for batch started at {StartedAt:u}; progress will still treat the batch as stopped.",
                batchStartedAt);
        }
    }

    internal static bool IsBatchRunsSchemaNotReady(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is not SqlException sql)
            {
                continue;
            }

            if (sql.Number == 208
                && sql.Message.Contains("DownloadMonitorBatchRuns", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (sql.Number == 207
                && (sql.Message.Contains("AbortedAt", StringComparison.OrdinalIgnoreCase)
                    || sql.Message.Contains("RecoveryFollowUpEmailSentAt", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }
}

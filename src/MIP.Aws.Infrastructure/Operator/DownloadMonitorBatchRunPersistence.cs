using MIP.Aws.Application.Abstractions;
using MIP.Aws.Domain.Entities;
using Microsoft.Data.SqlClient;
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
        catch (Exception ex) when (IsMissingBatchRunsTable(ex))
        {
            logger.LogWarning(
                ex,
                "DownloadMonitorBatchRuns table is missing; batch {HangfireJobId} will run but progress metadata was not persisted. Apply pending EF migrations.",
                hangfireJobId);
        }
    }

    internal static bool IsMissingBatchRunsTable(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 208 } sql
                && sql.Message.Contains("DownloadMonitorBatchRuns", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

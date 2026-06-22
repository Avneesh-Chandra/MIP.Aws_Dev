using Hangfire;
using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Infrastructure.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure.Operator;

/// <summary>
/// Ensures the download monitor status email is sent once per operator batch even when the
/// Hangfire orchestrator is cancelled (ECS redeploy) or the UI marks the batch complete first.
/// </summary>
internal static class DownloadMonitorBatchStatusEmailCoordinator
{
    public static async Task TryEnqueueCompletedBatchStatusEmailAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        bool isComplete,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!isComplete)
        {
            return;
        }

        if (!await ShouldSendStatusEmailAsync(db, batchStartedAt, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        BackgroundJob.Enqueue<DownloadMonitorScheduledJobs>(
            HangfireQueueOptions.Names.Email,
            j => j.SendCompletedBatchStatusEmailAsync(batchStartedAt));

        logger.LogInformation(
            "Enqueued download monitor status email for completed batch started at {BatchStartedAt:u}.",
            batchStartedAt);
    }

    public static async Task<bool> ShouldSendStatusEmailAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        var batchRun = await db.DownloadMonitorBatchRuns.AsNoTracking()
            .Where(b => !b.IsDeleted && b.StartedAt == batchStartedAt)
            .Select(b => new { b.StatusEmailSentAt })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (batchRun?.StatusEmailSentAt is not null)
        {
            return false;
        }

        var monitorDate = DateOnly.FromDateTime(batchStartedAt.UtcDateTime);
        var subjectPrefix = $"GFH MIP AWS — Download Monitor status ({monitorDate:yyyy-MM-dd})";
        var alreadySent = await db.EmailLogs.AsNoTracking()
            .AnyAsync(
                e => !e.IsDeleted
                     && e.Subject == subjectPrefix
                     && e.SentAt >= batchStartedAt
                     && e.Status == EmailDeliveryStatus.Sent,
                cancellationToken)
            .ConfigureAwait(false);

        return !alreadySent;
    }

    public static async Task MarkStatusEmailSentAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        var sentAt = DateTimeOffset.UtcNow;
        var updated = await db.DownloadMonitorBatchRuns
            .Where(b => !b.IsDeleted && b.StartedAt == batchStartedAt && b.StatusEmailSentAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(b => b.StatusEmailSentAt, sentAt),
                cancellationToken)
            .ConfigureAwait(false);

        if (updated == 0)
        {
            var batchRun = await db.DownloadMonitorBatchRuns
                .Where(b => !b.IsDeleted && b.StartedAt == batchStartedAt)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (batchRun is not null && batchRun.StatusEmailSentAt is null)
            {
                batchRun.StatusEmailSentAt = sentAt;
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

using Hangfire;
using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.NewsSources;
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
        string? hangfireJobId,
        int inProgressCount,
        int waitingCount,
        int autoRecoveryCount,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!isComplete)
        {
            return;
        }

        if (inProgressCount > 0 || waitingCount > 0 || autoRecoveryCount > 0)
        {
            logger.LogDebug(
                "Skipping premature batch status email for {BatchStartedAt:u} ({InProgress} in progress, {Waiting} waiting, {AutoRecovery} in auto recovery).",
                batchStartedAt,
                inProgressCount,
                waitingCount,
                autoRecoveryCount);
            return;
        }

        if (HangfireBatchOrchestratorState.IsBatchOrchestratorJobProcessing(hangfireJobId))
        {
            logger.LogDebug(
                "Skipping batch status email for {BatchStartedAt:u} because Hangfire orchestrator {JobId} is still processing.",
                batchStartedAt,
                hangfireJobId);
            return;
        }

        if (!await AreAllSourcesSettledForBatchAsync(db, batchStartedAt, cancellationToken).ConfigureAwait(false))
        {
            logger.LogDebug(
                "Skipping batch status email for {BatchStartedAt:u} because one or more sources have not reached a terminal state.",
                batchStartedAt);
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

    private static async Task<bool> AreAllSourcesSettledForBatchAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        var sources = await db.NewsSources.AsNoTracking()
            .Where(s => !s.IsDeleted && s.IsEnabled)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var monitoredSourceIds = sources
            .Where(PdfManagementSourceRules.IsPdfDownloadMonitoredSource)
            .Select(s => s.Id)
            .ToList();

        foreach (var sourceId in monitoredSourceIds)
        {
            if (!await DownloadMonitorBatchOutcomeHelper.IsSourceSettledAsync(
                    db,
                    sourceId,
                    batchStartedAt,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                return false;
            }
        }

        return monitoredSourceIds.Count > 0
               && !await HasIncompleteAutoRecoveryForMonitoredSourcesAsync(
                       db,
                       monitoredSourceIds,
                       batchStartedAt,
                       cancellationToken)
                   .ConfigureAwait(false);
    }

    private static async Task<bool> HasIncompleteAutoRecoveryForMonitoredSourcesAsync(
        IApplicationDbContext db,
        IReadOnlyList<Guid> sourceIds,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        var notBefore = batchStartedAt.AddMinutes(-1);

        return await db.AutoAiRecoveryRuns.AsNoTracking()
            .AnyAsync(
                r => !r.IsDeleted
                     && sourceIds.Contains(r.NewsSourceId)
                     && r.CreatedAt >= notBefore
                     && r.CompletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
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

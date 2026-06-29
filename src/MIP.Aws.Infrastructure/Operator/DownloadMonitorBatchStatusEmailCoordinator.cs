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
/// Sends the batch status email when downloads finish (not when auto-recovery finishes),
/// then sends a follow-up email after recovery completes.
/// </summary>
internal static class DownloadMonitorBatchStatusEmailCoordinator
{
    public static async Task TryEnqueueInitialBatchStatusEmailAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        bool isComplete,
        string? hangfireJobId,
        int downloadInProgressCount,
        int waitingCount,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!isComplete)
        {
            return;
        }

        if (downloadInProgressCount > 0 || waitingCount > 0)
        {
            logger.LogDebug(
                "Skipping premature batch status email for {BatchStartedAt:u} ({InProgress} download(s) in progress, {Waiting} waiting).",
                batchStartedAt,
                downloadInProgressCount,
                waitingCount);
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

        if (!await IsBatchReadyForInitialEmailAsync(db, batchStartedAt, cancellationToken).ConfigureAwait(false))
        {
            logger.LogDebug(
                "Skipping batch status email for {BatchStartedAt:u} because one or more sources have not finished downloading.",
                batchStartedAt);
            return;
        }

        if (!await ShouldSendInitialStatusEmailAsync(db, batchStartedAt, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        BackgroundJob.Enqueue<DownloadMonitorScheduledJobs>(
            HangfireQueueOptions.Names.Email,
            j => j.SendInitialBatchStatusEmailAsync(batchStartedAt));

        logger.LogInformation(
            "Enqueued initial download monitor status email for batch started at {BatchStartedAt:u}.",
            batchStartedAt);
    }

    public static async Task TryScheduleRecoveryFollowUpEmailAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!await ShouldSendRecoveryFollowUpEmailAsync(db, batchStartedAt, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var sourceIds = await LoadMonitoredSourceIdsAsync(db, cancellationToken).ConfigureAwait(false);
        if (!await DownloadMonitorBatchOutcomeHelper.HasPendingAutoRecoveryForBatchAsync(
                db,
                sourceIds,
                batchStartedAt,
                cancellationToken)
            .ConfigureAwait(false))
        {
            await MarkRecoveryFollowUpNotRequiredAsync(db, batchStartedAt, cancellationToken).ConfigureAwait(false);
            return;
        }

        BackgroundJob.Schedule<DownloadMonitorScheduledJobs>(
            HangfireQueueOptions.Names.Email,
            j => j.SendBatchRecoveryFollowUpStatusEmailAsync(batchStartedAt, 0),
            DownloadMonitorBatchTiming.DeferredEmailRetryInterval);

        logger.LogInformation(
            "Scheduled auto-recovery follow-up status email for batch started at {BatchStartedAt:u}.",
            batchStartedAt);
    }

    public static async Task TryEnqueueRecoveryFollowUpAfterRunAsync(
        IApplicationDbContext db,
        Guid failedDownloadJobId,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var batchStartedAt = await DownloadMonitorBatchOutcomeHelper.ResolveBatchForRecoveryFollowUpAsync(
                db,
                failedDownloadJobId,
                cancellationToken)
            .ConfigureAwait(false);

        if (batchStartedAt is null)
        {
            return;
        }

        if (!await ShouldSendRecoveryFollowUpEmailAsync(db, batchStartedAt.Value, cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        var sourceIds = await LoadMonitoredSourceIdsAsync(db, cancellationToken).ConfigureAwait(false);
        if (await DownloadMonitorBatchOutcomeHelper.HasPendingAutoRecoveryForBatchAsync(
                db,
                sourceIds,
                batchStartedAt.Value,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return;
        }

        BackgroundJob.Enqueue<DownloadMonitorScheduledJobs>(
            HangfireQueueOptions.Names.Email,
            j => j.SendBatchRecoveryFollowUpStatusEmailAsync(batchStartedAt.Value, 0));

        logger.LogInformation(
            "Enqueued auto-recovery follow-up status email for batch started at {BatchStartedAt:u}.",
            batchStartedAt);
    }

    private static async Task<bool> IsBatchReadyForInitialEmailAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        var sourceIds = await LoadMonitoredSourceIdsAsync(db, cancellationToken).ConfigureAwait(false);
        return await DownloadMonitorBatchOutcomeHelper.IsBatchReadyForInitialStatusEmailAsync(
                db,
                sourceIds,
                batchStartedAt,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<List<Guid>> LoadMonitoredSourceIdsAsync(
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var sources = await db.NewsSources.AsNoTracking()
            .Where(s => !s.IsDeleted && s.IsEnabled)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return sources.Where(PdfManagementSourceRules.IsPdfDownloadMonitoredSource).Select(s => s.Id).ToList();
    }

    public static async Task<bool> ShouldSendInitialStatusEmailAsync(
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
        var subjectPrefix = BuildInitialSubject(monitorDate);
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

    public static async Task<bool> ShouldSendRecoveryFollowUpEmailAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        var batchRun = await db.DownloadMonitorBatchRuns.AsNoTracking()
            .Where(b => !b.IsDeleted && b.StartedAt == batchStartedAt)
            .Select(b => new { b.StatusEmailSentAt, b.RecoveryFollowUpEmailSentAt })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (batchRun?.StatusEmailSentAt is null || batchRun.RecoveryFollowUpEmailSentAt is not null)
        {
            return false;
        }

        var monitorDate = DateOnly.FromDateTime(batchStartedAt.UtcDateTime);
        var subject = BuildRecoveryFollowUpSubject(monitorDate);
        var alreadySent = await db.EmailLogs.AsNoTracking()
            .AnyAsync(
                e => !e.IsDeleted
                     && e.Subject == subject
                     && e.SentAt >= batchStartedAt
                     && e.Status == EmailDeliveryStatus.Sent,
                cancellationToken)
            .ConfigureAwait(false);

        return !alreadySent;
    }

    public static async Task MarkInitialStatusEmailSentAsync(
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

    public static async Task MarkRecoveryFollowUpEmailSentAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        var sentAt = DateTimeOffset.UtcNow;
        var updated = await db.DownloadMonitorBatchRuns
            .Where(b => !b.IsDeleted && b.StartedAt == batchStartedAt && b.RecoveryFollowUpEmailSentAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(b => b.RecoveryFollowUpEmailSentAt, sentAt),
                cancellationToken)
            .ConfigureAwait(false);

        if (updated == 0)
        {
            var batchRun = await db.DownloadMonitorBatchRuns
                .Where(b => !b.IsDeleted && b.StartedAt == batchStartedAt)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (batchRun is not null && batchRun.RecoveryFollowUpEmailSentAt is null)
            {
                batchRun.RecoveryFollowUpEmailSentAt = sentAt;
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task MarkRecoveryFollowUpNotRequiredAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        await MarkRecoveryFollowUpEmailSentAsync(db, batchStartedAt, cancellationToken).ConfigureAwait(false);
    }

    public static string BuildInitialSubject(DateOnly monitorDate) =>
        $"GFH MIP AWS — Download Monitor status ({monitorDate:yyyy-MM-dd})";

    public static string BuildRecoveryFollowUpSubject(DateOnly monitorDate) =>
        $"GFH MIP AWS — Download Monitor recovery update ({monitorDate:yyyy-MM-dd})";
}

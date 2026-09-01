using Hangfire;
using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Reporting;
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
    public static async Task TryEnqueueInitialStatusEmailForBatchAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!await ShouldSendInitialStatusEmailAsync(db, batchStartedAt, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        if (!await DownloadMonitorStatusEmailGuard.TryMarkInitialStatusEmailEnqueuedAsync(
                db,
                batchStartedAt,
                cancellationToken)
            .ConfigureAwait(false))
        {
            logger.LogInformation(
                "Skipped enqueueing initial download monitor status email for batch {BatchStartedAt:u}; already enqueued or sent today.",
                batchStartedAt);
            return;
        }

        try
        {
            BackgroundJob.Enqueue<DownloadMonitorScheduledJobs>(
                HangfireQueueOptions.Names.Email,
                j => j.SendInitialBatchStatusEmailAsync(batchStartedAt));

            logger.LogInformation(
                "Enqueued initial download monitor status email for batch started at {BatchStartedAt:u}.",
                batchStartedAt);
        }
        catch
        {
            await DownloadMonitorStatusEmailGuard.ReleaseInitialStatusEmailClaimAsync(
                    db,
                    batchStartedAt,
                    cancellationToken)
                .ConfigureAwait(false);
            throw;
        }
    }

    public static async Task TryScheduleDeferredInitialStatusEmailAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!await ShouldSendInitialStatusEmailAsync(db, batchStartedAt, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        if (!await DownloadMonitorStatusEmailGuard.TryMarkInitialStatusEmailEnqueuedAsync(
                db,
                batchStartedAt,
                cancellationToken)
            .ConfigureAwait(false))
        {
            logger.LogInformation(
                "Skipped scheduling deferred initial status email for batch {BatchStartedAt:u}; already enqueued or sent today.",
                batchStartedAt);
            return;
        }

        try
        {
            BackgroundJob.Schedule<DownloadMonitorScheduledJobs>(
                HangfireQueueOptions.Names.Email,
                j => j.SendInitialBatchStatusEmailWhenReadyAsync(batchStartedAt, 0),
                DownloadMonitorBatchTiming.DeferredEmailRetryInterval);

            logger.LogInformation(
                "Scheduled deferred initial download monitor status email for batch started at {BatchStartedAt:u}.",
                batchStartedAt);
        }
        catch
        {
            await DownloadMonitorStatusEmailGuard.ReleaseInitialStatusEmailClaimAsync(
                    db,
                    batchStartedAt,
                    cancellationToken)
                .ConfigureAwait(false);
            throw;
        }
    }

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

        await TryEnqueueInitialStatusEmailForBatchAsync(db, batchStartedAt, logger, cancellationToken)
            .ConfigureAwait(false);
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
        if (await DownloadMonitorBatchOutcomeHelper.HasPendingAutoRecoveryForBatchAsync(
                db,
                sourceIds,
                batchStartedAt,
                cancellationToken)
            .ConfigureAwait(false))
        {
            BackgroundJob.Schedule<DownloadMonitorScheduledJobs>(
                HangfireQueueOptions.Names.Email,
                j => j.SendBatchRecoveryFollowUpStatusEmailAsync(batchStartedAt, 0),
                DownloadMonitorBatchTiming.DeferredEmailRetryInterval);

            logger.LogInformation(
                "Scheduled auto-recovery follow-up status email for batch started at {BatchStartedAt:u}.",
                batchStartedAt);
            return;
        }

        if (await DownloadMonitorBatchOutcomeHelper.HasCompletedAutoRecoveryRunsForBatchAsync(
                db,
                sourceIds,
                batchStartedAt,
                cancellationToken)
            .ConfigureAwait(false))
        {
            BackgroundJob.Enqueue<DownloadMonitorScheduledJobs>(
                HangfireQueueOptions.Names.Email,
                j => j.SendBatchRecoveryFollowUpStatusEmailAsync(batchStartedAt, 0));

            logger.LogInformation(
                "Enqueued auto-recovery follow-up status email for batch started at {BatchStartedAt:u} (recovery already completed).",
                batchStartedAt);
            return;
        }

        await MarkRecoveryFollowUpNotRequiredAsync(db, batchStartedAt, cancellationToken).ConfigureAwait(false);
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
        var monitorDate = DateOnly.FromDateTime(batchStartedAt.UtcDateTime);
        if (await DownloadMonitorStatusEmailGuard.HasConfirmedInitialStatusForMonitorDateAsync(
                db,
                monitorDate,
                cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (await DownloadMonitorStatusEmailGuard.HasActiveInitialStatusClaimForMonitorDateAsync(
                db,
                monitorDate,
                cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        return true;
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

        if (batchRun?.StatusEmailSentAt is null)
        {
            return false;
        }

        var monitorDate = DateOnly.FromDateTime(batchStartedAt.UtcDateTime);
        var subject = BuildRecoveryFollowUpSubject(monitorDate);
        if (await DownloadMonitorStatusEmailGuard.HasConfirmedDeliveryAsync(
                db,
                subject,
                batchStartedAt,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return false;
        }

        if (batchRun.RecoveryFollowUpEmailSentAt is DateTimeOffset followUpAt
            && !DownloadMonitorStatusEmailGuard.IsRecoveryFollowUpEnqueueClaim(followUpAt, batchStartedAt))
        {
            return false;
        }

        return true;
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

    public static string BuildManualCompletionSubject(DateOnly monitorDate) =>
        $"GFH MIP AWS — Download Monitor manual completion update ({monitorDate:yyyy-MM-dd})";

    public static void EnqueuePdfDownloadMail(ILogger logger)
    {
        try
        {
            BackgroundJob.Enqueue<DownloadMonitorScheduledJobs>(
                HangfireQueueOptions.Names.Email,
                job => job.SendPdfDownloadMailAsync());
            logger.LogInformation("Enqueued PDF Download mail eligibility check after batch activity.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not enqueue PDF Download mail eligibility check.");
        }
    }

    public static async Task TryEnqueueAfterManualUploadAsync(
        IApplicationDbContext db,
        IMailSettingsService mailSettings,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dayStart = new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);
        var batchStartedAt = await db.DownloadMonitorBatchRuns.AsNoTracking()
            .Where(batch => !batch.IsDeleted && batch.StartedAt >= dayStart && batch.StartedAt < dayEnd)
            .OrderByDescending(batch => batch.StartedAt)
            .Select(batch => new
            {
                batch.StartedAt
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (batchStartedAt is null)
        {
            logger.LogInformation("Manual PDF upload completed without a today's batch record; email triggers were skipped.");
            return;
        }

        var sourceIds = await LoadMonitoredSourceIdsAsync(db, cancellationToken).ConfigureAwait(false);
        if (sourceIds.Count == 0)
        {
            return;
        }

        var effective = await mailSettings.GetEffectiveSchedulerAsync(cancellationToken).ConfigureAwait(false);
        var initialStatusSent = await DownloadMonitorStatusEmailGuard.HasConfirmedInitialStatusForMonitorDateAsync(
                db,
                DateOnly.FromDateTime(batchStartedAt.StartedAt.UtcDateTime),
                cancellationToken)
            .ConfigureAwait(false);
        var statusReady = effective.StatusEmailEnabled
            && initialStatusSent
            && await DownloadMonitorBatchOutcomeHelper.IsBatchReadyForConfiguredMailAsync(
                db,
                sourceIds,
                batchStartedAt.StartedAt,
                effective.StatusEmailCompletionMode,
                cancellationToken).ConfigureAwait(false);
        var pdfReady = effective.PdfDownloadMailEnabled
            && await DownloadMonitorBatchOutcomeHelper.IsBatchReadyForConfiguredMailAsync(
                db,
                sourceIds,
                batchStartedAt.StartedAt,
                effective.PdfDownloadMailCompletionMode,
                cancellationToken).ConfigureAwait(false);

        if (statusReady)
        {
            if (await TryClaimManualCompletionStatusEmailAsync(db, batchStartedAt.StartedAt, cancellationToken)
                    .ConfigureAwait(false))
            {
                try
                {
                    BackgroundJob.Enqueue<DownloadMonitorScheduledJobs>(
                        HangfireQueueOptions.Names.Email,
                        job => job.SendManualCompletionStatusEmailAsync(batchStartedAt.StartedAt));
                    logger.LogInformation(
                        "Enqueued manual-upload completion status email for batch {BatchStartedAt:u}.",
                        batchStartedAt.StartedAt);
                }
                catch
                {
                    await ReleaseManualCompletionStatusEmailClaimAsync(db, batchStartedAt.StartedAt, cancellationToken)
                        .ConfigureAwait(false);
                    throw;
                }
            }
        }

        if (pdfReady)
        {
            EnqueuePdfDownloadMail(logger);
        }
    }

    public static async Task<bool> TryClaimManualCompletionStatusEmailAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken)
    {
        var subject = BuildManualCompletionSubject(DateOnly.FromDateTime(batchStartedAt.UtcDateTime));
        if (await DownloadMonitorStatusEmailGuard.HasConfirmedDeliveryAsync(
                db,
                subject,
                batchStartedAt,
                cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var updated = await db.DownloadMonitorBatchRuns
            .Where(batch => !batch.IsDeleted
                            && batch.StartedAt == batchStartedAt
                            && batch.ManualCompletionStatusEmailSentAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    batch => batch.ManualCompletionStatusEmailSentAt,
                    batchStartedAt),
                cancellationToken)
            .ConfigureAwait(false);
        return updated > 0;
    }

    public static Task ReleaseManualCompletionStatusEmailClaimAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken) =>
        db.DownloadMonitorBatchRuns
            .Where(batch => !batch.IsDeleted
                            && batch.StartedAt == batchStartedAt
                            && batch.ManualCompletionStatusEmailSentAt == batchStartedAt)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    batch => batch.ManualCompletionStatusEmailSentAt,
                    (DateTimeOffset?)null),
                cancellationToken);

    public static Task MarkManualCompletionStatusEmailSentAsync(
        IApplicationDbContext db,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken) =>
        db.DownloadMonitorBatchRuns
            .Where(batch => !batch.IsDeleted
                            && batch.StartedAt == batchStartedAt
                            && batch.ManualCompletionStatusEmailSentAt == batchStartedAt)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    batch => batch.ManualCompletionStatusEmailSentAt,
                    DateTimeOffset.UtcNow),
                cancellationToken);

}

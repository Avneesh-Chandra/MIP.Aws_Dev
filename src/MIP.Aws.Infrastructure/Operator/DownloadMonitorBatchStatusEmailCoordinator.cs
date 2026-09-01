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
/// Enqueues exactly one status email and one PDF download email per batch when configured.
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
        if (await DownloadMonitorStatusEmailGuard.HasConfirmedInitialStatusForBatchAsync(
                db,
                batchStartedAt,
                cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (await DownloadMonitorStatusEmailGuard.HasActiveInitialStatusClaimForBatchAsync(
                db,
                batchStartedAt,
                cancellationToken).ConfigureAwait(false))
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

    public static string BuildInitialSubject(DateOnly monitorDate) =>
        $"GFH MIP AWS — Download Monitor status ({monitorDate:yyyy-MM-dd})";

    public static string BuildRecoveryFollowUpSubject(DateOnly monitorDate) =>
        $"GFH MIP AWS — Download Monitor recovery update ({monitorDate:yyyy-MM-dd})";

    public static string BuildManualCompletionSubject(DateOnly monitorDate) =>
        $"GFH MIP AWS — Download Monitor manual completion update ({monitorDate:yyyy-MM-dd})";

    public static void EnqueuePdfDownloadMail(ILogger logger, DateTimeOffset batchStartedAt)
    {
        try
        {
            BackgroundJob.Enqueue<DownloadMonitorScheduledJobs>(
                HangfireQueueOptions.Names.Email,
                job => job.SendPdfDownloadMailForBatchAsync(batchStartedAt));
            logger.LogInformation(
                "Enqueued PDF Download mail for batch started at {BatchStartedAt:u}.",
                batchStartedAt);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Could not enqueue PDF Download mail for batch started at {BatchStartedAt:u}.",
                batchStartedAt);
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
        var pdfReady = effective.PdfDownloadMailEnabled
            && await DownloadMonitorBatchOutcomeHelper.IsBatchReadyForConfiguredMailAsync(
                db,
                sourceIds,
                batchStartedAt.StartedAt,
                effective.PdfDownloadMailCompletionMode,
                cancellationToken).ConfigureAwait(false);

        if (pdfReady)
        {
            EnqueuePdfDownloadMail(logger, batchStartedAt.StartedAt);
        }
    }

}

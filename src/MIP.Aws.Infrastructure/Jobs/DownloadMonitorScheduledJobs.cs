using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Operator;
using MIP.Aws.Infrastructure.Operator;
using MIP.Aws.Application.Abstractions.Reporting;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using Hangfire;
using Hangfire.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Jobs;

/// <summary>
/// Staggered daily downloads for download-monitor sources and the post-run status email.
/// </summary>
public sealed class DownloadMonitorScheduledJobs(
    IServiceScopeFactory scopeFactory,
    IOptions<PdfEditionSchedulerOptions> schedulerOptions,
    ILogger<DownloadMonitorScheduledJobs> logger)
{
    [Queue(HangfireQueueOptions.Names.Critical)]
    [DisableConcurrentExecution(timeoutInSeconds: 45 * 60)]
    [AutomaticRetry(Attempts = 1, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task ScheduleStaggeredDailyDownloadsAsync(PerformContext? context)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var opt = schedulerOptions.Value;
        var interval = Math.Clamp(opt.StaggerIntervalMinutes, 0, 60);
        var batchStartedAt = DateTimeOffset.UtcNow;
        var hangfireJobId = context?.BackgroundJob?.Id ?? string.Empty;

        var sources = (await db.NewsSources.AsNoTracking()
                .Where(s => !s.IsDeleted && s.IsEnabled)
                .OrderBy(s => s.Name)
                .ToListAsync(CancellationToken.None)
                .ConfigureAwait(false))
            .Where(PdfManagementSourceRules.IsPdfDownloadMonitoredSource)
            .ToList();

        if (sources.Count == 0)
        {
            logger.LogWarning("Daily download monitor batch skipped: no enabled monitored sources found.");
            return;
        }

        await DownloadMonitorBatchRunPersistence.PersistAsync(
                db,
                batchStartedAt,
                sources.Count,
                hangfireJobId,
                logger,
                CancellationToken.None)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Scheduled daily download monitor batch {HangfireJobId} for {Count} source(s) at {StartedAt:u} ({Interval} minute stagger).",
            hangfireJobId,
            sources.Count,
            batchStartedAt,
            interval);

        await ScheduleBatchDownloadsAsync(db, sources, interval, CancellationToken.None).ConfigureAwait(false);

        await FinishBatchAndSendStatusEmailAsync(
                db,
                sources,
                batchStartedAt,
                interval,
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    private async Task FinishBatchAndSendStatusEmailAsync(
        IApplicationDbContext db,
        IReadOnlyList<NewsSource> sources,
        DateTimeOffset batchStartedAt,
        int interval,
        CancellationToken cancellationToken)
    {
        var opt = schedulerOptions.Value;
        var waitTimeout = DownloadMonitorBatchTiming.ResolveOrchestratorWaitTimeout(opt, sources.Count);
        var sourceIds = sources.Select(s => s.Id).ToList();
        var downloadsComplete = await DownloadMonitorBatchOutcomeHelper.WaitForBatchDownloadPhaseCompleteAsync(
                db,
                sourceIds,
                batchStartedAt,
                waitTimeout,
                cancellationToken)
            .ConfigureAwait(false);

        var successCount = 0;
        foreach (var source in sources)
        {
            if (await DownloadMonitorBatchOutcomeHelper.IsSourceSuccessfulAsync(
                    db,
                    source.Id,
                    batchStartedAt,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                successCount++;
            }
        }

        logger.LogInformation(
            "Download monitor batch finished waiting for {Count} source(s): {SuccessCount} succeeded (downloads complete={DownloadsComplete}).",
            sources.Count,
            successCount,
            downloadsComplete);

        if (!opt.StatusEmailEnabled)
        {
            return;
        }

        if (downloadsComplete
            && await DownloadMonitorBatchOutcomeHelper.IsBatchReadyForInitialStatusEmailAsync(
                    db,
                    sourceIds,
                    batchStartedAt,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            await SendInitialBatchStatusEmailAsync(batchStartedAt).ConfigureAwait(false);
            return;
        }

        logger.LogWarning(
            "Download monitor batch started at {BatchStartedAt:u} timed out before all downloads finished; deferring initial status email.",
            batchStartedAt);
        BackgroundJob.Schedule<DownloadMonitorScheduledJobs>(
            HangfireQueueOptions.Names.Email,
            j => j.SendInitialBatchStatusEmailWhenReadyAsync(batchStartedAt, 0),
            DownloadMonitorBatchTiming.DeferredEmailRetryInterval);
    }

    [Queue(HangfireQueueOptions.Names.Email)]
    [AutomaticRetry(Attempts = 2, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task SendDebouncedManualRecoveryStatusEmailAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IDownloadMonitorDailyStatusEmailService>();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        if (await DownloadMonitorStatusEmailGuard.ShouldThrottleAdHocDailyStatusEmailAsync(
                db,
                date,
                CancellationToken.None)
            .ConfigureAwait(false))
        {
            logger.LogInformation(
                "Skipped debounced manual-recovery status email for {Date}: a status email was sent recently.",
                date);
            return;
        }

        await email.SendDailyStatusEmailAsync(date, CancellationToken.None).ConfigureAwait(false);
    }

    [Queue(HangfireQueueOptions.Names.Email)]
    [AutomaticRetry(Attempts = 2, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task SendDailyStatusEmailAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var email = scope.ServiceProvider.GetRequiredService<IDownloadMonitorDailyStatusEmailService>();
        await email.SendDailyStatusEmailAsync(null, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Operator "Execute PDF download task": stagger all monitored sources, wait for completion, send status email.
    /// </summary>
    [Queue(HangfireQueueOptions.Names.Critical)]
    [DisableConcurrentExecution(timeoutInSeconds: 45 * 60)]
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [120, 300], OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task ExecuteOperatorPdfBatchAsync(DateTimeOffset batchStartedAt)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var interval = Math.Clamp(schedulerOptions.Value.StaggerIntervalMinutes, 0, 60);

        var sources = await LoadMonitoredSourcesAsync(db, CancellationToken.None).ConfigureAwait(false);
        if (sources.Count == 0)
        {
            logger.LogWarning("Operator PDF batch skipped: no enabled monitored sources found.");
            return;
        }

        logger.LogInformation(
            "Operator PDF batch scheduling {Count} monitored source download(s) (batch started {BatchStartedAt:u}).",
            sources.Count,
            batchStartedAt);

        await ScheduleBatchDownloadsAsync(db, sources, interval, CancellationToken.None).ConfigureAwait(false);

        await FinishBatchAndSendStatusEmailAsync(
                db,
                sources,
                batchStartedAt,
                interval,
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    [Queue(HangfireQueueOptions.Names.Email)]
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [60, 180], OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task SendInitialBatchStatusEmailAsync(DateTimeOffset batchStartedAt)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IDownloadMonitorDailyStatusEmailService>();

        await DownloadMonitorStatusEmailGuard.ReleaseStaleInitialClaimIfNeededAsync(
                db,
                batchStartedAt,
                CancellationToken.None)
            .ConfigureAwait(false);

        var sources = await LoadMonitoredSourcesAsync(db, CancellationToken.None).ConfigureAwait(false);
        if (sources.Count > 0)
        {
            var sourceIds = sources.Select(s => s.Id).ToList();
            if (!await DownloadMonitorBatchOutcomeHelper.IsBatchReadyForInitialStatusEmailAsync(
                    db,
                    sourceIds,
                    batchStartedAt,
                    CancellationToken.None)
                .ConfigureAwait(false))
            {
                logger.LogInformation(
                    "Download monitor batch {BatchStartedAt:u} not ready for initial status email (downloads still in progress).",
                    batchStartedAt);
                BackgroundJob.Schedule<DownloadMonitorScheduledJobs>(
                    HangfireQueueOptions.Names.Email,
                    j => j.SendInitialBatchStatusEmailWhenReadyAsync(batchStartedAt, 0),
                    DownloadMonitorBatchTiming.DeferredEmailRetryInterval);
                return;
            }
        }

        if (!await DownloadMonitorBatchStatusEmailCoordinator.ShouldSendInitialStatusEmailAsync(
                db,
                batchStartedAt,
                CancellationToken.None)
            .ConfigureAwait(false))
        {
            logger.LogInformation(
                "Download monitor initial status email already sent for batch started at {BatchStartedAt:u}; skipping.",
                batchStartedAt);
            return;
        }

        if (!await DownloadMonitorStatusEmailGuard.TryClaimInitialStatusEmailAsync(
                db,
                batchStartedAt,
                CancellationToken.None)
            .ConfigureAwait(false))
        {
            logger.LogInformation(
                "Download monitor initial status email already claimed for batch started at {BatchStartedAt:u}; scheduling retry.",
                batchStartedAt);
            BackgroundJob.Schedule<DownloadMonitorScheduledJobs>(
                HangfireQueueOptions.Names.Email,
                j => j.SendInitialBatchStatusEmailAsync(batchStartedAt),
                TimeSpan.FromMinutes(2));
            return;
        }

        try
        {
            var sent = await email.SendInitialBatchStatusEmailAsync(batchStartedAt, CancellationToken.None)
                .ConfigureAwait(false);
            if (sent)
            {
                await DownloadMonitorBatchStatusEmailCoordinator.MarkInitialStatusEmailSentAsync(
                        db,
                        batchStartedAt,
                        CancellationToken.None)
                    .ConfigureAwait(false);
                await DownloadMonitorBatchStatusEmailCoordinator.TryScheduleRecoveryFollowUpEmailAsync(
                        db,
                        batchStartedAt,
                        logger,
                        CancellationToken.None)
                    .ConfigureAwait(false);
                logger.LogInformation(
                    "Download monitor initial status email sent for batch started at {BatchStartedAt:u}.",
                    batchStartedAt);
            }
            else
            {
                await DownloadMonitorStatusEmailGuard.ReleaseInitialStatusEmailClaimAsync(
                        db,
                        batchStartedAt,
                        CancellationToken.None)
                    .ConfigureAwait(false);
                logger.LogWarning(
                    "Download monitor initial status email was not delivered for batch started at {BatchStartedAt:u}; will retry.",
                    batchStartedAt);
                throw new InvalidOperationException("Download monitor initial status email was not delivered.");
            }
        }
        catch (Exception ex)
        {
            await DownloadMonitorStatusEmailGuard.ReleaseInitialStatusEmailClaimAsync(
                    db,
                    batchStartedAt,
                    CancellationToken.None)
                .ConfigureAwait(false);
            logger.LogError(
                ex,
                "Download monitor initial status email failed for batch started at {BatchStartedAt:u}.",
                batchStartedAt);
            throw;
        }
    }

    [Queue(HangfireQueueOptions.Names.Email)]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [300, 600, 900], OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task SendInitialBatchStatusEmailWhenReadyAsync(DateTimeOffset batchStartedAt, int attempt)
    {
        if (DateTimeOffset.UtcNow - batchStartedAt > DownloadMonitorBatchTiming.MaxStatusEmailWaitLifecycle)
        {
            logger.LogError(
                "Download monitor batch {BatchStartedAt:u} exceeded max initial status-email wait; email not sent because downloads did not finish.",
                batchStartedAt);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var sources = await LoadMonitoredSourcesAsync(db, CancellationToken.None).ConfigureAwait(false);
        if (sources.Count == 0)
        {
            return;
        }

        var sourceIds = sources.Select(s => s.Id).ToList();
        var remaining = DownloadMonitorBatchTiming.MaxStatusEmailWaitLifecycle - (DateTimeOffset.UtcNow - batchStartedAt);
        var waitBudget = remaining < DownloadMonitorBatchTiming.DeferredEmailRetryInterval * 2
            ? remaining
            : TimeSpan.FromMinutes(5);

        var downloadsComplete = await DownloadMonitorBatchOutcomeHelper.WaitForBatchDownloadPhaseCompleteAsync(
                db,
                sourceIds,
                batchStartedAt,
                waitBudget,
                CancellationToken.None)
            .ConfigureAwait(false);

        if (downloadsComplete
            && await DownloadMonitorBatchOutcomeHelper.IsBatchReadyForInitialStatusEmailAsync(
                    db,
                    sourceIds,
                    batchStartedAt,
                    CancellationToken.None)
                .ConfigureAwait(false))
        {
            await SendInitialBatchStatusEmailAsync(batchStartedAt).ConfigureAwait(false);
            return;
        }

        logger.LogInformation(
            "Download monitor batch {BatchStartedAt:u} waiting for downloads to finish; deferring initial email (attempt {Attempt}).",
            batchStartedAt,
            attempt + 1);

        BackgroundJob.Schedule<DownloadMonitorScheduledJobs>(
            HangfireQueueOptions.Names.Email,
            j => j.SendInitialBatchStatusEmailWhenReadyAsync(batchStartedAt, attempt + 1),
            DownloadMonitorBatchTiming.DeferredEmailRetryInterval);
    }

    [Queue(HangfireQueueOptions.Names.Email)]
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [60, 180], OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task SendBatchRecoveryFollowUpStatusEmailAsync(DateTimeOffset batchStartedAt, int attempt)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IDownloadMonitorDailyStatusEmailService>();

        await DownloadMonitorStatusEmailGuard.ReleaseStaleRecoveryFollowUpClaimIfNeededAsync(
                db,
                batchStartedAt,
                CancellationToken.None)
            .ConfigureAwait(false);

        if (!await DownloadMonitorBatchStatusEmailCoordinator.ShouldSendRecoveryFollowUpEmailAsync(
                db,
                batchStartedAt,
                CancellationToken.None)
            .ConfigureAwait(false))
        {
            return;
        }

        var sourceIds = (await LoadMonitoredSourcesAsync(db, CancellationToken.None).ConfigureAwait(false))
            .Select(s => s.Id)
            .ToList();

        if (await DownloadMonitorBatchOutcomeHelper.HasPendingAutoRecoveryForBatchAsync(
                db,
                sourceIds,
                batchStartedAt,
                CancellationToken.None)
            .ConfigureAwait(false))
        {
            if (attempt + 1 >= DownloadMonitorBatchTiming.MaxDeferredEmailAttempts)
            {
                logger.LogWarning(
                    "Download monitor batch {BatchStartedAt:u} recovery follow-up attempt cap reached; continuing to wait.",
                    batchStartedAt);
            }

            BackgroundJob.Schedule<DownloadMonitorScheduledJobs>(
                HangfireQueueOptions.Names.Email,
                j => j.SendBatchRecoveryFollowUpStatusEmailAsync(batchStartedAt, attempt + 1),
                DownloadMonitorBatchTiming.DeferredEmailRetryInterval);
            return;
        }

        if (!await DownloadMonitorStatusEmailGuard.TryClaimRecoveryFollowUpEmailAsync(
                db,
                batchStartedAt,
                CancellationToken.None)
            .ConfigureAwait(false))
        {
            logger.LogInformation(
                "Download monitor recovery follow-up email already claimed for batch started at {BatchStartedAt:u}; scheduling retry.",
                batchStartedAt);
            BackgroundJob.Schedule<DownloadMonitorScheduledJobs>(
                HangfireQueueOptions.Names.Email,
                j => j.SendBatchRecoveryFollowUpStatusEmailAsync(batchStartedAt, attempt),
                TimeSpan.FromMinutes(2));
            return;
        }

        try
        {
            var sent = await email.SendRecoveryFollowUpEmailAsync(batchStartedAt, CancellationToken.None)
                .ConfigureAwait(false);
            if (sent)
            {
                await DownloadMonitorBatchStatusEmailCoordinator.MarkRecoveryFollowUpEmailSentAsync(
                        db,
                        batchStartedAt,
                        CancellationToken.None)
                    .ConfigureAwait(false);
                logger.LogInformation(
                    "Download monitor recovery follow-up email sent for batch started at {BatchStartedAt:u}.",
                    batchStartedAt);
            }
            else
            {
                await DownloadMonitorStatusEmailGuard.ReleaseRecoveryFollowUpEmailClaimAsync(
                        db,
                        batchStartedAt,
                        CancellationToken.None)
                    .ConfigureAwait(false);
                throw new InvalidOperationException("Download monitor recovery follow-up email was not delivered.");
            }
        }
        catch (Exception ex)
        {
            await DownloadMonitorStatusEmailGuard.ReleaseRecoveryFollowUpEmailClaimAsync(
                    db,
                    batchStartedAt,
                    CancellationToken.None)
                .ConfigureAwait(false);
            logger.LogError(
                ex,
                "Download monitor recovery follow-up email failed for batch started at {BatchStartedAt:u}.",
                batchStartedAt);
            throw;
        }
    }

    [Obsolete("Use SendInitialBatchStatusEmailAsync.")]
    public Task SendCompletedBatchStatusEmailAsync(DateTimeOffset batchStartedAt) =>
        SendInitialBatchStatusEmailAsync(batchStartedAt);

    [Obsolete("Use SendInitialBatchStatusEmailWhenReadyAsync.")]
    public Task SendCompletedBatchStatusEmailWhenReadyAsync(DateTimeOffset batchStartedAt, int attempt) =>
        SendInitialBatchStatusEmailWhenReadyAsync(batchStartedAt, attempt);

    private async Task ScheduleBatchDownloadsAsync(
        IApplicationDbContext db,
        IReadOnlyList<NewsSource> sources,
        int interval,
        CancellationToken cancellationToken)
    {
        var scheduled = 0;
        foreach (var source in sources)
        {
            if (await DownloadMonitorBatchOutcomeHelper.HasTodaysDownloadedEditionAsync(
                    db,
                    source.Id,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                logger.LogInformation(
                    "Skipping download schedule for {Source}: today's edition is already stored.",
                    source.Name);
                continue;
            }

            var delay = TimeSpan.FromMinutes(scheduled * interval);
            ScheduleSourceDownload(source, delay);
            scheduled++;
        }

        if (scheduled == 0)
        {
            logger.LogInformation(
                "All {Count} monitored source(s) already have today's edition; batch will finalize and send status email.",
                sources.Count);
        }
    }

    private void ScheduleSourceDownload(NewsSource source, TimeSpan delay)
    {
        if (source.PdfDiscoveryEnabled
            && source.SourceType is NewsSourceType.PublicHtml or NewsSourceType.PublicPdf)
        {
            if (delay <= TimeSpan.Zero)
            {
                BackgroundJob.Enqueue<PdfEditionJobs>(j => j.DiscoverAndDownloadTodayPdfAsync(source.Id));
                logger.LogInformation("Enqueued immediate PDF edition download for {Source}.", source.Name);
            }
            else
            {
                BackgroundJob.Schedule<PdfEditionJobs>(
                    j => j.DiscoverAndDownloadTodayPdfAsync(source.Id),
                    delay);
                logger.LogInformation(
                    "Scheduled PDF edition download for {Source} in {Delay} (at ~{RunAt:u}).",
                    source.Name,
                    delay,
                    DateTimeOffset.UtcNow.Add(delay));
            }

            return;
        }

        if (source.SourceType == NewsSourceType.WebPortalLogin)
        {
            if (delay <= TimeSpan.Zero)
            {
                BackgroundJob.Enqueue<NewsIngestionJobs>(j => j.DownloadSourceAsync(source.Id));
                logger.LogInformation("Enqueued immediate portal download for {Source}.", source.Name);
            }
            else
            {
                BackgroundJob.Schedule<NewsIngestionJobs>(
                    j => j.DownloadSourceAsync(source.Id),
                    delay);
                logger.LogInformation(
                    "Scheduled portal download for {Source} in {Delay} (at ~{RunAt:u}).",
                    source.Name,
                    delay,
                    DateTimeOffset.UtcNow.Add(delay));
            }
        }
    }

    private static async Task<List<NewsSource>> LoadMonitoredSourcesAsync(
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var all = await db.NewsSources.AsNoTracking()
            .Where(s => !s.IsDeleted && s.IsEnabled)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return all.Where(PdfManagementSourceRules.IsPdfDownloadMonitoredSource).ToList();
    }
}

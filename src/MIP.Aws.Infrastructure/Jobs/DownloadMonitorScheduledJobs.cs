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
        var allSettled = await DownloadMonitorBatchOutcomeHelper.WaitForSourcesSettledAsync(
                db,
                sources.Select(s => s.Id).ToList(),
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
            "Download monitor batch finished waiting for {Count} source(s): {SuccessCount} succeeded (all settled={AllSettled}).",
            sources.Count,
            successCount,
            allSettled);

        if (!opt.StatusEmailEnabled)
        {
            return;
        }

        if (allSettled
            && await DownloadMonitorBatchOutcomeHelper.IsBatchReadyForStatusEmailAsync(
                    db,
                    sources.Select(s => s.Id).ToList(),
                    batchStartedAt,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            await SendCompletedBatchStatusEmailAsync(batchStartedAt).ConfigureAwait(false);
            return;
        }

        logger.LogWarning(
            "Download monitor batch started at {BatchStartedAt:u} timed out before all sources and auto recovery finished; deferring status email until final.",
            batchStartedAt);
        BackgroundJob.Schedule<DownloadMonitorScheduledJobs>(
            HangfireQueueOptions.Names.Email,
            j => j.SendCompletedBatchStatusEmailWhenReadyAsync(batchStartedAt, 0),
            DownloadMonitorBatchTiming.DeferredEmailRetryInterval);
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
    public async Task SendCompletedBatchStatusEmailAsync(DateTimeOffset batchStartedAt)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IDownloadMonitorDailyStatusEmailService>();

        var sources = await LoadMonitoredSourcesAsync(db, CancellationToken.None).ConfigureAwait(false);
        if (sources.Count > 0)
        {
            var sourceIds = sources.Select(s => s.Id).ToList();
            if (!await DownloadMonitorBatchOutcomeHelper.IsBatchReadyForStatusEmailAsync(
                    db,
                    sourceIds,
                    batchStartedAt,
                    CancellationToken.None)
                .ConfigureAwait(false))
            {
                logger.LogInformation(
                    "Download monitor batch {BatchStartedAt:u} not ready for status email (downloads or auto recovery still in progress).",
                    batchStartedAt);
                BackgroundJob.Schedule<DownloadMonitorScheduledJobs>(
                    HangfireQueueOptions.Names.Email,
                    j => j.SendCompletedBatchStatusEmailWhenReadyAsync(batchStartedAt, 0),
                    DownloadMonitorBatchTiming.DeferredEmailRetryInterval);
                return;
            }
        }

        if (!await DownloadMonitorBatchStatusEmailCoordinator.ShouldSendStatusEmailAsync(
                db,
                batchStartedAt,
                CancellationToken.None)
            .ConfigureAwait(false))
        {
            logger.LogInformation(
                "Download monitor status email already sent for batch started at {BatchStartedAt:u}; skipping.",
                batchStartedAt);
            return;
        }

        var monitorDate = DateOnly.FromDateTime(batchStartedAt.UtcDateTime);

        try
        {
            var sent = await email.SendDailyStatusEmailAsync(monitorDate, CancellationToken.None).ConfigureAwait(false);
            if (sent)
            {
                await DownloadMonitorBatchStatusEmailCoordinator.MarkStatusEmailSentAsync(
                        db,
                        batchStartedAt,
                        CancellationToken.None)
                    .ConfigureAwait(false);
                logger.LogInformation(
                    "Download monitor status email sent for completed batch started at {BatchStartedAt:u}.",
                    batchStartedAt);
            }
            else
            {
                logger.LogWarning(
                    "Download monitor status email was not delivered for batch started at {BatchStartedAt:u}; will retry on next deferred attempt.",
                    batchStartedAt);
                throw new InvalidOperationException("Download monitor status email was not delivered.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Download monitor status email failed for batch started at {BatchStartedAt:u}.",
                batchStartedAt);
            throw;
        }
    }

    [Queue(HangfireQueueOptions.Names.Email)]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [300, 600, 900], OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task SendCompletedBatchStatusEmailWhenReadyAsync(DateTimeOffset batchStartedAt, int attempt)
    {
        if (DateTimeOffset.UtcNow - batchStartedAt > DownloadMonitorBatchTiming.MaxStatusEmailWaitLifecycle)
        {
            logger.LogError(
                "Download monitor batch {BatchStartedAt:u} exceeded max status-email wait; email not sent because sources or auto recovery are still not final.",
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

        var allSettled = await DownloadMonitorBatchOutcomeHelper.WaitForSourcesSettledAsync(
                db,
                sourceIds,
                batchStartedAt,
                waitBudget,
                CancellationToken.None)
            .ConfigureAwait(false);

        if (allSettled
            && await DownloadMonitorBatchOutcomeHelper.IsBatchReadyForStatusEmailAsync(
                    db,
                    sourceIds,
                    batchStartedAt,
                    CancellationToken.None)
                .ConfigureAwait(false))
        {
            await SendCompletedBatchStatusEmailAsync(batchStartedAt).ConfigureAwait(false);
            return;
        }

        if (attempt + 1 >= DownloadMonitorBatchTiming.MaxDeferredEmailAttempts)
        {
            logger.LogWarning(
                "Download monitor batch {BatchStartedAt:u} deferred email attempt cap reached; continuing to wait for auto recovery to finish.",
                batchStartedAt);
        }

        logger.LogInformation(
            "Download monitor batch {BatchStartedAt:u} waiting for final status (downloads/auto recovery); deferring email (attempt {Attempt}).",
            batchStartedAt,
            attempt + 1);

        BackgroundJob.Schedule<DownloadMonitorScheduledJobs>(
            HangfireQueueOptions.Names.Email,
            j => j.SendCompletedBatchStatusEmailWhenReadyAsync(batchStartedAt, attempt + 1),
            DownloadMonitorBatchTiming.DeferredEmailRetryInterval);
    }

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

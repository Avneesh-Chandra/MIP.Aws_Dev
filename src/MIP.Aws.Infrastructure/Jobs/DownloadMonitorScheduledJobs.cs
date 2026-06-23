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
    [DisableConcurrentExecution(timeoutInSeconds: 6 * 60 * 60)]
    [AutomaticRetry(Attempts = 1, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task ScheduleStaggeredDailyDownloadsAsync(PerformContext? context)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var opt = schedulerOptions.Value;
        var interval = Math.Clamp(opt.StaggerIntervalMinutes, 1, 60);
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

        for (var index = 0; index < sources.Count; index++)
        {
            var delay = TimeSpan.FromMinutes(index * interval);
            ScheduleSourceDownload(sources[index], delay);
        }

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
        var waitTimeout = DownloadMonitorBatchTiming.ResolveOrchestratorWaitTimeout(sources.Count, interval);
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

        if (allSettled)
        {
            await SendCompletedBatchStatusEmailAsync(batchStartedAt).ConfigureAwait(false);
            return;
        }

        logger.LogWarning(
            "Download monitor batch started at {BatchStartedAt:u} timed out before all sources settled; deferring status email.",
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
    [DisableConcurrentExecution(timeoutInSeconds: 6 * 60 * 60)]
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [120, 300], OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task ExecuteOperatorPdfBatchAsync(DateTimeOffset batchStartedAt)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var interval = Math.Clamp(schedulerOptions.Value.StaggerIntervalMinutes, 1, 60);

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

        for (var index = 0; index < sources.Count; index++)
        {
            var delay = TimeSpan.FromMinutes(index * interval);
            ScheduleSourceDownload(sources[index], delay);
        }

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
            foreach (var source in sources)
            {
                if (!await DownloadMonitorBatchOutcomeHelper.IsSourceSettledAsync(
                        db,
                        source.Id,
                        batchStartedAt,
                        CancellationToken.None)
                    .ConfigureAwait(false))
                {
                    logger.LogInformation(
                        "Download monitor batch {BatchStartedAt:u} not fully settled; deferring status email.",
                        batchStartedAt);
                    BackgroundJob.Schedule<DownloadMonitorScheduledJobs>(
                        HangfireQueueOptions.Names.Email,
                        j => j.SendCompletedBatchStatusEmailWhenReadyAsync(batchStartedAt, 0),
                        DownloadMonitorBatchTiming.DeferredEmailRetryInterval);
                    return;
                }
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
            await email.SendDailyStatusEmailAsync(monitorDate, CancellationToken.None).ConfigureAwait(false);
            await DownloadMonitorBatchStatusEmailCoordinator.MarkStatusEmailSentAsync(
                    db,
                    batchStartedAt,
                    CancellationToken.None)
                .ConfigureAwait(false);
            logger.LogInformation(
                "Download monitor status email sent for completed batch started at {BatchStartedAt:u}.",
                batchStartedAt);
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
        if (DateTimeOffset.UtcNow - batchStartedAt > DownloadMonitorBatchTiming.MaxBatchLifecycle)
        {
            logger.LogWarning(
                "Sending final download monitor status email for batch {BatchStartedAt:u} after max lifecycle elapsed.",
                batchStartedAt);
            await SendCompletedBatchStatusEmailAsync(batchStartedAt).ConfigureAwait(false);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var interval = Math.Clamp(schedulerOptions.Value.StaggerIntervalMinutes, 1, 60);
        var sources = await LoadMonitoredSourcesAsync(db, CancellationToken.None).ConfigureAwait(false);
        if (sources.Count == 0)
        {
            return;
        }

        var remaining = DownloadMonitorBatchTiming.MaxBatchLifecycle - (DateTimeOffset.UtcNow - batchStartedAt);
        var waitBudget = remaining < DownloadMonitorBatchTiming.DeferredEmailRetryInterval * 2
            ? remaining
            : TimeSpan.FromMinutes(30);

        var allSettled = await DownloadMonitorBatchOutcomeHelper.WaitForSourcesSettledAsync(
                db,
                sources.Select(s => s.Id).ToList(),
                batchStartedAt,
                waitBudget,
                CancellationToken.None)
            .ConfigureAwait(false);

        if (allSettled)
        {
            await SendCompletedBatchStatusEmailAsync(batchStartedAt).ConfigureAwait(false);
            return;
        }

        if (attempt + 1 >= DownloadMonitorBatchTiming.MaxDeferredEmailAttempts)
        {
            logger.LogWarning(
                "Download monitor batch {BatchStartedAt:u} still not fully settled after deferred email attempts; sending best-effort status email.",
                batchStartedAt);
            await SendCompletedBatchStatusEmailAsync(batchStartedAt).ConfigureAwait(false);
            return;
        }

        logger.LogInformation(
            "Download monitor batch {BatchStartedAt:u} not fully settled; deferring status email (attempt {Attempt}).",
            batchStartedAt,
            attempt + 1);

        BackgroundJob.Schedule<DownloadMonitorScheduledJobs>(
            HangfireQueueOptions.Names.Email,
            j => j.SendCompletedBatchStatusEmailWhenReadyAsync(batchStartedAt, attempt + 1),
            DownloadMonitorBatchTiming.DeferredEmailRetryInterval);
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

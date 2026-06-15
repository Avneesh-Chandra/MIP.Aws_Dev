using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Operator;
using MIP.Aws.Infrastructure.Operator;
using MIP.Aws.Application.Abstractions.Reporting;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Jobs;

/// <summary>
/// Staggered daily downloads for download-monitor sources and the post-run status email.
/// </summary>
[Queue(HangfireQueueOptions.Names.Default)]
public sealed class DownloadMonitorScheduledJobs(
    IServiceScopeFactory scopeFactory,
    IOptions<PdfEditionSchedulerOptions> schedulerOptions,
    ILogger<DownloadMonitorScheduledJobs> logger)
{
    [AutomaticRetry(Attempts = 1, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task ScheduleStaggeredDailyDownloadsAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var opt = schedulerOptions.Value;
        var interval = Math.Clamp(opt.StaggerIntervalMinutes, 1, 60);

        var sources = (await db.NewsSources.AsNoTracking()
                .Where(s => !s.IsDeleted && s.IsEnabled)
                .OrderBy(s => s.Name)
                .ToListAsync(CancellationToken.None)
                .ConfigureAwait(false))
            .Where(PdfManagementSourceRules.IsPdfDownloadMonitoredSource)
            .ToList();

        logger.LogInformation(
            "Scheduling {Count} monitored source download(s) starting now with {Interval} minute stagger.",
            sources.Count,
            interval);

        for (var index = 0; index < sources.Count; index++)
        {
            var delay = TimeSpan.FromMinutes(index * interval);
            ScheduleSourceDownload(sources[index], delay);
        }
    }

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
    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteOperatorPdfBatchAsync(DateTimeOffset batchStartedAt)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IDownloadMonitorDailyStatusEmailService>();
        var opt = schedulerOptions.Value;
        var interval = Math.Clamp(opt.StaggerIntervalMinutes, 1, 60);

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

        var staggerWindow = TimeSpan.FromMinutes(Math.Max(0, sources.Count - 1) * interval);
        var waitTimeout = staggerWindow + TimeSpan.FromMinutes(50);

        await DownloadMonitorBatchOutcomeHelper.WaitForSourcesSettledAsync(
                db,
                sources.Select(s => s.Id).ToList(),
                batchStartedAt,
                waitTimeout,
                CancellationToken.None)
            .ConfigureAwait(false);

        var successCount = 0;
        foreach (var source in sources)
        {
            if (await DownloadMonitorBatchOutcomeHelper.IsSourceSuccessfulAsync(
                    db,
                    source.Id,
                    batchStartedAt,
                    CancellationToken.None)
                .ConfigureAwait(false))
            {
                successCount++;
            }
        }

        logger.LogInformation(
            "Operator PDF batch finished waiting for {Count} source(s): {SuccessCount} succeeded.",
            sources.Count,
            successCount);

        await email.SendDailyStatusEmailAsync(null, CancellationToken.None).ConfigureAwait(false);
        logger.LogInformation(
            "Operator PDF batch sent download monitor status email after batch completion ({SuccessCount}/{Total} succeeded).",
            successCount,
            sources.Count);
    }

    private void ScheduleSourceDownload(NewsSource source, TimeSpan delay)
    {
        if (source.PdfDiscoveryEnabled
            && source.SourceType is NewsSourceType.PublicHtml or NewsSourceType.PublicPdf)
        {
            BackgroundJob.Schedule<PdfEditionJobs>(
                j => j.DiscoverAndDownloadTodayPdfAsync(source.Id),
                delay);
            logger.LogInformation(
                "Scheduled PDF edition download for {Source} in {Delay} (at ~{RunAt:u}).",
                source.Name,
                delay,
                DateTimeOffset.UtcNow.Add(delay));
            return;
        }

        if (source.SourceType == NewsSourceType.WebPortalLogin)
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

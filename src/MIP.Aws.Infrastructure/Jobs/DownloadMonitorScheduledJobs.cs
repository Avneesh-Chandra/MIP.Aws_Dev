using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Operator;
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
}

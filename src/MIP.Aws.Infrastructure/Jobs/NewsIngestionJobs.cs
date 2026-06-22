using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Downloading;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Application.Abstractions.Telemetry;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Infrastructure.Browser;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure.Jobs;

/// <summary>
/// Hangfire entry points for newspaper ingestion (resolved via DI). All jobs run on the
/// <see cref="HangfireQueueOptions.Names.Downloads"/> queue so the API/worker can scale that queue
/// independently from OCR or AI workloads.
/// </summary>
[Queue(HangfireQueueOptions.Names.Downloads)]
public sealed class NewsIngestionJobs(IServiceScopeFactory scopeFactory, ILogger<NewsIngestionJobs> logger, ITelemetryService telemetry)
{
    [Queue(HangfireQueueOptions.Names.Downloads)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task DownloadSourceAsync(Guid newsSourceId) =>
        await PlaywrightDownloadConcurrencyGate.RunAsync(
            () => RunDownloadAsync(
                "hangfire.downloads.source",
                async manager =>
                {
                    using (DownloadExecutionContext.UseTrigger(DownloadJobTrigger.Scheduled))
                    {
                        await manager.ExecuteSourceDownloadAsync(newsSourceId, CancellationToken.None).ConfigureAwait(false);
                    }
                },
                activity => activity?.SetTag("newsSourceId", newsSourceId)),
            CancellationToken.None).ConfigureAwait(false);

    [Queue(HangfireQueueOptions.Names.Downloads)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task DownloadJobAsync(Guid downloadJobId) =>
        await PlaywrightDownloadConcurrencyGate.RunAsync(
            () => RunDownloadAsync(
                "hangfire.downloads.job",
                manager => manager.ExecuteDownloadJobAsync(downloadJobId, CancellationToken.None),
                activity => activity?.SetTag("downloadJobId", downloadJobId)),
            CancellationToken.None).ConfigureAwait(false);

    private async Task RunDownloadAsync(
        string activityName,
        Func<IDownloadManager, Task> execute,
        Action<System.Diagnostics.Activity?> configureActivity)
    {
        using var activity = telemetry.StartActivity(activityName);
        configureActivity(activity);

        using var scope = scopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IDownloadManager>();
        var start = DateTimeOffset.UtcNow;
        try
        {
            await execute(manager).ConfigureAwait(false);
            telemetry.IncrementCounter(TelemetryNames.DownloadSuccess);
        }
        catch
        {
            telemetry.IncrementCounter(TelemetryNames.DownloadFailures);
            throw;
        }
        finally
        {
            telemetry.RecordDuration(TelemetryNames.DownloadDuration, (DateTimeOffset.UtcNow - start).TotalMilliseconds);
        }
    }

    [Queue(HangfireQueueOptions.Names.Downloads)]
    public async Task DownloadAllActiveAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var sources = await db.NewsSources.AsNoTracking()
            .Where(s => s.IsEnabled && !s.IsDeleted)
            .ToListAsync(CancellationToken.None)
            .ConfigureAwait(false);

        var monitored = sources.Where(PdfManagementSourceRules.IsPdfDownloadMonitoredSource).ToList();
        var ids = sources
            .Where(s => !PdfManagementSourceRules.IsPdfDownloadMonitoredSource(s))
            .Select(s => s.Id)
            .ToList();

        if (monitored.Count > 0)
        {
            logger.LogInformation(
                "Skipping {Skipped} PDF download-monitor source(s) from hourly schedule (daily stagger applies): {Names}",
                monitored.Count,
                string.Join(", ", monitored.Select(s => s.Name)));
        }

        logger.LogInformation("Scheduling downloads for {Count} active sources.", ids.Count);
        foreach (var id in ids)
        {
            BackgroundJob.Enqueue<NewsIngestionJobs>(j => j.DownloadSourceAsync(id));
        }
    }

    [Queue(HangfireQueueOptions.Names.Default)]
    public async Task CleanupOldDownloadsAsync(int retentionDays)
    {
        using var scope = scopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IDownloadManager>();
        var removed = await manager.CleanupOldArtifactsAsync(retentionDays, CancellationToken.None).ConfigureAwait(false);
        logger.LogInformation("Cleanup removed {Count} old files.", removed);
    }

    [Queue(HangfireQueueOptions.Names.Downloads)]
    public async Task RetryFailedDownloadsAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IDownloadManager>();
        var n = await manager.RetryFailedJobsAsync(CancellationToken.None).ConfigureAwait(false);
        logger.LogInformation("Retry pass touched {Count} sources.", n);
    }
}

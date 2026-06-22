using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Scheduling;
using MIP.Aws.Infrastructure.Jobs;
using Hangfire;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Scheduling;

/// <summary>AWS lite recurring jobs: PDF downloads, download monitor, and housekeeping only.</summary>
public sealed class HangfireScheduledJobRegistry(
    IRecurringJobManager recurringJobs,
    IOptions<PdfEditionSchedulerOptions> pdfEditionOptions) : IScheduledJobRegistry
{
    public void RegisterRecurringJobs()
    {
        recurringJobs.RemoveIfExists("download-newspapers");
        recurringJobs.RemoveIfExists("extract-content");
        recurringJobs.RemoveIfExists("analyze-articles");
        recurringJobs.RemoveIfExists("fetch-market-rates");
        recurringJobs.RemoveIfExists("reporting-cleanup");
        recurringJobs.RemoveIfExists("reporting-daily-executive");
        recurringJobs.RemoveIfExists("market-import-daily");
        recurringJobs.RemoveIfExists("daily-executive-brief-generate");
        recurringJobs.RemoveIfExists("daily-intelligence-brief-email");

        recurringJobs.AddOrUpdate<NewsIngestionJobs>(
            "cleanup-downloads",
            job => job.CleanupOldDownloadsAsync(30),
            "0 3 * * *");

        recurringJobs.AddOrUpdate<NewsIngestionJobs>(
            "retry-failed-downloads",
            job => job.RetryFailedDownloadsAsync(),
            "15 */4 * * *");

        var pdfEdition = pdfEditionOptions.Value;
        var staggerCron = ToUtcCronDirect(
            pdfEdition.FirstSourceScheduleTimeUtc,
            fallbackUtcHour: 4,
            fallbackUtcMinute: 30);
        recurringJobs.AddOrUpdate<DownloadMonitorScheduledJobs>(
            "download-monitor-stagger-schedule",
            job => job.ScheduleStaggeredDailyDownloadsAsync(null),
            staggerCron);
        recurringJobs.RemoveIfExists("pdf-edition-daily");

        if (pdfEdition.StatusEmailEnabled && !pdfEdition.StatusEmailAfterBatchOnly)
        {
            var statusEmailCron = ToUtcCronDirect(
                pdfEdition.StatusEmailTimeUtc,
                fallbackUtcHour: 4,
                fallbackUtcMinute: 30);
            recurringJobs.AddOrUpdate<DownloadMonitorScheduledJobs>(
                "download-monitor-daily-status-email",
                job => job.SendDailyStatusEmailAsync(),
                statusEmailCron);
        }
        else
        {
            recurringJobs.RemoveIfExists("download-monitor-daily-status-email");
        }
    }

    private static string ToUtcCronDirect(string utcTime, int fallbackUtcHour, int fallbackUtcMinute = 0) =>
        TimeOnly.TryParse(utcTime, out var time)
            ? $"{time.Minute} {time.Hour} * * *"
            : $"{fallbackUtcMinute} {fallbackUtcHour} * * *";
}

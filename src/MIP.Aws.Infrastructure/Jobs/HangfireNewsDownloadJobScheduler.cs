using MIP.Aws.Application.Abstractions.Jobs;
using Hangfire;

namespace MIP.Aws.Infrastructure.Jobs;

public sealed class HangfireNewsDownloadJobScheduler : INewsDownloadJobScheduler
{
    public void EnqueueDownloadAllActive() =>
        BackgroundJob.Enqueue<NewsIngestionJobs>(j => j.DownloadAllActiveAsync());

    public void EnqueueDownloadSingle(Guid newsSourceId) =>
        BackgroundJob.Enqueue<NewsIngestionJobs>(j => j.DownloadSourceAsync(newsSourceId));

    public void EnqueueDownloadJob(Guid downloadJobId) =>
        BackgroundJob.Enqueue<NewsIngestionJobs>(j => j.DownloadJobAsync(downloadJobId));

    public void EnqueueCleanup(int retentionDays) =>
        BackgroundJob.Enqueue<NewsIngestionJobs>(j => j.CleanupOldDownloadsAsync(retentionDays));

    public void EnqueueRetryFailed() =>
        BackgroundJob.Enqueue<NewsIngestionJobs>(j => j.RetryFailedDownloadsAsync());
}

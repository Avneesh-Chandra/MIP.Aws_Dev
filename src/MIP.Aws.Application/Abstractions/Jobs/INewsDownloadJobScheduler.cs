namespace MIP.Aws.Application.Abstractions.Jobs;

/// <summary>
/// Schedules Hangfire work items without referencing Hangfire types from the Application layer.
/// </summary>
public interface INewsDownloadJobScheduler
{
    void EnqueueDownloadAllActive();

    void EnqueueDownloadSingle(Guid newsSourceId);

    void ScheduleDownloadSingle(Guid newsSourceId, TimeSpan delay);

    void ScheduleAlAyamDeferredDownload(Guid newsSourceId, int deferredAttemptIndex, TimeSpan delay);

    void EnqueueDownloadJob(Guid downloadJobId);

    void EnqueueCleanup(int retentionDays);

    void EnqueueRetryFailed();
}

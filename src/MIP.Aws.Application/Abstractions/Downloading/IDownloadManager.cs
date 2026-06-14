namespace MIP.Aws.Application.Abstractions.Downloading;

/// <summary>
/// Orchestrates newspaper downloads, persistence, and Hangfire-triggered execution.
/// </summary>
public interface IDownloadManager
{
    Task ExecuteSourceDownloadAsync(Guid newsSourceId, CancellationToken cancellationToken);

    Task ExecuteDownloadJobAsync(Guid downloadJobId, CancellationToken cancellationToken);

    Task<int> CleanupOldArtifactsAsync(int retentionDays, CancellationToken cancellationToken);

    Task<int> RetryFailedJobsAsync(CancellationToken cancellationToken);
}

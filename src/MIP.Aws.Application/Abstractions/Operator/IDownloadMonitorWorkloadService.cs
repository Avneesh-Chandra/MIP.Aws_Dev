namespace MIP.Aws.Application.Abstractions.Operator;

public interface IDownloadMonitorWorkloadService
{
    Task<DownloadMonitorWorkloadSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);

    Task<AbortDownloadMonitorWorkResult> AbortActiveWorkAsync(CancellationToken cancellationToken);
}

public sealed record DownloadMonitorWorkloadSnapshot(
    DownloadMonitorBatchProgressResult? BatchProgress,
    IReadOnlyList<DownloadMonitorActiveJobResult> ActiveJobs,
    int PendingHangfireDownloadJobs,
    bool HasActiveWork);

public sealed record DownloadMonitorActiveJobResult(
    Guid JobId,
    Guid SourceId,
    string SourceName,
    string Status,
    string Activity,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt);

public sealed record AbortDownloadMonitorWorkResult(
    int DownloadJobsCancelled,
    int HangfireJobsRemoved,
    bool BatchOrchestratorStopped,
    string Summary);

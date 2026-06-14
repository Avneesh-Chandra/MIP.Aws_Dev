namespace MIP.Aws.Application.Abstractions.Operator;

public interface IDownloadMonitorBatchRunService
{
    Task<DownloadMonitorBatchRunResult> StartBatchAsync(CancellationToken cancellationToken);

    Task<DownloadMonitorBatchProgressResult?> GetProgressAsync(
        DateTimeOffset? batchStartedAt,
        CancellationToken cancellationToken);
}

public sealed record DownloadMonitorBatchRunResult(
    DateTimeOffset StartedAt,
    int TotalSources,
    string HangfireJobId);

public sealed record DownloadMonitorBatchProgressResult(
    DateTimeOffset StartedAt,
    int TotalSources,
    int CompletedCount,
    int SuccessCount,
    int FailedCount,
    int InProgressCount,
    int WaitingCount,
    int AutoRecoveryCount,
    double PercentComplete,
    bool IsActive,
    bool IsComplete,
    string CurrentPhase,
    string StatusSummary,
    IReadOnlyList<DownloadMonitorBatchActivityResult> Activities);

public sealed record DownloadMonitorBatchActivityResult(
    string SourceName,
    string Activity,
    string State);

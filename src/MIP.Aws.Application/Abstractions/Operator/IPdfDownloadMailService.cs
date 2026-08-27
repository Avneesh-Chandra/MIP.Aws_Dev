namespace MIP.Aws.Application.Abstractions.Operator;

public interface IPdfDownloadMailService
{
    Task<bool> SendForLatestBatchAsync(
        CancellationToken cancellationToken,
        bool bypassSchedule = false);

    Task<bool> SendForBatchAsync(
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken,
        bool bypassSchedule = false);
}

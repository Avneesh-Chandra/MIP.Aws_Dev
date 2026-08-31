namespace MIP.Aws.Application.Abstractions.Operator;

public interface IDownloadMonitorDailyStatusEmailService
{
    /// <returns>True when at least one recipient received the status email.</returns>
    Task<bool> SendDailyStatusEmailAsync(
        DateOnly? monitorDate,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? recipientOverride = null,
        string? executiveSummaryPrefix = null,
        bool bypassThrottle = false,
        string? subjectOverride = null);

    Task<bool> SendInitialBatchStatusEmailAsync(
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken);

    /// <returns>True when at least one recipient received the recovery follow-up email.</returns>
    Task<bool> SendRecoveryFollowUpEmailAsync(
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken);

    Task<bool> SendManualCompletionStatusEmailAsync(
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken);

    Task<bool> SendManualStatusEmailAsync(
        DateOnly? monitorDate,
        IReadOnlyList<string>? recipientOverride,
        bool bypassThrottle,
        CancellationToken cancellationToken);
}

namespace MIP.Aws.Application.Abstractions.Operator;

public interface IDownloadMonitorDailyStatusEmailService
{
    /// <returns>True when at least one recipient received the status email.</returns>
    Task<bool> SendDailyStatusEmailAsync(
        DateOnly? monitorDate,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? recipientOverride = null);
}

namespace MIP.Aws.Application.Abstractions.Operator;

public interface IDownloadMonitorDailyStatusEmailService
{
    Task SendDailyStatusEmailAsync(
        DateOnly? monitorDate,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? recipientOverride = null);
}

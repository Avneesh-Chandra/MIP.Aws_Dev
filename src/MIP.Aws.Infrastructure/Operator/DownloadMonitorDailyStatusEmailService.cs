using MIP.Aws.Application.Abstractions.Operator;
using MIP.Aws.Application.Abstractions.Reporting;
using MIP.Aws.Application.Configuration;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure.Operator;

public sealed class DownloadMonitorDailyStatusEmailService(
    IOperatorDownloadMonitorService monitorService,
    IReportEmailSender emailSender,
    IDownloadMonitorStatusSummaryService summaryService,
    IMailSettingsService mailSettings,
    ILogger<DownloadMonitorDailyStatusEmailService> logger) : IDownloadMonitorDailyStatusEmailService
{
    public async Task<bool> SendDailyStatusEmailAsync(
        DateOnly? monitorDate,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? recipientOverride = null)
    {
        var scheduler = await mailSettings.GetEffectiveSchedulerAsync(cancellationToken).ConfigureAwait(false);
        if (!scheduler.StatusEmailEnabled)
        {
            logger.LogInformation("Download monitor status email skipped (StatusEmailEnabled=false).");
            return false;
        }

        var recipients = recipientOverride is { Count: > 0 }
            ? recipientOverride
            : ParseRecipients(scheduler.StatusEmailRecipient);
        if (recipients.Count == 0)
        {
            logger.LogWarning("Download monitor status email skipped: StatusEmailRecipient is not configured.");
            return false;
        }

        var date = monitorDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var monitor = await monitorService.GetMonitorAsync(date, cancellationToken).ConfigureAwait(false);
        var portalBase = ResolvePortalBaseUrl(scheduler.AdminPortalUrl);
        var summary = await summaryService.BuildSummaryAsync(monitor, cancellationToken).ConfigureAwait(false);
        var html = DownloadMonitorStatusEmailHtmlBuilder.Build(monitor, portalBase, summary);
        var subject = $"GFH MIP AWS — Download Monitor status ({date:yyyy-MM-dd})";

        var send = await emailSender.SendAsync(
            new ReportEmailMessage(recipients, subject, html, []),
            cancellationToken).ConfigureAwait(false);

        if (send.Success)
        {
            logger.LogInformation(
                "Download monitor status email sent to {Recipient} for {Date}.",
                string.Join(", ", recipients),
                date);
            return true;
        }

        logger.LogWarning(
            "Download monitor status email failed for {Date}: {Error}",
            date,
            send.ErrorMessage ?? send.Outcome.ToString());
        return false;
    }

    private static string ResolvePortalBaseUrl(string? configured) =>
        string.IsNullOrWhiteSpace(configured)
            ? string.Empty
            : configured.TrimEnd('/');

    private static IReadOnlyList<string> ParseRecipients(string value) =>
        value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}

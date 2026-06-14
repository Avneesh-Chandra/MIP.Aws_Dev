using MIP.Aws.Application.Abstractions.Operator;
using MIP.Aws.Application.Abstractions.Reporting;
using MIP.Aws.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Operator;

public sealed class DownloadMonitorDailyStatusEmailService(
    IOperatorDownloadMonitorService monitorService,
    IReportEmailSender emailSender,
    IOptions<PdfEditionSchedulerOptions> schedulerOptions,
    IOptions<MailAutomationOptions> mailAutomation,
    ILogger<DownloadMonitorDailyStatusEmailService> logger) : IDownloadMonitorDailyStatusEmailService
{
    public async Task SendDailyStatusEmailAsync(DateOnly? monitorDate, CancellationToken cancellationToken)
    {
        var opt = schedulerOptions.Value;
        if (!mailAutomation.Value.Enabled || !opt.StatusEmailEnabled)
        {
            logger.LogInformation("Download monitor status email skipped (mail or status email disabled).");
            return;
        }

        if (string.IsNullOrWhiteSpace(opt.StatusEmailRecipient))
        {
            logger.LogWarning("Download monitor status email skipped: StatusEmailRecipient is not configured.");
            return;
        }

        var date = monitorDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var monitor = await monitorService.GetMonitorAsync(date, cancellationToken).ConfigureAwait(false);
        var portalBase = ResolvePortalBaseUrl(opt.AdminPortalUrl);
        var html = DownloadMonitorStatusEmailHtmlBuilder.Build(monitor, portalBase);
        var subject = $"GFH MIP — Download Monitor status ({date:yyyy-MM-dd})";

        var send = await emailSender.SendAsync(
            new ReportEmailMessage([opt.StatusEmailRecipient.Trim()], subject, html, []),
            cancellationToken).ConfigureAwait(false);

        if (send.Success)
        {
            logger.LogInformation(
                "Download monitor status email sent to {Recipient} for {Date}.",
                opt.StatusEmailRecipient,
                date);
        }
        else
        {
            logger.LogWarning(
                "Download monitor status email failed for {Date}: {Error}",
                date,
                send.ErrorMessage ?? send.Outcome.ToString());
        }
    }

    private static string ResolvePortalBaseUrl(string? configured) =>
        string.IsNullOrWhiteSpace(configured)
            ? string.Empty
            : configured.TrimEnd('/');
}

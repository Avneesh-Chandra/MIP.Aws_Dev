using MIP.Aws.Application.Abstractions.News;
using MIP.Aws.Application.Abstractions.Reporting;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.NewsSources.PdfEdition;
using MIP.Aws.Application.Abstractions.Auditing;
using MIP.Aws.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.News.PdfEdition;

public sealed class PdfEditionAdminNotificationService(
    IReportEmailSender emailSender,
    IAuditService audit,
    IOptions<PdfEditionSchedulerOptions> options,
    IOptions<MailAutomationOptions> mailAutomation,
    ILogger<PdfEditionAdminNotificationService> logger) : IPdfEditionAdminNotificationService
{
    public async Task SendManualActionRequiredAsync(
        IReadOnlyList<PdfEditionJobResult> results,
        DateOnly editionDate,
        CancellationToken cancellationToken)
    {
        var opt = options.Value;
        if (!mailAutomation.Value.Enabled || !opt.NotificationEnabled || results.Count == 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(opt.AdminRecipientEmail))
        {
            logger.LogWarning("PDF manual-action notification skipped: AdminRecipientEmail is not configured.");
            return;
        }

        var subject = results.Count == 1
            ? $"GFH MIP — PDF download needs manual action ({results[0].SourceName}, {editionDate:yyyy-MM-dd})"
            : $"GFH MIP — {results.Count} PDF downloads need manual action ({editionDate:yyyy-MM-dd})";

        var html = PdfEditionNotificationHtmlBuilder.Build(results, editionDate, opt.AdminPortalUrl);
        var send = await emailSender.SendAsync(
            new ReportEmailMessage(
                [opt.AdminRecipientEmail.Trim()],
                subject,
                html,
                []),
            cancellationToken).ConfigureAwait(false);

        await audit.RecordAdminActionAsync(
            PdfEditionAuditEvents.ManualActionNotificationSent,
            "PdfEditionScheduler",
            editionDate.ToString("O"),
            new
            {
                recipient = opt.AdminRecipientEmail,
                send.Success,
                send.Outcome,
                send.ErrorMessage,
                sourceCount = results.Count,
                sources = results.Select(r => new { r.NewsSourceId, r.SourceName, status = r.Status.ToString() })
            },
            cancellationToken).ConfigureAwait(false);

        if (send.Success)
        {
            logger.LogInformation(
                "PDF manual-action notification sent to {Recipient} for {Count} source(s) on {Date}.",
                opt.AdminRecipientEmail,
                results.Count,
                editionDate);
        }
        else
        {
            logger.LogWarning(
                "PDF manual-action notification failed for {Count} source(s): {Error}",
                results.Count,
                send.ErrorMessage ?? send.Outcome.ToString());
        }
    }
}

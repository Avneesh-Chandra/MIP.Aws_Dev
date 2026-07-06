using System.Net;
using System.Text;
using MIP.Aws.Application.Features.AutoAiRecovery;
using MIP.Aws.Application.Time;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Infrastructure.Operator;

public static class DownloadMonitorRecoveryFollowUpEmailHtmlBuilder
{
    public static string Build(
        DateOnly monitorDate,
        string portalBaseUrl,
        IReadOnlyList<RecoveryFollowUpSourceSection> sections,
        string? adminRecipientEmail = null)
    {
        var monitorUrl = $"{portalBaseUrl.TrimEnd('/')}/operator/download-monitor";
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1f2937;max-width:900px;\">");
        sb.Append("<h2 style=\"color:#0A2342;margin:0 0 16px;\">GFH Media Intelligence — Auto-recovery update</h2>");
        sb.Append("<p style=\"margin:0 0 16px;line-height:1.5;\">")
            .Append("Auto AI recovery has finished for the ")
            .Append(WebUtility.HtmlEncode(monitorDate.ToString("yyyy-MM-dd")))
            .Append(" download batch. Summary below includes the final outcome and actions tried.")
            .Append("</p>");

        foreach (var section in sections)
        {
            AppendSourceSection(sb, section, monitorDate, adminRecipientEmail);
        }

        sb.Append("<p style=\"margin:16px 0 0;\"><a href=\"")
            .Append(WebUtility.HtmlEncode(monitorUrl))
            .Append("\" style=\"color:#1d4ed8;\">Open Download Monitor</a></p>");
        sb.Append("<p style=\"font-size:12px;color:#6b7280;margin-top:24px;\">")
            .Append("Automated recovery follow-up from GFH Media Intelligence. Replies are not monitored.")
            .Append($" All times are {MipDisplayTimeZone.ZoneSuffix()} ({MipDisplayTimeZone.DefaultIanaId}, UTC+3).")
            .Append("</p></div>");

        return sb.ToString();
    }

    private static void AppendSourceSection(
        StringBuilder sb,
        RecoveryFollowUpSourceSection section,
        DateOnly monitorDate,
        string? adminRecipientEmail)
    {
        var (bg, fg) = section.Succeeded
            ? ("#dcfce7", "#166534")
            : ("#fee2e2", "#991b1b");

        sb.Append("<div style=\"border:1px solid #e5e7eb;border-radius:8px;padding:12px 16px;margin:0 0 12px;\">");
        sb.Append("<div style=\"font-weight:600;margin-bottom:6px;\">")
            .Append(WebUtility.HtmlEncode(section.SourceName))
            .Append(" <span style=\"display:inline-block;padding:2px 10px;border-radius:999px;background:")
            .Append(bg)
            .Append(";color:")
            .Append(fg)
            .Append(";font-size:12px;font-weight:600;\">")
            .Append(WebUtility.HtmlEncode(FormatStatus(section.RunStatus)))
            .Append("</span></div>");

        if (!string.IsNullOrWhiteSpace(section.ResultSummary))
        {
            sb.Append("<p style=\"margin:0 0 8px;line-height:1.5;\"><strong>Outcome:</strong> ")
                .Append(WebUtility.HtmlEncode(section.ResultSummary))
                .Append("</p>");
        }

        if (!string.IsNullOrWhiteSpace(section.SuccessfulOptionTitle))
        {
            sb.Append("<p style=\"margin:0 0 8px;line-height:1.5;\"><strong>Successful fix:</strong> ")
                .Append(WebUtility.HtmlEncode(section.SuccessfulOptionTitle))
                .Append("</p>");
        }

        sb.Append("<p style=\"margin:0 0 8px;line-height:1.5;\"><strong>Suggestions tried:</strong> ")
            .Append(section.SuggestionsTried)
            .Append("</p>");

        if (section.Timeline.Count > 0)
        {
            sb.Append("<ul style=\"margin:0;padding-left:20px;\">");
            foreach (var step in section.Timeline)
            {
                sb.Append("<li style=\"margin-bottom:4px;\"><strong>")
                    .Append(WebUtility.HtmlEncode(step.Step))
                    .Append("</strong> — ")
                    .Append(WebUtility.HtmlEncode(step.Detail))
                    .Append(" <span style=\"color:#6b7280;font-size:12px;\">(")
                    .Append(MipDisplayTimeZone.Format(step.Timestamp))
                    .Append(")</span></li>");
            }

            sb.Append("</ul>");
        }

        if (!section.Succeeded && !string.IsNullOrWhiteSpace(adminRecipientEmail))
        {
            var timeline = section.Timeline
                .Select(step => (step.Step, step.Detail, step.Timestamp))
                .ToList();
            var body = DownloadMonitorStatusEmailActionHelper.BuildInformAdminBodyForRecoveryFollowUp(
                section.SourceName,
                monitorDate,
                FormatStatus(section.RunStatus),
                section.ResultSummary,
                section.SuccessfulOptionTitle,
                section.SuggestionsTried,
                timeline);
            if (body.Length > DownloadMonitorStatusEmailActionHelper.MaxMailToBodyLength)
            {
                body = body[..DownloadMonitorStatusEmailActionHelper.MaxMailToBodyLength] + "…";
            }

            var subject = $"GFH MIP — Auto-recovery failed: {section.SourceName} ({monitorDate:yyyy-MM-dd})";
            var mailTo = DownloadMonitorStatusEmailActionHelper.BuildMailToUri(adminRecipientEmail.Trim(), subject, body);
            sb.Append("<p style=\"margin:12px 0 0;\">")
                .Append(EmailHtmlLinkFormatter.Link(mailTo, "Inform Admin"))
                .Append("</p>");
        }

        sb.Append("</div>");
    }

    private static string FormatStatus(AutoAiRecoveryRunStatus status) => status switch
    {
        AutoAiRecoveryRunStatus.CompletedSuccess => "Recovered",
        AutoAiRecoveryRunStatus.CompletedFailure => "Recovery failed",
        AutoAiRecoveryRunStatus.SkippedIneligible => "Skipped",
        AutoAiRecoveryRunStatus.SkippedNoSuggestions => "No safe suggestions",
        AutoAiRecoveryRunStatus.SkippedRepeatedBaseline => "Skipped — baseline already active",
        AutoAiRecoveryRunStatus.SkippedCooldown => "Skipped (cooldown)",
        _ => status.ToString()
    };

    public sealed record RecoveryFollowUpSourceSection(
        string SourceName,
        AutoAiRecoveryRunStatus RunStatus,
        bool Succeeded,
        string? ResultSummary,
        string? SuccessfulOptionTitle,
        int SuggestionsTried,
        IReadOnlyList<AutoAiRecoveryTimelineStepDto> Timeline);
}

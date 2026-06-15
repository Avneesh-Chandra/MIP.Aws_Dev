using System.Net;
using System.Text;
using MIP.Aws.Application.Features.Operator;

namespace MIP.Aws.Infrastructure.Operator;

public static class DownloadMonitorStatusEmailHtmlBuilder
{
    public static string Build(DownloadMonitorDto monitor, string portalBaseUrl, string? executiveSummary = null)
    {
        var monitorUrl = $"{portalBaseUrl.TrimEnd('/')}/operator/download-monitor";
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1f2937;max-width:1100px;\">");
        sb.Append("<h2 style=\"color:#0A2342;margin:0 0 16px;\">GFH Media Intelligence — Download Monitor</h2>");

        if (!string.IsNullOrWhiteSpace(executiveSummary))
        {
            sb.Append("<p style=\"margin:0 0 16px;line-height:1.5;\">")
                .Append(WebUtility.HtmlEncode(executiveSummary))
                .Append("</p>");
        }

        AppendSummaryCards(sb, monitor);
        AppendAttentionSection(sb, monitor, monitorUrl);
        AppendSourcesTable(sb, monitor, portalBaseUrl, monitorUrl);

        sb.Append("<p style=\"font-size:12px;color:#6b7280;margin-top:24px;\">")
            .Append("Automated daily status from GFH Media Intelligence. Replies are not monitored.")
            .Append("</p></div>");

        return sb.ToString();
    }

    private static void AppendSummaryCards(StringBuilder sb, DownloadMonitorDto monitor)
    {
        var s = monitor.Summary;
        sb.Append("<table style=\"border-collapse:separate;border-spacing:8px;width:100%;margin:0 0 16px;\"><tr>");
        AppendMetricCard(sb, "Total sources", s.TotalSources.ToString(), "#1f2937");
        AppendMetricCard(sb, "Successful today", s.SuccessfulToday.ToString(), "#15803d");
        AppendMetricCard(sb, "Failed today", s.FailedToday.ToString(), "#b91c1c");
        AppendMetricCard(sb, "Manual intervention", s.PendingManualIntervention.ToString(), "#b45309");
        AppendMetricCard(sb, "PDFs today", s.PdfsDownloadedToday.ToString(), "#1f2937");
        AppendMetricCard(sb, "Admin alerts pending", s.AdminNotificationsPending.ToString(), "#0369a1");
        sb.Append("</tr></table>");
    }

    private static void AppendMetricCard(StringBuilder sb, string label, string value, string color)
    {
        sb.Append("<td style=\"border:1px solid #e5e7eb;border-radius:8px;padding:12px;vertical-align:top;width:16%;\">")
            .Append("<div style=\"font-size:12px;color:#6b7280;\">").Append(WebUtility.HtmlEncode(label)).Append("</div>")
            .Append("<div style=\"font-size:22px;font-weight:700;color:").Append(color).Append(";\">")
            .Append(WebUtility.HtmlEncode(value))
            .Append("</div></td>");
    }

    private static void AppendAttentionSection(StringBuilder sb, DownloadMonitorDto monitor, string monitorUrl)
    {
        if (monitor.Summary.SourcesRequiringAttention.Count == 0)
        {
            return;
        }

        sb.Append("<div style=\"border:1px solid #e5e7eb;border-radius:8px;padding:12px 16px;margin:0 0 16px;background:#fffbeb;\">");
        sb.Append("<div style=\"font-weight:600;margin-bottom:8px;\">Sources requiring attention — ")
            .Append(WebUtility.HtmlEncode(monitor.MonitorDate.ToString("yyyy-MM-dd")))
            .Append("</div><ul style=\"margin:0;padding-left:20px;\">");

        foreach (var item in monitor.Summary.SourcesRequiringAttention)
        {
            sb.Append("<li style=\"margin-bottom:6px;\"><strong>")
                .Append(WebUtility.HtmlEncode(item.SourceName))
                .Append("</strong> — ")
                .Append(WebUtility.HtmlEncode(item.Issue))
                .Append(' ')
                .Append(WebUtility.HtmlEncode(item.ActionRequired))
                .Append("</li>");
        }

        sb.Append("</ul><p style=\"margin:8px 0 0;\"><a href=\"")
            .Append(WebUtility.HtmlEncode(monitorUrl))
            .Append("\" style=\"color:#1d4ed8;\">Open Download Monitor</a></p></div>");
    }

    private static void AppendSourcesTable(
        StringBuilder sb,
        DownloadMonitorDto monitor,
        string portalBaseUrl,
        string monitorUrl)
    {
        sb.Append("<table style=\"border-collapse:collapse;width:100%;font-size:13px;\">");
        sb.Append("<thead><tr style=\"background:#f3f4f6;\">");
        foreach (var header in new[] { "Source", "Type", "Country", "Status", "Last attempt", "Last success", "Latest PDF", "Intervention", "Actions" })
        {
            sb.Append("<th style=\"text-align:left;padding:8px;border:1px solid #e5e7eb;\">")
                .Append(WebUtility.HtmlEncode(header))
                .Append("</th>");
        }

        sb.Append("</tr></thead><tbody>");

        foreach (var row in monitor.Sources)
        {
            sb.Append("<tr>");
            sb.Append("<td style=\"padding:8px;border:1px solid #e5e7eb;vertical-align:top;\"><strong>")
                .Append(WebUtility.HtmlEncode(row.SourceName))
                .Append("</strong></td>");
            sb.Append("<td style=\"padding:8px;border:1px solid #e5e7eb;\">")
                .Append(WebUtility.HtmlEncode(row.SourceType))
                .Append("</td>");
            sb.Append("<td style=\"padding:8px;border:1px solid #e5e7eb;\">")
                .Append(WebUtility.HtmlEncode(row.Country ?? "—"))
                .Append("</td>");
            sb.Append("<td style=\"padding:8px;border:1px solid #e5e7eb;\">")
                .Append(StatusBadge(row.LastDownloadStatus))
                .Append("</td>");
            sb.Append("<td style=\"padding:8px;border:1px solid #e5e7eb;\">")
                .Append(FormatUtc(row.LastDownloadTime))
                .Append("</td>");
            sb.Append("<td style=\"padding:8px;border:1px solid #e5e7eb;\">")
                .Append(FormatUtc(row.LastSuccessfulDownload))
                .Append("</td>");
            sb.Append("<td style=\"padding:8px;border:1px solid #e5e7eb;\">")
                .Append(LatestPdfCell(row, portalBaseUrl))
                .Append("</td>");
            sb.Append("<td style=\"padding:8px;border:1px solid #e5e7eb;\">")
                .Append(InterventionCell(row))
                .Append("</td>");
            sb.Append("<td style=\"padding:8px;border:1px solid #e5e7eb;text-align:right;white-space:nowrap;\">")
                .Append(ActionButton("Details", monitorUrl, "#ffffff", "#374151", "#d1d5db"))
                .Append(' ')
                .Append(ActionButton("Inform Admin", monitorUrl, "#ffffff", "#c2410c", "#fdba74"))
                .Append("</td>");
            sb.Append("</tr>");
        }

        sb.Append("</tbody></table>");
    }

    private static string StatusBadge(string status)
    {
        var (bg, fg) = status switch
        {
            DownloadMonitorStatusLabels.Success or DownloadMonitorStatusLabels.SuccessByAiRecovery => ("#dcfce7", "#166534"),
            DownloadMonitorStatusLabels.Failed => ("#fee2e2", "#991b1b"),
            DownloadMonitorStatusLabels.ManualActionRequired => ("#ffedd5", "#9a3412"),
            DownloadMonitorStatusLabels.ComplianceBlocked => ("#fee2e2", "#991b1b"),
            DownloadMonitorStatusLabels.InProgress or DownloadMonitorStatusLabels.Pending => ("#dbeafe", "#1e40af"),
            _ => ("#f3f4f6", "#374151")
        };

        return $"<span style=\"display:inline-block;padding:2px 10px;border-radius:999px;background:{bg};color:{fg};font-weight:600;font-size:12px;\">{WebUtility.HtmlEncode(status)}</span>";
    }

    private static string InterventionCell(DownloadMonitorSourceRowDto row)
    {
        if (!row.ManualInterventionRequired)
        {
            return "—";
        }

        var text = row.SuggestedIntervention ?? "Review source configuration and recent portal/PDF audit logs.";
        return "<span style=\"display:inline-block;padding:2px 8px;border-radius:4px;background:#ffedd5;color:#9a3412;font-weight:600;font-size:11px;margin-right:6px;\">Required</span>"
               + WebUtility.HtmlEncode(text);
    }

    private static string LatestPdfCell(DownloadMonitorSourceRowDto row, string portalBaseUrl)
    {
        if (row.LatestPdfFileId is not Guid fileId)
        {
            return "Not available";
        }

        var viewUrl = $"{portalBaseUrl.TrimEnd('/')}/api/v1/operator/sources/{row.SourceId}/latest-pdf?inline=true";
        var downloadUrl = $"{portalBaseUrl.TrimEnd('/')}/api/v1/operator/sources/{row.SourceId}/latest-pdf";
        return $"<a href=\"{WebUtility.HtmlEncode(viewUrl)}\" style=\"color:#1d4ed8;\">View</a> · <a href=\"{WebUtility.HtmlEncode(downloadUrl)}\" style=\"color:#1d4ed8;\">Download</a>";
    }

    private static string ActionButton(string label, string href, string bg, string fg, string border)
    {
        return $"<a href=\"{WebUtility.HtmlEncode(href)}\" style=\"display:inline-block;padding:6px 12px;border:1px solid {border};border-radius:6px;background:{bg};color:{fg};text-decoration:none;font-size:12px;font-weight:600;\">{WebUtility.HtmlEncode(label)}</a>";
    }

    private static string FormatUtc(DateTimeOffset? value) =>
        value is null ? "never" : $"{value.Value.UtcDateTime:yyyy-MM-dd HH:mm} UTC";
}

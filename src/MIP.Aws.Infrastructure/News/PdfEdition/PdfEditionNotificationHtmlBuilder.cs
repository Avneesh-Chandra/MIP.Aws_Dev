using System.Net;
using System.Text;
using MIP.Aws.Application.Abstractions.News;

namespace MIP.Aws.Infrastructure.News.PdfEdition;

public static class PdfEditionNotificationHtmlBuilder
{
    public static string Build(
        IReadOnlyList<PdfEditionJobResult> results,
        DateOnly editionDate,
        string? adminPortalUrl)
    {
        var pdfManagementUrl = string.IsNullOrWhiteSpace(adminPortalUrl)
            ? null
            : $"{adminPortalUrl.TrimEnd('/')}/admin/pdf-management";

        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1f2937;\">");
        sb.Append("<h2 style=\"color:#0A2342;margin:0 0 12px;\">PDF download requires manual action</h2>");
        sb.Append("<p>The scheduled daily PDF download job ran on <strong>")
            .Append(WebUtility.HtmlEncode(editionDate.ToString("dddd, dd MMMM yyyy")))
            .Append("</strong> but could not complete automatically for the source(s) below.</p>");

        sb.Append("<table style=\"border-collapse:collapse;width:100%;margin:16px 0;\">");
        sb.Append("<thead><tr style=\"background:#f3f4f6;\">");
        sb.Append("<th style=\"text-align:left;padding:8px;border:1px solid #e5e7eb;\">Source</th>");
        sb.Append("<th style=\"text-align:left;padding:8px;border:1px solid #e5e7eb;\">Status</th>");
        sb.Append("<th style=\"text-align:left;padding:8px;border:1px solid #e5e7eb;\">Details</th>");
        sb.Append("</tr></thead><tbody>");

        foreach (var result in results)
        {
            sb.Append("<tr>");
            sb.Append("<td style=\"padding:8px;border:1px solid #e5e7eb;vertical-align:top;\"><strong>")
                .Append(WebUtility.HtmlEncode(result.SourceName))
                .Append("</strong>");
            if (!string.IsNullOrWhiteSpace(result.DiscoveryPageUrl))
            {
                sb.Append("<br/><span style=\"font-size:12px;color:#6b7280;\">")
                    .Append(WebUtility.HtmlEncode(result.DiscoveryPageUrl))
                    .Append("</span>");
            }

            sb.Append("</td><td style=\"padding:8px;border:1px solid #e5e7eb;vertical-align:top;\">")
                .Append(WebUtility.HtmlEncode(result.Status.ToString()))
                .Append("</td><td style=\"padding:8px;border:1px solid #e5e7eb;vertical-align:top;\">");
            if (!string.IsNullOrWhiteSpace(result.FailureReason))
            {
                sb.Append(WebUtility.HtmlEncode(result.FailureReason));
            }

            if (!string.IsNullOrWhiteSpace(result.LastCandidateUrl))
            {
                sb.Append("<br/><span style=\"font-size:12px;\">Last URL: ")
                    .Append(WebUtility.HtmlEncode(result.LastCandidateUrl))
                    .Append("</span>");
            }

            sb.Append(BuildSourceHint(result));
            sb.Append("</td></tr>");
        }

        sb.Append("</tbody></table>");

        sb.Append("<h3 style=\"color:#0A2342;margin:20px 0 8px;\">Required steps</h3>");
        sb.Append("<ol style=\"margin:0 0 16px;padding-left:20px;\">");
        if (pdfManagementUrl is not null)
        {
            sb.Append("<li>Open <a href=\"")
                .Append(WebUtility.HtmlEncode(pdfManagementUrl))
                .Append("\" style=\"color:#1d4ed8;\">PDF management</a> in GFH Media Intelligence.</li>");
        }
        else
        {
            sb.Append("<li>Open <strong>Admin → PDF management</strong> in GFH Media Intelligence.</li>");
        }

        sb.Append("<li>For each source listed above, click <strong>Today's PDF</strong>.</li>");
        sb.Append("<li>If the result dialog shows <em>No public PDF available</em> or <em>Failed</em>, paste the URL you verified in your browser into <strong>Manual PDF or issue URL</strong>.</li>");
        sb.Append("<li>Optionally check <strong>Save this URL as the discovery page</strong> so future runs start from that page.</li>");
        sb.Append("<li>Click <strong>Download with manual URL</strong> and confirm the PDF downloads successfully.</li>");
        sb.Append("</ol>");

        sb.Append("<p style=\"font-size:12px;color:#6b7280;margin-top:24px;\">")
            .Append("This is an automated message from the GFH Media Intelligence PDF scheduler. ")
            .Append("Replies are not monitored.")
            .Append("</p></div>");

        return sb.ToString();
    }

    private static string BuildSourceHint(PdfEditionJobResult result)
    {
        if (!string.Equals(result.ConnectorKey, "news.aawsat", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return "<br/><span style=\"font-size:12px;color:#374151;\"><strong>Asharq Al-Awsat tip:</strong> "
            + "Open the publisher site, click the header PDF icon, open the issue viewer, then paste the browser URL "
            + "(e.g. <code>https://aawsat.com/files/pdf/issue…/index.html</code>) into the manual URL field.</span>";
    }
}

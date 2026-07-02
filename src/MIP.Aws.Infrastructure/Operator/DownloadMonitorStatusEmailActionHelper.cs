using System.Text;
using MIP.Aws.Application.Features.Operator;
using MIP.Aws.Application.Time;

namespace MIP.Aws.Infrastructure.Operator;

public static class DownloadMonitorStatusEmailActionHelper
{
    public const int MaxMailToBodyLength = 1800;

    public static string BuildDetailsUrl(string portalBaseUrl, Guid downloadJobId)
    {
        if (string.IsNullOrWhiteSpace(portalBaseUrl))
        {
            return string.Empty;
        }

        return $"{portalBaseUrl.TrimEnd('/')}/operator/download-monitor?jobId={downloadJobId:D}";
    }

    public static string? BuildInformAdminMailTo(
        string? adminRecipientEmail,
        DownloadMonitorSourceRowDto row,
        DateOnly monitorDate,
        DownloadMonitorEmailFailureContext? recoveryContext)
    {
        if (string.IsNullOrWhiteSpace(adminRecipientEmail)
            || !row.ManualInterventionRequired
            || row.LatestDownloadJobId is null)
        {
            return null;
        }

        var subject = $"GFH MIP — Manual intervention required: {row.SourceName} ({monitorDate:yyyy-MM-dd})";
        var body = BuildInformAdminBody(row, monitorDate, recoveryContext);
        if (body.Length > MaxMailToBodyLength)
        {
            body = body[..MaxMailToBodyLength] + "…";
        }

        return $"mailto:{Uri.EscapeDataString(adminRecipientEmail.Trim())}?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";
    }

    public static string BuildInformAdminBody(
        DownloadMonitorSourceRowDto row,
        DateOnly monitorDate,
        DownloadMonitorEmailFailureContext? recoveryContext)
    {
        var sb = new StringBuilder();
        sb.AppendLine("GFH Media Intelligence — operator manual intervention request");
        sb.AppendLine();
        sb.AppendLine($"Monitor date: {monitorDate:yyyy-MM-dd}");
        sb.AppendLine($"Source: {row.SourceName}");
        sb.AppendLine($"Type: {row.SourceType}");
        if (!string.IsNullOrWhiteSpace(row.Country))
        {
            sb.AppendLine($"Country: {row.Country}");
        }

        sb.AppendLine($"Status: {row.LastDownloadStatus}");
        if (!string.IsNullOrWhiteSpace(row.FailureCode))
        {
            sb.AppendLine($"Failure code: {row.FailureCode}");
        }

        if (!string.IsNullOrWhiteSpace(row.FailureReason))
        {
            sb.AppendLine($"Failure: {row.FailureReason}");
        }

        if (!string.IsNullOrWhiteSpace(row.SuggestedIntervention))
        {
            sb.AppendLine();
            sb.AppendLine("Suggested intervention:");
            sb.AppendLine(row.SuggestedIntervention);
        }

        if (recoveryContext is not null)
        {
            AppendRecoveryContext(sb, recoveryContext);
        }

        sb.AppendLine();
        sb.AppendLine($"Last attempt: {FormatWhen(row.LastDownloadTime)}");
        sb.AppendLine();
        sb.AppendLine("Sent from GFH MIP Download Monitor status email.");
        return sb.ToString().TrimEnd();
    }

    public static string BuildInformAdminBodyForRecoveryFollowUp(
        string sourceName,
        DateOnly monitorDate,
        string statusLabel,
        string? resultSummary,
        string? successfulOptionTitle,
        int suggestionsTried,
        IReadOnlyList<(string Step, string Detail, DateTimeOffset Timestamp)> timeline)
    {
        var sb = new StringBuilder();
        sb.AppendLine("GFH Media Intelligence — automatic AI recovery failed");
        sb.AppendLine();
        sb.AppendLine($"Monitor date: {monitorDate:yyyy-MM-dd}");
        sb.AppendLine($"Source: {sourceName}");
        sb.AppendLine($"Outcome: {statusLabel}");
        if (!string.IsNullOrWhiteSpace(resultSummary))
        {
            sb.AppendLine($"Summary: {resultSummary}");
        }

        if (!string.IsNullOrWhiteSpace(successfulOptionTitle))
        {
            sb.AppendLine($"Last successful fix: {successfulOptionTitle}");
        }

        sb.AppendLine($"Suggestions tried: {suggestionsTried}");
        if (timeline.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Actions tried:");
            foreach (var step in timeline)
            {
                sb.AppendLine($"- {step.Step}: {step.Detail} ({MipDisplayTimeZone.Format(step.Timestamp)})");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Sent from GFH MIP Download Monitor recovery follow-up email.");
        return sb.ToString().TrimEnd();
    }

    private static void AppendRecoveryContext(StringBuilder sb, DownloadMonitorEmailFailureContext recoveryContext)
    {
        if (!string.IsNullOrWhiteSpace(recoveryContext.RecoverySummary))
        {
            sb.AppendLine();
            sb.AppendLine("Auto AI recovery:");
            sb.AppendLine(recoveryContext.RecoverySummary);
        }

        if (recoveryContext.TimelineSteps.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("AI suggestions tried:");
            foreach (var step in recoveryContext.TimelineSteps)
            {
                sb.AppendLine($"- {step.Step}: {step.Detail} ({MipDisplayTimeZone.Format(step.Timestamp)})");
            }
        }
    }

    private static string FormatWhen(DateTimeOffset? value) =>
        value is null ? "never" : MipDisplayTimeZone.Format(value);
}

public sealed record DownloadMonitorEmailFailureContext(
    string? RecoverySummary,
    IReadOnlyList<DownloadMonitorEmailTimelineStep> TimelineSteps);

public sealed record DownloadMonitorEmailTimelineStep(
    string Step,
    string Detail,
    DateTimeOffset Timestamp);

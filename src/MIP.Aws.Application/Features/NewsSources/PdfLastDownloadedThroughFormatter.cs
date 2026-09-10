using MIP.Aws.Application.Time;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Application.Features.NewsSources;

/// <summary>Display labels for PDF management "Last downloaded Through".</summary>
public static class PdfLastDownloadedThroughFormatter
{
    public static string? Format(
        DownloadJobTrigger? trigger,
        DownloadJobStatus? jobStatus,
        PdfDiscoveryMethod? discoveryMethod,
        string? uploadedFileName,
        DateTimeOffset? downloadedAt,
        string? displayTimeZoneId = null)
    {
        if (trigger is null && downloadedAt is null)
        {
            return null;
        }

        if (IsPad(trigger, discoveryMethod, uploadedFileName))
        {
            return downloadedAt is null
                ? "PAD"
                : $"PAD on {MipDisplayTimeZone.Format(downloadedAt.Value, "yyyy-MM-dd HH:mm", displayTimeZoneId)}";
        }

        if (jobStatus is DownloadJobStatus.SuccessWithAutoAiRecovery
            || trigger is DownloadJobTrigger.AutoAiRecovery
                or DownloadJobTrigger.Recovery
                or DownloadJobTrigger.Retry)
        {
            return "MIP Auto recovery";
        }

        return trigger switch
        {
            DownloadJobTrigger.Scheduled => "MIP batch",
            DownloadJobTrigger.Manual => "MIP Manual",
            DownloadJobTrigger.ExternalRpa => downloadedAt is null
                ? "PAD"
                : $"PAD on {MipDisplayTimeZone.Format(downloadedAt.Value, "yyyy-MM-dd HH:mm", displayTimeZoneId)}",
            _ => downloadedAt is null ? null : "MIP Manual"
        };
    }

    /// <summary>
    /// PAD/RPA ingest uses <see cref="DownloadJobTrigger.ExternalRpa"/>. Older successful PAD uploads
    /// were stored as manual file uploads of INAF_*.pdf with ManualOverride discovery.
    /// </summary>
    private static bool IsPad(
        DownloadJobTrigger? trigger,
        PdfDiscoveryMethod? discoveryMethod,
        string? uploadedFileName)
    {
        if (trigger == DownloadJobTrigger.ExternalRpa)
        {
            return true;
        }

        if (trigger != DownloadJobTrigger.Manual
            || discoveryMethod != PdfDiscoveryMethod.ManualOverride)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(uploadedFileName)
               && uploadedFileName.StartsWith("INAF_", StringComparison.OrdinalIgnoreCase);
    }
}

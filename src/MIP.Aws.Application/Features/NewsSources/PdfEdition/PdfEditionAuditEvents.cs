namespace MIP.Aws.Application.Features.NewsSources.PdfEdition;

/// <summary>
/// Immutable audit action names for public PDF edition discovery and download.
/// </summary>
public static class PdfEditionAuditEvents
{
    public const string DiscoveryStarted = "pdf.discovery.started";
    public const string DiscoveryNoPdfFound = "pdf.discovery.no.pdf.found";
    public const string CandidateFound = "pdf.candidate.found";
    public const string CandidateValidated = "pdf.candidate.validated";
    public const string DownloadStarted = "pdf.download.started";
    public const string DownloadCompleted = "pdf.download.completed";
    public const string DownloadFailed = "pdf.download.failed";
    public const string DownloadBlockedByCompliance = "pdf.download.blocked.compliance";
    public const string ViewedByUser = "pdf.viewed";
    public const string ManualOverrideUsed = "pdf.manual.override";
    public const string ManualActionNotificationSent = "pdf.manual.action.notification.sent";
}

namespace MIP.Aws.Application.Portal;

/// <summary>Audit event kinds for licensed PressReader portal automation.</summary>
public static class PressReaderPortalAuditEvents
{
    public const string LoginStarted = "PressReaderLoginStarted";
    public const string LoginCompleted = "PressReaderLoginCompleted";
    public const string LoginFailed = "PressReaderLoginFailed";
    public const string DownloadStarted = "PressReaderDownloadStarted";
    public const string DownloadCompleted = "PressReaderDownloadCompleted";
    public const string DownloadFailed = "PressReaderDownloadFailed";
    public const string DownloadBlockedByCompliance = "PressReaderDownloadBlockedByCompliance";
    public const string PdfViewedByUser = "PdfViewedByUser";
}

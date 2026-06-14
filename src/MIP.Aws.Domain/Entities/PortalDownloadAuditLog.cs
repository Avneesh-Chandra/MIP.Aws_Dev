using MIP.Aws.Domain.Common;

namespace MIP.Aws.Domain.Entities;

/// <summary>
/// Immutable-style audit trail for licensed portal login attempts, navigation, and downloads.
/// </summary>
public class PortalDownloadAuditLog : AuditableEntity
{
    public Guid NewsSourceId { get; set; }

    public NewsSource NewsSource { get; set; } = null!;

    public Guid? DownloadJobId { get; set; }

    public DownloadJob? DownloadJob { get; set; }

    /// <summary>Machine-oriented event code, e.g. LoginAttempt, LoginSuccess, CaptchaDetected.</summary>
    public string EventKind { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? FailureCode { get; set; }

    public string? ScreenshotRelativePath { get; set; }

    public string? HtmlSnapshotRelativePath { get; set; }

    public int? HttpStatus { get; set; }
}

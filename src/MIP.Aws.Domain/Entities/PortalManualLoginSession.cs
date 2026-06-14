using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities;

/// <summary>
/// Tracks a single operator-driven, manual-assisted login session for an OTP/MFA-gated
/// licensed subscriber portal (e.g. Indian-language newspapers that authenticate users
/// with a mobile-number + SMS OTP).
/// </summary>
/// <remarks>
/// COMPLIANCE INVARIANTS (enforced at construction and in handlers):
/// <list type="bullet">
///   <item>The platform NEVER stores the OTP value, SMS message, or any plaintext
///   second factor on this row.</item>
///   <item><see cref="SessionArtifactRelativePath"/> points to an encrypted Playwright
///   storage-state blob produced AFTER the operator authenticated. It is the operator's
///   browser cookies/localStorage — not the OTP itself.</item>
///   <item><see cref="StartedByUserId"/> and <see cref="StartedByEmail"/> identify the
///   GFH operator who initiated and completed the assisted flow for audit.</item>
///   <item>Status transitions are append-only via the application-layer command handlers,
///   so the table doubles as a tamper-evident audit subject (the immutable
///   <c>PortalDownloadAuditLog</c> rows reference this row via <c>NewsSourceId</c>).</item>
/// </list>
/// </remarks>
public class PortalManualLoginSession : AuditableEntity
{
    public Guid NewsSourceId { get; set; }

    public NewsSource NewsSource { get; set; } = null!;

    /// <summary>Identity user-id of the operator who started the assisted login.</summary>
    public Guid? StartedByUserId { get; set; }

    /// <summary>Snapshot of the operator email at the time the session was opened (for audit).</summary>
    public string? StartedByEmail { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>UTC instant after which the captured session state is treated as stale and is rejected for downloads.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    public AssistedLoginSessionStatus Status { get; set; } = AssistedLoginSessionStatus.Pending;

    /// <summary>Relative storage path of the DataProtection-encrypted Playwright storage state captured after the operator completed login. Null until <see cref="AssistedLoginSessionStatus.LoggedIn"/>.</summary>
    public string? SessionArtifactRelativePath { get; set; }

    /// <summary>Machine-oriented failure code (e.g. <c>HeadedBrowserUnavailable</c>, <c>SessionExpired</c>).</summary>
    public string? FailureCode { get; set; }

    /// <summary>Human-readable failure reason for operator UI.</summary>
    public string? FailureReason { get; set; }

    /// <summary>Optional operator notes recorded at completion time (e.g. publisher edition codes).</summary>
    public string? Notes { get; set; }
}

namespace MIP.Aws.Domain.Enums;

/// <summary>
/// Lifecycle of an operator-driven assisted-login session against an OTP/MFA-gated portal.
/// </summary>
/// <remarks>
/// The status NEVER reflects the user's actual OTP value. Each transition is recorded as an
/// immutable <c>PortalDownloadAuditLog</c> entry.
/// </remarks>
public enum AssistedLoginSessionStatus
{
    /// <summary>Row created; headed browser is being launched.</summary>
    Pending = 1,

    /// <summary>Browser is open at the LoginUrl; operator must enter credentials + OTP.</summary>
    AwaitingUser = 2,

    /// <summary>Operator confirmed login; storage state captured and encrypted.</summary>
    LoggedIn = 3,

    /// <summary>A download is currently replaying the captured session.</summary>
    DownloadInProgress = 4,

    /// <summary>Download completed successfully against the assisted session.</summary>
    Completed = 5,

    /// <summary>Session passed the configured timeout window before being used for download.</summary>
    Expired = 6,

    /// <summary>Operator or system aborted the session (e.g. OTP failure, browser closed).</summary>
    Failed = 7,

    /// <summary>Operator explicitly cancelled the assisted session.</summary>
    Cancelled = 8
}

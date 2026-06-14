namespace MIP.Aws.Domain.Enums;

public enum SourceIngestionAlertType
{
    CaptchaDetected = 1,
    MfaDetected = 2,
    SelectorFailure = 3,
    DownloadControlMissing = 4,
    LayoutChanged = 5,
    SessionTimeout = 6,
    InvalidCredentials = 7,

    /// <summary>
    /// The source is gated by SMS/e-mail/authenticator OTP and unattended automation was
    /// (correctly) refused. An authorized operator must complete an assisted-login session
    /// before download can proceed.
    /// </summary>
    OtpRequired = 8,

    /// <summary>
    /// An assisted-login session expired before download could complete; operator must
    /// restart an assisted-login session to refresh the stored Playwright state.
    /// </summary>
    AssistedSessionExpired = 9,

    Other = 99
}

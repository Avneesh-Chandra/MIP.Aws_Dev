namespace MIP.Aws.Domain.Security;

/// <summary>Canonical failure type labels used by AI source recovery analysis.</summary>
public static class SourceRecoveryFailureTypes
{
    public const string LoginIconNotFound = "LoginIconNotFound";
    public const string LoginFormChanged = "LoginFormChanged";
    public const string UsernameFieldNotFound = "UsernameFieldNotFound";
    public const string PasswordFieldNotFound = "PasswordFieldNotFound";
    public const string SubmitButtonNotFound = "SubmitButtonNotFound";
    public const string CaptchaDetected = "CaptchaDetected";
    public const string MfaDetected = "MfaDetected";
    public const string DownloadButtonNotFound = "DownloadButtonNotFound";
    public const string PdfLinkNotFound = "PdfLinkNotFound";
    public const string RightClickMenuChanged = "RightClickMenuChanged";
    public const string PdfValidationFailed = "PdfValidationFailed";
    public const string PageLayoutChanged = "PageLayoutChanged";
    public const string SelectorMismatch = "SelectorMismatch";
    public const string Timeout = "Timeout";
    public const string AccessDenied = "AccessDenied";
    public const string UnexpectedPopup = "UnexpectedPopup";
    public const string PortalUpgradeDetected = "PortalUpgradeDetected";
    public const string UnknownFailure = "UnknownFailure";

    public static IReadOnlyList<string> All { get; } =
    [
        LoginIconNotFound, LoginFormChanged, UsernameFieldNotFound, PasswordFieldNotFound,
        SubmitButtonNotFound, CaptchaDetected, MfaDetected, DownloadButtonNotFound,
        PdfLinkNotFound, RightClickMenuChanged, PdfValidationFailed, PageLayoutChanged,
        SelectorMismatch, Timeout, AccessDenied, UnexpectedPopup, PortalUpgradeDetected,
        UnknownFailure
    ];
}

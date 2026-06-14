using MIP.Aws.Domain.Security;

namespace MIP.Aws.Application.Features.SourceRecovery;

public static class SourceRecoveryFailureTypeMapper
{
    public static string Map(string? failureCode, string? errorMessage)
    {
        var code = failureCode?.Trim() ?? string.Empty;
        var msg = errorMessage ?? string.Empty;

        return code switch
        {
            "LoginIconSelectorMissing" or "LoginUrlMissing" => SourceRecoveryFailureTypes.LoginIconNotFound,
            "SelectorsMissing" or "UsernameSelectorMissing" => SourceRecoveryFailureTypes.UsernameFieldNotFound,
            "PasswordSelectorMissing" => SourceRecoveryFailureTypes.PasswordFieldNotFound,
            "SubmitSelectorMissing" => SourceRecoveryFailureTypes.SubmitButtonNotFound,
            "RequiresCaptcha" or "CaptchaOnLoginPage" or "CaptchaOrMfa" => SourceRecoveryFailureTypes.CaptchaDetected,
            "MfaOnLoginPage" or "ManualAssistedRequired" or "RequiresManualAction" => SourceRecoveryFailureTypes.MfaDetected,
            "DownloadSelectorMissing" or "DownloadButtonNotFound" or "DownloadMenuNotFound" => SourceRecoveryFailureTypes.DownloadButtonNotFound,
            "ContextMenuNotFound" or "DownloadSubmenuNotFound" => SourceRecoveryFailureTypes.RightClickMenuChanged,
            "PdfValidationFailed" or "NotPdf" => SourceRecoveryFailureTypes.PdfValidationFailed,
            "SelectorTimeout" or "SelectorNotFound" or "SelectorMismatch" => SourceRecoveryFailureTypes.SelectorMismatch,
            "DownloadTimeout" or "NetworkTimeout" => SourceRecoveryFailureTypes.Timeout,
            "AccessDenied" or "InvalidCredentials" or "CredentialsNeedReEntry" => SourceRecoveryFailureTypes.AccessDenied,
            "NavigationFailed" => SourceRecoveryFailureTypes.PageLayoutChanged,
            "PortalUpgradeDetected" => SourceRecoveryFailureTypes.PortalUpgradeDetected,
            _ when msg.Contains("captcha", StringComparison.OrdinalIgnoreCase) => SourceRecoveryFailureTypes.CaptchaDetected,
            _ when msg.Contains("mfa", StringComparison.OrdinalIgnoreCase) || msg.Contains("otp", StringComparison.OrdinalIgnoreCase) => SourceRecoveryFailureTypes.MfaDetected,
            _ when msg.Contains("timeout", StringComparison.OrdinalIgnoreCase) => SourceRecoveryFailureTypes.Timeout,
            _ when msg.Contains("selector", StringComparison.OrdinalIgnoreCase) => SourceRecoveryFailureTypes.SelectorMismatch,
            _ when msg.Contains("download", StringComparison.OrdinalIgnoreCase) && msg.Contains("not found", StringComparison.OrdinalIgnoreCase) => SourceRecoveryFailureTypes.DownloadButtonNotFound,
            _ when msg.Contains("blocked", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("cloudflare", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("bot protection", StringComparison.OrdinalIgnoreCase) => SourceRecoveryFailureTypes.AccessDenied,
            _ when msg.Contains("No public PDF edition option", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("No Al Ayam full-edition PDF", StringComparison.OrdinalIgnoreCase) => SourceRecoveryFailureTypes.PdfLinkNotFound,
            _ when msg.Contains("Response appears to be HTML", StringComparison.OrdinalIgnoreCase) => SourceRecoveryFailureTypes.PdfValidationFailed,
            _ when msg.Contains("not a valid PDF", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("Missing PDF magic", StringComparison.OrdinalIgnoreCase) => SourceRecoveryFailureTypes.PdfValidationFailed,
            _ when msg.Contains("pdf", StringComparison.OrdinalIgnoreCase) => SourceRecoveryFailureTypes.PdfValidationFailed,
            _ when msg.Contains("Al Ayam", StringComparison.OrdinalIgnoreCase) => SourceRecoveryFailureTypes.SelectorMismatch,
            _ when msg.Contains("epaper-recovery-test-broken", StringComparison.OrdinalIgnoreCase) => SourceRecoveryFailureTypes.PageLayoutChanged,
            _ => SourceRecoveryFailureTypes.UnknownFailure
        };
    }
}

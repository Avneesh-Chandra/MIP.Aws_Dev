namespace MIP.Aws.Application.Features.Operator;

/// <summary>Maps portal/PDF failure codes to operator-facing manual intervention guidance.</summary>
public static class ManualInterventionSuggestions
{
    public static string GetSuggestion(string? failureCode, string? failureMessage, bool requiresManualAction, bool complianceBlocked)
    {
        if (complianceBlocked)
        {
            return "Admin must review IsDownloadAllowed and publisher licensing approval.";
        }

        if (requiresManualAction)
        {
            return "Source is flagged for manual action. Admin should review portal login or CAPTCHA/MFA requirements.";
        }

        if (!string.IsNullOrWhiteSpace(failureCode))
        {
            var suggestion = failureCode.Trim() switch
            {
                "LoginIconNotFound" => "Website layout may have changed. Admin should review login icon selector.",
                "InvalidCredentials" or "MissingCredentials" or "CredentialsNeedReEntry" =>
                    "Admin should verify username/password stored for this source.",
                "CaptchaDetected" or "RequiresCaptcha" or "CaptchaOrMfa" =>
                    "Manual login is required. Admin should review source and mark RequiresManualAction.",
                "MfaDetected" or "RequiresMfa" or "RequiresOtp" =>
                    "OTP/MFA required. Admin should initiate assisted login workflow.",
                "DownloadButtonNotFound" or "DownloadSelectorMissing" or "DownloadMenuNotFound" or "ContextMenuNotFound" =>
                    "Admin should verify PDF/download selector or use AI selector suggestion.",
                "PdfValidationFailed" or "InvalidPdf" =>
                    "Downloaded file was not a valid PDF. Admin should inspect source page and validation logs.",
                "ComplianceBlocked" or "DownloadNotAllowed" =>
                    "Admin must review IsDownloadAllowed and publisher licensing approval.",
                "NoPublicPdfAvailable" =>
                    "No public PDF was found. Use Public HTML article extraction instead.",
                "NetworkTimeout" or "Timeout" or "NavigationTimeout" =>
                    "Retry later or check internet/source availability.",
                "LoginUrlMissing" or "SelectorsMissing" or "EditionUrlMissing" =>
                    "Admin should verify portal URLs and CSS selectors in source settings.",
                "RequiresManualAction" =>
                    "Prior automation failure set RequiresManualAction. Admin should resolve alerts before retrying.",
                _ => null
            };

            if (suggestion is not null)
            {
                return suggestion;
            }
        }

        if (!string.IsNullOrWhiteSpace(failureMessage))
        {
            if (failureMessage.Contains("CAPTCHA", StringComparison.OrdinalIgnoreCase)
                || failureMessage.Contains("MFA", StringComparison.OrdinalIgnoreCase))
            {
                return "Manual login is required. Admin should review source and mark RequiresManualAction.";
            }

            if (failureMessage.Contains("compliance", StringComparison.OrdinalIgnoreCase)
                || failureMessage.Contains("not allowed", StringComparison.OrdinalIgnoreCase))
            {
                return "Admin must review IsDownloadAllowed and publisher licensing approval.";
            }

            if (failureMessage.Contains("credential", StringComparison.OrdinalIgnoreCase)
                || failureMessage.Contains("password", StringComparison.OrdinalIgnoreCase))
            {
                return "Admin should verify username/password stored for this source.";
            }

            if (failureMessage.Contains("PDF", StringComparison.OrdinalIgnoreCase)
                && failureMessage.Contains("valid", StringComparison.OrdinalIgnoreCase))
            {
                return "Downloaded file was not a valid PDF. Admin should inspect source page and validation logs.";
            }
        }

        return "Review source configuration and recent portal/PDF audit logs. Escalate to Admin if issue persists.";
    }

    public static bool RequiresManualIntervention(
        string? statusLabel,
        string? failureCode,
        bool requiresManualAction,
        bool complianceBlocked) =>
        requiresManualAction
        || complianceBlocked
        || statusLabel is DownloadMonitorStatusLabels.Failed
            or DownloadMonitorStatusLabels.ManualActionRequired
            or DownloadMonitorStatusLabels.ComplianceBlocked
            or DownloadMonitorStatusLabels.NoPdfAvailable;
}

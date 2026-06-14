using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.SourceRecovery;

namespace MIP.Aws.Application.Features.AutoAiRecovery;

/// <summary>Validates AI recovery patches against the auto-recovery allowlist.</summary>
public static class AutoAiRecoveryPatchValidator
{
    private static readonly HashSet<string> AllowedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(SourceRecoveryConfigurationPatchDto.PdfDownloadSelector),
        nameof(SourceRecoveryConfigurationPatchDto.PdfLinkSelector),
        nameof(SourceRecoveryConfigurationPatchDto.LoginIconSelector),
        nameof(SourceRecoveryConfigurationPatchDto.SubmitSelector),
        nameof(SourceRecoveryConfigurationPatchDto.DownloadMenuItemSelector),
        nameof(SourceRecoveryConfigurationPatchDto.NewspaperCanvasSelector),
        nameof(SourceRecoveryConfigurationPatchDto.DownloadSelector),
        nameof(SourceRecoveryConfigurationPatchDto.DownloadWaitTimeoutSeconds),
        nameof(SourceRecoveryConfigurationPatchDto.UseHeadlessBrowser),
        nameof(SourceRecoveryConfigurationPatchDto.PdfDiscoveryPageUrl),
        nameof(SourceRecoveryConfigurationPatchDto.EditionUrl),
        nameof(SourceRecoveryConfigurationPatchDto.BaseUrl),
        "WaitSelector",
        "PopupWaitRule",
        "InteractionSequence",
        "RetryDelaySeconds"
    };

    private static readonly HashSet<string> ForbiddenFields = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(SourceRecoveryConfigurationPatchDto.UsernameSelector),
        nameof(SourceRecoveryConfigurationPatchDto.PasswordSelector),
        "Credentials",
        "OAuth",
        "IsDownloadAllowed",
        "RequiresLogin",
        "RequiresMfa",
        "RequiresCaptcha"
    };

    public static bool IsPatchSafe(SourceRecoveryConfigurationPatchDto patch, out IReadOnlyList<string> rejectedFields)
    {
        var rejected = new List<string>();
        foreach (var field in GetSetFields(patch))
        {
            if (ForbiddenFields.Contains(field))
            {
                rejected.Add(field);
                continue;
            }

            if (!AllowedFields.Contains(field))
            {
                rejected.Add(field);
            }
        }

        rejectedFields = rejected;
        return rejected.Count == 0;
    }

    public static bool IsOptionSafeForAutoApply(
        SourceRecoveryOptionDto option,
        AutoAiDownloadRecoveryOptions settings,
        out string? rejectReason)
    {
        if (!IsPatchSafe(option.Patch, out var rejected))
        {
            rejectReason = $"RejectedUnsafeSuggestion: {string.Join(", ", rejected)}";
            return false;
        }

        if (option.ConfidenceScore < settings.MinimumConfidence * 100)
        {
            rejectReason = $"Confidence {option.ConfidenceScore}% below minimum {settings.MinimumConfidence * 100:0}%.";
            return false;
        }

        var maxRisk = ParseRisk(settings.MaximumRiskAllowed);
        if (option.RiskLevel > maxRisk)
        {
            rejectReason = $"Risk {option.RiskLevel} exceeds maximum {maxRisk}.";
            return false;
        }

        if (option.RiskLevel == Domain.Enums.SourceRecoveryRiskLevel.Medium
            && settings.RequireHumanApprovalForMediumRisk)
        {
            rejectReason = "Medium risk requires human approval.";
            return false;
        }

        if (option.RiskLevel == Domain.Enums.SourceRecoveryRiskLevel.High)
        {
            rejectReason = "High risk suggestions are never auto-applied.";
            return false;
        }

        rejectReason = null;
        return true;
    }

    private static Domain.Enums.SourceRecoveryRiskLevel ParseRisk(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "low" => Domain.Enums.SourceRecoveryRiskLevel.Low,
            "high" => Domain.Enums.SourceRecoveryRiskLevel.High,
            _ => Domain.Enums.SourceRecoveryRiskLevel.Medium
        };

    private static IEnumerable<string> GetSetFields(SourceRecoveryConfigurationPatchDto patch)
    {
        if (patch.UsernameSelector is not null) yield return nameof(patch.UsernameSelector);
        if (patch.PasswordSelector is not null) yield return nameof(patch.PasswordSelector);
        if (patch.SubmitSelector is not null) yield return nameof(patch.SubmitSelector);
        if (patch.DownloadSelector is not null) yield return nameof(patch.DownloadSelector);
        if (patch.LoginIconSelector is not null) yield return nameof(patch.LoginIconSelector);
        if (patch.NewspaperCanvasSelector is not null) yield return nameof(patch.NewspaperCanvasSelector);
        if (patch.ContextMenuSelector is not null) yield return nameof(patch.ContextMenuSelector);
        if (patch.DownloadMenuItemSelector is not null) yield return nameof(patch.DownloadMenuItemSelector);
        if (patch.LoginSuccessSelector is not null) yield return nameof(patch.LoginSuccessSelector);
        if (patch.SuccessUrlPattern is not null) yield return nameof(patch.SuccessUrlPattern);
        if (patch.PdfDownloadSelector is not null) yield return nameof(patch.PdfDownloadSelector);
        if (patch.PdfLinkSelector is not null) yield return nameof(patch.PdfLinkSelector);
        if (patch.BaseUrl is not null) yield return nameof(patch.BaseUrl);
        if (patch.EditionUrl is not null) yield return nameof(patch.EditionUrl);
        if (patch.PdfDiscoveryPageUrl is not null) yield return nameof(patch.PdfDiscoveryPageUrl);
        if (patch.DownloadWaitTimeoutSeconds is not null) yield return nameof(patch.DownloadWaitTimeoutSeconds);
        if (patch.UseHeadlessBrowser is not null) yield return nameof(patch.UseHeadlessBrowser);
    }
}

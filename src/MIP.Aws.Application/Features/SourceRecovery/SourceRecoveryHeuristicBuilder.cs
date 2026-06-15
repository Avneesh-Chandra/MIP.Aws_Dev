using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Domain.Security;

namespace MIP.Aws.Application.Features.SourceRecovery;

/// <summary>Deterministic recovery options when AI is disabled or unavailable.</summary>
public static class SourceRecoveryHeuristicBuilder
{
    public static IReadOnlyList<SourceRecoveryOptionDto> BuildOptions(SourceRecoveryAnalysisContext context)
    {
        if (IsAlAyamPublicPdf(context))
        {
            return [BuildAlAyamHeuristicOption(context)];
        }

        if (IsAawsatPublicPdf(context))
        {
            return [BuildAawsatHeuristicOption()];
        }

        var current = SourceRecoveryConfigurationSnapshot.FromJson(context.CurrentConfigurationJson)
                      ?? new SourceRecoveryConfigurationSnapshot(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 180, true);

        var patch = context.FailureType switch
        {
            _ when context.FailureType.Contains("Login", StringComparison.OrdinalIgnoreCase) => current.ToPatch() with
            {
                LoginIconSelector = "button:has-text('Sign In'), button:has-text('تسجيل الدخول')",
                SubmitSelector = "button[type='submit'], [role='dialog'] button:has-text('تسجيل الدخول')"
            },
            _ when context.FailureType.Contains("PdfValidation", StringComparison.OrdinalIgnoreCase)
                   || context.FailureMessage.Contains("HTML", StringComparison.OrdinalIgnoreCase) => current.ToPatch() with
            {
                PdfDownloadSelector = current.PdfDownloadSelector ?? "button[aria-label='Download']",
                PdfLinkSelector = current.PdfLinkSelector ?? "a:has-text('Full Publication')",
                DownloadWaitTimeoutSeconds = Math.Max(current.DownloadWaitTimeoutSeconds, 240)
            },
            _ when context.FailureType.Contains("Download", StringComparison.OrdinalIgnoreCase)
                   || context.FailureType.Contains("RightClick", StringComparison.OrdinalIgnoreCase) => current.ToPatch() with
            {
                DownloadMenuItemSelector = "button:has-text('تنزيل'), button:has-text('Download')",
                ContextMenuSelector = "[class*='page-actions'] button, [class*='toolbar'] button",
                NewspaperCanvasSelector = "[class*='issue-page'], [class*='page-image']",
                DownloadWaitTimeoutSeconds = Math.Max(current.DownloadWaitTimeoutSeconds, 240)
            },
            _ => current.ToPatch() with { DownloadWaitTimeoutSeconds = Math.Max(current.DownloadWaitTimeoutSeconds, 240) }
        };

        return
        [
            new SourceRecoveryOptionDto(
                0,
                "Heuristic selector refresh",
                "Refresh portal selectors using GFH recovery heuristics when AI is unavailable.",
                "Update login/download selectors and extend download wait timeout.",
                65,
                60,
                SourceRecoveryRiskLevel.Medium,
                ["DownloadMenuItemSelector", "DownloadWaitTimeoutSeconds"],
                ["login", "download"],
                patch,
                [])
        ];
    }

    public static IReadOnlyList<SourceRecoveryOptionDto> MergePublisherHeuristics(
        SourceRecoveryAnalysisContext context,
        IReadOnlyList<SourceRecoveryOptionDto> aiOptions)
    {
        SourceRecoveryOptionDto? publisherOption = null;
        if (IsAlAyamPublicPdf(context))
        {
            publisherOption = BuildAlAyamHeuristicOption(context);
        }
        else if (IsAawsatPublicPdf(context))
        {
            publisherOption = BuildAawsatHeuristicOption();
        }

        if (publisherOption is null)
        {
            return aiOptions;
        }

        var merged = new List<SourceRecoveryOptionDto> { publisherOption };
        var index = 1;
        foreach (var option in aiOptions)
        {
            merged.Add(option with { OptionIndex = index++ });
        }

        return merged;
    }

    private static bool IsAlAyamPublicPdf(SourceRecoveryAnalysisContext context) =>
        context.SourceName.Contains(AlAyamPublicPdfBaseline.SourceName, StringComparison.OrdinalIgnoreCase)
        || context.SourceUrl?.Contains("alayam.com", StringComparison.OrdinalIgnoreCase) == true
        || context.EditionUrl?.Contains("alayam.com", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsAawsatPublicPdf(SourceRecoveryAnalysisContext context) =>
        context.SourceName.Contains(AawsatPublicPdfBaseline.SourceName, StringComparison.OrdinalIgnoreCase)
        || context.SourceUrl?.Contains("aawsat.com", StringComparison.OrdinalIgnoreCase) == true
        || context.EditionUrl?.Contains("aawsat.com", StringComparison.OrdinalIgnoreCase) == true
        || context.FailureMessage.Contains("aawsat.com", StringComparison.OrdinalIgnoreCase);

    private static SourceRecoveryOptionDto BuildAlAyamHeuristicOption(SourceRecoveryAnalysisContext? context = null)
    {
        var blocked = IsPublisherBlockedFailure(context);
        return new SourceRecoveryOptionDto(
            0,
            blocked
                ? "Restore Al Ayam e-paper and retry via Playwright"
                : "Restore Al Ayam e-paper PDF link selector and page URL",
            blocked
                ? "Reset Al Ayam to the known-good e-paper page, embedded viewer Save button, and all-pages link, then retry through Playwright instead of blocked HTTP HTML."
                : "Reset Al Ayam PDF discovery to the e-paper viewer Save button and all-pages link selectors.",
            blocked
                ? "Restores PdfLinkSelector, BaseUrl, EditionUrl, PdfDiscoveryPageUrl, and UseHeadlessBrowser for anti-block fetch."
                : "Restores PdfLinkSelector, BaseUrl, EditionUrl, and PdfDiscoveryPageUrl for edition discovery.",
            92,
            blocked ? 90 : 88,
            SourceRecoveryRiskLevel.Low,
            blocked
                ? ["PdfLinkSelector", "BaseUrl", "EditionUrl", "PdfDiscoveryPageUrl", "UseHeadlessBrowser"]
                : ["PdfLinkSelector", "BaseUrl", "EditionUrl", "PdfDiscoveryPageUrl"],
            ["discover", "download"],
            AlAyamPublicPdfBaseline.RecoveryPatch(),
            []);
    }

    private static bool IsPublisherBlockedFailure(SourceRecoveryAnalysisContext? context) =>
        context is not null
        && (context.FailureType == SourceRecoveryFailureTypes.AccessDenied
            || context.FailureType == SourceRecoveryFailureTypes.PdfLinkNotFound
            || context.FailureMessage.Contains("blocked", StringComparison.OrdinalIgnoreCase)
            || context.FailureMessage.Contains("cloudflare", StringComparison.OrdinalIgnoreCase)
            || context.FailureMessage.Contains("bot protection", StringComparison.OrdinalIgnoreCase));

    private static SourceRecoveryOptionDto BuildAawsatHeuristicOption() =>
        new(
            0,
            "Restore Asharq Al-Awsat Full Publication click path",
            "Reset PDF discovery to the known-good Asharq Al-Awsat flow: latest issue viewer → Download → Full Publication.",
            "Restores PdfDownloadSelector, PdfLinkSelector, BaseUrl, EditionUrl, and PdfDiscoveryPageUrl so Playwright can fetch the PDF instead of the HTML issue page.",
            90,
            85,
            SourceRecoveryRiskLevel.Low,
            ["PdfDownloadSelector", "PdfLinkSelector", "BaseUrl", "EditionUrl", "PdfDiscoveryPageUrl"],
            ["discover", "download"],
            AawsatPublicPdfBaseline.RecoveryPatch(),
            []);
}

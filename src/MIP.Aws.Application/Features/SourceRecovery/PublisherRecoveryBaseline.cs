using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Application.Features.SourceRecovery;

/// <summary>
/// Known-good publisher recovery patches that must stay active even when the immediate post-apply retry fails.
/// </summary>
public static class PublisherRecoveryBaseline
{
    public static bool ShouldRetainConfigAfterFailedRetry(NewsSource source, SourceRecoveryOptionDto? option)
    {
        if (option is null || option.RiskLevel != SourceRecoveryRiskLevel.Low)
        {
            return false;
        }

        return IsBaselineRecoveryPatch(source.ConnectorKey, option.Patch);
    }

    public static bool IsBaselineRecoveryPatch(string? connectorKey, SourceRecoveryConfigurationPatchDto patch)
    {
        if (string.Equals(connectorKey, AawsatPublicPdfBaseline.ConnectorKey, StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(patch.PdfDownloadSelector, AawsatPublicPdfBaseline.PdfDownloadSelector, StringComparison.Ordinal)
                   && string.Equals(patch.PdfLinkSelector, AawsatPublicPdfBaseline.PdfLinkSelector, StringComparison.Ordinal)
                   && string.Equals(patch.BaseUrl, AawsatPublicPdfBaseline.BaseUrl, StringComparison.OrdinalIgnoreCase)
                   && patch.UseHeadlessBrowser == true;
        }

        if (string.Equals(connectorKey, AlAyamPublicPdfBaseline.ConnectorKey, StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(patch.PdfLinkSelector, AlAyamPublicPdfBaseline.PdfLinkSelector, StringComparison.Ordinal)
                   && string.Equals(patch.BaseUrl, AlAyamPublicPdfBaseline.EpaperUrl, StringComparison.OrdinalIgnoreCase)
                   && patch.UseHeadlessBrowser == true;
        }

        return false;
    }
}

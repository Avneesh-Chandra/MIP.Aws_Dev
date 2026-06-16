using MIP.Aws.Application.Features.AutoAiRecovery;
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

        if (IsBaselineRecoveryPatch(source.ConnectorKey, option.Patch))
        {
            return true;
        }

        if (DarAlKhaleejPressReaderBaseline.IsPressReaderSource(
                source.ConnectorKey,
                source.PortalStrategyKey,
                source.EditionUrl,
                source.BaseUrl)
            && IsPressReaderOperationalPatch(option.Patch))
        {
            return true;
        }

        return false;
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

        if (string.Equals(connectorKey, DarAlKhaleejPressReaderBaseline.ConnectorKey, StringComparison.OrdinalIgnoreCase))
        {
            return MatchesPressReaderBaseline(patch);
        }

        return false;
    }

    public static bool IsPressReaderOperationalPatch(SourceRecoveryConfigurationPatchDto patch) =>
        AutoAiRecoveryPatchValidator.IsPatchSafe(patch, out _)
        && (patch.NewspaperCanvasSelector is not null
            || patch.ContextMenuSelector is not null
            || patch.DownloadMenuItemSelector is not null
            || patch.DownloadWaitTimeoutSeconds is not null);

    private static bool MatchesPressReaderBaseline(SourceRecoveryConfigurationPatchDto patch) =>
        string.Equals(patch.NewspaperCanvasSelector, DarAlKhaleejPressReaderBaseline.NewspaperCanvasSelector, StringComparison.Ordinal)
        && string.Equals(patch.ContextMenuSelector, DarAlKhaleejPressReaderBaseline.ContextMenuSelector, StringComparison.Ordinal)
        && string.Equals(patch.DownloadMenuItemSelector, DarAlKhaleejPressReaderBaseline.DownloadMenuItemSelector, StringComparison.Ordinal);
}

using MIP.Aws.Application.Features.SourceRecovery;
using MIP.Aws.Domain.Entities;

namespace MIP.Aws.Application.Features.NewsSources;

/// <summary>Known-good public PDF settings for Bahrain - Al Ayam (local recovery test baseline).</summary>
public static class AlAyamPublicPdfBaseline
{
    public const string SourceName = "Bahrain - Al Ayam";
    public const string ConnectorKey = "news.alayam";
    public const string EpaperUrl = "https://www.alayam.com/epaper";
    public const string PdfLinkSelector = "a#aPDFdownloadAllPages, a:has-text('كل الصفحات')";

    public static class Broken
    {
        public const string PdfLinkSelector = "a#brokenRecoveryTestLink, a:has-text('INVALID DOWNLOAD')";
        public const string EpaperUrl = "https://www.alayam.com/epaper-recovery-test-broken";
    }

    public static SourceRecoveryConfigurationPatchDto RecoveryPatch() => new(
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        PdfLinkSelector,
        EpaperUrl,
        EpaperUrl,
        EpaperUrl,
        180,
        true);

    public static bool IsSource(NewsSource source) =>
        string.Equals(source.ConnectorKey, ConnectorKey, StringComparison.OrdinalIgnoreCase)
        || source.Name.Contains(SourceName, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when the source already matches the known-good Al Ayam recovery baseline.</summary>
    public static bool HasKnownGoodConfiguration(NewsSource source) =>
        source.UseHeadlessBrowser
        && string.Equals(source.BaseUrl, EpaperUrl, StringComparison.OrdinalIgnoreCase)
        && string.Equals(source.EditionUrl, EpaperUrl, StringComparison.OrdinalIgnoreCase)
        && string.Equals(source.PdfDiscoveryPageUrl, EpaperUrl, StringComparison.OrdinalIgnoreCase)
        && string.Equals(source.PdfLinkSelector, PdfLinkSelector, StringComparison.Ordinal);
}
